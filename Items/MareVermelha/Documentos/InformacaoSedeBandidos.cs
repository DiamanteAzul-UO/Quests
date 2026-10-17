using System;
using Server;

namespace Server.Custom.QuestSystem.Items.MareVermelha
{
    public class InformacaoSedeBandidos : Item
    {
        [Constructable]
        public InformacaoSedeBandidos() : base(0x2258)
        {
            Name = "Informação sobre a Sede dos Bandidos";
            Hue = 0; // Marrom
            Weight = 1.0;
            LootType = LootType.Blessed;
        }

        public override void OnDoubleClick(Mobile from)
        {
            from.SendMessage(1102, "=== INFORMAÇÃO DO PRISIONEIRO ===");
            from.SendMessage("");
            from.SendMessage("'O covil fica ao nordeste. Terreno acidentado.'");
            from.SendMessage("'Paliçadas de madeira, guardas em turnos. Uns trinta homens.'");
            from.SendMessage("'O chefe é Draven. Ex-cavaleiro. Conhece cada truque de vocês.'");
            from.SendMessage("'As armas vêm do leste. Da ilha. Gente rica. Muito rica.'");
            from.SendMessage("");
            from.SendMessage(38, "[O prisioneiro não sabe mais. Ou não quer dizer.]");
        }

        public InformacaoSedeBandidos(Serial serial) : base(serial) { }

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