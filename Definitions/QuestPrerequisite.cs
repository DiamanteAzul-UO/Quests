/*****************************************************
 * QuestPrerequisite.cs
 * Define os pré-requisitos de quest
 * Etapa 4 - QuestDefinition
 * 
 * Versão: 1.0.0
 * Compatível com: ServUO
 *****************************************************/

using System;
using System.Collections.Generic;
using Server;
using Server.Mobiles;

namespace Server.Custom.QuestSystem.Quests
{
    /// <summary>
    /// Pré-requisitos para aceitar uma quest
    /// </summary>
    public class QuestPrerequisites
    {
        #region Properties

        /// <summary>
        /// Quests que devem estar completas
        /// </summary>
        public List<string> RequiredQuests { get; set; }

        /// <summary>
        /// Nível mínimo (se usar sistema de level)
        /// </summary>
        public int MinLevel { get; set; }

        /// <summary>
        /// Skill mínima requerida
        /// </summary>
        public SkillRequirement MinSkill { get; set; }

        /// <summary>
        /// Lista de skills mínimas
        /// </summary>
        public List<SkillRequirement> RequiredSkills { get; set; }

        /// <summary>
        /// Fama mínima
        /// </summary>
        public int MinFame { get; set; }

        /// <summary>
        /// Karma mínimo (pode ser negativo)
        /// </summary>
        public int? MinKarma { get; set; }

        /// <summary>
        /// Karma máximo
        /// </summary>
        public int? MaxKarma { get; set; }

        /// <summary>
        /// Facção requerida
        /// </summary>
        public string RequiredFaction { get; set; }

        /// <summary>
        /// Guilda requerida
        /// </summary>
        public string RequiredGuild { get; set; }

        /// <summary>
        /// Itens que o jogador deve possuir
        /// </summary>
        public List<RequiredItem> RequiredItems { get; set; }

        /// <summary>
        /// Raças permitidas
        /// </summary>
        public List<string> AllowedRaces { get; set; }

        /// <summary>
        /// Mensagem quando pré-requisitos não são atendidos
        /// </summary>
        public string FailMessage { get; set; }

        #endregion

        #region Constructor

        public QuestPrerequisites()
        {
            RequiredQuests = new List<string>();
            RequiredSkills = new List<SkillRequirement>();
            RequiredItems = new List<RequiredItem>();
            AllowedRaces = new List<string>();
        }

        #endregion

        #region Methods

        /// <summary>
        /// Verifica se jogador atende aos pré-requisitos.
        ///
        /// Correções desta revisão:
        ///  - MinLevel, RequiredFaction, RequiredGuild e AllowedRaces eram lidos
        ///    do JSON mas nunca verificados (agora são).
        ///  - FailMessage definido pelo GM é usado quando presente.
        ///  - HasItem passa a procurar no inventário inteiro (sub-containers
        ///    incluídos) e reconhece itens de quest virtuais pela tag, não só
        ///    pelo nome — antes, "SeloFortaleza" nunca era encontrado porque o
        ///    item se chama "Selo da Fortaleza".
        /// </summary>
        public bool Check(PlayerMobile player, out string failReason)
        {
            failReason = null;

            if (player == null)
            {
                failReason = "Jogador invalido.";
                return false;
            }

            // Verifica quests completas
            foreach (string questId in RequiredQuests)
            {
                if (string.IsNullOrWhiteSpace(questId))
                    continue;

                var playerData = QuestManager.Instance.GetPlayerData(player);
                if (playerData == null || !playerData.HasCompletedQuest(questId))
                {
                    failReason = Fail("Voce precisa completar a missao anterior primeiro.");
                    return false;
                }
            }

            // Nível mínimo. O ServUO puro não tem sistema de level, então a
            // verificação usa uma propriedade "Level"/"ExpLevel" quando o shard
            // a fornece. Se não existir, o requisito é ignorado — falhar aqui
            // tornaria a quest impossível para sempre.
            if (MinLevel > 0)
            {
                int level = ReadLevel(player);

                if (level >= 0 && level < MinLevel)
                {
                    failReason = Fail($"Voce precisa ser nivel {MinLevel}.");
                    return false;
                }
            }

            // Verifica fama
            if (MinFame > 0 && player.Fame < MinFame)
            {
                failReason = Fail($"Voce precisa de pelo menos {MinFame} de fama.");
                return false;
            }

            // Verifica karma mínimo
            if (MinKarma.HasValue && player.Karma < MinKarma.Value)
            {
                failReason = Fail("Voce precisa de karma mais alto para esta missao.");
                return false;
            }

            // Verifica karma máximo
            if (MaxKarma.HasValue && player.Karma > MaxKarma.Value)
            {
                failReason = Fail("Seu karma e alto demais para esta missao.");
                return false;
            }

            // Verifica skill mínima principal
            if (MinSkill != null && MinSkill.Value > 0 && !HasSkillValue(player, MinSkill))
            {
                failReason = Fail($"Voce precisa de {MinSkill.Value:F1} em {MinSkill.SkillName}.");
                return false;
            }

            // Verifica lista de skills
            foreach (var req in RequiredSkills)
            {
                if (req == null || req.Value <= 0)
                    continue;

                if (!HasSkillValue(player, req))
                {
                    failReason = Fail($"Voce precisa de {req.Value:F1} em {req.SkillName}.");
                    return false;
                }
            }

            // Verifica itens
            foreach (var item in RequiredItems)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.Type))
                    continue;

                if (!HasItem(player, item))
                {
                    string label = !string.IsNullOrWhiteSpace(item.Name) ? item.Name : item.Type;
                    failReason = Fail($"Voce precisa de {item.Amount}x {label}.");
                    return false;
                }
            }

            // Facção
            if (!string.IsNullOrWhiteSpace(RequiredFaction))
            {
                var faction = Server.Factions.Faction.Find(player);
                bool member = false;

                if (faction != null && faction.Definition != null)
                {
                    member = EqualsIgnoreCase(faction.Definition.FriendlyName, RequiredFaction) ||
                             EqualsIgnoreCase(faction.Definition.Keyword, RequiredFaction) ||
                             EqualsIgnoreCase(faction.Definition.Abbreviation, RequiredFaction);
                }

                if (!member)
                {
                    failReason = Fail($"Esta missao e apenas para {RequiredFaction}.");
                    return false;
                }
            }

            // Guilda
            if (!string.IsNullOrWhiteSpace(RequiredGuild))
            {
                string guildName = player.Guild != null ? player.Guild.Name : null;
                string guildAbbr = player.Guild != null ? player.Guild.Abbreviation : null;

                if (!EqualsIgnoreCase(guildName, RequiredGuild) && !EqualsIgnoreCase(guildAbbr, RequiredGuild))
                {
                    failReason = Fail($"Esta missao e apenas para membros de {RequiredGuild}.");
                    return false;
                }
            }

            // Raças permitidas
            if (AllowedRaces.Count > 0)
            {
                string raceName = player.Race != null ? player.Race.Name : null;
                bool allowed = false;

                foreach (string race in AllowedRaces)
                {
                    if (string.IsNullOrWhiteSpace(race))
                        continue;

                    if (raceName != null && raceName.Equals(race, StringComparison.OrdinalIgnoreCase))
                    {
                        allowed = true;
                        break;
                    }

                    // Aceita também "Human"/"Elf"/"Gargoyle" escritos à mão
                    if (raceName != null &&
                        QuestItemMatcher.Normalize(raceName).Contains(QuestItemMatcher.Normalize(race)))
                    {
                        allowed = true;
                        break;
                    }
                }

                if (!allowed)
                {
                    failReason = Fail("Sua raca nao pode fazer esta missao.");
                    return false;
                }
            }

            return true;
        }

        private string Fail(string defaultMessage)
        {
            return string.IsNullOrWhiteSpace(FailMessage) ? defaultMessage : FailMessage;
        }

        private static bool EqualsIgnoreCase(string a, string b)
        {
            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
                return false;

            return a.Trim().Equals(b.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Lê o level do jogador. Retorna -1 quando o shard não tem sistema de
        /// level, sinalizando que o requisito deve ser ignorado.
        /// </summary>
        private static int ReadLevel(PlayerMobile player)
        {
            foreach (string propertyName in new[] { "Level", "ExpLevel", "PlayerLevel" })
            {
                try
                {
                    var property = player.GetType().GetProperty(propertyName,
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                    if (property != null && property.PropertyType == typeof(int))
                    {
                        object value = property.GetValue(player, null);
                        if (value != null)
                            return (int)value;
                    }
                }
                catch
                {
                    // ignora e tenta o próximo nome
                }
            }

            return -1;
        }

        private static bool HasSkillValue(PlayerMobile player, SkillRequirement req)
        {
            if (player == null || req == null)
                return false;

            var skill = player.Skills[req.Skill];
            return skill != null && skill.Value >= req.Value;
        }

        /// <summary>
        /// Verifica se tem pré-requisitos definidos
        /// </summary>
        public bool HasPrerequisites()
        {
            return RequiredQuests.Count > 0 || MinLevel > 0 || MinSkill != null ||
                   RequiredSkills.Count > 0 || MinFame > 0 || MinKarma.HasValue ||
                   MaxKarma.HasValue || RequiredItems.Count > 0 ||
                   AllowedRaces.Count > 0 ||
                   !string.IsNullOrWhiteSpace(RequiredFaction) ||
                   !string.IsNullOrWhiteSpace(RequiredGuild);
        }

        /// <summary>
        /// Verifica se o jogador possui o item exigido.
        ///
        /// Usa o QuestItemMatcher, que cobre três casos de uma vez:
        ///  1. classe real do ServUO ("Apple", "Bandage", "IronOre");
        ///  2. classe custom do shard presente no assembly;
        ///  3. item virtual de quest ("SeloFortaleza", "CartaLacradaOlin") —
        ///     representado por QuestItem com tag, que é como o sistema entrega
        ///     esses itens nas recompensas e nos grantItems.
        ///
        /// A busca percorre o inventário inteiro, não só o primeiro nível da
        /// mochila, e respeita a hue quando declarada.
        /// </summary>
        private bool HasItem(PlayerMobile player, RequiredItem req)
        {
            if (player == null || player.Deleted || req == null)
                return false;

            int amount = req.Amount > 0 ? req.Amount : 1;

            return QuestItemMatcher.CountInPack(player, req.Type, req.Name, req.Hue) >= amount;
        }
        /// <summary>
        /// Clona pré-requisitos
        /// </summary>
        public QuestPrerequisites Clone()
        {
            var clone = new QuestPrerequisites
            {
                MinLevel = this.MinLevel,
                MinFame = this.MinFame,
                MinKarma = this.MinKarma,
                MaxKarma = this.MaxKarma,
                RequiredFaction = this.RequiredFaction,
                RequiredGuild = this.RequiredGuild,
                FailMessage = this.FailMessage
            };

            clone.RequiredQuests.AddRange(this.RequiredQuests);
            clone.AllowedRaces.AddRange(this.AllowedRaces);

            if (this.MinSkill != null)
                clone.MinSkill = this.MinSkill.Clone();

            foreach (var skill in this.RequiredSkills)
                clone.RequiredSkills.Add(skill.Clone());

            foreach (var item in this.RequiredItems)
                clone.RequiredItems.Add(item.Clone());

            return clone;
        }

        #endregion
    }

    #region Skill Requirement

    /// <summary>
    /// Requisito de skill
    /// </summary>
    public class SkillRequirement
    {
        public SkillName Skill { get; set; }
        public string SkillName { get; set; }
        public double Value { get; set; }

        public SkillRequirement() { }

        public SkillRequirement(SkillName skill, double value)
        {
            Skill = skill;
            SkillName = skill.ToString();
            Value = value;
        }

        public SkillRequirement Clone()
        {
            return new SkillRequirement
            {
                Skill = this.Skill,
                SkillName = this.SkillName,
                Value = this.Value
            };
        }
    }

    #endregion

    #region Required Item

    /// <summary>
    /// Item requerido
    /// </summary>
    public class RequiredItem
    {
        public string Type { get; set; }
        public string Name { get; set; }
        public int Amount { get; set; }
        public int Hue { get; set; }
        public bool Consume { get; set; }

        public RequiredItem()
        {
            Amount = 1;
            Consume = false;
        }

        public RequiredItem Clone()
        {
            return new RequiredItem
            {
                Type = this.Type,
                Name = this.Name,
                Amount = this.Amount,
                Hue = this.Hue,
                Consume = this.Consume
            };
        }
    }

    #endregion
}