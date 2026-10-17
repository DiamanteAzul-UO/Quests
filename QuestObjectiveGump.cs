using System;
using System.Collections.Generic;
using Server;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;
using Server.Custom.QuestSystem.Quests;
using Server.Custom.QuestSystem;           

namespace Server.Gumps
{
    /// <summary>
    /// Mini gump que aparece ao aceitar uma quest ou ao logar
    /// Mostra os objetivos ativos de forma compacta
    /// </summary>
    public class MystQuestObjectiveGump : Gump

    {
        private readonly PlayerMobile m_Player;
        private readonly string m_QuestId;
        private readonly int m_QuestIndex;   // qual quest está sendo mostrada (se tiver várias)
        private readonly int m_TotalQuests;

        private const int GW = 320;
        private const int GH = 200;

        private const string DOURADO  = "#C9A227";
        private const string BRANCO   = "#E5E5E5";
        private const string CINZA    = "#888888";
        private const string VERDE    = "#2E8B57";
        private const string VERMELHO = "#8B1A1A";
        private const string AMARELO  = "#DAA520";

        // ═══════════════════════════════════════════════════════════════
        //  MOSTRAR PARA UMA QUEST
        // ═══════════════════════════════════════════════════════════════

        public static void MostrarParaQuest(PlayerMobile player, string questId)
        {
            if (player == null || string.IsNullOrEmpty(questId))
                return;

            player.CloseGump(typeof(MystQuestObjectiveGump ));
            player.SendGump(new MystQuestObjectiveGump (player, questId, 0, 1));
        }

        /// <summary>
        /// Mostra um gump por quest ativa — chamado ao logar
        /// </summary>
        public static void MostrarQuestsAtivas(PlayerMobile player)
        {
            if (player == null)
                return;

            var playerData = QuestManager.Instance.GetPlayerData(player);
            if (playerData == null)
                return;

            var ativas = playerData.GetAllActiveQuests();
            if (ativas.Count == 0)
                return;

            // Fecha gumps anteriores
            player.CloseGump(typeof(MystQuestObjectiveGump ));

            int total = ativas.Count;

            // Abre um gump por quest, com offsets para não sobrepor
            for (int i = 0; i < total; i++)
            {
                string questId = ativas[i].QuestId;
                int capturedI  = i;

                // Delay escalonado para não abrir todos ao mesmo tempo
                Timer.DelayCall(TimeSpan.FromMilliseconds(200 * i), () =>
                {
                    if (player != null && !player.Deleted && player.NetState != null)
                    {
                        player.SendGump(new MystQuestObjectiveGump(player, questId, capturedI, total));
                    }
                });
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  CONSTRUTOR
        // ═══════════════════════════════════════════════════════════════

        public MystQuestObjectiveGump(PlayerMobile player, string questId, int questIndex, int totalQuests)
            : base(10 + (questIndex * 30), 300 - (questIndex * 30))
        {
            m_Player      = player;
            m_QuestId     = questId;
            m_QuestIndex  = questIndex;
            m_TotalQuests = totalQuests;

            Closable   = true;
            Disposable = true;
            Dragable   = true;
            Resizable  = false;

            var definition = QuestRepository.Instance.GetQuest(questId);
            var playerData = QuestManager.Instance.GetPlayerData(player);
            var active     = playerData?.GetActiveQuest(questId);

            if (definition == null || active == null)
                return;

            // Calcula altura dinâmica baseada nos objetivos
            int objCount = definition.Objectives.Count;
            int dynH     = Math.Max(GH, 90 + (objCount * 28) + 30);

            AddPage(0);

            // Fundo
            AddBackground(0, 0, GW, dynH, 9270);
            AddImageTiled(5, 5, GW - 10, dynH - 10, 2702);
            AddAlphaRegion(5, 5, GW - 10, dynH - 10);

            // Título
            string titulo = definition.Name;
            if (totalQuests > 1)
                titulo += " (" + (questIndex + 1) + "/" + totalQuests + ")";

            AddHtml(8, 8, GW - 16, 20,
                Cor(DOURADO, "<CENTER>" + titulo + "</CENTER>"),
                false, false);

            Linha(8, 30, GW - 16);

            // Status da quest
            string statusTxt = "";
            string corStatus = AMARELO;

            if (active.Status == QuestStatus.ReadyToTurnIn)
            {
                statusTxt = "Pronta para entregar!";
                corStatus = VERDE;
            }
            else
            {
                statusTxt = "Em andamento";
                corStatus = AMARELO;
            }

            AddHtml(8, 34, GW - 16, 18,
                Cor(corStatus, "<CENTER>" + statusTxt + "</CENTER>"),
                false, false);

            Linha(8, 54, GW - 16);

            // Objetivos
            int y = 60;

            for (int i = 0; i < definition.Objectives.Count; i++)
            {
                var obj      = definition.Objectives[i];
                int current  = 0;
                int required = obj.Required;
                bool complete = false;

                if (i < active.Progress.Count)
                {
                    current  = active.Progress[i].Current;
                    complete = active.Progress[i].IsComplete;
                }

                string corObj   = complete ? VERDE : BRANCO;
                string marcador = complete ? "■" : "□";

                // Descrição do objetivo
                string objText = marcador + " " + obj.Description;
                if (required > 1)
                    objText += " (" + current + "/" + required + ")";

                AddHtml(10, y, GW - 20, 20,
                    Cor(corObj, objText),
                    false, false);

                // Barra de progresso compacta
                if (required > 1)
                {
                    int barW     = GW - 24;
                    int barH     = 6;
                    int progress = required > 0 ? (current * 100) / required : 100;
                    int fillW    = (barW * progress) / 100;
                    if (fillW < 1 && current > 0) fillW = 1;

                    y += 20;

                    AddImageTiled(10, y, barW, barH, 2702);
                    AddAlphaRegion(10, y, barW, barH);

                    if (fillW > 0)
                        AddImageTiled(10, y, fillW, barH, 0x1404);

                    y += 8;
                }

                y += 22;
            }

            // Linha e botões
            Linha(8, y, GW - 16);
            y += 6;

            // Botão fechar
            AddButton(GW - 80, y, 4017, 4019, 0, GumpButtonType.Reply, 0);
            AddHtml(GW - 52, y + 2, 50, 18,
                Cor(CINZA, "Fechar"),
                false, false);

            // Botão ver detalhes
            AddButton(10, y, 4011, 4013, 1, GumpButtonType.Reply, 0);
            AddHtml(40, y + 2, 120, 18,
                Cor(DOURADO, "Ver detalhes"),
                false, false);
        }

        // ═══════════════════════════════════════════════════════════════
        //  RESPOSTA
        // ═══════════════════════════════════════════════════════════════

        public override void OnResponse(NetState sender, RelayInfo info)
        {
            if (info.ButtonID == 1)
            {
                PlayerMobile pm = sender.Mobile as PlayerMobile;
                if (pm == null) return;

                pm.CloseGump(typeof(QuestLogGump));
                pm.SendGump(new QuestLogGump(pm, 0, 0, m_QuestId));
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  HELPERS
        // ═══════════════════════════════════════════════════════════════

        private void Linha(int x, int y, int w)
        {
            AddImageTiled(x, y, w, 2, 9304);
        }

        private static string Cor(string cor, string txt)
        {
            return String.Format("<BASEFONT COLOR={0}>{1}</BASEFONT>", cor, txt);
        }
    }
}