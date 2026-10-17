/*****************************************************
 * QuestManager.cs
 * Gerenciador Central do Sistema de Quests
 * Etapa 5 - QuestManager
 * 
 * Versão: 1.0.0
 * Compatível com: ServUO
 *****************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Server;
using Server.Mobiles;
using Server.Items;
using Server.Gumps;
using Server.Commands;
using Server.Custom.QuestSystem.Dialogue;
using Server.Custom.QuestSystem.Quests;
using Server.Engines.Quests;

namespace Server.Custom.QuestSystem.Quests
{
    /// <summary>
    /// Gerenciador central do sistema de quests
    /// </summary>
    public class QuestManager
    {
        #region Singleton

        private static QuestManager _instance;
        private static readonly object _lock = new object();

        public static QuestManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new QuestManager();
                        }
                    }
                }
                return _instance;
            }
        }

        #endregion

        #region Fields

        private Dictionary<Serial, PlayerQuestData> _playerData;
        private bool _isInitialized;

        #endregion

        #region Properties

        /// <summary>
        /// Sistema inicializado
        /// </summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// Total de jogadores com dados
        /// </summary>
        public int PlayerDataCount => _playerData.Count;

        #endregion

        #region Constructor

        private QuestManager()
        {
            _playerData = new Dictionary<Serial, PlayerQuestData>();
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Inicializa o QuestManager
        /// </summary>
        public static void Initialize()
        {
            Instance.Init();
            RegisterCommands();
            QuestEventHandler.Initialize();

            Console.WriteLine("[QuestManager] Sistema de Quests inicializado.");
        }

        private void Init()
        {
            _isInitialized = true;
        }

        private static void RegisterCommands()
        {
            CommandSystem.Register("queststart", AccessLevel.GameMaster, QuestStart_OnCommand);
            CommandSystem.Register("questcomplete", AccessLevel.GameMaster, QuestComplete_OnCommand);
            CommandSystem.Register("questfail", AccessLevel.GameMaster, QuestFail_OnCommand);
            CommandSystem.Register("questprogress", AccessLevel.GameMaster, QuestProgress_OnCommand);
            CommandSystem.Register("questactive", AccessLevel.GameMaster, QuestActive_OnCommand);
            CommandSystem.Register("questreset", AccessLevel.Administrator, QuestReset_OnCommand);
            CommandSystem.Register("questgive", AccessLevel.GameMaster, QuestGive_OnCommand);
        }

        #endregion

        #region Player Data Management

        /// <summary>
        /// Injeta dados de quest de um jogador (usado na deserialização)
        /// </summary>
        public void InjectPlayerData(Serial serial, PlayerQuestData data)
        {
            if (data != null)
                _playerData[serial] = data;
        }

        /// <summary>
        /// Obtém ou cria dados de quest do jogador
        /// </summary>
        public PlayerQuestData GetPlayerData(PlayerMobile player)
        {
            if (player == null)
                return null;

            if (!_playerData.TryGetValue(player.Serial, out PlayerQuestData data))
            {
                data = new PlayerQuestData(player);
                _playerData[player.Serial] = data;
            }

            return data;
        }

        /// <summary>
        /// Verifica se jogador tem dados
        /// </summary>
        public bool HasPlayerData(PlayerMobile player)
        {
            return player != null && _playerData.ContainsKey(player.Serial);
        }

        /// <summary>
        /// Remove dados do jogador
        /// </summary>
        public void RemovePlayerData(PlayerMobile player)
        {
            if (player != null)
                _playerData.Remove(player.Serial);
        }

        /// <summary>
        /// Limpa dados de jogadores offline há muito tempo
        /// </summary>
        public void CleanupOldData(TimeSpan maxAge)
        {
            // TODO: Implementar limpeza de dados antigos
        }

        #endregion

        #region Quest Status

        /// <summary>
        /// Obtém status de uma quest para o jogador
        /// </summary>
        public QuestStatus GetQuestStatus(PlayerMobile player, string questId)
        {
            var data = GetPlayerData(player);
            if (data == null)
                return QuestStatus.Available;

            return data.GetQuestStatus(questId);
        }

        /// <summary>
        /// Obtém tag de status formatada
        /// </summary>
        public string GetQuestStatusTag(PlayerMobile player, string questId)
        {
            var data = GetPlayerData(player);
            if (data == null)
                return "";

            return data.GetStatusTag(questId);
        }

        /// <summary>
        /// Verifica se jogador pode aceitar quest
        /// </summary>
        public bool CanAcceptQuest(PlayerMobile player, string questId, out string reason)
        {
            reason = null;

            var definition = QuestRepository.Instance.GetQuest(questId);
            if (definition == null)
            {
                reason = "Quest nao encontrada.";
                return false;
            }

            var data = GetPlayerData(player);

            // Verifica se já está ativa
            if (data.HasActiveQuest(questId))
            {
                reason = "Voce ja esta fazendo esta missao.";
                return false;
            }

            // Verifica cooldown
            if (data.IsOnCooldown(questId, out TimeSpan remaining))
            {
                reason = $"Esta missao estara disponivel em {FormatTimeSpan(remaining)}.";
                return false;
            }

            // Verifica se já completou (não repetível)
            if (!definition.Repeatable && data.HasCompletedQuest(questId))
            {
                reason = "Voce ja completou esta missao.";
                return false;
            }

            // Verifica máximo de completações
            if (definition.MaxCompletions > 0)
            {
                int count = data.GetCompletionCount(questId);
                if (count >= definition.MaxCompletions)
                {
                    reason = "Voce ja completou esta missao o maximo de vezes.";
                    return false;
                }
            }

            // Verifica limite de quests ativas
            int maxActive = QuestSystemConfig.Instance.MaxActiveQuestsPerPlayer;
            if (data.ActiveQuests.Count >= maxActive)
            {
                reason = $"Voce ja tem {maxActive} missoes ativas.";
                return false;
            }

            // Verifica pré-requisitos
            if (definition.Prerequisites != null && definition.Prerequisites.HasPrerequisites())
            {
                if (!definition.Prerequisites.Check(player, out string prereqReason))
                {
                    reason = prereqReason ?? "Pre-requisitos nao atendidos.";
                    return false;
                }
            }

            return true;
        }

        #endregion

        #region Quest Actions

        /// <summary>
        /// Inicia uma quest para o jogador
        /// </summary>
        public bool StartQuest(PlayerMobile player, string questId, out string message)
        {
            message = null;

            if (!CanAcceptQuest(player, questId, out message))
                return false;

            var definition = QuestRepository.Instance.GetQuest(questId);
            var data = GetPlayerData(player);

            var activeQuest = data.StartQuest(definition);

            QuestNotificationManager.Instance.NotifyQuestAccepted(player, definition);
            GiveQuestItems(player, definition);

            // ── MOSTRA O MINI GUMP DE OBJETIVOS ──────────────────────────
            Timer.DelayCall(TimeSpan.FromMilliseconds(500), () =>
            {
                if (player != null && !player.Deleted && player.NetState != null)
                    MystQuestObjectiveGump.MostrarParaQuest(player, questId);
            });
            // ─────────────────────────────────────────────────────────────

            message = "Missao '" + definition.Name + "' aceita!";
            return true;
        }

        public bool CompleteQuest(PlayerMobile player, string questId, out string message)
        {
            message = null;

            var data = GetPlayerData(player);
            var active = data.GetActiveQuest(questId);

            if (active == null)
            {
                message = "Voce nao esta fazendo esta missao.";
                return false;
            }

            var definition = QuestRepository.Instance.GetQuest(questId);
            if (definition == null)
            {
                message = "Erro: definicao de quest nao encontrada.";
                return false;
            }

            // Sincroniza itens da mochila antes de validar
            SyncCollectObjectives(player, definition, active);


            if (active.Status != QuestStatus.ReadyToTurnIn)
            {
                message = "Voce ainda nao completou todos os objetivos.";
                return false;
            }

            // Consome itens de coleta se necessário
            ConsumeQuestItems(player, definition, active);

            // Marca como completa
            data.CompleteQuest(questId, definition);

            // Dá recompensas
            GiveRewards(player, definition);

            // Notifica
            QuestNotificationManager.Instance.NotifyQuestCompleted(player, definition);

            // Verifica próxima quest da chain
            if (!string.IsNullOrEmpty(definition.NextQuestId))
            {
                var nextQuest = QuestRepository.Instance.GetQuest(definition.NextQuestId);
                if (nextQuest != null)
                    QuestNotificationManager.Instance.NotifyQuestAvailable(player, nextQuest);
            }

            message = $"Missao '{definition.Name}' completa!";
            return true;
        }
        /// <summary>
        /// Falha uma quest
        /// </summary>
        public bool FailQuest(PlayerMobile player, string questId, QuestFailReason reason, out string message)
        {
            message = null;

            var data = GetPlayerData(player);
            var active = data.GetActiveQuest(questId);

            if (active == null)
            {
                message = "Voce nao esta fazendo esta missao.";
                return false;
            }

            var definition = QuestRepository.Instance.GetQuest(questId);

            // Marca como falhada
            data.FailQuest(questId, reason);

            // Notifica
            QuestNotificationManager.Instance.NotifyQuestFailed(player, definition, reason);

            message = $"Missao '{definition?.Name ?? questId}' falhou.";
            return true;
        }

        /// <summary>
        /// Abandona uma quest
        /// </summary>
        public bool AbandonQuest(PlayerMobile player, string questId, out string message)
        {
            return FailQuest(player, questId, QuestFailReason.Abandoned, out message);
        }

        #endregion

        #region Progress Tracking

        /// <summary>
        /// Atualiza progresso de objetivo
        /// </summary>
        public void UpdateProgress(PlayerMobile player, string questId, int objectiveIndex, int amount = 1)
        {
            var data = GetPlayerData(player);
            var active = data.GetActiveQuest(questId);

            if (active == null || active.Status != QuestStatus.Active)
                return;

            var definition = QuestRepository.Instance.GetQuest(questId);
            if (definition == null)
                return;

            if (!definition.IsObjectiveActive(objectiveIndex, active.Progress))
                return;

            bool completed = active.UpdateProgress(objectiveIndex, amount);

            if (objectiveIndex < definition.Objectives.Count)
            {
                var objective = definition.Objectives[objectiveIndex];
                var progress = active.Progress[objectiveIndex];

                QuestNotificationManager.Instance.NotifyObjectiveProgress(
                    player, definition, objective, progress.Current, progress.Required);

                if (completed)
                    QuestNotificationManager.Instance.NotifyObjectiveComplete(player, definition, objective);
            }

            if (active.Status == QuestStatus.ReadyToTurnIn)
                QuestNotificationManager.Instance.NotifyQuestReadyToTurnIn(player, definition);

            // ── ATUALIZA O MINI GUMP ──────────────────────────────────────
            AtualizarMiniGump(player, questId);
            // ─────────────────────────────────────────────────────────────
        }

        /// <summary>
        /// Reabre o mini gump de objetivos com dados atualizados
        /// </summary>
        private void AtualizarMiniGump(PlayerMobile player, string questId)
        {
            if (player == null || player.Deleted || player.NetState == null)
                return;

            // Fecha o gump atual e reabre com dados novos
            player.CloseGump(typeof(MystQuestObjectiveGump));

            var playerData = GetPlayerData(player);
            if (playerData == null)
                return;

            var ativas = playerData.GetAllActiveQuests();
            int total = ativas.Count;

            // Encontra o índice desta quest na lista de ativas
            int questIndex = 0;
            for (int i = 0; i < ativas.Count; i++)
            {
                if (ativas[i].QuestId == questId)
                {
                    questIndex = i;
                    break;
                }
            }

            player.SendGump(new MystQuestObjectiveGump(player, questId, questIndex, total));
        }

        /// <summary>
        /// Processa evento de kill
        /// </summary>
        public void OnCreatureKilled(PlayerMobile player, Mobile killed)
        {
            if (player == null || killed == null)
                return;

            var data = GetPlayerData(player);
            string killedType = killed.GetType().Name;

            foreach (var active in data.ActiveQuests.ToList())
            {
                if (active.Status != QuestStatus.Active)
                    continue;

                var definition = QuestRepository.Instance.GetQuest(active.QuestId);
                if (definition == null)
                    continue;

                for (int i = 0; i < definition.Objectives.Count; i++)
                {
                    if (!(definition.Objectives[i] is KillObjective killObj))
                        continue;

                    if (active.Progress[i].IsComplete)
                        continue;

                    // Verifica se é o tipo correto
                    bool matches = false;

                    if (!string.IsNullOrEmpty(killObj.TargetType))
                    {
                        matches = killedType.Equals(killObj.TargetType, StringComparison.OrdinalIgnoreCase);
                    }

                    if (!matches && killObj.TargetTypes != null && killObj.TargetTypes.Count > 0)
                    {
                        matches = killObj.TargetTypes.Any(t =>
                            killedType.Equals(t, StringComparison.OrdinalIgnoreCase));
                    }

                    if (matches)
                    {
                        // Verifica região se necessário
                        if (!string.IsNullOrEmpty(killObj.RequiredRegion))
                        {
                            if (player.Region == null ||
                                !player.Region.Name.Equals(killObj.RequiredRegion, StringComparison.OrdinalIgnoreCase))
                                continue;
                        }

                        UpdateProgress(player, active.QuestId, i, 1);
                    }
                }
            }
        }

        /// <summary>
        /// Processa coleta de item
        /// </summary>
        public void OnItemCollected(PlayerMobile player, Item item)
        {
            if (player == null || item == null)
                return;

            var data = GetPlayerData(player);
            string itemType = item.GetType().Name;

            foreach (var active in data.ActiveQuests.ToList())
            {
                if (active.Status != QuestStatus.Active)
                    continue;

                var definition = QuestRepository.Instance.GetQuest(active.QuestId);
                if (definition == null)
                    continue;

                for (int i = 0; i < definition.Objectives.Count; i++)
                {
                    if (!(definition.Objectives[i] is CollectObjective collectObj))
                        continue;

                    if (active.Progress[i].IsComplete)
                        continue;

                    // Verifica tipo
                    if (!itemType.Equals(collectObj.ItemType, StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Verifica hue se necessário
                    if (collectObj.ItemHue > 0 && item.Hue != collectObj.ItemHue)
                        continue;

                    // Conta itens na mochila
                    int count = CountItemsInPack(player, collectObj.ItemType, collectObj.ItemHue);
                    int needed = collectObj.Required - active.Progress[i].Current;

                    if (count >= needed)
                    {
                        // Completa objetivo
                        int toAdd = needed;
                        UpdateProgress(player, active.QuestId, i, toAdd);
                    }
                    else if (count > 0)
                    {
                        // Progresso parcial - recalcula baseado no total
                        active.Progress[i].Current = Math.Min(count, collectObj.Required);
                        active.Progress[i].IsComplete = active.Progress[i].Current >= collectObj.Required;
                        active.CheckCompletion();

                        QuestNotificationManager.Instance.NotifyObjectiveProgress(
                            player,
                            definition,
                            collectObj,
                            active.Progress[i].Current,
                            collectObj.Required
                        );
                    }
                }
            }
        }

        /// <summary>
        /// Processa entrada em região/local
        /// </summary>
        public void OnLocationEntered(PlayerMobile player, Point3D location, Map map)
        {
            if (player == null)
                return;

            var data = GetPlayerData(player);

            foreach (var active in data.ActiveQuests.ToList())
            {
                if (active.Status != QuestStatus.Active)
                    continue;

                var definition = QuestRepository.Instance.GetQuest(active.QuestId);
                if (definition == null)
                    continue;

                for (int i = 0; i < definition.Objectives.Count; i++)
                {
                    if (!(definition.Objectives[i] is ExploreObjective exploreObj))
                        continue;

                    if (active.Progress[i].IsComplete)
                        continue;

                    // Verifica mapa
                    Map targetMap = exploreObj.GetMap();
                    if (map != targetMap)
                        continue;

                    // Verifica distância
                    Point3D targetLoc = exploreObj.GetLocation();
                    double distance = player.GetDistanceToSqrt(targetLoc);

                    if (distance <= exploreObj.RadiusTiles)
                    {
                        UpdateProgress(player, active.QuestId, i, 1);
                    }
                }
            }
        }

        /// <summary>
        /// Processa fala com NPC
        /// </summary>
        public void OnTalkToNpc(PlayerMobile player, Mobile npc, string npcId)
        {
            if (player == null || npc == null)
                return;

            var data = GetPlayerData(player);

            foreach (var active in data.ActiveQuests.ToList())
            {
                if (active.Status != QuestStatus.Active)
                    continue;

                var definition = QuestRepository.Instance.GetQuest(active.QuestId);
                if (definition == null)
                    continue;

                for (int i = 0; i < definition.Objectives.Count; i++)
                {
                    if (!(definition.Objectives[i] is TalkToObjective talkObj))
                        continue;

                    if (active.Progress[i].IsComplete)
                        continue;

                    bool matches = false;

                    if (!string.IsNullOrEmpty(talkObj.TargetNpcId) &&
                        talkObj.TargetNpcId.Equals(npcId, StringComparison.OrdinalIgnoreCase))
                    {
                        matches = true;
                    }

                    if (!matches && !string.IsNullOrEmpty(talkObj.TargetNpcName) &&
                        npc.Name != null &&
                        npc.Name.IndexOf(talkObj.TargetNpcName, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        matches = true;
                    }

                    if (!matches)
                        continue;

                    UpdateProgress(player, active.QuestId, i, 1);

                    // Entrega itens e sincroniza objetivos Collect correspondentes
                    if (active.Progress[i].IsComplete && talkObj.GrantItems != null && talkObj.GrantItems.Count > 0)
                    {
                        foreach (var grant in talkObj.GrantItems)
                        {
                            // Cria e entrega o item
                            var itemType = ScriptCompiler.FindTypeByName(grant.ItemType);
                            if (itemType == null)
                            {
                                Console.WriteLine($"[QuestManager] GrantItem: tipo '{grant.ItemType}' não encontrado.");
                                continue;
                            }

                            var item = Activator.CreateInstance(itemType) as Item;
                            if (item == null)
                                continue;

                            player.AddToBackpack(item);
                            player.SendMessage(54, $"Você recebeu: {grant.ItemName}.");

                            // Atualiza objetivos Collect que esperam este mesmo ItemType
                            for (int j = 0; j < definition.Objectives.Count; j++)
                            {
                                if (!(definition.Objectives[j] is CollectObjective collectObj))
                                    continue;

                                if (active.Progress[j].IsComplete)
                                    continue;

                                if (!string.Equals(collectObj.ItemType, grant.ItemType, StringComparison.OrdinalIgnoreCase))
                                    continue;

                                int needed = collectObj.Required - active.Progress[j].Current;
                                int toAdd = Math.Min(grant.Amount, needed);

                                if (toAdd > 0)
                                    UpdateProgress(player, active.QuestId, j, toAdd);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Processa interação com item/objeto (DoubleClick, Speech, etc)
        /// </summary>
        public void OnInteract(PlayerMobile player, object target, string interactionType = "DoubleClick")
        {
            if (player == null || target == null)
                return;

            var data = GetPlayerData(player);
            string targetTypeName = target.GetType().Name;

            Item targetItem = target as Item;
            Mobile targetMobile = target as Mobile;

            foreach (var active in data.ActiveQuests.ToList())
            {
                if (active.Status != QuestStatus.Active)
                    continue;

                var definition = QuestRepository.Instance.GetQuest(active.QuestId);
                if (definition == null)
                    continue;

                for (int i = 0; i < definition.Objectives.Count; i++)
                {
                    if (!(definition.Objectives[i] is InteractObjective interactObj))
                        continue;

                    if (active.Progress[i].IsComplete)
                        continue;

                    // Verifica tipo de interação (DoubleClick, Speech, etc)
                    if (!string.IsNullOrEmpty(interactObj.InteractionType) &&
                        !interactObj.InteractionType.Equals(interactionType, StringComparison.OrdinalIgnoreCase))
                        continue;

                    bool matches = false;

                    // Verifica pelo nome do tipo (ex: "MarcasEscavacao")
                    if (!string.IsNullOrEmpty(interactObj.TargetType) &&
                        targetTypeName.Equals(interactObj.TargetType, StringComparison.OrdinalIgnoreCase))
                    {
                        matches = true;
                    }

                    // Verifica pelo nome do objeto, se definido
                    if (!matches && !string.IsNullOrEmpty(interactObj.TargetName))
                    {
                        string name = targetItem?.Name ?? targetMobile?.Name;
                        if (name != null && name.IndexOf(interactObj.TargetName, StringComparison.OrdinalIgnoreCase) >= 0)
                            matches = true;
                    }

                    if (!matches)
                        continue;

                    // Se a quest exige um objeto específico (por serial), valida
                    if (interactObj.TargetSerials != null && interactObj.TargetSerials.Count > 0)
                    {
                        Serial serial = targetItem?.Serial ?? targetMobile?.Serial ?? Serial.MinusOne;
                        if (!interactObj.TargetSerials.Contains(serial))
                            continue;
                    }

                    UpdateProgress(player, active.QuestId, i, 1);
                }
            }
        }



        // =====================================================
        // COLOQUE OS DOIS METODOS NOVOS AQUI EMBAIXO
        // =====================================================

        /// <summary>
        /// Cria item de quest com tag de tipo para identificacao
        /// </summary>
        private Item CreateQuestItem(string itemType, string itemName, int amount)
        {
            try
            {
                var item = new QuestItem(0x14EF);

                item.QuestItemType = itemType;
                item.Name = !string.IsNullOrEmpty(itemName) ? itemName : itemType;
                item.LootType = LootType.Blessed;
                item.Weight = 1.0;
                item.Hue = 1153;

                if (amount > 1 && item.Stackable)
                    item.Amount = amount;

                return item;
            }
            catch (Exception ex)
            {
                Console.WriteLine("[QuestSystem] Erro ao criar QuestItem '{0}': {1}", itemType, ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Entrega itens do TalkTo ao jogador
        /// </summary>
        private void GiveItemsToPlayer(PlayerMobile player, Mobile npc, List<ObjectiveGrantItem> items)
        {
            if (player == null || items == null || items.Count == 0)
                return;

            foreach (var giveItem in items)
            {
                if (string.IsNullOrEmpty(giveItem.ItemType) || giveItem.Amount <= 0)
                    continue;

                Item item = null;

                Type realType = ScriptCompiler.FindTypeByName(giveItem.ItemType);

                if (realType != null)
                {
                    try
                    {
                        item = (Item)Activator.CreateInstance(realType);
                        if (item != null)
                        {
                            if (!string.IsNullOrEmpty(giveItem.ItemName))
                                item.Name = giveItem.ItemName;
                            if (item.Stackable && giveItem.Amount > 1)
                                item.Amount = giveItem.Amount;
                            item.LootType = LootType.Blessed;
                        }
                    }
                    catch { }
                }
                else
                {
                    item = CreateQuestItem(giveItem.ItemType, giveItem.ItemName, giveItem.Amount);
                }

                if (item != null)
                {
                    if (player.Backpack != null && player.Backpack.TryDropItem(player, item, false))
                    {
                        string name = !string.IsNullOrEmpty(giveItem.ItemName) ? giveItem.ItemName : giveItem.ItemType;
                        player.SendMessage(0x35, "{0} entregou a voce: {1}",
                            npc != null ? npc.Name : "O NPC", name);
                    }
                    else
                    {
                        item.MoveToWorld(player.Location, player.Map);
                        player.SendMessage(0x22, "Sua mochila esta cheia! O item foi colocado no chao.");
                    }
                }
            }
        }

        /// <summary>
        /// Processa craft de item
        /// </summary>
        public void OnItemCrafted(PlayerMobile player, Item item, string craftSystem)
        {
            if (player == null || item == null)
                return;

            var data = GetPlayerData(player);
            string itemType = item.GetType().Name;

            foreach (var active in data.ActiveQuests.ToList())
            {
                if (active.Status != QuestStatus.Active)
                    continue;

                var definition = QuestRepository.Instance.GetQuest(active.QuestId);
                if (definition == null)
                    continue;

                for (int i = 0; i < definition.Objectives.Count; i++)
                {
                    if (!(definition.Objectives[i] is CraftObjective craftObj))
                        continue;

                    if (active.Progress[i].IsComplete)
                        continue;

                    // Verifica tipo
                    if (!itemType.Equals(craftObj.ItemType, StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Verifica sistema de craft
                    if (!string.IsNullOrEmpty(craftObj.CraftSkill) &&
                        !craftSystem.Equals(craftObj.CraftSkill, StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Verifica excepcional
                    if (craftObj.RequireExceptional)
                    {
                        // TODO: Verificar qualidade do item
                    }

                    UpdateProgress(player, active.QuestId, i, 1);
                }
            }
        }

        /// <summary>
        /// Processa uso de skill
        /// </summary>
        public void OnSkillUsed(PlayerMobile player, SkillName skill, bool success)
        {
            if (player == null)
                return;

            var data = GetPlayerData(player);

            foreach (var active in data.ActiveQuests.ToList())
            {
                if (active.Status != QuestStatus.Active)
                    continue;

                var definition = QuestRepository.Instance.GetQuest(active.QuestId);
                if (definition == null)
                    continue;

                for (int i = 0; i < definition.Objectives.Count; i++)
                {
                    if (!(definition.Objectives[i] is UseSkillObjective useObj))
                        continue;

                    if (active.Progress[i].IsComplete)
                        continue;

                    if (useObj.Skill != skill)
                        continue;

                    if (useObj.RequireSuccess && !success)
                        continue;

                    UpdateProgress(player, active.QuestId, i, 1);
                }
            }
        }

        /// <summary>
        /// Processa ganho de skill
        /// </summary>
        public void OnSkillGain(PlayerMobile player, SkillName skill, double newValue)
        {
            if (player == null)
                return;

            var data = GetPlayerData(player);

            foreach (var active in data.ActiveQuests.ToList())
            {
                if (active.Status != QuestStatus.Active)
                    continue;

                var definition = QuestRepository.Instance.GetQuest(active.QuestId);
                if (definition == null)
                    continue;

                for (int i = 0; i < definition.Objectives.Count; i++)
                {
                    if (!(definition.Objectives[i] is SkillGainObjective gainObj))
                        continue;

                    if (active.Progress[i].IsComplete)
                        continue;

                    if (gainObj.Skill != skill)
                        continue;

                    if (gainObj.Mode == "ReachValue")
                    {
                        if (newValue >= gainObj.TargetValue)
                        {
                            // Completa
                            active.Progress[i].Current = active.Progress[i].Required;
                            active.Progress[i].IsComplete = true;
                            active.CheckCompletion();

                            QuestNotificationManager.Instance.NotifyObjectiveComplete(player, definition, gainObj);

                            if (active.Status == QuestStatus.ReadyToTurnIn)
                            {
                                QuestNotificationManager.Instance.NotifyQuestReadyToTurnIn(player, definition);
                            }
                        }
                    }
                }
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Conta itens na mochila - suporta tipos custom por nome
        /// </summary>
        private int CountItemsInPack(PlayerMobile player, string itemTypeName, int hue = 0)
        {
            if (player?.Backpack == null)
                return 0;

            Type itemType = ScriptCompiler.FindTypeByName(itemTypeName);

            // Tipo existe no ServUO - busca normal
            if (itemType != null)
            {
                Item[] items = player.Backpack.FindItemsByType(itemType);
                int count = 0;

                foreach (Item item in items)
                {
                    if (hue == 0 || item.Hue == hue)
                        count += item.Amount;
                }

                return count;
            }

            // Tipo custom (CartaLacradaOlin, etc) - busca por nome
            return CountItemsByName(player, itemTypeName, hue);
        }

        /// <summary>
        /// Conta itens na mochila por nome (para itens custom de quest)
        /// </summary>
        private int CountItemsByName(PlayerMobile player, string itemTypeName, int hue = 0)
        {
            if (player?.Backpack == null || string.IsNullOrEmpty(itemTypeName))
                return 0;

            int count = 0;

            foreach (Item item in player.Backpack.Items)
            {
                if (item == null || item.Deleted)
                    continue;

                // Verifica pelo Name do item
                if (item.Name != null && item.Name.IndexOf(itemTypeName, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (hue == 0 || item.Hue == hue)
                        count += item.Amount;
                    continue;
                }

                // Verifica por tag QuestItemType (mais preciso)
                string tag = GetQuestItemTag(item);
                if (tag != null && tag.Equals(itemTypeName, StringComparison.OrdinalIgnoreCase))
                {
                    if (hue == 0 || item.Hue == hue)
                        count += item.Amount;
                }
            }

            return count;
        }

        /// <summary>
        /// Recupera a tag de tipo de quest de um item
        /// </summary>
        private string GetQuestItemTag(Item item)
        {
            if (item == null)
                return null;

            // Usa NamedPropertyInfo se disponivel, senao usa Name
            // A tag e guardada no momento da criacao do item
            try
            {
                if (item is QuestItem qi)
                    return qi.QuestItemType;
            }
            catch { }

            return null;
        }

        /// <summary>
        /// Da itens de quest ao jogador - suporta tipos custom
        /// </summary>
        private void GiveQuestItems(PlayerMobile player, QuestDefinition definition)
        {
            foreach (var obj in definition.Objectives)
            {
                if (obj is DeliverObjective deliver && deliver.GiveItemOnAccept)
                {
                    Type itemType = ScriptCompiler.FindTypeByName(deliver.ItemType);

                    Item item = null;

                    if (itemType != null)
                    {
                        // Tipo existe no ServUO
                        try
                        {
                            item = Activator.CreateInstance(itemType) as Item;
                            if (item != null)
                            {
                                item.Name = deliver.ItemName ?? item.Name;
                                if (deliver.ItemHue > 0)
                                    item.Hue = deliver.ItemHue;
                            }
                        }
                        catch { }
                    }
                    else
                    {
                        // Tipo custom - cria item generico
                        item = CreateQuestItem(deliver.ItemType, deliver.ItemName, 1);
                    }

                    if (item != null)
                    {
                        item.LootType = LootType.Blessed;
                        player.AddToBackpack(item);
                        player.SendMessage(0x40, "Voce recebeu: {0}", item.Name);
                    }
                }
            }
        }

        // ── No QuestManager.cs ─────────────────────────────────────────────────

        /// <summary>
        /// Chamado quando um membro da party recebe crédito de kill compartilhado.
        /// Só processa objetivos com CountPartyKills == true.
        /// </summary>


        /// <summary>
        /// Processa evento de kill
        /// isPartyKill = true quando o crédito vem de um membro da party (não o killer direto)
        /// </summary>
        public void OnCreatureKilled(PlayerMobile player, Mobile killed, bool isPartyKill = false)
        {
            if (player == null || killed == null)
                return;

            var data = GetPlayerData(player);
            string killedType = killed.GetType().Name;

            foreach (var active in data.ActiveQuests.ToList())
            {
                if (active.Status != QuestStatus.Active)
                    continue;

                var definition = QuestRepository.Instance.GetQuest(active.QuestId);
                if (definition == null)
                    continue;

                for (int i = 0; i < definition.Objectives.Count; i++)
                {
                    if (!(definition.Objectives[i] is KillObjective killObj))
                        continue;

                    if (active.Progress[i].IsComplete)
                        continue;

                    // ── FILTRO DE PARTY: se o kill veio de um membro da party,
                    //    só conta se o objetivo permitir compartilhamento ──────
                    if (isPartyKill && !killObj.CountPartyKills)
                        continue;

                    // Verifica se é o tipo correto
                    bool matches = false;

                    if (!string.IsNullOrEmpty(killObj.TargetType))
                    {
                        matches = killedType.Equals(killObj.TargetType, StringComparison.OrdinalIgnoreCase);
                    }

                    if (!matches && killObj.TargetTypes != null && killObj.TargetTypes.Count > 0)
                    {
                        matches = killObj.TargetTypes.Any(t =>
                            killedType.Equals(t, StringComparison.OrdinalIgnoreCase));
                    }

                    if (matches)
                    {
                        // Verifica região se necessário
                        if (!string.IsNullOrEmpty(killObj.RequiredRegion))
                        {
                            if (player.Region == null ||
                                !player.Region.Name.Equals(killObj.RequiredRegion, StringComparison.OrdinalIgnoreCase))
                                continue;
                        }

                        UpdateProgress(player, active.QuestId, i, 1);
                    }
                }
            }
        }



        /// <summary>
        /// Processa entrega de item a NPC específico
        /// Chamado pelo NPC ao falar "entregar" ou drag-drop
        /// </summary>
        public bool OnDeliverItem(PlayerMobile player, Mobile npc, string npcId, Item draggedItem = null)
        {
            if (player == null || npc == null)
                return false;

            var data = GetPlayerData(player);
            bool anyDelivered = false;

            foreach (var active in data.ActiveQuests.ToList())
            {
                if (active.Status != QuestStatus.Active)
                    continue;

                var definition = QuestRepository.Instance.GetQuest(active.QuestId);
                if (definition == null)
                    continue;

                for (int i = 0; i < definition.Objectives.Count; i++)
                {
                    if (!(definition.Objectives[i] is DeliverObjective deliverObj))
                        continue;

                    if (active.Progress[i].IsComplete)
                        continue;

                    // ── 1. Valida se este NPC é o destinatário ────────────────
                    bool npcMatch = false;

                    if (!string.IsNullOrEmpty(deliverObj.TargetNpcId))
                        npcMatch = deliverObj.TargetNpcId.Equals(npcId, StringComparison.OrdinalIgnoreCase);

                    if (!npcMatch && !string.IsNullOrEmpty(deliverObj.TargetNpcName) && npc.Name != null)
                        npcMatch = npc.Name.IndexOf(deliverObj.TargetNpcName, StringComparison.OrdinalIgnoreCase) >= 0;

                    if (!npcMatch)
                    {
                        Console.WriteLine($"[QuestManager] OnDeliverItem: NPC '{npc.Name}' nao e o destinatario de '{deliverObj.ItemType}'");
                        continue;
                    }

                    // ── 2. Verifica se jogador possui o item ──────────────────
                    int countInBag = 0;
                    Type deliverType = ScriptCompiler.FindTypeByName(deliverObj.ItemType);

                    if (draggedItem != null)
                    {
                        // Drag-drop: valida o item arrastado
                        bool itemMatch = false;

                        if (deliverType != null && deliverType.IsInstanceOfType(draggedItem))
                            itemMatch = true;

                        if (!itemMatch)
                        {
                            string tag = GetQuestItemTag(draggedItem);
                            if (tag != null && tag.Equals(deliverObj.ItemType, StringComparison.OrdinalIgnoreCase))
                                itemMatch = true;
                        }

                        if (!itemMatch && draggedItem.Name != null &&
                            draggedItem.Name.IndexOf(deliverObj.ItemType, StringComparison.OrdinalIgnoreCase) >= 0)
                            itemMatch = true;

                        if (itemMatch && deliverObj.ItemHue > 0 && draggedItem.Hue != deliverObj.ItemHue)
                            itemMatch = false;

                        if (!itemMatch)
                        {
                            npc.Say($"Isso nao e o que preciso, {player.Name}.");
                            continue;
                        }

                        countInBag = draggedItem.Amount;
                    }
                    else
                    {
                        // Via fala: busca na mochila
                        countInBag = CountItemsInPack(player, deliverObj.ItemType, deliverObj.ItemHue);
                    }

                    int needed = deliverObj.Required - active.Progress[i].Current;

                    if (countInBag <= 0)
                    {
                        npc.Say($"Voce nao tem o item que preciso, {player.Name}.");
                        continue;
                    }

                    if (countInBag < needed)
                    {
                        npc.Say($"Voce precisa de {needed} e so tem {countInBag}.");
                        continue;
                    }

                    // ── 3. Consome o item ─────────────────────────────────────
                    int toConsume = needed;

                    if (draggedItem != null)
                    {
                        // Consome o item arrastado
                        int take = Math.Min(draggedItem.Amount, toConsume);
                        if (draggedItem.Amount <= take)
                            draggedItem.Delete();
                        else
                            draggedItem.Amount -= take;
                        toConsume -= take;
                    }

                    // Se ainda precisa consumir mais (ou veio via fala)
                    if (toConsume > 0)
                    {
                        if (deliverType != null)
                        {
                            foreach (Item bagItem in player.Backpack.FindItemsByType(deliverType).ToList())
                            {
                                if (toConsume <= 0) break;
                                if (deliverObj.ItemHue > 0 && bagItem.Hue != deliverObj.ItemHue) continue;

                                int take = Math.Min(bagItem.Amount, toConsume);
                                bagItem.Amount -= take;
                                if (bagItem.Amount <= 0) bagItem.Delete();
                                toConsume -= take;
                            }
                        }
                        else
                        {
                            ConsumeItemsByName(player, deliverObj.ItemType, deliverObj.ItemHue, toConsume);
                        }
                    }

                    // ── 4. Atualiza progresso ─────────────────────────────────
                    // Marca diretamente como completo (o item já foi consumido)
                    active.Progress[i].Current = active.Progress[i].Required;
                    active.Progress[i].IsComplete = true;

                    Console.WriteLine($"[QuestManager] Deliver OK: '{deliverObj.ItemType}' entregue a '{npc.Name}' por {player.Name}");

                    // Verifica se a quest toda ficou pronta
                    active.CheckCompletion();

                    var def = definition; // captura para o timer
                    if (active.Status == QuestStatus.ReadyToTurnIn)
                        QuestNotificationManager.Instance.NotifyQuestReadyToTurnIn(player, def);

                    QuestNotificationManager.Instance.NotifyObjectiveComplete(player, definition, deliverObj);
                    AtualizarMiniGump(player, active.QuestId);

                    anyDelivered = true;
                }
            }

            return anyDelivered;
        }

        /// <summary>
        /// Consome itens de quest - suporta tipos custom por nome
        /// </summary>
        /// 
        /// 
        private void ConsumeQuestItems(PlayerMobile player, QuestDefinition definition, ActiveQuest active)
        {
            if (player?.Backpack == null)
                return;

            for (int i = 0; i < definition.Objectives.Count; i++)
            {
                // ── Deliver: item já foi consumido no OnDeliverItem ───────────
                // NÃO consome novamente aqui!
                if (definition.Objectives[i] is DeliverObjective)
                    continue;

                int toConsume = 0;
                string itemTypeName = null;
                int itemHue = 0;

                if (definition.Objectives[i] is CollectObjective collect && collect.ConsumeOnComplete)
                {
                    toConsume = collect.Required;
                    itemTypeName = collect.ItemType;
                    itemHue = collect.ItemHue;
                }
                else
                {
                    continue;
                }

                Type itemType = ScriptCompiler.FindTypeByName(itemTypeName);

                if (itemType != null)
                {
                    foreach (Item item in player.Backpack.FindItemsByType(itemType).ToList())
                    {
                        if (toConsume <= 0) break;

                        if (itemHue > 0 && item.Hue != itemHue)
                            continue;

                        if (item.Amount <= toConsume)
                        {
                            toConsume -= item.Amount;
                            item.Delete();
                        }
                        else
                        {
                            item.Amount -= toConsume;
                            toConsume = 0;
                        }
                    }
                }
                else
                {
                    ConsumeItemsByName(player, itemTypeName, itemHue, toConsume);
                }
            }
        }
        /// <summary>
        /// Sincroniza objetivos Collect com o que o jogador tem na mochila
        /// </summary>
        private void SyncCollectObjectives(PlayerMobile player, QuestDefinition definition, ActiveQuest active)
        {
            if (player?.Backpack == null)
                return;

            bool anyUpdated = false;

            for (int i = 0; i < definition.Objectives.Count; i++)
            {
                if (!(definition.Objectives[i] is CollectObjective collectObj))
                    continue;

                if (active.Progress[i].IsComplete)
                    continue;

                var itemType = ScriptCompiler.FindTypeByName(collectObj.ItemType);
                if (itemType == null)
                {
                    Console.WriteLine($"[QuestManager] SyncCollect: tipo '{collectObj.ItemType}' não encontrado.");
                    continue;
                }

                int countInBag = 0;
                foreach (Item bagItem in player.Backpack.FindItemsByType(itemType))
                    countInBag += bagItem.Amount;

                if (countInBag <= 0)
                    continue;

                int needed = collectObj.Required - active.Progress[i].Current;
                int toAdd = Math.Min(countInBag, needed);

                if (toAdd <= 0)
                    continue;

                // Atualiza diretamente, sem passar pelos guards do UpdateProgress
                active.Progress[i].AddProgress(toAdd);

                anyUpdated = true;

                Console.WriteLine($"[QuestManager] SyncCollect: objetivo {i} '{collectObj.ItemType}' atualizado para {active.Progress[i].Current}/{active.Progress[i].Required}");
            }

            // Força recálculo do status da quest
            if (anyUpdated)
                active.CheckCompletion();
        }

        /// <summary>
        /// Sincroniza objetivos Deliver com o que o jogador tem na mochila
        /// (a entrega em si acontece via conversa, não drag-drop)
        /// </summary>




        /// <summary>
        /// Consome itens custom por nome (CartaLacradaOlin, etc)
        /// </summary>
        private void ConsumeItemsByName(PlayerMobile player, string itemTypeName, int hue, int toConsume)
        {
            if (player?.Backpack == null || string.IsNullOrEmpty(itemTypeName))
                return;

            // Cria lista para evitar modificar colecao durante iteracao
            List<Item> toCheck = new List<Item>(player.Backpack.Items);

            foreach (Item item in toCheck)
            {
                if (toConsume <= 0)
                    break;

                if (item == null || item.Deleted)
                    continue;

                bool matches = false;

                // Verifica por nome
                if (item.Name != null && item.Name.IndexOf(itemTypeName, StringComparison.OrdinalIgnoreCase) >= 0)
                    matches = true;

                // Verifica por tag QuestItemType
                if (!matches)
                {
                    string tag = GetQuestItemTag(item);
                    if (tag != null && tag.Equals(itemTypeName, StringComparison.OrdinalIgnoreCase))
                        matches = true;
                }

                if (!matches)
                    continue;

                if (hue > 0 && item.Hue != hue)
                    continue;

                if (item.Amount <= toConsume)
                {
                    toConsume -= item.Amount;
                    item.Delete();
                }
                else
                {
                    item.Amount -= toConsume;
                    toConsume = 0;
                }
            }
        }
        /// <summary>
        /// Dá recompensas ao jogador
        /// </summary>
        /// <summary>
        /// Dá recompensas ao jogador
        /// </summary>
        private void GiveRewards(PlayerMobile player, QuestDefinition definition)
        {
            Console.WriteLine($"[QuestSystem] GiveRewards chamado para quest: {definition?.Id}");

            if (player == null || definition == null)
            {
                Console.WriteLine("[QuestSystem] ERRO: player ou definition null!");
                return;
            }

            var rewards = definition.Rewards;
            if (rewards == null)
            {
                Console.WriteLine("[QuestSystem] Nenhuma recompensa configurada.");
                return;
            }

            Console.WriteLine($"[QuestSystem] Gold: {rewards.Gold}, Fame: {rewards.Fame}, Items: {rewards.Items.Count}");

            // Gold
            if (rewards.Gold > 0)
            {
                try
                {
                    if (rewards.BankDeposit)
                    {
                        Banker.Deposit(player, rewards.Gold);
                        player.SendMessage(0x40, $"Depositado {rewards.Gold} moedas de ouro em seu banco.");
                        Console.WriteLine($"[QuestSystem] {rewards.Gold} gold depositado no banco.");
                    }
                    else
                    {
                        if (player.Backpack != null)
                        {
                            Gold goldItem = new Gold(rewards.Gold);
                            player.Backpack.DropItem(goldItem);
                            player.SendMessage(0x40, $"Voce recebeu {rewards.Gold} moedas de ouro.");
                            Console.WriteLine($"[QuestSystem] {rewards.Gold} gold adicionado na mochila.");
                        }
                        else
                        {
                            Console.WriteLine("[QuestSystem] ERRO: Player sem backpack!");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[QuestSystem] ERRO ao dar gold: {ex.Message}");
                }
            }

            // Fame
            if (rewards.Fame > 0)
            {
                try
                {
                    player.Fame += rewards.Fame;
                    player.SendMessage(0x40, $"Sua fama aumentou em {rewards.Fame}.");
                    Console.WriteLine($"[QuestSystem] Fame +{rewards.Fame}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[QuestSystem] ERRO ao dar fame: {ex.Message}");
                }
            }

            // Karma
            if (rewards.Karma != 0)
            {
                try
                {
                    player.Karma += rewards.Karma;
                    string karmaMsg = rewards.Karma > 0 ? $"aumentou em {rewards.Karma}" : $"diminuiu em {Math.Abs(rewards.Karma)}";
                    player.SendMessage(0x40, $"Seu karma {karmaMsg}.");
                    Console.WriteLine($"[QuestSystem] Karma {rewards.Karma}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[QuestSystem] ERRO ao dar karma: {ex.Message}");
                }
            }

            // Items
            if (rewards.Items != null && rewards.Items.Count > 0)
            {
                foreach (var rewardItem in rewards.Items)
                {
                    // Verifica chance
                    if (rewardItem.Chance < 100)
                    {
                        if (Utility.Random(100) >= rewardItem.Chance)
                        {
                            Console.WriteLine($"[QuestSystem] Item {rewardItem.Type} falhou no teste de chance.");
                            continue;
                        }
                    }

                    Type itemType = ScriptCompiler.FindTypeByName(rewardItem.Type);
                    if (itemType == null)
                    {
                        Console.WriteLine($"[QuestSystem] ERRO: Tipo nao encontrado: {rewardItem.Type}");
                        continue;
                    }

                    try
                    {
                        Item item = Activator.CreateInstance(itemType) as Item;
                        if (item != null)
                        {
                            if (!string.IsNullOrEmpty(rewardItem.Name))
                                item.Name = rewardItem.Name;
                            if (rewardItem.Hue > 0)
                                item.Hue = rewardItem.Hue;

                            // Se é stackável e quantidade > 1
                            if (item.Stackable && rewardItem.Amount > 1)
                            {
                                item.Amount = rewardItem.Amount;

                                if (player.Backpack != null)
                                {
                                    player.Backpack.DropItem(item);
                                    player.SendMessage(0x40, $"Voce recebeu: {rewardItem.Amount}x {rewardItem.Name ?? item.Name}");
                                    Console.WriteLine($"[QuestSystem] Item stackavel dado: {rewardItem.Amount}x {rewardItem.Type}");
                                }
                                else
                                {
                                    item.Delete();
                                }
                            }
                            else
                            {
                                // Não stackável - cria múltiplos
                                for (int i = 0; i < rewardItem.Amount; i++)
                                {
                                    Item singleItem;
                                    if (i == 0)
                                    {
                                        singleItem = item;
                                    }
                                    else
                                    {
                                        singleItem = Activator.CreateInstance(itemType) as Item;
                                        if (singleItem == null)
                                            continue;

                                        if (!string.IsNullOrEmpty(rewardItem.Name))
                                            singleItem.Name = rewardItem.Name;
                                        if (rewardItem.Hue > 0)
                                            singleItem.Hue = rewardItem.Hue;
                                    }

                                    if (player.Backpack != null)
                                    {
                                        player.Backpack.DropItem(singleItem);
                                    }
                                    else
                                    {
                                        singleItem.Delete();
                                    }
                                }

                                player.SendMessage(0x40, $"Voce recebeu: {rewardItem.Amount}x {rewardItem.Name ?? item.Name}");
                                Console.WriteLine($"[QuestSystem] Itens nao stackaveis dados: {rewardItem.Amount}x {rewardItem.Type}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[QuestSystem] ERRO ao criar item {rewardItem.Type}: {ex.Message}");
                    }
                }
            }

            // Skill Gains
            if (rewards.SkillGains != null && rewards.SkillGains.Count > 0)
            {
                foreach (var skillReward in rewards.SkillGains)
                {
                    try
                    {
                        Skill skill = player.Skills[skillReward.Skill];
                        if (skill != null)
                        {
                            skill.Base += skillReward.Amount;
                            player.SendMessage(0x40, $"Sua habilidade {skillReward.SkillName ?? skillReward.Skill.ToString()} aumentou em {skillReward.Amount}.");
                            Console.WriteLine($"[QuestSystem] Skill {skillReward.Skill} +{skillReward.Amount}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[QuestSystem] ERRO ao dar skill: {ex.Message}");
                    }
                }
            }

            // Stat Gains
            if (rewards.StatGains != null && rewards.StatGains.Count > 0)
            {
                foreach (var statReward in rewards.StatGains)
                {
                    try
                    {
                        switch (statReward.Stat?.ToLower())
                        {
                            case "str":
                                player.RawStr += statReward.Amount;
                                player.SendMessage(0x40, $"Sua forca aumentou em {statReward.Amount}.");
                                Console.WriteLine($"[QuestSystem] STR +{statReward.Amount}");
                                break;
                            case "dex":
                                player.RawDex += statReward.Amount;
                                player.SendMessage(0x40, $"Sua destreza aumentou em {statReward.Amount}.");
                                Console.WriteLine($"[QuestSystem] DEX +{statReward.Amount}");
                                break;
                            case "int":
                                player.RawInt += statReward.Amount;
                                player.SendMessage(0x40, $"Sua inteligencia aumentou em {statReward.Amount}.");
                                Console.WriteLine($"[QuestSystem] INT +{statReward.Amount}");
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[QuestSystem] ERRO ao dar stat: {ex.Message}");
                    }
                }
            }

            // Unlocked Quests
            if (rewards.UnlockedQuests != null && rewards.UnlockedQuests.Count > 0)
            {
                var data = GetPlayerData(player);
                foreach (var questId in rewards.UnlockedQuests)
                {
                    data.SetFlag($"quest_unlocked_{questId}", true);
                    player.SendMessage(0x40, $"Nova missao desbloqueada!");
                    Console.WriteLine($"[QuestSystem] Quest desbloqueada: {questId}");
                }
            }

            Console.WriteLine("[QuestSystem] GiveRewards concluido com sucesso.");
        }
        private string FormatTimeSpan(TimeSpan ts)
        {
            if (ts.TotalHours >= 1)
                return $"{(int)ts.TotalHours}h {ts.Minutes}m";
            if (ts.TotalMinutes >= 1)
                return $"{(int)ts.TotalMinutes}m";
            return $"{(int)ts.TotalSeconds}s";
        }

        #endregion

        #region Serialization

        /// <summary>
        /// Serializa dados do jogador
        /// </summary>
        public void SerializePlayerData(PlayerMobile player, GenericWriter writer)
        {
            var data = GetPlayerData(player);
            if (data != null)
            {
                writer.Write(true);
                data.Serialize(writer);
            }
            else
            {
                writer.Write(false);
            }
        }

        /// <summary>
        /// Deserializa dados do jogador
        /// </summary>
        public void DeserializePlayerData(PlayerMobile player, GenericReader reader)
        {
            bool hasData = reader.ReadBool();
            if (hasData)
            {
                var data = new PlayerQuestData(player);
                data.Deserialize(reader);
                _playerData[player.Serial] = data;
            }
        }

        #endregion

        #region GM Commands

        [Usage("queststart <questId>")]
        [Description("Inicia uma quest para o jogador alvo.")]
        private static void QuestStart_OnCommand(CommandEventArgs e)
        {
            if (e.Arguments.Length < 1)
            {
                e.Mobile.SendMessage(0x26, "Uso: [queststart <questId>");
                return;
            }

            string questId = e.Arguments[0];
            e.Mobile.SendMessage(0x480, "Clique no jogador...");
            e.Mobile.Target = new QuestTargetAction(questId, "start");
        }

        [Usage("questcomplete <questId>")]
        [Description("Completa uma quest do jogador alvo.")]
        private static void QuestComplete_OnCommand(CommandEventArgs e)
        {
            if (e.Arguments.Length < 1)
            {
                e.Mobile.SendMessage(0x26, "Uso: [questcomplete <questId>");
                return;
            }

            string questId = e.Arguments[0];
            e.Mobile.SendMessage(0x480, "Clique no jogador...");
            e.Mobile.Target = new QuestTargetAction(questId, "complete");
        }

        [Usage("questfail <questId>")]
        [Description("Falha uma quest do jogador alvo.")]
        private static void QuestFail_OnCommand(CommandEventArgs e)
        {
            if (e.Arguments.Length < 1)
            {
                e.Mobile.SendMessage(0x26, "Uso: [questfail <questId>");
                return;
            }

            string questId = e.Arguments[0];
            e.Mobile.SendMessage(0x480, "Clique no jogador...");
            e.Mobile.Target = new QuestTargetAction(questId, "fail");
        }

        [Usage("questprogress <questId> <objectiveIndex> [amount]")]
        [Description("Adiciona progresso a um objetivo.")]
        private static void QuestProgress_OnCommand(CommandEventArgs e)
        {
            if (e.Arguments.Length < 2)
            {
                e.Mobile.SendMessage(0x26, "Uso: [questprogress <questId> <objectiveIndex> [amount]");
                return;
            }

            string questId = e.Arguments[0];
            int objIndex = int.Parse(e.Arguments[1]);
            int amount = e.Arguments.Length > 2 ? int.Parse(e.Arguments[2]) : 1;

            e.Mobile.SendMessage(0x480, "Clique no jogador...");
            e.Mobile.Target = new QuestProgressTarget(questId, objIndex, amount);
        }

        [Usage("questactive")]
        [Description("Lista quests ativas do jogador alvo.")]
        private static void QuestActive_OnCommand(CommandEventArgs e)
        {
            e.Mobile.SendMessage(0x480, "Clique no jogador...");
            e.Mobile.Target = new QuestListActiveTarget();
        }

        [Usage("questreset <questId>")]
        [Description("Reseta uma quest do jogador alvo.")]
        private static void QuestReset_OnCommand(CommandEventArgs e)
        {
            if (e.Arguments.Length < 1)
            {
                e.Mobile.SendMessage(0x26, "Uso: [questreset <questId>");
                return;
            }

            string questId = e.Arguments[0];
            e.Mobile.SendMessage(0x480, "Clique no jogador...");
            e.Mobile.Target = new QuestTargetAction(questId, "reset");
        }

        [Usage("questgive <questId>")]
        [Description("Da uma quest ao jogador (ignora pre-requisitos).")]
        private static void QuestGive_OnCommand(CommandEventArgs e)
        {
            if (e.Arguments.Length < 1)
            {
                e.Mobile.SendMessage(0x26, "Uso: [questgive <questId>");
                return;
            }

            string questId = e.Arguments[0];
            e.Mobile.SendMessage(0x480, "Clique no jogador...");
            e.Mobile.Target = new QuestTargetAction(questId, "give");
        }

        #endregion

        #region Target Classes

        private class QuestTargetAction : Server.Targeting.Target
        {
            private string _questId;
            private string _action;

            public QuestTargetAction(string questId, string action) : base(-1, false, Server.Targeting.TargetFlags.None)
            {
                _questId = questId;
                _action = action;
            }

            protected override void OnTarget(Mobile from, object targeted)
            {
                if (!(targeted is PlayerMobile player))
                {
                    from.SendMessage(0x26, "Alvo deve ser um jogador.");
                    return;
                }

                string message;

                switch (_action)
                {
                    case "start":
                        if (Instance.StartQuest(player, _questId, out message))
                            from.SendMessage(0x40, message);
                        else
                            from.SendMessage(0x26, message);
                        break;

                    case "complete":
                        // Força status ReadyToTurnIn
                        var data = Instance.GetPlayerData(player);
                        var active = data.GetActiveQuest(_questId);
                        if (active != null)
                        {
                            foreach (var p in active.Progress)
                            {
                                p.Current = p.Required;
                                p.IsComplete = true;
                            }
                            active.Status = QuestStatus.ReadyToTurnIn;
                        }

                        if (Instance.CompleteQuest(player, _questId, out message))
                            from.SendMessage(0x40, message);
                        else
                            from.SendMessage(0x26, message);
                        break;

                    case "fail":
                        if (Instance.FailQuest(player, _questId, QuestFailReason.Abandoned, out message))
                            from.SendMessage(0x40, message);
                        else
                            from.SendMessage(0x26, message);
                        break;

                    case "reset":
                        var playerData = Instance.GetPlayerData(player);
                        playerData.RemoveActiveQuest(_questId);
                        playerData.CompletedQuests.RemoveAll(q => q.QuestId == _questId);
                        playerData.FailedQuests.RemoveAll(q => q.QuestId == _questId);
                        from.SendMessage(0x40, $"Quest '{_questId}' resetada para {player.Name}.");
                        break;

                    case "give":
                        var definition = QuestRepository.Instance.GetQuest(_questId);
                        if (definition == null)
                        {
                            from.SendMessage(0x26, "Quest nao encontrada.");
                            return;
                        }
                        var pData = Instance.GetPlayerData(player);
                        pData.StartQuest(definition);
                        Instance.GiveQuestItems(player, definition);
                        from.SendMessage(0x40, $"Quest '{_questId}' dada a {player.Name}.");
                        QuestNotificationManager.Instance.NotifyQuestAccepted(player, definition);
                        break;
                }
            }
        }

        private class QuestProgressTarget : Server.Targeting.Target
        {
            private string _questId;
            private int _objIndex;
            private int _amount;

            public QuestProgressTarget(string questId, int objIndex, int amount) : base(-1, false, Server.Targeting.TargetFlags.None)
            {
                _questId = questId;
                _objIndex = objIndex;
                _amount = amount;
            }

            protected override void OnTarget(Mobile from, object targeted)
            {
                if (!(targeted is PlayerMobile player))
                {
                    from.SendMessage(0x26, "Alvo deve ser um jogador.");
                    return;
                }

                Instance.UpdateProgress(player, _questId, _objIndex, _amount);
                from.SendMessage(0x40, $"Adicionado {_amount} progresso ao objetivo {_objIndex}.");
            }
        }

        private class QuestListActiveTarget : Server.Targeting.Target
        {
            public QuestListActiveTarget() : base(-1, false, Server.Targeting.TargetFlags.None)
            {
            }

            protected override void OnTarget(Mobile from, object targeted)
            {
                if (!(targeted is PlayerMobile player))
                {
                    from.SendMessage(0x26, "Alvo deve ser um jogador.");
                    return;
                }

                var data = Instance.GetPlayerData(player);

                from.SendMessage(0x480, $"=== Quests Ativas de {player.Name} ===");

                if (data.ActiveQuests.Count == 0)
                {
                    from.SendMessage(0x480, "Nenhuma quest ativa.");
                    return;
                }

                foreach (var quest in data.ActiveQuests)
                {
                    from.SendMessage(0x480, $"[{quest.Status}] {quest.QuestId}: {quest.QuestName}");

                    for (int i = 0; i < quest.Progress.Count; i++)
                    {
                        var p = quest.Progress[i];
                        string status = p.IsComplete ? "OK" : $"{p.Current}/{p.Required}";
                        from.SendMessage(0x480, $"  {i}. {p.Description} [{status}]");
                    }
                }
            }
        }

        #endregion
    }
}