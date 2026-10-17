using System;
using Server;

namespace Server.Custom.QuestSystem.Items.MareVermelha
{
    public class FragmentoRelatorioCifrado3 : Item
    {
        [Constructable]
        public FragmentoRelatorioCifrado3() : base(0x2258)
        {
            Name = "Fragmento de Relatório Cifrado [III — Torre]";
            Hue = 0;
            Weight = 0.5;
            LootType = LootType.Blessed;
        }

        public override void OnDoubleClick(Mobile from)
        {
            from.SendMessage(1157, "=== FRAGMENTO CIFRADO III ===");
            from.SendMessage("Terceiro e último fragmento. A cifra está completa.");
            from.SendMessage("");
            from.SendMessage("Palavras legíveis: '...coordenadas da ilha confirmadas...'");
            from.SendMessage("'...Ithre'Morn eliminou testemunhas...'");
            from.SendMessage("'...o minério é a chave de tudo...'");
            from.SendMessage("'...Valdren aguarda o sinal...'");
            from.SendMessage("");
            from.SendMessage(38, "Encontrado com Thaelwyn Ithre'Morn no topo da torre.");
        }

        public FragmentoRelatorioCifrado3(Serial serial) : base(serial) { }

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