using System;
using Server;

namespace Server.Custom.QuestSystem.Items.MareVermelha
{
    public class CartaAutoridade : Item
    {
        [Constructable]
        public CartaAutoridade() : base(0x2258)
        {
            Name = "Carta de Autoridade — Agir em Nome de Valdranthar";
            Hue = 0; // Dourado
            Weight = 1.0;
            LootType = LootType.Blessed;
        }

        public override void OnDoubleClick(Mobile from)
        {
            from.SendMessage(1161, "=== CARTA DE AUTORIDADE REAL ===");
            from.SendMessage("O portador desta carta está investido de autoridade plena");
            from.SendMessage("para agir em nome da Coroa de Valdranthar em todos os assuntos");
            from.SendMessage("relacionados à segurança do reino.");
            from.SendMessage(1161, "Assinada pelo Conselho da Coroa em sessão extraordinária.");
        }

        public CartaAutoridade(Serial serial) : base(serial) { }

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