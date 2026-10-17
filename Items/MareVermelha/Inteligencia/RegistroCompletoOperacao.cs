using System;
using Server;

namespace Server.Custom.QuestSystem.Items.MareVermelha
{
    public class RegistroCompletoOperacao : Item
    {
        [Constructable]
        public RegistroCompletoOperacao() : base(0x0FF2) // Livro grande
        {
            Name = "Registro Completo da Operação dos Financiadores";
            Hue = 0; // Vermelho carmesim
            Weight = 5.0;
            LootType = LootType.Blessed;
        }

        public override void OnDoubleClick(Mobile from)
        {
            from.SendMessage(38, "=== REGISTRO COMPLETO — CASA VALDREN ===");
            from.SendMessage("");
            from.SendMessage("O registro abrange décadas de operações.");
            from.SendMessage("");
            from.SendMessage("• Compra de minério da Fenda do Abismo desde o ano 7.");
            from.SendMessage("• Financiamento de bandidos em Arathos e região.");
            from.SendMessage("• Armamento de piratas na Ilha Pirata.");
            from.SendMessage("• Suprimento de nórdicos no arquipélago.");
            from.SendMessage("• Suborno de oficiais em Valdranthar (nomes listados).");
            from.SendMessage("• Contato com Thaelwyn Ithre'Morn para pesquisa do minério.");
            from.SendMessage("");
            from.SendMessage("A última página é uma carta pessoal de Aldric Valdren:");
            from.SendMessage(38, "'Vorathas virá. Não há como parar. Apenas negociar.'");
            from.SendMessage(38, "'Se o mundo deve ser consumido, que eu seja o último prato.'");
            from.SendMessage("");
            from.SendMessage(38, "[A letra treme nas últimas linhas. A tinta cheira a medo.]");
        }

        public RegistroCompletoOperacao(Serial serial) : base(serial) { }

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