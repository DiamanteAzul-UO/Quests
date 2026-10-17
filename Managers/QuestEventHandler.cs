using System;
using System.Collections.Generic;
using Server;
using Server.Gumps;
using Server.Mobiles;
using Server.Items;
using Server.Engines.Quests;
using Server.Engines.PartySystem; // ← Party e PartyMemberInfo

namespace Server.Custom.QuestSystem.Quests
{
    public static class QuestEventHandler
    {
        private static bool _initialized = false;

        // Distância máxima para membro da party receber crédito
        private const int PartyKillRange = 24;

        public static void Initialize()
        {
            if (_initialized)
                return;

            EventSink.OnKilledBy += OnKilledBy;
            EventSink.Movement   += OnMovement;
            EventSink.Login      += OnLogin;

            _initialized = true;
            Console.WriteLine("[QuestEventHandler] Eventos registrados.");
        }

        // ═══════════════════════════════════════════════════════════════
        //  LOGIN
        // ═══════════════════════════════════════════════════════════════

        private static void OnLogin(LoginEventArgs e)
        {
            PlayerMobile pm = e.Mobile as PlayerMobile;
            if (pm == null)
                return;

            EnsureQuestBackpack(pm);

            Timer.DelayCall(TimeSpan.FromSeconds(3.0), () =>
            {
                if (pm == null || pm.Deleted || pm.NetState == null)
                    return;

                MystQuestObjectiveGump.MostrarQuestsAtivas(pm);
            });
        }

        private static void EnsureQuestBackpack(PlayerMobile player)
        {
            if (player == null || player.Deleted)
                return;

            if (player.Backpack is QuestBackpack)
                return;

            Console.WriteLine($"[QuestEventHandler] Substituindo mochila de {player.Name}...");

            QuestBackpack novaMochila = new QuestBackpack();

            if (player.Backpack != null)
            {
                var itens = new List<Item>(player.Backpack.Items);

                foreach (Item item in itens)
                    novaMochila.DropItem(item);

                player.Backpack.Delete();
            }

            player.AddItem(novaMochila);

            Console.WriteLine($"[QuestEventHandler] Mochila substituída para {player.Name}.");
        }

        // ═══════════════════════════════════════════════════════════════
        //  KILL — com suporte a Party e Pet
        // ═══════════════════════════════════════════════════════════════

        private static void OnKilledBy(OnKilledByEventArgs e)
        {
            if (e.Killed == null || e.KilledBy == null)
                return;

            PlayerMobile killer = ResolveKiller(e.KilledBy);

            if (killer == null)
                return;

            Mobile victim = e.Killed;

            // Processa para o killer direto (sem restrição de flag)
            QuestManager.Instance.OnCreatureKilled(killer, victim);

            // Compartilha com membros da party próximos
            ShareKillWithParty(killer, victim);
        }

        /// <summary>
        /// Resolve o PlayerMobile responsável pelo kill.
        /// Suporta: jogador direto, pet controlado, criatura summoned.
        /// </summary>
        private static PlayerMobile ResolveKiller(Mobile killedBy)
        {
            if (killedBy is PlayerMobile pm)
                return pm;

            if (killedBy is BaseCreature bc)
            {
                if (bc.ControlMaster is PlayerMobile owner)
                    return owner;

                if (bc.SummonMaster is PlayerMobile summoner)
                    return summoner;
            }

            return null;
        }

        /// <summary>
        /// Compartilha o crédito do kill com membros próximos da party.
        /// A flag CountPartyKills é verificada por objetivo dentro do QuestManager.
        /// </summary>
        private static void ShareKillWithParty(PlayerMobile killer, Mobile victim)
        {
            Party party = Party.Get(killer);

            if (party == null || party.Members.Count <= 1)
                return;

            foreach (PartyMemberInfo info in party.Members)
            {
                PlayerMobile member = info.Mobile as PlayerMobile;

                // Ignora o killer (já processado) e membros inválidos
                if (member == null || member == killer || member.Deleted)
                    continue;

                // Membro deve estar vivo, no mesmo mapa e próximo
                if (!member.Alive)
                    continue;

                if (member.Map != victim.Map)
                    continue;

                if (!member.InRange(victim.Location, PartyKillRange))
                    continue;

                // Processa com flag isPartyKill = true
                // (o QuestManager só conta objetivos com CountPartyKills == true)
                QuestManager.Instance.OnCreatureKilled(member, victim, isPartyKill: true);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  MOVEMENT — objetivos de exploração
        // ═══════════════════════════════════════════════════════════════

        private static void OnMovement(MovementEventArgs e)
        {
            if (!(e.Mobile is PlayerMobile player))
                return;

            QuestManager.Instance.OnLocationEntered(player, player.Location, player.Map);
        }
    }
}