/*****************************************************
 * PlayerMobileExtensions.cs
 * Extensões para PlayerMobile (dados de quest)
 * Etapa 5 - QuestManager
 * 
 * Versão: 1.0.0
 * Compatível com: ServUO
 *****************************************************/

using System;
using System.Collections.Generic;
using Server;
using Server.Mobiles;
using Server.Custom.QuestSystem.Quests;

namespace Server.Custom.QuestSystem
{
    /// <summary>
    /// Extensões para facilitar acesso a dados de quest no PlayerMobile
    /// </summary>
    public static class PlayerMobileQuestExtensions
    {
        #region Quest Data Access

        /// <summary>
        /// Obtém dados de quest do jogador
        /// </summary>
        public static PlayerQuestData GetQuestData(this PlayerMobile player)
        {
            return QuestManager.Instance.GetPlayerData(player);
        }

        /// <summary>
        /// Verifica se tem quest ativa
        /// </summary>
        public static bool HasActiveQuest(this PlayerMobile player, string questId)
        {
            var data = player.GetQuestData();
            return data?.HasActiveQuest(questId) ?? false;
        }

        /// <summary>
        /// Verifica se completou quest
        /// </summary>
        public static bool HasCompletedQuest(this PlayerMobile player, string questId)
        {
            var data = player.GetQuestData();
            return data?.HasCompletedQuest(questId) ?? false;
        }

        /// <summary>
        /// Obtém status de quest
        /// </summary>
        public static QuestStatus GetQuestStatus(this PlayerMobile player, string questId)
        {
            return QuestManager.Instance.GetQuestStatus(player, questId);
        }

        /// <summary>
        /// Obtém lista de quests ativas
        /// </summary>
        public static List<ActiveQuest> GetActiveQuests(this PlayerMobile player)
        {
            var data = player.GetQuestData();
            return data?.GetAllActiveQuests() ?? new List<ActiveQuest>();
        }

        /// <summary>
        /// Obtém quests prontas para entregar
        /// </summary>
        public static List<ActiveQuest> GetQuestsReadyToTurnIn(this PlayerMobile player)
        {
            var data = player.GetQuestData();
            return data?.GetReadyToTurnIn() ?? new List<ActiveQuest>();
        }

        /// <summary>
        /// Obtém quests prontas para entregar a um NPC específico.
        ///
        /// Correção crítica: a comparação antiga era
        /// <c>definition.TurnInNpcId.Equals(npcId)</c> onde <c>npcId</c> era
        /// <c>Serial.ToString()</c> ("12345") e <c>TurnInNpcId</c> era algo como
        /// "npc_capita_maelis". Quando o NPC que entrega não era o mesmo que
        /// ofereceu a quest, ela NUNCA aparecia como entregável — a quest ficava
        /// concluída no progresso, mas impossível de finalizar.
        ///
        /// Agora a identificação usa os mesmos critérios dos objetivos TalkTo e
        /// Deliver: id declarado no NPC (IQuestNpcIdentity), serial decimal,
        /// serial hexadecimal e nome de exibição.
        /// </summary>
        public static List<ActiveQuest> GetQuestsReadyToTurnIn(this PlayerMobile player, string npcId)
        {
            Mobile npc = null;

            uint serialValue;
            if (!string.IsNullOrEmpty(npcId) && uint.TryParse(npcId, out serialValue))
                npc = World.FindMobile(new Serial(serialValue));

            return FilterTurnIn(player, npc, npcId);
        }

        /// <summary>Sobrecarga que recebe o NPC diretamente (forma recomendada).</summary>
        public static List<ActiveQuest> GetQuestsReadyToTurnIn(this PlayerMobile player, Mobile npc)
        {
            return FilterTurnIn(player, npc, npc != null ? npc.Serial.ToString() : null);
        }

        private static List<ActiveQuest> FilterTurnIn(PlayerMobile player, Mobile npc, string npcId)
        {
            var readyQuests = player.GetQuestsReadyToTurnIn();
            var result = new List<ActiveQuest>();

            List<string> identifiers = QuestManager.GetNpcIdentifiers(npc, npcId);

            foreach (var active in readyQuests)
            {
                var definition = QuestRepository.Instance.GetQuest(active.QuestId);
                if (definition == null)
                    continue;

                if (IsTurnInNpc(definition, npc, identifiers))
                    result.Add(active);
            }

            return result;
        }

        /// <summary>
        /// Diz se o NPC é o responsável por receber a entrega da quest.
        /// Quando a definição não declara NPC de entrega, aceita-se o NPC que
        /// ofereceu a quest e, na ausência de qualquer declaração, qualquer NPC
        /// de quest — melhor entregar do que travar a quest para sempre.
        /// </summary>
        private static bool IsTurnInNpc(QuestDefinition definition, Mobile npc, List<string> identifiers)
        {
            if (definition == null)
                return false;

            bool declaresTurnIn = !string.IsNullOrWhiteSpace(definition.TurnInNpcId) ||
                                  !string.IsNullOrWhiteSpace(definition.TurnInNpcName);

            if (!declaresTurnIn)
            {
                declaresTurnIn = !string.IsNullOrWhiteSpace(definition.GiverNpcId) ||
                                 !string.IsNullOrWhiteSpace(definition.GiverNpcName);

                if (!declaresTurnIn)
                    return true;

                return QuestManager.MatchesNpc(definition.GiverNpcId, definition.GiverNpcName, npc, identifiers);
            }

            return QuestManager.MatchesNpc(definition.TurnInNpcId, definition.TurnInNpcName, npc, identifiers);
        }

        #endregion

        #region Quest Actions

        /// <summary>
        /// Aceita uma quest
        /// </summary>
        public static bool AcceptQuest(this PlayerMobile player, string questId, out string message)
        {
            return QuestManager.Instance.StartQuest(player, questId, out message);
        }

        /// <summary>
        /// Entrega uma quest
        /// </summary>
        public static bool TurnInQuest(this PlayerMobile player, string questId, out string message)
        {
            return QuestManager.Instance.CompleteQuest(player, questId, out message);
        }

        /// <summary>
        /// Abandona uma quest
        /// </summary>
        public static bool AbandonQuest(this PlayerMobile player, string questId, out string message)
        {
            return QuestManager.Instance.AbandonQuest(player, questId, out message);
        }

        #endregion

        #region Quest Progress

        /// <summary>
        /// Obtém progresso de uma quest
        /// </summary>
        public static List<ObjectiveProgress> GetQuestProgress(this PlayerMobile player, string questId)
        {
            var data = player.GetQuestData();
            var active = data?.GetActiveQuest(questId);
            return active?.Progress ?? new List<ObjectiveProgress>();
        }

        /// <summary>
        /// Obtém texto de progresso formatado
        /// </summary>
        public static List<string> GetQuestProgressText(this PlayerMobile player, string questId)
        {
            var progress = player.GetQuestProgress(questId);
            var result = new List<string>();

            foreach (var p in progress)
            {
                string status = p.IsComplete ? "[OK]" : $"[{p.Current}/{p.Required}]";
                result.Add($"{p.Description} {status}");
            }

            return result;
        }

        #endregion

        #region Quest Flags

        /// <summary>
        /// Define flag de quest
        /// </summary>
        public static void SetQuestFlag(this PlayerMobile player, string key, bool value)
        {
            var data = player.GetQuestData();
            data?.SetFlag(key, value);
        }

        /// <summary>
        /// Obtém flag de quest
        /// </summary>
        public static bool GetQuestFlag(this PlayerMobile player, string key, bool defaultValue = false)
        {
            var data = player.GetQuestData();
            return data?.GetFlag(key, defaultValue) ?? defaultValue;
        }

        #endregion

        #region Statistics

        /// <summary>
        /// Obtém total de quests completadas
        /// </summary>
        public static int GetTotalQuestsCompleted(this PlayerMobile player)
        {
            var data = player.GetQuestData();
            return data?.Statistics?.TotalQuestsCompleted ?? 0;
        }

        #endregion
    }
}