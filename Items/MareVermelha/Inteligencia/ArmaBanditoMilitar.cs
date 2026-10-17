using System;
using Server;

namespace Server.Custom.QuestSystem.Items.MareVermelha
{
    public class ArmaBanditoMilitar : Item
    {
        [Constructable]
        public ArmaBanditoMilitar() : base(0x13B9) // Espada
        {
            Name = "Arma de Qualidade Militar";
            Hue = 2500; // Metal escuro
            Weight = 4.0;
            LootType = LootType.Regular;
        }

        public override void OnDoubleClick(Mobile from)
        {
            from.SendMessage(38, "Uma arma de qualidade militar. Forjada com precisão.");
            from.SendMessage("A marca no ricasso indica produção na costa leste.");
            from.SendMessage("Definitivamente não é trabalho de ferreiro de vila.");
            from.SendMessage(38, "Evidência de que os bandidos são financiados por alguém poderoso.");
        }

        public ArmaBanditoMilitar(Serial serial) : base(serial) { }

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