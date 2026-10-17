using System;
using Server;

namespace Server.Custom.QuestSystem.Items.MareVermelha
{
    public class SeloComandante : Item
    {
        [Constructable]
        public SeloComandante() : base(0x2F5B)
        {
            Name = "Selo de Comandante da Frota";
            Hue = 38; // Vermelho carmesim
            Weight = 0.5;
            LootType = LootType.Blessed;
        }

        public override void OnDoubleClick(Mobile from)
        {
            from.SendMessage(38, "Selo de Comandante da Frota de Valdranthar.");
            from.SendMessage("Concede autoridade sobre navios militares da Maré de Ferro.");
            from.SendMessage("Outorgado pelo Almirante da Maré e ratificado pelo Conselho.");
        }

        public SeloComandante(Serial serial) : base(serial) { }

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