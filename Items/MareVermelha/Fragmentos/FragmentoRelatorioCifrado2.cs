using System;
using Server;

namespace Server.Custom.QuestSystem.Items.MareVermelha
{
    public class FragmentoRelatorioCifrado2 : Item
    {
        [Constructable]
        public FragmentoRelatorioCifrado2() : base(0x2258)
        {
            Name = "Fragmento de Relatório Cifrado [II — Covil]";
            Hue = 0;
            Weight = 0.5;
            LootType = LootType.Blessed;
        }

        public override void OnDoubleClick(Mobile from)
        {
            from.SendMessage(1157, "=== FRAGMENTO CIFRADO II ===");
            from.SendMessage("Segundo fragmento. Mesma cifra, mesma mão.");
            from.SendMessage("");
            from.SendMessage("Palavras legíveis: '...rotas comerciais mapeadas...'");
            from.SendMessage("'...Blackthorn cumpriu sua parte...' '...armas entregues...'");
            from.SendMessage("'...a ilha aguarda confirmação...'");
            from.SendMessage("");
            from.SendMessage(38, "Encontrado com Draven Blackthorn na Sede dos Bandidos.");
        }

        public FragmentoRelatorioCifrado2(Serial serial) : base(serial) { }

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