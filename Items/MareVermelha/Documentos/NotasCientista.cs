using System;
using Server;

namespace Server.Custom.QuestSystem.Items.MareVermelha
{
    public class NotasCientista : Item
    {
        [Constructable]
        public NotasCientista() : base(0x2258)
        {
            Name = "Notas Completas do Cientista";
            Hue = 0; // Cinza claro
            Weight = 1.0;
            LootType = LootType.Blessed;
        }

        public override void OnDoubleClick(Mobile from)
        {
            from.SendMessage(1150, "=== NOTAS DE PESQUISA — [NOME RISCADO] ===");
            from.SendMessage("");
            from.SendMessage("O minério da Fenda do Abismo possui propriedades únicas.");
            from.SendMessage("Amplifica ressonâncias dimensionais. Funciona como condutor");
            from.SendMessage("para energias que desafiam o éter conhecido.");
            from.SendMessage("");
            from.SendMessage("Amostras reagiram a estímulos que só podem ser descritos como");
            from.SendMessage("'fome'. O minério quer ser usado. Quer ser tocado.");
            from.SendMessage("");
            from.SendMessage("COORDENADAS DA OPERAÇÃO DE MINERAÇÃO:");
            from.SendMessage("Fenda do Abismo — Setor 7, nível inferior.");
            from.SendMessage("");
            from.SendMessage(38, "[As últimas linhas estão borradas com sangue]");
            from.SendMessage(38, "'Algo me observa pelo minério. Não é o minério. É o que está atrás.'");
        }

        public NotasCientista(Serial serial) : base(serial) { }

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