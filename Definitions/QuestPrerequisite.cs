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
        /// Verifica se jogador atende aos pré-requisitos
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
            // TODO: Integrar com QuestManager

            foreach (string questId in RequiredQuests)
            {
                var playerData = QuestManager.Instance.GetPlayerData(player);
                if (playerData == null || !playerData.HasCompletedQuest(questId))
                {
                    failReason = "Voce precisa completar a missao anterior primeiro.";
                    return false;
                }
            }

            // Verifica fama
            if (MinFame > 0 && player.Fame < MinFame)
            {
                failReason = $"Voce precisa de pelo menos {MinFame} de fama.";
                return false;
            }

            // Verifica karma mínimo
            if (MinKarma.HasValue && player.Karma < MinKarma.Value)
            {
                failReason = $"Seu karma e muito baixo para esta missao.";
                return false;
            }

            // Verifica karma máximo
            if (MaxKarma.HasValue && player.Karma > MaxKarma.Value)
            {
                failReason = $"Seu karma e muito alto para esta missao.";
                return false;
            }

            // Verifica skill mínima principal
            if (MinSkill != null && MinSkill.Value > 0)
            {
                double playerSkill = player.Skills[MinSkill.Skill].Value;
                if (playerSkill < MinSkill.Value)
                {
                    failReason = $"Voce precisa de {MinSkill.Value} em {MinSkill.SkillName}.";
                    return false;
                }
            }

            // Verifica lista de skills
            foreach (var req in RequiredSkills)
            {
                double playerSkill = player.Skills[req.Skill].Value;
                if (playerSkill < req.Value)
                {
                    failReason = $"Voce precisa de {req.Value} em {req.SkillName}.";
                    return false;
                }
            }

            // Verifica itens
            foreach (var item in RequiredItems)
            {
                if (!HasItem(player, item))
                {
                    failReason = $"Voce precisa de {item.Amount}x {item.Name}.";
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Verifica se tem pré-requisitos definidos
        /// </summary>
        public bool HasPrerequisites()
        {
            return RequiredQuests.Count > 0 || MinLevel > 0 || MinSkill != null ||
                   RequiredSkills.Count > 0 || MinFame > 0 || MinKarma.HasValue ||
                   RequiredItems.Count > 0;
        }

        /// <summary>
        /// Verifica se jogador tem item - suporta tipos custom por nome
        /// </summary>
        private bool HasItem(PlayerMobile player, RequiredItem req)
        {
            if (player?.Backpack == null)
                return false;

            Type itemType = ScriptCompiler.FindTypeByName(req.Type);

            if (itemType != null)
            {
                // Tipo existe no ServUO - busca normal
                Item[] items = player.Backpack.FindItemsByType(itemType);
                int total = 0;

                foreach (Item item in items)
                {
                    if (req.Hue == 0 || item.Hue == req.Hue)
                        total += item.Amount;
                }

                return total >= req.Amount;
            }

            // Tipo custom - busca por nome
            int count = 0;

            foreach (Item item in player.Backpack.Items)
            {
                if (item == null || item.Deleted)
                    continue;

                bool matches = false;

                if (item.Name != null && item.Name.IndexOf(req.Type, StringComparison.OrdinalIgnoreCase) >= 0)
                    matches = true;

                if (!matches && item is QuestItem qi &&
                    qi.QuestItemType != null &&
                    qi.QuestItemType.Equals(req.Type, StringComparison.OrdinalIgnoreCase))
                    matches = true;

                if (matches)
                {
                    if (req.Hue == 0 || item.Hue == req.Hue)
                        count += item.Amount;
                }
            }

            return count >= req.Amount;
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