/*****************************************************
 * DialogueLine.cs
 * Representa uma linha de diálogo
 * Etapa 3 - DialogueFlow
 * 
 * Versão: 1.0.0
 * Compatível com: ServUO
 *****************************************************/

using System;
using System.Collections.Generic;
using Server;
using Server.Mobiles;

namespace Server.Custom.QuestSystem.Dialogue
{
    /// <summary>
    /// Uma linha de diálogo que o NPC fala
    /// </summary>
    public class DialogueLine
    {
        #region Properties

        /// <summary>
        /// Texto da fala
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// Delay antes de falar esta linha (ms)
        /// </summary>
        public int DelayBefore { get; set; }

        /// <summary>
        /// Delay após falar esta linha (ms)
        /// </summary>
        public int DelayAfter { get; set; }

        /// <summary>
        /// Cor da fala (hue)
        /// </summary>
        public int Hue { get; set; }

        /// <summary>
        /// Som a tocar junto com a fala
        /// </summary>
        public int Sound { get; set; }

        /// <summary>
        /// Ação especial a executar
        /// </summary>
        public string Action { get; set; }

        /// <summary>
        /// Condição para mostrar esta linha
        /// </summary>
        public string Condition { get; set; }

        /// <summary>
        /// É uma mensagem privada (SendMessage) ao invés de Say
        /// </summary>
        public bool IsPrivate { get; set; }

        #endregion

        #region Constructors

        public DialogueLine()
        {
            DelayBefore = 0;
            DelayAfter = 2000; // 2 segundos padrão
            Hue = 0x151;   //Cor Vermelha
            Sound = -1;
            IsPrivate = false;
        }

        public DialogueLine(string text) : this()
        {
            Text = text;
        }

        public DialogueLine(string text, int delayAfter) : this()
        {
            Text = text;
            DelayAfter = delayAfter;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Formata o texto substituindo placeholders
        /// </summary>
        public string Format(Mobile npc, PlayerMobile player, Dictionary<string, string> extraData = null)
        {
            if (string.IsNullOrEmpty(Text))
                return "";

            string result = Text;

            // Substitui placeholders padrão
            if (player != null)
            {
                result = result.Replace("{playerName}", player.Name ?? "aventureiro");
                result = result.Replace("{player.Name}", player.Name ?? "aventureiro");
            }

            if (npc != null)
            {
                result = result.Replace("{npcName}", npc.Name ?? "NPC");
                result = result.Replace("{npc.Name}", npc.Name ?? "NPC");
            }

            // Substitui dados extras
            if (extraData != null)
            {
                foreach (var kvp in extraData)
                {
                    result = result.Replace($"{{{kvp.Key}}}", kvp.Value);
                }
            }

            return result;
        }

        /// <summary>
        /// Verifica se a condição é atendida
        /// </summary>
        public bool CheckCondition(PlayerMobile player, Dictionary<string, object> context)
        {
            if (string.IsNullOrEmpty(Condition))
                return true;

            // TODO: Implementar parser de condições
            // Por enquanto, sempre retorna true
            return true;
        }

        #endregion

        #region Static Helpers

        /// <summary>
        /// Cria linha simples
        /// </summary>
        public static DialogueLine Simple(string text)
        {
            return new DialogueLine(text);
        }

        /// <summary>
        /// Cria linha com delay customizado
        /// </summary>
        public static DialogueLine WithDelay(string text, int delayAfterMs)
        {
            return new DialogueLine(text, delayAfterMs);
        }

        /// <summary>
        /// Cria linha privada (SendMessage)
        /// </summary>
        public static DialogueLine Private(string text, int hue = 0x151)
        {
            return new DialogueLine
            {
                Text = text,
                IsPrivate = true,
                Hue = hue,
                DelayAfter = 0
            };
        }

        #endregion
    }
}