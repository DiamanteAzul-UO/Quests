using System;
using Server;

namespace Server.Custom.QuestSystem.Items.MareVermelha
{
    public class IntelNordica : Item
    {
        [Constructable]
        public IntelNordica() : base(0x2258)
        {
            Name = "Relatório de Inteligência — Defesas Nórdicas";
            Hue = 1154; // Azul gelo
            Weight = 1.0;
            LootType = LootType.Blessed;
        }

        public override void OnDoubleClick(Mobile from)
        {
            from.SendMessage(1154, "=== INTELIGÊNCIA — DEFESAS NÓRDICAS ===");
            from.SendMessage("");
            from.SendMessage("Fortaleza esculpida em penhasco de gelo.");
            from.SendMessage("Muralhas de pedra crua. Aproximação difícil por mar.");
            from.SendMessage("Guerreiros estimados: 200 a 300. Berserkers entre eles.");
            from.SendMessage("Xamãs com magia elementar. Não subestimar.");
            from.SendMessage("");
            from.SendMessage("Ponto fraco: flanco norte do penhasco. Gelo fino.");
            from.SendMessage("Ravenna anotou: 'Se for necessário, ataque por ali.'");
        }

        public IntelNordica(Serial serial) : base(serial) { }

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