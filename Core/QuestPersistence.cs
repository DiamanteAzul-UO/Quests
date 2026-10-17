using System;
using System.IO;
using System.Collections.Generic;
using Server;
using Server.Mobiles;
using Server.Custom.QuestSystem.Quests;

namespace Server.Custom.QuestSystem
{
    public static class QuestPersistence
    {
        private static readonly string SavePath = Path.Combine(
            Core.BaseDirectory, "Saves", "QuestSystem", "QuestData.bin");

        private static readonly string BackupPath = Path.Combine(
            Core.BaseDirectory, "Saves", "QuestSystem", "QuestData.bak");

        private static bool _initialized;

        /// <summary>
        /// Registra a persistencia.
        ///
        /// Correcao critica: o ServUO invoca por reflexao todo metodo
        /// "public static void Initialize()" do assembly, e o
        /// QuestSystemInitializer TAMBEM chama este metodo explicitamente no
        /// evento WorldLoad. Sem o guard abaixo, "EventSink.WorldSave += OnWorldSave"
        /// era assinado DUAS vezes: o save era gravado em duplicidade e o
        /// QuestData.bak virava copia do arquivo recem-gravado (backup inutil).
        /// </summary>
        public static void Initialize()
        {
            if (_initialized)
                return;

            _initialized = true;

            EventSink.WorldSave += OnWorldSave;
            Load();
            Console.WriteLine("[QuestPersistence] Sistema de persistencia de quests registrado.");
        }

        private static void OnWorldSave(WorldSaveEventArgs e)
        {
            try
            {
                string dir = Path.GetDirectoryName(SavePath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                if (File.Exists(SavePath))
                {
                    if (File.Exists(BackupPath))
                        File.Delete(BackupPath);
                    File.Copy(SavePath, BackupPath);
                }

                using (FileStream fs = new FileStream(SavePath, FileMode.Create, FileAccess.Write))
                {
                    BinaryFileWriter writer = new BinaryFileWriter(fs, true);
                    Save(writer);
                    writer.Close();
                }

                Console.WriteLine("[QuestPersistence] Dados salvos com sucesso.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("[QuestPersistence] ERRO ao salvar: " + ex.Message);
            }
        }

        private static void Save(BinaryFileWriter writer)
        {
            writer.Write((int)1);

            var manager = QuestManager.Instance;
            var toSave  = new List<KeyValuePair<Serial, PlayerQuestData>>();

            foreach (Mobile m in World.Mobiles.Values)
            {
                PlayerMobile pm = m as PlayerMobile;
                if (pm == null || pm.Deleted)
                    continue;

                if (!manager.HasPlayerData(pm))
                    continue;

                var data = manager.GetPlayerData(pm);
                if (data == null)
                    continue;

                if (data.ActiveQuests.Count    == 0 &&
                    data.CompletedQuests.Count == 0 &&
                    data.FailedQuests.Count    == 0 &&
                    data.QuestFlags.Count      == 0)
                    continue;

                toSave.Add(new KeyValuePair<Serial, PlayerQuestData>(pm.Serial, data));
            }

            writer.Write((int)toSave.Count);

            foreach (var kvp in toSave)
            {
                writer.Write((int)kvp.Key.Value);
                kvp.Value.Serialize(writer);
            }

            Console.WriteLine("[QuestPersistence] " + toSave.Count + " jogadores salvos.");
        }

        private static void Load()
        {
            if (File.Exists(SavePath))
            {
                if (TryLoad(SavePath))
                    return;

                Console.WriteLine("[QuestPersistence] Arquivo principal corrompido. Tentando backup...");
            }

            if (File.Exists(BackupPath))
            {
                if (TryLoad(BackupPath))
                {
                    Console.WriteLine("[QuestPersistence] Dados restaurados do backup.");
                    return;
                }
            }

            Console.WriteLine("[QuestPersistence] Nenhum arquivo de save encontrado. Iniciando do zero.");
        }

        private static bool TryLoad(string path)
        {
            try
            {
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                {
                    BinaryFileReader reader = new BinaryFileReader(new BinaryReader(fs));
                    DoLoad(reader);
                    reader.Close();
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("[QuestPersistence] ERRO ao carregar " + path + ": " + ex.Message);
                return false;
            }
        }

        private static void DoLoad(BinaryFileReader reader)
        {
            int version = reader.ReadInt();
            int count   = reader.ReadInt();

            int loaded  = 0;
            int skipped = 0;

            var manager = QuestManager.Instance;

            for (int i = 0; i < count; i++)
            {
                int serialValue  = reader.ReadInt();
                Serial serial = (Serial)serialValue;
                Mobile m         = World.FindMobile(serial);
                PlayerMobile pm  = m as PlayerMobile;

                if (pm != null && !pm.Deleted)
                {
                    var data = new PlayerQuestData(pm);
                    data.Deserialize(reader);
                    manager.InjectPlayerData(pm.Serial, data);
                    loaded++;
                }
                else
                {
                    var dummy = new PlayerQuestData();
                    dummy.Deserialize(reader);
                    skipped++;
                }
            }

            Console.WriteLine("[QuestPersistence] Carregados: " + loaded + " | Ignorados: " + skipped);
        }
    }
}