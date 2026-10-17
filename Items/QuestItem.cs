/*****************************************************
 * QuestItem.cs
 * Item generico de quest com tag de tipo para identificacao
 *
 * E o representante fisico de "itens virtuais": tipos citados
 * no JSON (CartaLacradaOlin, AmuletoOssoRunas, SeloFortaleza...)
 * que nao possuem classe C# propria no shard.
 *
 * A tag QuestItemType e a unica fonte de verdade usada pelo
 * QuestItemMatcher para contar, consumir e validar esses itens.
 *
 * Versao: 2.0.0
 * Compativel com: ServUO
 *****************************************************/

using System;
using Server;

namespace Server.Custom.QuestSystem
{
    /// <summary>
    /// Implementado por qualquer item que carregue uma tag de tipo de quest.
    /// Permite que itens custom do shard participem dos objetivos
    /// Collect/Deliver/Craft sem precisar herdar de QuestItem.
    /// </summary>
    public interface IQuestTaggedItem
    {
        string QuestItemType { get; }
    }

    /// <summary>
    /// Item generico de quest com tag de tipo para identificacao
    /// </summary>
    public class QuestItem : Item, IQuestTaggedItem
    {
        private string m_QuestItemType;
        private string m_QuestDisplayName;

        /// <summary>
        /// Tipo custom do item de quest (ex: CartaLacradaOlin)
        /// </summary>
        [CommandProperty(AccessLevel.GameMaster)]
        public string QuestItemType
        {
            get { return m_QuestItemType; }
            set { m_QuestItemType = value; }
        }

        /// <summary>
        /// Nome amigavel usado nas definicoes de quest (ex: "Carta Lacrada de Olin").
        /// Guardado separadamente de Name para sobreviver a renomeacoes no jogo.
        /// </summary>
        [CommandProperty(AccessLevel.GameMaster)]
        public string QuestDisplayName
        {
            get { return m_QuestDisplayName; }
            set { m_QuestDisplayName = value; }
        }

        /// <summary>
        /// Itens de quest usam LootType.Blessed (definido no construtor),
        /// portanto nao caem no chao nem no corpo quando o jogador morre.
        /// </summary>
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

        /// <summary>
        /// Fabrica um QuestItem ja configurado com tag, nome e quantidade.
        /// </summary>
        public static QuestItem Create(string questItemType, string displayName, int hue = 0, int amount = 1)
        {
            QuestItem item = new QuestItem();

            item.m_QuestItemType = string.IsNullOrWhiteSpace(questItemType) ? displayName : questItemType;
            item.m_QuestDisplayName = displayName;
            item.Name = string.IsNullOrWhiteSpace(displayName) ? questItemType : displayName;
            item.LootType = LootType.Blessed;
            item.Weight = 1.0;
            item.Hue = hue > 0 ? hue : 1153;

            if (amount > 1 && item.Stackable)
                item.Amount = amount;

            return item;
        }

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write((int)1); // version
            writer.Write(m_QuestItemType);
            writer.Write(m_QuestDisplayName);
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            int version = reader.ReadInt();

            m_QuestItemType = reader.ReadString();

            if (version >= 1)
                m_QuestDisplayName = reader.ReadString();
        }
    }
}
