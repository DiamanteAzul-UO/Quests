/*****************************************************
 * NPCBehaviorConfig.cs
 * Configuração de comportamento do NPC de Quest
 * Etapa 2 - NPCBehavior
 * 
 * Versão: 1.0.0
 * Compatível com: ServUO
 *****************************************************/

using System;
using System.Collections.Generic;
using Server;

namespace Server.Custom.QuestSystem.Conversation
{
    /// <summary>
    /// Configuração de comportamento de um NPC de quests
    /// Pode ser customizada por NPC individual
    /// </summary>
    public class NPCBehaviorConfig
    {
        #region Interaction Settings

        /// <summary>
        /// Palavras que iniciam conversa
        /// </summary>
        public string[] TriggerWords { get; set; }

        /// <summary>
        /// Palavras de despedida
        /// </summary>
        public string[] FarewellWords { get; set; }

        /// <summary>
        /// Palavras de aceitação
        /// </summary>
        public string[] AcceptWords { get; set; }

        /// <summary>
        /// Palavras de recusa
        /// </summary>
        public string[] DeclineWords { get; set; }

        /// <summary>
        /// Palavras para voltar
        /// </summary>
        public string[] BackWords { get; set; }

        #endregion

        #region Limits

        /// <summary>
        /// Distância máxima para interação (tiles)
        /// </summary>
        public int MaxInteractionRange { get; set; }

        /// <summary>
        /// Máximo de conversas simultâneas
        /// </summary>
        public int MaxSimultaneousConversations { get; set; }

        /// <summary>
        /// Timeout da conversa em segundos
        /// </summary>
        public int ConversationTimeoutSeconds { get; set; }

        /// <summary>
        /// Timeout para input do jogador em segundos
        /// </summary>
        public int InputTimeoutSeconds { get; set; }

        #endregion

        #region Messages

        /// <summary>
        /// Mensagem quando NPC está ocupado
        /// </summary>
        public string OnBusyMessage { get; set; }

        /// <summary>
        /// Mensagem quando jogador está fora de alcance
        /// </summary>
        public string OnOutOfRangeMessage { get; set; }

        /// <summary>
        /// Mensagem de timeout
        /// </summary>
        public string OnTimeoutMessage { get; set; }

        /// <summary>
        /// Mensagem de input inválido
        /// </summary>
        public string OnInvalidInputMessage { get; set; }

        /// <summary>
        /// Mensagem quando jogador se afasta
        /// </summary>
        public string OnPlayerMovedMessage { get; set; }

        #endregion

        #region NPC Info

        /// <summary>
        /// Biografia/descrição do NPC
        /// </summary>
        public string Bio { get; set; }

        /// <summary>
        /// Descrição do local onde NPC está
        /// </summary>
        public string LocationDescription { get; set; }

        /// <summary>
        /// Título do NPC
        /// </summary>
        public string Title { get; set; }

        #endregion

        #region Speech Settings

        /// <summary>
        /// Cor da fala do NPC
        /// </summary>
        public int SpeechHue { get; set; }

        /// <summary>
        /// NPC fala em overhead (true) ou system message (false)
        /// </summary>
        public bool UseOverheadSpeech { get; set; }

        /// <summary>
        /// Delay entre linhas de fala (ms)
        /// </summary>
        public int SpeechDelayMs { get; set; }

        #endregion

        #region Behavior Flags

        /// <summary>
        /// NPC vira para o jogador ao conversar
        /// </summary>
        public bool FacePlayerOnTalk { get; set; }

        /// <summary>
        /// NPC emite som ao iniciar conversa
        /// </summary>
        public bool PlaySoundOnGreet { get; set; }

        /// <summary>
        /// Som ao iniciar conversa
        /// </summary>
        public int GreetSound { get; set; }

        /// <summary>
        /// NPC responde a qualquer fala quando em conversa
        /// </summary>
        public bool RespondToAnySpeech { get; set; }

        /// <summary>
        /// Encerra conversa se jogador se afastar
        /// </summary>
        public bool EndOnPlayerMoved { get; set; }

        #endregion

        #region Constructor

        /// <summary>
        /// Cria configuração com valores padrão
        /// </summary>
        public NPCBehaviorConfig()
        {
            // Interaction
            TriggerWords = QuestConstants.DefaultTriggerWords;
            FarewellWords = QuestConstants.DefaultFarewellWords;
            AcceptWords = QuestConstants.DefaultAcceptWords;
            DeclineWords = QuestConstants.DefaultDeclineWords;
            BackWords = QuestConstants.DefaultBackWords;

            // Limits
            MaxInteractionRange = QuestConstants.DefaultMaxInteractionRange;
            MaxSimultaneousConversations = 1;
            ConversationTimeoutSeconds = QuestConstants.DefaultConversationTimeout;
            InputTimeoutSeconds = QuestConstants.DefaultInputTimeout;

            // Messages
            OnBusyMessage = QuestConstants.MsgNpcBusy;
            OnOutOfRangeMessage = QuestConstants.MsgOutOfRange;
            OnTimeoutMessage = QuestConstants.MsgTimeout;
            OnInvalidInputMessage = QuestConstants.MsgInvalidInput;
            OnPlayerMovedMessage = "Ei! Volte aqui para continuarmos.";

            // NPC Info
            Bio = "Um habitante deste mundo.";
            LocationDescription = "Um lugar comum.";
            Title = null;

            // Speech
            SpeechHue = QuestConstants.HueNpcSpeech;
            UseOverheadSpeech = true;
            SpeechDelayMs = 500;

            // Behavior
            FacePlayerOnTalk = true;
            PlaySoundOnGreet = false;
            GreetSound = 0;
            RespondToAnySpeech = true;
            EndOnPlayerMoved = false;
        }

        #endregion

        #region Factory Methods

        /// <summary>
        /// Cria configuração padrão
        /// </summary>
        public static NPCBehaviorConfig Default => new NPCBehaviorConfig();

        /// <summary>
        /// Cria configuração para NPC de quest importante
        /// </summary>
        public static NPCBehaviorConfig ImportantNPC()
        {
            var config = new NPCBehaviorConfig
            {
                MaxInteractionRange = 5,
                ConversationTimeoutSeconds = 120,
                PlaySoundOnGreet = true,
                GreetSound = 0x5B6,
                SpeechHue = 0x455,
            };
            return config;
        }

        /// <summary>
        /// Cria configuração para NPC de guilda
        /// </summary>
        public static NPCBehaviorConfig GuildNPC()
        {
            var config = new NPCBehaviorConfig
            {
                MaxInteractionRange = 3,
                ConversationTimeoutSeconds = 90,
                FacePlayerOnTalk = true
            };
            return config;
        }

        /// <summary>
        /// Cria configuração para NPC vendor
        /// </summary>
        public static NPCBehaviorConfig VendorNPC()
        {
            var config = new NPCBehaviorConfig
            {
                MaxInteractionRange = 2,
                ConversationTimeoutSeconds = 60,
                EndOnPlayerMoved = true
            };
            return config;
        }

        #endregion

        #region Clone

        /// <summary>
        /// Cria cópia da configuração
        /// </summary>
        public NPCBehaviorConfig Clone()
        {
            return new NPCBehaviorConfig
            {
                TriggerWords = (string[])TriggerWords.Clone(),
                FarewellWords = (string[])FarewellWords.Clone(),
                AcceptWords = (string[])AcceptWords.Clone(),
                DeclineWords = (string[])DeclineWords.Clone(),
                BackWords = (string[])BackWords.Clone(),
                MaxInteractionRange = MaxInteractionRange,
                MaxSimultaneousConversations = MaxSimultaneousConversations,
                ConversationTimeoutSeconds = ConversationTimeoutSeconds,
                InputTimeoutSeconds = InputTimeoutSeconds,
                OnBusyMessage = OnBusyMessage,
                OnOutOfRangeMessage = OnOutOfRangeMessage,
                OnTimeoutMessage = OnTimeoutMessage,
                OnInvalidInputMessage = OnInvalidInputMessage,
                OnPlayerMovedMessage = OnPlayerMovedMessage,
                Bio = Bio,
                LocationDescription = LocationDescription,
                Title = Title,
                SpeechHue = SpeechHue,
                UseOverheadSpeech = UseOverheadSpeech,
                SpeechDelayMs = SpeechDelayMs,
                FacePlayerOnTalk = FacePlayerOnTalk,
                PlaySoundOnGreet = PlaySoundOnGreet,
                GreetSound = GreetSound,
                RespondToAnySpeech = RespondToAnySpeech,
                EndOnPlayerMoved = EndOnPlayerMoved
            };
        }

        #endregion
    }
}