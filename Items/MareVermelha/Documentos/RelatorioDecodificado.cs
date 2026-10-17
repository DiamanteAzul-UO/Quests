using System;
using Server;

namespace Server.Custom.QuestSystem.Items.MareVermelha
{
    public class RelatorioDecodificado : Item
    {
        [Constructable]
        public RelatorioDecodificado() : base(0x2258)
        {
            Name = "Relatório Decodificado — Operação dos Financiadores";
            Hue = 0; // Verde escuro
            Weight = 1.0;
            LootType = LootType.Blessed;
        }

        public override void OnDoubleClick(Mobile from)
        {
            from.SendMessage(1266, "=== RELATÓRIO DECODIFICADO — ACADEMIA NEUTRA ===");
            from.SendMessage("Diretor Voss, Academia Neutra.");
            from.SendMessage("");
            from.SendMessage("Os Financiadores de Armas operam a partir de uma ilha a leste.");
            from.SendMessage("Compram minério da Fenda do Abismo — material que amplifica");
            from.SendMessage("a influência de Vorathas. Funciona como condutor dimensional.");
            from.SendMessage("");
            from.SendMessage("Vendem armas feitas com o minério para todas as facções:");
            from.SendMessage("bandidos, piratas e nórdicos. Deliberadamente.");
            from.SendMessage("");
            from.SendMessage(1266, "Objetivo: Criar caos suficiente para encobrir o avanço do Devorador.");
        }

        public RelatorioDecodificado(Serial serial) : base(serial) { }

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