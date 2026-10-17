using System;
using Server;

namespace Server.Custom.QuestSystem.Items.MareVermelha
{
    public class FragmentoRelatorioCifrado1 : Item
    {
        [Constructable]
        public FragmentoRelatorioCifrado1() : base(0x2258)
        {
            Name = "Fragmento de Relatório Cifrado [I — Vindorel]";
            Hue = 0; // Azul escuro
            Weight = 0.5;
            LootType = LootType.Blessed;
        }

        public override void OnDoubleClick(Mobile from)
        {
            from.SendMessage(1157, "=== FRAGMENTO CIFRADO I ===");
            from.SendMessage("O texto está codificado em duas camadas de cifra.");
            from.SendMessage("Reconhece-se caligrafia educada — mão treinada, tinta de qualidade.");
            from.SendMessage("");
            from.SendMessage("*Palavras legíveis* '...o veio...' '...entrega para o norte...'");
            from.SendMessage("'...amostras confirmam...' '...propriedades que desafiam...'");
            from.SendMessage("");
            from.SendMessage(38, "Encontrado nos pertences de um Tocado de Vorathas em Vindorel.");
        }

        public FragmentoRelatorioCifrado1(Serial serial) : base(serial) { }

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