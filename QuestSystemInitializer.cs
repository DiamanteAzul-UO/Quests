using System;
using Server;
using Server.Custom.QuestSystem.Quests;

namespace Server.Custom.QuestSystem
{
    public static class QuestSystemInitializer
    {
        private static bool Initialized = false;
        private static DateTime InitializedAt;

        public static void Configure()
        {
            EventSink.WorldLoad += OnWorldLoad;
        }

        private static void OnWorldLoad()
        {
            Initialize();
        }

        public static void Initialize()
        {
            if (Initialized)
                return;

            try
            {
                Console.WriteLine();
                Console.WriteLine("╔══════════════════════════════════════════╗");
                Console.WriteLine("║     Iniciando Sistema de Quests...       ║");
                Console.WriteLine("╚══════════════════════════════════════════╝");

                var config = QuestSystemConfig.Instance;

                if (!config.Enabled)
                {
                    Console.WriteLine("[QuestSystem] Sistema DESATIVADO na configuracao.");
                    return;
                }

                config.Validate();

                // 1. Carrega definicoes de quests do JSON
                QuestRepository.Initialize();

                // 2. Inicializa o gerenciador
                QuestManager.Initialize();

                // 3. Carrega dados salvos dos jogadores
                //    (deve vir DEPOIS do QuestManager e dentro do WorldLoad)
                QuestPersistence.Initialize();

                // 4. Registra comandos de NPC
                QuestNPCCommands.Initialize();

                Initialized    = true;
                InitializedAt  = DateTime.UtcNow;

                Console.WriteLine(config.GetSystemInfo());
                Console.WriteLine("[QuestSystem] " + QuestRepository.Instance.QuestCount + " quests carregadas.");
                Console.WriteLine("[QuestSystem] Sistema inicializado com sucesso!");
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine("[QuestSystem] ERRO CRITICO na inicializacao: " + ex.Message);
                Console.WriteLine(ex.StackTrace);
            }
        }
    }
}