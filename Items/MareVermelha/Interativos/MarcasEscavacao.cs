/*****************************************************
 * MarcasEscavacao.cs
 * Item interagível de quest — Marcas de Escavação
 * 
 * Funcionalidades:
 * - Visualmente parece marcas no chão
 * - Ao clicar 2x: examina as marcas (para quest)
 * - Opção de teleporte configurável via [props
 * - Pode ser revelado por Detect Hidden
 * 
 * Versão: 1.0.0
 * Compatível com: ServUO
 *****************************************************/

using System;
using Server;
using Server.Mobiles;
using Server.Network;
using Server.Custom.QuestSystem.Quests;

namespace Server.Custom.QuestSystem.Items.MareVermelha
{
    public class MarcasEscavacao : Item
    {
        #region Properties

        private Point3D m_Destination;
        private Map m_DestinationMap;
        private bool m_TeleportEnabled;
        private bool m_Examined;
        private string m_ExamineMessage;
        private string m_TeleportMessage;
        private int m_ExamineSound;
        private int m_TeleportSound;

        /// <summary>
        /// Coordenadas do destino de teleporte
        /// </summary>
        [CommandProperty(AccessLevel.GameMaster)]
        public Point3D Destination
        {
            get { return m_Destination; }
            set { m_Destination = value; }
        }

        /// <summary>
        /// Mapa do destino de teleporte
        /// </summary>
        [CommandProperty(AccessLevel.GameMaster)]
        public Map DestinationMap
        {
            get { return m_DestinationMap; }
            set { m_DestinationMap = value; }
        }

        /// <summary>
        /// Se o teleporte está ativo
        /// </summary>
        [CommandProperty(AccessLevel.GameMaster)]
        public bool TeleportEnabled
        {
            get { return m_TeleportEnabled; }
            set { m_TeleportEnabled = value; }
        }

        /// <summary>
        /// Mensagem ao examinar
        /// </summary>
        [CommandProperty(AccessLevel.GameMaster)]
        public string ExamineMessage
        {
            get { return m_ExamineMessage; }
            set { m_ExamineMessage = value; }
        }

        /// <summary>
        /// Mensagem ao teleportar
        /// </summary>
        [CommandProperty(AccessLevel.GameMaster)]
        public string TeleportMessage
        {
            get { return m_TeleportMessage; }
            set { m_TeleportMessage = value; }
        }

        /// <summary>
        /// Som ao examinar
        /// </summary>
        [CommandProperty(AccessLevel.GameMaster)]
        public int ExamineSound
        {
            get { return m_ExamineSound; }
            set { m_ExamineSound = value; }
        }

        /// <summary>
        /// Som ao teleportar
        /// </summary>
        [CommandProperty(AccessLevel.GameMaster)]
        public int TeleportSound
        {
            get { return m_TeleportSound; }
            set { m_TeleportSound = value; }
        }

        #endregion

        #region Constructor

        [Constructable]
        public MarcasEscavacao() : base(0x1B71) // Mesmo ID do HiddenTrapDoor
        {
            Name = "Marcas de Escavacao";
            Hue = 1109; // Marrom terra
            Movable = false;
            Visible = true;

            m_Destination = Point3D.Zero;
            m_DestinationMap = null;
            m_TeleportEnabled = false;
            m_Examined = false;

            m_ExamineMessage = "";
            m_TeleportMessage = "Voce desce pelas marcas de escavacao...";

            m_ExamineSound = 0x241; // Som de descoberta
            m_TeleportSound = 0x1FE; // Som de movimento
        }

        public MarcasEscavacao(Serial serial) : base(serial)
        {
        }

        #endregion

        #region Interaction

        public override void OnDoubleClick(Mobile from)
        {
            if (from == null || from.Deleted)
                return;

            if (!from.InRange(GetWorldLocation(), 2))
            {
                from.SendMessage("Voce esta muito longe para examinar as marcas.");
                return;
            }

            // Primeira interação: examinar
            if (!m_Examined || !m_TeleportEnabled)
            {
                ExaminarMarcas(from);
                return;
            }

            // Segunda interação: teleportar (se habilitado)
            if (m_TeleportEnabled && m_Examined)
            {
                TeleportarJogador(from);
            }
        }

        /// <summary>
        /// Examina as marcas de escavação
        /// </summary>
        private void ExaminarMarcas(Mobile from)
        {

            // ── NOTIFICA O SISTEMA DE QUESTS ──
            if (from is PlayerMobile pm)
            {
                Server.Custom.QuestSystem.Quests.QuestManager.Instance.OnInteract(pm, this);
            }


            // Som
            if (m_ExamineSound > 0)
                from.PlaySound(m_ExamineSound);

            // Mensagens de lore
            from.SendMessage(1109, "=== MARCAS DE ESCAVACAO ===");
            from.SendMessage("");
            from.SendMessage("Voce se abaixa e examina as marcas no chao...");
            from.SendMessage("");
            from.SendMessage("Riscos frescos na pedra. Ferramentas foram usadas aqui recentemente.");
            from.SendMessage("A terra esta revolvida — alguem cavou fundo, com pressa.");
            from.SendMessage("Ha marcas de botas no barro. Varias pessoas. Pelo menos tres.");
            from.SendMessage("");

            if (!string.IsNullOrEmpty(m_ExamineMessage))
            {
                from.SendMessage(38, m_ExamineMessage);
                from.SendMessage("");
            }

            // Se teleporte está habilitado, informa
            if (m_TeleportEnabled && m_Destination != Point3D.Zero)
            {
                from.SendMessage(53, "Ha uma passagem escavada aqui. Parece levar para baixo...");
                from.SendMessage(53, "Clique novamente para descer.");
                m_Examined = true;
            }
            else
            {
                from.SendMessage("As marcas nao levam a lugar algum visivel. Mas alguem esteve aqui.");
                from.SendMessage(38, "Evidencia recolhida.");
            }
        }

        /// <summary>
        /// Teleporta o jogador para o destino
        /// </summary>
        private void TeleportarJogador(Mobile from)
        {
            if (m_Destination == Point3D.Zero)
            {
                from.SendMessage("A passagem esta bloqueada.");
                return;
            }

            Map targetMap = m_DestinationMap ?? Map;

            // Confirmação
            from.SendMessage(53, m_TeleportMessage);

            // Som de teleporte
            if (m_TeleportSound > 0)
                from.PlaySound(m_TeleportSound);

            // Timer para dar tempo do efeito
            Timer.DelayCall(TimeSpan.FromSeconds(1.0), delegate
            {
                if (from == null || from.Deleted)
                    return;

                // Teleporta
                from.MoveToWorld(m_Destination, targetMap);

                from.SendMessage(1109, "Voce desce pela passagem escavada...");
                from.SendMessage("O cheiro de terra umida e enxofre enche suas narinas.");
            });
        }

        #endregion

        #region Mouse Over

        public override void OnSingleClick(Mobile from)
        {
            if (from.InRange(GetWorldLocation(), 4))
            {
                LabelTo(from, "Marcas de Escavacao");
                LabelTo(from, "[Examine com duplo clique]");
            }
        }

        public override void GetProperties(ObjectPropertyList list)
        {
            base.GetProperties(list);
            list.Add("Riscos frescos na pedra");

            if (m_TeleportEnabled && m_Destination != Point3D.Zero)
                list.Add("Uma passagem parece levar para baixo...");
        }

        #endregion

        #region Serialization

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write((int)1); // Versão

            // Versão 0
            writer.Write(m_Destination);
            writer.Write(m_DestinationMap);
            writer.Write(m_TeleportEnabled);
            writer.Write(m_ExamineMessage);
            writer.Write(m_TeleportMessage);
            writer.Write(m_ExamineSound);
            writer.Write(m_TeleportSound);

            // Versão 1
            writer.Write(m_Examined);
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            int version = reader.ReadInt();

            // Versão 0
            m_Destination = reader.ReadPoint3D();
            m_DestinationMap = reader.ReadMap();
            m_TeleportEnabled = reader.ReadBool();
            m_ExamineMessage = reader.ReadString();
            m_TeleportMessage = reader.ReadString();
            m_ExamineSound = reader.ReadInt();
            m_TeleportSound = reader.ReadInt();

            // Versão 1
            if (version >= 1)
                m_Examined = reader.ReadBool();

            // Reset examined ao reiniciar servidor (para quest funcionar novamente)
            m_Examined = false;
        }

        #endregion
    }
}