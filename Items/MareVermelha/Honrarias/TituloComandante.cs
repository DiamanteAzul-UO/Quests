using System;
using Server;

namespace Server.Custom.QuestSystem.Items.MareVermelha
{
    public class TituloComandante : Item
    {
        [Constructable]
        public TituloComandante() : base(0x2258)
        {
            Name = "Título: Comandante da Frota de Valdranthar";
            Hue = 38; // Vermelho carmesim
            Weight = 1.0;
            LootType = LootType.Blessed;
        }

        public override void OnDoubleClick(Mobile from)
        {
            if (from.Title == "Comandante da Frota")
            {
                from.SendMessage(38, "Você já ostenta este título com honra.");
                return;
            }

            from.Title = "Comandante da Frota";
            from.SendMessage(38, "=== TÍTULO CONCEDIDO ===");
            from.SendMessage("Você agora é reconhecido como:");
            from.SendMessage(1161, $"{from.Name}, Comandante da Frota de Valdranthar");
            from.SendMessage("");
            from.SendMessage("Este título é visível a todos e permanente.");
            from.SendMessage(38, "Use-o com honra. Valdranthar observa.");

            this.Delete(); // Consome o item após uso
        }

        public TituloComandante(Serial serial) : base(serial) { }

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write((int)0);
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            int version = reader.ReadInt();
        }
    }
}