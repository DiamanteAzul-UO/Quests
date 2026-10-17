/*****************************************************
 * QuestEventHandler.cs
 * Ponte entre os eventos do ServUO e o QuestManager
 *
 * Correcao central desta revisao:
 *   A versao anterior assinava apenas OnKilledBy, Movement e Login.
 *   OnItemCrafted, OnSkillUsed e OnSkillGain existiam no QuestManager mas
 *   NUNCA eram chamados por nada — objetivos Craft, UseSkill e SkillGain eram
 *   portanto impossiveis de concluir. UseItem, Destroy, Survive e Defend nem
 *   existiam como classe. Explore dependia de um evento que dispara ANTES de o
 *   jogador mudar de tile e nunca disparava em teleporte.
 *
 *   Aqui todos os tipos de objetivo tem uma fonte de evento real.
 *
 * Versao: 2.0.0
 * Compativel com: ServUO
 *****************************************************/

using System;
using System.Collections.Generic;
using Server;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Custom.QuestSystem.NPCs;
using Server.Engines.PartySystem;

namespace Server.Custom.QuestSystem.Quests
{
    public static class QuestEventHandler
    {
        private static bool _initialized = false;

        /// <summary>Distancia maxima para membro da party receber credito do kill.</summary>
        private const int PartyKillRange = 24;

        public static void Initialize()
        {
            if (_initialized)
                return;

            _initialized = true;

            // ── Combate ──────────────────────────────────────────────────────
            EventSink.OnKilledBy += OnKilledBy;

            // ── Coleta de itens ──────────────────────────────────────────────
            // OnItemObtained dispara para QUALQUER item que entra no inventario
            // do jogador (inclusive dentro de sub-containers). Substitui com
            // vantagem a troca forcada de mochila por QuestBackpack.
            EventSink.OnItemObtained += OnItemObtained;

            // ── Exploracao / regiao ──────────────────────────────────────────
            EventSink.Movement += OnMovement;
            EventSink.OnEnterRegion += OnEnterRegion;
            EventSink.TeleportMovement += OnTeleportMovement;

            // ── Craft ────────────────────────────────────────────────────────
            EventSink.CraftSuccess += OnCraftSuccess;

            // ── Skills ───────────────────────────────────────────────────────
            EventSink.SkillCheck += OnSkillCheck;
            EventSink.SkillGain += OnSkillGain;

            // ── Uso de item / interacao ──────────────────────────────────────
            EventSink.OnItemUse += OnItemUse;

            // ── Destruicao de objetos ────────────────────────────────────────
            // OnObjectDestroyed existia no QuestManager mas NUNCA era chamado por
            // nada, entao todo objetivo Destroy de item/estrutura era impossivel
            // de concluir (so o de criatura funcionava, via OnCreatureKilled).
            //
            // ItemDeleted nao informa o AUTOR, e Item.Delete() ja zerou o Map
            // quando o evento dispara — nao da para atribuir por proximidade.
            // A atribuicao vem do livro-razao de "ultimo toque": quem usou/
            // interagiu com o serial pouco antes de ele sumir recebe o credito.
            EventSink.ItemDeleted += OnItemDeleted;

            // ── Morte / sessao ───────────────────────────────────────────────
            EventSink.PlayerDeath += OnPlayerDeath;
            EventSink.Login += OnLogin;
            EventSink.Logout += OnLogout;

            Console.WriteLine("[QuestEventHandler] Eventos registrados: kill, item, movimento, regiao, " +
                              "teleporte, craft, skillcheck, skillgain, useitem, morte, login/logout.");
        }

        #region Login / Logout

        private static void OnLogin(LoginEventArgs e)
        {
            PlayerMobile pm = e.Mobile as PlayerMobile;
            if (pm == null)
                return;

            // Garante que exista PlayerQuestData para este serial
            QuestManager.Instance.GetPlayerData(pm);

            // Trocar a mochila do jogador por QuestBackpack e destrutivo
            // (deleta a mochila original) e hoje e desnecessario: OnItemObtained
            // cobre a coleta. Fica opt-in pela configuracao.
            if (QuestSystemConfig.Instance.UseQuestBackpack)
                EnsureQuestBackpack(pm);

            Timer.DelayCall(TimeSpan.FromSeconds(3.0), () =>
            {
                if (pm.Deleted || pm.NetState == null)
                    return;

                // Quests com prazo podem ter expirado enquanto offline
                QuestManager.Instance.CheckTimeLimits(pm);

                // Um [questreload pode ter mudado as definicoes enquanto o
                // jogador estava offline. ActiveQuest.Progress e um snapshot do
                // aceite, entao sem realinhar aqui a quest editada ficaria
                // impossivel de concluir.
                QuestManager.Instance.ReconcileProgress(pm);

                // O jogador pode ter logado dentro da area de um objetivo Explore
                QuestManager.Instance.OnLocationEntered(pm, pm.Location, pm.Map);

                // Retoma timers de Escolta/Defesa/Sobrevivencia que estavam ativos
                QuestObjectiveDrivers.ResumeForPlayer(QuestManager.Instance, pm);

                MystQuestObjectiveGump.MostrarQuestsAtivas(pm);
            });
        }

        private static void OnLogout(LogoutEventArgs e)
        {
            PlayerMobile pm = e.Mobile as PlayerMobile;
            if (pm == null)
                return;

            // Para timers de permanencia/sobrevivencia/escolta/defesa
            QuestObjectiveDrivers.StopForPlayer(pm);
        }

        /// <summary>
        /// Substitui a mochila do jogador por uma QuestBackpack.
        /// Mantido apenas para compatibilidade com shards que dependiam do
        /// comportamento antigo; desligado por padrao.
        /// </summary>
        private static void EnsureQuestBackpack(PlayerMobile player)
        {
            if (player == null || player.Deleted)
                return;

            if (player.Backpack is QuestBackpack)
                return;

            QuestBackpack novaMochila = new QuestBackpack();

            if (player.Backpack != null)
            {
                var itens = new List<Item>(player.Backpack.Items);

                foreach (Item item in itens)
                    novaMochila.DropItem(item);

                player.Backpack.Delete();
            }

            player.AddItem(novaMochila);

            Console.WriteLine($"[QuestEventHandler] Mochila substituida para {player.Name}.");
        }

        #endregion

        #region Kill — party e pet

        private static void OnKilledBy(OnKilledByEventArgs e)
        {
            if (e.Killed == null || e.KilledBy == null)
                return;

            bool isPet;
            PlayerMobile killer = ResolveKiller(e.KilledBy, out isPet);

            if (killer == null)
                return;

            Mobile victim = e.Killed;

            // Credito direto para o killer
            QuestManager.Instance.OnCreatureKilled(killer, victim, false, isPet);

            // Compartilha com membros da party proximos
            ShareKillWithParty(killer, victim, isPet);
        }

        /// <summary>
        /// Resolve o PlayerMobile responsavel pelo kill.
        /// Suporta: jogador direto, pet controlado e criatura summonada.
        /// </summary>
        private static PlayerMobile ResolveKiller(Mobile killedBy, out bool isPet)
        {
            isPet = false;

            PlayerMobile pm = killedBy as PlayerMobile;
            if (pm != null)
                return pm;

            BaseCreature bc = killedBy as BaseCreature;
            if (bc != null)
            {
                PlayerMobile owner = bc.ControlMaster as PlayerMobile;
                if (owner != null)
                {
                    isPet = true;
                    return owner;
                }

                PlayerMobile summoner = bc.SummonMaster as PlayerMobile;
                if (summoner != null)
                {
                    isPet = true;
                    return summoner;
                }
            }

            return null;
        }

        /// <summary>
        /// Compartilha o credito do kill com membros proximos da party.
        /// A flag CountPartyKills e verificada por objetivo dentro do QuestManager.
        /// </summary>
        private static void ShareKillWithParty(PlayerMobile killer, Mobile victim, bool isPet)
        {
            Party party = Party.Get(killer);

            if (party == null || party.Members.Count <= 1)
                return;

            foreach (PartyMemberInfo info in party.Members)
            {
                PlayerMobile member = info.Mobile as PlayerMobile;

                if (member == null || member == killer || member.Deleted)
                    continue;

                if (!member.Alive)
                    continue;

                if (member.Map != victim.Map)
                    continue;

                if (!member.InRange(victim.Location, PartyKillRange))
                    continue;

                QuestManager.Instance.OnCreatureKilled(member, victim, true, isPet);
            }
        }

        #endregion

        #region Coleta de itens

        private static void OnItemObtained(OnItemObtainedEventArgs e)
        {
            PlayerMobile pm = e.Mobile as PlayerMobile;
            if (pm == null || pm.Deleted)
                return;

            if (e.Item == null || e.Item.Deleted)
                return;

            QuestManager.Instance.OnItemCollected(pm, e.Item);
        }

        #endregion

        #region Movimento / regiao / teleporte

        private static void OnMovement(MovementEventArgs e)
        {
            PlayerMobile player = e.Mobile as PlayerMobile;
            if (player == null || player.Deleted)
                return;

            // O ServUO dispara Movement ANTES de commitar o novo tile
            // (Mobile.InternalOnMove -> EventSink.InvokeMovement), entao
            // player.Location ainda e a posicao antiga. Adiamos para o proximo
            // tick, quando o movimento ja foi aplicado — e so fazemos isso se o
            // jogador tiver de fato um objetivo Explore pendente.
            if (!QuestManager.Instance.HasPendingExploreObjective(player))
                return;

            Timer.DelayCall(TimeSpan.Zero, delegate
            {
                if (player.Deleted)
                    return;

                QuestManager.Instance.OnLocationEntered(player, player.Location, player.Map);
            });
        }

        private static void OnEnterRegion(OnEnterRegionEventArgs e)
        {
            PlayerMobile player = e.From as PlayerMobile;
            if (player == null || player.Deleted)
                return;

            QuestManager.Instance.OnRegionEntered(player, e.OldRegion, e.NewRegion);
        }

        private static void OnTeleportMovement(TeleportMovementEventArgs e)
        {
            PlayerMobile player = e.Mobile as PlayerMobile;
            if (player == null || player.Deleted)
                return;

            // Teleporte/moongate/recall nao dispara Movement — sem isto,
            // objetivos Explore alcancados por teleporte nunca completavam.
            QuestManager.Instance.OnLocationEntered(player, e.NewLocation, player.Map);
        }

        #endregion

        #region Craft

        private static void OnCraftSuccess(CraftSuccessEventArgs e)
        {
            PlayerMobile player = e.Crafter as PlayerMobile;
            if (player == null || player.Deleted)
                return;

            if (e.CraftedItem == null)
                return;

            string craftSystem = e.Tool != null ? e.Tool.GetType().Name : null;

            QuestManager.Instance.OnItemCrafted(player, e.CraftedItem, craftSystem);
        }

        #endregion

        #region Skills

        private static void OnSkillCheck(SkillCheckEventArgs e)
        {
            PlayerMobile player = e.From as PlayerMobile;
            if (player == null || player.Deleted)
                return;

            if (e.Skill == null)
                return;

            QuestManager.Instance.OnSkillUsed(player, e.Skill.SkillName, e.Success);
        }

        private static void OnSkillGain(SkillGainEventArgs e)
        {
            PlayerMobile player = e.From as PlayerMobile;
            if (player == null || player.Deleted)
                return;

            if (e.Skill == null)
                return;

            QuestManager.Instance.OnSkillGain(player, e.Skill.SkillName, e.Skill.Base);
        }

        #endregion

        #region Uso de item / interacao

        private static void OnItemUse(OnItemUseEventArgs e)
        {
            PlayerMobile player = e.From as PlayerMobile;
            if (player == null || player.Deleted)
                return;

            if (e.Item == null || e.Item.Deleted)
                return;

            RememberTouch(player, e.Item);

            QuestManager.Instance.OnItemUsed(player, e.Item);

            // Uso de item tambem conta como interacao com objeto
            QuestManager.Instance.OnInteract(player, e.Item, "DoubleClick");
        }

        /// <summary>
        /// Livro-razão de "último toque" para atribuir autoria de destruição.
        ///
        /// Por que é necessário:
        ///   EventSink.ItemDeleted informa apenas o Item — não diz QUEM o
        ///   destruiu. E Item.Delete() zera o Map antes de OnAfterDelete(), então
        ///   nem por proximidade dá para deduzir o autor. Sem essa atribuição, o
        ///   objetivo Destroy de item/estrutura nunca progredia: OnObjectDestroyed
        ///   existia no QuestManager mas ninguém o chamava.
        ///
        ///   Aqui registramos quem usou/interagiu com cada serial. Quando o item
        ///   some dentro da janela, esse jogador recebe o crédito.
        /// </summary>
        private static readonly Dictionary<Serial, TouchRecord> _lastTouch =
            new Dictionary<Serial, TouchRecord>();

        /// <summary>Janela em que um toque ainda explica a destruição do item.</summary>
        private static readonly TimeSpan TouchWindow = TimeSpan.FromSeconds(30);

        /// <summary>Limite de entradas — evita crescimento sem teto num shard movimentado.</summary>
        private const int TouchLedgerCap = 512;

        private struct TouchRecord
        {
            public PlayerMobile Player;
            public DateTime When;

            public TouchRecord(PlayerMobile player, DateTime when)
            {
                Player = player;
                When = when;
            }
        }

        private static void RememberTouch(PlayerMobile player, Item item)
        {
            if (player == null || item == null)
                return;

            DateTime now = DateTime.UtcNow;

            lock (_lastTouch)
            {
                _lastTouch[item.Serial] = new TouchRecord(player, now);

                // Poda periódica: só quando passa do teto, para não custar a cada uso
                if (_lastTouch.Count > TouchLedgerCap)
                {
                    var stale = new List<Serial>();

                    foreach (var pair in _lastTouch)
                    {
                        if (now - pair.Value.When > TouchWindow ||
                            pair.Value.Player == null || pair.Value.Player.Deleted)
                            stale.Add(pair.Key);
                    }

                    for (int i = 0; i < stale.Count; i++)
                        _lastTouch.Remove(stale[i]);
                }
            }
        }

        /// <summary>
        /// Item saiu do mundo: credita objetivos Destroy a quem o tocou por último.
        /// </summary>
        private static void OnItemDeleted(ItemDeletedEventArgs e)
        {
            Item item = e.Item;

            if (item == null)
                return;

            TouchRecord record;

            lock (_lastTouch)
            {
                if (!_lastTouch.TryGetValue(item.Serial, out record))
                    return;

                _lastTouch.Remove(item.Serial);
            }

            PlayerMobile player = record.Player;

            if (player == null || player.Deleted)
                return;

            if (DateTime.UtcNow - record.When > TouchWindow)
                return;

            // O item já está Deleted e sem Map, mas OnObjectDestroyed só precisa
            // de tipo, nome e serial — nada disso é afetado pelo Delete().
            QuestManager.Instance.OnObjectDestroyed(player, item);
        }

        #endregion

        #region Morte

        private static void OnPlayerDeath(PlayerDeathEventArgs e)
        {
            PlayerMobile player = e.Mobile as PlayerMobile;
            if (player == null)
                return;

            QuestManager.Instance.OnPlayerDied(player);
        }

        #endregion
    }
}
