using System;
using Server;

namespace Server.Custom.QuestSystem.Items.MareVermelha
{
    public class MapasOperacoesCriminais : Item
    {
        [Constructable]
        public MapasOperacoesCriminais() : base(0x14EB) // Ícone de mapa
        {
            Name = "Mapas das Operações Criminais";
            Hue = 0; // Azul escuro
            Weight = 2.0;
            LootType = LootType.Blessed;
        }

        public override void OnDoubleClick(Mobile from)
        {
            from.SendMessage(1157, "=== MAPAS DAS OPERAÇÕES CRIMINAIS ===");
            from.SendMessage("");
            from.SendMessage("Mapas detalhados de todas as rotas comerciais da Coroa.");
            from.SendMessage("Pontos de emboscada marcados com X vermelho.");
            from.SendMessage("Contatos em cada vila aliada identificados por codinome.");
            from.SendMessage("Cronograma de entregas de armas dos Financiadores.");
            from.SendMessage("");
            from.SendMessage("Rotas de fuga para o leste estão sublinhadas.");
            from.SendMessage(1157, "A operação é muito maior do que bandidos comuns.");
        }

        public MapasOperacoesCriminais(Serial serial) : base(serial) { }

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