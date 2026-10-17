/*****************************************************
 * DialogueOption.cs
 * Representa uma opção de resposta do jogador
 * Etapa 3 - DialogueFlow
 * 
 * Versão: 1.0.0
 * Compatível com: ServUO
 *****************************************************/

using System;
using Server.Mobiles;

namespace Server.Custom.QuestSystem.Dialogue
{
    /// <summary>
    /// Uma opção de resposta que o jogador pode escolher
    /// </summary>
    public class DialogueOption
    {
        #region Properties

        /// <summary>
        /// Número/tecla da opção
        /// </summary>
        public int Key { get; set; }

        /// <summary>
        /// Texto da opção exibido ao jogador
        /// </summary>
        public string Label { get; set; }

        /// <summary>
        /// Ação a executar quando selecionada
        /// </summary>
        public DialogueAction Action { get; set; }

        /// <summary>
        /// ID da quest associada (se aplicável)
        /// </summary>
        public string QuestId { get; set; }

        /// <summary>
        /// Condição para mostrar esta opção
        /// </summary>
        public string Condition { get; set; }

        /// <summary>
        /// Opção está habilitada
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Texto alternativo se desabilitada
        /// </summary>
        public string DisabledLabel { get; set; }

        /// <summary>
        /// Cor do texto
        /// </summary>
        public int Hue { get; set; }

        #endregion

        #region Constructors

        public DialogueOption()
        {
            Enabled = true;
            Hue = 0x151; // Branco
        }

        public DialogueOption(int key, string label, DialogueAction action) : this()
        {
            Key = key;
            Label = label;
            Action = action;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Verifica se a opção deve ser mostrada
        /// </summary>
        public bool ShouldShow(PlayerMobile player)
        {
            if (string.IsNullOrEmpty(Condition))
                return true;

            // TODO: Implementar parser de condições
            return true;
        }

        /// <summary>
        /// Verifica se a opção está habilitada para o jogador
        /// </summary>
        public bool IsEnabled(PlayerMobile player)
        {
            return Enabled && ShouldShow(player);
        }

        /// <summary>
        /// Retorna o texto formatado para exibição
        /// </summary>
        public string GetDisplayText()
        {
            if (!Enabled && !string.IsNullOrEmpty(DisabledLabel))
                return $"{Key} - {DisabledLabel}";

            return $"{Key} - {Label}";
        }

        #endregion

        #region Static Helpers

        /// <summary>
        /// Cria opção simples
        /// </summary>
        public static DialogueOption Create(int key, string label, DialogueAction action)
        {
            return new DialogueOption(key, label, action);
        }

        /// <summary>
        /// Cria opção de voltar
        /// </summary>
        public static DialogueOption Back(int key = 0)
        {
            return new DialogueOption(key, "Voltar", DialogueAction.Back);
        }

        /// <summary>
        /// Cria opção de sair
        /// </summary>
        public static DialogueOption Exit(int key = 5)
        {
            return new DialogueOption(key, "Tchau", DialogueAction.Exit);
        }

        #endregion
    }

    /// <summary>
    /// Ações possíveis de uma opção de diálogo
    /// </summary>
    public enum DialogueAction
    {
        None,
        ShowBio,
        ShowLocation,
        ShowQuests,
        ShowQuestDetails,
        AcceptQuest,
        DeclineQuest,
        TurnIn,
        Back,
        BackToMain,
        Exit,
        Custom
    }
}