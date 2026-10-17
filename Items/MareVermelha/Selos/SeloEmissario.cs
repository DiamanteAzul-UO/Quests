using System;
using Server;

namespace Server.Custom.QuestSystem.Items.MareVermelha
{
    public class SeloEmissario : Item
    {
        [Constructable]
        public SeloEmissario() : base(0x2F5B) // Medalhão/Selo
        {
            Name = "Selo de Emissário";
            Hue = 1153; // Azul real
            Weight = 0.5;
            LootType = LootType.Blessed;
        }

        public override void OnDoubleClick(Mobile from)
        {
            from.SendMessage(1153, "Selo oficial de Emissário da Coroa de Valdranthar.");
            from.SendMessage("Concede acesso a postos aliados e direito de entrega de despachos.");
        }

        public SeloEmissario(Serial serial) : base(serial) { }

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