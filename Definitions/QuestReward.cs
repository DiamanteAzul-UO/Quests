/*****************************************************
 * QuestReward.cs
 * Define as recompensas de quest
 * Etapa 4 - QuestDefinition
 * 
 * Versão: 1.0.0
 * Compatível com: ServUO
 *****************************************************/

using System;
using System.Collections.Generic;
using Server;

namespace Server.Custom.QuestSystem.Quests
{
    /// <summary>
    /// Contém todas as recompensas de uma quest
    /// </summary>
    public class QuestRewards
    {
        #region Properties

        /// <summary>
        /// Ouro
        /// </summary>
        public int Gold { get; set; }

        /// <summary>
        /// Depositar ouro no banco
        /// </summary>
        public bool BankDeposit { get; set; }

        /// <summary>
        /// Fama
        /// </summary>
        public int Fame { get; set; }

        /// <summary>
        /// Karma
        /// </summary>
        public int Karma { get; set; }

        /// <summary>
        /// Experiência (se usar sistema de XP)
        /// </summary>
        public int Experience { get; set; }

        /// <summary>
        /// Itens de recompensa
        /// </summary>
        public List<RewardItem> Items { get; set; }

        /// <summary>
        /// Itens aleatórios
        /// </summary>
        public List<RandomRewardPool> RandomItems { get; set; }

        /// <summary>
        /// Ganhos de skill
        /// </summary>
        public List<SkillReward> SkillGains { get; set; }

        /// <summary>
        /// Ganhos de stat
        /// </summary>
        public List<StatReward> StatGains { get; set; }

        /// <summary>
        /// Títulos
        /// </summary>
        public List<TitleReward> Titles { get; set; }

        /// <summary>
        /// Quests desbloqueadas
        /// </summary>
        public List<string> UnlockedQuests { get; set; }

        /// <summary>
        /// Áreas desbloqueadas
        /// </summary>
        public List<string> UnlockedAreas { get; set; }

        /// <summary>
        /// Receitas desbloqueadas
        /// </summary>
        public List<string> UnlockedRecipes { get; set; }

        #endregion

        #region Constructor

        public QuestRewards()
        {
            Items = new List<RewardItem>();
            RandomItems = new List<RandomRewardPool>();
            SkillGains = new List<SkillReward>();
            StatGains = new List<StatReward>();
            Titles = new List<TitleReward>();
            UnlockedQuests = new List<string>();
            UnlockedAreas = new List<string>();
            UnlockedRecipes = new List<string>();
        }

        #endregion

        #region Methods

        /// <summary>
        /// Retorna resumo das recompensas
        /// </summary>
        public string GetSummary()
        {
            var parts = new List<string>();

            if (Gold > 0)
                parts.Add($"{Gold} ouro");

            if (Fame > 0)
                parts.Add($"{Fame} fama");

            if (Karma != 0)
                parts.Add($"{Karma} karma");

            if (Items.Count > 0)
                parts.Add($"{Items.Count} item(ns)");

            if (SkillGains.Count > 0)
                parts.Add($"ganho de skill");

            return string.Join(", ", parts);
        }

        /// <summary>
        /// Retorna lista detalhada de recompensas
        /// </summary>
        public List<string> GetDetailedList()
        {
            var list = new List<string>();

            if (Gold > 0)
                list.Add($"Ouro: {Gold}");

            if (Fame > 0)
                list.Add($"Fama: +{Fame}");

            if (Karma > 0)
                list.Add($"Karma: +{Karma}");
            else if (Karma < 0)
                list.Add($"Karma: {Karma}");

            if (Experience > 0)
                list.Add($"Experiencia: {Experience}");

            foreach (var item in Items)
            {
                list.Add($"Item: {item.Amount}x {item.Name}");
            }

            foreach (var skill in SkillGains)
            {
                list.Add($"Skill: +{skill.Amount} {skill.SkillName}");
            }

            foreach (var title in Titles)
            {
                list.Add($"Titulo: {title.Title}");
            }

            return list;
        }

        /// <summary>
        /// Verifica se tem recompensas
        /// </summary>
        public bool HasRewards()
        {
            return Gold > 0 || Fame > 0 || Karma != 0 || 
                   Items.Count > 0 || SkillGains.Count > 0 || Titles.Count > 0;
        }

        /// <summary>
        /// Clona recompensas
        /// </summary>
        public QuestRewards Clone()
        {
            var clone = new QuestRewards
            {
                Gold = this.Gold,
                BankDeposit = this.BankDeposit,
                Fame = this.Fame,
                Karma = this.Karma,
                Experience = this.Experience
            };

            foreach (var item in Items)
                clone.Items.Add(item.Clone());

            foreach (var pool in RandomItems)
                clone.RandomItems.Add(pool.Clone());

            foreach (var skill in SkillGains)
                clone.SkillGains.Add(skill.Clone());

            foreach (var stat in StatGains)
                clone.StatGains.Add(stat.Clone());

            foreach (var title in Titles)
                clone.Titles.Add(title.Clone());

            clone.UnlockedQuests.AddRange(UnlockedQuests);
            clone.UnlockedAreas.AddRange(UnlockedAreas);
            clone.UnlockedRecipes.AddRange(UnlockedRecipes);

            return clone;
        }

        #endregion
    }

    #region Reward Item

    /// <summary>
    /// Item de recompensa
    /// </summary>
    public class RewardItem
    {
        public string Type { get; set; }
        public string Name { get; set; }
        public int Amount { get; set; }
        public int Hue { get; set; }
        public int Chance { get; set; }
        public Dictionary<string, object> Properties { get; set; }

        public RewardItem()
        {
            Amount = 1;
            Chance = 100;
            Properties = new Dictionary<string, object>();
        }

        public RewardItem Clone()
        {
            return new RewardItem
            {
                Type = this.Type,
                Name = this.Name,
                Amount = this.Amount,
                Hue = this.Hue,
                Chance = this.Chance,
                Properties = new Dictionary<string, object>(this.Properties)
            };
        }
    }

    #endregion

    #region Random Reward Pool

    /// <summary>
    /// Pool de itens aleatórios
    /// </summary>
    public class RandomRewardPool
    {
        public string PoolName { get; set; }
        public int PickCount { get; set; }
        public List<WeightedItem> Items { get; set; }

        public RandomRewardPool()
        {
            PickCount = 1;
            Items = new List<WeightedItem>();
        }

        public RandomRewardPool Clone()
        {
            var clone = new RandomRewardPool
            {
                PoolName = this.PoolName,
                PickCount = this.PickCount
            };

            foreach (var item in Items)
                clone.Items.Add(item.Clone());

            return clone;
        }
    }

    /// <summary>
    /// Item com peso para sorteio
    /// </summary>
    public class WeightedItem
    {
        public string Type { get; set; }
        public string Name { get; set; }
        public int Weight { get; set; }
        public int Amount { get; set; }
        public int Hue { get; set; }

        public WeightedItem()
        {
            Weight = 1;
            Amount = 1;
        }

        public WeightedItem Clone()
        {
            return new WeightedItem
            {
                Type = this.Type,
                Name = this.Name,
                Weight = this.Weight,
                Amount = this.Amount,
                Hue = this.Hue
            };
        }
    }

    #endregion

    #region Skill Reward

    /// <summary>
    /// Recompensa de skill
    /// </summary>
    public class SkillReward
    {
        public SkillName Skill { get; set; }
        public string SkillName { get; set; }
        public double Amount { get; set; }
        public double CapIncrease { get; set; }

        public SkillReward Clone()
        {
            return new SkillReward
            {
                Skill = this.Skill,
                SkillName = this.SkillName,
                Amount = this.Amount,
                CapIncrease = this.CapIncrease
            };
        }
    }

    #endregion

    #region Stat Reward

    /// <summary>
    /// Recompensa de stat
    /// </summary>
    public class StatReward
    {
        public string Stat { get; set; } // "Str", "Dex", "Int"
        public int Amount { get; set; }

        public StatReward Clone()
        {
            return new StatReward
            {
                Stat = this.Stat,
                Amount = this.Amount
            };
        }
    }

    #endregion

    #region Title Reward

    /// <summary>
    /// Recompensa de título
    /// </summary>
    public class TitleReward
    {
        public string Title { get; set; }
        public string Type { get; set; } // "Prefix", "Suffix", "Custom"

        public TitleReward Clone()
        {
            return new TitleReward
            {
                Title = this.Title,
                Type = this.Type
            };
        }
    }

    #endregion
}