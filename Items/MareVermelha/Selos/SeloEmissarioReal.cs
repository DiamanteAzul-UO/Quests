using System;
using Server;

namespace Server.Custom.QuestSystem.Items.MareVermelha
{
    public class SeloEmissarioReal : Item
    {
        [Constructable]
        public SeloEmissarioReal() : base(0x2F5B)
        {
            Name = "Selo de Emissário Real da Coroa";
            Hue = 1161; // Dourado
            Weight = 0.5;
            LootType = LootType.Blessed;
        }

        public override void OnDoubleClick(Mobile from)
        {
            from.SendMessage(1161, "Selo de Emissário Real da Coroa de Valdranthar.");
            from.SendMessage("Autoridade plena para agir em nome do Conselho da Coroa.");
            from.SendMessage("Concedido em sessão extraordinária do Conselho.");
        }

        public SeloEmissarioReal(Serial serial) : base(serial) { }

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