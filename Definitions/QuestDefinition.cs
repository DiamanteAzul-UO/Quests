/*****************************************************
 * QuestDefinition.cs
 * Estrutura principal de uma Quest
 * Etapa 4 - QuestDefinition
 * 
 * Versão: 1.0.0
 * Compatível com: ServUO
 *****************************************************/

using System;
using System.Collections.Generic;
using Server;
using Server.Custom.QuestSystem.Dialogue;

namespace Server.Custom.QuestSystem.Quests
{
    /// <summary>
    /// Definição completa de uma quest
    /// </summary>
    public class QuestDefinition
    {
        #region Identity

        /// <summary>
        /// ID único da quest
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Nome da quest
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Descrição da quest
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Categoria (Tutorial, Main, Side, Daily, etc.)
        /// </summary>
        public QuestCategory Category { get; set; }

        /// <summary>
        /// Dificuldade
        /// </summary>
        public QuestDifficulty Difficulty { get; set; }

        /// <summary>
        /// ID da chain de quests
        /// </summary>
        public string ChainId { get; set; }

        /// <summary>
        /// Ordem na chain
        /// </summary>
        public int ChainOrder { get; set; }

        /// <summary>
        /// Ícone (item ID)
        /// </summary>
        public int Icon { get; set; }

        /// <summary>
        /// Cor do ícone
        /// </summary>
        public int IconHue { get; set; }

        #endregion

        #region NPCs

        /// <summary>
        /// ID do NPC que dá a quest
        /// </summary>
        public string GiverNpcId { get; set; }

        /// <summary>
        /// Nome do NPC que dá a quest
        /// </summary>
        public string GiverNpcName { get; set; }

        /// <summary>
        /// ID do NPC de entrega
        /// </summary>
        public string TurnInNpcId { get; set; }

        /// <summary>
        /// Nome do NPC de entrega
        /// </summary>
        public string TurnInNpcName { get; set; }

        #endregion

        #region Settings

        /// <summary>
        /// Quest repetível
        /// </summary>
        public bool Repeatable { get; set; }

        /// <summary>
        /// Cooldown em horas
        /// </summary>
        public int CooldownHours { get; set; }

        /// <summary>
        /// Máximo de vezes que pode completar
        /// </summary>
        public int MaxCompletions { get; set; }

        /// <summary>
        /// Pode compartilhar com grupo
        /// </summary>
        public bool Shareable { get; set; }

        /// <summary>
        /// Inicia automaticamente ao falar com NPC
        /// </summary>
        public bool AutoStart { get; set; }

        #endregion

        #region Time Limit

        /// <summary>
        /// Tem limite de tempo
        /// </summary>
        public bool HasTimeLimit { get; set; }

        /// <summary>
        /// Tempo limite em minutos
        /// </summary>
        public int TimeLimitMinutes { get; set; }

        /// <summary>
        /// Ação quando tempo expira
        /// </summary>
        public string OnTimeExpire { get; set; }

        /// <summary>
        /// Avisos de tempo restante (em minutos)
        /// </summary>
        public List<int> TimeWarnings { get; set; }

        #endregion

        #region Requirements & Objectives

        /// <summary>
        /// Pré-requisitos
        /// </summary>
        public QuestPrerequisites Prerequisites { get; set; }

        /// <summary>
        /// Objetivos devem ser sequenciais
        /// </summary>
        public bool SequentialObjectives { get; set; }

        /// <summary>
        /// Lista de objetivos
        /// </summary>
        public List<QuestObjective> Objectives { get; set; }

        #endregion

        #region Rewards

        /// <summary>
        /// Recompensas
        /// </summary>
        public QuestRewards Rewards { get; set; }

        #endregion

        #region Dialogue

        /// <summary>
        /// Diálogos customizados
        /// </summary>
        public QuestDialogue Dialogue { get; set; }

        #endregion

        #region Chain

        /// <summary>
        /// Próxima quest na chain
        /// </summary>
        public string NextQuestId { get; set; }

        /// <summary>
        /// Mensagem de conclusão
        /// </summary>
        public string CompletionMessage { get; set; }

        #endregion

        #region Constructor

        public QuestDefinition()
        {
            Category = QuestCategory.Side;
            Difficulty = QuestDifficulty.Normal;
            Prerequisites = new QuestPrerequisites();
            Objectives = new List<QuestObjective>();
            Rewards = new QuestRewards();
            TimeWarnings = new List<int>();
            OnTimeExpire = "Fail";
        }

        public QuestDefinition(string id, string name) : this()
        {
            Id = id;
            Name = name;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Verifica se todos os objetivos obrigatorios estão completos.
        /// Objetivos marcados como IsOptional NAO impedem a conclusão —
        /// é exatamente isso que ActiveQuest.CheckCompletion consulta.
        /// </summary>
        public bool AreObjectivesComplete(List<ObjectiveProgress> progress)
        {
            // Antes, qualquer diferença entre o tamanho de progress e o número de
            // objetivos fazia a quest nunca poder ser entregue — caso comum quando
            // o JSON é editado depois que o jogador já aceitou a quest. Agora a
            // comparação é feita item a item e faltas contam como "não completo",
            // enquanto sobras são ignoradas.
            if (progress == null)
                return Objectives.Count == 0;

            if (Objectives.Count == 0)
                return false;

            for (int i = 0; i < Objectives.Count; i++)
            {
                if (Objectives[i] == null)
                    continue;

                if (Objectives[i].IsOptional)
                    continue;

                if (i >= progress.Count || progress[i] == null)
                    return false;

                if (!progress[i].IsComplete)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Obtém índice do próximo objetivo OBRIGATÓRIO pendente.
        /// Objetivos opcionais nunca bloqueiam a fila — antes disso, um
        /// opcional não feito travava todos os objetivos seguintes para sempre.
        /// </summary>
        public int GetNextObjectiveIndex(List<ObjectiveProgress> progress)
        {
            if (!SequentialObjectives)
                return -1; // Todos ativos

            if (progress == null)
                return -1;

            for (int i = 0; i < Objectives.Count && i < progress.Count; i++)
            {
                if (Objectives[i].IsOptional)
                    continue;

                if (!progress[i].IsComplete)
                    return i;
            }

            return -1; // Todos completos
        }

        /// <summary>
        /// Verifica se objetivo está ativo (aceita progresso)
        /// </summary>
        public bool IsObjectiveActive(int index, List<ObjectiveProgress> progress)
        {
            if (index < 0 || index >= Objectives.Count)
                return false;

            if (progress == null || index >= progress.Count)
                return false;

            if (progress[index].IsComplete)
                return false;

            if (!SequentialObjectives)
                return true;

            // Objetivo opcional: libera assim que todos os obrigatorios
            // anteriores estiverem prontos.
            if (Objectives[index].IsOptional)
            {
                for (int i = 0; i < index; i++)
                {
                    if (Objectives[i].IsOptional)
                        continue;

                    if (!progress[i].IsComplete)
                        return false;
                }

                return true;
            }

            return GetNextObjectiveIndex(progress) == index;
        }

        /// <summary>
        /// Clona a definição (para instância do jogador)
        /// </summary>
        public QuestDefinition Clone()
        {
            var clone = new QuestDefinition
            {
                Id = this.Id,
                Name = this.Name,
                Description = this.Description,
                Category = this.Category,
                Difficulty = this.Difficulty,
                ChainId = this.ChainId,
                ChainOrder = this.ChainOrder,
                Icon = this.Icon,
                IconHue = this.IconHue,
                GiverNpcId = this.GiverNpcId,
                GiverNpcName = this.GiverNpcName,
                TurnInNpcId = this.TurnInNpcId,
                TurnInNpcName = this.TurnInNpcName,
                Repeatable = this.Repeatable,
                CooldownHours = this.CooldownHours,
                MaxCompletions = this.MaxCompletions,
                Shareable = this.Shareable,
                AutoStart = this.AutoStart,
                HasTimeLimit = this.HasTimeLimit,
                TimeLimitMinutes = this.TimeLimitMinutes,
                OnTimeExpire = this.OnTimeExpire,
                SequentialObjectives = this.SequentialObjectives,
                NextQuestId = this.NextQuestId,
                CompletionMessage = this.CompletionMessage
            };

            clone.TimeWarnings.AddRange(this.TimeWarnings);
            clone.Prerequisites = this.Prerequisites?.Clone();
            clone.Rewards = this.Rewards?.Clone();

            foreach (var obj in this.Objectives)
                clone.Objectives.Add(obj.Clone());

            return clone;
        }

        /// <summary>
        /// Cria lista de progresso inicial
        /// </summary>
        public List<ObjectiveProgress> CreateProgressList()
        {
            var list = new List<ObjectiveProgress>();

            for (int i = 0; i < Objectives.Count; i++)
            {
                list.Add(new ObjectiveProgress
                {
                    Index = i,
                    Type = Objectives[i].Type,
                    Current = 0,
                    Required = Objectives[i].Required,
                    IsComplete = Objectives[i].Required <= 0,
                    IsOptional = Objectives[i].IsOptional,
                    IsHidden = Objectives[i].IsHidden,
                    Description = Objectives[i].Description
                });
            }

            return list;
        }

        #endregion

        #region Validation

        /// <summary>
        /// Valida a definição da quest
        /// </summary>
        public bool Validate(out List<string> errors)
        {
            errors = new List<string>();

            if (string.IsNullOrEmpty(Id))
                errors.Add("ID da quest e obrigatorio.");

            if (string.IsNullOrEmpty(Name))
                errors.Add("Nome da quest e obrigatorio.");

            if (Objectives.Count == 0)
                errors.Add("Quest deve ter pelo menos um objetivo.");

            foreach (var obj in Objectives)
            {
                if (obj.Required <= 0)
                    errors.Add($"Objetivo '{obj.Description}' deve ter Required > 0.");
            }

            return errors.Count == 0;
        }

        #endregion
    }

    #region Objective Progress

    /// <summary>
    /// Progresso de um objetivo (para PlayerQuestData)
    /// </summary>
    public class ObjectiveProgress
    {
        public int Index { get; set; }
        public ObjectiveType Type { get; set; }
        public int Current { get; set; }
        public int Required { get; set; }
        public bool IsComplete { get; set; }
        public string Description { get; set; }

        /// <summary>
        /// Objetivo opcional: nao impede a conclusao da quest.
        /// Copiado da definicao em CreateProgressList e persistido junto do
        /// progresso para que ActiveQuest.CheckCompletion funcione sem precisar
        /// consultar o repositorio.
        /// </summary>
        public bool IsOptional { get; set; }

        /// <summary>Objetivo oculto ate ser descoberto.</summary>
        public bool IsHidden { get; set; }

        public void AddProgress(int amount = 1)
        {
            Current = Math.Min(Current + amount, Required);
            IsComplete = Required <= 0 || Current >= Required;
        }

        /// <summary>Define o progresso absoluto (usado por objetivos contados por inventario).</summary>
        public void SetProgress(int value)
        {
            Current = Math.Max(0, Math.Min(value, Required));
            IsComplete = Required <= 0 || Current >= Required;
        }

        public string GetProgressText()
        {
            return $"{Description} ({Current}/{Required})";
        }

        public float GetProgressPercent()
        {
            if (Required <= 0) return 100f;
            return (float)Current / Required * 100f;
        }
    }

    #endregion
}