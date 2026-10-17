/*****************************************************
 * QuestEnums.cs
 * Enumeradores do Sistema de Quests
 * Etapa 1 - Estrutura Base
 * 
 * Versão: 1.0.0
 * Compatível com: ServUO
 *****************************************************/

using System;

namespace Server.Custom.QuestSystem
{
    #region Quest Status

    /// <summary>
    /// Status atual de uma quest para um jogador
    /// </summary>
    public enum QuestStatus
    {
        /// <summary>Quest disponível para aceitar</summary>
        Available = 0,

        /// <summary>Quest em andamento</summary>
        Active = 1,

        /// <summary>Todos objetivos completos, pronta para entregar</summary>
        ReadyToTurnIn = 2,

        /// <summary>Quest completada com sucesso</summary>
        Completed = 3,

        /// <summary>Quest falhou</summary>
        Failed = 4,

        /// <summary>Quest em cooldown (repetível)</summary>
        OnCooldown = 5,

        /// <summary>Quest bloqueada (pré-requisitos não atendidos)</summary>
        Locked = 6,

        /// <summary>Quest abandonada pelo jogador</summary>
        Abandoned = 7
    }

    #endregion

    #region Quest Category

    /// <summary>
    /// Categoria da quest
    /// </summary>
    public enum QuestCategory
    {
        /// <summary>Missão principal da história</summary>
        Main = 0,

        /// <summary>Missão secundária</summary>
        Side = 1,

        /// <summary>Missão diária (reseta todo dia)</summary>
        Daily = 2,

        /// <summary>Missão semanal</summary>
        Weekly = 3,

        /// <summary>Missão de evento especial</summary>
        Event = 4,

        /// <summary>Missão de guilda/facção</summary>
        Guild = 5,

        /// <summary>Missão tutorial</summary>
        Tutorial = 6,

        /// <summary>Missão de profissão/craft</summary>
        Profession = 7
    }

    #endregion

    #region Quest Difficulty

    /// <summary>
    /// Dificuldade da quest
    /// </summary>
    public enum QuestDifficulty
    {
        /// <summary>Muito fácil - Iniciantes</summary>
        Trivial = 0,

        /// <summary>Fácil</summary>
        Easy = 1,

        /// <summary>Normal</summary>
        Normal = 2,

        /// <summary>Difícil</summary>
        Hard = 3,

        /// <summary>Muito difícil</summary>
        VeryHard = 4,

        /// <summary>Épico - Requer grupo</summary>
        Epic = 5,

        /// <summary>Lendário - Extremamente difícil</summary>
        Legendary = 6
    }

    #endregion

    #region Objective Types

    /// <summary>
    /// Tipos de objetivos disponíveis
    /// </summary>
    public enum ObjectiveType
    {
        /// <summary>Matar criaturas</summary>
        Kill = 0,

        /// <summary>Coletar itens</summary>
        Collect = 1,

        /// <summary>Entregar item para NPC</summary>
        Deliver = 2,

        /// <summary>Escoltar NPC até local</summary>
        Escort = 3,

        /// <summary>Defender local de ondas de inimigos</summary>
        Defend = 4,

        /// <summary>Destruir objeto/estrutura</summary>
        Destroy = 5,

        /// <summary>Explorar/visitar local</summary>
        Explore = 6,

        /// <summary>Ganhar skill até certo valor</summary>
        SkillGain = 7,

        /// <summary>Craftar item</summary>
        Craft = 8,

        /// <summary>Usar skill X vezes</summary>
        UseSkill = 9,

        /// <summary>Interagir com objeto/NPC</summary>
        Interact = 10,

        /// <summary>Falar com NPC</summary>
        TalkTo = 11,

        /// <summary>Usar item específico</summary>
        UseItem = 12,

        /// <summary>Sobreviver por tempo</summary>
        Survive = 13
    }

    #endregion

    #region Conversation State

    /// <summary>
    /// Estado atual da conversa com NPC
    /// </summary>
    public enum ConversationStep
    {
        /// <summary>Sem conversa ativa</summary>
        None = 0,

        /// <summary>Saudação inicial</summary>
        Greet = 1,

        /// <summary>Mostrando biografia do NPC</summary>
        ShowBio = 2,

        /// <summary>Mostrando info do local</summary>
        ShowLocation = 3,

        /// <summary>Listando quests disponíveis</summary>
        ShowQuests = 4,

        /// <summary>Mostrando detalhes de uma quest</summary>
        QuestDetails = 5,

        /// <summary>Aguardando confirmação para aceitar</summary>
        ConfirmAccept = 6,

        /// <summary>Processando entrega de quest</summary>
        TurnIn = 7,

        /// <summary>Mostrando progresso da quest</summary>
        ShowProgress = 8,

        /// <summary>Finalizando conversa</summary>
        Farewell = 9
    }

    #endregion

    #region Fail Reasons

    /// <summary>
    /// Motivos de falha de uma quest
    /// </summary>
    public enum QuestFailReason
    {
        /// <summary>Não falhou</summary>
        None = 0,

        /// <summary>Tempo expirou</summary>
        TimeExpired = 1,

        /// <summary>NPC de escolta morreu</summary>
        EscortDied = 2,

        /// <summary>Jogador abandonou</summary>
        Abandoned = 3,

        /// <summary>Jogador morreu</summary>
        PlayerDied = 4,

        /// <summary>Alvo de defesa destruído</summary>
        DefenseTargetDestroyed = 5,

        /// <summary>Item de quest perdido</summary>
        QuestItemLost = 6,

        /// <summary>Pré-requisitos deixaram de ser atendidos</summary>
        PrerequisitesLost = 7
    }

    #endregion

    #region On Fail Actions

    /// <summary>
    /// Ações quando quest falha
    /// </summary>
    public enum OnFailAction
    {
        /// <summary>Falha a quest completamente</summary>
        FailQuest = 0,

        /// <summary>Reseta a quest para o início</summary>
        ResetQuest = 1,

        /// <summary>Reseta apenas o objetivo atual</summary>
        ResetObjective = 2,

        /// <summary>Não faz nada (ignora)</summary>
        Nothing = 3,

        /// <summary>Respawna NPC/Item</summary>
        Respawn = 4
    }

    #endregion

    #region Reward Types

    /// <summary>
    /// Tipos de recompensa
    /// </summary>
    [Flags]
    public enum RewardType
    {
        None = 0,
        Gold = 1 << 0,
        Items = 1 << 1,
        Fame = 1 << 2,
        Karma = 1 << 3,
        SkillGain = 1 << 4,
        StatGain = 1 << 5,
        Title = 1 << 6,
        QuestUnlock = 1 << 7,
        AreaUnlock = 1 << 8,
        Recipe = 1 << 9,
        Experience = 1 << 10
    }

    #endregion

    #region Access Level

    /// <summary>
    /// Nível de acesso para comandos
    /// </summary>
    public enum QuestAccessLevel
    {
        Player = 0,
        Counselor = 1,
        GameMaster = 2,
        Seer = 3,
        Administrator = 4,
        Developer = 5,
        Owner = 6
    }

    #endregion

    #region Persistence Format

    /// <summary>
    /// Formato de persistência dos dados
    /// </summary>
    public enum PersistenceFormat
    {
        /// <summary>Serialização binária do ServUO</summary>
        Binary = 0,

        /// <summary>Arquivo XML</summary>
        XML = 1,

        /// <summary>Arquivo JSON</summary>
        JSON = 2
    }

    #endregion

    #region Notification Type

    /// <summary>
    /// Tipo de notificação para o jogador
    /// </summary>
    public enum NotificationType
    {
        /// <summary>Quest aceita</summary>
        QuestAccepted = 0,

        /// <summary>Progresso do objetivo</summary>
        ObjectiveProgress = 1,

        /// <summary>Objetivo completo</summary>
        ObjectiveComplete = 2,

        /// <summary>Quest pronta para entregar</summary>
        QuestReadyToTurn = 3,

        /// <summary>Quest completada</summary>
        QuestCompleted = 4,

        /// <summary>Quest falhou</summary>
        QuestFailed = 5,

        /// <summary>Aviso de tempo</summary>
        TimeLimitWarning = 6,

        /// <summary>Quest disponível</summary>
        QuestAvailable = 7
    }

    #endregion
}