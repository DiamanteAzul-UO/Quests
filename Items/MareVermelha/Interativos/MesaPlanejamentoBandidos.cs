/*****************************************************
 * MesaPlanejamentoBandidos.cs
 * Item interagível de quest — Mesa de Planejamento
 * Quest: Cap 5 — O Covil
 * 
 * Versão: 1.0.0
 * Compatível com: ServUO
 *****************************************************/

using System;
using Server;
using Server.Mobiles;
using Server.Custom.QuestSystem.Quests;

namespace Server.Custom.QuestSystem.Items.MareVermelha
{
    public class MesaPlanejamentoBandidos : Item
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

        [CommandProperty(AccessLevel.GameMaster)]
        public Point3D Destination { get { return m_Destination; } set { m_Destination = value; } }

        [CommandProperty(AccessLevel.GameMaster)]
        public Map DestinationMap { get { return m_DestinationMap; } set { m_DestinationMap = value; } }

        [CommandProperty(AccessLevel.GameMaster)]
        public bool TeleportEnabled { get { return m_TeleportEnabled; } set { m_TeleportEnabled = value; } }

        [CommandProperty(AccessLevel.GameMaster)]
        public string ExamineMessage { get { return m_ExamineMessage; } set { m_ExamineMessage = value; } }

        [CommandProperty(AccessLevel.GameMaster)]
        public string TeleportMessage { get { return m_TeleportMessage; } set { m_TeleportMessage = value; } }

        [CommandProperty(AccessLevel.GameMaster)]
        public int ExamineSound { get { return m_ExamineSound; } set { m_ExamineSound = value; } }

        [CommandProperty(AccessLevel.GameMaster)]
        public int TeleportSound { get { return m_TeleportSound; } set { m_TeleportSound = value; } }

        #endregion

        #region Constructor

        [Constructable]
        public MesaPlanejamentoBandidos() : base(0x1B71)
        {
            Name = "Mesa de Planejamento";
            Hue = 0;
            Movable = false;
            Visible = true;

            m_Destination = Point3D.Zero;
            m_DestinationMap = null;
            m_TeleportEnabled = false;
            m_Examined = false;

            m_ExamineMessage = "";
            m_TeleportMessage = "Voce encontra uma passagem secreta sob a mesa...";
            m_ExamineSound = 0x241;
            m_TeleportSound = 0x1FE;
        }

        public MesaPlanejamentoBandidos(Serial serial) : base(serial) { }

        #endregion

        #region Interaction

        public override void OnDoubleClick(Mobile from)
        {
            if (from == null || from.Deleted) return;

            if (!from.InRange(GetWorldLocation(), 2))
            {
                from.SendMessage("Voce esta muito longe para examinar a mesa.");
                return;
            }

            if (!m_Examined || !m_TeleportEnabled)
            {
                Examinar(from);
                return;
            }

            if (m_TeleportEnabled && m_Examined)
                TeleportarJogador(from);
        }

        private void Examinar(Mobile from)
        {
            if (from is PlayerMobile pm)
                QuestManager.Instance.OnInteract(pm, this);

            if (m_ExamineSound > 0)
                from.PlaySound(m_ExamineSound);

            from.SendMessage(1157, "=== MESA DE PLANEJAMENTO ===");
            from.SendMessage("");
            from.SendMessage("Mapas detalhados de todas as rotas comerciais da Coroa estao espalhados.");
            from.SendMessage("Pontos de emboscada marcados com X vermelho.");
            from.SendMessage("Cronogramas de patrulha da guarda — atualizados ate a semana passada.");
            from.SendMessage("");
            from.SendMessage("Alguem dentro da Coroa esta alimentando informacoes aos bandidos.");
            from.SendMessage("Os mapas tem anotacoes em duas caligrafias diferentes.");
            from.SendMessage("");
            from.SendMessage(38, "Uma delas e a de Draven Blackthorn.");
            from.SendMessage("A outra e culta. Demasiado culta para um bandido.");
            from.SendMessage("Alguem acima de Blackthorn esta comandando a operacao.");

            if (!string.IsNullOrEmpty(m_ExamineMessage))
            {
                from.SendMessage("");
                from.SendMessage(38, m_ExamineMessage);
            }

            if (m_TeleportEnabled && m_Destination != Point3D.Zero)
            {
                from.SendMessage("");
                from.SendMessage(53, "Ha uma passagem secreta sob a mesa...");
                from.SendMessage(53, "Clique novamente para descer.");
                m_Examined = true;
            }
            else
            {
                from.SendMessage("");
                from.SendMessage(38, "Evidencia recolhida. Os mapas revelam a escala da operacao.");
            }
        }

        private void TeleportarJogador(Mobile from)
        {
            if (m_Destination == Point3D.Zero) { from.SendMessage("A passagem esta bloqueada."); return; }

            Map targetMap = m_DestinationMap ?? Map;
            from.SendMessage(53, m_TeleportMessage);

            if (m_TeleportSound > 0) from.PlaySound(m_TeleportSound);

            Timer.DelayCall(TimeSpan.FromSeconds(1.0), delegate
            {
                if (from == null || from.Deleted) return;
                from.MoveToWorld(m_Destination, targetMap);
                from.SendMessage(1157, "Voce desce por uma passagem escondida sob a mesa...");
            });
        }

        #endregion

        #region Display

        public override void OnSingleClick(Mobile from)
        {
            if (from.InRange(GetWorldLocation(), 4))
            {
                LabelTo(from, "Mesa de Planejamento");
                LabelTo(from, "[Examine com duplo clique]");
            }
        }

        public override void GetProperties(ObjectPropertyList list)
        {
            base.GetProperties(list);
            list.Add("Mapas e documentos espalhados");
        }

        #endregion

        #region Serialization

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write((int)1);
            writer.Write(m_Destination);
            writer.Write(m_DestinationMap);
            writer.Write(m_TeleportEnabled);
            writer.Write(m_ExamineMessage);
            writer.Write(m_TeleportMessage);
            writer.Write(m_ExamineSound);
            writer.Write(m_TeleportSound);
            writer.Write(m_Examined);
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            int version = reader.ReadInt();
            m_Destination = reader.ReadPoint3D();
            m_DestinationMap = reader.ReadMap();
            m_TeleportEnabled = reader.ReadBool();
            m_ExamineMessage = reader.ReadString();
            m_TeleportMessage = reader.ReadString();
            m_ExamineSound = reader.ReadInt();
            m_TeleportSound = reader.ReadInt();
            if (version >= 1) m_Examined = reader.ReadBool();
            m_Examined = false;
        }

        #endregion
    }
}