using System;
using Server;

namespace Server.Custom.QuestSystem.Items.MareVermelha
{
    public class CoordenadasIlhaFinanciadores : Item
    {
        [Constructable]
        public CoordenadasIlhaFinanciadores() : base(0x14EB) // Mapa
        {
            Name = "Coordenadas da Ilha dos Financiadores";
            Hue = 0; // Vermelho
            Weight = 1.0;
            LootType = LootType.Blessed;
        }

        public override void OnDoubleClick(Mobile from)
        {
            from.SendMessage(38, "=== COORDENADAS — ILHA DOS FINANCIADORES ===");
            from.SendMessage("");
            from.SendMessage("Mapa detalhado com a localização exata da ilha.");
            from.SendMessage("Rotas de aproximação marcadas. Correntes marítimas indicadas.");
            from.SendMessage("Pontos cegos nas defesas assinalados a tinta vermelha.");
            from.SendMessage("");
            from.SendMessage("Uma nota na margem: 'Valdren não espera ataque pelo norte.'");
        }

        public CoordenadasIlhaFinanciadores(Serial serial) : base(serial) { }

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