using System;
using System.Collections.Generic;
using Server;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;
using Server.Custom.QuestSystem;
using Server.Custom.QuestSystem.Quests;

namespace Server.Gumps
{
    public class QuestLogGump : Gump
    {
        public static void Initialize()
        {
            Timer.DelayCall(TimeSpan.FromSeconds(1.0), () =>
            {
                PacketHandlers.RegisterEncoded(0x32, true, DirectQuestButton);
                Console.WriteLine("[QuestLogGump] Handler de quest substituido.");
            });
        }

        private static void DirectQuestButton(NetState state, IEntity e, EncodedReader reader)
        {
            if (state == null || state.Mobile == null) return;

            PlayerMobile pm = state.Mobile as PlayerMobile;
            if (pm == null) return;

            pm.CloseGump(typeof(XMLQuestLogGump));
            pm.CloseGump(typeof(QuestLogGump));
            pm.SendGump(new QuestLogGump(pm));
        }

        // ═══════════════════════════════════════════════════════════════
        //  CAMPOS
        // ═══════════════════════════════════════════════════════════════

        private readonly PlayerMobile m_Player;
        private readonly int m_Tab;
        private readonly int m_Page;
        private readonly string m_SelectedQuestId;

        private const int GW = 720;
        private const int GH = 540;
        private const int EntriesPerPage = 8;

        private const string DOURADO = "#C9A227";
        private const string BRANCO = "#E5E5E5";
        private const string CINZA = "#888888";
        private const string VERDE = "#2E8B57";
        private const string VERMELHO = "#8B1A1A";
        private const string AMARELO = "#DAA520";

        // ═══════════════════════════════════════════════════════════════
        //  CONSTRUTOR
        // ═══════════════════════════════════════════════════════════════

        public QuestLogGump(PlayerMobile player, int tab = 0, int page = 0, string selectedQuestId = null)
            : base(50, 30)
        {
            m_Player = player;
            m_Tab = tab;
            m_Page = page;
            m_SelectedQuestId = selectedQuestId;

            Closable = true;
            Disposable = true;
            Dragable = true;
            Resizable = false;

            AddPage(0);

            DesenharEstrutura();
            DesenharAbas();

            if (!string.IsNullOrEmpty(m_SelectedQuestId))
                DesenharDetalhes();
            else
                DesenharLista();
        }

        // ═══════════════════════════════════════════════════════════════
        //  ESTRUTURA
        // ═══════════════════════════════════════════════════════════════

        private void DesenharEstrutura()
        {
            AddBackground(0, 0, GW, GH, 9270);
            AddImageTiled(10, 10, GW - 20, GH - 20, 2702);
            AddAlphaRegion(10, 10, GW - 20, GH - 20);

            AddImage(-49, 60, 10440, 1645);
            AddImage(GW - 30, 60, 10441, 1645);

            AddHtml(20, 15, GW - 40, 24,
                Cor(DOURADO, "<CENTER>━━━  DIARIO DE MISSOES  ━━━</CENTER>"),
                false, false);

            Linha(20, 42, GW - 40);

            int totalCompletas = m_Player.GetTotalQuestsCompleted();
            int totalAtivas = m_Player.GetActiveQuests().Count;

            AddHtml(25, 48, GW - 50, 18,
                Cor(CINZA, "<CENTER>Ativas: ") +
                Cor(BRANCO, totalAtivas.ToString()) +
                Cor(CINZA, "  |  Completadas: ") +
                Cor(VERDE, totalCompletas.ToString()) +
                Cor(CINZA, "</CENTER>"),
                false, false);

            Linha(20, 68, GW - 40);
            Linha(20, GH - 45, GW - 40);
        }

        // ═══════════════════════════════════════════════════════════════
        //  ABAS — mais separadas
        // ═══════════════════════════════════════════════════════════════

        private void DesenharAbas()
        {
            int abaY = 74;
            int abaW = 190;  // largura fixa por aba
            int gap = 12;   // espaço entre abas
            int totalW = (abaW * 3) + (gap * 2);
            int startX = (GW - totalW) / 2;

            string[] nomes = { "Em Andamento", "Completadas", "Falhadas" };

            for (int i = 0; i < 3; i++)
            {
                int x = startX + (i * (abaW + gap));
                bool sel = (m_Tab == i);

                // Fundo da aba selecionada
                if (sel)
                {
                    AddImageTiled(x, abaY, abaW, 28, 2702);
                    AddAlphaRegion(x, abaY, abaW, 28);
                }

                // Borda destacada na aba selecionada
                if (sel)
                    Linha(x, abaY + 28, abaW);

                AddButton(x + 6, abaY + -2,
                    sel ? 2154 : 2472, 2154,
                    100 + i, GumpButtonType.Reply, 0);

                AddHtml(x + 40, abaY + 6, abaW - 32, 18,
                    Cor(sel ? DOURADO : CINZA, nomes[i]),
                    false, false);
            }

            Linha(20, abaY + 32, GW - 40);
        }

        // ═══════════════════════════════════════════════════════════════
        //  LISTA
        // ═══════════════════════════════════════════════════════════════

        private void DesenharLista()
        {
            int startY = 114;
            int listW = GW - 50;

            var quests = ObterQuestsDaAba();

            if (quests.Count == 0)
            {
                string msg;
                switch (m_Tab)
                {
                    case 1: msg = "Nenhuma missao completada ainda."; break;
                    case 2: msg = "Nenhuma missao falhada."; break;
                    default: msg = "Nenhuma missao ativa no momento."; break;
                }

                AddHtml(25, startY + 120, listW, 40,
                    Cor(CINZA, "<CENTER><I>" + msg + "</I></CENTER>"),
                    false, false);
                return;
            }

            int totalPages = Math.Max(1, (quests.Count + EntriesPerPage - 1) / EntriesPerPage);
            int startIdx = m_Page * EntriesPerPage;
            int endIdx = Math.Min(startIdx + EntriesPerPage, quests.Count);

            // Cabeçalho
            AddHtml(30, startY, 300, 18, Cor(DOURADO, "Missao"), false, false);
            AddHtml(360, startY, 120, 18, Cor(DOURADO, "Categoria"), false, false);
            AddHtml(490, startY, 100, 18, Cor(DOURADO, "Status"), false, false);
            AddHtml(600, startY, 80, 18, Cor(DOURADO, "Detalhes"), false, false);

            Linha(25, startY + 20, listW);

            int y = startY + 26;

            for (int i = startIdx; i < endIdx; i++)
            {
                var entry = quests[i];
                bool par = ((i - startIdx) % 2 == 0);

                if (par)
                {
                    AddImageTiled(25, y - 2, listW, 30, 2702);
                    AddAlphaRegion(25, y - 2, listW, 30);
                }

                string corNome = BRANCO;
                string statusText = "";

                switch (entry.Status)
                {
                    case QuestStatus.Active:
                        corNome = BRANCO;
                        statusText = Cor(AMARELO, "Em andamento");
                        break;
                    case QuestStatus.ReadyToTurnIn:
                        corNome = VERDE;
                        statusText = Cor(VERDE, "Pronta!");
                        break;
                    case QuestStatus.Completed:
                        corNome = VERDE;
                        statusText = Cor(VERDE, "Completa");
                        break;
                    case QuestStatus.Failed:
                        corNome = VERMELHO;
                        statusText = Cor(VERMELHO, "Falhou");
                        break;
                }

                AddHtml(30, y + 2, 320, 20, Cor(corNome, entry.Name), false, false);
                AddHtml(360, y + 2, 120, 20, Cor(CINZA, entry.Category), false, false);
                AddHtml(490, y + 2, 100, 20, statusText, false, false);

                AddButton(608, y + 2, 4011, 4013, 200 + i, GumpButtonType.Reply, 0);

                y += 32;
            }

            // Paginação
            int pageY = GH - 38;

            if (totalPages > 1)
            {
                AddHtml(GW / 2 - 40, pageY, 100, 18,
                    Cor(DOURADO, "<CENTER>" + (m_Page + 1) + " / " + totalPages + "</CENTER>"),
                    false, false);

                if (m_Page > 0)
                    AddButton(GW / 2 - 80, pageY, 4014, 4016, 50, GumpButtonType.Reply, 0);

                if (m_Page < totalPages - 1)
                    AddButton(GW / 2 + 60, pageY, 4005, 4007, 51, GumpButtonType.Reply, 0);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  DETALHES
        // ═══════════════════════════════════════════════════════════════

        private void DesenharDetalhes()
        {
            var definition = QuestRepository.Instance.GetQuest(m_SelectedQuestId);

            if (definition == null)
            {
                AddHtml(25, 150, GW - 50, 40,
                    Cor(VERMELHO, "<CENTER>Missao nao encontrada.</CENTER>"),
                    false, false);
                BotaoVoltar();
                return;
            }

            int y = 114;
            int cW = GW - 50;

            AddImageTiled(25, y, 3, 360, 9154);

            AddHtml(35, y, cW - 10, 24,
                Cor(DOURADO, "<CENTER>━  " + definition.Name.ToUpper() + "  ━</CENTER>"),
                false, false);

            Linha(30, y + 26, cW - 10);
            y += 34;

            AddHtml(35, y, cW - 10, 18,
                Cor(CINZA, "Categoria: ") + Cor(BRANCO, definition.Category.ToString()) +
                Cor(CINZA, "  |  Dif: ") + Cor(AMARELO, definition.Difficulty.ToString()) +
                Cor(CINZA, "  |  NPC: ") + Cor(BRANCO, definition.GiverNpcName ?? "?"),
                false, false);

            y += 24;
            Linha(30, y, cW - 10);
            y += 8;

            // ═════════════════════════════════════════════════════════
            //  DESCRIÇÃO — scrollbar
            // ═════════════════════════════════════════════════════════

            AddHtml(35, y, cW - 10, 20, Cor(DOURADO, "Descricao:"), false, false);
            y += 22;

            AddHtml(35, y, cW - 10, 60,
                Cor(BRANCO, definition.Description ?? "Sem descricao."),
                false, true);

            y += 66;
            Linha(30, y, cW - 10);
            y += 8;

            // ═════════════════════════════════════════════════════════
            //  OBJETIVOS — scrollbar
            // ═════════════════════════════════════════════════════════

            AddHtml(35, y, cW - 10, 20, Cor(DOURADO, "Objetivos:"), false, false);
            y += 24;

            string objetivosHtml = MontarHtmlObjetivos(definition);

            AddHtml(35, y, cW - 10, 100,
                objetivosHtml,
                false, true);

            y += 106;
            Linha(30, y, cW - 10);
            y += 8;

            // ═════════════════════════════════════════════════════════
            //  RECOMPENSAS — scrollbar
            // ═════════════════════════════════════════════════════════

            if (definition.Rewards != null && definition.Rewards.HasRewards())
            {
                AddHtml(35, y, cW - 10, 20, Cor(DOURADO, "Recompensas:"), false, false);
                y += 22;

                string recompensasHtml = MontarHtmlRecompensas(definition);

                AddHtml(35, y, cW - 10, 60,
                    recompensasHtml,
                    false, true);

                y += 66;
            }

            BotaoVoltar();

            var playerData = QuestManager.Instance.GetPlayerData(m_Player);
            var activeQuest = playerData?.GetActiveQuest(m_SelectedQuestId);

            if (activeQuest != null && activeQuest.Status == QuestStatus.Active)
            {
                AddButton(GW - 180, GH - 38, 4017, 4019, 300, GumpButtonType.Reply, 0);
                AddHtml(GW - 150, GH - 36, 140, 20,
                    Cor(VERMELHO, "Abandonar missao"),
                    false, false);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  MONTA HTML DOS OBJETIVOS
        // ═══════════════════════════════════════════════════════════════

        private string MontarHtmlObjetivos(QuestDefinition definition)
        {
            var playerData = QuestManager.Instance.GetPlayerData(m_Player);
            var activeQuest = playerData?.GetActiveQuest(m_SelectedQuestId);

            string html = "";

            for (int i = 0; i < definition.Objectives.Count; i++)
            {
                var obj = definition.Objectives[i];
                int current = 0;
                int required = obj.Required;
                bool complete = false;

                if (activeQuest != null && i < activeQuest.Progress.Count)
                {
                    current = activeQuest.Progress[i].Current;
                    complete = activeQuest.Progress[i].IsComplete;
                }

                string corObj = complete ? VERDE : BRANCO;
                string marcador = complete ? "■" : "□";

                html += Cor(corObj, marcador + " " + obj.Description);

                if (required > 1)
                {
                    string corProg = complete ? VERDE : AMARELO;
                    html += "  " + Cor(corProg, "[" + current + "/" + required + "]");
                }

                html += "<BR>";

                if (!string.IsNullOrEmpty(obj.LocationHint))
                {
                    html += Cor(CINZA, "<I>  Dica: " + obj.LocationHint + "</I>") + "<BR>";
                }

                if (i < definition.Objectives.Count - 1)
                    html += "<BR>";
            }

            return html;
        }

        // ═══════════════════════════════════════════════════════════════
        //  MONTA HTML DAS RECOMPENSAS
        // ═══════════════════════════════════════════════════════════════

        private string MontarHtmlRecompensas(QuestDefinition definition)
        {
            string html = "";

            if (definition.Rewards == null || !definition.Rewards.HasRewards())
                return Cor(CINZA, "<I>Nenhuma recompensa.</I>");

            var rewardList = definition.Rewards.GetDetailedList();

            for (int i = 0; i < rewardList.Count; i++)
            {
                html += Cor(VERDE, "• " + rewardList[i]);

                if (i < rewardList.Count - 1)
                    html += "<BR>";
            }

            return html;
        }

        // ═══════════════════════════════════════════════════════════════
        //  RESPOSTA
        // ═══════════════════════════════════════════════════════════════

        public override void OnResponse(NetState sender, RelayInfo info)
        {
            Mobile from = sender.Mobile;
            if (from == null) return;

            PlayerMobile pm = from as PlayerMobile;
            if (pm == null) return;

            int btn = info.ButtonID;
            if (btn == 0) return;

            if (btn >= 100 && btn <= 102)
            {
                pm.CloseGump(typeof(QuestLogGump));
                pm.SendGump(new QuestLogGump(pm, btn - 100, 0, null));
                return;
            }

            if (btn == 50)
            {
                pm.CloseGump(typeof(QuestLogGump));
                pm.SendGump(new QuestLogGump(pm, m_Tab, Math.Max(0, m_Page - 1), null));
                return;
            }

            if (btn == 51)
            {
                pm.CloseGump(typeof(QuestLogGump));
                pm.SendGump(new QuestLogGump(pm, m_Tab, m_Page + 1, null));
                return;
            }

            if (btn == 99)
            {
                pm.CloseGump(typeof(QuestLogGump));
                pm.SendGump(new QuestLogGump(pm, m_Tab, m_Page, null));
                return;
            }

            if (btn >= 200 && btn < 300)
            {
                int idx = btn - 200;
                var quests = ObterQuestsDaAba();

                if (idx >= 0 && idx < quests.Count)
                {
                    pm.CloseGump(typeof(QuestLogGump));
                    pm.SendGump(new QuestLogGump(pm, m_Tab, m_Page, quests[idx].QuestId));
                }
                return;
            }

            if (btn == 300 && !string.IsNullOrEmpty(m_SelectedQuestId))
            {
                string message;
                QuestManager.Instance.AbandonQuest(pm, m_SelectedQuestId, out message);

                pm.SendMessage(0x22, message);
                pm.CloseGump(typeof(QuestLogGump));
                pm.SendGump(new QuestLogGump(pm, m_Tab, m_Page, null));
                return;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  DADOS DA ABA
        // ═══════════════════════════════════════════════════════════════

        private List<QuestLogEntry> ObterQuestsDaAba()
        {
            var result = new List<QuestLogEntry>();
            var playerData = QuestManager.Instance.GetPlayerData(m_Player);

            if (playerData == null)
                return result;

            switch (m_Tab)
            {
                case 0:
                    foreach (var aq in playerData.GetAllActiveQuests())
                    {
                        var def = QuestRepository.Instance.GetQuest(aq.QuestId);
                        if (def != null)
                        {
                            result.Add(new QuestLogEntry
                            {
                                QuestId = aq.QuestId,
                                Name = def.Name,
                                Category = def.Category.ToString(),
                                Status = aq.Status
                            });
                        }
                    }
                    break;

                case 1:
                    foreach (var cq in playerData.CompletedQuests)
                    {
                        var def = QuestRepository.Instance.GetQuest(cq.QuestId);
                        result.Add(new QuestLogEntry
                        {
                            QuestId = cq.QuestId,
                            Name = def != null ? def.Name : cq.QuestName,
                            Category = def != null ? def.Category.ToString() : "?",
                            Status = QuestStatus.Completed
                        });
                    }
                    break;

                case 2:
                    foreach (var fq in playerData.FailedQuests)
                    {
                        var def = QuestRepository.Instance.GetQuest(fq.QuestId);
                        result.Add(new QuestLogEntry
                        {
                            QuestId = fq.QuestId,
                            Name = def != null ? def.Name : fq.QuestName,
                            Category = def != null ? def.Category.ToString() : "?",
                            Status = QuestStatus.Failed
                        });
                    }
                    break;
            }

            return result;
        }

        // ═══════════════════════════════════════════════════════════════
        //  HELPERS
        // ═══════════════════════════════════════════════════════════════

        private void BotaoVoltar()
        {
            AddButton(25, GH - 38, 4014, 4016, 99, GumpButtonType.Reply, 0);
            AddHtml(60, GH - 36, 100, 20, Cor(CINZA, "◀  Voltar"), false, false);
        }

        private void Linha(int x, int y, int w)
        {
            AddImageTiled(x, y, w, 2, 9304);
        }

        private static string Cor(string cor, string txt)
        {
            return String.Format("<BASEFONT COLOR={0}>{1}</BASEFONT>", cor, txt);
        }

        private class QuestLogEntry
        {
            public string QuestId;
            public string Name;
            public string Category;
            public QuestStatus Status;
        }
    }
}