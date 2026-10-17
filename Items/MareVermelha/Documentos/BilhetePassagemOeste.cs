using System;
using Server;

namespace Server.Custom.QuestSystem.Items.MareVermelha
{
    public class BilhetePassagemOeste : Item
    {
        [Constructable]
        public BilhetePassagemOeste() : base(0x2258) // Pergaminho
        {
            Name = "Bilhete de Passagem para Vindorel";
            Hue = 0; // Azul real
            Weight = 1.0;
            LootType = LootType.Blessed;
        }

        public override void OnDoubleClick(Mobile from)
        {
            from.SendMessage(53, "=== BILHETE DE PASSAGEM PARA VINDOREL ===");
            from.SendMessage("Por ordem do Conselho da Coroa, o portador deste documento");
            from.SendMessage("está autorizado a viajar para Vindorel.");
            from.SendMessage(53, "Selo do Capitão Wulfric Stormwall.");
        }

        public BilhetePassagemOeste(Serial serial) : base(serial) { }

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