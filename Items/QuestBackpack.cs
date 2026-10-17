/*****************************************************
 * QuestBackpack.cs
 * Mochila customizada que notifica o QuestManager
 * quando itens são adicionados
 *****************************************************/

using System;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Server.Custom.QuestSystem.Quests
{
    public class QuestBackpack : Backpack
    {
        [Constructable]
        public QuestBackpack() : base()
        {
        }

        /// <summary>
        /// Chamado automaticamente pelo ServUO sempre que
        /// qualquer item entra neste container
        /// </summary>
        public override void OnItemAdded(Item item)
        {
            // Sempre chama o base primeiro
            base.OnItemAdded(item);

            // Verifica se o dono é um PlayerMobile
            if (!(RootParent is PlayerMobile player))
                return;

            // Ignora itens deletados
            if (item == null || item.Deleted)
                return;

            Console.WriteLine($"[QuestBackpack] Item entrou na mochila: " +
                              $"{item.GetType().Name} Amount={item.Amount} " +
                              $"Hue={item.Hue} Player={player.Name}");

            // Notifica o QuestManager
            QuestManager.Instance.OnItemCollected(player, item);
        }

        // ── Serialização obrigatória no ServUO ───────────────────────

        public QuestBackpack(Serial serial) : base(serial)
        {
        }

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(0); // versão
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            int version = reader.ReadInt();
        }
    }
}