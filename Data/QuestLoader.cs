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

        private static readonly string QuestsPath = Path.Combine(Core.BaseDirectory, "Data", "Quests");

        #endregion

        #region Load All Quests

        /// <summary>
        /// Carrega todas as quests da pasta Data/Quests
        /// </summary>
        public static List<QuestDefinition> LoadAllQuests()
        {
            var quests = new List<QuestDefinition>();

            if (!Directory.Exists(QuestsPath))
            {
                Directory.CreateDirectory(QuestsPath);
                Console.WriteLine("[QuestLoader] Pasta Data/Quests criada.");
                return quests;
            }

            string[] files = Directory.GetFiles(QuestsPath, "*.json", SearchOption.AllDirectories);

            foreach (string file in files)
            {
                try
                {
                    var loadedQuests = LoadQuestsFromFile(file);
                    quests.AddRange(loadedQuests);
                    Console.WriteLine($"[QuestLoader] Carregado: {Path.GetFileName(file)} ({loadedQuests.Count} quests)");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[QuestLoader] Erro ao carregar {file}: {ex.Message}");
                }
            }

            Console.WriteLine($"[QuestLoader] Total: {quests.Count} quests carregadas.");
            return quests;
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
                    quest.TimeLimitMinutes = GetInt(timeLimit, "minutes");
                    quest.OnTimeExpire = GetString(timeLimit, "onExpire") ?? "Fail";
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

        private static QuestObjective ParseObjective(Dictionary<string, object> data)
        {
            if (data == null)
                return null;

            string type = GetString(data, "type");
            QuestObjective objective = null;

            switch (type?.ToLower())
            {
                case "kill":
                    objective = new KillObjective
                    {
                        TargetType = GetString(data, "targetType"),
                        TargetName = GetString(data, "targetName"),
                        RequiredRegion = GetString(data, "requiresRegion"),
                        CountPartyKills = GetBool(data, "countPartyKills", true)
                    };
                    break;

                case "collect":
                    objective = new CollectObjective
                    {
                        ItemType = GetString(data, "itemType"),
                        ItemName = GetString(data, "itemName"),
                        ConsumeOnComplete = GetBool(data, "consumeOnComplete", true),
                        DropChance = GetInt(data, "dropChance", 100)
                    };
                    break;

                case "explore":
                    objective = new ExploreObjective
                    {
                        LocationName = GetString(data, "locationName"),
                        LocationX = GetInt(data, "locationX"),
                        LocationY = GetInt(data, "locationY"),
                        LocationZ = GetInt(data, "locationZ"),
                        MapName = GetString(data, "locationMap") ?? GetString(data, "map") ?? "Trammel",
                        RadiusTiles = GetInt(data, "radiusTiles", 5),
                        TriggerOnEnter = GetBool(data, "triggerOnEnter", true)
                    };
                    break;

                case "deliver":
                    objective = new DeliverObjective
                    {
                        ItemType = GetString(data, "itemType"),
                        ItemName = GetString(data, "itemName"),
                        GiveItemOnAccept = GetBool(data, "giveItemOnAccept", true),
                        TargetNpcId = GetString(data, "targetNpcId"),
                        TargetNpcName = GetString(data, "targetNpcName")
                    };
                    break;

                case "talkto":
                    var talkToObj = new TalkToObjective
                    {
                        TargetNpcId = GetString(data, "targetNpcId"),
                        TargetNpcName = GetString(data, "targetNpcName"),
                        SpeechTrigger = GetString(data, "speechTrigger")
                    };

                    // Lê itens a conceder ao completar o objetivo
                    if (data.ContainsKey("grantItems"))
                    {
                        var grantList = data["grantItems"] as List<object>;
                        if (grantList != null)
                        {
                            foreach (var grantItem in grantList)
                            {
                                var grantData = grantItem as Dictionary<string, object>;
                                if (grantData != null)
                                {
                                    talkToObj.GrantItems.Add(new ObjectiveGrantItem
                                    {
                                        ItemType = GetString(grantData, "itemType"),
                                        ItemName = GetString(grantData, "itemName"),
                                        Amount = GetInt(grantData, "amount", 1)
                                    });
                                }
                            }
                        }
                    }

                    objective = talkToObj;
                    break;

                case "escort":
                    objective = new EscortObjective
                    {
                        EscortNpcId = GetString(data, "escortNpcId"),
                        EscortNpcName = GetString(data, "escortNpcName"),
                        SpawnOnAccept = GetBool(data, "spawnOnAccept", true),
                        DestinationName = GetString(data, "destinationName") ?? GetNestedString(data, "destination", "name"),
                        DestinationX = GetInt(data, "destinationX") > 0 ? GetInt(data, "destinationX") : GetNestedInt(data, "destination", "x"),
                        DestinationY = GetInt(data, "destinationY") > 0 ? GetInt(data, "destinationY") : GetNestedInt(data, "destination", "y"),
                        RadiusToComplete = GetInt(data, "radiusToComplete", 5),
                        OnEscortDead = GetString(data, "onEscortDead") ?? "FailQuest"
                    };
                    break;

                case "craft":
                    objective = new CraftObjective
                    {
                        ItemType = GetString(data, "itemType"),
                        ItemName = GetString(data, "itemName"),
                        CraftSkill = GetString(data, "craftSkill"),
                        RequireExceptional = GetBool(data, "requireExceptional"),
                        ConsumeOnComplete = GetBool(data, "consumeOnComplete", true)
                    };
                    break;

                case "useskill":
                    var useSkillObj = new UseSkillObjective
                    {
                        SkillName = GetString(data, "skillName"),
                        RequireSuccess = GetBool(data, "requireSuccess")
                    };
                    string skillName1 = GetString(data, "skill");
                    if (TryParseSkillName(skillName1, out SkillName parsedSkill1))
                        useSkillObj.Skill = parsedSkill1;
                    objective = useSkillObj;
                    break;

                case "skillgain":
                    var skillGainObj = new SkillGainObjective
                    {
                        SkillName = GetString(data, "skillName"),
                        TargetValue = GetDouble(data, "required"),
                        Mode = GetString(data, "mode") ?? "ReachValue"
                    };
                    string skillName2 = GetString(data, "skill");
                    if (TryParseSkillName(skillName2, out SkillName parsedSkill2))
                        skillGainObj.Skill = parsedSkill2;
                    objective = skillGainObj;
                    break;

                case "interact":
                    objective = new InteractObjective
                    {
                        TargetType = GetString(data, "targetType"),
                        TargetName = GetString(data, "targetName"),
                        InteractionType = GetString(data, "interactionType") ?? "DoubleClick",
                        SpeechTrigger = GetString(data, "speechTrigger")
                    };
                    break;

                case "defend":
                    objective = new DefendObjective
                    {
                        LocationName = GetString(data, "locationName"),
                        LocationX = GetInt(data, "locationX"),
                        LocationY = GetInt(data, "locationY"),
                        MapName = GetString(data, "locationMap") ?? "Trammel",
                        RadiusTiles = GetInt(data, "radiusTiles", 15),
                        TotalWaves = GetInt(data, "waves", 3),
                        EnemyType = GetString(data, "enemyType"),
                        EnemiesPerWave = GetInt(data, "enemiesPerWave", 5)
                    };
                    break;

                case "destroy":
                    objective = new DestroyObjective
                    {
                        TargetType = GetString(data, "targetType"),
                        TargetName = GetString(data, "targetName"),
                        RespawnOnFail = GetBool(data, "respawnOnFail", true)
                    };
                    break;

                default:
                    Console.WriteLine($"[QuestLoader] Tipo de objetivo desconhecido: {type}");
                    return null;
            }

            // Propriedades comuns
            if (objective != null)
            {
                objective.Description = GetString(data, "description");
                objective.Required = GetInt(data, "required", 1);
                objective.LocationHint = GetString(data, "locationHint");
                objective.IsOptional = GetBool(data, "isOptional");
                objective.IsHidden = GetBool(data, "isHidden");
            }

            return objective;
        }

        #endregion

        #region Parse Prerequisites

        private static QuestPrerequisites ParsePrerequisites(Dictionary<string, object> data)
        {
            var prereqs = new QuestPrerequisites();

            if (data == null)
                return prereqs;

            prereqs.MinLevel = GetInt(data, "minLevel");
            prereqs.MinFame = GetInt(data, "minFame");
            prereqs.RequiredFaction = GetString(data, "requiredFaction");

            // Required Quests
            if (data.ContainsKey("questIds"))
            {
                var questIds = data["questIds"] as List<object>;
                if (questIds != null)
                {
                    foreach (var id in questIds)
                    {
                        if (id != null)
                            prereqs.RequiredQuests.Add(id.ToString());
                    }
                }
            }

            // Min Skill
            if (data.ContainsKey("minSkill"))
            {
                var skillData = data["minSkill"] as Dictionary<string, object>;
                if (skillData != null)
                {
                    var skillReq = new SkillRequirement
                    {
                        SkillName = GetString(skillData, "skill"),
                        Value = GetDouble(skillData, "value")
                    };
                    string skillName = skillReq.SkillName;
                    if (TryParseSkillName(skillName, out SkillName parsedSkill))
                        skillReq.Skill = parsedSkill;
                    prereqs.MinSkill = skillReq;
                }
            }

            // Min Karma
            if (data.ContainsKey("minKarma"))
            {
                prereqs.MinKarma = GetInt(data, "minKarma");
            }

            return prereqs;
        }

        #endregion

        #region Parse Rewards

        private static QuestRewards ParseRewards(Dictionary<string, object> data)
        {
            var rewards = new QuestRewards();

            if (data == null)
                return rewards;

            rewards.Gold = GetInt(data, "gold");
            rewards.Fame = GetInt(data, "fame");
            rewards.Karma = GetInt(data, "karma");
            rewards.Experience = GetInt(data, "experience");

            // Items
            if (data.ContainsKey("items"))
            {
                var items = data["items"] as List<object>;
                if (items != null)
                {
                    foreach (var item in items)
                    {
                        var itemData = item as Dictionary<string, object>;
                        if (itemData != null)
                        {
                            rewards.Items.Add(new RewardItem
                            {
                                Type = GetString(itemData, "type"),
                                Name = GetString(itemData, "name"),
                                Amount = GetInt(itemData, "amount", 1),
                                Hue = GetInt(itemData, "hue"),
                                Chance = GetInt(itemData, "chance", 100)
                            });
                        }
                    }
                }
            }

            // Skill Gains
            if (data.ContainsKey("skillGains"))
            {
                var skills = data["skillGains"] as List<object>;
                if (skills != null)
                {
                    foreach (var item in skills)
                    {
                        var skillData = item as Dictionary<string, object>;
                        if (skillData != null)
                        {
                            var skillReward = new SkillReward
                            {
                                SkillName = GetString(skillData, "skill"),
                                Amount = GetDouble(skillData, "amount")
                            };
                            string skillName = skillReward.SkillName;
                            if (TryParseSkillName(skillName, out SkillName parsedSkill))
                                skillReward.Skill = parsedSkill;
                            rewards.SkillGains.Add(skillReward);
                        }
                    }
                }
            }

            // Titles
            if (data.ContainsKey("titles"))
            {
                var titles = data["titles"] as List<object>;
                if (titles != null)
                {
                    foreach (var item in titles)
                    {
                        var titleData = item as Dictionary<string, object>;
                        if (titleData != null)
                        {
                            rewards.Titles.Add(new TitleReward
                            {
                                Title = GetString(titleData, "title"),
                                Type = GetString(titleData, "type") ?? "Suffix"
                            });
                        }
                    }
                }
            }

            // Unlocks
            if (data.ContainsKey("unlocks"))
            {
                var unlocks = data["unlocks"] as Dictionary<string, object>;
                if (unlocks != null)
                {
                    if (unlocks.ContainsKey("quests"))
                    {
                        var quests = unlocks["quests"] as List<object>;
                        if (quests != null)
                        {
                            foreach (var q in quests)
                            {
                                if (q != null)
                                    rewards.UnlockedQuests.Add(q.ToString());
                            }
                        }
                    }
                }
            }

            return rewards;
        }

        #endregion

        #region Parse Dialogue

        private static QuestDialogue ParseQuestDialogue(Dictionary<string, object> data, string questId, string questName)
        {
            if (data == null)
                return null;

            var dialogue = new QuestDialogue(questId, questName);

            if (data.ContainsKey("onAccept"))
            {
                dialogue.Accept.Lines.Clear();
                ParseDialogueLines(data["onAccept"], dialogue.Accept.Lines);
            }

            if (data.ContainsKey("onProgress"))
            {
                dialogue.Progress.Lines.Clear();
                ParseDialogueLines(data["onProgress"], dialogue.Progress.Lines);
            }

            if (data.ContainsKey("onComplete"))
            {
                dialogue.Complete.Lines.Clear();
                ParseDialogueLines(data["onComplete"], dialogue.Complete.Lines);
            }

            return dialogue;
        }

        private static void ParseDialogueLines(object data, List<DialogueLine> lines)
        {
            if (data is string text)
            {
                lines.Add(new DialogueLine(text, 2000));
            }
            else if (data is List<object> array)
            {
                foreach (var item in array)
                {
                    if (item is string line)
                    {
                        lines.Add(new DialogueLine(line, 2000));
                    }
                }
            }
        }

        #endregion

        #region Helper Methods

        private static string GetString(Dictionary<string, object> data, string key)
        {
            if (data == null || !data.ContainsKey(key))
                return null;
            return data[key]?.ToString();
        }

        private static string GetNestedString(Dictionary<string, object> data, string parentKey, string key)
        {
            if (data == null || !data.ContainsKey(parentKey))
                return null;

            var nested = data[parentKey] as Dictionary<string, object>;
            return GetString(nested, key);
        }

        private static int GetInt(Dictionary<string, object> data, string key, int defaultValue = 0)
        {
            if (data == null || !data.ContainsKey(key))
                return defaultValue;

            var value = data[key];
            if (value is int intVal)
                return intVal;
            if (value is long longVal)
                return (int)longVal;
            if (value is double doubleVal)
                return (int)doubleVal;
            if (int.TryParse(value?.ToString(), out int result))
                return result;

            return defaultValue;
        }

        private static int GetNestedInt(Dictionary<string, object> data, string parentKey, string key)
        {
            if (data == null || !data.ContainsKey(parentKey))
                return 0;

            var nested = data[parentKey] as Dictionary<string, object>;
            return GetInt(nested, key);
        }

        private static double GetDouble(Dictionary<string, object> data, string key, double defaultValue = 0)
        {
            if (data == null || !data.ContainsKey(key))
                return defaultValue;

            var value = data[key];
            if (value is double doubleVal)
                return doubleVal;
            if (value is int intVal)
                return intVal;
            if (value is long longVal)
                return longVal;
            if (double.TryParse(value?.ToString(), out double result))
                return result;

            return defaultValue;
        }

        private static bool GetBool(Dictionary<string, object> data, string key, bool defaultValue = false)
        {
            if (data == null || !data.ContainsKey(key))
                return defaultValue;

            var value = data[key];
            if (value is bool boolVal)
                return boolVal;
            if (bool.TryParse(value?.ToString(), out bool result))
                return result;

            return defaultValue;
        }

        private static T ParseEnum<T>(string value, T defaultValue) where T : struct
        {
            if (string.IsNullOrEmpty(value))
                return defaultValue;

            if (Enum.TryParse(value, true, out T result))
                return result;

            return defaultValue;
        }

        private static bool TryParseSkillName(string name, out SkillName skill)
        {
            skill = SkillName.Alchemy;

            if (string.IsNullOrEmpty(name))
                return false;

            return Enum.TryParse(name, true, out skill);
        }

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