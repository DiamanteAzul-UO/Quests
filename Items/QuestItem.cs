using System;
using Server;

namespace Server.Custom.QuestSystem
{
    /// <summary>
    /// Item generico de quest com tag de tipo para identificacao
    /// </summary>
    public class QuestItem : Item
    {
        private string m_QuestItemType;

        /// <summary>
        /// Tipo custom do item de quest (ex: CartaLacradaOlin)
        /// </summary>
        [CommandProperty(AccessLevel.GameMaster)]
        public string QuestItemType
        {
            get { return m_QuestItemType; }
            set { m_QuestItemType = value; }
        }

        [Constructable]
        public QuestItem() : this(0x14EF)
        {
        }

        [Constructable]
        public QuestItem(int itemId) : base(itemId)
        {
            Weight = 1.0;
            LootType = LootType.Blessed;
            Hue = 1153;
        }

        public QuestItem(Serial serial) : base(serial)
        {
        }

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write((int)0); // version
            writer.Write(m_QuestItemType);
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            int version = reader.ReadInt();
            m_QuestItemType = reader.ReadString();
        }
    }
}