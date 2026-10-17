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
using System.Text;
using Server;
using Server.Mobiles;
using Server.Items;
using Server.Gumps;
using Server.Commands;
using Server.Network;
using Server.Custom.QuestSystem.Dialogue;

// Observacao: NAO importar "Server.Engines.Quests". O ServUO ja possui, nesse
// namespace, tipos com os mesmos nomes usados aqui (QuestObjective,
// EscortObjective, DeliverObjective, QuestItem, BaseQuest, QuestSystem...).
// Importa-lo cria ambiguidade (CS0104) e pode fazer o codigo resolver para a
// classe errada do ServUO em vez da deste sistema.

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

        /// <summary>Timer periodico que aplica expiracao e avisos de tempo.</summary>
        private Timer _timeLimitTimer;

        /// <summary>
        /// Avisos de tempo ja disparados, por "serial|questId". Fica apenas em
        /// memoria: nao precisa sobreviver a restart.
        /// </summary>
        private readonly Dictionary<string, List<int>> _firedWarnings = new Dictionary<string, List<int>();

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
        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized)
                return;

            _initialized = true;

            Instance.Init();
            RegisterCommands();
            QuestEventHandler.Initialize();

            Console.WriteLine("[QuestManager] Sistema de Quests inicializado.");
        }

        private void Init()
        {
            _isInitialized = true;

            StartTimeLimitTimer();
        }

        /// <summary>
        /// Agenda a verificacao periodica de limite de tempo.
        ///
        /// Correcao: CheckTimeLimits so era chamado no login e dentro de
        /// CompleteQuest. Uma quest com prazo de 30 minutos que o jogador
        /// deixasse correr continuava "ativa" indefinidamente enquanto ele
        /// estivesse online — e, pior, podia ser entregue depois de expirada.
        /// </summary>
        private void StartTimeLimitTimer()
        {
            if (_timeLimitTimer != null)
                return;

            _timeLimitTimer = new TimeLimitTimer(this);
            _timeLimitTimer.Start();

            Console.WriteLine("[QuestManager] Verificacao periodica de limite de tempo ativa (30s).");
        }

        /// <summary>Percorre os jogadores online e aplica expiracao/avisos.</summary>
        public void CheckAllTimeLimits()
        {
            // Itera pelas conexoes ativas: PlayerMobile offline nao esta em
            // World.Mobiles, e _playerData pode guardar dados de quem ja saiu.
            foreach (NetState state in NetState.Instances)
            {
                if (state == null)
                    continue;

                PlayerMobile player = state.Mobile as PlayerMobile;

                if (player == null || player.Deleted)
                    continue;

                PlayerQuestData data;
                if (!_playerData.TryGetValue(player.Serial, out data))
                    continue;

                if (data == null || data.ActiveQuests.Count == 0)
                    continue;

                CheckTimeLimits(player);
            }
        }

        /// <summary>Limpa os avisos ja disparados de uma quest.</summary>
        internal void ClearTimeWarnings(PlayerMobile player, string questId)
        {
            if (player == null || string.IsNullOrEmpty(questId))
                return;

            _firedWarnings.Remove(player.Serial.Value + "|" + questId);
        }

        private class TimeLimitTimer : Timer
        {
            private readonly QuestManager _manager;

            public TimeLimitTimer(QuestManager manager)
                : base(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30))
            {
                _manager = manager;
                Priority = TimerPriority.OneSecond;
            }

            protected override void OnTick()
            {
                try
                {
                    _manager.CheckAllTimeLimits();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[QuestManager] Erro no timer de limite de tempo: " + ex.Message);
                }
            }
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
        /// Limpa dados obsoletos de jogadores.
        ///
        /// Estava como "TODO" vazio, então _playerData crescia para sempre:
        /// cada personagem deletado deixava uma entrada órfã no dicionário e
        /// cada falha de quest acumulava um FailedQuest que ninguém nunca lia.
        ///
        /// O que é removido (e o que NUNCA é):
        ///   - entrada inteira cujo personagem não existe mais no mundo E cuja
        ///     atividade mais recente é anterior a maxAge;
        ///   - FailedQuest mais antigo que maxAge (histórico de falhas é só
        ///     cosmético — o retry é permitido independentemente dele);
        ///   - entradas que ficaram completamente vazias após a poda.
        ///
        ///   Nunca toca em ActiveQuest (quest em andamento) nem em
        ///   CompletedQuest (usado por repeatable/maxCompletions/pré-requisito).
        ///   Apagar isso corromperia o progresso de quem só ficou offline.
        /// </summary>
        /// <param name="maxAge">Idade máxima considerada "recente". Zero ou negativo desliga a limpeza.</param>
        /// <returns>Quantas entradas foram removidas do dicionário.</returns>
        public int CleanupOldData(TimeSpan maxAge)
        {
            if (maxAge <= TimeSpan.Zero || _playerData == null || _playerData.Count == 0)
                return 0;

            // Durante o load o mundo ainda está incompleto: FindMobile devolveria
            // null para todo mundo e a limpeza apagaria dados válidos.
            if (World.Loading)
                return 0;

            DateTime cutoff = DateTime.UtcNow - maxAge;
            int removed = 0;
            int prunedFailures = 0;

            var toRemove = new List<Serial>();

            foreach (var pair in _playerData)
            {
                Serial serial = pair.Key;
                PlayerQuestData data = pair.Value;

                if (data == null)
                {
                    toRemove.Add(serial);
                    continue;
                }

                Mobile mobile = World.FindMobile(serial);
                bool characterGone = mobile == null || mobile.Deleted;

                DateTime lastActivity = GetLastActivity(data);

                if (characterGone && lastActivity < cutoff)
                {
                    toRemove.Add(serial);
                    continue;
                }

                // Poda o histórico de falhas (mantém quests ativas e completas)
                if (data.FailedQuests != null && data.FailedQuests.Count > 0)
                {
                    int before = data.FailedQuests.Count;

                    data.FailedQuests.RemoveAll(f => f != null && f.FailedAt < cutoff);
                    data.FailedQuests.RemoveAll(f => f == null);

                    prunedFailures += before - data.FailedQuests.Count;
                }

                // Entrada que ficou totalmente vazia não vale a memória nem o I/O
                if (IsEmpty(data))
                    toRemove.Add(serial);
            }

            for (int i = 0; i < toRemove.Count; i++)
            {
                if (_playerData.Remove(toRemove[i]))
                    removed++;
            }

            if (removed > 0 || prunedFailures > 0)
            {
                Console.WriteLine("[QuestManager] CleanupOldData: " + removed +
                                  " entrada(s) removida(s), " + prunedFailures +
                                  " falha(s) antiga(s) podada(s). Restam " + _playerData.Count + ".");
            }

            return removed;
        }

        /// <summary>Atividade mais recente entre quests ativas, completas e falhas.</summary>
        private static DateTime GetLastActivity(PlayerQuestData data)
        {
            DateTime latest = DateTime.MinValue;

            if (data.ActiveQuests != null)
            {
                foreach (var active in data.ActiveQuests)
                {
                    if (active == null)
                        continue;

                    if (active.LastUpdate > latest) latest = active.LastUpdate;
                    if (active.AcceptedAt > latest) latest = active.AcceptedAt;
                }
            }

            if (data.CompletedQuests != null)
            {
                foreach (var completed in data.CompletedQuests)
                {
                    if (completed != null && completed.CompletedAt > latest)
                        latest = completed.CompletedAt;
                }
            }

            if (data.FailedQuests != null)
            {
                foreach (var failed in data.FailedQuests)
                {
                    if (failed != null && failed.FailedAt > latest)
                        latest = failed.FailedAt;
                }
            }

            // Sem nenhuma marca de tempo (dados antigos/migrados): considera
            // "agora" para não apagar o que não sabemos datar.
            return latest == DateTime.MinValue ? DateTime.UtcNow : latest;
        }

        private static bool IsEmpty(PlayerQuestData data)
        {
            return (data.ActiveQuests == null || data.ActiveQuests.Count == 0) &&
                   (data.CompletedQuests == null || data.CompletedQuests.Count == 0) &&
                   (data.FailedQuests == null || data.FailedQuests.Count == 0) &&
                   (data.QuestFlags == null || data.QuestFlags.Count == 0);
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

            var status = data.GetQuestStatus(questId);

            // Sem isto, QuestStatus.Locked existia no enum mas nunca era usado:
            // uma quest com pré-requisitos aparecia como "Available" na lista do
            // NPC para QUALQUER jogador, e só era barrada no momento do aceite.
            // O requisito é o oposto — a quest não deve nem aparecer (ex.: a do
            // ladino só existe para quem tem Stealing e Snooping).
            if (status == QuestStatus.Available && player != null)
            {
                var definition = QuestRepository.Instance.GetQuest(questId);

                if (definition != null && definition.Prerequisites != null &&
                    !definition.Prerequisites.Check(player, out _))
                {
                    return QuestStatus.Locked;
                }
            }

            return status;
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

            // Inicia drivers de objetivos que precisam de processo proprio
            // (escolta, defesa, sobrevivencia). Sem isto esses objetivos
            // ficavam parados para sempre.
            QuestObjectiveDrivers.StartForQuest(this, player, definition, activeQuest);
            ClearTimeWarnings(player, questId);

            // Sincroniza Collect com o que o jogador JA tem no inventario
            SyncCollectObjectives(player, definition, activeQuest, notify: true);

            // Sincroniza SkillGain: sem isto, quem JA tinha a skill acima do alvo
            // ficava travado para sempre — o objetivo so credita em OnSkillGain,
            // e um jogador no cap nunca mais ganha ponto naquela skill.
            SyncSkillGainObjectives(player, definition, activeQuest, notify: true);

            // Avalia Explore imediatamente: o jogador pode ter aceitado a
            // missao ja em pe dentro da area (antes so o evento de movimento
            // contava, entao quem nao andasse mais nao completava nunca).
            OnLocationEntered(player, player.Location, player.Map);

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

            // Limite de tempo: uma quest expirada nao pode ser entregue
            CheckTimeLimits(player);
            if (!data.HasActiveQuest(questId))
            {
                message = "O tempo desta missao expirou.";
                return false;
            }

            if (active.Status != QuestStatus.ReadyToTurnIn)
            {
                message = BuildPendingObjectivesMessage(player, definition, active);
                return false;
            }

            // Encerra drivers (escolta, defesa, sobrevivencia) ligados a esta quest
            QuestObjectiveDrivers.StopForQuest(player, questId);
            ClearTimeWarnings(player, questId);

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
        /// Monta a mensagem de "o que ainda falta", listando cada objetivo
        /// pendente com seu progresso. Substitui o antigo texto generico
        /// "Voce ainda nao completou todos os objetivos", que nao dizia NADA
        /// e deixava o jogador sem saber por que a missao nao fechava.
        /// </summary>
        private string BuildPendingObjectivesMessage(PlayerMobile player, QuestDefinition definition, ActiveQuest active)
        {
            var pending = new List<string>();

            for (int i = 0; i < definition.Objectives.Count && i < active.Progress.Count; i++)
            {
                var progress = active.Progress[i];

                if (progress.IsComplete || progress.IsOptional)
                    continue;

                string description = string.IsNullOrWhiteSpace(progress.Description)
                    ? definition.Objectives[i].Description
                    : progress.Description;

                if (string.IsNullOrWhiteSpace(description))
                    description = definition.Objectives[i].Type.ToString();

                pending.Add($"{description} ({progress.Current}/{progress.Required})");
            }

            if (pending.Count == 0)
                return "Voce ainda nao completou todos os objetivos.";

            if (player != null)
            {
                player.SendMessage(QuestConstants.HueWarning, "Ainda falta:");

                foreach (string line in pending)
                    player.SendMessage(QuestConstants.HueInfo, "  - " + line);
            }

            return $"Ainda faltam {pending.Count} objetivo(s) para concluir.";
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

            // Encerra drivers (escolta em campo, ondas de defesa, timers)
            QuestObjectiveDrivers.StopForQuest(player, questId);
            ClearTimeWarnings(player, questId);

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
            if (data == null)
                return;

            var active = data.GetActiveQuest(questId);

            // ReadyToTurnIn tambem aceita progresso: objetivos opcionais podem
            // continuar sendo feitos depois que os obrigatorios terminaram.
            if (active == null ||
                (active.Status != QuestStatus.Active && active.Status != QuestStatus.ReadyToTurnIn))
                return;

            var definition = QuestRepository.Instance.GetQuest(questId);
            if (definition == null)
                return;

            if (!definition.IsObjectiveActive(objectiveIndex, active.Progress))
                return;

            bool wasReady = active.Status == QuestStatus.ReadyToTurnIn;

            bool completed = active.UpdateProgress(objectiveIndex, amount);

            if (objectiveIndex < definition.Objectives.Count && objectiveIndex < active.Progress.Count)
            {
                var objective = definition.Objectives[objectiveIndex];
                var progress = active.Progress[objectiveIndex];

                QuestNotificationManager.Instance.NotifyObjectiveProgress(
                    player, definition, objective, progress.Current, progress.Required);

                if (completed)
                    QuestNotificationManager.Instance.NotifyObjectiveComplete(player, definition, objective);
            }

            // So notifica na TRANSICAO para ReadyToTurnIn (antes spamava a cada passo)
            if (!wasReady && active.Status == QuestStatus.ReadyToTurnIn)
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
        /// Processa evento de kill.
        /// Credita objetivos Kill e Destroy (quando o alvo e uma criatura) e
        /// rola o drop de itens de coleta declarados em FromMobTypes/DropChance.
        /// </summary>
        /// <param name="isPartyKill">Credito veio de um membro da party (nao do killer direto).</param>
        /// <param name="isPetKill">Credito veio de um pet/criatura invocada do jogador.</param>
        public void OnCreatureKilled(PlayerMobile player, Mobile killed, bool isPartyKill = false, bool isPetKill = false)
        {
            if (player == null || player.Deleted || killed == null)
                return;

            var data = GetPlayerData(player);
            if (data == null || data.ActiveQuests.Count == 0)
                return;

            string killedType = killed.GetType().Name;

            // Gera itens de quest que o mob carrega (CollectObjective.FromMobTypes)
            RollMobDrops(player, killed, killedType);

            foreach (var active in data.ActiveQuests.ToList())
            {
                if (active.Status != QuestStatus.Active)
                    continue;

                var definition = QuestRepository.Instance.GetQuest(active.QuestId);
                if (definition == null)
                    continue;

                for (int i = 0; i < definition.Objectives.Count && i < active.Progress.Count; i++)
                {
                    if (!definition.IsObjectiveActive(i, active.Progress))
                        continue;

                    var objective = definition.Objectives[i];

                    if (objective is KillObjective killObj)
                    {
                        if (isPartyKill && !killObj.CountPartyKills)
                            continue;

                        if (isPetKill && !killObj.CountPetKills)
                            continue;

                        if (!MatchesMobile(killObj.TargetType, killObj.TargetTypes, killObj.TargetName, killed, killedType))
                            continue;

                        if (!RegionMatches(player, killObj.RequiredRegion))
                            continue;

                        UpdateProgress(player, active.QuestId, i, 1);
                    }
                    else if (objective is DestroyObjective destroyObj)
                    {
                        // Destroy tambem pode ser uma criatura/estrutura viva
                        if (!MatchesMobile(destroyObj.TargetType, null, destroyObj.TargetName, killed, killedType))
                            continue;

                        if (destroyObj.TargetSerials != null && destroyObj.TargetSerials.Count > 0 &&
                            !destroyObj.TargetSerials.Contains(killed.Serial))
                            continue;

                        UpdateProgress(player, active.QuestId, i, 1);
                    }
                    else if (objective is DefendObjective defendObj)
                    {
                        QuestObjectiveDrivers.NotifyDefendKill(this, player, active, definition, i, defendObj, killed, killedType);
                    }
                }
            }
        }

        /// <summary>
        /// Compara uma criatura morta com o alvo declarado no objetivo.
        /// Aceita: nome da classe C# (exatamente ou como subclasse), lista de
        /// classes e o nome exibido da criatura (para bosses nomeados).
        /// </summary>
        private static bool MatchesMobile(string targetType, List<string> targetTypes, string targetName,
                                          Mobile killed, string killedType)
        {
            bool hasFilter = !string.IsNullOrWhiteSpace(targetType) ||
                             (targetTypes != null && targetTypes.Count > 0) ||
                             !string.IsNullOrWhiteSpace(targetName);

            // Sem filtro declarado: qualquer criatura conta (objetivo generico).
            if (!hasFilter)
                return killed is BaseCreature;

            if (!string.IsNullOrWhiteSpace(targetType))
            {
                if (TargetTypeMatches(killed, killedType, targetType))
                    return true;
            }

            if (targetTypes != null)
            {
                for (int t = 0; t < targetTypes.Count; t++)
                {
                    if (TargetTypeMatches(killed, killedType, targetTypes[t]))
                        return true;
                }
            }

            // Nome exibido da criatura — cobre casos como targetType "Esqueleto"
            // onde o GM escreveu o nome em portugues e nao a classe "Skeleton".
            if (!string.IsNullOrWhiteSpace(killed.Name))
            {
                string normalizedName = QuestItemMatcher.Normalize(killed.Name);

                if (!string.IsNullOrWhiteSpace(targetType) &&
                    normalizedName.Contains(QuestItemMatcher.Normalize(targetType)))
                    return true;

                if (!string.IsNullOrWhiteSpace(targetName) &&
                    normalizedName.Contains(QuestItemMatcher.Normalize(targetName)))
                    return true;
            }

            return false;
        }

        private static bool TargetTypeMatches(Mobile killed, string killedType, string spec)
        {
            if (string.IsNullOrWhiteSpace(spec))
                return false;

            if (killedType.Equals(spec, StringComparison.OrdinalIgnoreCase))
                return true;

            // ResolveAnyType (e nao ResolveType): ResolveType descarta tudo que
            // nao for Item, entao devolvia null para qualquer criatura e o match
            // por heranca nunca acontecia — so o nome exato da classe funcionava.
            Type real = QuestItemMatcher.ResolveAnyType(spec);
            if (real != null && real.IsInstanceOfType(killed))
                return true;

            return QuestItemMatcher.Normalize(killed.Name ?? string.Empty)
                       .Contains(QuestItemMatcher.Normalize(spec));
        }

        /// <summary>
        /// Verifica se o jogador esta na regiao exigida pelo objetivo.
        /// Aceita o nome exato da regiao ou qualquer regiao pai (IsPartOf).
        /// </summary>
        private static bool RegionMatches(PlayerMobile player, string regionName)
        {
            if (string.IsNullOrWhiteSpace(regionName))
                return true;

            if (player == null || player.Region == null)
                return false;

            if (player.Region.Name != null &&
                player.Region.Name.Equals(regionName, StringComparison.OrdinalIgnoreCase))
                return true;

            try
            {
                return player.Region.IsPartOf(regionName);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Rola o drop de itens de quest definidos em CollectObjective.FromMobTypes.
        /// Sem isso, objetivos do tipo "colete N itens do mob X" eram impossiveis:
        /// DropChance e FromMobTypes existiam na definicao mas nunca eram usados.
        /// </summary>
        private void RollMobDrops(PlayerMobile player, Mobile killed, string killedType)
        {
            if (player == null || killed == null)
                return;

            var data = GetPlayerData(player);
            if (data == null)
                return;

            foreach (var active in data.ActiveQuests.ToList())
            {
                if (active.Status != QuestStatus.Active)
                    continue;

                var definition = QuestRepository.Instance.GetQuest(active.QuestId);
                if (definition == null)
                    continue;

                for (int i = 0; i < definition.Objectives.Count && i < active.Progress.Count; i++)
                {
                    var collect = definition.Objectives[i] as CollectObjective;
                    if (collect == null)
                        continue;

                    if (active.Progress[i].IsComplete)
                        continue;

                    if (collect.FromMobTypes == null || collect.FromMobTypes.Count == 0)
                        continue;

                    bool fromThisMob = false;
                    for (int m = 0; m < collect.FromMobTypes.Count; m++)
                    {
                        if (TargetTypeMatches(killed, killedType, collect.FromMobTypes[m]))
                        {
                            fromThisMob = true;
                            break;
                        }
                    }

                    if (!fromThisMob)
                        continue;

                    int chance = collect.DropChance <= 0 ? 100 : Math.Min(100, collect.DropChance);
                    if (chance < 100 && Utility.Random(100) >= chance)
                        continue;

                    int needed = collect.Required - active.Progress[i].Current;
                    if (needed <= 0)
                        continue;

                    Item drop = QuestItemMatcher.Create(collect.ItemType, collect.ItemName, collect.ItemHue, 1);
                    if (drop == null)
                        continue;

                    Container corpse = killed.Corpse;
                    if (corpse != null && !corpse.Deleted)
                        corpse.DropItem(drop);
                    else
                        drop.MoveToWorld(killed.Location, killed.Map);

                    Console.WriteLine("[QuestManager] Drop de quest gerado: " + collect.ItemType +
                                      " de " + killedType + " para " + player.Name);
                }
            }
        }

        /// <summary>
        /// Processa obtencao de item (EventSink.OnItemObtained / QuestBackpack).
        /// Objetivos Collect sao medidos pelo inventario real do jogador, entao
        /// empilhar, guardar em sub-container ou comprar tambem conta.
        /// </summary>
        public void OnItemCollected(PlayerMobile player, Item item)
        {
            if (player == null || player.Deleted)
                return;

            var data = GetPlayerData(player);
            if (data == null || data.ActiveQuests.Count == 0)
                return;

            foreach (var active in data.ActiveQuests.ToList())
            {
                if (active.Status != QuestStatus.Active && active.Status != QuestStatus.ReadyToTurnIn)
                    continue;

                var definition = QuestRepository.Instance.GetQuest(active.QuestId);
                if (definition == null)
                    continue;

                bool changed = SyncCollectObjectives(player, definition, active, notify: true);

                if (changed)
                    AtualizarMiniGump(player, active.QuestId);
            }
        }

        /// <summary>
        /// Processa entrada em regiao (EventSink.OnEnterRegion).
        /// Cobre Explore por nome de regiao e por coordenadas apos teleporte.
        /// </summary>
        public void OnRegionEntered(PlayerMobile player, Region oldRegion, Region newRegion)
        {
            if (player == null || player.Deleted)
                return;

            EvaluateExploreObjectives(player, player.Location, player.Map, newRegion);
        }

        /// <summary>
        /// Processa entrada em local (movimento, teleporte, login).
        /// </summary>
        public void OnLocationEntered(PlayerMobile player, Point3D location, Map map)
        {
            if (player == null || player.Deleted)
                return;

            EvaluateExploreObjectives(player, location, map, player.Region);
        }

        /// <summary>
        /// Avalia todos os objetivos Explore ativos do jogador contra uma posicao.
        /// </summary>
        /// <remarks>
        /// Correcoes aplicadas aqui:
        ///  - usa a posicao INFORMADA (o evento Movement do ServUO dispara ANTES
        ///    de o Mobile mudar de tile, entao player.Location ainda era o tile antigo);
        ///  - e chamado tambem por teleporte, entrada de regiao e login, entao
        ///    chegar de moongate/recall agora conclui o objetivo;
        ///  - progresso e setado para o valor cheio de uma vez (antes somava +1
        ///    a cada passo e podia estourar o Required ou nunca disparar).
        /// </remarks>
        private void EvaluateExploreObjectives(PlayerMobile player, Point3D location, Map map, Region region)
        {
            var data = GetPlayerData(player);
            if (data == null || data.ActiveQuests.Count == 0)
                return;

            foreach (var active in data.ActiveQuests.ToList())
            {
                if (active.Status != QuestStatus.Active && active.Status != QuestStatus.ReadyToTurnIn)
                    continue;

                var definition = QuestRepository.Instance.GetQuest(active.QuestId);
                if (definition == null)
                    continue;

                for (int i = 0; i < definition.Objectives.Count && i < active.Progress.Count; i++)
                {
                    var explore = definition.Objectives[i] as ExploreObjective;
                    if (explore == null)
                        continue;

                    if (!definition.IsObjectiveActive(i, active.Progress))
                        continue;

                    if (!IsInsideExploreArea(player, explore, location, map, region))
                    {
                        // Saiu da area antes de cumprir o tempo de permanencia
                        QuestObjectiveDrivers.CancelStay(player, active.QuestId, i);
                        continue;
                    }

                    if (explore.StayDurationSeconds > 0)
                    {
                        QuestObjectiveDrivers.BeginStay(this, player, active.QuestId, i, explore.StayDurationSeconds);
                        continue;
                    }

                    int needed = explore.Required - active.Progress[i].Current;
                    if (needed > 0)
                        UpdateProgress(player, active.QuestId, i, needed);
                }
            }
        }

        /// <summary>
        /// True quando o jogador esta dentro da area do objetivo Explore.
        /// Suporta coordenadas+raio e/ou nome de regiao.
        /// </summary>
        private static bool IsInsideExploreArea(PlayerMobile player, ExploreObjective explore,
                                                Point3D location, Map map, Region region)
        {
            bool hasCoords = explore.LocationX != 0 || explore.LocationY != 0;
            bool hasRegion = !string.IsNullOrWhiteSpace(explore.LocationName);

            // Nada configurado: objetivo invalido (reportado pelo [questvalidate)
            if (!hasCoords && !hasRegion)
                return false;

            if (hasCoords)
            {
                Map targetMap = explore.GetMap();

                if (map != null && targetMap != null && map != targetMap)
                    return false;

                int radius = explore.RadiusTiles > 0 ? explore.RadiusTiles : 1;

                if (player != null)
                {
                    if (!player.InRange(new Point2D(explore.LocationX, explore.LocationY), radius))
                        return false;
                }
                else
                {
                    int dx = location.X - explore.LocationX;
                    int dy = location.Y - explore.LocationY;

                    if (dx * dx + dy * dy > radius * radius)
                        return false;
                }

                // Se so ha coordenadas, ja passou
                if (!hasRegion)
                    return true;
            }

            if (hasRegion)
            {
                Region check = region ?? (player != null ? player.Region : null);

                if (check == null)
                    return false;

                if (check.Name != null && check.Name.Equals(explore.LocationName, StringComparison.OrdinalIgnoreCase))
                    return true;

                try
                {
                    if (check.IsPartOf(explore.LocationName))
                        return true;
                }
                catch
                {
                    // nome de regiao inexistente
                }

                return false;
            }

            return true;
        }

        /// <summary>
        /// True quando o jogador tem algum objetivo Explore pendente.
        /// Usado para decidir se vale a pena reagir a eventos de movimento.
        /// </summary>
        public bool HasPendingExploreObjective(PlayerMobile player)
        {
            var data = GetPlayerData(player);
            if (data == null)
                return false;

            foreach (var active in data.ActiveQuests)
            {
                if (active.Status != QuestStatus.Active && active.Status != QuestStatus.ReadyToTurnIn)
                    continue;

                var definition = QuestRepository.Instance.GetQuest(active.QuestId);
                if (definition == null)
                    continue;

                for (int i = 0; i < definition.Objectives.Count && i < active.Progress.Count; i++)
                {
                    if (definition.Objectives[i] is ExploreObjective && definition.IsObjectiveActive(i, active.Progress))
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Processa uso de item (EventSink.OnItemUse) — objetivos UseItem.
        /// </summary>
        public void OnItemUsed(PlayerMobile player, Item item)
        {
            if (player == null || player.Deleted || item == null || item.Deleted)
                return;

            var data = GetPlayerData(player);
            if (data == null || data.ActiveQuests.Count == 0)
                return;

            foreach (var active in data.ActiveQuests.ToList())
            {
                if (active.Status != QuestStatus.Active && active.Status != QuestStatus.ReadyToTurnIn)
                    continue;

                var definition = QuestRepository.Instance.GetQuest(active.QuestId);
                if (definition == null)
                    continue;

                for (int i = 0; i < definition.Objectives.Count && i < active.Progress.Count; i++)
                {
                    var useObj = definition.Objectives[i] as UseItemObjective;
                    if (useObj == null)
                        continue;

                    if (!definition.IsObjectiveActive(i, active.Progress))
                        continue;

                    if (!QuestItemMatcher.Matches(item, useObj.ItemType, useObj.ItemName, useObj.ItemHue))
                        continue;

                    UpdateProgress(player, active.QuestId, i, 1);

                    if (useObj.ConsumeOnUse)
                        QuestItemMatcher.Consume(player, useObj.ItemType, useObj.ItemName, useObj.ItemHue, 1);
                }
            }
        }

        /// <summary>
        /// Processa destruicao de um objeto do mundo — objetivos Destroy.
        /// Pode ser chamado por itens custom (OnDoubleClick/OnDelete) ou pelo
        /// evento de morte quando o alvo e uma criatura.
        /// </summary>
        public void OnObjectDestroyed(PlayerMobile player, object target)
        {
            if (player == null || player.Deleted || target == null)
                return;

            var data = GetPlayerData(player);
            if (data == null || data.ActiveQuests.Count == 0)
                return;

            Item targetItem = target as Item;
            Mobile targetMobile = target as Mobile;

            string targetType = target.GetType().Name;
            string targetName = targetItem != null ? targetItem.Name : (targetMobile != null ? targetMobile.Name : null);
            Serial targetSerial = targetItem != null ? targetItem.Serial
                                : (targetMobile != null ? targetMobile.Serial : Serial.MinusOne);

            foreach (var active in data.ActiveQuests.ToList())
            {
                if (active.Status != QuestStatus.Active && active.Status != QuestStatus.ReadyToTurnIn)
                    continue;

                var definition = QuestRepository.Instance.GetQuest(active.QuestId);
                if (definition == null)
                    continue;

                for (int i = 0; i < definition.Objectives.Count && i < active.Progress.Count; i++)
                {
                    var destroyObj = definition.Objectives[i] as DestroyObjective;
                    if (destroyObj == null)
                        continue;

                    if (!definition.IsObjectiveActive(i, active.Progress))
                        continue;

                    bool matches = false;

                    if (!string.IsNullOrWhiteSpace(destroyObj.TargetType))
                    {
                        // ResolveAnyType: o alvo de Destroy pode ser criatura OU
                        // estrutura; ResolveType descartava tudo que nao era Item.
                        Type real = QuestItemMatcher.ResolveAnyType(destroyObj.TargetType);

                        if (real != null && real.IsInstanceOfType(target))
                            matches = true;
                        else if (targetType.Equals(destroyObj.TargetType, StringComparison.OrdinalIgnoreCase))
                            matches = true;
                        else if (targetItem != null &&
                                 QuestItemMatcher.Matches(targetItem, destroyObj.TargetType, destroyObj.TargetName,
                                                          0, true))
                            matches = true;
                    }

                    if (!matches && !string.IsNullOrWhiteSpace(destroyObj.TargetName) && !string.IsNullOrWhiteSpace(targetName))
                        matches = QuestItemMatcher.Normalize(targetName).Contains(QuestItemMatcher.Normalize(destroyObj.TargetName));

                    if (!matches && !string.IsNullOrWhiteSpace(destroyObj.TargetType) && !string.IsNullOrWhiteSpace(targetName))
                        matches = QuestItemMatcher.Normalize(targetName).Contains(QuestItemMatcher.Normalize(destroyObj.TargetType));

                    if (!matches)
                        continue;

                    if (destroyObj.TargetSerials != null && destroyObj.TargetSerials.Count > 0 &&
                        !destroyObj.TargetSerials.Contains(targetSerial))
                        continue;

                    UpdateProgress(player, active.QuestId, i, 1);
                }
            }
        }

        /// <summary>
        /// Processa a morte do jogador — objetivos Survive com FailOnDeath
        /// e quests configuradas para falhar na morte.
        /// </summary>
        public void OnPlayerDied(PlayerMobile player)
        {
            if (player == null)
                return;

            var data = GetPlayerData(player);
            if (data == null || data.ActiveQuests.Count == 0)
                return;

            foreach (var active in data.ActiveQuests.ToList())
            {
                if (active.Status != QuestStatus.Active)
                    continue;

                var definition = QuestRepository.Instance.GetQuest(active.QuestId);
                if (definition == null)
                    continue;

                for (int i = 0; i < definition.Objectives.Count && i < active.Progress.Count; i++)
                {
                    var survive = definition.Objectives[i] as SurviveObjective;
                    if (survive == null || !survive.FailOnDeath)
                        continue;

                    QuestObjectiveDrivers.CancelStay(player, active.QuestId, i);

                    string message;
                    FailQuest(player, active.QuestId, QuestFailReason.PlayerDied, out message);
                    break;
                }
            }
        }

        /// <summary>
        /// Verifica e aplica expiracao de quests com limite de tempo.
        /// Antes desta rotina ExpiresAt/IsExpired/OnTimeExpire existiam mas
        /// nada nunca os consultava: quests com tempo limite nunca expiravam.
        /// </summary>
        public void CheckTimeLimits(PlayerMobile player)
        {
            var data = GetPlayerData(player);
            if (data == null || data.ActiveQuests.Count == 0)
                return;

            foreach (var active in data.ActiveQuests.ToList())
            {
                if (active.Status == QuestStatus.Completed || active.Status == QuestStatus.Failed)
                    continue;

                var definition = QuestRepository.Instance.GetQuest(active.QuestId);

                if (!active.IsExpired)
                {
                    NotifyTimeWarnings(player, active, definition);
                    continue;
                }

                string onExpire = definition != null && !string.IsNullOrWhiteSpace(definition.OnTimeExpire)
                    ? definition.OnTimeExpire
                    : "Fail";

                if (onExpire.Equals("Nothing", StringComparison.OrdinalIgnoreCase) ||
                    onExpire.Equals("Ignore", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (onExpire.Equals("Reset", StringComparison.OrdinalIgnoreCase) ||
                    onExpire.Equals("ResetQuest", StringComparison.OrdinalIgnoreCase))
                {
                    ResetQuestProgress(player, active.QuestId);
                    continue;
                }

                string message;
                FailQuest(player, active.QuestId, QuestFailReason.TimeExpired, out message);
            }
        }

        /// <summary>
        /// Emite os avisos de tempo restante declarados em "timeLimit.warnings".
        /// Cada marco (em minutos) é avisado uma única vez por quest.
        /// </summary>
        private void NotifyTimeWarnings(PlayerMobile player, ActiveQuest active, QuestDefinition definition)
        {
            if (player == null || active == null || definition == null)
                return;

            if (!definition.HasTimeLimit || !active.ExpiresAt.HasValue)
                return;

            if (definition.TimeWarnings == null || definition.TimeWarnings.Count == 0)
                return;

            TimeSpan remaining = active.ExpiresAt.Value - DateTime.UtcNow;

            if (remaining <= TimeSpan.Zero)
                return;

            int minutesLeft = (int)Math.Ceiling(remaining.TotalMinutes);
            string key = player.Serial.Value + "|" + active.QuestId;

            List<int> fired;
            if (!_firedWarnings.TryGetValue(key, out fired))
            {
                fired = new List<int>();
                _firedWarnings[key] = fired;
            }

            foreach (int mark in definition.TimeWarnings)
            {
                if (mark <= 0 || fired.Contains(mark))
                    continue;

                if (minutesLeft > mark)
                    continue;

                fired.Add(mark);

                QuestNotificationManager.Instance.NotifyTimeWarning(player, definition, minutesLeft);
                break;
            }
        }

        /// <summary>
        /// Zera o progresso de uma quest ativa mantendo-a na lista.
        /// </summary>
        public void ResetQuestProgress(PlayerMobile player, string questId)
        {
            var data = GetPlayerData(player);
            if (data == null)
                return;

            var active = data.GetActiveQuest(questId);
            if (active == null)
                return;

            var definition = QuestRepository.Instance.GetQuest(questId);

            foreach (var progress in active.Progress)
            {
                progress.Current = 0;
                progress.IsComplete = progress.Required <= 0;
            }

            active.Status = QuestStatus.Active;
            active.AcceptedAt = DateTime.UtcNow;

            if (definition != null && definition.HasTimeLimit)
                active.ExpiresAt = DateTime.UtcNow.AddMinutes(definition.TimeLimitMinutes);

            if (player != null)
                player.SendMessage(QuestConstants.HueWarning, "A missao '{0}' reiniciou.", active.QuestName);
        }

        /// <summary>
        /// Processa fala/interacao com NPC — objetivos TalkTo.
        /// </summary>
        /// <remarks>
        /// Correcoes:
        ///  - o NPC agora e identificado por uma lista de IDs (id configurado,
        ///    serial e nome normalizado). Antes só chegava aqui o Serial.ToString(),
        ///    então "targetNpcId": "npc_enfermeira_sera" nunca casava;
        ///  - grantItems usa GiveItemsToPlayer, que cria um QuestItem com tag
        ///    quando a classe nao existe. Antes o item era simplesmente descartado
        ///    ("tipo nao encontrado") e o Collect dependente ficava impossivel.
        /// </remarks>
        public void OnTalkToNpc(PlayerMobile player, Mobile npc, string npcId)
        {
            if (player == null || player.Deleted || npc == null)
                return;

            var data = GetPlayerData(player);
            if (data == null || data.ActiveQuests.Count == 0)
                return;

            List<string> npcIdentifiers = GetNpcIdentifiers(npc, npcId);

            foreach (var active in data.ActiveQuests.ToList())
            {
                if (active.Status != QuestStatus.Active && active.Status != QuestStatus.ReadyToTurnIn)
                    continue;

                var definition = QuestRepository.Instance.GetQuest(active.QuestId);
                if (definition == null)
                    continue;

                bool changed = false;

                for (int i = 0; i < definition.Objectives.Count && i < active.Progress.Count; i++)
                {
                    var talkObj = definition.Objectives[i] as TalkToObjective;
                    if (talkObj == null)
                        continue;

                    if (!definition.IsObjectiveActive(i, active.Progress))
                        continue;

                    if (!MatchesNpc(talkObj.TargetNpcId, talkObj.TargetNpcName, npc, npcIdentifiers))
                        continue;

                    UpdateProgress(player, active.QuestId, i, 1);
                    changed = true;

                    // Entrega os itens prometidos por este objetivo
                    if (active.Progress[i].IsComplete && talkObj.GrantItems != null && talkObj.GrantItems.Count > 0)
                        GiveItemsToPlayer(player, npc, talkObj.GrantItems);
                }

                if (changed)
                {
                    // Re-sincroniza Collect (as cartas acabaram de entrar na mochila)
                    SyncCollectObjectives(player, definition, active, notify: true);
                    AtualizarMiniGump(player, active.QuestId);
                }
            }
        }

        /// <summary>
        /// Monta a lista de identificadores possiveis para um NPC:
        /// id declarado (IQuestNpcIdentity), serial e nome normalizado.
        /// </summary>
        internal static List<string> GetNpcIdentifiers(Mobile npc, string npcId)
        {
            var ids = new List<string>();

            if (!string.IsNullOrWhiteSpace(npcId))
                ids.Add(npcId);

            if (npc == null)
                return ids;

            IQuestNpcIdentity identity = npc as IQuestNpcIdentity;
            if (identity != null && !string.IsNullOrWhiteSpace(identity.QuestNpcId))
                ids.Add(identity.QuestNpcId);

            string serialText = npc.Serial.ToString();
            if (!ids.Contains(serialText))
                ids.Add(serialText);

            string hexText = "0x" + npc.Serial.Value.ToString("X");
            if (!ids.Contains(hexText))
                ids.Add(hexText);

            return ids;
        }

        /// <summary>
        /// Verifica se um NPC satisfaz o alvo declarado num objetivo TalkTo/Deliver.
        /// </summary>
        internal static bool MatchesNpc(string targetNpcId, string targetNpcName, Mobile npc, List<string> npcIdentifiers)
        {
            if (npc == null)
                return false;

            bool hasIdFilter = !string.IsNullOrWhiteSpace(targetNpcId);
            bool hasNameFilter = !string.IsNullOrWhiteSpace(targetNpcName);

            if (!hasIdFilter && !hasNameFilter)
                return false;   // objetivo sem alvo declarado => invalido (ver [questvalidate)

            if (hasIdFilter && npcIdentifiers != null)
            {
                string wanted = QuestItemMatcher.Normalize(targetNpcId);

                for (int i = 0; i < npcIdentifiers.Count; i++)
                {
                    if (string.IsNullOrWhiteSpace(npcIdentifiers[i]))
                        continue;

                    string have = QuestItemMatcher.Normalize(npcIdentifiers[i]);

                    if (have == wanted || have.Contains(wanted) || wanted.Contains(have))
                        return true;
                }
            }

            if (hasNameFilter && !string.IsNullOrWhiteSpace(npc.Name))
            {
                string normalizedName = QuestItemMatcher.Normalize(npc.Name);
                string wantedName = QuestItemMatcher.Normalize(targetNpcName);

                if (normalizedName == wantedName || normalizedName.Contains(wantedName))
                    return true;
            }

            // Ultimo recurso: a classe C# do NPC (ex.: "QuestNPC" subclasses nomeadas)
            if (hasIdFilter &&
                QuestItemMatcher.Normalize(npc.GetType().Name).Contains(QuestItemMatcher.Normalize(targetNpcId)))
                return true;

            return false;
        }

        /// <summary>
        /// Processa interação com item/objeto (DoubleClick, Speech, etc)
        /// </summary>
        public void OnInteract(PlayerMobile player, object target, string interactionType = "DoubleClick", string speech = null)
        {
            if (player == null || player.Deleted || target == null)
                return;

            var data = GetPlayerData(player);
            if (data == null || data.ActiveQuests.Count == 0)
                return;

            string targetTypeName = target.GetType().Name;

            Item targetItem = target as Item;
            Mobile targetMobile = target as Mobile;
            string targetDisplayName = targetItem != null ? targetItem.Name : (targetMobile != null ? targetMobile.Name : null);

            foreach (var active in data.ActiveQuests.ToList())
            {
                if (active.Status != QuestStatus.Active && active.Status != QuestStatus.ReadyToTurnIn)
                    continue;

                var definition = QuestRepository.Instance.GetQuest(active.QuestId);
                if (definition == null)
                    continue;

                for (int i = 0; i < definition.Objectives.Count && i < active.Progress.Count; i++)
                {
                    var interactObj = definition.Objectives[i] as InteractObjective;
                    if (interactObj == null)
                        continue;

                    if (!definition.IsObjectiveActive(i, active.Progress))
                        continue;

                    // Verifica tipo de interação (DoubleClick, Speech, etc)
                    if (!string.IsNullOrWhiteSpace(interactObj.InteractionType) &&
                        !interactObj.InteractionType.Equals(interactionType, StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Se exige uma palavra falada, confere
                    if (!string.IsNullOrWhiteSpace(interactObj.SpeechTrigger))
                    {
                        if (string.IsNullOrWhiteSpace(speech))
                            continue;

                        if (!QuestItemMatcher.Normalize(speech).Contains(QuestItemMatcher.Normalize(interactObj.SpeechTrigger)))
                            continue;
                    }

                    bool matches = false;

                    // Pela classe C# do alvo (ex: "MarcasEscavacao"), aceitando subclasses
                    if (!string.IsNullOrWhiteSpace(interactObj.TargetType))
                    {
                        // ResolveAnyType: o alvo de Interact pode ser criatura ou
                        // objeto; ResolveType descartava tudo que nao era Item.
                        Type real = QuestItemMatcher.ResolveAnyType(interactObj.TargetType);

                        if (real != null && real.IsInstanceOfType(target))
                            matches = true;
                        else if (targetTypeName.Equals(interactObj.TargetType, StringComparison.OrdinalIgnoreCase))
                            matches = true;
                        else if (targetItem != null &&
                                 QuestItemMatcher.Matches(targetItem, interactObj.TargetType, interactObj.TargetName))
                            matches = true;
                    }

                    // Pelo nome do objeto, se definido
                    if (!matches && !string.IsNullOrWhiteSpace(interactObj.TargetName) && !string.IsNullOrWhiteSpace(targetDisplayName))
                    {
                        if (QuestItemMatcher.Normalize(targetDisplayName).Contains(QuestItemMatcher.Normalize(interactObj.TargetName)))
                            matches = true;
                    }

                    if (!matches)
                        continue;

                    // Se a quest exige um objeto específico (por serial), valida
                    if (interactObj.TargetSerials != null && interactObj.TargetSerials.Count > 0)
                    {
                        Serial serial = targetItem != null ? targetItem.Serial
                                      : (targetMobile != null ? targetMobile.Serial : Serial.MinusOne);

                        if (!interactObj.TargetSerials.Contains(serial))
                            continue;
                    }

                    UpdateProgress(player, active.QuestId, i, 1);
                }
            }
        }

        /// <summary>
        /// Cria item de quest com tag de tipo para identificacao.
        /// Se a classe existir no shard, cria a classe real; senao cria um
        /// QuestItem com a tag (assim itens "virtuais" passam a existir).
        /// </summary>
        private Item CreateQuestItem(string itemType, string itemName, int amount)
        {
            return QuestItemMatcher.Create(itemType, itemName, 0, amount);
        }

        /// <summary>
        /// Entrega itens (grantItems de TalkTo, recompensas intermediarias) ao jogador.
        /// Nunca descarta item: se a classe nao existe, cria um QuestItem com tag.
        /// </summary>
        private void GiveItemsToPlayer(PlayerMobile player, Mobile npc, List<ObjectiveGrantItem> items)
        {
            if (player == null || player.Deleted || items == null || items.Count == 0)
                return;

            foreach (var giveItem in items)
            {
                if (string.IsNullOrWhiteSpace(giveItem.ItemType) && string.IsNullOrWhiteSpace(giveItem.ItemName))
                {
                    Console.WriteLine("[QuestManager] grantItem sem itemType/itemName ignorado.");
                    continue;
                }

                int amount = giveItem.Amount > 0 ? giveItem.Amount : 1;

                for (int n = 0; n < amount; n++)
                {
                    // Cria 1 por vez: itens nao-empilhaveis precisam de instancias separadas
                    Item item = QuestItemMatcher.Create(giveItem.ItemType, giveItem.ItemName, 0, 1);

                    if (item == null)
                        continue;

                    bool onGround;
                    if (!QuestItemMatcher.GiveTo(player, item, out onGround))
                        continue;

                    string name = !string.IsNullOrWhiteSpace(giveItem.ItemName) ? giveItem.ItemName : giveItem.ItemType;

                    if (onGround)
                        player.SendMessage(QuestConstants.HueWarning, "Sua mochila esta cheia! {0} foi colocado no chao.", name);
                    else
                        player.SendMessage(QuestConstants.HueSuccess, "{0} entregou a voce: {1}",
                            npc != null ? npc.Name : "O NPC", name);
                }
            }
        }

        /// <summary>
        /// Processa craft de item (EventSink.CraftSuccess)
        /// </summary>
        public void OnItemCrafted(PlayerMobile player, Item item, string craftSystem)
        {
            if (player == null || player.Deleted || item == null)
                return;

            var data = GetPlayerData(player);
            if (data == null || data.ActiveQuests.Count == 0)
                return;

            foreach (var active in data.ActiveQuests.ToList())
            {
                if (active.Status != QuestStatus.Active && active.Status != QuestStatus.ReadyToTurnIn)
                    continue;

                var definition = QuestRepository.Instance.GetQuest(active.QuestId);
                if (definition == null)
                    continue;

                for (int i = 0; i < definition.Objectives.Count && i < active.Progress.Count; i++)
                {
                    var craftObj = definition.Objectives[i] as CraftObjective;
                    if (craftObj == null)
                        continue;

                    if (!definition.IsObjectiveActive(i, active.Progress))
                        continue;

                    if (!QuestItemMatcher.Matches(item, craftObj.ItemType, craftObj.ItemName))
                        continue;

                    // Verifica sistema de craft (vazio = qualquer um)
                    if (!CraftSystemMatches(craftObj.CraftSkill, craftSystem))
                        continue;

                    // Verifica qualidade excepcional
                    if (craftObj.RequireExceptional && !IsExceptional(item))
                        continue;

                    UpdateProgress(player, active.QuestId, i, 1);
                }
            }
        }

        /// <summary>
        /// Casa o "craftSkill" declarado no JSON com o sistema de craft que
        /// realmente produziu o item.
        ///
        /// Por que não é uma comparação direta:
        ///   QuestEventHandler passa como craftSystem o nome da CLASSE DA
        ///   FERRAMENTA (e.Tool.GetType().Name), ou seja "SewingKit",
        ///   "SmithyHammer", "DovetailSaw". Um GM que escreve o óbvio
        ///   ("craftSkill": "Tailoring" ou "Blacksmith") nunca casava, e o
        ///   objetivo Craft ficava impossível de concluir — sem nenhum aviso.
        ///
        ///   Aqui aceitamos os dois: o nome da ferramenta (comportamento antigo,
        ///   preservado) ou o nome do ofício, traduzido pela tabela abaixo.
        /// </summary>
        /// <param name="declared">Valor de "craftSkill" no JSON; vazio = qualquer ofício.</param>
        /// <param name="craftSystem">Nome da classe da ferramenta usada no craft.</param>
        private static bool CraftSystemMatches(string declared, string craftSystem)
        {
            if (string.IsNullOrWhiteSpace(declared))
                return true;    // sem exigência de ofício

            if (string.IsNullOrWhiteSpace(craftSystem))
                return false;   // exigiu ofício mas o evento não trouxe a ferramenta

            declared = declared.Trim();

            // 1) Nome exato da ferramenta (ex.: "SewingKit")
            if (declared.Equals(craftSystem, StringComparison.OrdinalIgnoreCase))
                return true;

            // 2) Nome do ofício (ex.: "Tailoring") → ferramentas daquele ofício
            string[] tools;

            if (_craftToolsBySkill.TryGetValue(NormalizeCraftKey(declared), out tools))
            {
                for (int i = 0; i < tools.Length; i++)
                {
                    if (tools[i].Equals(craftSystem, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// True quando o valor declarado em "craftSkill" é reconhecido — seja um
        /// nome de ofício da tabela, seja uma classe de ferramenta que exista no
        /// shard.
        ///
        /// Exposto para o [questdiag] apontar erro de grafia ANTES de virar uma
        /// quest impossível de concluir.
        /// </summary>
        public static bool IsKnownCraftSkill(string declared)
        {
            if (string.IsNullOrWhiteSpace(declared))
                return false;

            declared = declared.Trim();

            if (_craftToolsBySkill.ContainsKey(NormalizeCraftKey(declared)))
                return true;

            return QuestItemMatcher.ResolveAnyType(declared) != null;
        }

        private static string NormalizeCraftKey(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            var sb = new StringBuilder(value.Length);

            foreach (char c in value)
            {
                if (char.IsLetterOrDigit(c))
                    sb.Append(char.ToLowerInvariant(c));
            }

            return sb.ToString();
        }

        /// <summary>
        /// Ofício → classes de ferramenta que o representam.
        /// Só entram aqui classes cuja existência foi confirmada no ServUO.
        /// </summary>
        private static readonly Dictionary<string, string[]> _craftToolsBySkill =
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                { "blacksmith",       new[] { "SmithyHammer", "SmithHammer", "RunicHammer", "AncientSmithyHammer", "WornHammer" } },
                { "blacksmithing",    new[] { "SmithyHammer", "SmithHammer", "RunicHammer", "AncientSmithyHammer", "WornHammer" } },
                { "smithing",         new[] { "SmithyHammer", "SmithHammer", "RunicHammer", "AncientSmithyHammer", "WornHammer" } },
                { "tailoring",        new[] { "SewingKit", "RunicSewingKit" } },
                { "tailor",           new[] { "SewingKit", "RunicSewingKit" } },
                { "carpentry",        new[] { "DovetailSaw", "RunicDovetailSaw", "DrawKnife" } },
                { "carpenter",        new[] { "DovetailSaw", "RunicDovetailSaw", "DrawKnife" } },
                { "fletching",        new[] { "FletcherTools" } },
                { "bowcraft",         new[] { "FletcherTools" } },
                { "bowyer",           new[] { "FletcherTools" } },
                { "alchemy",          new[] { "MortarPestle" } },
                { "inscription",      new[] { "ScribesPen" } },
                { "scribing",         new[] { "ScribesPen" } },
                { "tinkering",        new[] { "TinkersTools" } },
                { "tinker",           new[] { "TinkersTools" } },
                { "cartography",      new[] { "MapmakersPen" } }
            };

        /// <summary>
        /// Le a propriedade Quality (ItemQuality) de itens craftaveis via reflexao,
        /// porque nem toda classe de item a expoe.
        /// </summary>
        private static bool IsExceptional(Item item)
        {
            if (item == null)
                return false;

            try
            {
                var prop = item.GetType().GetProperty("Quality");

                if (prop != null && prop.PropertyType == typeof(ItemQuality))
                    return (ItemQuality)prop.GetValue(item, null) == ItemQuality.Exceptional;
            }
            catch
            {
                // sem Quality => nao excepcional
            }

            return false;
        }

        /// <summary>
        /// Processa uso de skill (EventSink.SkillCheck)
        /// </summary>
        public void OnSkillUsed(PlayerMobile player, SkillName skill, bool success)
        {
            if (player == null || player.Deleted)
                return;

            var data = GetPlayerData(player);
            if (data == null || data.ActiveQuests.Count == 0)
                return;

            foreach (var active in data.ActiveQuests.ToList())
            {
                if (active.Status != QuestStatus.Active && active.Status != QuestStatus.ReadyToTurnIn)
                    continue;

                var definition = QuestRepository.Instance.GetQuest(active.QuestId);
                if (definition == null)
                    continue;

                for (int i = 0; i < definition.Objectives.Count && i < active.Progress.Count; i++)
                {
                    var useObj = definition.Objectives[i] as UseSkillObjective;
                    if (useObj == null)
                        continue;

                    if (!definition.IsObjectiveActive(i, active.Progress))
                        continue;

                    if (!SkillMatches(useObj.Skill, useObj.SkillName, skill))
                        continue;

                    if (useObj.RequireSuccess && !success)
                        continue;

                    UpdateProgress(player, active.QuestId, i, 1);
                }
            }
        }

        /// <summary>
        /// Processa ganho de skill (EventSink.SkillGain)
        /// </summary>
        public void OnSkillGain(PlayerMobile player, SkillName skill, double newValue)
        {
            if (player == null || player.Deleted)
                return;

            var data = GetPlayerData(player);
            if (data == null || data.ActiveQuests.Count == 0)
                return;

            foreach (var active in data.ActiveQuests.ToList())
            {
                if (active.Status != QuestStatus.Active && active.Status != QuestStatus.ReadyToTurnIn)
                    continue;

                var definition = QuestRepository.Instance.GetQuest(active.QuestId);
                if (definition == null)
                    continue;

                for (int i = 0; i < definition.Objectives.Count && i < active.Progress.Count; i++)
                {
                    var gainObj = definition.Objectives[i] as SkillGainObjective;
                    if (gainObj == null)
                        continue;

                    if (!definition.IsObjectiveActive(i, active.Progress))
                        continue;

                    if (!SkillMatches(gainObj.Skill, gainObj.SkillName, skill))
                        continue;

                    if (string.Equals(gainObj.Mode, "GainAmount", StringComparison.OrdinalIgnoreCase))
                    {
                        UpdateProgress(player, active.QuestId, i, 1);
                        continue;
                    }

                    // Modo padrao: ReachValue
                    if (newValue >= gainObj.TargetValue)
                    {
                        int remaining = gainObj.Required - active.Progress[i].Current;
                        if (remaining > 0)
                            UpdateProgress(player, active.QuestId, i, remaining);
                    }
                }
            }
        }

        /// <summary>
        /// Casa a skill do objetivo com a skill do evento.
        /// Aceita o enum OU o nome em texto — o loader antigo só lia uma das
        /// duas chaves e a outra ficava com o valor padrao (Alchemy),
        /// fazendo o objetivo contar a skill errada.
        /// </summary>
        private static bool SkillMatches(SkillName objectiveSkill, string objectiveSkillName, SkillName actual)
        {
            if (!string.IsNullOrWhiteSpace(objectiveSkillName))
            {
                SkillName parsed;
                if (TryParseSkill(objectiveSkillName, out parsed))
                    return parsed == actual;

                // texto livre (ex.: "Curar") — compara normalizado com o nome do enum
                return QuestItemMatcher.Normalize(objectiveSkillName) == QuestItemMatcher.Normalize(actual.ToString());
            }

            return objectiveSkill == actual;
        }

        /// <summary>Converte texto em SkillName sem o fallback silencioso para Alchemy.</summary>
        internal static bool TryParseSkill(string text, out SkillName skill)
        {
            skill = SkillName.Alchemy;

            if (string.IsNullOrWhiteSpace(text))
                return false;

            return Enum.TryParse<SkillName>(text.Trim(), true, out skill);
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Conta itens na mochila (recursivo) — suporta tipos reais e virtuais.
        /// </summary>
        private int CountItemsInPack(PlayerMobile player, string itemTypeName, int hue = 0)
        {
            return QuestItemMatcher.CountInPack(player, itemTypeName, hue);
        }

        /// <summary>Mantido por compatibilidade; delega para o QuestItemMatcher.</summary>
        private int CountItemsByName(PlayerMobile player, string itemTypeName, int hue = 0)
        {
            return QuestItemMatcher.CountInPack(player, itemTypeName, hue);
        }

        /// <summary>Recupera a tag de tipo de quest de um item.</summary>
        private string GetQuestItemTag(Item item)
        {
            return QuestItemMatcher.GetQuestTag(item);
        }

        /// <summary>
        /// Da itens de quest ao jogador ao aceitar (DeliverObjective.GiveItemOnAccept).
        /// </summary>
        private void GiveQuestItems(PlayerMobile player, QuestDefinition definition)
        {
            if (player == null || definition == null)
                return;

            foreach (var obj in definition.Objectives)
            {
                var deliver = obj as DeliverObjective;
                if (deliver == null || !deliver.GiveItemOnAccept)
                    continue;

                if (string.IsNullOrWhiteSpace(deliver.ItemType) && string.IsNullOrWhiteSpace(deliver.ItemName))
                {
                    Console.WriteLine("[QuestManager] AVISO: Deliver de '" + definition.Id +
                                      "' sem itemType — nada sera entregue ao aceitar.");
                    continue;
                }

                int amount = deliver.Required > 0 ? deliver.Required : 1;

                for (int n = 0; n < amount; n++)
                {
                    Item item = QuestItemMatcher.Create(deliver.ItemType, deliver.ItemName, deliver.ItemHue, 1);

                    if (item == null)
                        continue;

                    bool onGround;
                    QuestItemMatcher.GiveTo(player, item, out onGround);

                    if (onGround)
                        player.SendMessage(QuestConstants.HueWarning, "Sua mochila esta cheia! {0} foi colocado no chao.", item.Name);
                    else
                        player.SendMessage(QuestConstants.HueSuccess, "Voce recebeu: {0}", item.Name);
                }
            }
        }

        /// <summary>
        /// Processa entrega de item a NPC específico.
        /// Chamado pelo NPC ao falar "entregar" ou por drag-drop.
        /// </summary>
        public bool OnDeliverItem(PlayerMobile player, Mobile npc, string npcId, Item draggedItem = null)
        {
            if (player == null || player.Deleted || npc == null)
                return false;

            var data = GetPlayerData(player);
            if (data == null || data.ActiveQuests.Count == 0)
                return false;

            bool anyDelivered = false;
            List<string> npcIdentifiers = GetNpcIdentifiers(npc, npcId);

            foreach (var active in data.ActiveQuests.ToList())
            {
                if (active.Status != QuestStatus.Active && active.Status != QuestStatus.ReadyToTurnIn)
                    continue;

                var definition = QuestRepository.Instance.GetQuest(active.QuestId);
                if (definition == null)
                    continue;

                for (int i = 0; i < definition.Objectives.Count && i < active.Progress.Count; i++)
                {
                    var deliverObj = definition.Objectives[i] as DeliverObjective;
                    if (deliverObj == null)
                        continue;

                    if (active.Progress[i].IsComplete)
                        continue;

                    // ── 1. Valida se este NPC é o destinatário ────────────────
                    if (!MatchesNpc(deliverObj.TargetNpcId, deliverObj.TargetNpcName, npc, npcIdentifiers))
                        continue;

                    if (string.IsNullOrWhiteSpace(deliverObj.ItemType) && string.IsNullOrWhiteSpace(deliverObj.ItemName))
                    {
                        Console.WriteLine("[QuestManager] OnDeliverItem: objetivo Deliver de '" + definition.Id +
                                          "' sem itemType/itemName — impossivel validar.");
                        continue;
                    }

                    // ── 2. Verifica se jogador possui o item ──────────────────
                    int needed = deliverObj.Required - active.Progress[i].Current;
                    if (needed <= 0)
                        needed = deliverObj.Required > 0 ? deliverObj.Required : 1;

                    int countInBag;

                    if (draggedItem != null)
                    {
                        if (!QuestItemMatcher.Matches(draggedItem, deliverObj.ItemType, deliverObj.ItemName, deliverObj.ItemHue))
                        {
                            npc.Say($"Isso nao e o que preciso, {player.Name}.");
                            continue;
                        }

                        // Item arrastado + o resto que estiver no inventario
                        countInBag = Math.Max(1, draggedItem.Amount) +
                                     QuestItemMatcher.CountInPack(player, deliverObj.ItemType, deliverObj.ItemName, deliverObj.ItemHue);
                    }
                    else
                    {
                        countInBag = QuestItemMatcher.CountInPack(player, deliverObj.ItemType, deliverObj.ItemName, deliverObj.ItemHue);
                    }

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
                    if (draggedItem != null && !draggedItem.Deleted)
                    {
                        int take = Math.Min(Math.Max(1, draggedItem.Amount), needed);

                        if (!draggedItem.Stackable || draggedItem.Amount <= take)
                            draggedItem.Delete();
                        else
                            draggedItem.Amount -= take;
                    }

                    QuestItemMatcher.Consume(player, deliverObj.ItemType, deliverObj.ItemName, deliverObj.ItemHue, needed);

                    // ── 4. Atualiza progresso ─────────────────────────────────
                    active.Progress[i].SetProgress(active.Progress[i].Required);

                    Console.WriteLine("[QuestManager] Deliver OK: '" + deliverObj.ItemType + "' entregue a '" +
                                      npc.Name + "' por " + player.Name);

                    active.CheckCompletion();

                    if (active.Status == QuestStatus.ReadyToTurnIn)
                        QuestNotificationManager.Instance.NotifyQuestReadyToTurnIn(player, definition);

                    QuestNotificationManager.Instance.NotifyObjectiveComplete(player, definition, deliverObj);
                    AtualizarMiniGump(player, active.QuestId);

                    anyDelivered = true;
                }
            }

            return anyDelivered;
        }

        /// <summary>
        /// Consome itens de quest na entrega final (objetivos Collect com ConsumeOnComplete).
        /// </summary>
        private void ConsumeQuestItems(PlayerMobile player, QuestDefinition definition, ActiveQuest active)
        {
            if (player == null || player.Deleted || definition == null || active == null)
                return;

            for (int i = 0; i < definition.Objectives.Count && i < active.Progress.Count; i++)
            {
                // ── Deliver: item já foi consumido no OnDeliverItem ───────────
                if (definition.Objectives[i] is DeliverObjective)
                    continue;

                var collect = definition.Objectives[i] as CollectObjective;
                if (collect == null || !collect.ConsumeOnComplete)
                    continue;

                if (!active.Progress[i].IsComplete)
                    continue;

                int toConsume = collect.Required > 0 ? collect.Required : 1;

                int removed = QuestItemMatcher.Consume(player, collect.ItemType, collect.ItemName, collect.ItemHue, toConsume);

                if (removed < toConsume)
                {
                    Console.WriteLine("[QuestManager] AVISO: consume de '" + collect.ItemType +
                                      "' removeu " + removed + " de " + toConsume + " para " + player.Name);
                }
            }
        }

        /// <summary>
        /// Sincroniza objetivos Collect com o inventario real do jogador.
        /// </summary>
        /// <remarks>
        /// Correcao critica: a versao anterior desistia do objetivo quando
        /// ScriptCompiler.FindTypeByName retornava null. Como itens de quest
        /// "virtuais" (CartaLacradaOlin, AmuletoOssoRunas...) NAO tem classe C#,
        /// a sincronizacao nunca acontecia e a quest nunca ficava ReadyToTurnIn.
        /// </remarks>
        private bool SyncCollectObjectives(PlayerMobile player, QuestDefinition definition, ActiveQuest active, bool notify = false)
        {
            if (player == null || player.Deleted || definition == null || active == null)
                return false;

            bool anyUpdated = false;

            for (int i = 0; i < definition.Objectives.Count && i < active.Progress.Count; i++)
            {
                var collectObj = definition.Objectives[i] as CollectObjective;
                if (collectObj == null)
                    continue;

                if (string.IsNullOrWhiteSpace(collectObj.ItemType) && string.IsNullOrWhiteSpace(collectObj.ItemName))
                {
                    Console.WriteLine("[QuestManager] SyncCollect: objetivo " + i + " de '" + definition.Id +
                                      "' sem itemType — impossivel contar.");
                    continue;
                }

                int countInBag = QuestItemMatcher.CountInPack(
                    player, collectObj.ItemType, collectObj.ItemName, collectObj.ItemHue);

                int target = Math.Min(countInBag, collectObj.Required);

                if (target == active.Progress[i].Current)
                    continue;

                bool wasComplete = active.Progress[i].IsComplete;

                active.Progress[i].SetProgress(target);
                anyUpdated = true;

                Console.WriteLine("[QuestManager] SyncCollect: objetivo " + i + " '" + collectObj.ItemType +
                                  "' => " + active.Progress[i].Current + "/" + active.Progress[i].Required);

                if (!notify || player.NetState == null)
                    continue;

                if (active.Progress[i].IsComplete && !wasComplete)
                    QuestNotificationManager.Instance.NotifyObjectiveComplete(player, definition, collectObj);
                else
                    QuestNotificationManager.Instance.NotifyObjectiveProgress(
                        player, definition, collectObj, active.Progress[i].Current, collectObj.Required);
            }

            if (anyUpdated)
                active.CheckCompletion();

            return anyUpdated;
        }

        /// <summary>
        /// Credita objetivos SkillGain (modo ReachValue) cuja meta o jogador JA
        /// atingiu antes de aceitar a quest.
        ///
        /// Por que é necessário:
        ///   O objetivo só progredia em OnSkillGain. Um jogador que já tivesse a
        ///   skill no valor alvo — ou no cap — nunca mais gerava ganho naquela
        ///   skill, então o objetivo ficava em 0 para sempre e a quest era
        ///   impossível de concluir. É a mesma classe de bug que Collect tinha
        ///   (quem já carregava o item não contava), resolvida com SyncCollect.
        ///
        ///   O modo GainAmount mede ganhos FUTUROS por definição, então não é
        ///   sincronizado aqui.
        /// </summary>
        private bool SyncSkillGainObjectives(PlayerMobile player, QuestDefinition definition, ActiveQuest active, bool notify = false)
        {
            if (player == null || player.Deleted || definition == null || active == null)
                return false;

            bool anyUpdated = false;

            for (int i = 0; i < definition.Objectives.Count && i < active.Progress.Count; i++)
            {
                var gainObj = definition.Objectives[i] as SkillGainObjective;
                if (gainObj == null)
                    continue;

                if (string.Equals(gainObj.Mode, "GainAmount", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (gainObj.TargetValue <= 0)
                {
                    Console.WriteLine("[QuestManager] SyncSkillGain: objetivo " + i + " de '" + definition.Id +
                                      "' em modo ReachValue sem targetValue — nunca completa.");
                    continue;
                }

                SkillName skill;
                if (!TryResolveSkillName(gainObj.SkillName, gainObj.Skill, out skill))
                {
                    Console.WriteLine("[QuestManager] SyncSkillGain: objetivo " + i + " de '" + definition.Id +
                                      "' com skill irresolvivel '" + gainObj.SkillName + "'.");
                    continue;
                }

                Skill playerSkill = player.Skills[skill];
                if (playerSkill == null)
                    continue;

                if (playerSkill.Value < gainObj.TargetValue)
                    continue;

                int target = gainObj.Required > 0 ? gainObj.Required : 1;

                if (active.Progress[i].Current >= target)
                    continue;

                bool wasComplete = active.Progress[i].IsComplete;

                active.Progress[i].SetProgress(target);
                anyUpdated = true;

                Console.WriteLine("[QuestManager] SyncSkillGain: objetivo " + i + " '" + skill +
                                  "' ja em " + playerSkill.Value.ToString("F1") + " >= " +
                                  gainObj.TargetValue.ToString("F1") + " => creditado.");

                if (!notify || player.NetState == null)
                    continue;

                if (active.Progress[i].IsComplete && !wasComplete)
                    QuestNotificationManager.Instance.NotifyObjectiveComplete(player, definition, gainObj);
                else
                    QuestNotificationManager.Instance.NotifyObjectiveProgress(
                        player, definition, gainObj, active.Progress[i].Current, target);
            }

            if (anyUpdated)
                active.CheckCompletion();

            return anyUpdated;
        }

        /// <summary>
        /// Resolve a skill de um objetivo para o enum do ServUO.
        ///
        /// Não usa o valor do enum como fallback cego: quando o loader não
        /// consegue resolver o nome, ele deixa Skill no padrão (Alchemy) e
        /// SkillName com o texto cru. Cair em Alchemy nesse caso faria o
        /// objetivo medir a skill errada sem nenhum aviso.
        /// </summary>
        private static bool TryResolveSkillName(string skillName, SkillName fallback, out SkillName skill)
        {
            skill = SkillName.Alchemy;

            if (!string.IsNullOrWhiteSpace(skillName))
            {
                SkillName parsed;

                if (Enum.TryParse(skillName, true, out parsed) && Enum.IsDefined(typeof(SkillName), parsed))
                {
                    skill = parsed;
                    return true;
                }

                return false;   // texto presente mas inválido: não adivinha
            }

            if (Enum.IsDefined(typeof(SkillName), fallback))
            {
                skill = fallback;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Consome itens (tipos reais ou virtuais). Mantido por compatibilidade.
        /// </summary>
        private void ConsumeItemsByName(PlayerMobile player, string itemTypeName, int hue, int toConsume)
        {
            QuestItemMatcher.Consume(player, itemTypeName, hue, toConsume);
        }

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
                    if (string.IsNullOrWhiteSpace(rewardItem.Type) && string.IsNullOrWhiteSpace(rewardItem.Name))
                    {
                        Console.WriteLine("[QuestSystem] Recompensa de item sem 'type' ignorada.");
                        continue;
                    }

                    // Verifica chance
                    if (rewardItem.Chance > 0 && rewardItem.Chance < 100)
                    {
                        if (Utility.Random(100) >= rewardItem.Chance)
                        {
                            Console.WriteLine($"[QuestSystem] Item {rewardItem.Type} falhou no teste de chance.");
                            continue;
                        }
                    }

                    int amount = rewardItem.Amount > 0 ? rewardItem.Amount : 1;

                    // Cria a primeira instancia para saber se e empilhavel
                    Item probe = QuestItemMatcher.Create(rewardItem.Type, rewardItem.Name, rewardItem.Hue, 1);
                    if (probe == null)
                        continue;

                    if (probe.Stackable && amount > 1)
                    {
                        probe.Amount = amount;

                        bool onGround;
                        QuestItemMatcher.GiveTo(player, probe, out onGround);

                        player.SendMessage(QuestConstants.HueSuccess, "Voce recebeu: {0}x {1}",
                            amount, rewardItem.Name ?? probe.Name ?? rewardItem.Type);

                        if (onGround)
                            player.SendMessage(QuestConstants.HueWarning, "Sua mochila esta cheia! Item colocado no chao.");

                        Console.WriteLine($"[QuestSystem] Item stackavel dado: {amount}x {rewardItem.Type}");
                        continue;
                    }

                    // Nao empilhavel: entrega a sondagem e cria o restante
                    for (int i = 0; i < amount; i++)
                    {
                        Item single = i == 0
                            ? probe
                            : QuestItemMatcher.Create(rewardItem.Type, rewardItem.Name, rewardItem.Hue, 1);

                        if (single == null)
                            continue;

                        bool onGround;
                        QuestItemMatcher.GiveTo(player, single, out onGround);

                        if (onGround)
                            player.SendMessage(QuestConstants.HueWarning, "Sua mochila esta cheia! {0} foi colocado no chao.", single.Name);
                    }

                    player.SendMessage(QuestConstants.HueSuccess, "Voce recebeu: {0}x {1}",
                        amount, rewardItem.Name ?? probe.Name ?? rewardItem.Type);

                    Console.WriteLine($"[QuestSystem] Itens dados: {amount}x {rewardItem.Type}");
                }
            }

            // Titles
            // Obs.: este ServUO nao expoe NamePrefix/NameSuffix em Mobile, entao o
            // titulo e registrado como flag de quest (leia com GetQuestFlag) e
            // anunciado ao jogador. Um shard que queira o titulo visivel no nome
            // deve sobrescrever PlayerMobile.ApplyNameSuffix e ler essas flags.
            if (rewards.Titles != null && rewards.Titles.Count > 0)
            {
                var titleData = GetPlayerData(player);

                foreach (var titleReward in rewards.Titles)
                {
                    if (string.IsNullOrWhiteSpace(titleReward.Title))
                        continue;

                    if (titleData != null)
                        titleData.SetFlag("title_" + QuestItemMatcher.Normalize(titleReward.Title), true);

                    player.SendMessage(QuestConstants.HueSuccess, $"Voce recebeu o titulo: {titleReward.Title}");
                    Console.WriteLine($"[QuestSystem] Titulo concedido: {titleReward.Title}");
                }
            }

            // Skill Gains
            if (rewards.SkillGains != null && rewards.SkillGains.Count > 0)
            {
                foreach (var skillReward in rewards.SkillGains)
                {
                    try
                    {
                        SkillName target = ResolveRewardSkill(skillReward);

                        Skill skill = player.Skills[target];
                        if (skill == null)
                            continue;

                        double cap = player.SkillsCap;
                        double total = 0;
                        foreach (Skill s in player.Skills)
                            total += s.Base;

                        if (cap > 0 && total + skillReward.Amount > cap)
                        {
                            player.SendMessage(QuestConstants.HueWarning,
                                "Voce atingiu o limite de skills: o ganho de {0} foi reduzido.", target);

                            double room = cap - total;
                            if (room <= 0)
                                continue;

                            skillReward.Amount = room;
                        }

                        skill.Base += skillReward.Amount;
                        player.SendMessage(QuestConstants.HueSuccess,
                            $"Sua habilidade {target} aumentou em {skillReward.Amount}.");
                        Console.WriteLine($"[QuestSystem] Skill {target} +{skillReward.Amount}");
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

        /// <summary>
        /// Resolve a skill de uma recompensa.
        /// Se o JSON trouxe um nome valido ("Healing"), usa o nome — o enum
        /// Skill sozinho vinha com o padrao Alchemy quando o loader nao
        /// encontrava a chave "skill", dando bonus na skill errada.
        /// </summary>
        private static SkillName ResolveRewardSkill(SkillReward reward)
        {
            if (reward == null)
                return SkillName.Alchemy;

            SkillName parsed;
            if (!string.IsNullOrWhiteSpace(reward.SkillName) && TryParseSkill(reward.SkillName, out parsed))
                return parsed;

            return reward.Skill;
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