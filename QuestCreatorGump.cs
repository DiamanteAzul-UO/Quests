using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Server;
using Server.Commands;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;
using Server.Custom.QuestSystem.Quests;

namespace Server.Gumps
{
    public class QuestCreatorGump : Gump
    {
        private readonly Mobile m_From;
        private readonly int m_Step;
        private readonly QuestCreatorData m_Data;

        private const int GW = 700;
        private const int GH = 580;

        private const string DOURADO = "#C9A227";
        private const string BRANCO = "#E5E5E5";
        private const string CINZA = "#888888";
        private const string VERDE = "#2E8B57";
        private const string VERMELHO = "#8B1A1A";

        public static void Initialize()
        {
            CommandSystem.Register("criarquest", AccessLevel.GameMaster, OnCommand);
            Console.WriteLine("[QuestCreator] Comando [criarquest registrado");
        }

        private static void OnCommand(CommandEventArgs e)
        {
            e.Mobile.CloseGump(typeof(QuestCreatorGump));
            e.Mobile.SendGump(new QuestCreatorGump(e.Mobile, 1, new QuestCreatorData()));
        }

        public QuestCreatorGump(Mobile from, int step, QuestCreatorData data) : base(50, 20)
        {
            m_From = from;
            m_Step = step;
            m_Data = data ?? new QuestCreatorData();

            Closable = true;
            Disposable = true;
            Dragable = true;
            Resizable = false;

            AddPage(0);

            DesenharEstrutura();

            switch (m_Step)
            {
                case 1: DesenharPasso1_Identidade(); break;
                case 2: DesenharPasso2_NPCs(); break;
                case 3: DesenharPasso3_Objetivos(); break;
                case 4: DesenharPasso4_Recompensas(); break;
                case 5: DesenharPasso5_Dialogo(); break;
                case 6: DesenharPasso6_Revisao(); break;
            }

            DesenharBarraProgresso();
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
                Cor(DOURADO, "<CENTER>━━━  CRIAR QUEST  ━━━</CENTER>"),
                false, false);

            string[] passos = { "", "Identidade", "NPCs", "Objetivos", "Recompensas", "Dialogo", "Revisao" };
            AddHtml(20, 42, GW - 40, 18,
                Cor(BRANCO, "<CENTER>Passo " + m_Step + " de 6: " + passos[m_Step] + "</CENTER>"),
                false, false);

            Linha(20, 62, GW - 40);
            Linha(20, GH - 55, GW - 40);
        }

        private void DesenharBarraProgresso()
        {
            int barX = 30;
            int barY = GH - 48;
            int barW = GW - 60;
            int barH = 12;

            int progress = (m_Step * 100) / 6;
            int fillW = (barW * progress) / 100;
            if (fillW < 1 && progress > 0) fillW = 1;

            AddImageTiled(barX, barY, barW, barH, 2702);
            AddAlphaRegion(barX, barY, barW, barH);

            if (fillW > 0)
                AddImageTiled(barX, barY, fillW, barH, 0x1404);

            string corProg = progress >= 100 ? VERDE : DOURADO;
            AddHtml(barX, barY - 18, barW, 18,
                Cor(corProg, "<CENTER>Progresso: " + progress + "%</CENTER>"),
                false, false);
        }

        // ═══════════════════════════════════════════════════════════════
        //  PASSO 1 — IDENTIDADE
        // ═══════════════════════════════════════════════════════════════

        private void DesenharPasso1_Identidade()
        {
            int y = 75;
            int lW = 140;

            Campo(y, "ID da Quest:", 0, m_Data.Id, lW); y += 34;
            Campo(y, "Nome:", 1, m_Data.Name, lW); y += 34;

            AddHtml(25, y + 3, lW, 20, Cor(DOURADO, "Descricao:"), false, false);
            AddBackground(25 + lW, y, GW - lW - 50, 80, 9270);
            AddTextEntry(30 + lW, y + 4, GW - lW - 60, 72, 0, 2, m_Data.Description ?? "");
            y += 90;

            Campo(y, "Categoria:", 3, m_Data.Category, lW); y += 34;
            Campo(y, "Dificuldade:", 4, m_Data.Difficulty, lW); y += 34;
            Campo(y, "Chain ID:", 5, m_Data.ChainId, lW); y += 34;
            Campo(y, "Chain Order:", 6, m_Data.ChainOrderStr, lW); y += 34;

            AddHtml(25, y, GW - 50, 40,
                Cor(CINZA, "<I>Categorias: Tutorial, Main, Side, Daily, Weekly, Event, Hidden<BR>" +
                    "Dificuldades: Easy, Normal, Hard, Elite, Legendary</I>"),
                false, false);

            BotaoAvancar();
        }

        // ═══════════════════════════════════════════════════════════════
        //  PASSO 2 — NPCs
        // ═══════════════════════════════════════════════════════════════

        private void DesenharPasso2_NPCs()
        {
            int y = 75;
            int lW = 160;

            AddHtml(25, y, GW - 50, 20, Cor(DOURADO, "NPC que da a quest:"), false, false);
            y += 24;
            Campo(y, "Giver NPC ID:", 10, m_Data.GiverNpcId, lW); y += 34;
            Campo(y, "Giver NPC Nome:", 11, m_Data.GiverNpcName, lW); y += 44;

            AddHtml(25, y, GW - 50, 20, Cor(DOURADO, "NPC de entrega:"), false, false);
            y += 24;
            Campo(y, "TurnIn NPC ID:", 12, m_Data.TurnInNpcId, lW); y += 34;
            Campo(y, "TurnIn NPC Nome:", 13, m_Data.TurnInNpcName, lW); y += 44;

            AddHtml(25, y, GW - 50, 20, Cor(DOURADO, "Configuracoes:"), false, false);
            y += 24;

            AddCheck(25, y, 210, 211, m_Data.Repeatable, 20);
            AddHtml(50, y + 2, 150, 18, Cor(BRANCO, "Repetivel"), false, false);

            AddCheck(200, y, 210, 211, m_Data.Shareable, 21);
            AddHtml(225, y + 2, 150, 18, Cor(BRANCO, "Compartilhavel"), false, false);

            AddCheck(400, y, 210, 211, m_Data.AutoStart, 22);
            AddHtml(425, y + 2, 150, 18, Cor(BRANCO, "Auto-iniciar"), false, false);
            y += 30;

            Campo(y, "Cooldown (horas):", 14, m_Data.CooldownStr, lW); y += 34;

            BotaoVoltar();
            BotaoAvancar();
        }

        // ═══════════════════════════════════════════════════════════════
        //  PASSO 3 — OBJETIVOS
        // ═══════════════════════════════════════════════════════════════

        private void DesenharPasso3_Objetivos()
        {
            int y = 75;

            AddHtml(25, y, GW - 50, 20,
                Cor(DOURADO, "Objetivos (" + m_Data.Objectives.Count + "):"),
                false, false);

            // Botão adicionar
            AddButton(GW - 180, y, 4005, 4007, 400, GumpButtonType.Reply, 0);
            AddHtml(GW - 145, y + 2, 140, 18, Cor(VERDE, "Adicionar objetivo"), false, false);

            y += 28;
            Linha(25, y, GW - 50);
            y += 6;

            if (m_Data.Objectives.Count == 0)
            {
                AddHtml(25, y + 60, GW - 50, 40,
                    Cor(CINZA, "<CENTER><I>Nenhum objetivo adicionado.<BR>Clique em 'Adicionar objetivo'.</I></CENTER>"),
                    false, false);
            }
            else
            {
                for (int i = 0; i < m_Data.Objectives.Count; i++)
                {
                    var obj = m_Data.Objectives[i];
                    bool par = (i % 2 == 0);

                    if (par)
                    {
                        AddImageTiled(25, y - 2, GW - 50, 55, 2702);
                        AddAlphaRegion(25, y - 2, GW - 50, 55);
                    }

                    AddHtml(30, y, 40, 18, Cor(DOURADO, "#" + (i + 1)), false, false);
                    AddHtml(55, y, 100, 18, Cor(AMARELO(), "[" + obj.Type + "]"), false, false);
                    AddHtml(160, y, GW - 260, 18, Cor(BRANCO, obj.Description ?? ""), false, false);

                    AddHtml(30, y + 20, 100, 18, Cor(CINZA, "Alvo: " + (obj.Target ?? "")), false, false);
                    AddHtml(250, y + 20, 100, 18, Cor(CINZA, "Qtd: " + obj.Required), false, false);

                    // Botão remover
                    AddButton(GW - 75, y + 5, 4017, 4019, 500 + i, GumpButtonType.Reply, 0);

                    y += 58;

                    if (y > GH - 120) break;
                }
            }

            BotaoVoltar();
            BotaoAvancar();
        }

        // ═══════════════════════════════════════════════════════════════
        //  PASSO 4 — RECOMPENSAS
        // ═══════════════════════════════════════════════════════════════

        private void DesenharPasso4_Recompensas()
        {
            int y = 75;
            int lW = 140;

            AddHtml(25, y, GW - 50, 20, Cor(DOURADO, "Recompensas:"), false, false);
            y += 28;

            Campo(y, "Ouro:", 30, m_Data.RewardGold, lW); y += 34;
            Campo(y, "Fama:", 31, m_Data.RewardFame, lW); y += 34;
            Campo(y, "Karma:", 32, m_Data.RewardKarma, lW); y += 44;

            AddHtml(25, y, GW - 50, 20, Cor(DOURADO, "Itens de recompensa:"), false, false);

            AddButton(GW - 180, y, 4005, 4007, 410, GumpButtonType.Reply, 0);
            AddHtml(GW - 145, y + 2, 140, 18, Cor(VERDE, "Adicionar item"), false, false);

            y += 28;

            for (int i = 0; i < m_Data.RewardItems.Count; i++)
            {
                var item = m_Data.RewardItems[i];

                AddHtml(35, y, GW - 110, 18,
                    Cor(BRANCO, "  • " + item.Amount + "x " + item.Type + " (" + (item.Name ?? "") + ")"),
                    false, false);

                AddButton(GW - 75, y, 4017, 4019, 600 + i, GumpButtonType.Reply, 0);
                y += 22;

                if (y > GH - 120) break;
            }

            BotaoVoltar();
            BotaoAvancar();
        }

        // ═══════════════════════════════════════════════════════════════
        //  PASSO 5 — DIÁLOGO
        // ═══════════════════════════════════════════════════════════════

        private void DesenharPasso5_Dialogo()
        {
            int y = 75;
            int lW = 140;

            AddHtml(25, y, GW - 50, 20, Cor(DOURADO, "Dialogos do NPC:"), false, false);
            y += 28;

            AddHtml(25, y + 3, lW, 20, Cor(CINZA, "Ao aceitar:"), false, false);
            AddBackground(25 + lW, y, GW - lW - 50, 60, 9270);
            AddTextEntry(30 + lW, y + 4, GW - lW - 60, 52, 0, 40, m_Data.DialogueAccept ?? "");
            y += 70;

            AddHtml(25, y + 3, lW, 20, Cor(CINZA, "Em progresso:"), false, false);
            AddBackground(25 + lW, y, GW - lW - 50, 60, 9270);
            AddTextEntry(30 + lW, y + 4, GW - lW - 60, 52, 0, 41, m_Data.DialogueProgress ?? "");
            y += 70;

            AddHtml(25, y + 3, lW, 20, Cor(CINZA, "Ao completar:"), false, false);
            AddBackground(25 + lW, y, GW - lW - 50, 60, 9270);
            AddTextEntry(30 + lW, y + 4, GW - lW - 60, 52, 0, 42, m_Data.DialogueComplete ?? "");
            y += 70;

            AddHtml(25, y, GW - 50, 40,
                Cor(CINZA, "<I>Separe multiplas falas com | (pipe).<BR>Ex: Muito bem!|Aqui esta sua recompensa!</I>"),
                false, false);

            BotaoVoltar();
            BotaoAvancar();
        }

        // ═══════════════════════════════════════════════════════════════
        //  PASSO 6 — REVISÃO E SALVAR
        // ═══════════════════════════════════════════════════════════════

        private void DesenharPasso6_Revisao()
        {
            int y = 75;

            AddHtml(25, y, GW - 50, 20,
                Cor(DOURADO, "Revisao final:"),
                false, false);
            y += 28;

            Info(y, "ID:", m_Data.Id); y += 20;
            Info(y, "Nome:", m_Data.Name); y += 20;
            Info(y, "Categoria:", m_Data.Category); y += 20;
            Info(y, "Dificuldade:", m_Data.Difficulty); y += 20;
            Info(y, "NPC Giver:", m_Data.GiverNpcName + " (" + m_Data.GiverNpcId + ")"); y += 20;
            Info(y, "Repetivel:", m_Data.Repeatable ? "Sim" : "Nao"); y += 20;
            Info(y, "Objetivos:", m_Data.Objectives.Count.ToString()); y += 20;
            Info(y, "Ouro:", m_Data.RewardGold); y += 20;
            Info(y, "Itens:", m_Data.RewardItems.Count.ToString()); y += 28;

            Linha(25, y, GW - 50);
            y += 8;

            AddHtml(25, y, GW - 50, 40,
                Cor(CINZA, "<I>Ao salvar, o arquivo JSON sera criado em Data/Quests/.<BR>" +
                    "Use [questreload para carregar a quest no servidor.</I>"),
                false, false);

            BotaoVoltar();

            // Botão SALVAR
            AddButton(GW - 180, GH - 80, 4023, 4025, 999, GumpButtonType.Reply, 0);
            AddHtml(GW - 155, GH - 78, 140, 20,
                Cor(VERDE, "✦  SALVAR QUEST"),
                false, false);
        }

        // ═══════════════════════════════════════════════════════════════
        //  RESPOSTA
        // ═══════════════════════════════════════════════════════════════

        public override void OnResponse(NetState sender, RelayInfo info)
        {
            Mobile from = sender.Mobile;
            if (from == null) return;

            SalvarCampos(info);

            int btn = info.ButtonID;

            if (btn == 0) return;

            // Navegação
            if (btn == 1) // Avançar
            {
                from.CloseGump(typeof(QuestCreatorGump));
                from.SendGump(new QuestCreatorGump(from, Math.Min(6, m_Step + 1), m_Data));
                return;
            }
            if (btn == 2) // Voltar
            {
                from.CloseGump(typeof(QuestCreatorGump));
                from.SendGump(new QuestCreatorGump(from, Math.Max(1, m_Step - 1), m_Data));
                return;
            }

            // Adicionar objetivo
            if (btn == 400)
            {
                from.CloseGump(typeof(QuestCreatorGump));
                from.SendGump(new AddObjectiveGump(from, m_Data));
                return;
            }

            // Remover objetivo
            if (btn >= 500 && btn < 600)
            {
                int idx = btn - 500;
                if (idx < m_Data.Objectives.Count)
                    m_Data.Objectives.RemoveAt(idx);

                from.CloseGump(typeof(QuestCreatorGump));
                from.SendGump(new QuestCreatorGump(from, 3, m_Data));
                return;
            }

            // Adicionar item de recompensa
            if (btn == 410)
            {
                from.CloseGump(typeof(QuestCreatorGump));
                from.SendGump(new AddRewardItemGump(from, m_Data));
                return;
            }

            // Remover item de recompensa
            if (btn >= 600 && btn < 700)
            {
                int idx = btn - 600;
                if (idx < m_Data.RewardItems.Count)
                    m_Data.RewardItems.RemoveAt(idx);

                from.CloseGump(typeof(QuestCreatorGump));
                from.SendGump(new QuestCreatorGump(from, 4, m_Data));
                return;
            }

            // Salvar
            if (btn == 999)
            {
                SalvarQuest(from);
                return;
            }
        }

        private void SalvarCampos(RelayInfo info)
        {
            switch (m_Step)
            {
                case 1:
                    m_Data.Id = GetText(info, 0, m_Data.Id);
                    m_Data.Name = GetText(info, 1, m_Data.Name);
                    m_Data.Description = GetText(info, 2, m_Data.Description);
                    m_Data.Category = GetText(info, 3, m_Data.Category);
                    m_Data.Difficulty = GetText(info, 4, m_Data.Difficulty);
                    m_Data.ChainId = GetText(info, 5, m_Data.ChainId);
                    m_Data.ChainOrderStr = GetText(info, 6, m_Data.ChainOrderStr);
                    break;

                case 2:
                    m_Data.GiverNpcId = GetText(info, 10, m_Data.GiverNpcId);
                    m_Data.GiverNpcName = GetText(info, 11, m_Data.GiverNpcName);
                    m_Data.TurnInNpcId = GetText(info, 12, m_Data.TurnInNpcId);
                    m_Data.TurnInNpcName = GetText(info, 13, m_Data.TurnInNpcName);
                    m_Data.CooldownStr = GetText(info, 14, m_Data.CooldownStr);
                    m_Data.Repeatable = info.IsSwitched(20);
                    m_Data.Shareable = info.IsSwitched(21);
                    m_Data.AutoStart = info.IsSwitched(22);
                    break;

                case 4:
                    m_Data.RewardGold = GetText(info, 30, m_Data.RewardGold);
                    m_Data.RewardFame = GetText(info, 31, m_Data.RewardFame);
                    m_Data.RewardKarma = GetText(info, 32, m_Data.RewardKarma);
                    break;

                case 5:
                    m_Data.DialogueAccept = GetText(info, 40, m_Data.DialogueAccept);
                    m_Data.DialogueProgress = GetText(info, 41, m_Data.DialogueProgress);
                    m_Data.DialogueComplete = GetText(info, 42, m_Data.DialogueComplete);
                    break;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  SALVAR JSON
        // ═══════════════════════════════════════════════════════════════

        private void SalvarQuest(Mobile from)
        {
            if (string.IsNullOrWhiteSpace(m_Data.Id))
            {
                from.SendMessage(0x22, "ID da quest nao pode ser vazio!");
                from.CloseGump(typeof(QuestCreatorGump));
                from.SendGump(new QuestCreatorGump(from, 1, m_Data));
                return;
            }

            if (string.IsNullOrWhiteSpace(m_Data.Name))
            {
                from.SendMessage(0x22, "Nome da quest nao pode ser vazio!");
                from.CloseGump(typeof(QuestCreatorGump));
                from.SendGump(new QuestCreatorGump(from, 1, m_Data));
                return;
            }

            try
            {
                string json = m_Data.ToJson();
                string path = Path.Combine(Core.BaseDirectory, "Data", "Quests");

                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);

                string fileName = m_Data.Id.Replace(" ", "_") + ".json";
                string fullPath = Path.Combine(path, fileName);

                File.WriteAllText(fullPath, json, Encoding.UTF8);

                from.SendMessage(0x40, "━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                from.SendMessage(0x40, "  Quest salva com sucesso!");
                from.SendMessage(0x40, "  Arquivo: " + fileName);
                from.SendMessage(0x40, "  Use [questreload para carregar.");
                from.SendMessage(0x40, "━━━━━━━━━━━━━━━━━━━━━━━━━━━");

                Console.WriteLine("[QuestCreator] Quest salva: " + fullPath);
            }
            catch (Exception ex)
            {
                from.SendMessage(0x22, "Erro ao salvar: " + ex.Message);
                Console.WriteLine("[QuestCreator] Erro: " + ex.Message);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  HELPERS
        // ═══════════════════════════════════════════════════════════════

        private void Campo(int y, string label, int entryId, string value, int labelW)
        {
            AddHtml(25, y + 3, labelW, 20, Cor(CINZA, label), false, false);
            AddBackground(25 + labelW, y, GW - labelW - 50, 26, 9270);
            AddTextEntry(30 + labelW, y + 3, GW - labelW - 60, 20, 0, entryId, value ?? "");
        }

        private void Info(int y, string label, string value)
        {
            AddHtml(35, y, GW - 70, 18,
                Cor(CINZA, label + " ") + Cor(BRANCO, value ?? ""),
                false, false);
        }

        private void BotaoVoltar()
        {
            if (m_Step > 1)
            {
                AddButton(25, GH - 80, 4014, 4016, 2, GumpButtonType.Reply, 0);
                AddHtml(60, GH - 78, 100, 20, Cor(CINZA, "◀  Voltar"), false, false);
            }
        }

        private void BotaoAvancar()
        {
            if (m_Step < 6)
            {
                AddButton(GW - 140, GH - 80, 4005, 4007, 1, GumpButtonType.Reply, 0);
                AddHtml(GW - 105, GH - 78, 100, 20, Cor(DOURADO, "Avancar  ▶"), false, false);
            }
        }

        private string GetText(RelayInfo info, int id, string fallback)
        {
            TextRelay relay = info.GetTextEntry(id);
            if (relay != null && !string.IsNullOrEmpty(relay.Text))
                return relay.Text;
            return fallback ?? "";
        }

        private void Linha(int x, int y, int w)
        {
            AddImageTiled(x, y, w, 2, 9304);
        }

        private static string Cor(string cor, string txt)
        {
            return String.Format("<BASEFONT COLOR={0}>{1}</BASEFONT>", cor, txt);
        }

        private string AMARELO() { return "#DAA520"; }
    }

    // ═══════════════════════════════════════════════════════════════
    //  GUMP ADICIONAR OBJETIVO
    // ═══════════════════════════════════════════════════════════════

    public class AddObjectiveGump : Gump
    {
        private Mobile m_From;
        private QuestCreatorData m_Data;

        private const string DOURADO = "#C9A227";
        private const string BRANCO = "#E5E5E5";
        private const string CINZA = "#888888";
        private const string VERDE = "#2E8B57";

        public AddObjectiveGump(Mobile from, QuestCreatorData data) : base(150, 100)
        {
            m_From = from;
            m_Data = data;

            Closable = true;
            Dragable = true;

            AddPage(0);

            AddBackground(0, 0, 450, 340, 9270);
            AddImageTiled(10, 10, 430, 320, 2702);
            AddAlphaRegion(10, 10, 430, 320);

            AddHtml(20, 15, 410, 24,
                Cor(DOURADO, "<CENTER>━━━  NOVO OBJETIVO  ━━━</CENTER>"),
                false, false);

            AddImageTiled(20, 42, 410, 2, 9304);

            int y = 52;
            int lW = 120;

            AddHtml(25, y + 3, lW, 20, Cor(CINZA, "Tipo:"), false, false);
            AddBackground(25 + lW, y, 280, 26, 9270);
            AddTextEntry(30 + lW, y + 3, 270, 20, 0, 0, "kill");
            y += 34;

            AddHtml(25, y + 3, lW, 20, Cor(CINZA, "Descricao:"), false, false);
            AddBackground(25 + lW, y, 280, 26, 9270);
            AddTextEntry(30 + lW, y + 3, 270, 20, 0, 1, "");
            y += 34;

            AddHtml(25, y + 3, lW, 20, Cor(CINZA, "Alvo (tipo):"), false, false);
            AddBackground(25 + lW, y, 280, 26, 9270);
            AddTextEntry(30 + lW, y + 3, 270, 20, 0, 2, "");
            y += 34;

            AddHtml(25, y + 3, lW, 20, Cor(CINZA, "Nome do alvo:"), false, false);
            AddBackground(25 + lW, y, 280, 26, 9270);
            AddTextEntry(30 + lW, y + 3, 270, 20, 0, 3, "");
            y += 34;

            AddHtml(25, y + 3, lW, 20, Cor(CINZA, "Quantidade:"), false, false);
            AddBackground(25 + lW, y, 100, 26, 9270);
            AddTextEntry(30 + lW, y + 3, 90, 20, 0, 4, "1");
            y += 34;

            AddHtml(25, y + 3, lW, 20, Cor(CINZA, "Dica local:"), false, false);
            AddBackground(25 + lW, y, 280, 26, 9270);
            AddTextEntry(30 + lW, y + 3, 270, 20, 0, 5, "");
            y += 40;

            AddHtml(25, y, 400, 30,
                Cor(CINZA, "<I>Tipos: kill, collect, explore, deliver, talkto, escort, craft, useskill, interact, defend</I>"),
                false, false);

            AddButton(150, 300, 4005, 4007, 1, GumpButtonType.Reply, 0);
            AddHtml(185, 302, 100, 20, Cor(VERDE, "Adicionar"), false, false);

            AddButton(300, 300, 4017, 4019, 0, GumpButtonType.Reply, 0);
            AddHtml(335, 302, 80, 20, Cor(BRANCO, "Cancelar"), false, false);
        }

        public override void OnResponse(NetState sender, RelayInfo info)
        {
            if (info.ButtonID == 1)
            {
                var obj = new QuestCreatorObjective();
                obj.Type = GetText(info, 0, "kill");
                obj.Description = GetText(info, 1, "");
                obj.Target = GetText(info, 2, "");
                obj.TargetName = GetText(info, 3, "");

                int req;
                int.TryParse(GetText(info, 4, "1"), out req);
                obj.Required = Math.Max(1, req);

                obj.LocationHint = GetText(info, 5, "");

                m_Data.Objectives.Add(obj);
            }

            m_From.CloseGump(typeof(AddObjectiveGump));
            m_From.SendGump(new QuestCreatorGump(m_From, 3, m_Data));
        }

        private string GetText(RelayInfo info, int id, string fallback)
        {
            TextRelay r = info.GetTextEntry(id);
            return (r != null && !string.IsNullOrEmpty(r.Text)) ? r.Text : fallback;
        }

        private static string Cor(string cor, string txt)
        {
            return String.Format("<BASEFONT COLOR={0}>{1}</BASEFONT>", cor, txt);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  GUMP ADICIONAR ITEM DE RECOMPENSA
    // ═══════════════════════════════════════════════════════════════

    public class AddRewardItemGump : Gump
    {
        private Mobile m_From;
        private QuestCreatorData m_Data;

        private const string DOURADO = "#C9A227";
        private const string BRANCO = "#E5E5E5";
        private const string CINZA = "#888888";
        private const string VERDE = "#2E8B57";

        public AddRewardItemGump(Mobile from, QuestCreatorData data) : base(200, 150)
        {
            m_From = from;
            m_Data = data;

            Closable = true;
            Dragable = true;

            AddPage(0);

            AddBackground(0, 0, 400, 220, 9270);
            AddImageTiled(10, 10, 380, 200, 2702);
            AddAlphaRegion(10, 10, 380, 200);

            AddHtml(20, 15, 360, 24,
                Cor(DOURADO, "<CENTER>━━━  NOVO ITEM  ━━━</CENTER>"),
                false, false);

            AddImageTiled(20, 42, 360, 2, 9304);

            int y = 52;
            int lW = 100;

            AddHtml(25, y + 3, lW, 20, Cor(CINZA, "Tipo (class):"), false, false);
            AddBackground(25 + lW, y, 250, 26, 9270);
            AddTextEntry(30 + lW, y + 3, 240, 20, 0, 0, "Gold");
            y += 34;

            AddHtml(25, y + 3, lW, 20, Cor(CINZA, "Nome:"), false, false);
            AddBackground(25 + lW, y, 250, 26, 9270);
            AddTextEntry(30 + lW, y + 3, 240, 20, 0, 1, "");
            y += 34;

            AddHtml(25, y + 3, lW, 20, Cor(CINZA, "Quantidade:"), false, false);
            AddBackground(25 + lW, y, 100, 26, 9270);
            AddTextEntry(30 + lW, y + 3, 90, 20, 0, 2, "1");
            y += 40;

            AddButton(120, 180, 4005, 4007, 1, GumpButtonType.Reply, 0);
            AddHtml(155, 182, 100, 20, Cor(VERDE, "Adicionar"), false, false);

            AddButton(260, 180, 4017, 4019, 0, GumpButtonType.Reply, 0);
            AddHtml(295, 182, 80, 20, Cor(BRANCO, "Cancelar"), false, false);
        }

        public override void OnResponse(NetState sender, RelayInfo info)
        {
            if (info.ButtonID == 1)
            {
                var item = new QuestCreatorRewardItem();
                item.Type = GetText(info, 0, "Gold");
                item.Name = GetText(info, 1, "");

                int amt;
                int.TryParse(GetText(info, 2, "1"), out amt);
                item.Amount = Math.Max(1, amt);

                m_Data.RewardItems.Add(item);
            }

            m_From.CloseGump(typeof(AddRewardItemGump));
            m_From.SendGump(new QuestCreatorGump(m_From, 4, m_Data));
        }

        private string GetText(RelayInfo info, int id, string fallback)
        {
            TextRelay r = info.GetTextEntry(id);
            return (r != null && !string.IsNullOrEmpty(r.Text)) ? r.Text : fallback;
        }

        private static string Cor(string cor, string txt)
        {
            return String.Format("<BASEFONT COLOR={0}>{1}</BASEFONT>", cor, txt);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  DADOS TEMPORÁRIOS DO CRIADOR
    // ═══════════════════════════════════════════════════════════════

    public class QuestCreatorData
    {
        // Identidade
        public string Id = "";
        public string Name = "";
        public string Description = "";
        public string Category = "Side";
        public string Difficulty = "Normal";
        public string ChainId = "";
        public string ChainOrderStr = "0";

        // NPCs
        public string GiverNpcId = "";
        public string GiverNpcName = "";
        public string TurnInNpcId = "";
        public string TurnInNpcName = "";

        // Settings
        public bool Repeatable = false;
        public bool Shareable = false;
        public bool AutoStart = false;
        public string CooldownStr = "0";

        // Objetivos
        public List<QuestCreatorObjective> Objectives = new List<QuestCreatorObjective>();

        // Recompensas
        public string RewardGold = "0";
        public string RewardFame = "0";
        public string RewardKarma = "0";
        public List<QuestCreatorRewardItem> RewardItems = new List<QuestCreatorRewardItem>();

        // Diálogo
        public string DialogueAccept = "";
        public string DialogueProgress = "";
        public string DialogueComplete = "";

        // ═══════════════════════════════════════════════════════════════
        //  GERAR JSON
        // ═══════════════════════════════════════════════════════════════

        public string ToJson()
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");

            // Identity
            sb.AppendLine("  \"identity\": {");
            sb.AppendLine("    \"id\": \"" + Esc(Id) + "\",");
            sb.AppendLine("    \"name\": \"" + Esc(Name) + "\",");
            sb.AppendLine("    \"description\": \"" + Esc(Description) + "\",");
            sb.AppendLine("    \"category\": \"" + Esc(Category) + "\",");
            sb.AppendLine("    \"difficulty\": \"" + Esc(Difficulty) + "\",");
            sb.AppendLine("    \"chainId\": \"" + Esc(ChainId) + "\",");
            sb.AppendLine("    \"chainOrder\": " + SafeInt(ChainOrderStr));
            sb.AppendLine("  },");

            // NPCs
            sb.AppendLine("  \"npcs\": {");
            sb.AppendLine("    \"giverId\": \"" + Esc(GiverNpcId) + "\",");
            sb.AppendLine("    \"giverName\": \"" + Esc(GiverNpcName) + "\",");
            sb.AppendLine("    \"turnInId\": \"" + Esc(TurnInNpcId) + "\",");
            sb.AppendLine("    \"turnInName\": \"" + Esc(TurnInNpcName) + "\"");
            sb.AppendLine("  },");

            // Settings
            sb.AppendLine("  \"settings\": {");
            sb.AppendLine("    \"repeatable\": " + (Repeatable ? "true" : "false") + ",");
            sb.AppendLine("    \"cooldownHours\": " + SafeInt(CooldownStr) + ",");
            sb.AppendLine("    \"shareableWithParty\": " + (Shareable ? "true" : "false") + ",");
            sb.AppendLine("    \"autoStart\": " + (AutoStart ? "true" : "false"));
            sb.AppendLine("  },");

            // Objectives
            sb.AppendLine("  \"objectives\": [");
            for (int i = 0; i < Objectives.Count; i++)
            {
                var obj = Objectives[i];
                sb.AppendLine("    {");
                sb.AppendLine("      \"type\": \"" + Esc(obj.Type) + "\",");
                sb.AppendLine("      \"description\": \"" + Esc(obj.Description) + "\",");
                sb.AppendLine("      \"targetType\": \"" + Esc(obj.Target) + "\",");
                sb.AppendLine("      \"targetName\": \"" + Esc(obj.TargetName) + "\",");
                sb.AppendLine("      \"required\": " + obj.Required + ",");
                sb.AppendLine("      \"locationHint\": \"" + Esc(obj.LocationHint) + "\"");
                sb.Append("    }");
                if (i < Objectives.Count - 1) sb.Append(",");
                sb.AppendLine();
            }
            sb.AppendLine("  ],");

            // Rewards
            sb.AppendLine("  \"rewards\": {");
            sb.AppendLine("    \"gold\": " + SafeInt(RewardGold) + ",");
            sb.AppendLine("    \"fame\": " + SafeInt(RewardFame) + ",");
            sb.AppendLine("    \"karma\": " + SafeInt(RewardKarma) + ",");
            sb.AppendLine("    \"items\": [");
            for (int i = 0; i < RewardItems.Count; i++)
            {
                var item = RewardItems[i];
                sb.AppendLine("      {");
                sb.AppendLine("        \"type\": \"" + Esc(item.Type) + "\",");
                sb.AppendLine("        \"name\": \"" + Esc(item.Name) + "\",");
                sb.AppendLine("        \"amount\": " + item.Amount);
                sb.Append("      }");
                if (i < RewardItems.Count - 1) sb.Append(",");
                sb.AppendLine();
            }
            sb.AppendLine("    ]");
            sb.AppendLine("  },");

            // Dialogue
            sb.AppendLine("  \"dialogue\": {");
            sb.AppendLine("    \"onAccept\": " + LinesToJsonArray(DialogueAccept) + ",");
            sb.AppendLine("    \"onProgress\": " + LinesToJsonArray(DialogueProgress) + ",");
            sb.AppendLine("    \"onComplete\": " + LinesToJsonArray(DialogueComplete));
            sb.AppendLine("  }");

            sb.AppendLine("}");

            return sb.ToString();
        }

        private string Esc(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");
        }

        private string SafeInt(string s)
        {
            int val;
            if (int.TryParse(s, out val))
                return val.ToString();
            return "0";
        }

        private string LinesToJsonArray(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "[]";

            string[] parts = text.Split('|');
            var sb = new StringBuilder();
            sb.Append("[");
            for (int i = 0; i < parts.Length; i++)
            {
                sb.Append("\"" + Esc(parts[i].Trim()) + "\"");
                if (i < parts.Length - 1) sb.Append(", ");
            }
            sb.Append("]");
            return sb.ToString();
        }
    }

    public class QuestCreatorObjective
    {
        public string Type = "kill";
        public string Description = "";
        public string Target = "";
        public string TargetName = "";
        public int Required = 1;
        public string LocationHint = "";
    }

    public class QuestCreatorRewardItem
    {
        public string Type = "Gold";
        public string Name = "";
        public int Amount = 1;
    }
}