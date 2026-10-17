/*****************************************************
 * QuestRepository.cs
 * Repositório central de quests
 * Etapa 4 - QuestDefinition
 * 
 * Versão: 1.0.0
 * Compatível com: ServUO
 *****************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Commands;

namespace Server.Custom.QuestSystem.Quests
{
    /// <summary>
    /// Repositório central de todas as quests
    /// </summary>
    public class QuestRepository
    {
        #region Singleton

        private static QuestRepository _instance;
        private static readonly object _lock = new object();

        public static QuestRepository Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new QuestRepository();
                        }
                    }
                }
                return _instance;
            }
        }

        #endregion

        #region Fields

        private Dictionary<string, QuestDefinition> _quests;
        private Dictionary<string, List<string>> _questChains;
        private Dictionary<string, List<string>> _questsByNpc;
        private bool _isLoaded;

        #endregion

        #region Properties

        /// <summary>
        /// Total de quests carregadas
        /// </summary>
        public int QuestCount => _quests.Count;

        /// <summary>
        /// Quests foram carregadas
        /// </summary>
        public bool IsLoaded => _isLoaded;

        #endregion

        #region Constructor

        private QuestRepository()
        {
            _quests = new Dictionary<string, QuestDefinition>(StringComparer.OrdinalIgnoreCase);
            _questChains = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            _questsByNpc = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Inicializa o repositório
        /// </summary>
        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized)
                return;

            _initialized = true;

            Instance.LoadQuests();
            RegisterCommands();
        }

        /// <summary>
        /// Carrega todas as quests
        /// </summary>
        public void LoadQuests()
        {
            _quests.Clear();
            _questChains.Clear();
            _questsByNpc.Clear();

            var loadedQuests = QuestLoader.LoadAllQuests();

            foreach (var quest in loadedQuests)
            {
                if (quest == null || string.IsNullOrEmpty(quest.Id))
                    continue;

                // Valida
                if (!quest.Validate(out List<string> errors))
                {
                    Console.WriteLine($"[QuestRepository] Quest '{quest.Id}' invalida:");
                    foreach (var error in errors)
                        Console.WriteLine($"  - {error}");
                    continue;
                }

                // Adiciona ao repositório
                _quests[quest.Id] = quest;

                // Indexa por chain (sem duplicar: reloads repetidos inflavam a lista)
                if (!string.IsNullOrEmpty(quest.ChainId))
                {
                    if (!_questChains.ContainsKey(quest.ChainId))
                        _questChains[quest.ChainId] = new List<string>();

                    if (!_questChains[quest.ChainId].Contains(quest.Id))
                        _questChains[quest.ChainId].Add(quest.Id);
                }

                // Indexa por NPC — giver E turn-in.
                //
                // Correção: antes só o giver era indexado. Quando o NPC de
                // entrega era diferente do que oferecia a quest (caso de
                // quest_nh_bandagens: giver = Marcus, turn-in = Helena), a quest
                // não aparecia na lista do NPC de entrega e o jogador nunca
                // conseguia finalizá-la.
                IndexByNpc(quest.GiverNpcId, quest.Id);
                IndexByNpc(quest.TurnInNpcId, quest.Id);
            }

            // Ordena chains
            foreach (var chain in _questChains.Values)
            {
                chain.Sort((a, b) =>
                {
                    var qa = GetQuest(a);
                    var qb = GetQuest(b);
                    return (qa?.ChainOrder ?? 0).CompareTo(qb?.ChainOrder ?? 0);
                });
            }

            _isLoaded = true;
            Console.WriteLine($"[QuestRepository] {_quests.Count} quests indexadas. {_questChains.Count} chains.");
        }

        /// <summary>Registra uma quest no índice de um NPC (sem duplicar).</summary>
        private void IndexByNpc(string npcId, string questId)
        {
            if (string.IsNullOrWhiteSpace(npcId) || string.IsNullOrWhiteSpace(questId))
                return;

            List<string> list;
            if (!_questsByNpc.TryGetValue(npcId, out list))
            {
                list = new List<string>();
                _questsByNpc[npcId] = list;
            }

            if (!list.Contains(questId))
                list.Add(questId);
        }

        /// <summary>
        /// Recarrega quests
        /// </summary>
        public void Reload()
        {
            Console.WriteLine("[QuestRepository] Recarregando quests...");
            LoadQuests();
        }

        #endregion

        #region Quest Access

        /// <summary>
        /// Obtém quest por ID
        /// </summary>
        public QuestDefinition GetQuest(string questId)
        {
            if (string.IsNullOrEmpty(questId))
                return null;

            _quests.TryGetValue(questId, out QuestDefinition quest);
            return quest;
        }

        /// <summary>
        /// Verifica se quest existe
        /// </summary>
        public bool HasQuest(string questId)
        {
            return _quests.ContainsKey(questId);
        }

        /// <summary>
        /// Obtém todas as quests
        /// </summary>
        public List<QuestDefinition> GetAllQuests()
        {
            return _quests.Values.ToList();
        }

        /// <summary>
        /// Obtém quests por categoria
        /// </summary>
        public List<QuestDefinition> GetQuestsByCategory(QuestCategory category)
        {
            return _quests.Values.Where(q => q.Category == category).ToList();
        }

        /// <summary>
        /// Obtém quests por NPC
        /// </summary>
        public List<QuestDefinition> GetQuestsByNpc(string npcId)
        {
            if (string.IsNullOrEmpty(npcId) || !_questsByNpc.ContainsKey(npcId))
                return new List<QuestDefinition>();

            var questIds = _questsByNpc[npcId];
            return questIds.Select(id => GetQuest(id)).Where(q => q != null).ToList();
        }

        /// <summary>
        /// Obtém IDs de quests por NPC
        /// </summary>
        public List<string> GetQuestIdsByNpc(string npcId)
        {
            if (string.IsNullOrEmpty(npcId) || !_questsByNpc.ContainsKey(npcId))
                return new List<string>();

            return new List<string>(_questsByNpc[npcId]);
        }

        #endregion

        #region Quest Chains

        /// <summary>
        /// Obtém quests de uma chain
        /// </summary>
        public List<QuestDefinition> GetQuestChain(string chainId)
        {
            if (string.IsNullOrEmpty(chainId) || !_questChains.ContainsKey(chainId))
                return new List<QuestDefinition>();

            var questIds = _questChains[chainId];
            return questIds.Select(id => GetQuest(id)).Where(q => q != null).ToList();
        }

        /// <summary>
        /// Obtém próxima quest da chain
        /// </summary>
        public QuestDefinition GetNextInChain(string questId)
        {
            var quest = GetQuest(questId);
            if (quest == null || string.IsNullOrEmpty(quest.NextQuestId))
                return null;

            return GetQuest(quest.NextQuestId);
        }

        /// <summary>
        /// Obtém primeira quest da chain
        /// </summary>
        public QuestDefinition GetChainStart(string chainId)
        {
            var chain = GetQuestChain(chainId);
            return chain.FirstOrDefault();
        }

        #endregion

        #region Quest Registration

        /// <summary>
        /// Registra quest manualmente
        /// </summary>
        public void RegisterQuest(QuestDefinition quest)
        {
            if (quest == null || string.IsNullOrEmpty(quest.Id))
                return;

            _quests[quest.Id] = quest;

            // Indexa por NPC
            if (!string.IsNullOrEmpty(quest.GiverNpcId))
            {
                IndexByNpc(quest.GiverNpcId, quest.Id);
                IndexByNpc(quest.TurnInNpcId, quest.Id);
            }
        }

        /// <summary>
        /// Remove quest
        /// </summary>
        public void UnregisterQuest(string questId)
        {
            if (string.IsNullOrEmpty(questId))
                return;

            _quests.Remove(questId);

            // Remove dos índices
            foreach (var list in _questsByNpc.Values)
                list.Remove(questId);

            foreach (var list in _questChains.Values)
                list.Remove(questId);
        }

        #endregion

        #region Commands

        private static void RegisterCommands()
        {
            CommandSystem.Register("questreload", AccessLevel.Administrator, QuestReload_OnCommand);
            CommandSystem.Register("questlist", AccessLevel.GameMaster, QuestList_OnCommand);
            CommandSystem.Register("questinfo", AccessLevel.GameMaster, QuestInfo_OnCommand);
        }

        [Usage("questreload")]
        [Description("Recarrega todas as quests dos arquivos JSON.")]
        private static void QuestReload_OnCommand(CommandEventArgs e)
        {
            Instance.Reload();
            e.Mobile.SendMessage(0x40, $"[QuestSystem] {Instance.QuestCount} quests recarregadas.");

            // As definições acabaram de mudar sob quests que jogadores têm em
            // andamento. ActiveQuest.Progress é um snapshot tirado no aceite, então
            // sem realinhar aqui uma quest editada (objetivo a mais ou a menos)
            // ficava impossível de concluir: os 18 loops do sistema iteram até o
            // menor dos dois tamanhos, e CheckCompletion() tropeça nas entradas
            // órfãs que sobraram.
            int realigned = QuestManager.Instance.ReconcileAllProgress();

            if (realigned > 0)
                e.Mobile.SendMessage(0x3B1, $"[QuestSystem] {realigned} quest(s) em andamento realinhada(s) com as novas definições.");
        }

        [Usage("questlist")]
        [Description("Lista todas as quests carregadas.")]
        private static void QuestList_OnCommand(CommandEventArgs e)
        {
            var quests = Instance.GetAllQuests();
            
            e.Mobile.SendMessage(0x480, $"=== Quests Carregadas ({quests.Count}) ===");
            
            foreach (var quest in quests.OrderBy(q => q.Category).ThenBy(q => q.Name))
            {
                e.Mobile.SendMessage(0x480, $"  [{quest.Category}] {quest.Id}: {quest.Name}");
            }
        }

        [Usage("questinfo <questId>")]
        [Description("Mostra informações detalhadas de uma quest.")]
        private static void QuestInfo_OnCommand(CommandEventArgs e)
        {
            if (e.Arguments.Length < 1)
            {
                e.Mobile.SendMessage(0x26, "Uso: [questinfo <questId>");
                return;
            }

            string questId = e.Arguments[0];
            var quest = Instance.GetQuest(questId);

            if (quest == null)
            {
                e.Mobile.SendMessage(0x26, $"Quest '{questId}' nao encontrada.");
                return;
            }

            e.Mobile.SendMessage(0x480, $"=== {quest.Name} ===");
            e.Mobile.SendMessage(0x480, $"ID: {quest.Id}");
            e.Mobile.SendMessage(0x480, $"Categoria: {quest.Category}");
            e.Mobile.SendMessage(0x480, $"Dificuldade: {quest.Difficulty}");
            e.Mobile.SendMessage(0x480, $"Descricao: {quest.Description}");
            e.Mobile.SendMessage(0x480, $"NPC Giver: {quest.GiverNpcName} ({quest.GiverNpcId})");
            e.Mobile.SendMessage(0x480, $"Repetivel: {quest.Repeatable}");
            e.Mobile.SendMessage(0x480, $"Objetivos: {quest.Objectives.Count}");
            
            for (int i = 0; i < quest.Objectives.Count; i++)
            {
                var obj = quest.Objectives[i];
                e.Mobile.SendMessage(0x480, $"  {i + 1}. [{obj.Type}] {obj.Description} (x{obj.Required})");
            }

            e.Mobile.SendMessage(0x480, $"Recompensas: {quest.Rewards.GetSummary()}");
        }

        #endregion
    }
}