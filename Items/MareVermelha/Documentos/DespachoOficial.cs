using System;
using Server;

namespace Server.Custom.QuestSystem.Items.MareVermelha
{
    public class DespachoOficial : Item
    {
        [Constructable]
        public DespachoOficial() : base(0x2258) // Pergaminho
        {
            Name = "Despacho Oficial da Coroa";
            Hue = 0; // Azul real
            Weight = 1.0;
            LootType = LootType.Blessed;
        }

        public override void OnDoubleClick(Mobile from)
        {
            from.SendMessage(53, "=== DESPACHO OFICIAL DA COROA DE VALDRANTHAR ===");
            from.SendMessage("Por ordem do Conselho da Coroa, o portador deste documento");
            from.SendMessage("está autorizado a entregar correspondência oficial");
            from.SendMessage("aos postos aliados do reino.");
            from.SendMessage(53, "Selo do Chanceler Edric Greymoor.");
        }

        public DespachoOficial(Serial serial) : base(serial) { }

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