/*****************************************************
 * QuestNotificationManager.cs
 * Gerenciador de Notificações de Quest
 * Etapa 5 - QuestManager
 * 
 * Versão: 1.0.0
 * Compatível com: ServUO
 *****************************************************/

using System;
using Server;
using Server.Mobiles;
using Server.Network;

namespace Server.Custom.QuestSystem.Quests
{
    /// <summary>
    /// Gerencia notificações de quest para jogadores
    /// </summary>
    public class QuestNotificationManager
    {
        #region Singleton

        private static QuestNotificationManager _instance;
        private static readonly object _lock = new object();

        public static QuestNotificationManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new QuestNotificationManager();
                        }
                    }
                }
                return _instance;
            }
        }

        #endregion

        #region Constants

        private const int HUE_SUCCESS = 0x40;    // Verde
        private const int HUE_INFO = 0x480;      // Branco
        private const int HUE_WARNING = 0x35;    // Amarelo
        private const int HUE_ERROR = 0x26;      // Vermelho
        private const int HUE_PROGRESS = 0x3B;   // Azul

        private const int SOUND_QUEST_ACCEPTED = 0x5B6;
        private const int SOUND_QUEST_COMPLETED = 0x5B5;
        private const int SOUND_OBJECTIVE_COMPLETE = 0x5B4;
        private const int SOUND_QUEST_FAILED = 0x5B3;
        private const int SOUND_PROGRESS = 0x5AA;

        #endregion

        #region Quest Notifications

        /// <summary>
        /// Notifica quest aceita
        /// </summary>
        public void NotifyQuestAccepted(PlayerMobile player, QuestDefinition quest)
        {
            if (player == null || quest == null)
                return;

            PlaySound(player, SOUND_QUEST_ACCEPTED);

            player.SendMessage(HUE_SUCCESS, "");
            player.SendMessage(HUE_SUCCESS, "============================================");
            player.SendMessage(HUE_SUCCESS, $"  NOVA MISSAO: {quest.Name}");
            player.SendMessage(HUE_SUCCESS, "============================================");
            player.SendMessage(HUE_INFO, "");
            
            if (!string.IsNullOrEmpty(quest.Description))
            {
                player.SendMessage(HUE_INFO, quest.Description);
                player.SendMessage(HUE_INFO, "");
            }

            player.SendMessage(HUE_INFO, "Objetivos:");
            for (int i = 0; i < quest.Objectives.Count; i++)
            {
                var obj = quest.Objectives[i];
                player.SendMessage(HUE_INFO, $"  - {obj.Description} (0/{obj.Required})");
            }

            player.SendMessage(HUE_INFO, "");
            player.SendMessage(HUE_INFO, $"Recompensas: {quest.Rewards.GetSummary()}");
        }

        /// <summary>
        /// Notifica quest completa
        /// </summary>
        public void NotifyQuestCompleted(PlayerMobile player, QuestDefinition quest)
        {
            if (player == null || quest == null)
                return;

            PlaySound(player, SOUND_QUEST_COMPLETED);

            player.SendMessage(HUE_SUCCESS, "");
            player.SendMessage(HUE_SUCCESS, "********************************************");
            player.SendMessage(HUE_SUCCESS, $"  MISSAO COMPLETA: {quest.Name}");
            player.SendMessage(HUE_SUCCESS, "********************************************");

            if (!string.IsNullOrEmpty(quest.CompletionMessage))
            {
                player.SendMessage(HUE_INFO, "");
                player.SendMessage(HUE_INFO, quest.CompletionMessage);
            }

            player.SendMessage(HUE_INFO, "");
            player.SendMessage(HUE_SUCCESS, "Recompensas recebidas!");
        }

        /// <summary>
        /// Notifica quest falhada
        /// </summary>
        public void NotifyQuestFailed(PlayerMobile player, QuestDefinition quest, QuestFailReason reason)
        {
            if (player == null)
                return;

            PlaySound(player, SOUND_QUEST_FAILED);

            string questName = quest?.Name ?? "Missao";
            string reasonText = GetFailReasonText(reason);

            player.SendMessage(HUE_ERROR, "");
            player.SendMessage(HUE_ERROR, "============================================");
            player.SendMessage(HUE_ERROR, $"  MISSAO FALHOU: {questName}");
            player.SendMessage(HUE_ERROR, $"  Motivo: {reasonText}");
            player.SendMessage(HUE_ERROR, "============================================");
        }

        /// <summary>
        /// Notifica quest pronta para entregar
        /// </summary>
        public void NotifyQuestReadyToTurnIn(PlayerMobile player, QuestDefinition quest)
        {
            if (player == null || quest == null)
                return;

            PlaySound(player, SOUND_OBJECTIVE_COMPLETE);

            player.SendMessage(HUE_SUCCESS, "");
            player.SendMessage(HUE_SUCCESS, "============================================");
            player.SendMessage(HUE_SUCCESS, $"  MISSAO PRONTA: {quest.Name}");
            player.SendMessage(HUE_SUCCESS, "============================================");
            player.SendMessage(HUE_INFO, "");
            
            if (!string.IsNullOrEmpty(quest.TurnInNpcName))
            {
                player.SendMessage(HUE_INFO, $"Retorne a {quest.TurnInNpcName} para receber sua recompensa!");
            }
            else
            {
                player.SendMessage(HUE_INFO, "Retorne ao NPC para receber sua recompensa!");
            }
        }

        /// <summary>
        /// Notifica quest disponível
        /// </summary>
        public void NotifyQuestAvailable(PlayerMobile player, QuestDefinition quest)
        {
            if (player == null || quest == null)
                return;

            player.SendMessage(HUE_INFO, "");
            player.SendMessage(HUE_INFO, $"Nova missao disponivel: {quest.Name}");
            
            if (!string.IsNullOrEmpty(quest.GiverNpcName))
            {
                player.SendMessage(HUE_INFO, $"Fale com {quest.GiverNpcName} para mais detalhes.");
            }
        }

        #endregion

        #region Objective Notifications

        /// <summary>
        /// Notifica progresso de objetivo
        /// </summary>
        public void NotifyObjectiveProgress(PlayerMobile player, QuestDefinition quest, QuestObjective objective, int current, int required)
        {
            if (player == null || objective == null)
                return;

            // Não notifica se já completo
            if (current >= required)
                return;

            // Só notifica a cada 25% ou quando falta pouco
            bool shouldNotify = false;
            float percent = (float)current / required * 100f;
            
            if (current == 1) // Primeiro
                shouldNotify = true;
            else if (percent >= 25 && percent < 26)
                shouldNotify = true;
            else if (percent >= 50 && percent < 51)
                shouldNotify = true;
            else if (percent >= 75 && percent < 76)
                shouldNotify = true;
            else if (required - current <= 2) // Faltam 2 ou menos
                shouldNotify = true;

            if (shouldNotify && QuestSystemConfig.Instance.ShowProgressNotifications)
            {
                PlaySound(player, SOUND_PROGRESS);
                player.SendMessage(HUE_PROGRESS, $"[{quest?.Name}] {objective.Description}: {current}/{required}");
            }
        }

        /// <summary>
        /// Notifica objetivo completo
        /// </summary>
        public void NotifyObjectiveComplete(PlayerMobile player, QuestDefinition quest, QuestObjective objective)
        {
            if (player == null || objective == null)
                return;

            PlaySound(player, SOUND_OBJECTIVE_COMPLETE);

            player.SendMessage(HUE_SUCCESS, "");
            player.SendMessage(HUE_SUCCESS, $"[{quest?.Name}] Objetivo completo: {objective.Description}");
        }

        #endregion

        #region Time Notifications

        /// <summary>
        /// Notifica aviso de tempo
        /// </summary>
        public void NotifyTimeWarning(PlayerMobile player, QuestDefinition quest, int minutesRemaining)
        {
            if (player == null || quest == null)
                return;

            player.SendMessage(HUE_WARNING, "");
            player.SendMessage(HUE_WARNING, $"[{quest.Name}] AVISO: Restam {minutesRemaining} minutos!");
        }

        /// <summary>
        /// Notifica tempo esgotado
        /// </summary>
        public void NotifyTimeExpired(PlayerMobile player, QuestDefinition quest)
        {
            if (player == null || quest == null)
                return;

            player.SendMessage(HUE_ERROR, "");
            player.SendMessage(HUE_ERROR, $"[{quest.Name}] Tempo esgotado!");
        }

        #endregion

        #region Helper Methods

        private void PlaySound(PlayerMobile player, int soundId)
        {
            if (player == null || player.NetState == null)
                return;

            if (QuestSystemConfig.Instance.PlayNotificationSounds)
            {
                player.PlaySound(soundId);
            }
        }

        private string GetFailReasonText(QuestFailReason reason)
        {
            switch (reason)
            {
                case QuestFailReason.TimeExpired:
                    return "Tempo esgotado";
                case QuestFailReason.EscortDied:
                    return "NPC escoltado morreu";
                case QuestFailReason.Abandoned:
                    return "Abandonada";
                case QuestFailReason.PlayerDied:
                    return "Voce morreu";
                case QuestFailReason.DefenseTargetDestroyed:
                    return "Alvo de defesa destruido";
                case QuestFailReason.QuestItemLost:
                    return "Item de quest perdido";
                default:
                    return "Motivo desconhecido";
            }
        }

        #endregion

        #region Broadcast

        /// <summary>
        /// Anuncia quest completa para todos (opcional)
        /// </summary>
        public void BroadcastQuestComplete(PlayerMobile player, QuestDefinition quest)
        {
            // Pode ser usado para anunciar conquistas importantes
            // World.Broadcast(0x35, false, $"{player.Name} completou a missao: {quest.Name}!");
        }

        #endregion
    }
}