/*****************************************************
 * PlayerQuestData.cs
 * Dados de quests do jogador
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
    /// Dados de quests de um jogador
    /// </summary>
    public class PlayerQuestData
    {
        #region Properties



        /// <summary>
        /// Serial do jogador
        /// </summary>
        public Serial PlayerSerial { get; set; }

        /// <summary>
        /// Nome do jogador (cache)
        /// </summary>
        public string PlayerName { get; set; }

        /// <summary>
        /// Quests ativas
        /// </summary>
        public List<ActiveQuest> ActiveQuests { get; set; }

        /// <summary>
        /// Quests completadas
        /// </summary>
        public List<CompletedQuest> CompletedQuests { get; set; }

        /// <summary>
        /// Quests falhadas
        /// </summary>
        public List<FailedQuest> FailedQuests { get; set; }

        /// <summary>
        /// Flags de quest (para condições)
        /// </summary>
        public Dictionary<string, bool> QuestFlags { get; set; }

        /// <summary>
        /// Estatísticas
        /// </summary>
        public QuestStatistics Statistics { get; set; }

        #endregion

        #region Constructor

        /// <summary>
        /// Construtor vazio — usado apenas para leitura/descarte de dados
        /// </summary>
        public PlayerQuestData()
        {
            ActiveQuests = new List<ActiveQuest>();
            CompletedQuests = new List<CompletedQuest>();
            FailedQuests = new List<FailedQuest>();
            QuestFlags = new Dictionary<string, bool>();
            Statistics = new QuestStatistics();
        }



        public PlayerQuestData(PlayerMobile player) : this()
        {
            PlayerSerial = player.Serial;
            PlayerName = player.Name;
        }

        #endregion

        #region Active Quests

        /// <summary>
        /// Inicia uma nova quest
        /// </summary>
        public ActiveQuest StartQuest(QuestDefinition definition)
        {
            var active = new ActiveQuest
            {
                QuestId = definition.Id,
                QuestName = definition.Name,
                Status = QuestStatus.Active,
                AcceptedAt = DateTime.UtcNow,
                Progress = definition.CreateProgressList()
            };

            if (definition.HasTimeLimit)
            {
                active.ExpiresAt = DateTime.UtcNow.AddMinutes(definition.TimeLimitMinutes);
            }

            ActiveQuests.Add(active);
            return active;
        }

        /// <summary>
        /// Obtém quest ativa por ID
        /// </summary>
        public ActiveQuest GetActiveQuest(string questId)
        {
            return ActiveQuests.Find(q => q.QuestId == questId);
        }

        /// <summary>
        /// Verifica se tem quest ativa
        /// </summary>
        public bool HasActiveQuest(string questId)
        {
            return GetActiveQuest(questId) != null;
        }

        /// <summary>
        /// Obtém todas quests ativas
        /// </summary>
        public List<ActiveQuest> GetAllActiveQuests()
        {
            return new List<ActiveQuest>(ActiveQuests);
        }

        /// <summary>
        /// Obtém quests prontas para entregar
        /// </summary>
        public List<ActiveQuest> GetReadyToTurnIn()
        {
            // Não confia apenas no Status gravado: saves antigos (ou quests cujos
            // objetivos foram completados antes desta correção) podem estar com
            // Status == Active mesmo tendo tudo concluído. Nesse caso a quest
            // nunca apareceria como entregável e ficaria travada para sempre.
            return ActiveQuests.FindAll(q =>
            {
                if (q == null)
                    return false;

                if (q.Status == QuestStatus.ReadyToTurnIn)
                    return true;

                if (q.Status != QuestStatus.Active)
                    return false;

                var definition = QuestRepository.Instance.GetQuest(q.QuestId);
                if (definition == null)
                    return false;

                if (!definition.AreObjectivesComplete(q.Progress))
                    return false;

                q.Status = QuestStatus.ReadyToTurnIn;
                return true;
            });
        }

        /// <summary>
        /// Remove quest ativa
        /// </summary>
        public bool RemoveActiveQuest(string questId)
        {
            return ActiveQuests.RemoveAll(q => q.QuestId == questId) > 0;
        }

        #endregion

        #region Completed Quests

        /// <summary>
        /// Marca quest como completa
        /// </summary>
        public CompletedQuest CompleteQuest(string questId, QuestDefinition definition)
        {
            var active = GetActiveQuest(questId);
            if (active == null) return null;

            ActiveQuests.Remove(active);

            // Correção crítica: antes era criado um NOVO CompletedQuest a cada
            // entrega com CompletionCount = GetCompletionCount(questId) + 1, e
            // GetCompletionCount somava todas as entradas. O resultado crescia
            // 1, 3, 7, 15... e estourava MaxCompletions já na segunda
            // repetição, bloqueando para sempre qualquer quest repetível.
            var existing = CompletedQuests.Find(q =>
                string.Equals(q.QuestId, questId, StringComparison.OrdinalIgnoreCase));

            CompletedQuest completed;

            if (existing != null)
            {
                completed = existing;
                completed.QuestName = active.QuestName;
                completed.CompletedAt = DateTime.UtcNow;
                completed.CompletionCount++;
                completed.CanRepeat = definition.Repeatable;
            }
            else
            {
                completed = new CompletedQuest
                {
                    QuestId = questId,
                    QuestName = active.QuestName,
                    CompletedAt = DateTime.UtcNow,
                    CompletionCount = 1,
                    CanRepeat = definition.Repeatable
                };

                CompletedQuests.Add(completed);
            }

            if (definition.Repeatable && definition.CooldownHours > 0)
                completed.CooldownEndsAt = DateTime.UtcNow.AddHours(definition.CooldownHours);
            else
                completed.CooldownEndsAt = null;

            // Falhas anteriores não devem continuar valendo depois que a quest
            // é finalmente concluída.
            FailedQuests.RemoveAll(q => q != null &&
                string.Equals(q.QuestId, questId, StringComparison.OrdinalIgnoreCase));

            Statistics.TotalQuestsCompleted++;

            return completed;
        }

        /// <summary>
        /// Verifica se completou quest
        /// </summary>
        public bool HasCompletedQuest(string questId)
        {
            if (string.IsNullOrEmpty(questId))
                return false;

            return CompletedQuests.Exists(q => q != null &&
                string.Equals(q.QuestId, questId, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Obtém número de vezes que completou
        /// </summary>
        public int GetCompletionCount(string questId)
        {
            if (string.IsNullOrEmpty(questId))
                return 0;

            int count = 0;

            foreach (var c in CompletedQuests)
            {
                if (c != null && string.Equals(c.QuestId, questId, StringComparison.OrdinalIgnoreCase))
                    count += Math.Max(1, c.CompletionCount);
            }

            return count;
        }

        /// <summary>
        /// Verifica se quest está em cooldown
        /// </summary>
        public bool IsOnCooldown(string questId, out TimeSpan remaining)
        {
            remaining = TimeSpan.Zero;
            var completed = CompletedQuests.FindLast(q => q.QuestId == questId);

            if (completed == null || !completed.CooldownEndsAt.HasValue)
                return false;

            if (DateTime.UtcNow < completed.CooldownEndsAt.Value)
            {
                remaining = completed.CooldownEndsAt.Value - DateTime.UtcNow;
                return true;
            }

            return false;
        }

        #endregion

        #region Failed Quests

        /// <summary>
        /// Marca quest como falhada
        /// </summary>
        public void FailQuest(string questId, QuestFailReason reason)
        {
            var active = GetActiveQuest(questId);
            if (active == null) return;

            ActiveQuests.Remove(active);

            FailedQuests.Add(new FailedQuest
            {
                QuestId = questId,
                QuestName = active.QuestName,
                FailedAt = DateTime.UtcNow,
                Reason = reason
            });

            Statistics.TotalQuestsFailed++;
        }

        /// <summary>
        /// Abandona quest
        /// </summary>
        public void AbandonQuest(string questId)
        {
            FailQuest(questId, QuestFailReason.Abandoned);
        }

        #endregion

        #region Quest Flags

        /// <summary>
        /// Define flag
        /// </summary>
        public void SetFlag(string key, bool value)
        {
            QuestFlags[key] = value;
        }

        /// <summary>
        /// Obtém flag
        /// </summary>
        public bool GetFlag(string key, bool defaultValue = false)
        {
            return QuestFlags.TryGetValue(key, out bool value) ? value : defaultValue;
        }

        /// <summary>
        /// Remove flag
        /// </summary>
        public void RemoveFlag(string key)
        {
            QuestFlags.Remove(key);
        }

        #endregion

        #region Quest Status

        /// <summary>
        /// Obtém status de uma quest para o jogador
        /// </summary>
        public QuestStatus GetQuestStatus(string questId)
        {
            // Verifica se está ativa
            var active = GetActiveQuest(questId);
            if (active != null)
                return active.Status;

            // Verifica se está completa
            var completed = CompletedQuests.FindLast(q => q.QuestId == questId);
            if (completed != null)
            {
                // Verifica cooldown
                if (completed.CanRepeat)
                {
                    if (completed.CooldownEndsAt.HasValue && DateTime.UtcNow < completed.CooldownEndsAt.Value)
                        return QuestStatus.OnCooldown;

                    return QuestStatus.Available;
                }

                return QuestStatus.Completed;
            }

            // Não tem histórico = disponível (se pré-requisitos ok)
            return QuestStatus.Available;
        }

        /// <summary>
        /// Obtém tag de status formatada
        /// </summary>
        public string GetStatusTag(string questId)
        {
            var status = GetQuestStatus(questId);

            switch (status)
            {
                case QuestStatus.Active:
                    return "[EM ANDAMENTO]";
                case QuestStatus.ReadyToTurnIn:
                    return "[PRONTA]";
                case QuestStatus.Completed:
                    return "[COMPLETA]";
                case QuestStatus.OnCooldown:
                    if (IsOnCooldown(questId, out TimeSpan remaining))
                        return $"[COOLDOWN {FormatTimeSpan(remaining)}]";
                    return "";
                default:
                    return "";
            }
        }

        private string FormatTimeSpan(TimeSpan ts)
        {
            if (ts.TotalHours >= 1)
                return $"{(int)ts.TotalHours}h";
            if (ts.TotalMinutes >= 1)
                return $"{(int)ts.TotalMinutes}m";
            return $"{(int)ts.TotalSeconds}s";
        }

        #endregion

        #region Serialization

        /// <summary>
        /// Serializa dados
        /// </summary>
        public void Serialize(GenericWriter writer)
        {
            writer.Write(1); // Versão

            // Active Quests
            writer.Write(ActiveQuests.Count);
            foreach (var quest in ActiveQuests)
            {
                quest.Serialize(writer);
            }

            // Completed Quests
            writer.Write(CompletedQuests.Count);
            foreach (var quest in CompletedQuests)
            {
                quest.Serialize(writer);
            }

            // Failed Quests
            writer.Write(FailedQuests.Count);
            foreach (var quest in FailedQuests)
            {
                quest.Serialize(writer);
            }

            // Flags
            writer.Write(QuestFlags.Count);
            foreach (var kvp in QuestFlags)
            {
                writer.Write(kvp.Key);
                writer.Write(kvp.Value);
            }

            // Statistics
            Statistics.Serialize(writer);
        }

        /// <summary>
        /// Deserializa dados
        /// </summary>
        public void Deserialize(GenericReader reader)
        {
            int version = reader.ReadInt();

            // Active Quests
            int activeCount = reader.ReadInt();
            for (int i = 0; i < activeCount; i++)
            {
                var quest = new ActiveQuest();
                quest.Deserialize(reader);
                ActiveQuests.Add(quest);
            }

            // Completed Quests
            int completedCount = reader.ReadInt();
            for (int i = 0; i < completedCount; i++)
            {
                var quest = new CompletedQuest();
                quest.Deserialize(reader);
                CompletedQuests.Add(quest);
            }

            // Failed Quests
            int failedCount = reader.ReadInt();
            for (int i = 0; i < failedCount; i++)
            {
                var quest = new FailedQuest();
                quest.Deserialize(reader);
                FailedQuests.Add(quest);
            }

            // Flags
            int flagCount = reader.ReadInt();
            for (int i = 0; i < flagCount; i++)
            {
                string key = reader.ReadString();
                bool value = reader.ReadBool();
                QuestFlags[key] = value;
            }

            // Statistics
            Statistics.Deserialize(reader);
        }

        #endregion
    }

    #region Active Quest

    /// <summary>
    /// Quest ativa do jogador
    /// </summary>
    public class ActiveQuest
    {
        public string QuestId { get; set; }
        public string QuestName { get; set; }
        public QuestStatus Status { get; set; }
        public DateTime AcceptedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public string GiverNpcId { get; set; }
        public List<ObjectiveProgress> Progress { get; set; }
        public DateTime LastUpdate { get; set; }

        public ActiveQuest()
        {
            Progress = new List<ObjectiveProgress>();
            Status = QuestStatus.Active;
            LastUpdate = DateTime.UtcNow;
        }

        /// <summary>
        /// Verifica se está expirada
        /// </summary>
        public bool IsExpired => ExpiresAt.HasValue && DateTime.UtcNow > ExpiresAt.Value;

        /// <summary>
        /// Atualiza progresso de objetivo
        /// </summary>
        public bool UpdateProgress(int objectiveIndex, int amount = 1)
        {
            if (objectiveIndex < 0 || objectiveIndex >= Progress.Count)
                return false;

            var obj = Progress[objectiveIndex];
            obj.AddProgress(amount);
            LastUpdate = DateTime.UtcNow;

            // Verifica se todos objetivos estão completos
            CheckCompletion();

            return obj.IsComplete;
        }

        /// <summary>
        /// Verifica se quest está pronta para ser entregue.
        /// Objetivos opcionais (IsOptional) NAO bloqueiam a conclusão.
        /// </summary>
        public void CheckCompletion()
        {
            bool allComplete = true;

            foreach (var obj in Progress)
            {
                if (obj.IsOptional)
                    continue;

                if (!obj.IsComplete)
                {
                    allComplete = false;
                    break;
                }
            }

            if (allComplete)
            {
                Status = QuestStatus.ReadyToTurnIn;
            }
            else if (Status == QuestStatus.ReadyToTurnIn)
            {
                // Regrediu (ex.: item de coleta foi consumido/perdido antes da entrega)
                Status = QuestStatus.Active;
            }
        }

        public void Serialize(GenericWriter writer)
        {
            writer.Write(2); // Versão

            writer.Write(QuestId ?? "");
            writer.Write(QuestName ?? "");
            writer.Write((int)Status);
            writer.Write(AcceptedAt);
            writer.Write(ExpiresAt.HasValue);
            if (ExpiresAt.HasValue)
                writer.Write(ExpiresAt.Value);
            writer.Write(GiverNpcId ?? "");
            writer.Write(LastUpdate);

            // Progress
            writer.Write(Progress.Count);
            foreach (var p in Progress)
            {
                writer.Write(p.Index);
                writer.Write((int)p.Type);
                writer.Write(p.Current);
                writer.Write(p.Required);
                writer.Write(p.IsComplete);
                writer.Write(p.Description ?? "");

                // version 2
                writer.Write(p.IsOptional);
                writer.Write(p.IsHidden);
            }
        }

        public void Deserialize(GenericReader reader)
        {
            int version = reader.ReadInt();

            QuestId = reader.ReadString();
            QuestName = reader.ReadString();
            Status = (QuestStatus)reader.ReadInt();
            AcceptedAt = reader.ReadDateTime();

            if (reader.ReadBool())
                ExpiresAt = reader.ReadDateTime();

            GiverNpcId = reader.ReadString();
            LastUpdate = reader.ReadDateTime();

            // Progress
            int count = reader.ReadInt();
            Progress = new List<ObjectiveProgress>();
            for (int i = 0; i < count; i++)
            {
                var progress = new ObjectiveProgress
                {
                    Index = reader.ReadInt(),
                    Type = (ObjectiveType)reader.ReadInt(),
                    Current = reader.ReadInt(),
                    Required = reader.ReadInt(),
                    IsComplete = reader.ReadBool(),
                    Description = reader.ReadString()
                };

                if (version >= 2)
                {
                    progress.IsOptional = reader.ReadBool();
                    progress.IsHidden = reader.ReadBool();
                }

                Progress.Add(progress);
            }
        }
    }

    #endregion

    #region Completed Quest

    /// <summary>
    /// Quest completada
    /// </summary>
    public class CompletedQuest
    {
        public string QuestId { get; set; }
        public string QuestName { get; set; }
        public DateTime CompletedAt { get; set; }
        public int CompletionCount { get; set; }
        public bool CanRepeat { get; set; }
        public DateTime? CooldownEndsAt { get; set; }

        public void Serialize(GenericWriter writer)
        {
            writer.Write(1); // Versão

            writer.Write(QuestId ?? "");
            writer.Write(QuestName ?? "");
            writer.Write(CompletedAt);
            writer.Write(CompletionCount);
            writer.Write(CanRepeat);
            writer.Write(CooldownEndsAt.HasValue);
            if (CooldownEndsAt.HasValue)
                writer.Write(CooldownEndsAt.Value);
        }

        public void Deserialize(GenericReader reader)
        {
            int version = reader.ReadInt();

            QuestId = reader.ReadString();
            QuestName = reader.ReadString();
            CompletedAt = reader.ReadDateTime();
            CompletionCount = reader.ReadInt();
            CanRepeat = reader.ReadBool();

            if (reader.ReadBool())
                CooldownEndsAt = reader.ReadDateTime();
        }
    }

    #endregion

    #region Failed Quest

    /// <summary>
    /// Quest falhada
    /// </summary>
    public class FailedQuest
    {
        public string QuestId { get; set; }
        public string QuestName { get; set; }
        public DateTime FailedAt { get; set; }
        public QuestFailReason Reason { get; set; }

        public void Serialize(GenericWriter writer)
        {
            writer.Write(1); // Versão

            writer.Write(QuestId ?? "");
            writer.Write(QuestName ?? "");
            writer.Write(FailedAt);
            writer.Write((int)Reason);
        }

        public void Deserialize(GenericReader reader)
        {
            int version = reader.ReadInt();

            QuestId = reader.ReadString();
            QuestName = reader.ReadString();
            FailedAt = reader.ReadDateTime();
            Reason = (QuestFailReason)reader.ReadInt();
        }
    }

    #endregion

    #region Quest Statistics

    /// <summary>
    /// Estatísticas do jogador
    /// </summary>
    public class QuestStatistics
    {
        public int TotalQuestsCompleted { get; set; }
        public int TotalQuestsFailed { get; set; }
        public int TotalGoldEarned { get; set; }
        public int TotalMobsKilled { get; set; }
        public int TotalItemsCollected { get; set; }

        public void Serialize(GenericWriter writer)
        {
            writer.Write(1); // Versão

            writer.Write(TotalQuestsCompleted);
            writer.Write(TotalQuestsFailed);
            writer.Write(TotalGoldEarned);
            writer.Write(TotalMobsKilled);
            writer.Write(TotalItemsCollected);
        }

        public void Deserialize(GenericReader reader)
        {
            int version = reader.ReadInt();

            TotalQuestsCompleted = reader.ReadInt();
            TotalQuestsFailed = reader.ReadInt();
            TotalGoldEarned = reader.ReadInt();
            TotalMobsKilled = reader.ReadInt();
            TotalItemsCollected = reader.ReadInt();
        }
    }

    #endregion
}