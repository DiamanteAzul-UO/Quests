/*****************************************************
 * QuestConstants.cs
 * Constantes do Sistema de Quests
 * Etapa 1 - Estrutura Base
 * 
 * Versão: 1.0.0
 * Compatível com: ServUO
 *****************************************************/

using System;

namespace Server.Custom.QuestSystem
{
    /// <summary>
    /// Constantes globais do sistema de quests
    /// </summary>
    public static class QuestConstants
    {
        #region System Info

        /// <summary>Nome do sistema</summary>
        public const string SystemName = "ServUO Quest System";

        /// <summary>Versão atual</summary>
        public const string Version = "1.0.0";

        /// <summary>Autor</summary>
        public const string Author = "GM";

        #endregion

        #region File Paths

        /// <summary>Pasta base do sistema</summary>
        public const string BasePath = "Saves/QuestSystem/";

        /// <summary>Arquivo de quests</summary>
        public const string QuestsFile = "Quests.xml";

        /// <summary>Arquivo de dados dos jogadores</summary>
        public const string PlayerDataFile = "PlayerData.xml";

        /// <summary>Arquivo de configuração</summary>
        public const string ConfigFile = "Config.xml";

        /// <summary>Pasta de backups</summary>
        public const string BackupPath = "Saves/QuestSystem/Backups/";

        #endregion

        #region Default Limits

        /// <summary>Máximo de quests ativas por jogador</summary>
        public const int DefaultMaxActiveQuests = 10;

        /// <summary>Máximo de quests no histórico</summary>
        public const int DefaultMaxCompletedHistory = 100;

        /// <summary>Distância máxima de interação com NPC (tiles)</summary>
        public const int DefaultMaxInteractionRange = 3;

        /// <summary>Tempo limite de conversa (segundos)</summary>
        public const int DefaultConversationTimeout = 60;

        /// <summary>Tempo limite para input do jogador (segundos)</summary>
        public const int DefaultInputTimeout = 30;

        /// <summary>Intervalo de auto-save (minutos)</summary>
        public const int DefaultAutoSaveInterval = 15;

        /// <summary>Máximo de backups mantidos</summary>
        public const int DefaultMaxBackups = 7;

        #endregion

        #region Message Hues (Colors)

        /// <summary>Cor padrão de mensagem do NPC (azul claro)</summary>
        public const int HueNpcSpeech = 0x59; // 89 decimal - azul seguro

        /// <summary>Cor de sucesso (verde)</summary>
        public const int HueSuccess = 0x40; // 64 decimal - verde

        /// <summary>Cor de erro/falha (vermelho)</summary>
        public const int HueError = 0x26; // 38 decimal - vermelho

        /// <summary>Cor de aviso (amarelo/laranja)</summary>
        public const int HueWarning = 0x35; // 53 decimal - amarelo

        /// <summary>Cor de progresso (azul)</summary>
        public const int HueProgress = 0x3B; // 59 decimal - azul

        /// <summary>Cor de informação (branco)</summary>
        public const int HueInfo = 0x480; // 1152 decimal - branco brilhante

        /// <summary>Cor de sistema</summary>
        public const int HueSystem = 0x25; // 37 decimal

        #endregion
        #region Sound Effects

        /// <summary>Som de quest aceita</summary>
        public const int SoundQuestAccepted = 0x5B6; // 1462

        /// <summary>Som de quest completa</summary>
        public const int SoundQuestCompleted = 0x5B5; // 1461

        /// <summary>Som de objetivo completo</summary>
        public const int SoundObjectiveComplete = 0x5B4; // 1460

        /// <summary>Som de quest falhou</summary>
        public const int SoundQuestFailed = 0x5B3; // 1459

        /// <summary>Som de aviso</summary>
        public const int SoundWarning = 0x5B2; // 1458

        /// <summary>Som de clique/seleção</summary>
        public const int SoundClick = 0x5AA; // 1450

        #endregion

        #region Trigger Words

        /// <summary>Palavras que iniciam conversa</summary>
        public static readonly string[] DefaultTriggerWords =
        {
            "oi", "olá", "hello", "hi", "hey",
            "missão", "missões", "quest", "quests",
            "ajuda", "help"
        };

        /// <summary>Palavras de despedida</summary>
        public static readonly string[] DefaultFarewellWords =
        {
            "tchau", "até mais", "adeus", "bye",
            "goodbye", "farewell", "sair", "exit"
        };

        /// <summary>Palavras de aceitação</summary>
        public static readonly string[] DefaultAcceptWords =
        {
            "aceitar", "aceito", "sim", "yes",
            "accept", "ok", "confirmar", "confirm"
        };

        /// <summary>Palavras de recusa</summary>
        public static readonly string[] DefaultDeclineWords =
        {
            "recusar", "recuso", "não", "no",
            "decline", "cancelar", "cancel"
        };

        /// <summary>Palavras para voltar</summary>
        public static readonly string[] DefaultBackWords =
        {
            "voltar", "back", "menu", "retornar", "return"
        };

        #endregion

        #region Default Messages

        /// <summary>Mensagem quando NPC está ocupado</summary>
        public const string MsgNpcBusy = "Estou ocupado agora, aguarde um momento.";

        /// <summary>Mensagem quando jogador está longe</summary>
        public const string MsgOutOfRange = "Aproxime-se mais para conversarmos.";

        /// <summary>Mensagem de timeout</summary>
        public const string MsgTimeout = "Parece que você se afastou. Até mais!";

        /// <summary>Mensagem de input inválido</summary>
        public const string MsgInvalidInput = "Não entendi. Digite uma das opções disponíveis.";

        /// <summary>Mensagem de saudação padrão</summary>
        public const string MsgDefaultGreet = "Olá, {playerName}! Em que posso ajudar?";

        /// <summary>Mensagem de despedida padrão</summary>
        public const string MsgDefaultFarewell = "Até mais, {playerName}! Boa sorte em suas aventuras.";

        /// <summary>Mensagem sem quests</summary>
        public const string MsgNoQuests = "Não tenho missões para você no momento.";

        #endregion

        #region Placeholders

        /// <summary>Placeholder para nome do jogador</summary>
        public const string PlaceholderPlayerName = "{playerName}";

        /// <summary>Placeholder para nome do NPC</summary>
        public const string PlaceholderNpcName = "{npcName}";

        /// <summary>Placeholder para nome da quest</summary>
        public const string PlaceholderQuestName = "{questName}";

        /// <summary>Placeholder para progresso atual</summary>
        public const string PlaceholderCurrent = "{current}";

        /// <summary>Placeholder para valor requerido</summary>
        public const string PlaceholderRequired = "{required}";

        #endregion

        #region Gump IDs

        /// <summary>ID do Gump de Quest Log</summary>
        public const int GumpIdQuestLog = 0x1000;

        /// <summary>ID do Gump de Criação de Quest (GM)</summary>
        public const int GumpIdQuestCreator = 0x1001;

        /// <summary>ID do Gump de Conversa com NPC</summary>
        public const int GumpIdConversation = 0x1002;

        /// <summary>ID do Gump de Detalhes da Quest</summary>
        public const int GumpIdQuestDetails = 0x1003;

        #endregion
    }
}