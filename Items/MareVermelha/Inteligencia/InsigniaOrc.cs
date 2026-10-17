using System;
using Server;

namespace Server.Custom.QuestSystem.Items.MareVermelha
{
    public class InsigniaOrc : Item
    {
        [Constructable]
        public InsigniaOrc() : base(0x2F5B) // Medalhão
        {
            Name = "Insígnia Orc de Coordenação";
            Hue = 1175; // Verde orc
            Weight = 0.5;
            LootType = LootType.Regular;
        }

        public override void OnDoubleClick(Mobile from)
        {
            from.SendMessage(1175, "Uma insígnia de metal bruto com símbolos orc.");
            from.SendMessage("Não é artesanato individual — é padronizada.");
            from.SendMessage("Indica coordenação militar. Algo incomum para orcs.");
        }

        public InsigniaOrc(Serial serial) : base(serial) { }

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