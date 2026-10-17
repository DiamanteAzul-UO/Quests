using System;
using Server;

namespace Server.Custom.QuestSystem.Items.MareVermelha
{
    public class TratadoApoioElfico : Item
    {
        [Constructable]
        public TratadoApoioElfico() : base(0x2258)
        {
            Name = "Tratado de Apoio Élfico";
            Hue = 0; // Prata
            Weight = 1.0;
            LootType = LootType.Blessed;
        }

        public override void OnDoubleClick(Mobile from)
        {
            from.SendMessage(1150, "=== TRATADO DE APOIO — SYLANAAR E VALDRANTHAR ===");
            from.SendMessage("");
            from.SendMessage("Pelos termos acordados entre o Coro Prateado de Sylanaar");
            from.SendMessage("e o Conselho da Coroa de Valdranthar:");
            from.SendMessage("");
            from.SendMessage("• Sylanaar enviará um destacamento de arqueiros de elite.");
            from.SendMessage("• Um mago do Aspecto da Teia será designado para auxílio.");
            from.SendMessage("• Em troca, Valdranthar concede acesso pleno aos relatórios sobre Vorathas.");
            from.SendMessage("• Representação élfica plena no planejamento militar.");
            from.SendMessage("");
            from.SendMessage("Selado com cera prateada pelo Coro Prateado.");
            from.SendMessage(1150, "Os termos não são gentis. Mas são justos.");
        }

        public TratadoApoioElfico(Serial serial) : base(serial) { }

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