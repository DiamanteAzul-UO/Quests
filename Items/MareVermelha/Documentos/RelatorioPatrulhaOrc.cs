using System;
using Server;

namespace Server.Custom.QuestSystem.Items.MareVermelha
{
    public class RelatorioPatrulhaOrc : Item
    {
        [Constructable]
        public RelatorioPatrulhaOrc() : base(0x2258)
        {
            Name = "Relatório de Patrulha — Movimentação Orc";
            Hue = 1109; // Marrom escuro
            Weight = 1.0;
            LootType = LootType.Blessed;
        }

        public override void OnDoubleClick(Mobile from)
        {
            from.SendMessage(1109, "=== RELATÓRIO DE PATRULHA — FERRAVERA ===");
            from.SendMessage("Capitão Halvard Ironward, Punhos de Ferravera.");
            from.SendMessage("");
            from.SendMessage("Movimentações orc intensificaram ao norte.");
            from.SendMessage("Não são incursões dispersas. Há indícios de coordenação.");
            from.SendMessage("Insígnias padronizadas encontradas. Padrão militar incomum.");
            from.SendMessage("Os orcs parecem estar sendo empurrados, não atacando por vontade.");
            from.SendMessage("");
            from.SendMessage(1109, "Recomendação: Investigação urgente em Krag-Gath.");
        }

        public RelatorioPatrulhaOrc(Serial serial) : base(serial) { }

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