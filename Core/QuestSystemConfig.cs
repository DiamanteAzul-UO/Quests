/*****************************************************
 * QuestSystemConfig.cs
 * Configuração Principal do Sistema de Quests
 * Etapa 1 - Estrutura Base
 * 
 * Versão: 1.0.0
 * Compatível com: ServUO
 *****************************************************/

using System;
using System.IO;
using System.Xml;
using Server;

namespace Server.Custom.QuestSystem
{
    /// <summary>
    /// Configuração principal do sistema de quests
    /// Carregada na inicialização do servidor
    /// </summary>
    public class QuestSystemConfig
    {
        #region Singleton

        private static QuestSystemConfig _instance;
        private static readonly object _lock = new object();

        /// <summary>
        /// Instância única da configuração
        /// </summary>
        public static QuestSystemConfig Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new QuestSystemConfig();
                            _instance.Load();
                        }
                    }
                }
                return _instance;
            }
        }

        #endregion

        #region System Properties

        /// <summary>Sistema habilitado</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Modo debug (mensagens detalhadas)</summary>
        public bool DebugMode { get; set; } = false;

        /// <summary>Versão do sistema</summary>
        public string Version => QuestConstants.Version;

        /// <summary>Data da última atualização</summary>
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        #endregion

        #region Limits

        /// <summary>Máximo de quests ativas por jogador</summary>
        public int MaxActiveQuestsPerPlayer { get; set; } = QuestConstants.DefaultMaxActiveQuests;

        /// <summary>Máximo de quests no histórico de completadas</summary>
        public int MaxCompletedQuestsHistory { get; set; } = QuestConstants.DefaultMaxCompletedHistory;

        /// <summary>Distância máxima para interagir com NPC</summary>
        public int MaxInteractionRange { get; set; } = QuestConstants.DefaultMaxInteractionRange;

        /// <summary>Máximo de conversas simultâneas por NPC</summary>
        public int MaxSimultaneousConversations { get; set; } = 1;

        #endregion

        #region Timing

        /// <summary>Timeout de conversa em segundos</summary>
        public int ConversationTimeoutSeconds { get; set; } = QuestConstants.DefaultConversationTimeout;

        /// <summary>Timeout de input em segundos</summary>
        public int InputTimeoutSeconds { get; set; } = QuestConstants.DefaultInputTimeout;

        /// <summary>Intervalo de auto-save em minutos</summary>
        public int AutoSaveIntervalMinutes { get; set; } = QuestConstants.DefaultAutoSaveInterval;

        #endregion

        #region Persistence

        /// <summary>Arquivo de quests</summary>
        public string QuestsFilePath => Path.Combine(QuestConstants.BasePath, QuestConstants.QuestsFile);

        /// <summary>Arquivo de dados dos jogadores</summary>
        public string PlayerDataFilePath => Path.Combine(QuestConstants.BasePath, QuestConstants.PlayerDataFile);

        /// <summary>Arquivo de configuração</summary>
        public string ConfigFilePath => Path.Combine(QuestConstants.BasePath, QuestConstants.ConfigFile);

        /// <summary>Formato de persistência</summary>
        public PersistenceFormat PersistenceFormat { get; set; } = PersistenceFormat.XML;

        /// <summary>Backup habilitado</summary>
        public bool BackupEnabled { get; set; } = true;

        /// <summary>Intervalo de backup em horas</summary>
        public int BackupIntervalHours { get; set; } = 24;

        /// <summary>Máximo de backups mantidos</summary>
        public int MaxBackups { get; set; } = QuestConstants.DefaultMaxBackups;

        #endregion

        #region Notifications

        /// <summary>Mostrar notificações de progresso</summary>
        public bool ShowProgressNotifications { get; set; } = true;

        /// <summary>Tocar sons de notificação</summary>
        public bool PlayNotificationSounds { get; set; } = true;

        /// <summary>Mostrar efeitos visuais</summary>
        public bool ShowVisualEffects { get; set; } = true;

        #endregion

        #region Quest Log

        /// <summary>Quest Log habilitado</summary>
        public bool QuestLogEnabled { get; set; } = true;

        /// <summary>Comando do Quest Log</summary>
        public string QuestLogCommand { get; set; } = "questlog";

        /// <summary>Mostrar quests completadas no log</summary>
        public bool ShowCompletedInLog { get; set; } = true;

        /// <summary>Mostrar quests falhadas no log</summary>
        public bool ShowFailedInLog { get; set; } = false;

        #endregion

        #region Constructor

        /// <summary>
        /// Construtor privado (Singleton)
        /// </summary>
        private QuestSystemConfig()
        {
            EnsureDirectoryExists();
        }

        #endregion

        #region Directory Management

        /// <summary>
        /// Garante que os diretórios existem
        /// </summary>
        private void EnsureDirectoryExists()
        {
            try
            {
                if (!Directory.Exists(QuestConstants.BasePath))
                {
                    Directory.CreateDirectory(QuestConstants.BasePath);
                    LogDebug("Diretório criado: " + QuestConstants.BasePath);
                }

                if (!Directory.Exists(QuestConstants.BackupPath))
                {
                    Directory.CreateDirectory(QuestConstants.BackupPath);
                    LogDebug("Diretório de backup criado: " + QuestConstants.BackupPath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[QuestSystem] Erro ao criar diretórios: " + ex.Message);
            }
        }

        #endregion

        #region Load/Save

        /// <summary>
        /// Carrega configuração do arquivo XML
        /// </summary>
        public void Load()
        {
            string filePath = ConfigFilePath;

            if (!File.Exists(filePath))
            {
                LogInfo("Arquivo de configuração não encontrado. Usando padrões.");
                Save(); // Cria arquivo com valores padrão
                return;
            }

            try
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(filePath);

                XmlNode root = doc.SelectSingleNode("QuestSystemConfig");
                if (root == null)
                {
                    LogWarning("Formato de configuração inválido. Usando padrões.");
                    return;
                }

                // System
                Enabled = GetBoolValue(root, "Enabled", true);
                DebugMode = GetBoolValue(root, "DebugMode", false);

                // Limits
                MaxActiveQuestsPerPlayer = GetIntValue(root, "MaxActiveQuestsPerPlayer", 
                    QuestConstants.DefaultMaxActiveQuests);
                MaxCompletedQuestsHistory = GetIntValue(root, "MaxCompletedQuestsHistory", 
                    QuestConstants.DefaultMaxCompletedHistory);
                MaxInteractionRange = GetIntValue(root, "MaxInteractionRange", 
                    QuestConstants.DefaultMaxInteractionRange);
                MaxSimultaneousConversations = GetIntValue(root, "MaxSimultaneousConversations", 1);

                // Timing
                ConversationTimeoutSeconds = GetIntValue(root, "ConversationTimeoutSeconds", 
                    QuestConstants.DefaultConversationTimeout);
                InputTimeoutSeconds = GetIntValue(root, "InputTimeoutSeconds", 
                    QuestConstants.DefaultInputTimeout);
                AutoSaveIntervalMinutes = GetIntValue(root, "AutoSaveIntervalMinutes", 
                    QuestConstants.DefaultAutoSaveInterval);

                // Persistence
                string formatStr = GetStringValue(root, "PersistenceFormat", "XML");
                if (Enum.TryParse(formatStr, out PersistenceFormat format))
                    PersistenceFormat = format;

                BackupEnabled = GetBoolValue(root, "BackupEnabled", true);
                BackupIntervalHours = GetIntValue(root, "BackupIntervalHours", 24);
                MaxBackups = GetIntValue(root, "MaxBackups", QuestConstants.DefaultMaxBackups);

                // Notifications
                ShowProgressNotifications = GetBoolValue(root, "ShowProgressNotifications", true);
                PlayNotificationSounds = GetBoolValue(root, "PlayNotificationSounds", true);
                ShowVisualEffects = GetBoolValue(root, "ShowVisualEffects", true);

                // Quest Log
                QuestLogEnabled = GetBoolValue(root, "QuestLogEnabled", true);
                QuestLogCommand = GetStringValue(root, "QuestLogCommand", "questlog");
                ShowCompletedInLog = GetBoolValue(root, "ShowCompletedInLog", true);
                ShowFailedInLog = GetBoolValue(root, "ShowFailedInLog", false);

                LogInfo("Configuração carregada com sucesso.");
            }
            catch (Exception ex)
            {
                LogError("Erro ao carregar configuração: " + ex.Message);
            }
        }

        /// <summary>
        /// Salva configuração no arquivo XML
        /// </summary>
        public void Save()
        {
            try
            {
                EnsureDirectoryExists();

                XmlDocument doc = new XmlDocument();
                
                // Declaração XML
                XmlDeclaration declaration = doc.CreateXmlDeclaration("1.0", "UTF-8", null);
                doc.AppendChild(declaration);

                // Comentário
                XmlComment comment = doc.CreateComment(
                    " Configuração do Sistema de Quests - ServUO \n" +
                    $" Versão: {Version} \n" +
                    $" Última atualização: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC ");
                doc.AppendChild(comment);

                // Root
                XmlElement root = doc.CreateElement("QuestSystemConfig");
                doc.AppendChild(root);

                // System
                AddElement(doc, root, "Enabled", Enabled.ToString().ToLower());
                AddElement(doc, root, "DebugMode", DebugMode.ToString().ToLower());
                AddElement(doc, root, "Version", Version);

                // Limits
                XmlElement limits = doc.CreateElement("Limits");
                root.AppendChild(limits);
                AddElement(doc, limits, "MaxActiveQuestsPerPlayer", MaxActiveQuestsPerPlayer.ToString());
                AddElement(doc, limits, "MaxCompletedQuestsHistory", MaxCompletedQuestsHistory.ToString());
                AddElement(doc, limits, "MaxInteractionRange", MaxInteractionRange.ToString());
                AddElement(doc, limits, "MaxSimultaneousConversations", MaxSimultaneousConversations.ToString());

                // Timing
                XmlElement timing = doc.CreateElement("Timing");
                root.AppendChild(timing);
                AddElement(doc, timing, "ConversationTimeoutSeconds", ConversationTimeoutSeconds.ToString());
                AddElement(doc, timing, "InputTimeoutSeconds", InputTimeoutSeconds.ToString());
                AddElement(doc, timing, "AutoSaveIntervalMinutes", AutoSaveIntervalMinutes.ToString());

                // Persistence
                XmlElement persistence = doc.CreateElement("Persistence");
                root.AppendChild(persistence);
                AddElement(doc, persistence, "PersistenceFormat", PersistenceFormat.ToString());
                AddElement(doc, persistence, "BackupEnabled", BackupEnabled.ToString().ToLower());
                AddElement(doc, persistence, "BackupIntervalHours", BackupIntervalHours.ToString());
                AddElement(doc, persistence, "MaxBackups", MaxBackups.ToString());

                // Notifications
                XmlElement notifications = doc.CreateElement("Notifications");
                root.AppendChild(notifications);
                AddElement(doc, notifications, "ShowProgressNotifications", 
                    ShowProgressNotifications.ToString().ToLower());
                AddElement(doc, notifications, "PlayNotificationSounds", 
                    PlayNotificationSounds.ToString().ToLower());
                AddElement(doc, notifications, "ShowVisualEffects", 
                    ShowVisualEffects.ToString().ToLower());

                // Quest Log
                XmlElement questLog = doc.CreateElement("QuestLog");
                root.AppendChild(questLog);
                AddElement(doc, questLog, "Enabled", QuestLogEnabled.ToString().ToLower());
                AddElement(doc, questLog, "Command", QuestLogCommand);
                AddElement(doc, questLog, "ShowCompleted", ShowCompletedInLog.ToString().ToLower());
                AddElement(doc, questLog, "ShowFailed", ShowFailedInLog.ToString().ToLower());

                // Salvar
                doc.Save(ConfigFilePath);
                LastUpdated = DateTime.UtcNow;

                LogInfo("Configuração salva com sucesso.");
            }
            catch (Exception ex)
            {
                LogError("Erro ao salvar configuração: " + ex.Message);
            }
        }

        #endregion

        #region XML Helpers

        private void AddElement(XmlDocument doc, XmlElement parent, string name, string value)
        {
            XmlElement element = doc.CreateElement(name);
            element.InnerText = value;
            parent.AppendChild(element);
        }

        private string GetStringValue(XmlNode parent, string name, string defaultValue)
        {
            XmlNode node = parent.SelectSingleNode(name) ?? parent.SelectSingleNode($"*/{name}");
            return node?.InnerText ?? defaultValue;
        }

        private int GetIntValue(XmlNode parent, string name, int defaultValue)
        {
            string value = GetStringValue(parent, name, null);
            return int.TryParse(value, out int result) ? result : defaultValue;
        }

        private bool GetBoolValue(XmlNode parent, string name, bool defaultValue)
        {
            string value = GetStringValue(parent, name, null);
            return bool.TryParse(value, out bool result) ? result : defaultValue;
        }

        #endregion

        #region Logging

        /// <summary>
        /// Log de informação
        /// </summary>
        public void LogInfo(string message)
        {
            Console.WriteLine($"[QuestSystem] {message}");
        }

        /// <summary>
        /// Log de aviso
        /// </summary>
        public void LogWarning(string message)
        {
            Console.WriteLine($"[QuestSystem] AVISO: {message}");
        }

        /// <summary>
        /// Log de erro
        /// </summary>
        public void LogError(string message)
        {
            Console.WriteLine($"[QuestSystem] ERRO: {message}");
        }

        /// <summary>
        /// Log de debug (apenas se DebugMode estiver ativo)
        /// </summary>
        public void LogDebug(string message)
        {
            if (DebugMode)
                Console.WriteLine($"[QuestSystem] DEBUG: {message}");
        }

        #endregion

        #region Reload

        /// <summary>
        /// Recarrega a configuração do arquivo
        /// </summary>
        public void Reload()
        {
            Load();
            LogInfo("Configuração recarregada.");
        }

        #endregion

        #region Validation

        /// <summary>
        /// Valida as configurações
        /// </summary>
        public bool Validate()
        {
            bool valid = true;

            if (MaxActiveQuestsPerPlayer < 1)
            {
                LogWarning("MaxActiveQuestsPerPlayer deve ser >= 1. Corrigido para 1.");
                MaxActiveQuestsPerPlayer = 1;
                valid = false;
            }

            if (MaxInteractionRange < 1)
            {
                LogWarning("MaxInteractionRange deve ser >= 1. Corrigido para 1.");
                MaxInteractionRange = 1;
                valid = false;
            }

            if (ConversationTimeoutSeconds < 10)
            {
                LogWarning("ConversationTimeoutSeconds deve ser >= 10. Corrigido para 10.");
                ConversationTimeoutSeconds = 10;
                valid = false;
            }

            if (AutoSaveIntervalMinutes < 1)
            {
                LogWarning("AutoSaveIntervalMinutes deve ser >= 1. Corrigido para 1.");
                AutoSaveIntervalMinutes = 1;
                valid = false;
            }

            return valid;
        }

        #endregion

        #region Info

        /// <summary>
        /// Retorna informações do sistema como string
        /// </summary>
        public string GetSystemInfo()
        {
            return $@"
═══════════════════════════════════════
  {QuestConstants.SystemName}
  Versão: {Version}
═══════════════════════════════════════
  Status: {(Enabled ? "ATIVADO" : "DESATIVADO")}
  Debug: {(DebugMode ? "ATIVADO" : "DESATIVADO")}
  
  Limites:
    - Max Quests Ativas: {MaxActiveQuestsPerPlayer}
    - Range de Interação: {MaxInteractionRange} tiles
    - Timeout Conversa: {ConversationTimeoutSeconds}s
    
  Persistência:
    - Formato: {PersistenceFormat}
    - Auto-Save: {AutoSaveIntervalMinutes} min
    - Backup: {(BackupEnabled ? "ATIVADO" : "DESATIVADO")}
═══════════════════════════════════════";
        }

        #endregion
    }
}