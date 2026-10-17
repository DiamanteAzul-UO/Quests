using System;
using Server;

namespace Server.Custom.QuestSystem.Items.MareVermelha
{
    public class MedalhaHeroiKaelum : Item
    {
        [Constructable]
        public MedalhaHeroiKaelum() : base(0x2F5B) // Medalhão
        {
            Name = "Medalha do Herói de Kaelum";
            Hue = 1161; // Dourado
            Weight = 0.5;
            LootType = LootType.Blessed;
            Layer = Layer.Talisman;
        }

        public override void OnDoubleClick(Mobile from)
        {
            from.SendMessage(1161, "=== MEDALHA DO HERÓI DE KAELUM ===");
            from.SendMessage("");
            from.SendMessage("Concedida pelo Conselho da Coroa de Valdranthar");
            from.SendMessage("por serviço extraordinário ao reino.");
            from.SendMessage("");
            from.SendMessage("'Por coragem em Vindorel. Força em Krag-Gath.");
            from.SendMessage("Diplomacia em Sylanaar. Liderança no mar.");
            from.SendMessage("Por destruir os Financiadores e trazer a verdade à luz.'");
            from.SendMessage("");
            from.SendMessage(1161, "Que esta medalha lembre ao mundo o que um cidadão pode alcançar.");
        }

        public MedalhaHeroiKaelum(Serial serial) : base(serial) { }

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