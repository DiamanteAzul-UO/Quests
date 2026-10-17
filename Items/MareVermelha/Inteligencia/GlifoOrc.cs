using System;
using Server;

namespace Server.Custom.QuestSystem.Items.MareVermelha
{
    public class GlifoOrc : Item
    {
        [Constructable]
        public GlifoOrc() : base(0x1F14) // Pedra rúnica
        {
            Name = "Glifo Orc — Sussurro de Pedra";
            Hue = 1109; // Marrom antigo
            Weight = 3.0;
            LootType = LootType.Blessed;
        }

        public override void OnDoubleClick(Mobile from)
        {
            from.SendMessage(1109, "=== GLIFO ORC — SUSSURRO DE PEDRA ===");
            from.SendMessage("");
            from.SendMessage("Uma pedra pesada coberta de inscrições antigas.");
            from.SendMessage("Os símbolos parecem Orcish, mas com traços que lembram");
            from.SendMessage("a escrita Drow — mais antigos que qualquer registro conhecido.");
            from.SendMessage("");
            from.SendMessage("Ao tocar a pedra, você sente uma vibração profunda.");
            from.SendMessage("Não um som. Uma memória. De algo que foi dito há milênios.");
            from.SendMessage("");
            from.SendMessage(38, "O último Sussurro de Pedra. Preservado por séculos nas profundezas de Krag-Gath.");
        }

        public GlifoOrc(Serial serial) : base(serial) { }

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