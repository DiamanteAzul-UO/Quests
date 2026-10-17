using System;
using Server;

namespace Server.Custom.QuestSystem.Items.MareVermelha
{
    public class MapasRotasFinanciadores : Item
    {
        [Constructable]
        public MapasRotasFinanciadores() : base(0x14EB)
        {
            Name = "Mapas das Rotas de Suprimento dos Financiadores";
            Hue = 0; // Azul
            Weight = 2.0;
            LootType = LootType.Blessed;
        }

        public override void OnDoubleClick(Mobile from)
        {
            from.SendMessage(1157, "=== ROTAS DE SUPRIMENTO ===");
            from.SendMessage("");
            from.SendMessage("Mapas navais detalhados mostrando rotas de suprimento.");
            from.SendMessage("Da Fenda do Abismo à Ilha dos Financiadores.");
            from.SendMessage("Da ilha para bandidos, piratas e nórdicos.");
            from.SendMessage("Cada ponto de entrega marcado com data e quantidade.");
        }

        public MapasRotasFinanciadores(Serial serial) : base(serial) { }

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