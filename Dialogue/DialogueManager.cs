/*****************************************************
 * DialogueManager.cs
 * Gerenciador de Diálogos
 * 
 * Versão: 2.0.0 - Sem Prompt (usa Say)
 * Compatível com: ServUO
 *****************************************************/

using System;
using System.Collections.Generic;
using Server;
using Server.Mobiles;
using Server.Custom.QuestSystem.NPCs;
using Server.Custom.QuestSystem.Conversation;

namespace Server.Custom.QuestSystem.Dialogue
{
    /// <summary>
    /// Gerencia a execução de diálogos
    /// </summary>
    public class DialogueManager
    {
        #region Singleton

        private static DialogueManager _instance;
        private static readonly object _lock = new object();

        public static DialogueManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new DialogueManager();
                        }
                    }
                }
                return _instance;
            }
        }

        #endregion

        #region Constants

        private const int DEFAULT_LINE_DELAY = 2000;
        private const int NPC_SPEECH_HUE = 0x151;
        private const int PLAYER_MSG_HUE = 0x151;

        #endregion

        #region Fields

        private Dictionary<string, DialogueData> _dialogueCache;

        #endregion

        #region Constructor

        private DialogueManager()
        {
            _dialogueCache = new Dictionary<string, DialogueData>();
        }

        #endregion

        #region Dialogue Execution

        /// <summary>
        /// Executa um nó de diálogo (NPC fala via Say)
        /// </summary>
        public void ExecuteNode(
            Mobile npc, 
            PlayerMobile player, 
            ConversationState state,
            DialogueNode node,
            Action onComplete = null)
        {
            if (npc == null || player == null || node == null)
            {
                onComplete?.Invoke();
                return;
            }

            var context = new Dictionary<string, string>
            {
                { "playerName", player.Name ?? "aventureiro" },
                { "npcName", npc.Name ?? "NPC" }
            };

            if (!string.IsNullOrEmpty(state?.SelectedQuestId))
            {
                context["questId"] = state.SelectedQuestId;

                // Antes questName recebia o proprio ID, entao uma fala escrita
                // como "Voce aceitou {questName}" renderizava
                // "Voce aceitou quest_nh_comida" — o identificador interno
                // vazando para o jogador em vez do titulo.
                string questName = state.SelectedQuestId;

                try
                {
                    var definition = Server.Custom.QuestSystem.Quests.QuestRepository
                        .Instance.GetQuest(state.SelectedQuestId);

                    if (definition != null && !string.IsNullOrWhiteSpace(definition.Name))
                        questName = definition.Name;
                }
                catch
                {
                    // repositório ainda não carregado: mantém o ID como fallback
                }

                context["questName"] = questName;
            }

            ExecuteLines(npc, player, node.Lines, context, onComplete);
        }

        /// <summary>
        /// Executa lista de linhas de fala
        /// </summary>
        private void ExecuteLines(
            Mobile npc,
            PlayerMobile player,
            List<DialogueLine> lines,
            Dictionary<string, string> context,
            Action onComplete)
        {
            if (lines == null || lines.Count == 0)
            {
                onComplete?.Invoke();
                return;
            }

            int currentDelay = 0;

            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                int delay = currentDelay + line.DelayBefore;

                var capturedLine = line;
                var capturedNpc = npc;
                var capturedPlayer = player;
                var capturedContext = context;

                Timer.DelayCall(TimeSpan.FromMilliseconds(delay), delegate
                {
                    if (capturedNpc == null || capturedNpc.Deleted || capturedPlayer == null || capturedPlayer.Deleted)
                        return;

                    if (!capturedLine.CheckCondition(capturedPlayer, null))
                        return;

                    string text = capturedLine.Format(capturedNpc, capturedPlayer, capturedContext);

                    if (string.IsNullOrEmpty(text))
                        return;

                    if (capturedLine.IsPrivate)
                    {
                        capturedPlayer.SendMessage(capturedLine.Hue > 0 ? capturedLine.Hue : PLAYER_MSG_HUE, text);
                    }
                    else
                    {
                        capturedNpc.Say(capturedLine.Hue > 0 ? capturedLine.Hue : NPC_SPEECH_HUE, text);
                    }

                    if (capturedLine.Sound > 0)
                    {
                        capturedPlayer.PlaySound(capturedLine.Sound);
                    }
                });

                currentDelay = delay + line.DelayAfter;
            }

            if (onComplete != null)
            {
                int finalDelay = currentDelay + 500;
                Timer.DelayCall(TimeSpan.FromMilliseconds(finalDelay), delegate
                {
                    onComplete();
                });
            }
        }

        #endregion

        #region Quick Dialogue Methods

        /// <summary>
        /// NPC fala uma única linha
        /// </summary>
        public void NPCSay(Mobile npc, string text, int hue = 0x151)
        {
            if (npc == null || npc.Deleted || string.IsNullOrEmpty(text))
                return;

            npc.Say(hue > 0 ? hue : NPC_SPEECH_HUE, text);
        }

        /// <summary>
        /// NPC fala múltiplas linhas
        /// </summary>
        public void NPCSayLines(Mobile npc, string[] lines, int delayBetween = 2000, Action onComplete = null)
        {
            if (npc == null || lines == null || lines.Length == 0)
            {
                onComplete?.Invoke();
                return;
            }

            int delay = 0;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                int currentDelay = delay;

                var capturedNpc = npc;
                var capturedLine = line;

                Timer.DelayCall(TimeSpan.FromMilliseconds(currentDelay), delegate
                {
                    if (capturedNpc != null && !capturedNpc.Deleted)
                    {
                        capturedNpc.Say(NPC_SPEECH_HUE, capturedLine);
                    }
                });

                delay += delayBetween;
            }

            if (onComplete != null)
            {
                int finalDelay = delay + 500;
                Timer.DelayCall(TimeSpan.FromMilliseconds(finalDelay), delegate
                {
                    onComplete();
                });
            }
        }

        #endregion

        #region Quest Dialogue

        /// <summary>
        /// Executa diálogo de oferta de quest
        /// </summary>
        public void ShowQuestOffer(
            Mobile npc,
            PlayerMobile player,
            ConversationState state,
            QuestDialogue questDialogue)
        {
            if (questDialogue?.Offer == null)
            {
                var defaultNode = new DialogueNode();
                defaultNode.AddLine("A missao e muito importante.");
                defaultNode.AddLine("Aceita o desafio?");

                ExecuteNode(npc, player, state, defaultNode);
                return;
            }

            ExecuteNode(npc, player, state, questDialogue.Offer);
        }

        /// <summary>
        /// Executa diálogo de aceitação
        /// </summary>
        public void ShowQuestAccept(
            Mobile npc,
            PlayerMobile player,
            ConversationState state,
            QuestDialogue questDialogue)
        {
            DialogueNode node = questDialogue?.Accept;

            if (node == null)
            {
                node = new DialogueNode();
                node.AddLine("Excelente!");
                node.AddLine("Boa sorte em sua jornada!");
            }

            var capturedPlayer = player;
            var capturedQuestDialogue = questDialogue;
            var capturedState = state;

            ExecuteNode(npc, player, state, node, delegate
            {
                capturedPlayer?.PlaySound(0x5B6);
                
                if (capturedPlayer != null)
                {
                    string questName = capturedQuestDialogue?.QuestName ?? capturedState?.SelectedQuestId ?? "Missao";
                    capturedPlayer.SendMessage(0x40, "");
                    capturedPlayer.SendMessage(0x40, $"*** Missao aceita: {questName} ***");
                }
            });
        }

        /// <summary>
        /// Executa diálogo de recusa
        /// </summary>
        public void ShowQuestDecline(
            Mobile npc,
            PlayerMobile player,
            ConversationState state,
            QuestDialogue questDialogue)
        {
            DialogueNode node = questDialogue?.Decline;

            if (node == null)
            {
                node = new DialogueNode();
                node.AddLine("Tudo bem, volte quando mudar de ideia.");
            }

            ExecuteNode(npc, player, state, node);
        }

        /// <summary>
        /// Executa diálogo de progresso
        /// </summary>
        public void ShowQuestProgress(
            Mobile npc,
            PlayerMobile player,
            ConversationState state,
            QuestDialogue questDialogue,
            string progressInfo)
        {
            DialogueNode node = questDialogue?.Progress;

            if (node == null)
            {
                node = new DialogueNode();
                node.AddLine("Como vai a missao?");
                if (!string.IsNullOrEmpty(progressInfo))
                    node.AddLine(progressInfo);
                node.AddLine("Continue assim!");
            }

            ExecuteNode(npc, player, state, node);
        }

        /// <summary>
        /// Executa diálogo de conclusão
        /// </summary>
        public void ShowQuestComplete(
            Mobile npc,
            PlayerMobile player,
            ConversationState state,
            QuestDialogue questDialogue)
        {
            DialogueNode node = questDialogue?.Complete;

            if (node == null)
            {
                node = new DialogueNode();
                node.AddLine("Muito bem!");
                node.AddLine("Voce completou a missao!");
                node.AddLine("Aqui esta sua recompensa!");
            }

            var capturedPlayer = player;
            var capturedQuestDialogue = questDialogue;
            var capturedState = state;

            ExecuteNode(npc, player, state, node, delegate
            {
                capturedPlayer?.PlaySound(0x5B5);

                if (capturedPlayer != null)
                {
                    string questName = capturedQuestDialogue?.QuestName ?? capturedState?.SelectedQuestId ?? "Missao";
                    capturedPlayer.SendMessage(0x40, "");
                    capturedPlayer.SendMessage(0x40, $"*** Missao completa: {questName} ***");
                }
            });
        }

        #endregion

        #region Dialogue Cache

        public DialogueData GetDialogueData(string id)
        {
            if (_dialogueCache.TryGetValue(id, out DialogueData data))
                return data;
            return null;
        }

        public void CacheDialogueData(string id, DialogueData data)
        {
            _dialogueCache[id] = data;
        }

        public void RemoveFromCache(string id)
        {
            _dialogueCache.Remove(id);
        }

        public void ClearCache()
        {
            _dialogueCache.Clear();
        }

        #endregion
    }
}