/*****************************************************
 * QuestLoader.cs
 * Carrega quests de arquivos JSON
 * 
 * Versão: 1.2.0 - Parser JSON próprio
 * Compatível com: ServUO
 *****************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Server;
using Server.Custom.QuestSystem.Dialogue;

namespace Server.Custom.QuestSystem.Quests
{
    /// <summary>
    /// Carrega definições de quests de JSON
    /// </summary>
    public static class QuestLoader
    {
        #region Constants

        /// <summary>Pasta padrão com os JSON de quests.</summary>
        private static readonly string DefaultQuestsPath = Path.Combine(Core.BaseDirectory, "Data", "Quests");

        /// <summary>Pasta efetiva (pode ser trocada pela configuração).</summary>
        public static string QuestsPath
        {
            get
            {
                string configured = null;

                try { configured = QuestSystemConfig.Instance.QuestsDataPath; }
                catch { configured = null; }

                return string.IsNullOrWhiteSpace(configured) ? DefaultQuestsPath : configured;
            }
        }

        #endregion

        #region Diagnostics

        private static readonly List<string> _warnings = new List<string>();
        private static string _currentFile = "?";

        /// <summary>Problemas encontrados no último carregamento.</summary>
        public static List<string> Warnings { get { return _warnings; } }

        private static void Warn(string message)
        {
            string line = "[" + Path.GetFileName(_currentFile) + "] " + message;
            _warnings.Add(line);
            Console.WriteLine("[QuestLoader] AVISO: " + line);
        }

        #endregion

        #region Load All Quests

        /// <summary>
        /// Carrega todas as quests da pasta configurada.
        ///
        /// Correções desta revisão:
        ///  - IDs duplicados entre arquivos não se sobrescrevem mais em silêncio:
        ///    a primeira definição ganha e o conflito é relatado em Warnings.
        ///    (tutorial_new_haven.json e fortaleza_abalada.json definiam as
        ///    mesmas 3 quests; o resultado dependia da ordem do sistema de
        ///    arquivos e uma das duas ficava impossivel de concluir.)
        ///  - Cadeias de quest não são mais registradas duas vezes.
        /// </summary>
        public static List<QuestDefinition> LoadAllQuests()
        {
            var quests = new List<QuestDefinition>();
            _warnings.Clear();

            string path = QuestsPath;

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                Console.WriteLine("[QuestLoader] Pasta criada: " + path);
                return quests;
            }

            string[] files = Directory.GetFiles(path, "*.json", SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);

            var seen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (string file in files)
            {
                _currentFile = file;

                try
                {
                    var loadedQuests = LoadQuestsFromFile(file);

                    foreach (var quest in loadedQuests)
                    {
                        if (quest == null || string.IsNullOrWhiteSpace(quest.Id))
                        {
                            Warn("Quest sem 'identity.id' foi ignorada.");
                            continue;
                        }

                        string owner;
                        if (seen.TryGetValue(quest.Id, out owner))
                        {
                            Warn("ID duplicado '" + quest.Id + "' — já definido em " + Path.GetFileName(owner) +
                                 ". Esta cópia foi ignorada.");
                            continue;
                        }

                        seen[quest.Id] = file;
                        quests.Add(quest);
                    }

                    Console.WriteLine("[QuestLoader] Carregado: " + Path.GetFileName(file) +
                                      " (" + loadedQuests.Count + " quests)");
                }
                catch (Exception ex)
                {
                    Warn("Erro ao carregar arquivo: " + ex.Message);
                    Console.WriteLine("[QuestLoader] ERRO em " + file + ": " + ex);
                }
            }

            _currentFile = "?";

            Console.WriteLine("[QuestLoader] Total: " + quests.Count + " quests carregadas." +
                              (_warnings.Count > 0 ? " " + _warnings.Count + " aviso(s) — use [questdiag]." : ""));

            return quests;
        }

        /// <summary>Valida uma lista de definições já carregadas (útil após reload).</summary>
        public static List<string> ValidateQuests(List<QuestDefinition> quests)
        {
            var problems = new List<string>();

            if (quests == null)
                return problems;

            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var quest in quests)
            {
                if (quest == null)
                    continue;

                string label = string.IsNullOrEmpty(quest.Id) ? "(sem id)" : quest.Id;

                if (!ids.Add(label))
                    problems.Add(label + ": ID duplicado na coleção.");

                if (string.IsNullOrWhiteSpace(quest.Name))
                    problems.Add(label + ": sem nome.");

                if (quest.Objectives == null || quest.Objectives.Count == 0)
                    problems.Add(label + ": sem objetivos — não pode ser concluída.");

                if (quest.Objectives != null)
                {
                    for (int i = 0; i < quest.Objectives.Count; i++)
                    {
                        var objective = quest.Objectives[i];
                        if (objective == null)
                            problems.Add(label + ": objetivo " + i + " é nulo.");
                        else if (objective.Required <= 0)
                            problems.Add(label + ": objetivo " + i + " (" + objective.Type +
                                         ") tem required <= 0 — nunca completará.");
                    }
                }

                if (!string.IsNullOrWhiteSpace(quest.NextQuestId) &&
                    !ContainsId(quests, quest.NextQuestId))
                {
                    problems.Add(label + ": nextQuest '" + quest.NextQuestId + "' não existe — a cadeia quebra aqui.");
                }

                if (quest.Prerequisites != null && quest.Prerequisites.RequiredQuests != null)
                {
                    foreach (string required in quest.Prerequisites.RequiredQuests)
                    {
                        if (!string.IsNullOrWhiteSpace(required) && !ContainsId(quests, required))
                            problems.Add(label + ": pré-requisito '" + required + "' não existe — quest inalcançável.");
                    }
                }
            }

            return problems;
        }

        private static bool ContainsId(List<QuestDefinition> quests, string id)
        {
            if (quests == null || string.IsNullOrWhiteSpace(id))
                return false;

            foreach (var q in quests)
            {
                if (q != null && string.Equals(q.Id, id, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        #endregion


        #region Load From File

        /// <summary>
        /// Carrega quests de um arquivo JSON específico
        /// </summary>
        public static List<QuestDefinition> LoadQuestsFromFile(string filePath)
        {
            var quests = new List<QuestDefinition>();

            if (!File.Exists(filePath))
                return quests;

            string json = File.ReadAllText(filePath);
            var parser = new SimpleJsonParser();
            var data = parser.Parse(json);

            if (data == null)
                return quests;

            // Verifica se é arquivo com múltiplas quests
            if (data.ContainsKey("Quests"))
            {
                var questsArray = data["Quests"] as List<object>;
                if (questsArray != null)
                {
                    foreach (var item in questsArray)
                    {
                        var questData = item as Dictionary<string, object>;
                        if (questData != null)
                        {
                            var quest = ParseQuestDefinition(questData);
                            if (quest != null)
                                quests.Add(quest);
                        }
                    }
                }
            }
            // Ou quest única
            else if (data.ContainsKey("identity") || data.ContainsKey("Id") || data.ContainsKey("id"))
            {
                var quest = ParseQuestDefinition(data);
                if (quest != null)
                    quests.Add(quest);
            }

            return quests;
        }

        #endregion

        #region Parse Quest Definition

        private static QuestDefinition ParseQuestDefinition(Dictionary<string, object> data)
        {
            if (data == null)
                return null;

            var quest = new QuestDefinition();

            // Identity
            if (data.ContainsKey("identity"))
            {
                var identity = data["identity"] as Dictionary<string, object>;
                if (identity != null)
                {
                    quest.Id = GetString(identity, "id");
                    quest.Name = GetString(identity, "name");
                    quest.Description = GetString(identity, "description");
                    quest.Category = ParseEnum<QuestCategory>(GetString(identity, "category"), QuestCategory.Side);
                    quest.Difficulty = ParseEnum<QuestDifficulty>(GetString(identity, "difficulty"), QuestDifficulty.Normal);
                    quest.ChainId = GetString(identity, "chainId");
                    quest.ChainOrder = GetInt(identity, "chainOrder");
                }
            }
            else
            {
                quest.Id = GetString(data, "id") ?? GetString(data, "Id");
                quest.Name = GetString(data, "name") ?? GetString(data, "Name");
                quest.Description = GetString(data, "description") ?? GetString(data, "Description");
            }

            // NPCs
            if (data.ContainsKey("npcs"))
            {
                var npcs = data["npcs"] as Dictionary<string, object>;
                if (npcs != null)
                {
                    quest.GiverNpcId = GetString(npcs, "giverId");
                    quest.GiverNpcName = GetString(npcs, "giverName");
                    quest.TurnInNpcId = GetString(npcs, "turnInId");
                    quest.TurnInNpcName = GetString(npcs, "turnInName");
                }
            }

            // Settings
            if (data.ContainsKey("settings"))
            {
                var settings = data["settings"] as Dictionary<string, object>;
                if (settings != null)
                {
                    quest.Repeatable = GetBool(settings, "repeatable");
                    quest.CooldownHours = GetInt(settings, "cooldownHours");
                    quest.MaxCompletions = GetInt(settings, "maxCompletions");
                    quest.Shareable = GetBool(settings, "shareableWithParty");
                    quest.AutoStart = GetBool(settings, "autoStart");
                }
            }

            // Time Limit
            if (data.ContainsKey("timeLimit"))
            {
                var timeLimit = data["timeLimit"] as Dictionary<string, object>;
                if (timeLimit != null)
                {
                    quest.HasTimeLimit = GetBool(timeLimit, "enabled");
                    quest.TimeLimitMinutes = GetFirstInt(timeLimit, "minutes", "minutesLimit", "durationMinutes");
                    quest.OnTimeExpire = GetFirstString(timeLimit, "onExpire", "onExpireAction") ?? "Fail";

                    // "warnings" nunca era lido — os avisos de tempo restante
                    // configurados pelo GM simplesmente não existiam.
                    var warnings = GetFirstList(timeLimit, "warnings", "warnAtMinutes");
                    if (warnings != null)
                    {
                        foreach (object w in warnings)
                        {
                            int minutes;
                            if (w != null && int.TryParse(w.ToString(), out minutes) && minutes > 0)
                                quest.TimeWarnings.Add(minutes);
                        }
                    }

                    // Se o GM declarou minutos mas esqueceu "enabled": true,
                    // o limite de tempo era silenciosamente ignorado.
                    if (!quest.HasTimeLimit && quest.TimeLimitMinutes > 0)
                        quest.HasTimeLimit = true;

                    if (quest.HasTimeLimit && quest.TimeLimitMinutes <= 0)
                    {
                        Warn("timeLimit habilitado em '" + quest.Id + "' mas sem 'minutes' válido — limite ignorado.");
                        quest.HasTimeLimit = false;
                    }
                }
            }

            // Prerequisites
            if (data.ContainsKey("prerequisites"))
            {
                quest.Prerequisites = ParsePrerequisites(data["prerequisites"] as Dictionary<string, object>);
            }

            // Objectives
            if (data.ContainsKey("objectives"))
            {
                var objectivesData = data["objectives"];
                List<object> objectivesList = null;

                if (objectivesData is Dictionary<string, object> objDict)
                {
                    quest.SequentialObjectives = GetBool(objDict, "sequential");
                    if (objDict.ContainsKey("list"))
                        objectivesList = objDict["list"] as List<object>;
                }
                else if (objectivesData is List<object>)
                {
                    objectivesList = objectivesData as List<object>;
                }

                if (objectivesList != null)
                {
                    int index = 0;
                    foreach (var item in objectivesList)
                    {
                        var objData = item as Dictionary<string, object>;
                        if (objData != null)
                        {
                            var objective = ParseObjective(objData);
                            if (objective != null)
                            {
                                objective.Index = index++;
                                quest.Objectives.Add(objective);
                            }
                        }
                    }
                }
            }

            // Rewards
            if (data.ContainsKey("rewards"))
            {
                quest.Rewards = ParseRewards(data["rewards"] as Dictionary<string, object>);
            }

            // Dialogue
            if (data.ContainsKey("dialogue"))
            {
                quest.Dialogue = ParseQuestDialogue(data["dialogue"] as Dictionary<string, object>, quest.Id, quest.Name);
            }

            // Next Quest
            quest.NextQuestId = GetString(data, "nextQuest");
            quest.CompletionMessage = GetString(data, "completionMessage");

            return quest;
        }

        #endregion

        #region Parse Objectives

        /// <summary>
        /// Converte o JSON de um objetivo em sua classe concreta.
        ///
        /// Correções desta revisão:
        ///  - Todos os campos declarados nas classes de objetivo são lidos.
        ///    Antes, Escort/Defend/Survive não liam NENHUM campo e
        ///    Collect/Deliver/Interact/Craft/UseSkill/SkillGain liam só parte,
        ///    o que tornava esses objetivos impossíveis de concluir.
        ///  - Nomes de campo alternativos são aceitos (targetType/itemType,
        ///    x/locationX, map/mapName, amount/required, npcId/targetNpcId...).
        ///    Isso conserta as quests geradas pelo QuestCreatorGump, que emite
        ///    targetType/targetName para todo tipo de objetivo.
        ///  - Tipo de objetivo inválido não vira "Kill" silenciosamente: é
        ///    registrado em Warnings e descartado.
        /// </summary>
        private static QuestObjective ParseObjective(Dictionary<string, object> data)
        {
            if (data == null)
                return null;

            string rawType = GetFirstString(data, "type", "objectiveType", "kind");
            ObjectiveType type;

            if (!TryParseObjectiveType(rawType, out type))
            {
                Warn("Objetivo com type desconhecido: '" + rawType + "' — objetivo descartado.");
                return null;
            }

            QuestObjective objective;

            switch (type)
            {
                case ObjectiveType.Kill: objective = ParseKillObjective(data); break;
                case ObjectiveType.Collect: objective = ParseCollectObjective(data); break;
                case ObjectiveType.Deliver: objective = ParseDeliverObjective(data); break;
                case ObjectiveType.Escort: objective = ParseEscortObjective(data); break;
                case ObjectiveType.Defend: objective = ParseDefendObjective(data); break;
                case ObjectiveType.Destroy: objective = ParseDestroyObjective(data); break;
                case ObjectiveType.Explore: objective = ParseExploreObjective(data); break;
                case ObjectiveType.SkillGain: objective = ParseSkillGainObjective(data); break;
                case ObjectiveType.Craft: objective = ParseCraftObjective(data); break;
                case ObjectiveType.UseSkill: objective = ParseUseSkillObjective(data); break;
                case ObjectiveType.Interact: objective = ParseInteractObjective(data); break;
                case ObjectiveType.TalkTo: objective = ParseTalkToObjective(data); break;
                case ObjectiveType.UseItem: objective = ParseUseItemObjective(data); break;
                case ObjectiveType.Survive: objective = ParseSurviveObjective(data); break;
                default:
                    Warn("Type '" + rawType + "' não tem parser — objetivo descartado.");
                    return null;
            }

            if (objective == null)
                return null;

            // Campos comuns
            objective.Type = type;
            objective.Description = GetFirstString(data, "description", "desc", "text") ?? objective.Description;
            objective.LocationHint = GetFirstString(data, "locationHint", "hint", "location") ?? objective.LocationHint;
            objective.IsOptional = GetFirstBool(data, "optional", "isOptional", "Optional");
            objective.IsHidden = GetFirstBool(data, "hidden", "isHidden", "Hidden");

            if (data.ContainsKey("required"))
                objective.Required = GetInt(data, "required", objective.Required);

            if (objective.Required <= 0)
                objective.Required = DefaultRequiredFor(objective);

            ValidateObjective(objective);

            return objective;
        }

        private static int DefaultRequiredFor(QuestObjective objective)
        {
            if (objective is SurviveObjective)
                return 1;

            return 1;
        }

        #region Objetivos concretos

        private static KillObjective ParseKillObjective(Dictionary<string, object> data)
        {
            var o = new KillObjective();

            o.TargetType = GetFirstString(data, "targetType", "mobType", "creatureType", "itemType", "type");
            o.TargetName = GetFirstString(data, "targetName", "mobName", "name");
            o.RequiredRegion = GetFirstString(data, "requiredRegion", "region");
            o.CountPetKills = GetFirstBool(data, "countPetKills", "petsCount", "allowPets");
            o.CountPartyKills = GetFirstBool(data, "countPartyKills", "shareWithParty");

            var types = GetFirstList(data, "targetTypes", "mobTypes", "types");
            if (types != null)
            {
                foreach (object t in types)
                {
                    string s = t == null ? null : t.ToString();
                    if (!string.IsNullOrWhiteSpace(s))
                        o.TargetTypes.Add(s.Trim());
                }
            }

            return o;
        }

        private static CollectObjective ParseCollectObjective(Dictionary<string, object> data)
        {
            var o = new CollectObjective();

            o.ItemType = GetFirstString(data, "itemType", "targetType", "type", "item");
            o.ItemName = GetFirstString(data, "itemName", "targetName", "name");
            o.ItemHue = GetFirstInt(data, "itemHue", "hue", "Hue");
            o.ConsumeOnComplete = GetFirstBool(data, "consumeOnComplete", "consume");
            o.MarkAsQuestItem = GetFirstBool(data, "markAsQuestItem", "questItem");
            o.DropChance = GetFirstInt(data, "dropChance", "chance");

            var from = GetFirstList(data, "fromMobTypes", "fromMobs", "mobTypes");
            if (from != null)
            {
                foreach (object t in from)
                {
                    string s = t == null ? null : t.ToString();
                    if (!string.IsNullOrWhiteSpace(s))
                        o.FromMobTypes.Add(s.Trim());
                }
            }

            return o;
        }

        private static DeliverObjective ParseDeliverObjective(Dictionary<string, object> data)
        {
            var o = new DeliverObjective();

            o.ItemType = GetFirstString(data, "itemType", "targetType", "type", "item");
            o.ItemName = GetFirstString(data, "itemName", "targetName", "name");
            o.ItemHue = GetFirstInt(data, "itemHue", "hue", "Hue");
            o.GiveItemOnAccept = GetFirstBool(data, "giveItemOnAccept", "giveOnAccept");
            o.TargetNpcId = GetFirstString(data, "targetNpcId", "npcId", "turnInNpcId", "deliverTo");
            o.TargetNpcName = GetFirstString(data, "targetNpcName", "npcName", "turnInNpcName");
            o.OnItemLostAction = GetFirstString(data, "onItemLostAction", "onItemLost") ?? "Fail";

            return o;
        }

        private static EscortObjective ParseEscortObjective(Dictionary<string, object> data)
        {
            var o = new EscortObjective();

            o.EscortNpcId = GetFirstString(data, "escortNpcId", "npcId", "targetNpcId");
            o.EscortNpcName = GetFirstString(data, "escortNpcName", "npcName", "targetName", "targetType");
            o.SpawnOnAccept = GetFirstBool(data, "spawnOnAccept", true);
            o.SpawnX = GetFirstInt(data, "spawnX", "x");
            o.SpawnY = GetFirstInt(data, "spawnY", "y");
            o.SpawnZ = GetFirstInt(data, "spawnZ", "z");
            o.SpawnMap = GetFirstString(data, "spawnMap", "map", "mapName");
            o.DestinationName = GetFirstString(data, "destinationName", "destination", "locationHint", "location");
            o.DestinationX = GetFirstInt(data, "destinationX", "destX", "locationX");
            o.DestinationY = GetFirstInt(data, "destinationY", "destY", "locationY");
            o.DestinationZ = GetFirstInt(data, "destinationZ", "destZ", "locationZ");
            o.DestinationMap = GetFirstString(data, "destinationMap", "destMap", "mapName", "map");
            o.DestinationRegion = GetFirstString(data, "destinationRegion", "destRegion", "region");
            o.RadiusToComplete = GetFirstInt(data, 5, "radiusToComplete", "radiusTiles", "radius");
            o.OnEscortDead = GetFirstString(data, "onEscortDead", "onDeath") ?? "FailQuest";

            // Aparência e comportamento
            o.EscortBody = GetFirstInt(data, 0x190, "escortBody", "body");
            o.EscortHue = GetFirstInt(data, "escortHue", "hue");
            o.EscortHits = GetFirstInt(data, "escortHits", "hits");
            o.DefendsItself = GetFirstBool(data, "defendsItself", "canFight");
            o.TeleportWhenStuck = GetFirstBool(data, true, "teleportWhenStuck", "rescueWhenStuck");
            o.MaxDistanceFromOwner = GetFirstInt(data, 24, "maxDistanceFromOwner", "maxDistance");
            o.StuckTimeoutSeconds = GetFirstInt(data, 20, "stuckTimeoutSeconds", "stuckTimeout");

            // Destino declarado apenas por nome de lugar: sem coordenadas o
            // driver resolve o centro da região em runtime.
            if (o.DestinationX == 0 && o.DestinationY == 0 &&
                string.IsNullOrWhiteSpace(o.DestinationRegion) &&
                !string.IsNullOrWhiteSpace(o.DestinationName))
            {
                o.DestinationRegion = o.DestinationName;
            }

            return o;
        }

        private static DefendObjective ParseDefendObjective(Dictionary<string, object> data)
        {
            var o = new DefendObjective();

            o.LocationName = GetFirstString(data, "locationName", "location", "locationHint");
            o.LocationX = GetFirstInt(data, "locationX", "x");
            o.LocationY = GetFirstInt(data, "locationY", "y");
            o.MapName = GetFirstString(data, "mapName", "map");
            o.RadiusTiles = GetFirstInt(data, "radiusTiles", "radius", 12);
            o.TotalWaves = GetFirstInt(data, "totalWaves", "waves", 1);
            o.EnemyType = GetFirstString(data, "enemyType", "mobType", "targetType", "enemy");
            o.EnemiesPerWave = GetFirstInt(data, "enemiesPerWave", "enemies", 1);
            o.TimeBetweenWavesSeconds = GetFirstInt(data, "timeBetweenWavesSeconds", "timeBetweenWaves", 30);

            // O total de abates é derivado das ondas quando o JSON não o informa
            if (!data.ContainsKey("required"))
                o.Required = Math.Max(1, o.TotalWaves * o.EnemiesPerWave);

            return o;
        }

        private static DestroyObjective ParseDestroyObjective(Dictionary<string, object> data)
        {
            var o = new DestroyObjective();

            o.TargetType = GetFirstString(data, "targetType", "objectType", "itemType", "type");
            o.TargetName = GetFirstString(data, "targetName", "objectName", "name");
            o.RespawnOnFail = GetFirstBool(data, "respawnOnFail");

            var serials = GetFirstList(data, "targetSerials", "serials");
            if (serials != null)
            {
                foreach (object s in serials)
                {
                    uint value;
                    if (s != null && uint.TryParse(s.ToString(), out value))
                        o.TargetSerials.Add(new Serial(value));
                }
            }

            return o;
        }

        private static ExploreObjective ParseExploreObjective(Dictionary<string, object> data)
        {
            var o = new ExploreObjective();

            o.LocationName = GetFirstString(data, "locationName", "location", "targetName", "locationHint");
            o.LocationX = GetFirstInt(data, "locationX", "x");
            o.LocationY = GetFirstInt(data, "locationY", "y");
            o.LocationZ = GetFirstInt(data, "locationZ", "z");
            o.MapName = GetFirstString(data, "mapName", "map");
            o.RadiusTiles = GetFirstInt(data, "radiusTiles", "radius", 12);
            o.TriggerOnEnter = GetFirstBool(data, "triggerOnEnter", true);
            o.StayDurationSeconds = GetFirstInt(data, "stayDurationSeconds", "stayDuration", "staySeconds");

            if (o.StayDurationSeconds > 0 && !data.ContainsKey("required"))
                o.Required = 1;

            return o;
        }

        private static SkillGainObjective ParseSkillGainObjective(Dictionary<string, object> data)
        {
            var o = new SkillGainObjective();

            string raw = GetFirstString(data, "skillName", "skill", "targetType", "name");
            SkillName skill;

            if (ResolveSkillName(raw, out skill))
            {
                o.Skill = skill;
                o.SkillName = skill.ToString();
            }
            else
            {
                o.SkillName = raw;
                Warn("SkillGain: skill '" + raw + "' não reconhecida.");
            }

            o.TargetValue = GetFirstDouble(data, "targetValue", "required", "target", "value");
            o.StartValue = GetFirstDouble(data, "startValue", "from");
            o.Mode = GetFirstString(data, "mode") ?? "ReachValue";

            if (o.TargetValue > 0 && o.Mode.Equals("ReachValue", StringComparison.OrdinalIgnoreCase))
                o.Required = 1;

            return o;
        }

        private static CraftObjective ParseCraftObjective(Dictionary<string, object> data)
        {
            var o = new CraftObjective();

            o.ItemType = GetFirstString(data, "itemType", "targetType", "type", "item");
            o.ItemName = GetFirstString(data, "itemName", "targetName", "name");
            o.CraftSkill = GetFirstString(data, "craftSkill", "craftSystem", "skill", "skillName");
            o.RequireExceptional = GetFirstBool(data, "requireExceptional", "exceptional");
            o.ConsumeOnComplete = GetFirstBool(data, "consumeOnComplete", "consume");

            return o;
        }

        private static UseSkillObjective ParseUseSkillObjective(Dictionary<string, object> data)
        {
            var o = new UseSkillObjective();

            string raw = GetFirstString(data, "skillName", "skill", "targetType", "name");
            SkillName skill;

            if (ResolveSkillName(raw, out skill))
            {
                o.Skill = skill;
                o.SkillName = skill.ToString();
            }
            else
            {
                o.SkillName = raw;
                Warn("UseSkill: skill '" + raw + "' não reconhecida.");
            }

            o.RequireSuccess = GetFirstBool(data, "requireSuccess", "onlySuccess");

            return o;
        }

        private static InteractObjective ParseInteractObjective(Dictionary<string, object> data)
        {
            var o = new InteractObjective();

            o.TargetType = GetFirstString(data, "targetType", "objectType", "itemType", "type");
            o.TargetName = GetFirstString(data, "targetName", "objectName", "name");
            o.InteractionType = GetFirstString(data, "interactionType", "action") ?? "DoubleClick";
            o.SpeechTrigger = GetFirstString(data, "speechTrigger", "trigger");

            var serials = GetFirstList(data, "targetSerials", "serials");
            if (serials != null)
            {
                foreach (object s in serials)
                {
                    uint value;
                    if (s != null && uint.TryParse(s.ToString(), out value))
                        o.TargetSerials.Add(new Serial(value));
                }
            }

            return o;
        }

        private static TalkToObjective ParseTalkToObjective(Dictionary<string, object> data)
        {
            var o = new TalkToObjective();

            o.TargetNpcId = GetFirstString(data, "targetNpcId", "npcId", "targetType");
            o.TargetNpcName = GetFirstString(data, "targetNpcName", "npcName", "targetName", "name");
            o.SpeechTrigger = GetFirstString(data, "speechTrigger", "trigger");

            var grants = GetFirstList(data, "grantItems", "giveItems", "items");
            if (grants != null)
            {
                foreach (object entry in grants)
                {
                    var dict = entry as Dictionary<string, object>;
                    if (dict == null)
                        continue;

                    var grant = new ObjectiveGrantItem
                    {
                        ItemType = GetFirstString(dict, "itemType", "type", "targetType"),
                        ItemName = GetFirstString(dict, "itemName", "name"),
                        Amount = GetFirstInt(dict, "amount", "quantity", "count", 1)
                    };

                    if (!string.IsNullOrWhiteSpace(grant.ItemType) || !string.IsNullOrWhiteSpace(grant.ItemName))
                        o.GrantItems.Add(grant);
                }
            }

            return o;
        }

        private static UseItemObjective ParseUseItemObjective(Dictionary<string, object> data)
        {
            var o = new UseItemObjective();

            o.ItemType = GetFirstString(data, "itemType", "targetType", "type", "item");
            o.ItemName = GetFirstString(data, "itemName", "targetName", "name");
            o.ItemHue = GetFirstInt(data, "itemHue", "hue");
            o.ConsumeOnUse = GetFirstBool(data, "consumeOnUse", "consume");

            return o;
        }

        private static SurviveObjective ParseSurviveObjective(Dictionary<string, object> data)
        {
            var o = new SurviveObjective();

            o.DurationSeconds = GetFirstInt(data, "durationSeconds", "duration", "seconds", "timeLimit");
            o.FailOnDeath = GetFirstBool(data, "failOnDeath", true);
            o.RequiredRegion = GetFirstString(data, "requiredRegion", "region");

            if (o.DurationSeconds <= 0)
                o.DurationSeconds = Math.Max(1, GetInt(data, "required", 60));

            if (!data.ContainsKey("required"))
                o.Required = o.DurationSeconds;

            return o;
        }

        #endregion

        #region Validação de objetivos

        /// <summary>
        /// Avisa no console quando um objetivo foi carregado sem a informação
        /// mínima necessária para progredir. Antes, esses casos passavam em
        /// silêncio e a quest ficava travada para sempre.
        /// </summary>
        private static void ValidateObjective(QuestObjective objective)
        {
            KillObjective kill = objective as KillObjective;
            if (kill != null && string.IsNullOrWhiteSpace(kill.TargetType) &&
                (kill.TargetTypes == null || kill.TargetTypes.Count == 0))
            {
                Warn("Kill sem targetType — nunca receberá progresso. Descrição: " + Safe(kill.Description));
            }

            CollectObjective collect = objective as CollectObjective;
            if (collect != null && string.IsNullOrWhiteSpace(collect.ItemType) &&
                string.IsNullOrWhiteSpace(collect.ItemName))
            {
                Warn("Collect sem itemType/itemName — nunca receberá progresso. Descrição: " + Safe(collect.Description));
            }

            DeliverObjective deliver = objective as DeliverObjective;
            if (deliver != null && string.IsNullOrWhiteSpace(deliver.ItemType) &&
                string.IsNullOrWhiteSpace(deliver.ItemName))
            {
                Warn("Deliver sem itemType/itemName — nunca receberá progresso. Descrição: " + Safe(deliver.Description));
            }

            TalkToObjective talk = objective as TalkToObjective;
            if (talk != null && string.IsNullOrWhiteSpace(talk.TargetNpcId) &&
                string.IsNullOrWhiteSpace(talk.TargetNpcName))
            {
                Warn("TalkTo sem targetNpcId/targetNpcName — nunca receberá progresso. Descrição: " + Safe(talk.Description));
            }

            ExploreObjective explore = objective as ExploreObjective;
            if (explore != null && (explore.LocationX == 0 && explore.LocationY == 0) &&
                string.IsNullOrWhiteSpace(explore.LocationName))
            {
                Warn("Explore sem locationX/locationY e sem locationName — nunca receberá progresso. Descrição: " + Safe(explore.Description));
            }

            CraftObjective craft = objective as CraftObjective;
            if (craft != null && string.IsNullOrWhiteSpace(craft.ItemType) &&
                string.IsNullOrWhiteSpace(craft.ItemName))
            {
                Warn("Craft sem itemType/itemName — nunca receberá progresso. Descrição: " + Safe(craft.Description));
            }

            UseSkillObjective useSkill = objective as UseSkillObjective;
            if (useSkill != null && string.IsNullOrWhiteSpace(useSkill.SkillName))
            {
                Warn("UseSkill sem skill/skillName — nunca receberá progresso. Descrição: " + Safe(useSkill.Description));
            }

            SkillGainObjective gain = objective as SkillGainObjective;
            if (gain != null && string.IsNullOrWhiteSpace(gain.SkillName))
            {
                Warn("SkillGain sem skill/skillName — nunca receberá progresso. Descrição: " + Safe(gain.Description));
            }

            DefendObjective defend = objective as DefendObjective;
            if (defend != null && string.IsNullOrWhiteSpace(defend.EnemyType))
            {
                Warn("Defend sem enemyType — nenhuma onda será gerada. Descrição: " + Safe(defend.Description));
            }

            DestroyObjective destroy = objective as DestroyObjective;
            if (destroy != null && string.IsNullOrWhiteSpace(destroy.TargetType) &&
                string.IsNullOrWhiteSpace(destroy.TargetName) && destroy.TargetSerials.Count == 0)
            {
                Warn("Destroy sem targetType/targetSerials — nunca receberá progresso. Descrição: " + Safe(destroy.Description));
            }

            InteractObjective interact = objective as InteractObjective;
            if (interact != null && string.IsNullOrWhiteSpace(interact.TargetType) &&
                string.IsNullOrWhiteSpace(interact.TargetName) && interact.TargetSerials.Count == 0)
            {
                Warn("Interact sem targetType/targetSerials — nunca receberá progresso. Descrição: " + Safe(interact.Description));
            }

            UseItemObjective useItem = objective as UseItemObjective;
            if (useItem != null && string.IsNullOrWhiteSpace(useItem.ItemType) &&
                string.IsNullOrWhiteSpace(useItem.ItemName))
            {
                Warn("UseItem sem itemType/itemName — nunca receberá progresso. Descrição: " + Safe(useItem.Description));
            }

            EscortObjective escort = objective as EscortObjective;
            if (escort != null && escort.DestinationX == 0 && escort.DestinationY == 0 &&
                string.IsNullOrWhiteSpace(escort.DestinationRegion))
            {
                Warn("Escort sem destinationX/destinationY e sem destinationRegion — a chegada nunca será detectada. Descrição: " + Safe(escort.Description));
            }
        }

        private static string Safe(string s)
        {
            return string.IsNullOrEmpty(s) ? "(sem descrição)" : s;
        }

        #endregion

        #region Reconhecimento de tipos

        /// <summary>
        /// Aceita variações comuns de escrita: "kill", "Kill", "KILL", "matar",
        /// "talkto", "talk_to", "Talk To", "use_skill", "skillgain"...
        /// </summary>
        private static bool TryParseObjectiveType(string raw, out ObjectiveType type)
        {
            type = ObjectiveType.Kill;

            if (string.IsNullOrWhiteSpace(raw))
                return false;

            string normalized = Normalize(raw);

            // Português / variações frequentes
            switch (normalized)
            {
                case "matar": case "abater": case "slay": type = ObjectiveType.Kill; return true;
                case "coletar": case "recolher": case "gather": case "recolheritens": type = ObjectiveType.Collect; return true;
                case "entregar": type = ObjectiveType.Deliver; return true;
                case "escoltar": type = ObjectiveType.Escort; return true;
                case "defender": type = ObjectiveType.Defend; return true;
                case "destruir": type = ObjectiveType.Destroy; return true;
                case "explorar": case "visitar": type = ObjectiveType.Explore; return true;
                case "ganharskill": case "treinarskill": case "skillup": case "train": type = ObjectiveType.SkillGain; return true;
                case "craftar": case "criar": case "fabricar": case "make": type = ObjectiveType.Craft; return true;
                case "usarskill": type = ObjectiveType.UseSkill; return true;
                case "interagir": type = ObjectiveType.Interact; return true;
                case "falar": case "falarcom": case "conversar": case "talk": type = ObjectiveType.TalkTo; return true;
                case "usaritem": case "usar": type = ObjectiveType.UseItem; return true;
                case "sobreviver": type = ObjectiveType.Survive; return true;
            }

            if (Enum.TryParse(normalized, true, out ObjectiveType parsed))
            {
                type = parsed;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Reconhece o nome de uma skill aceitando apelidos comuns
        /// ("Swordsmanship", "Taming", "Evaluating Intelligence", "Magery"...).
        /// Não cai mais silenciosamente em Alchemy quando o nome é inválido.
        /// </summary>
        private static bool ResolveSkillName(string raw, out SkillName skill)
        {
            skill = SkillName.Alchemy;

            if (string.IsNullOrWhiteSpace(raw))
                return false;

            string normalized = Normalize(raw);

            if (Enum.TryParse(normalized, true, out SkillName parsed))
            {
                skill = parsed;
                return true;
            }

            if (_skillAliases.TryGetValue(normalized, out string alias))
                return Enum.TryParse(alias, true, out skill);

            return false;
        }

        private static readonly Dictionary<string, string> _skillAliases =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "swordsmanship", "Swords" }, { "sword", "Swords" }, { "swordmanship", "Swords" },
                { "macefighting", "MaceFighting" }, { "mace", "MaceFighting" }, { "maces", "MaceFighting" },
                { "wrestling", "Wrestling" }, { "fencing", "Fencing" }, { "archery", "Archery" },
                { "animaltaming", "AnimalTaming" }, { "taming", "AnimalTaming" }, { "tame", "AnimalTaming" },
                { "animallore", "AnimalLore" }, { "veterinary", "Veterinary" }, { "vet", "Veterinary" },
                { "evaluatingintelligence", "EvalInt" }, { "evalint", "EvalInt" }, { "evaluateintelligence", "EvalInt" },
                { "detecthidden", "DetectHidden" }, { "detectinghidden", "DetectHidden" }, { "hiding", "Hiding" },
                { "removetrap", "RemoveTrap" }, { "trapremoval", "RemoveTrap" },
                { "armslore", "ArmsLore" }, { "itemid", "ItemID" }, { "itemidentification", "ItemID" },
                { "peacemaking", "Peacemaking" }, { "peacemake", "Peacemaking" }, { "provocation", "Provocation" },
                { "spiritspeak", "SpiritSpeak" }, { "spirituality", "SpiritSpeak" },
                { "magery", "Magery" }, { "magic", "Magery" }, { "magicresistance", "MagicResist" },
                { "resisting", "MagicResist" }, { "resist", "MagicResist" }, { "resistingspells", "MagicResist" },
                { "inscription", "Inscription" }, { "scribing", "Inscription" }, { "scribe", "Inscription" },
                { "alchemy", "Alchemy" }, { "poisoning", "Poisoning" }, { "tasteidentification", "TasteID" },
                { "tasteid", "TasteID" }, { "tasting", "TasteID" },
                { "cartography", "Cartography" }, { "camping", "Camping" }, { "fishing", "Fishing" },
                { "cooking", "Cooking" }, { "tailoring", "Tailoring" }, { "sewing", "Tailoring" },
                { "blacksmithy", "Blacksmith" }, { "blacksmith", "Blacksmith" }, { "smithing", "Blacksmith" },
                { "mining", "Mining" }, { "lumberjacking", "Lumberjacking" }, { "carpentry", "Carpentry" },
                { "bowcraft", "Bowcraft" }, { "fletching", "Bowcraft" }, { "tinkering", "Tinkering" },
                { "begging", "Begging" }, { "healing", "Healing" }, { "anatomy", "Anatomy" },
                { "herding", "Herding" }, { "tracking", "Tracking" }, { "forensicevaluation", "ForensicEval" },
                { "forensics", "ForensicEval" }, { "lockpicking", "Lockpicking" }, { "snooping", "Snooping" },
                { "stealing", "Stealing" }, { "stealth", "Stealth" }, { "parrying", "Parrying" }, { "parry", "Parrying" },
                { "tactics", "Tactics" }, { "musicanship", "Musicianship" }, { "musicianship", "Musicianship" },
                { "discordance", "Discordance" }, { "meditation", "Meditation" }, { "focus", "Focus" },
                { "chivalry", "Chivalry" }, { "necromancy", "Necromancy" }, { "bushido", "Bushido" },
                { "ninjitsu", "Ninjitsu" }, { "spellweaving", "Spellweaving" }, { "spellweave", "Spellweaving" },
                { "mysticism", "Mysticism" }, { "imbuing", "Imbuing" }, { "throwing", "Throwing" },
                { "spirit", "SpiritSpeak" }, { "necro", "Necromancy" }
            };

        /// <summary>Mantém apenas letras/dígitos e remove separadores.</summary>
        private static string Normalize(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return string.Empty;

            var sb = new StringBuilder(raw.Length);

            foreach (char c in raw)
            {
                if (char.IsLetterOrDigit(c))
                    sb.Append(c);
            }

            return sb.ToString();
        }

        #endregion

        #endregion

        #region Parse Prerequisites

        private static QuestPrerequisites ParsePrerequisites(Dictionary<string, object> data)
        {
            var prereqs = new QuestPrerequisites();

            if (data == null)
                return prereqs;

            prereqs.MinLevel = GetFirstInt(data, "minLevel", "minimumLevel", "level");
            prereqs.MinFame = GetFirstInt(data, "minFame", "fame");
            prereqs.RequiredFaction = GetFirstString(data, "requiredFaction", "faction");
            prereqs.RequiredGuild = GetFirstString(data, "requiredGuild", "guild");
            prereqs.FailMessage = GetFirstString(data, "failMessage", "message");

            if (data.ContainsKey("minKarma"))
                prereqs.MinKarma = GetInt(data, "minKarma");

            if (data.ContainsKey("maxKarma"))
                prereqs.MaxKarma = GetInt(data, "maxKarma");

            // Quests anteriores — "questIds" é a grafia usada nos JSON do shard
            var requiredQuests = GetFirstList(data, "questIds", "requiredQuests", "requiredQuestIds", "quests", "completedQuests");
            if (requiredQuests != null)
            {
                foreach (object q in requiredQuests)
                {
                    string s = q == null ? null : q.ToString();
                    if (!string.IsNullOrWhiteSpace(s))
                        prereqs.RequiredQuests.Add(s.Trim());
                }
            }

            // Raças permitidas
            var races = GetFirstList(data, "allowedRaces", "races");
            if (races != null)
            {
                foreach (object r in races)
                {
                    string s = r == null ? null : r.ToString();
                    if (!string.IsNullOrWhiteSpace(s))
                        prereqs.AllowedRaces.Add(s.Trim());
                }
            }

            // Skill mínima única
            if (data.ContainsKey("minSkill"))
            {
                var minSkill = data["minSkill"] as Dictionary<string, object>;
                if (minSkill != null)
                {
                    var req = ParseSkillRequirement(minSkill);
                    if (req != null)
                    {
                        prereqs.MinSkill = req;
                        prereqs.RequiredSkills.Add(req);
                    }
                }
                else
                {
                    string raw = GetString(data, "minSkill");
                    if (!string.IsNullOrWhiteSpace(raw))
                        Warn("minSkill deve ser um objeto {skill, value}; recebido texto '" + raw + "'.");
                }
            }

            // Lista de skills
            var skills = GetFirstList(data, "requiredSkills", "skills");
            if (skills != null)
            {
                foreach (object s in skills)
                {
                    var req = ParseSkillRequirement(s as Dictionary<string, object>);
                    if (req != null)
                        prereqs.RequiredSkills.Add(req);
                }
            }

            // Itens exigidos — aceita objetos {itemType, amount} e também
            // a forma curta usada nos JSON do shard: ["SeloFortaleza"]
            var items = GetFirstList(data, "requiredItems", "items");
            if (items != null)
            {
                foreach (object it in items)
                {
                    var req = it as Dictionary<string, object> != null
                        ? ParseRequiredItem(it as Dictionary<string, object>)
                        : ParseRequiredItem(it == null ? null : it.ToString());

                    if (req != null)
                        prereqs.RequiredItems.Add(req);
                    else if (it != null)
                        Warn("Pré-requisito de item ignorado (sem 'type'): " + it);
                }
            }

            return prereqs;
        }

        private static RequiredItem ParseRequiredItem(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return null;

            return new RequiredItem { Type = raw.Trim(), Amount = 1 };
        }

        private static SkillRequirement ParseSkillRequirement(Dictionary<string, object> data)
        {
            if (data == null)
                return null;

            string raw = GetFirstString(data, "skillName", "skill", "name", "type");
            if (string.IsNullOrWhiteSpace(raw))
                return null;

            var req = new SkillRequirement { SkillName = raw };

            SkillName skill;
            if (ResolveSkillName(raw, out skill))
            {
                req.Skill = skill;
                req.SkillName = skill.ToString();
            }
            else
            {
                Warn("Pré-requisito de skill desconhecido: '" + raw + "'.");
            }

            req.Value = GetFirstDouble(data, "value", "min", "minimum", "required", "amount");
            return req;
        }

        private static RequiredItem ParseRequiredItem(Dictionary<string, object> data)
        {
            if (data == null)
                return null;

            string type = GetFirstString(data, "itemType", "type", "targetType", "name");
            if (string.IsNullOrWhiteSpace(type))
                return null;

            return new RequiredItem
            {
                Type = type,
                Name = GetFirstString(data, "itemName", "name"),
                Amount = GetFirstInt(data, "amount", "quantity", "count", 1),
                Hue = GetFirstInt(data, "hue", "itemHue"),
                Consume = GetFirstBool(data, "consume")
            };
        }

        #endregion

        #region Parse Rewards

        private static QuestRewards ParseRewards(Dictionary<string, object> data)
        {
            var rewards = new QuestRewards();

            if (data == null)
                return rewards;

            rewards.Gold = GetFirstInt(data, "gold", "goldAmount", "money");
            rewards.BankDeposit = GetFirstBool(data, "bankDeposit", "depositInBank");
            rewards.Fame = GetFirstInt(data, "fame", "famePoints");
            rewards.Karma = GetFirstInt(data, "karma", "karmaPoints");
            rewards.Experience = GetFirstInt(data, "experience", "exp", "xp");

            // Itens fixos
            var items = GetFirstList(data, "items", "rewardItems");
            if (items != null)
            {
                foreach (object entry in items)
                {
                    var item = ParseRewardItem(entry as Dictionary<string, object>);
                    if (item != null)
                        rewards.Items.Add(item);
                    else
                        Warn("Recompensa de item ignorada (sem 'type'): " + entry);
                }
            }

            // Pools aleatórios
            var pools = GetFirstList(data, "randomItems", "randomRewards", "pools");
            if (pools != null)
            {
                foreach (object entry in pools)
                {
                    var pool = ParseRandomPool(entry as Dictionary<string, object>);
                    if (pool != null)
                        rewards.RandomItems.Add(pool);
                }
            }

            // Skills — aceita "skillGains", "skills" e o formato simplificado
            // { "skillName": "Magery", "bonus": 5 } usado em Enfermaria.json
            var skillGains = GetFirstList(data, "skillGains", "skills", "skillRewards");
            if (skillGains != null)
            {
                foreach (object entry in skillGains)
                {
                    var reward = ParseSkillReward(entry as Dictionary<string, object>);
                    if (reward != null)
                        rewards.SkillGains.Add(reward);
                    else
                        Warn("Recompensa de skill ignorada (skill não reconhecida): " + entry);
                }
            }

            // Stats
            var statGains = GetFirstList(data, "statGains", "stats", "statRewards");
            if (statGains != null)
            {
                foreach (object entry in statGains)
                {
                    var reward = ParseStatReward(entry as Dictionary<string, object>);
                    if (reward != null)
                        rewards.StatGains.Add(reward);
                }
            }

            // Títulos
            var titles = GetFirstList(data, "titles", "titleRewards");
            if (titles != null)
            {
                foreach (object entry in titles)
                {
                    string title = null;
                    string type = null;

                    var dict = entry as Dictionary<string, object>;
                    if (dict != null)
                    {
                        title = GetFirstString(dict, "title", "name", "text");
                        type = GetFirstString(dict, "type", "kind");
                    }
                    else if (entry != null)
                    {
                        title = entry.ToString();
                    }

                    if (!string.IsNullOrWhiteSpace(title))
                        rewards.Titles.Add(new TitleReward { Title = title, Type = type ?? "Suffix" });
                }
            }

            // Desbloqueios
            AddStrings(rewards.UnlockedQuests, GetFirstList(data, "unlockedQuests", "unlockQuests"));
            AddStrings(rewards.UnlockedAreas, GetFirstList(data, "unlockedAreas", "unlockAreas", "areas"));
            AddStrings(rewards.UnlockedRecipes, GetFirstList(data, "unlockedRecipes", "unlockRecipes", "recipes"));

            if (data.ContainsKey("classUnlock"))
            {
                // Aceita tanto a forma curta ("classUnlock": "Curandeiro") quanto
                // a forma usada em Enfermaria.json
                // ("classUnlock": { "type": "Curandeiro", "name": "Classe de Curandeiro" }).
                string classUnlock = GetString(data, "classUnlock");
                var classDict = GetObject(data, "classUnlock");

                if (classDict != null)
                {
                    classUnlock = GetFirstString(classDict, "type", "name", "class");

                    string label = GetFirstString(classDict, "name");
                    if (!string.IsNullOrWhiteSpace(label) && !rewards.UnlockedAreas.Contains(label))
                        rewards.UnlockedAreas.Add(label);
                }

                if (!string.IsNullOrWhiteSpace(classUnlock) && !rewards.UnlockedAreas.Contains(classUnlock))
                    rewards.UnlockedAreas.Add(classUnlock);
            }

            return rewards;
        }

        private static void AddStrings(List<string> target, List<object> source)
        {
            if (target == null || source == null)
                return;

            foreach (object o in source)
            {
                string s = o == null ? null : o.ToString();
                if (!string.IsNullOrWhiteSpace(s) && !target.Contains(s))
                    target.Add(s);
            }
        }

        private static RewardItem ParseRewardItem(Dictionary<string, object> data)
        {
            if (data == null)
                return null;

            string type = GetFirstString(data, "itemType", "type", "targetType", "item");
            if (string.IsNullOrWhiteSpace(type))
                return null;

            var item = new RewardItem
            {
                Type = type,
                Name = GetFirstString(data, "itemName", "name"),
                Amount = GetFirstInt(data, "amount", "quantity", "count", 1),
                Hue = GetFirstInt(data, "hue", "itemHue"),
                Chance = GetFirstInt(data, "chance", "probability", 100)
            };

            if (item.Amount <= 0)
                item.Amount = 1;

            if (item.Chance <= 0)
                item.Chance = 100;

            return item;
        }

        private static RandomRewardPool ParseRandomPool(Dictionary<string, object> data)
        {
            if (data == null)
                return null;

            var pool = new RandomRewardPool
            {
                PoolName = GetFirstString(data, "poolName", "name") ?? "Pool",
                PickCount = GetFirstInt(data, "pickCount", "picks", "count", 1)
            };

            var items = GetFirstList(data, "items", "entries");
            if (items != null)
            {
                foreach (object entry in items)
                {
                    var dict = entry as Dictionary<string, object>;
                    if (dict == null)
                        continue;

                    string type = GetFirstString(dict, "itemType", "type", "targetType", "item");
                    if (string.IsNullOrWhiteSpace(type))
                        continue;

                    pool.Items.Add(new WeightedItem
                    {
                        Type = type,
                        Name = GetFirstString(dict, "itemName", "name"),
                        Weight = GetFirstInt(dict, "weight", "chance", 100),
                        Amount = GetFirstInt(dict, "amount", "quantity", 1),
                        Hue = GetFirstInt(dict, "hue", "itemHue")
                    });
                }
            }

            if (pool.PickCount <= 0)
                pool.PickCount = 1;

            return pool.Items.Count > 0 ? pool : null;
        }

        private static SkillReward ParseSkillReward(Dictionary<string, object> data)
        {
            if (data == null)
                return null;

            string raw = GetFirstString(data, "skillName", "skill", "name", "type");
            SkillName skill;

            if (!ResolveSkillName(raw, out skill))
            {
                if (!string.IsNullOrWhiteSpace(raw))
                    Warn("Recompensa de skill desconhecida: '" + raw + "'.");
                return null;
            }

            double amount = GetFirstDouble(data, "amount", "bonus", "value", "points", "gain");

            return new SkillReward
            {
                Skill = skill,
                SkillName = skill.ToString(),
                Amount = amount,
                CapIncrease = GetFirstDouble(data, "capIncrease", "cap")
            };
        }

        private static StatReward ParseStatReward(Dictionary<string, object> data)
        {
            if (data == null)
                return null;

            string stat = GetFirstString(data, "stat", "statName", "name", "type");
            if (string.IsNullOrWhiteSpace(stat))
                return null;

            switch (Normalize(stat).ToLowerInvariant())
            {
                case "str": case "strength": case "forca": stat = "Str"; break;
                case "dex": case "dexterity": case "destreza": stat = "Dex"; break;
                case "int": case "intelligence": case "inteligencia": stat = "Int"; break;
                default:
                    Warn("Stat desconhecido em recompensa: '" + stat + "'.");
                    return null;
            }

            return new StatReward
            {
                Stat = stat,
                Amount = GetFirstInt(data, "amount", "bonus", "value", "points")
            };
        }

        #endregion

        #region Parse Dialogue

        /// <summary>
        /// Lê os diálogos da quest. Antes só onAccept/onProgress/onComplete eram
        /// lidos; onOffer e onDecline — que o tutorial documenta e o arquivo
        /// "teste" usa — eram descartados em silêncio.
        /// </summary>
        private static QuestDialogue ParseQuestDialogue(Dictionary<string, object> data, string questId, string questName)
        {
            var dialogue = new QuestDialogue(questId, questName);

            if (data == null)
                return dialogue;

            dialogue.ShortDescription = GetFirstString(data, "shortDescription", "summary", "description");
            dialogue.RewardSummary = GetFirstString(data, "rewardSummary", "rewardText");

            ReplaceLines(data, "onOffer", "offer", dialogue.Offer);
            ReplaceLines(data, "onAccept", "accept", dialogue.Accept);
            ReplaceLines(data, "onDecline", "decline", dialogue.Decline);
            ReplaceLines(data, "onProgress", "progress", dialogue.Progress);
            ReplaceLines(data, "onComplete", "complete", dialogue.Complete);
            ReplaceLines(data, "onAlreadyComplete", "alreadyComplete", dialogue.AlreadyComplete);
            ReplaceLines(data, "onCannotAccept", "cannotAccept", dialogue.CannotAccept);

            var descriptions = GetFirstList(data, "objectiveDescriptions", "objectivesText");
            if (descriptions != null)
            {
                foreach (object o in descriptions)
                {
                    string s = o == null ? null : o.ToString();
                    if (!string.IsNullOrWhiteSpace(s))
                        dialogue.ObjectiveDescriptions.Add(s);
                }
            }

            return dialogue;
        }

        /// <summary>
        /// Substitui as falas de um nó mantendo as opções padrão (Aceitar/Recusar),
        /// sem as quais o jogador não conseguiria aceitar a quest.
        /// </summary>
        private static void ReplaceLines(Dictionary<string, object> data, string key, string alias, DialogueNode node)
        {
            if (node == null)
                return;

            object raw = null;

            if (data.ContainsKey(key))
                raw = data[key];
            else if (data.ContainsKey(alias))
                raw = data[alias];

            if (raw == null)
                return;

            // Formato objeto: { "lines": [...], "text": "..." }
            var dict = raw as Dictionary<string, object>;
            if (dict != null)
            {
                object inner = null;

                if (dict.ContainsKey("lines")) inner = dict["lines"];
                else if (dict.ContainsKey("text")) inner = dict["text"];
                else if (dict.ContainsKey("message")) inner = dict["message"];

                if (inner == null)
                    return;

                raw = inner;
            }

            node.Lines.Clear();
            ParseDialogueLines(raw, node.Lines);

            if (node.Lines.Count == 0)
                Warn("Diálogo '" + key + "' presente mas vazio.");
        }

        private static void ParseDialogueLines(object data, List<DialogueLine> lines)
        {
            if (data is string text)
            {
                if (!string.IsNullOrWhiteSpace(text))
                    lines.Add(new DialogueLine(text, 2000));
            }
            else if (data is List<object> array)
            {
                foreach (var item in array)
                {
                    if (item is string line && !string.IsNullOrWhiteSpace(line))
                        lines.Add(new DialogueLine(line, 2000));
                    else if (item != null)
                    {
                        string s = item.ToString();
                        if (!string.IsNullOrWhiteSpace(s))
                            lines.Add(new DialogueLine(s, 2000));
                    }
                }
            }
        }

        #endregion

        #region Helper Methods

        private static Dictionary<string, object> GetObject(Dictionary<string, object> data, string key)
        {
            if (data == null || !data.ContainsKey(key))
                return null;

            return data[key] as Dictionary<string, object>;
        }

        private static string GetString(Dictionary<string, object> data, string key)
        {
            if (data == null || !data.ContainsKey(key))
                return null;

            object value = data[key];
            if (value == null)
                return null;

            // Objetos e listas não viram texto: antes isso produzia
            // "System.Collections.Generic.Dictionary`2[...]" como nome de quest.
            if (value is Dictionary<string, object> || value is List<object>)
                return null;

            return value.ToString();
        }

        private static string GetNestedString(Dictionary<string, object> data, string parentKey, string key)
        {
            return GetString(GetObject(data, parentKey), key);
        }

        /// <summary>
        /// Retorna o primeiro campo presente entre várias grafias possíveis.
        /// É o que permite aceitar tanto o formato do tutorial quanto o gerado
        /// pelo QuestCreatorGump (que emite targetType/targetName para tudo).
        /// </summary>
        private static string GetFirstString(Dictionary<string, object> data, params string[] keys)
        {
            if (data == null)
                return null;

            foreach (string key in keys)
            {
                string value = GetString(data, key);
                if (!string.IsNullOrWhiteSpace(value))
                    return value.Trim();
            }

            return null;
        }

        private static int GetInt(Dictionary<string, object> data, string key, int defaultValue = 0)
        {
            if (data == null || !data.ContainsKey(key))
                return defaultValue;

            object value = data[key];
            if (value == null)
                return defaultValue;

            if (value is int intVal) return intVal;
            if (value is long longVal) return (int)longVal;
            if (value is double doubleVal) return (int)doubleVal;
            if (value is bool boolVal) return boolVal ? 1 : 0;
            if (int.TryParse(value.ToString(), out int result)) return result;

            Warn("Valor '" + value + "' do campo '" + key + "' não é numérico; usando " + defaultValue + ".");
            return defaultValue;
        }

        private static int GetNestedInt(Dictionary<string, object> data, string parentKey, string key)
        {
            return GetInt(GetObject(data, parentKey), key);
        }

        private static int GetFirstInt(Dictionary<string, object> data, params string[] keys)
        {
            return GetFirstInt(data, 0, keys);
        }

        private static int GetFirstInt(Dictionary<string, object> data, int defaultValue, params string[] keys)
        {
            if (data == null)
                return defaultValue;

            foreach (string key in keys)
            {
                if (data.ContainsKey(key) && data[key] != null)
                    return GetInt(data, key, defaultValue);
            }

            return defaultValue;
        }

        private static double GetDouble(Dictionary<string, object> data, string key, double defaultValue = 0)
        {
            if (data == null || !data.ContainsKey(key))
                return defaultValue;

            object value = data[key];
            if (value == null)
                return defaultValue;

            if (value is double doubleVal) return doubleVal;
            if (value is int intVal) return intVal;
            if (value is long longVal) return longVal;
            if (double.TryParse(value.ToString(), out double result)) return result;

            Warn("Valor '" + value + "' do campo '" + key + "' não é numérico; usando " + defaultValue + ".");
            return defaultValue;
        }

        private static double GetFirstDouble(Dictionary<string, object> data, params string[] keys)
        {
            if (data == null)
                return 0;

            foreach (string key in keys)
            {
                if (data.ContainsKey(key) && data[key] != null)
                    return GetDouble(data, key);
            }

            return 0;
        }

        private static bool GetBool(Dictionary<string, object> data, string key, bool defaultValue = false)
        {
            if (data == null || !data.ContainsKey(key))
                return defaultValue;

            object value = data[key];
            if (value == null)
                return defaultValue;

            if (value is bool boolVal) return boolVal;
            if (value is int intVal) return intVal != 0;
            if (value is double dblVal) return dblVal != 0;

            string s = value.ToString().Trim().ToLowerInvariant();
            if (s == "true" || s == "1" || s == "yes" || s == "sim" || s == "on") return true;
            if (s == "false" || s == "0" || s == "no" || s == "nao" || s == "off") return false;

            return defaultValue;
        }

        private static bool GetFirstBool(Dictionary<string, object> data, params string[] keys)
        {
            return GetFirstBool(data, false, keys);
        }

        private static bool GetFirstBool(Dictionary<string, object> data, bool defaultValue, params string[] keys)
        {
            if (data == null)
                return defaultValue;

            foreach (string key in keys)
            {
                if (data.ContainsKey(key) && data[key] != null)
                    return GetBool(data, key, defaultValue);
            }

            return defaultValue;
        }

        private static List<object> GetList(Dictionary<string, object> data, string key)
        {
            if (data == null || !data.ContainsKey(key))
                return null;

            object value = data[key];
            if (value == null)
                return null;

            var list = value as List<object>;
            if (list != null)
                return list;

            // Aceita um único objeto no lugar de uma lista de um elemento
            var single = value as Dictionary<string, object>;
            if (single != null)
                return new List<object> { single };

            // ...ou um valor escalar (ex.: "requiredQuests": "quest_a")
            return new List<object> { value };
        }

        private static List<object> GetFirstList(Dictionary<string, object> data, params string[] keys)
        {
            if (data == null)
                return null;

            foreach (string key in keys)
            {
                var list = GetList(data, key);
                if (list != null && list.Count > 0)
                    return list;
            }

            return null;
        }

        private static T ParseEnum<T>(string value, T defaultValue) where T : struct
        {
            if (string.IsNullOrEmpty(value))
                return defaultValue;

            string normalized = Normalize(value);

            if (Enum.TryParse(normalized, true, out T result))
                return result;

            // Valores documentados no tutorial que não existem no enum
            string aliasKey = typeof(T).Name + ":" + normalized.ToLowerInvariant();
            string alias;

            if (_enumAliases.TryGetValue(aliasKey, out alias) && Enum.TryParse(alias, true, out T aliased))
                return aliased;

            Warn("Valor '" + value + "' não reconhecido para " + typeof(T).Name + "; usando " + defaultValue + ".");
            return defaultValue;
        }

        private static readonly Dictionary<string, string> _enumAliases =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "QuestCategory:hidden", "Side" },
                { "QuestCategory:secret", "Side" },
                { "QuestCategory:story", "Main" },
                { "QuestCategory:mainstory", "Main" },
                { "QuestCategory:class", "Profession" },
                { "QuestCategory:craft", "Profession" },
                { "QuestCategory:reputation", "Side" },
                { "QuestCategory:raid", "Event" },
                { "QuestCategory:group", "Event" },
                { "QuestDifficulty:elite", "Epic" },
                { "QuestDifficulty:extreme", "Legendary" },
                { "QuestDifficulty:medium", "Normal" },
                { "QuestDifficulty:average", "Normal" },
                { "QuestDifficulty:veryeasy", "Trivial" },
                { "QuestDifficulty:beginner", "Trivial" },
                { "QuestDifficulty:tough", "Hard" },
                { "QuestDifficulty:brutal", "VeryHard" },
                { "QuestDifficulty:mythic", "Legendary" }
            };

        #endregion

    }

    #region Simple JSON Parser

    /// <summary>
    /// Parser JSON simples (sem dependências externas)
    /// </summary>
    public class SimpleJsonParser
    {
        private string _json;
        private int _index;

        public Dictionary<string, object> Parse(string json)
        {
            if (string.IsNullOrEmpty(json))
                return null;

            _json = json;
            _index = 0;

            SkipWhitespace();

            if (_index < _json.Length && _json[_index] == '{')
            {
                return ParseObject();
            }

            return null;
        }

        private Dictionary<string, object> ParseObject()
        {
            var result = new Dictionary<string, object>();

            _index++;
            SkipWhitespace();

            while (_index < _json.Length && _json[_index] != '}')
            {
                SkipWhitespace();

                string key = ParseString();
                if (key == null)
                    break;

                SkipWhitespace();

                if (_index < _json.Length && _json[_index] == ':')
                    _index++;

                SkipWhitespace();

                object value = ParseValue();
                result[key] = value;

                SkipWhitespace();

                if (_index < _json.Length && _json[_index] == ',')
                    _index++;

                SkipWhitespace();
            }

            if (_index < _json.Length && _json[_index] == '}')
                _index++;

            return result;
        }

        private List<object> ParseArray()
        {
            var result = new List<object>();

            _index++;
            SkipWhitespace();

            while (_index < _json.Length && _json[_index] != ']')
            {
                SkipWhitespace();

                object value = ParseValue();
                result.Add(value);

                SkipWhitespace();

                if (_index < _json.Length && _json[_index] == ',')
                    _index++;

                SkipWhitespace();
            }

            if (_index < _json.Length && _json[_index] == ']')
                _index++;

            return result;
        }

        private object ParseValue()
        {
            SkipWhitespace();

            if (_index >= _json.Length)
                return null;

            char c = _json[_index];

            if (c == '"')
                return ParseString();
            else if (c == '{')
                return ParseObject();
            else if (c == '[')
                return ParseArray();
            else if (c == 't' || c == 'f')
                return ParseBool();
            else if (c == 'n')
                return ParseNull();
            else if (c == '-' || char.IsDigit(c))
                return ParseNumber();

            return null;
        }

        private string ParseString()
        {
            if (_index >= _json.Length || _json[_index] != '"')
                return null;

            _index++;

            var sb = new StringBuilder();

            while (_index < _json.Length)
            {
                char c = _json[_index];

                if (c == '"')
                {
                    _index++;
                    return sb.ToString();
                }
                else if (c == '\\' && _index + 1 < _json.Length)
                {
                    _index++;
                    char next = _json[_index];
                    switch (next)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (_index + 4 < _json.Length)
                            {
                                string hex = _json.Substring(_index + 1, 4);
                                if (int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out int code))
                                {
                                    sb.Append((char)code);
                                    _index += 4;
                                }
                            }
                            break;
                        default:
                            sb.Append(next);
                            break;
                    }
                }
                else
                {
                    sb.Append(c);
                }

                _index++;
            }

            return sb.ToString();
        }

        private object ParseNumber()
        {
            int start = _index;

            if (_json[_index] == '-')
                _index++;

            while (_index < _json.Length && char.IsDigit(_json[_index]))
                _index++;

            bool isDouble = false;

            if (_index < _json.Length && _json[_index] == '.')
            {
                isDouble = true;
                _index++;
                while (_index < _json.Length && char.IsDigit(_json[_index]))
                    _index++;
            }

            if (_index < _json.Length && (_json[_index] == 'e' || _json[_index] == 'E'))
            {
                isDouble = true;
                _index++;
                if (_index < _json.Length && (_json[_index] == '+' || _json[_index] == '-'))
                    _index++;
                while (_index < _json.Length && char.IsDigit(_json[_index]))
                    _index++;
            }

            string numStr = _json.Substring(start, _index - start);

            if (isDouble)
            {
                if (double.TryParse(numStr, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double d))
                    return d;
            }
            else
            {
                if (int.TryParse(numStr, out int i))
                    return i;
                if (long.TryParse(numStr, out long l))
                    return l;
            }

            return 0;
        }

        private bool ParseBool()
        {
            if (_index + 4 <= _json.Length && _json.Substring(_index, 4) == "true")
            {
                _index += 4;
                return true;
            }
            else if (_index + 5 <= _json.Length && _json.Substring(_index, 5) == "false")
            {
                _index += 5;
                return false;
            }
            return false;
        }

        private object ParseNull()
        {
            if (_index + 4 <= _json.Length && _json.Substring(_index, 4) == "null")
            {
                _index += 4;
                return null;
            }
            return null;
        }

        private void SkipWhitespace()
        {
            while (_index < _json.Length && char.IsWhiteSpace(_json[_index]))
            {
                _index++;
            }
        }
    }

    #endregion
}