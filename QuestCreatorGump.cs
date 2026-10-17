/*****************************************************
 * QuestCreatorGump.cs
 * Criador/EDITOR de quests em jogo
 *
 * REESCRITA COMPLETA (v2)
 * -----------------------
 * Problemas da versão anterior:
 *
 *  1. ToJson() emitia apenas type/description/targetType/targetName/required/
 *     locationHint para TODOS os objetivos. O loader, porém, lê "itemType" para
 *     Collect, "targetNpcId" para TalkTo, "skill" para UseSkill, "locationX/Y"
 *     para Explore, e assim por diante. Resultado: qualquer quest criada pelo
 *     gump — exceto as de Kill — nascia sem alvo e era IMPOSSÍVEL de concluir.
 *
 *  2. Não havia como editar uma quest existente. O GM precisava recriar tudo do
 *     zero e sobrescrever o JSON na mão.
 *
 *  3. Pré-requisitos não existiam no gump: nenhuma quest criada por ele podia
 *     exigir quest anterior, skill mínima, item, fama ou raça.
 *
 *  4. Recompensas cobriam só ouro/fama/karma e itens simples — sem skills,
 *     títulos, experiência, hue ou chance.
 *
 *  5. O gump listava categorias ("Hidden") e dificuldades ("Elite") que não
 *     existem nos enums, gerando JSON que o loader descartava.
 *
 *  6. Sem timeLimit, sem nextQuest, sem grantItems, sem dialogo de oferta/recusa.
 *
 * Agora:
 *  - cada tipo de objetivo tem seu próprio formulário com TODOS os campos que o
 *    QuestLoader lê (tabela única em ObjectiveFields — gump e JSON nunca
 *    divergem do loader);
 *  - [editarquest abre a lista de quests carregadas e carrega o JSON de volta
 *    para o formulário (QuestCreatorData.FromDefinition);
 *  - passo novo de Pré-requisitos (quests, level, skills, itens, fama, karma,
 *    facção, guilda, raças);
 *  - recompensas completas (ouro, fama, karma, exp, itens com hue/chance,
 *    skills com cap, títulos);
 *  - validação antes de salvar, apontando exatamente o que tornaria a quest
 *    impossível.
 *
 * Comandos: [criarquest  [editarquest
 *
 * Versão: 2.0.0
 * Compatível com: ServUO
 *****************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Globalization;
using Server;
using Server.Commands;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;
using Server.Custom.QuestSystem.Quests;

namespace Server.Gumps
{
    // ═══════════════════════════════════════════════════════════════════════
    //  GUMP PRINCIPAL (assistente de 7 passos)
    // ═══════════════════════════════════════════════════════════════════════

    public class QuestCreatorGump : Gump
    {
        private readonly Mobile m_From;
        private readonly int m_Step;
        private readonly QuestCreatorData m_Data;

        private const int GW = 720;
        private const int GH = 600;
        private const int TOTAL_STEPS = 7;

        private const string DOURADO = "#C9A227";
        private const string BRANCO = "#E5E5E5";
        private const string CINZA = "#888888";
        private const string VERDE = "#2E8B57";
        private const string VERMELHO = "#8B1A1A";
        private const string AZUL = "#4A90D9";

        private static readonly string[] PASSOS =
            { "", "Identidade", "NPCs", "Objetivos", "Pre-requisitos", "Recompensas", "Dialogo", "Revisao" };

        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized)
                return;

            _initialized = true;

            CommandSystem.Register("criarquest", AccessLevel.GameMaster, OnCreateCommand);
            CommandSystem.Register("editarquest", AccessLevel.GameMaster, OnEditCommand);

            Console.WriteLine("[QuestCreator] Comandos [criarquest e [editarquest registrados");
        }

        private static void OnCreateCommand(CommandEventArgs e)
        {
            e.Mobile.CloseGump(typeof(QuestCreatorGump));
            e.Mobile.SendGump(new QuestCreatorGump(e.Mobile, 1, new QuestCreatorData()));
        }

        private static void OnEditCommand(CommandEventArgs e)
        {
            e.Mobile.CloseGump(typeof(QuestCreatorGump));
            e.Mobile.CloseGump(typeof(QuestEditListGump));

            string arg = e.ArgString != null ? e.ArgString.Trim() : "";

            if (arg.Length > 0)
            {
                var definition = QuestRepository.Instance.GetQuest(arg);

                if (definition == null)
                {
                    e.Mobile.SendMessage(0x26, "Quest '" + arg + "' nao encontrada. Use [questlist");
                    return;
                }

                OpenForEdit(e.Mobile, definition);
                return;
            }

            e.Mobile.SendGump(new QuestEditListGump(e.Mobile, 0));
        }

        /// <summary>Abre o assistente já preenchido com uma quest existente.</summary>
        internal static void OpenForEdit(Mobile from, QuestDefinition definition)
        {
            if (from == null || definition == null)
                return;

            var data = QuestCreatorData.FromDefinition(definition);

            from.SendMessage(0x3B1, "Editando '" + definition.Id + "'. Salvar vai sobrescrever o arquivo de origem.");

            from.CloseGump(typeof(QuestCreatorGump));
            from.CloseGump(typeof(QuestEditListGump));
            from.SendGump(new QuestCreatorGump(from, 1, data));
        }

        public QuestCreatorGump(Mobile from, int step, QuestCreatorData data) : base(50, 20)
        {
            m_From = from;
            m_Step = Math.Max(1, Math.Min(TOTAL_STEPS, step));
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
                case 4: DesenharPasso4_PreRequisitos(); break;
                case 5: DesenharPasso5_Recompensas(); break;
                case 6: DesenharPasso6_Dialogo(); break;
                case 7: DesenharPasso7_Revisao(); break;
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

            string titulo = m_Data.IsEditing
                ? "EDITAR QUEST"
                : "CRIAR QUEST";

            AddHtml(20, 15, GW - 40, 24,
                Cor(DOURADO, "<CENTER>━━━  " + titulo + "  ━━━</CENTER>"),
                false, false);

            AddHtml(20, 42, GW - 40, 18,
                Cor(BRANCO, "<CENTER>Passo " + m_Step + " de " + TOTAL_STEPS + ": " + PASSOS[m_Step] + "</CENTER>"),
                false, false);

            Linha(20, 62, GW - 40);
            Linha(20, GH - 58, GW - 40);
        }

        private void DesenharBarraProgresso()
        {
            int barX = 30;
            int barY = GH - 48;
            int barW = GW - 60;
            int barH = 10;

            int progress = (m_Step * 100) / TOTAL_STEPS;
            int fillW = (barW * progress) / 100;
            if (fillW < 1 && progress > 0) fillW = 1;

            AddImageTiled(barX, barY, barW, barH, 2702);
            AddAlphaRegion(barX, barY, barW, barH);

            if (fillW > 0)
                AddImageTiled(barX, barY, fillW, barH, 0x1404);

            string corProg = progress >= 100 ? VERDE : DOURADO;

            AddHtml(barX, barY - 16, barW, 16,
                Cor(corProg, "<CENTER>" + progress + "%</CENTER>"),
                false, false);
        }

        // ═══════════════════════════════════════════════════════════════
        //  PASSO 1 — IDENTIDADE
        // ═══════════════════════════════════════════════════════════════

        private void DesenharPasso1_Identidade()
        {
            int y = 72;
            int lW = 150;

            if (m_Data.IsEditing)
            {
                AddHtml(25, y, GW - 50, 18,
                    Cor(AZUL, "Modo EDICAO: arquivo '" + Esc(m_Data.SourceFile) + "'"), false, false);
                y += 20;
            }

            Campo(y, "ID da Quest:", 0, m_Data.Id, lW, m_Data.IsEditing); y += 32;
            Campo(y, "Nome:", 1, m_Data.Name, lW); y += 32;

            Area(y, "Descricao:", 2, m_Data.Description, lW, 66); y += 74;

            Campo(y, "Categoria:", 3, m_Data.Category, lW); y += 32;
            Campo(y, "Dificuldade:", 4, m_Data.Difficulty, lW); y += 32;
            Campo(y, "Chain ID:", 5, m_Data.ChainId, lW); y += 32;
            Campo(y, "Chain Order:", 6, m_Data.ChainOrderStr, lW); y += 32;
            Campo(y, "Proxima quest (id):", 7, m_Data.NextQuestId, lW); y += 32;

            AddHtml(25, y, GW - 50, 44,
                Cor(CINZA, "<I>Categorias validas: Main, Side, Daily, Weekly, Event, Guild, Tutorial, Profession<BR>" +
                    "Dificuldades validas: Trivial, Easy, Normal, Hard, VeryHard, Epic, Legendary<BR>" +
                    "'Proxima quest' liga esta ao id de outra, formando uma cadeia.</I>"),
                false, false);

            BotaoAvancar();
        }

        // ═══════════════════════════════════════════════════════════════
        //  PASSO 2 — NPCs E CONFIGURAÇÕES
        // ═══════════════════════════════════════════════════════════════

        private void DesenharPasso2_NPCs()
        {
            int y = 72;
            int lW = 170;

            AddHtml(25, y, GW - 50, 18, Cor(DOURADO, "NPC que oferece a quest:"), false, false);
            y += 20;
            Campo(y, "Giver NPC ID:", 10, m_Data.GiverNpcId, lW); y += 32;
            Campo(y, "Giver NPC Nome:", 11, m_Data.GiverNpcName, lW); y += 38;

            AddHtml(25, y, GW - 50, 18, Cor(DOURADO, "NPC que recebe a entrega:"), false, false);
            y += 20;
            Campo(y, "TurnIn NPC ID:", 12, m_Data.TurnInNpcId, lW); y += 32;
            Campo(y, "TurnIn NPC Nome:", 13, m_Data.TurnInNpcName, lW); y += 38;

            AddHtml(25, y, GW - 50, 18, Cor(DOURADO, "Configuracoes:"), false, false);
            y += 22;

            Check(25, y, 20, m_Data.Repeatable, "Repetivel");
            Check(215, y, 21, m_Data.Shareable, "Compartilhavel");
            Check(430, y, 22, m_Data.AutoStart, "Auto-iniciar");
            y += 26;

            Campo(y, "Cooldown (horas):", 14, m_Data.CooldownStr, lW); y += 32;
            Campo(y, "Max. conclusoes:", 15, m_Data.MaxCompletionsStr, lW); y += 38;

            AddHtml(25, y, GW - 50, 18, Cor(DOURADO, "Limite de tempo:"), false, false);
            y += 20;

            Check(25, y, 23, m_Data.TimeLimitEnabled, "Ativo");
            Campo(y + 22, "Minutos:", 16, m_Data.TimeLimitMinutesStr, 170); y += 54;
            Campo(y, "Ao expirar:", 17, m_Data.OnTimeExpire, 170); y += 32;
            Campo(y, "Avisos (min, |):", 18, m_Data.TimeWarningsStr, 170); y += 34;

            AddHtml(25, y, GW - 50, 30,
                Cor(CINZA, "<I>IDs de NPC: use os mesmos dos JSON (ex.: npc_capita_maelis).<BR>" +
                    "Vincule com [questnpcset npcid &lt;id&gt; no NPC. Ao expirar: Fail, Reset ou Nothing.<BR>" +
                    "Avisos: '30|10|5' avisa quando faltarem 30, 10 e 5 minutos.</I>"),
                false, false);

            BotaoVoltar();
            BotaoAvancar();
        }

        // ═══════════════════════════════════════════════════════════════
        //  PASSO 3 — OBJETIVOS
        // ═══════════════════════════════════════════════════════════════

        private void DesenharPasso3_Objetivos()
        {
            int y = 72;

            AddHtml(25, y, 400, 18,
                Cor(DOURADO, "Objetivos (" + m_Data.Objectives.Count + "):"), false, false);

            AddButton(GW - 190, y - 2, 4005, 4007, 400, GumpButtonType.Reply, 0);
            AddHtml(GW - 155, y, 150, 18, Cor(VERDE, "Adicionar objetivo"), false, false);

            y += 24;

            Check(25, y, 24, m_Data.SequentialObjectives, "Sequencial (um objetivo libera o proximo)");
            y += 26;
            Linha(25, y, GW - 50);
            y += 6;

            if (m_Data.Objectives.Count == 0)
            {
                AddHtml(25, y + 50, GW - 50, 40,
                    Cor(CINZA, "<CENTER><I>Nenhum objetivo adicionado.<BR>Clique em 'Adicionar objetivo'.</I></CENTER>"),
                    false, false);
            }
            else
            {
                for (int i = 0; i < m_Data.Objectives.Count; i++)
                {
                    var obj = m_Data.Objectives[i];

                    if (i % 2 == 0)
                    {
                        AddImageTiled(25, y - 2, GW - 50, 54, 2702);
                        AddAlphaRegion(25, y - 2, GW - 50, 54);
                    }

                    AddHtml(30, y, 34, 18, Cor(DOURADO, "#" + (i + 1)), false, false);
                    AddHtml(62, y, 96, 18, Cor(AZUL, "[" + obj.Type + "]"), false, false);
                    AddHtml(160, y, GW - 300, 18, Cor(BRANCO, Trunc(obj.Description, 62)), false, false);

                    AddHtml(30, y + 20, GW - 200, 18,
                        Cor(CINZA, Trunc(obj.Summary(), 88)), false, false);

                    if (obj.IsOptional)
                        AddHtml(GW - 200, y + 20, 90, 18, Cor(VERMELHO, "opcional"), false, false);

                    // editar
                    AddButton(GW - 100, y + 4, 4011, 4013, 550 + i, GumpButtonType.Reply, 0);
                    // remover
                    AddButton(GW - 70, y + 4, 4017, 4019, 500 + i, GumpButtonType.Reply, 0);

                    y += 56;

                    if (y > GH - 130)
                    {
                        AddHtml(25, y, GW - 50, 18,
                            Cor(CINZA, "<I>... e mais " + (m_Data.Objectives.Count - i - 1) + " objetivo(s).</I>"),
                            false, false);
                        break;
                    }
                }
            }

            BotaoVoltar();
            BotaoAvancar();
        }

        // ═══════════════════════════════════════════════════════════════
        //  PASSO 4 — PRÉ-REQUISITOS
        // ═══════════════════════════════════════════════════════════════

        private void DesenharPasso4_PreRequisitos()
        {
            int y = 72;
            int lW = 175;

            AddHtml(25, y, GW - 50, 18,
                Cor(DOURADO, "Quem pode aceitar esta quest:"), false, false);
            y += 24;

            Area(y, "Quests exigidas:", 30, m_Data.RequiredQuestsStr, lW, 44); y += 52;

            // Numerico curto: dois por linha para devolver a altura que o campo
            // de FailMessage consumiu (senao as listas de skills/itens invadiam
            // os botoes de Voltar/Avancar).
            int metade = (GW - 60) / 2;

            CampoEm(25, y, metade, "Level minimo:", 31, m_Data.MinLevelStr, 120);
            CampoEm(35 + metade, y, metade, "Fama minima:", 32, m_Data.MinFameStr, 120);
            y += 32;

            CampoEm(25, y, metade, "Karma minimo:", 33, m_Data.MinKarmaStr, 120);
            CampoEm(35 + metade, y, metade, "Karma maximo:", 34, m_Data.MaxKarmaStr, 120);
            y += 32;

            Campo(y, "Faccao:", 35, m_Data.RequiredFaction, lW); y += 32;
            Campo(y, "Guilda:", 36, m_Data.RequiredGuild, lW); y += 32;
            Campo(y, "Racas permitidas:", 37, m_Data.AllowedRacesStr, lW); y += 32;

            // FailMessage estava no modelo, era emitido no JSON e recarregado na
            // edicao, mas nao havia campo para defini-lo ao CRIAR a quest — ou
            // seja, "todos os pre-requisitos pelo gump" nao era verdade. E justo
            // esta mensagem que o jogador ve quando nao satisfaz a condicao.
            Campo(y, "Msg. quando recusado:", 38, m_Data.FailMessage, lW); y += 38;

            AddHtml(25, y, GW - 50, 18, Cor(DOURADO, "Skills minimas:"), false, false);

            AddButton(GW - 190, y - 2, 4005, 4007, 700, GumpButtonType.Reply, 0);
            AddHtml(GW - 155, y, 150, 18, Cor(VERDE, "Adicionar skill"), false, false);
            y += 24;

            for (int i = 0; i < m_Data.RequiredSkills.Count; i++)
            {
                var req = m_Data.RequiredSkills[i];

                AddHtml(35, y, GW - 130, 18,
                    Cor(BRANCO, "  " + req.SkillName + " >= " + req.Value), false, false);

                AddButton(GW - 70, y, 4017, 4019, 750 + i, GumpButtonType.Reply, 0);
                y += 22;
            }

            y += 4;

            AddHtml(25, y, GW - 50, 18, Cor(DOURADO, "Itens exigidos no inventario:"), false, false);

            AddButton(GW - 190, y - 2, 4005, 4007, 800, GumpButtonType.Reply, 0);
            AddHtml(GW - 155, y, 150, 18, Cor(VERDE, "Adicionar item"), false, false);
            y += 24;

            for (int i = 0; i < m_Data.RequiredItems.Count; i++)
            {
                var req = m_Data.RequiredItems[i];

                AddHtml(35, y, GW - 130, 18,
                    Cor(BRANCO, "  " + req.Amount + "x " + req.Type +
                        (string.IsNullOrEmpty(req.Name) ? "" : " (" + req.Name + ")")), false, false);

                AddButton(GW - 70, y, 4017, 4019, 850 + i, GumpButtonType.Reply, 0);
                y += 22;
            }

            BotaoVoltar();
            BotaoAvancar();
        }

        // ═══════════════════════════════════════════════════════════════
        //  PASSO 5 — RECOMPENSAS
        // ═══════════════════════════════════════════════════════════════

        private void DesenharPasso5_Recompensas()
        {
            int y = 72;
            int lW = 150;

            Campo(y, "Ouro:", 40, m_Data.RewardGold, lW); y += 32;
            Campo(y, "Fama:", 41, m_Data.RewardFame, lW); y += 32;
            Campo(y, "Karma:", 42, m_Data.RewardKarma, lW); y += 32;
            Campo(y, "Experiencia:", 43, m_Data.RewardExperience, lW); y += 30;

            Check(25, y, 44, m_Data.BankDeposit, "Depositar ouro no banco");
            y += 30;

            AddHtml(25, y, GW - 50, 18, Cor(DOURADO, "Itens:"), false, false);
            AddButton(GW - 190, y - 2, 4005, 4007, 410, GumpButtonType.Reply, 0);
            AddHtml(GW - 155, y, 150, 18, Cor(VERDE, "Adicionar item"), false, false);
            y += 22;

            y = ListaSimples(y, m_Data.RewardItems.Count, 600,
                i => m_Data.RewardItems[i].Amount + "x " + m_Data.RewardItems[i].Type +
                     (string.IsNullOrEmpty(m_Data.RewardItems[i].Name) ? "" : " (" + m_Data.RewardItems[i].Name + ")") +
                     (m_Data.RewardItems[i].Hue > 0 ? " hue " + m_Data.RewardItems[i].Hue : "") +
                     (m_Data.RewardItems[i].Chance < 100 ? " chance " + m_Data.RewardItems[i].Chance + "%" : ""));

            y += 4;

            AddHtml(25, y, GW - 50, 18, Cor(DOURADO, "Ganho de skills:"), false, false);
            AddButton(GW - 190, y - 2, 4005, 4007, 420, GumpButtonType.Reply, 0);
            AddHtml(GW - 155, y, 150, 18, Cor(VERDE, "Adicionar skill"), false, false);
            y += 22;

            y = ListaSimples(y, m_Data.RewardSkills.Count, 650,
                i => m_Data.RewardSkills[i].SkillName + " +" + m_Data.RewardSkills[i].Amount +
                     (m_Data.RewardSkills[i].CapIncrease > 0 ? " (cap +" + m_Data.RewardSkills[i].CapIncrease + ")" : ""));

            y += 4;

            Campo(y, "Titulos (separar por |):", 45, m_Data.RewardTitlesStr, lW); y += 34;

            AddHtml(25, y, GW - 50, 26,
                Cor(CINZA, "<I>Tipo do item: classe do ServUO (Gold, Longsword, Bandage) ou nome de item<BR>" +
                    "virtual de quest (SeloFortaleza) — o sistema cria o item automaticamente.</I>"),
                false, false);

            BotaoVoltar();
            BotaoAvancar();
        }

        private int ListaSimples(int y, int count, int buttonBase, Func<int, string> formatter)
        {
            for (int i = 0; i < count; i++)
            {
                AddHtml(35, y, GW - 130, 18, Cor(BRANCO, "  " + Trunc(formatter(i), 84)), false, false);
                AddButton(GW - 70, y, 4017, 4019, buttonBase + i, GumpButtonType.Reply, 0);
                y += 22;

                if (y > GH - 130)
                    break;
            }

            return y;
        }

        // ═══════════════════════════════════════════════════════════════
        //  PASSO 6 — DIÁLOGO
        // ═══════════════════════════════════════════════════════════════

        private void DesenharPasso6_Dialogo()
        {
            int y = 72;
            int lW = 140;

            Area(y, "Ao oferecer:", 50, m_Data.DialogueOffer, lW, 50); y += 58;
            Area(y, "Ao aceitar:", 51, m_Data.DialogueAccept, lW, 50); y += 58;
            Area(y, "Ao recusar:", 52, m_Data.DialogueDecline, lW, 50); y += 58;
            Area(y, "Em progresso:", 53, m_Data.DialogueProgress, lW, 50); y += 58;
            Area(y, "Ao completar:", 54, m_Data.DialogueComplete, lW, 50); y += 58;

            Campo(y, "Mensagem final:", 55, m_Data.CompletionMessage, lW); y += 34;

            AddHtml(25, y, GW - 50, 40,
                Cor(CINZA, "<I>Separe multiplas falas com | (pipe).<BR>" +
                    "Ex: Muito bem!|Aqui esta sua recompensa!<BR>" +
                    "Marcadores: {playerName} e {questName} sao substituidos em jogo.</I>"),
                false, false);

            BotaoVoltar();
            BotaoAvancar();
        }

        // ═══════════════════════════════════════════════════════════════
        //  PASSO 7 — REVISÃO E SALVAR
        // ═══════════════════════════════════════════════════════════════

        private void DesenharPasso7_Revisao()
        {
            int y = 72;

            Info(y, "ID:", m_Data.Id); y += 18;
            Info(y, "Nome:", m_Data.Name); y += 18;
            Info(y, "Categoria / Dificuldade:", m_Data.Category + " / " + m_Data.Difficulty); y += 18;
            Info(y, "Giver:", m_Data.GiverNpcName + " (" + m_Data.GiverNpcId + ")"); y += 18;
            Info(y, "Turn-in:", m_Data.TurnInNpcName + " (" + m_Data.TurnInNpcId + ")"); y += 18;
            Info(y, "Repetivel:", m_Data.Repeatable ? "Sim" : "Nao"); y += 18;
            Info(y, "Objetivos:", m_Data.Objectives.Count.ToString()); y += 18;
            Info(y, "Pre-requisitos:", m_Data.PrerequisitesSummary()); y += 18;
            Info(y, "Recompensas:", m_Data.RewardsSummary()); y += 18;

            if (m_Data.TimeLimitEnabled)
            {
                Info(y, "Limite de tempo:", m_Data.TimeLimitMinutesStr + " min (" + m_Data.OnTimeExpire + ")");
                y += 18;
            }

            if (!string.IsNullOrWhiteSpace(m_Data.NextQuestId))
            {
                Info(y, "Proxima quest:", m_Data.NextQuestId); y += 18;
            }

            y += 6;
            Linha(25, y, GW - 50);
            y += 8;

            // Validação: mostra o que tornaria a quest impossível
            var problems = m_Data.Validate();

            if (problems.Count == 0)
            {
                AddHtml(25, y, GW - 50, 20,
                    Cor(VERDE, "Validacao: nenhum problema encontrado."), false, false);
                y += 22;
            }
            else
            {
                AddHtml(25, y, GW - 50, 20,
                    Cor(VERMELHO, "Validacao: " + problems.Count + " problema(s):"), false, false);
                y += 20;

                for (int i = 0; i < problems.Count && y < GH - 130; i++)
                {
                    AddHtml(35, y, GW - 70, 18, Cor(VERMELHO, "• " + Trunc(problems[i], 92)), false, false);
                    y += 18;
                }
            }

            AddHtml(25, y, GW - 50, 34,
                Cor(CINZA, "<I>O JSON sera gravado em Data/Quests/.<BR>" +
                    "Depois rode [questreload para carregar no servidor.</I>"),
                false, false);

            BotaoVoltar();

            AddButton(GW - 190, GH - 100, 4023, 4025, 999, GumpButtonType.Reply, 0);
            AddHtml(GW - 165, GH - 98, 160, 20,
                Cor(VERDE, m_Data.IsEditing ? "✦  SALVAR ALTERACOES" : "✦  SALVAR QUEST"),
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
            if (btn == 1)
            {
                Reabrir(from, Math.Min(TOTAL_STEPS, m_Step + 1));
                return;
            }

            if (btn == 2)
            {
                Reabrir(from, Math.Max(1, m_Step - 1));
                return;
            }

            // Adicionar objetivo
            if (btn == 400)
            {
                from.CloseGump(typeof(QuestCreatorGump));
                from.SendGump(new ObjectiveTypeGump(from, m_Data));
                return;
            }

            // Remover objetivo
            if (btn >= 500 && btn < 550)
            {
                int idx = btn - 500;
                if (idx < m_Data.Objectives.Count)
                    m_Data.Objectives.RemoveAt(idx);

                Reabrir(from, 3);
                return;
            }

            // Editar objetivo
            if (btn >= 550 && btn < 600)
            {
                int idx = btn - 550;
                if (idx < m_Data.Objectives.Count)
                {
                    from.CloseGump(typeof(QuestCreatorGump));
                    from.SendGump(new AddObjectiveGump(from, m_Data, m_Data.Objectives[idx], idx));
                }
                return;
            }

            // Remover item de recompensa
            if (btn >= 600 && btn < 650)
            {
                int idx = btn - 600;
                if (idx < m_Data.RewardItems.Count)
                    m_Data.RewardItems.RemoveAt(idx);

                Reabrir(from, 5);
                return;
            }

            // Remover skill de recompensa
            if (btn >= 650 && btn < 700)
            {
                int idx = btn - 650;
                if (idx < m_Data.RewardSkills.Count)
                    m_Data.RewardSkills.RemoveAt(idx);

                Reabrir(from, 5);
                return;
            }

            // Adicionar skill de pré-requisito
            if (btn == 700)
            {
                from.CloseGump(typeof(QuestCreatorGump));
                from.SendGump(new AddPrereqSkillGump(from, m_Data));
                return;
            }

            // Remover skill de pré-requisito
            if (btn >= 750 && btn < 800)
            {
                int idx = btn - 750;
                if (idx < m_Data.RequiredSkills.Count)
                    m_Data.RequiredSkills.RemoveAt(idx);

                Reabrir(from, 4);
                return;
            }

            // Adicionar item de pré-requisito
            if (btn == 800)
            {
                from.CloseGump(typeof(QuestCreatorGump));
                from.SendGump(new AddPrereqItemGump(from, m_Data));
                return;
            }

            // Remover item de pré-requisito
            if (btn >= 850 && btn < 900)
            {
                int idx = btn - 850;
                if (idx < m_Data.RequiredItems.Count)
                    m_Data.RequiredItems.RemoveAt(idx);

                Reabrir(from, 4);
                return;
            }

            // Adicionar item de recompensa
            if (btn == 410)
            {
                from.CloseGump(typeof(QuestCreatorGump));
                from.SendGump(new AddRewardItemGump(from, m_Data));
                return;
            }

            // Adicionar skill de recompensa
            if (btn == 420)
            {
                from.CloseGump(typeof(QuestCreatorGump));
                from.SendGump(new AddRewardSkillGump(from, m_Data));
                return;
            }

            // Salvar
            if (btn == 999)
            {
                SalvarQuest(from);
                return;
            }
        }

        private void Reabrir(Mobile from, int step)
        {
            from.CloseGump(typeof(QuestCreatorGump));
            from.SendGump(new QuestCreatorGump(from, step, m_Data));
        }

        private void SalvarCampos(RelayInfo info)
        {
            switch (m_Step)
            {
                case 1:
                    if (!m_Data.IsEditing)
                        m_Data.Id = GetText(info, 0, m_Data.Id);

                    m_Data.Name = GetText(info, 1, m_Data.Name);
                    m_Data.Description = GetText(info, 2, m_Data.Description);
                    m_Data.Category = GetText(info, 3, m_Data.Category);
                    m_Data.Difficulty = GetText(info, 4, m_Data.Difficulty);
                    m_Data.ChainId = GetText(info, 5, m_Data.ChainId);
                    m_Data.ChainOrderStr = GetText(info, 6, m_Data.ChainOrderStr);
                    m_Data.NextQuestId = GetText(info, 7, m_Data.NextQuestId);
                    break;

                case 2:
                    m_Data.GiverNpcId = GetText(info, 10, m_Data.GiverNpcId);
                    m_Data.GiverNpcName = GetText(info, 11, m_Data.GiverNpcName);
                    m_Data.TurnInNpcId = GetText(info, 12, m_Data.TurnInNpcId);
                    m_Data.TurnInNpcName = GetText(info, 13, m_Data.TurnInNpcName);
                    m_Data.CooldownStr = GetText(info, 14, m_Data.CooldownStr);
                    m_Data.MaxCompletionsStr = GetText(info, 15, m_Data.MaxCompletionsStr);
                    m_Data.TimeLimitMinutesStr = GetText(info, 16, m_Data.TimeLimitMinutesStr);
                    m_Data.OnTimeExpire = GetText(info, 17, m_Data.OnTimeExpire);
                    m_Data.TimeWarningsStr = GetText(info, 18, m_Data.TimeWarningsStr);
                    m_Data.Repeatable = info.IsSwitched(20);
                    m_Data.Shareable = info.IsSwitched(21);
                    m_Data.AutoStart = info.IsSwitched(22);
                    m_Data.TimeLimitEnabled = info.IsSwitched(23);
                    break;

                case 3:
                    m_Data.SequentialObjectives = info.IsSwitched(24);
                    break;

                case 4:
                    m_Data.RequiredQuestsStr = GetText(info, 30, m_Data.RequiredQuestsStr);
                    m_Data.MinLevelStr = GetText(info, 31, m_Data.MinLevelStr);
                    m_Data.MinFameStr = GetText(info, 32, m_Data.MinFameStr);
                    m_Data.MinKarmaStr = GetText(info, 33, m_Data.MinKarmaStr);
                    m_Data.MaxKarmaStr = GetText(info, 34, m_Data.MaxKarmaStr);
                    m_Data.RequiredFaction = GetText(info, 35, m_Data.RequiredFaction);
                    m_Data.RequiredGuild = GetText(info, 36, m_Data.RequiredGuild);
                    m_Data.AllowedRacesStr = GetText(info, 37, m_Data.AllowedRacesStr);
                    m_Data.FailMessage = GetText(info, 38, m_Data.FailMessage);
                    break;

                case 5:
                    m_Data.RewardGold = GetText(info, 40, m_Data.RewardGold);
                    m_Data.RewardFame = GetText(info, 41, m_Data.RewardFame);
                    m_Data.RewardKarma = GetText(info, 42, m_Data.RewardKarma);
                    m_Data.RewardExperience = GetText(info, 43, m_Data.RewardExperience);
                    m_Data.RewardTitlesStr = GetText(info, 45, m_Data.RewardTitlesStr);
                    m_Data.BankDeposit = info.IsSwitched(44);
                    break;

                case 6:
                    m_Data.DialogueOffer = GetText(info, 50, m_Data.DialogueOffer);
                    m_Data.DialogueAccept = GetText(info, 51, m_Data.DialogueAccept);
                    m_Data.DialogueDecline = GetText(info, 52, m_Data.DialogueDecline);
                    m_Data.DialogueProgress = GetText(info, 53, m_Data.DialogueProgress);
                    m_Data.DialogueComplete = GetText(info, 54, m_Data.DialogueComplete);
                    m_Data.CompletionMessage = GetText(info, 55, m_Data.CompletionMessage);
                    break;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  SALVAR
        // ═══════════════════════════════════════════════════════════════

        private void SalvarQuest(Mobile from)
        {
            if (string.IsNullOrWhiteSpace(m_Data.Id))
            {
                from.SendMessage(0x22, "ID da quest nao pode ser vazio!");
                Reabrir(from, 1);
                return;
            }

            if (string.IsNullOrWhiteSpace(m_Data.Name))
            {
                from.SendMessage(0x22, "Nome da quest nao pode ser vazio!");
                Reabrir(from, 1);
                return;
            }

            if (m_Data.Objectives.Count == 0)
            {
                from.SendMessage(0x22, "A quest precisa de ao menos um objetivo, senao nao pode ser concluida.");
                Reabrir(from, 3);
                return;
            }

            var problems = m_Data.Validate();

            if (problems.Count > 0)
            {
                from.SendMessage(0x26, "Ha " + problems.Count + " problema(s) que impedem a conclusao:");

                for (int i = 0; i < problems.Count && i < 8; i++)
                    from.SendMessage(0x26, " - " + problems[i]);

                from.SendMessage(0x26, "Corrija antes de salvar (a quest seria impossivel de concluir).");
                Reabrir(from, 7);
                return;
            }

            try
            {
                string json = m_Data.ToJson();
                string path = QuestLoader.QuestsPath;

                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);

                string fileName = !string.IsNullOrWhiteSpace(m_Data.SourceFile)
                    ? m_Data.SourceFile
                    : SanitizeFileName(m_Data.Id) + ".json";

                string fullPath = Path.Combine(path, fileName);

                if (File.Exists(fullPath))
                    File.Copy(fullPath, fullPath + ".bak", true);

                File.WriteAllText(fullPath, json, Encoding.UTF8);

                from.SendMessage(0x40, "━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                from.SendMessage(0x40, m_Data.IsEditing ? "  Quest atualizada!" : "  Quest salva!");
                from.SendMessage(0x40, "  Arquivo: " + fileName);
                from.SendMessage(0x40, "  Rode [questreload para carregar.");
                from.SendMessage(0x40, "━━━━━━━━━━━━━━━━━━━━━━━━━━━");

                Console.WriteLine("[QuestCreator] Quest gravada: " + fullPath);

                from.CloseGump(typeof(QuestCreatorGump));
            }
            catch (Exception ex)
            {
                from.SendMessage(0x22, "Erro ao salvar: " + ex.Message);
                Console.WriteLine("[QuestCreator] Erro: " + ex);
            }
        }

        private static string SanitizeFileName(string id)
        {
            var sb = new StringBuilder(id.Length);

            foreach (char c in id)
            {
                if (char.IsLetterOrDigit(c) || c == '_' || c == '-')
                    sb.Append(c);
                else
                    sb.Append('_');
            }

            return sb.Length > 0 ? sb.ToString() : "quest";
        }

        // ═══════════════════════════════════════════════════════════════
        //  HELPERS DE DESENHO
        // ═══════════════════════════════════════════════════════════════

        private void Campo(int y, string label, int entryId, string value, int labelW)
        {
            Campo(y, label, entryId, value, labelW, false);
        }

        private void Campo(int y, string label, int entryId, string value, int labelW, bool readOnly)
        {
            AddHtml(25, y + 3, labelW, 20, Cor(readOnly ? VERMELHO : CINZA, label), false, false);
            AddBackground(25 + labelW, y, GW - labelW - 50, 26, 9270);

            if (readOnly)
            {
                // Editar o ID de uma quest existente quebraria o histórico dos
                // jogadores que já a completaram.
                AddHtml(30 + labelW, y + 4, GW - labelW - 60, 20,
                    Cor(VERMELHO, Esc(value) + "  (imutavel em edicao)"), false, false);
            }
            else
            {
                AddTextEntry(30 + labelW, y + 3, GW - labelW - 60, 20, 0, entryId, value ?? "");
            }
        }

        /// <summary>
        /// Campo em posição e largura explícitas, para colocar dois campos curtos
        /// lado a lado na mesma linha.
        ///
        /// Existe por causa de altura: o passo 4 ganhou o campo de FailMessage e,
        /// com tudo em linha própria, sobrava ~1 entrada para as listas de skills
        /// e itens exigidos — o GM que adicionasse 2 skills via a lista invadir os
        /// botões. Level/Fama e Karma mín/máx são numéricos curtos e cabem bem
        /// dois por linha, o que devolve a folga.
        /// </summary>
        private void CampoEm(int x, int y, int largura, string label, int entryId, string value, int labelW)
        {
            AddHtml(x, y + 3, labelW, 20, Cor(CINZA, label), false, false);
            AddBackground(x + labelW, y, largura - labelW, 26, 9270);
            AddTextEntry(x + labelW + 5, y + 3, largura - labelW - 12, 20, 0, entryId, value ?? "");
        }

        private void Area(int y, string label, int entryId, string value, int labelW, int height)
        {
            AddHtml(25, y + 3, labelW, 20, Cor(CINZA, label), false, false);
            AddBackground(25 + labelW, y, GW - labelW - 50, height, 9270);
            AddTextEntry(30 + labelW, y + 4, GW - labelW - 60, height - 8, 0, entryId, value ?? "");
        }

        private void Check(int x, int y, int switchId, bool value, string label)
        {
            AddCheck(x, y, 210, 211, value, switchId);
            AddHtml(x + 25, y + 1, GW - x - 60, 18, Cor(BRANCO, label), false, false);
        }

        private void Info(int y, string label, string value)
        {
            AddHtml(35, y, GW - 70, 18,
                Cor(CINZA, label + " ") + Cor(BRANCO, Trunc(value ?? "", 78)),
                false, false);
        }

        private void BotaoVoltar()
        {
            if (m_Step > 1)
            {
                AddButton(25, GH - 98, 4014, 4016, 2, GumpButtonType.Reply, 0);
                AddHtml(60, GH - 96, 100, 20, Cor(CINZA, "◀  Voltar"), false, false);
            }
        }

        private void BotaoAvancar()
        {
            if (m_Step < TOTAL_STEPS)
            {
                AddButton(GW - 140, GH - 98, 4005, 4007, 1, GumpButtonType.Reply, 0);
                AddHtml(GW - 105, GH - 96, 100, 20, Cor(DOURADO, "Avancar  ▶"), false, false);
            }
        }

        private string GetText(RelayInfo info, int id, string fallback)
        {
            TextRelay relay = info.GetTextEntry(id);

            // GetText pode retornar null quando o campo não existe no gump
            // (ex.: ID bloqueado em modo edição). Nesses casos preserva o valor.
            if (relay == null)
                return fallback ?? "";

            return relay.Text ?? "";
        }

        private void Linha(int x, int y, int w)
        {
            AddImageTiled(x, y, w, 2, 9304);
        }

        internal static string Cor(string cor, string txt)
        {
            return "<BASEFONT COLOR=" + cor + ">" + txt + "</BASEFONT>";
        }

        private static string Trunc(string s, int max)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= max)
                return s;

            return s.Substring(0, Math.Max(0, max - 3)) + "...";
        }

        private static string Esc(string s)
        {
            if (string.IsNullOrEmpty(s))
                return "";

            return s.Replace("<", "&lt;").Replace(">", "&gt;");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  LISTA DE QUESTS PARA EDITAR
    // ═══════════════════════════════════════════════════════════════════════

    public class QuestEditListGump : Gump
    {
        private const int PAGE_SIZE = 14;

        private readonly Mobile m_From;
        private readonly int m_Page;
        private List<QuestDefinition> m_Quests;

        public QuestEditListGump(Mobile from, int page) : base(80, 60)
        {
            m_From = from;
            m_Page = Math.Max(0, page);

            Closable = true;
            Dragable = true;

            m_Quests = QuestRepository.Instance.GetAllQuests() ?? new List<QuestDefinition>();

            int total = m_Quests.Count;
            int pages = Math.Max(1, (total + PAGE_SIZE - 1) / PAGE_SIZE);

            if (m_Page >= pages)
                m_Page = pages - 1;

            int w = 620;
            int h = 130 + PAGE_SIZE * 22 + 40;

            AddPage(0);
            AddBackground(0, 0, w, h, 9270);
            AddImageTiled(10, 10, w - 20, h - 20, 2702);
            AddAlphaRegion(10, 10, w - 20, h - 20);

            AddHtml(20, 16, w - 40, 22,
                QuestCreatorGump.Cor("#C9A227", "<CENTER>━━━  EDITAR QUEST  ━━━</CENTER>"), false, false);

            AddHtml(20, 42, w - 40, 18,
                QuestCreatorGump.Cor("#888888",
                    "<CENTER>" + total + " quest(s) carregadas — pagina " + (m_Page + 1) + "/" + pages + "</CENTER>"),
                false, false);

            AddHtml(20, 62, w - 40, 34,
                QuestCreatorGump.Cor("#888888",
                    "<I>Clique numa quest para abri-la no editor. O arquivo JSON original e<BR>" +
                    "recarregado no formulario e sobrescrito ao salvar (com .bak).</I>"), false, false);

            AddImageTiled(20, 100, w - 40, 2, 9304);

            int y = 110;

            if (total == 0)
            {
                AddHtml(20, y + 20, w - 40, 40,
                    QuestCreatorGump.Cor("#8B1A1A",
                        "<CENTER>Nenhuma quest carregada.<BR>Rode [questreload ou crie uma com [criarquest</CENTER>"),
                    false, false);
            }
            else
            {
                int start = m_Page * PAGE_SIZE;
                int end = Math.Min(total, start + PAGE_SIZE);

                for (int i = start; i < end; i++)
                {
                    var quest = m_Quests[i];
                    if (quest == null) continue;

                    int row = i - start;

                    if (row % 2 == 0)
                    {
                        AddImageTiled(25, y - 2, w - 50, 22, 2702);
                        AddAlphaRegion(25, y - 2, w - 50, 22);
                    }

                    AddHtml(30, y, 250, 18,
                        QuestCreatorGump.Cor("#E5E5E5", Trunc(quest.Id, 38)), false, false);

                    AddHtml(285, y, 220, 18,
                        QuestCreatorGump.Cor("#C9A227", Trunc(quest.Name, 32)), false, false);

                    AddHtml(510, y, 60, 18,
                        QuestCreatorGump.Cor("#888888",
                            (quest.Objectives != null ? quest.Objectives.Count : 0) + " obj"), false, false);

                    AddButton(w - 60, y, 4011, 4013, 100 + i, GumpButtonType.Reply, 0);

                    y += 22;
                }
            }

            if (m_Page > 0)
            {
                AddButton(25, h - 44, 4014, 4016, 1, GumpButtonType.Reply, 0);
                AddHtml(60, h - 42, 100, 18, QuestCreatorGump.Cor("#888888", "◀ Anterior"), false, false);
            }

            if (m_Page + 1 < pages)
            {
                AddButton(w - 130, h - 44, 4005, 4007, 2, GumpButtonType.Reply, 0);
                AddHtml(w - 95, h - 42, 100, 18, QuestCreatorGump.Cor("#888888", "Proxima ▶"), false, false);
            }

            AddButton(w / 2 - 60, h - 44, 4005, 4007, 3, GumpButtonType.Reply, 0);
            AddHtml(w / 2 - 25, h - 42, 120, 18,
                QuestCreatorGump.Cor("#2E8B57", "Criar nova quest"), false, false);
        }

        public override void OnResponse(NetState sender, RelayInfo info)
        {
            int btn = info.ButtonID;

            if (btn == 0) return;

            if (btn == 1)
            {
                m_From.CloseGump(typeof(QuestEditListGump));
                m_From.SendGump(new QuestEditListGump(m_From, m_Page - 1));
                return;
            }

            if (btn == 2)
            {
                m_From.CloseGump(typeof(QuestEditListGump));
                m_From.SendGump(new QuestEditListGump(m_From, m_Page + 1));
                return;
            }

            if (btn == 3)
            {
                m_From.CloseGump(typeof(QuestEditListGump));
                m_From.CloseGump(typeof(QuestCreatorGump));
                m_From.SendGump(new QuestCreatorGump(m_From, 1, new QuestCreatorData()));
                return;
            }

            if (btn >= 100 && btn < 100 + m_Quests.Count)
            {
                var quest = m_Quests[btn - 100];

                m_From.CloseGump(typeof(QuestEditListGump));
                QuestCreatorGump.OpenForEdit(m_From, quest);
            }
        }

        private static string Trunc(string s, int max)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= max)
                return s ?? "";

            return s.Substring(0, Math.Max(0, max - 3)) + "...";
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  TABELA DE CAMPOS POR TIPO DE OBJETIVO
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Fonte única de verdade dos campos de cada tipo de objetivo.
    ///
    /// O gump desenha a partir daqui e o JSON é emitido a partir daqui, usando
    /// EXATAMENTE as chaves que o QuestLoader lê. Antes, o gump tinha 6 campos
    /// fixos e emitia "targetType" para tudo, enquanto o loader procurava
    /// "itemType" (Collect), "targetNpcId" (TalkTo), "skill" (UseSkill)... —
    /// daí toda quest criada em jogo nascia impossível de concluir.
    /// </summary>
    public static class ObjectiveFields
    {
        public const string TEXT = "text";
        public const string INT = "int";
        public const string NUM = "num";
        public const string BOOL = "bool";
        public const string GRANT = "grant";

        public class Field
        {
            public string Key;
            public string Label;
            public string Kind;
            public string Hint;
            public string Default;

            public Field(string key, string label, string kind, string defaultValue, string hint)
            {
                Key = key;
                Label = label;
                Kind = kind;
                Default = defaultValue;
                Hint = hint;
            }
        }

        private static readonly Dictionary<string, Field[]> _table =
            new Dictionary<string, Field[]>(StringComparer.OrdinalIgnoreCase)
            {
                { "kill", new[]
                    {
                        new Field("targetType", "Classe do mob", TEXT, "", "Ex.: Rabbit, GreyWolf, Skeleton"),
                        new Field("targetName", "Nome exibido", TEXT, "", "Casa bosses renomeados"),
                        new Field("targetTypes", "Classes extras", TEXT, "", "Separadas por virgula"),
                        new Field("requiredRegion", "Regiao exigida", TEXT, "", "Vazio = qualquer lugar"),
                        new Field("countPetKills", "Conta kill de pet", BOOL, "false", ""),
                        new Field("countPartyKills", "Conta kill da party", BOOL, "false", "")
                    } },

                { "collect", new[]
                    {
                        new Field("itemType", "Classe do item", TEXT, "", "Ex.: Bandage, IronOre, Apple"),
                        new Field("itemName", "Nome exibido", TEXT, "", "Ex.: Atadura"),
                        new Field("required", "Quantidade", INT, "1", ""),
                        new Field("itemHue", "Hue (cor)", INT, "0", "0 = qualquer"),
                        new Field("consumeOnComplete", "Consome ao concluir", BOOL, "true", ""),
                        new Field("markAsQuestItem", "Marca como item de quest", BOOL, "false", ""),
                        new Field("fromMobTypes", "Dropa de (classes)", TEXT, "", "Ex.: GreyWolf,Skeleton"),
                        new Field("dropChance", "Chance de drop (%)", INT, "100", "So vale com 'Dropa de'")
                    } },

                { "deliver", new[]
                    {
                        new Field("itemType", "Classe do item", TEXT, "", ""),
                        new Field("itemName", "Nome exibido", TEXT, "", ""),
                        new Field("targetNpcId", "NPC que recebe (id)", TEXT, "", "Ex.: npc_capita_maelis"),
                        new Field("targetNpcName", "NPC que recebe (nome)", TEXT, "", ""),
                        new Field("giveItemOnAccept", "Da o item ao aceitar", BOOL, "true", ""),
                        new Field("itemHue", "Hue (cor)", INT, "0", ""),
                        new Field("onItemLostAction", "Se perder o item", TEXT, "Fail", "Fail ou Nothing"),
                        new Field("required", "Quantidade", INT, "1", "")
                    } },

                { "talkto", new[]
                    {
                        new Field("targetNpcId", "NPC (id)", TEXT, "", "Ex.: npc_mestre_docas_olin"),
                        new Field("targetNpcName", "NPC (nome)", TEXT, "", "Ex.: Mestre de Docas Olin"),
                        new Field("speechTrigger", "Palavra exigida", TEXT, "", "Vazio = qualquer fala"),
                        new Field("grantItems", "Itens concedidos", GRANT, "", "Tipo|Nome|Qtd separados por ;")
                    } },

                { "escort", new[]
                    {
                        new Field("escortNpcName", "Nome do escoltado", TEXT, "", "Ex.: Ferreiro Dorik"),
                        new Field("escortNpcId", "Id do escoltado", TEXT, "", ""),
                        new Field("destinationRegion", "Regiao de destino", TEXT, "", "Ex.: Britain, Cove"),
                        new Field("destinationName", "Nome do destino", TEXT, "", "Texto mostrado ao jogador"),
                        new Field("destinationX", "X do destino", INT, "0", "0 = usar a regiao"),
                        new Field("destinationY", "Y do destino", INT, "0", ""),
                        new Field("destinationZ", "Z do destino", INT, "0", ""),
                        new Field("destinationMap", "Mapa do destino", TEXT, "", "Trammel/Felucca/Malas..."),
                        new Field("radiusToComplete", "Raio de chegada", INT, "5", ""),
                        new Field("spawnOnAccept", "Cria NPC ao aceitar", BOOL, "true", ""),
                        new Field("spawnX", "X do spawn", INT, "0", "0 = local do jogador"),
                        new Field("spawnY", "Y do spawn", INT, "0", ""),
                        new Field("spawnZ", "Z do spawn", INT, "0", ""),
                        new Field("spawnMap", "Mapa do spawn", TEXT, "", ""),
                        new Field("escortBody", "Body", INT, "0x190", "0x190 homem / 0x191 mulher"),
                        new Field("escortHue", "Hue", INT, "0", "0 = aleatorio"),
                        new Field("escortHits", "Vida", INT, "0", "0 = padrao (120-150)"),
                        new Field("defendsItself", "Revida ataques", BOOL, "false", ""),
                        new Field("teleportWhenStuck", "Resgata se travar", BOOL, "true", "Evita quest impossivel"),
                        new Field("maxDistanceFromOwner", "Dist. max. do jogador", INT, "24", ""),
                        new Field("stuckTimeoutSeconds", "Segundos p/ travar", INT, "20", ""),
                        new Field("onEscortDead", "Se o NPC morrer", TEXT, "FailQuest", "FailQuest/Respawn/ResetObjective/Nothing")
                    } },

                { "defend", new[]
                    {
                        new Field("enemyType", "Classe do inimigo", TEXT, "", "Ex.: Skeleton, Orc"),
                        new Field("enemiesPerWave", "Inimigos por onda", INT, "3", ""),
                        new Field("totalWaves", "Total de ondas", INT, "1", ""),
                        new Field("timeBetweenWavesSeconds", "Segundos entre ondas", INT, "30", ""),
                        new Field("locationName", "Nome do local", TEXT, "", ""),
                        new Field("locationX", "X do local", INT, "0", "0 = local do jogador"),
                        new Field("locationY", "Y do local", INT, "0", ""),
                        new Field("mapName", "Mapa", TEXT, "", ""),
                        new Field("radiusTiles", "Raio", INT, "12", ""),
                        new Field("required", "Abates necessarios", INT, "0", "0 = ondas x inimigos")
                    } },

                { "destroy", new[]
                    {
                        new Field("targetType", "Classe do objeto", TEXT, "", "Ex.: WoodenDoor, IronGate"),
                        new Field("targetName", "Nome exibido", TEXT, "", ""),
                        new Field("targetSerials", "Seriais especificos", TEXT, "", "Separados por virgula"),
                        new Field("respawnOnFail", "Recria se falhar", BOOL, "false", ""),
                        new Field("required", "Quantidade", INT, "1", "")
                    } },

                { "explore", new[]
                    {
                        new Field("locationName", "Nome do local", TEXT, "", "Ex.: Mina Abandonada"),
                        new Field("locationX", "X", INT, "0", ""),
                        new Field("locationY", "Y", INT, "0", ""),
                        new Field("locationZ", "Z", INT, "0", ""),
                        new Field("mapName", "Mapa", TEXT, "", ""),
                        new Field("radiusTiles", "Raio (tiles)", INT, "12", ""),
                        new Field("stayDurationSeconds", "Permanencia (s)", INT, "0", "0 = so chegar"),
                        new Field("triggerOnEnter", "Dispara ao entrar", BOOL, "true", "")
                    } },

                { "craft", new[]
                    {
                        new Field("itemType", "Classe do item", TEXT, "", "Ex.: Longsword, Shirt"),
                        new Field("itemName", "Nome exibido", TEXT, "", ""),
                        new Field("craftSkill", "Oficio", TEXT, "", "Ex.: Blacksmith, Tailoring"),
                        new Field("requireExceptional", "Exige excepcional", BOOL, "false", ""),
                        new Field("consumeOnComplete", "Consome ao concluir", BOOL, "false", ""),
                        new Field("required", "Quantidade", INT, "1", "")
                    } },

                { "useskill", new[]
                    {
                        new Field("skill", "Skill", TEXT, "", "Ex.: Stealing, Snooping, Lockpicking"),
                        new Field("requireSuccess", "So conta se acertar", BOOL, "false", ""),
                        new Field("required", "Quantidade", INT, "1", "")
                    } },

                { "skillgain", new[]
                    {
                        new Field("skill", "Skill", TEXT, "", "Ex.: Stealing"),
                        new Field("targetValue", "Valor alvo", NUM, "30.0", ""),
                        new Field("startValue", "Valor inicial", NUM, "0", "0 = valor atual"),
                        new Field("mode", "Modo", TEXT, "ReachValue", "ReachValue ou GainAmount"),
                        new Field("required", "Quantidade", INT, "1", "")
                    } },

                { "interact", new[]
                    {
                        new Field("targetType", "Classe do objeto", TEXT, "", ""),
                        new Field("targetName", "Nome exibido", TEXT, "", ""),
                        new Field("targetSerials", "Seriais especificos", TEXT, "", "Separados por virgula"),
                        new Field("interactionType", "Tipo de interacao", TEXT, "DoubleClick", "DoubleClick ou Speech"),
                        new Field("speechTrigger", "Palavra exigida", TEXT, "", ""),
                        new Field("required", "Quantidade", INT, "1", "")
                    } },

                { "useitem", new[]
                    {
                        new Field("itemType", "Classe do item", TEXT, "", "Ex.: Bandage, HealPotion"),
                        new Field("itemName", "Nome exibido", TEXT, "", ""),
                        new Field("itemHue", "Hue (cor)", INT, "0", ""),
                        new Field("consumeOnUse", "Consome ao usar", BOOL, "false", ""),
                        new Field("required", "Quantidade", INT, "1", "")
                    } },

                { "survive", new[]
                    {
                        new Field("durationSeconds", "Duracao (segundos)", INT, "60", ""),
                        new Field("requiredRegion", "Regiao exigida", TEXT, "", "Vazio = qualquer"),
                        new Field("failOnDeath", "Falha se morrer", BOOL, "true", "")
                    } }
            };

        /// <summary>Tipos disponíveis, na ordem de exibição.</summary>
        public static readonly string[] Types =
            {
                "kill", "collect", "deliver", "talkto", "escort", "defend",
                "destroy", "explore", "craft", "useskill", "skillgain",
                "interact", "useitem", "survive"
            };

        public static readonly string[] TypeLabels =
            {
                "Kill — matar criaturas",
                "Collect — coletar itens",
                "Deliver — entregar item a NPC",
                "TalkTo — falar com NPC",
                "Escort — escoltar NPC ate um local",
                "Defend — defender local de ondas",
                "Destroy — destruir objeto/estrutura",
                "Explore — visitar/permanecer num local",
                "Craft — fabricar item",
                "UseSkill — usar skill N vezes",
                "SkillGain — elevar skill ate um valor",
                "Interact — interagir com objeto",
                "UseItem — usar um item",
                "Survive — sobreviver por um tempo"
            };

        /// <summary>Campos comuns a todos os objetivos.</summary>
        public static readonly Field[] Common =
            {
                new Field("description", "Descricao", TEXT, "", "Texto mostrado no rastreador"),
                new Field("locationHint", "Dica de local", TEXT, "", ""),
                new Field("optional", "Opcional", BOOL, "false", "Nao bloqueia a conclusao"),
                new Field("hidden", "Oculto", BOOL, "false", "Nao aparece no rastreador")
            };

        public static Field[] GetFields(string type)
        {
            Field[] fields;
            if (type != null && _table.TryGetValue(type, out fields))
                return fields;

            return new Field[0];
        }

        public static string DefaultFor(string type, string key)
        {
            foreach (Field f in GetFields(type))
            {
                if (f.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
                    return f.Default;
            }

            foreach (Field f in Common)
            {
                if (f.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
                    return f.Default;
            }

            return "";
        }

        /// <summary>
        /// Nome do campo principal de alvo de cada tipo — usado na validação e
        /// no resumo exibido na lista de objetivos.
        /// </summary>
        public static string PrimaryTargetKey(string type)
        {
            switch ((type ?? "").ToLowerInvariant())
            {
                case "kill": case "destroy": case "interact": return "targetType";
                case "collect": case "deliver": case "craft": case "useitem": return "itemType";
                case "talkto": return "targetNpcId";
                case "escort": return "destinationRegion";
                case "defend": return "enemyType";
                case "explore": return "locationName";
                case "useskill": case "skillgain": return "skill";
                case "survive": return "durationSeconds";
                default: return "";
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  ESCOLHA DO TIPO DE OBJETIVO
    // ═══════════════════════════════════════════════════════════════════════

    public class ObjectiveTypeGump : Gump
    {
        private readonly Mobile m_From;
        private readonly QuestCreatorData m_Data;

        public ObjectiveTypeGump(Mobile from, QuestCreatorData data) : base(140, 90)
        {
            m_From = from;
            m_Data = data;

            Closable = true;
            Dragable = true;

            int w = 430;
            int h = 96 + ObjectiveFields.Types.Length * 24 + 40;

            AddPage(0);
            AddBackground(0, 0, w, h, 9270);
            AddImageTiled(10, 10, w - 20, h - 20, 2702);
            AddAlphaRegion(10, 10, w - 20, h - 20);

            AddHtml(20, 16, w - 40, 22,
                QuestCreatorGump.Cor("#C9A227", "<CENTER>━━━  TIPO DE OBJETIVO  ━━━</CENTER>"), false, false);

            AddImageTiled(20, 42, w - 40, 2, 9304);

            AddHtml(20, 50, w - 40, 34,
                QuestCreatorGump.Cor("#888888",
                    "<I>Cada tipo tem seu proprio formulario, com todos os campos<BR>" +
                    "que o carregador de JSON reconhece.</I>"), false, false);

            int y = 90;

            for (int i = 0; i < ObjectiveFields.Types.Length; i++)
            {
                AddButton(25, y, 4005, 4007, 100 + i, GumpButtonType.Reply, 0);
                AddHtml(60, y + 2, w - 90, 18,
                    QuestCreatorGump.Cor("#E5E5E5", ObjectiveFields.TypeLabels[i]), false, false);
                y += 24;
            }

            AddButton(w - 130, h - 38, 4017, 4019, 0, GumpButtonType.Reply, 0);
            AddHtml(w - 95, h - 36, 90, 18,
                QuestCreatorGump.Cor("#888888", "Cancelar"), false, false);
        }

        public override void OnResponse(NetState sender, RelayInfo info)
        {
            int btn = info.ButtonID;

            if (btn < 100 || btn >= 100 + ObjectiveFields.Types.Length)
            {
                m_From.CloseGump(typeof(ObjectiveTypeGump));
                m_From.SendGump(new QuestCreatorGump(m_From, 3, m_Data));
                return;
            }

            string type = ObjectiveFields.Types[btn - 100];

            var objective = new QuestCreatorObjective(type);

            m_From.CloseGump(typeof(ObjectiveTypeGump));
            m_From.SendGump(new AddObjectiveGump(m_From, m_Data, objective, -1));
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  FORMULÁRIO DE OBJETIVO
    // ═══════════════════════════════════════════════════════════════════════

    public class AddObjectiveGump : Gump
    {
        private readonly Mobile m_From;
        private readonly QuestCreatorData m_Data;
        private readonly QuestCreatorObjective m_Objective;
        private readonly int m_Index;
        private readonly int m_Scroll;

        private const int W = 560;
        private const int VISIBLE = 430;

        public AddObjectiveGump(Mobile from, QuestCreatorData data, QuestCreatorObjective objective, int index)
            : this(from, data, objective, index, 0)
        {
        }

        public AddObjectiveGump(Mobile from, QuestCreatorData data, QuestCreatorObjective objective, int index, int scroll)
            : base(120, 80)
        {
            m_From = from;
            m_Data = data;
            m_Objective = objective;
            m_Index = index;
            m_Scroll = Math.Max(0, scroll);

            Closable = true;
            Dragable = true;

            var all = BuildFieldList(objective);

            int h = VISIBLE + 90;

            AddPage(0);
            AddBackground(0, 0, W, h, 9270);
            AddImageTiled(10, 10, W - 20, h - 20, 2702);
            AddAlphaRegion(10, 10, W - 20, h - 20);

            bool editando = index >= 0;

            AddHtml(20, 16, W - 40, 22,
                QuestCreatorGump.Cor("#C9A227",
                    "<CENTER>━━━  " + (editando ? "EDITAR" : "NOVO") + " OBJETIVO: " +
                    objective.Type.ToUpperInvariant() + "  ━━━</CENTER>"), false, false);

            AddImageTiled(20, 42, W - 40, 2, 9304);

            AddHtml(20, 48, W - 40, 18,
                QuestCreatorGump.Cor("#4A90D9", HintFor(objective.Type)), false, false);

            int y = 74;
            int lW = 185;
            int first = Math.Min(m_Scroll, Math.Max(0, all.Count - 1));

            for (int i = first; i < all.Count; i++)
            {
                if (y > VISIBLE + 40)
                    break;

                var f = all[i];
                string current = objective.Get(f.Key);

                if (string.IsNullOrEmpty(current))
                    current = f.Default;

                if (f.Kind == ObjectiveFields.BOOL)
                {
                    bool on = current.Equals("true", StringComparison.OrdinalIgnoreCase) || current == "1";

                    AddCheck(25, y + 2, 210, 211, on, 5000 + i);
                    AddHtml(52, y + 2, lW, 18, QuestCreatorGump.Cor("#888888", f.Label), false, false);
                }
                else
                {
                    AddHtml(25, y + 3, lW, 20, QuestCreatorGump.Cor("#888888", f.Label), false, false);
                    AddBackground(25 + lW, y, W - lW - 50, 24, 9270);
                    AddTextEntry(30 + lW, y + 3, W - lW - 60, 18, 0, 1000 + i, current);
                }

                if (!string.IsNullOrEmpty(f.Hint))
                {
                    AddHtml(25 + lW, y + 24, W - lW - 50, 14,
                        QuestCreatorGump.Cor("#666666", "<I>" + f.Hint + "</I>"), false, false);
                    y += 14;
                }

                y += 30;
            }

            if (m_Scroll > 0)
            {
                AddButton(25, h - 40, 4014, 4016, 3, GumpButtonType.Reply, 0);
                AddHtml(60, h - 38, 90, 18, QuestCreatorGump.Cor("#888888", "◀ Campos"), false, false);
            }

            if (y > VISIBLE + 40)
            {
                AddButton(135, h - 40, 4005, 4007, 4, GumpButtonType.Reply, 0);
                AddHtml(170, h - 38, 110, 18, QuestCreatorGump.Cor("#888888", "Mais campos ▶"), false, false);
            }

            AddButton(W - 260, h - 40, 4005, 4007, 1, GumpButtonType.Reply, 0);
            AddHtml(W - 225, h - 38, 100, 20,
                QuestCreatorGump.Cor("#2E8B57", editando ? "Atualizar" : "Adicionar"), false, false);

            AddButton(W - 130, h - 40, 4017, 4019, 0, GumpButtonType.Reply, 0);
            AddHtml(W - 95, h - 38, 90, 18, QuestCreatorGump.Cor("#888888", "Cancelar"), false, false);

            m_FieldList = all;
            m_First = first;
        }

        private List<ObjectiveFields.Field> m_FieldList;
        private int m_First;

        /// <summary>Campos comuns primeiro, depois os do tipo.</summary>
        private static List<ObjectiveFields.Field> BuildFieldList(QuestCreatorObjective objective)
        {
            var list = new List<ObjectiveFields.Field>();

            // "required" fica junto dos comuns para todos os tipos
            list.Add(new ObjectiveFields.Field("required", "Quantidade necessaria", ObjectiveFields.INT, "1", ""));
            list.AddRange(ObjectiveFields.Common);
            list.AddRange(ObjectiveFields.GetFields(objective.Type));

            return list;
        }

        private static string HintFor(string type)
        {
            switch ((type ?? "").ToLowerInvariant())
            {
                case "escort":
                    return "Informe a regiao de destino OU as coordenadas. Sem destino a escolta nunca termina.";
                case "talkto":
                    return "O NPC precisa estar vinculado com [questnpcset npcid &lt;id&gt;.";
                case "collect":
                    return "Itens virtuais (que nao existem no ServUO) sao criados automaticamente pelo sistema.";
                case "defend":
                    return "As ondas sao geradas no local indicado; o progresso e o total de abates.";
                case "survive":
                    return "Conta segundos vivo (e dentro da regiao, se informada).";
                default:
                    return "Preencha ao menos o alvo principal, senao o objetivo nunca progride.";
            }
        }

        public override void OnResponse(NetState sender, RelayInfo info)
        {
            int btn = info.ButtonID;

            if (btn == 3)
            {
                Reabrir(Math.Max(0, m_Scroll - 8));
                return;
            }

            if (btn == 4)
            {
                Reabrir(m_Scroll + 8);
                return;
            }

            if (btn != 1)
            {
                m_From.CloseGump(typeof(AddObjectiveGump));
                m_From.SendGump(new QuestCreatorGump(m_From, 3, m_Data));
                return;
            }

            // Lê os campos visíveis desta página
            for (int i = 0; i < m_FieldList.Count; i++)
            {
                if (i < m_First)
                    continue;

                var f = m_FieldList[i];

                if (f.Kind == ObjectiveFields.BOOL)
                {
                    m_Objective.Set(f.Key, info.IsSwitched(5000 + i) ? "true" : "false");
                    continue;
                }

                TextRelay relay = info.GetTextEntry(1000 + i);

                if (relay == null)
                    continue;

                string value = relay.Text;

                if (value != null)
                    value = value.Trim();

                if (string.IsNullOrEmpty(value) || value == f.Default)
                    m_Objective.Fields.Remove(f.Key);
                else
                    m_Objective.Set(f.Key, value);
            }

            if (m_Index >= 0 && m_Index < m_Data.Objectives.Count)
                m_Data.Objectives[m_Index] = m_Objective;
            else
                m_Data.Objectives.Add(m_Objective);

            m_From.CloseGump(typeof(AddObjectiveGump));
            m_From.SendGump(new QuestCreatorGump(m_From, 3, m_Data));
        }

        private void Reabrir(int scroll)
        {
            m_From.CloseGump(typeof(AddObjectiveGump));
            m_From.SendGump(new AddObjectiveGump(m_From, m_Data, m_Objective, m_Index, scroll));
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  HELPERS COMPARTILHADOS DOS SUB-GUMPS
    // ═══════════════════════════════════════════════════════════════════════

    internal static class GumpForms
    {
        public static void Campo(Gump g, int w, int y, string label, int id, string value, int lW)
        {
            g.AddHtml(25, y + 3, lW, 20, QuestCreatorGump.Cor("#888888", label), false, false);
            g.AddBackground(25 + lW, y, w - lW - 50, 26, 9270);
            g.AddTextEntry(30 + lW, y + 3, w - lW - 60, 20, 0, id, value ?? "");
        }

        public static string GetText(RelayInfo info, int id, string fallback)
        {
            TextRelay r = info.GetTextEntry(id);

            if (r == null || string.IsNullOrEmpty(r.Text))
                return fallback ?? "";

            return r.Text.Trim();
        }

        public static int ParseInt(string s, int fallback)
        {
            int v;
            return int.TryParse(s, out v) ? v : fallback;
        }

        public static double ParseDouble(string s, double fallback)
        {
            double v;
            return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : fallback;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  RECOMPENSAS
    // ═══════════════════════════════════════════════════════════════════════

    public class AddRewardItemGump : Gump
    {
        private readonly Mobile m_From;
        private readonly QuestCreatorData m_Data;

        private const int W = 440;
        private const int H = 300;

        public AddRewardItemGump(Mobile from, QuestCreatorData data) : base(200, 150)
        {
            m_From = from;
            m_Data = data;

            Closable = true;
            Dragable = true;

            AddPage(0);
            AddBackground(0, 0, W, H, 9270);
            AddImageTiled(10, 10, W - 20, H - 20, 2702);
            AddAlphaRegion(10, 10, W - 20, H - 20);

            AddHtml(20, 16, W - 40, 22,
                QuestCreatorGump.Cor("#C9A227", "<CENTER>━━━  ITEM DE RECOMPENSA  ━━━</CENTER>"), false, false);
            AddImageTiled(20, 42, W - 40, 2, 9304);

            int y = 54, lW = 135;

            GumpForms.Campo(this, W, y, "Tipo (classe):", 0, "Gold", lW); y += 32;
            GumpForms.Campo(this, W, y, "Nome exibido:", 1, "", lW); y += 32;
            GumpForms.Campo(this, W, y, "Quantidade:", 2, "1", lW); y += 32;
            GumpForms.Campo(this, W, y, "Hue (0 = normal):", 3, "0", lW); y += 32;
            GumpForms.Campo(this, W, y, "Chance (%):", 4, "100", lW); y += 40;

            AddHtml(25, y, W - 50, 46,
                QuestCreatorGump.Cor("#888888",
                    "<I>Use uma classe real do ServUO (Gold, Longsword, Bandage) ou um nome de<BR>" +
                    "item virtual de quest (SeloFortaleza, CartaLacradaOlin) — o sistema cria o<BR>" +
                    "item automaticamente quando a classe nao existe.</I>"), false, false);

            AddButton(W - 250, H - 42, 4005, 4007, 1, GumpButtonType.Reply, 0);
            AddHtml(W - 215, H - 40, 100, 20, QuestCreatorGump.Cor("#2E8B57", "Adicionar"), false, false);

            AddButton(W - 120, H - 42, 4017, 4019, 0, GumpButtonType.Reply, 0);
            AddHtml(W - 85, H - 40, 80, 18, QuestCreatorGump.Cor("#888888", "Cancelar"), false, false);
        }

        public override void OnResponse(NetState sender, RelayInfo info)
        {
            if (info.ButtonID == 1)
            {
                var item = new QuestCreatorRewardItem
                {
                    Type = GumpForms.GetText(info, 0, "Gold"),
                    Name = GumpForms.GetText(info, 1, ""),
                    Amount = Math.Max(1, GumpForms.ParseInt(GumpForms.GetText(info, 2, "1"), 1)),
                    Hue = GumpForms.ParseInt(GumpForms.GetText(info, 3, "0"), 0),
                    Chance = GumpForms.ParseInt(GumpForms.GetText(info, 4, "100"), 100)
                };

                if (item.Chance <= 0 || item.Chance > 100)
                    item.Chance = 100;

                if (!string.IsNullOrWhiteSpace(item.Type))
                    m_Data.RewardItems.Add(item);
            }

            m_From.CloseGump(typeof(AddRewardItemGump));
            m_From.SendGump(new QuestCreatorGump(m_From, 5, m_Data));
        }
    }

    public class AddRewardSkillGump : Gump
    {
        private readonly Mobile m_From;
        private readonly QuestCreatorData m_Data;

        private const int W = 440;
        private const int H = 250;

        public AddRewardSkillGump(Mobile from, QuestCreatorData data) : base(200, 170)
        {
            m_From = from;
            m_Data = data;

            Closable = true;
            Dragable = true;

            AddPage(0);
            AddBackground(0, 0, W, H, 9270);
            AddImageTiled(10, 10, W - 20, H - 20, 2702);
            AddAlphaRegion(10, 10, W - 20, H - 20);

            AddHtml(20, 16, W - 40, 22,
                QuestCreatorGump.Cor("#C9A227", "<CENTER>━━━  GANHO DE SKILL  ━━━</CENTER>"), false, false);
            AddImageTiled(20, 42, W - 40, 2, 9304);

            int y = 54, lW = 150;

            GumpForms.Campo(this, W, y, "Skill:", 0, "Stealing", lW); y += 32;
            GumpForms.Campo(this, W, y, "Pontos ganhos:", 1, "1.0", lW); y += 32;
            GumpForms.Campo(this, W, y, "Aumento de cap:", 2, "0", lW); y += 40;

            AddHtml(25, y, W - 50, 46,
                QuestCreatorGump.Cor("#888888",
                    "<I>Aceita apelidos: 'Swordsmanship' vira Swords, 'Taming' vira AnimalTaming.<BR>" +
                    "O ganho e limitado ao SkillsCap do jogador — nao estoura o teto.</I>"),
                false, false);

            AddButton(W - 250, H - 42, 4005, 4007, 1, GumpButtonType.Reply, 0);
            AddHtml(W - 215, H - 40, 100, 20, QuestCreatorGump.Cor("#2E8B57", "Adicionar"), false, false);

            AddButton(W - 120, H - 42, 4017, 4019, 0, GumpButtonType.Reply, 0);
            AddHtml(W - 85, H - 40, 80, 18, QuestCreatorGump.Cor("#888888", "Cancelar"), false, false);
        }

        public override void OnResponse(NetState sender, RelayInfo info)
        {
            if (info.ButtonID == 1)
            {
                var reward = new QuestCreatorSkillReward
                {
                    SkillName = GumpForms.GetText(info, 0, ""),
                    Amount = GumpForms.ParseDouble(GumpForms.GetText(info, 1, "1.0"), 1.0),
                    CapIncrease = GumpForms.ParseDouble(GumpForms.GetText(info, 2, "0"), 0)
                };

                if (!string.IsNullOrWhiteSpace(reward.SkillName))
                    m_Data.RewardSkills.Add(reward);
            }

            m_From.CloseGump(typeof(AddRewardSkillGump));
            m_From.SendGump(new QuestCreatorGump(m_From, 5, m_Data));
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  PRÉ-REQUISITOS
    // ═══════════════════════════════════════════════════════════════════════

    public class AddPrereqSkillGump : Gump
    {
        private readonly Mobile m_From;
        private readonly QuestCreatorData m_Data;

        private const int W = 440;
        private const int H = 220;

        public AddPrereqSkillGump(Mobile from, QuestCreatorData data) : base(200, 170)
        {
            m_From = from;
            m_Data = data;

            Closable = true;
            Dragable = true;

            AddPage(0);
            AddBackground(0, 0, W, H, 9270);
            AddImageTiled(10, 10, W - 20, H - 20, 2702);
            AddAlphaRegion(10, 10, W - 20, H - 20);

            AddHtml(20, 16, W - 40, 22,
                QuestCreatorGump.Cor("#C9A227", "<CENTER>━━━  SKILL EXIGIDA  ━━━</CENTER>"), false, false);
            AddImageTiled(20, 42, W - 40, 2, 9304);

            int y = 54, lW = 150;

            GumpForms.Campo(this, W, y, "Skill:", 0, "Stealing", lW); y += 32;
            GumpForms.Campo(this, W, y, "Valor minimo:", 1, "30.0", lW); y += 40;

            AddHtml(25, y, W - 50, 30,
                QuestCreatorGump.Cor("#888888",
                    "<I>Quem nao tiver esse valor nao ve a quest como disponivel.<BR>" +
                    "Ex.: Stealing 30 e Snooping 30 para uma quest de ladino.</I>"), false, false);

            AddButton(W - 250, H - 42, 4005, 4007, 1, GumpButtonType.Reply, 0);
            AddHtml(W - 215, H - 40, 100, 20, QuestCreatorGump.Cor("#2E8B57", "Adicionar"), false, false);

            AddButton(W - 120, H - 42, 4017, 4019, 0, GumpButtonType.Reply, 0);
            AddHtml(W - 85, H - 40, 80, 18, QuestCreatorGump.Cor("#888888", "Cancelar"), false, false);
        }

        public override void OnResponse(NetState sender, RelayInfo info)
        {
            if (info.ButtonID == 1)
            {
                var req = new QuestCreatorSkillReq
                {
                    SkillName = GumpForms.GetText(info, 0, ""),
                    Value = GumpForms.ParseDouble(GumpForms.GetText(info, 1, "0"), 0)
                };

                if (!string.IsNullOrWhiteSpace(req.SkillName))
                    m_Data.RequiredSkills.Add(req);
            }

            m_From.CloseGump(typeof(AddPrereqSkillGump));
            m_From.SendGump(new QuestCreatorGump(m_From, 4, m_Data));
        }
    }

    public class AddPrereqItemGump : Gump
    {
        private readonly Mobile m_From;
        private readonly QuestCreatorData m_Data;

        private const int W = 440;
        private const int H = 250;

        public AddPrereqItemGump(Mobile from, QuestCreatorData data) : base(200, 170)
        {
            m_From = from;
            m_Data = data;

            Closable = true;
            Dragable = true;

            AddPage(0);
            AddBackground(0, 0, W, H, 9270);
            AddImageTiled(10, 10, W - 20, H - 20, 2702);
            AddAlphaRegion(10, 10, W - 20, H - 20);

            AddHtml(20, 16, W - 40, 22,
                QuestCreatorGump.Cor("#C9A227", "<CENTER>━━━  ITEM EXIGIDO  ━━━</CENTER>"), false, false);
            AddImageTiled(20, 42, W - 40, 2, 9304);

            int y = 54, lW = 150;

            GumpForms.Campo(this, W, y, "Tipo (classe):", 0, "", lW); y += 32;
            GumpForms.Campo(this, W, y, "Nome exibido:", 1, "", lW); y += 32;
            GumpForms.Campo(this, W, y, "Quantidade:", 2, "1", lW); y += 40;

            AddHtml(25, y, W - 50, 30,
                QuestCreatorGump.Cor("#888888",
                    "<I>A busca percorre mochila, sub-containers e itens equipados,<BR>" +
                    "e reconhece itens virtuais de quest pela tag.</I>"), false, false);

            AddButton(W - 250, H - 42, 4005, 4007, 1, GumpButtonType.Reply, 0);
            AddHtml(W - 215, H - 40, 100, 20, QuestCreatorGump.Cor("#2E8B57", "Adicionar"), false, false);

            AddButton(W - 120, H - 42, 4017, 4019, 0, GumpButtonType.Reply, 0);
            AddHtml(W - 85, H - 40, 80, 18, QuestCreatorGump.Cor("#888888", "Cancelar"), false, false);
        }

        public override void OnResponse(NetState sender, RelayInfo info)
        {
            if (info.ButtonID == 1)
            {
                var req = new QuestCreatorItemReq
                {
                    Type = GumpForms.GetText(info, 0, ""),
                    Name = GumpForms.GetText(info, 1, ""),
                    Amount = Math.Max(1, GumpForms.ParseInt(GumpForms.GetText(info, 2, "1"), 1))
                };

                if (!string.IsNullOrWhiteSpace(req.Type))
                    m_Data.RequiredItems.Add(req);
            }

            m_From.CloseGump(typeof(AddPrereqItemGump));
            m_From.SendGump(new QuestCreatorGump(m_From, 4, m_Data));
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  MODELO DE DADOS DO CRIADOR/EDITOR
    // ═══════════════════════════════════════════════════════════════════════

    public class QuestCreatorObjective
    {
        public string Type;
        public Dictionary<string, string> Fields =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public QuestCreatorObjective() : this("kill") { }

        public QuestCreatorObjective(string type)
        {
            Type = string.IsNullOrWhiteSpace(type) ? "kill" : type.Trim();
        }

        public string Get(string key)
        {
            string value;
            return Fields.TryGetValue(key, out value) ? value : "";
        }

        public void Set(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                Fields.Remove(key);
            else
                Fields[key] = value.Trim();
        }

        public bool IsOptional
        {
            get { return Get("optional").Equals("true", StringComparison.OrdinalIgnoreCase); }
        }

        public string Description
        {
            get
            {
                string d = Get("description");
                return string.IsNullOrEmpty(d) ? "(" + Type + ")" : d;
            }
        }

        /// <summary>Resumo de uma linha para a lista de objetivos.</summary>
        public string Summary()
        {
            var parts = new List<string>();

            string primary = Get(ObjectiveFields.PrimaryTargetKey(Type));
            if (!string.IsNullOrEmpty(primary))
                parts.Add(primary);

            string[] extras = ExtraSummaryKeys();

            foreach (string key in extras)
            {
                string value = Get(key);
                if (!string.IsNullOrEmpty(value) && value != "0")
                    parts.Add(key + "=" + value);
            }

            string required = Get("required");
            if (!string.IsNullOrEmpty(required) && required != "1")
                parts.Add("qtd=" + required);

            return string.Join("  ", parts.ToArray());
        }

        private string[] ExtraSummaryKeys()
        {
            switch (Type.ToLowerInvariant())
            {
                case "kill": return new[] { "targetName", "requiredRegion" };
                case "collect": return new[] { "itemName", "fromMobTypes" };
                case "deliver": return new[] { "targetNpcName" };
                case "talkto": return new[] { "targetNpcName" };
                case "escort": return new[] { "destinationName", "escortNpcName" };
                case "defend": return new[] { "totalWaves", "enemiesPerWave" };
                case "explore": return new[] { "locationX", "locationY", "radiusTiles", "stayDurationSeconds" };
                case "craft": return new[] { "itemName", "craftSkill" };
                case "useskill": case "skillgain": return new[] { "targetValue" };
                case "survive": return new[] { "requiredRegion" };
                case "destroy": case "interact": return new[] { "targetName" };
                case "useitem": return new[] { "itemName" };
                default: return new string[0];
            }
        }
    }

    public class QuestCreatorRewardItem
    {
        public string Type = "Gold";
        public string Name = "";
        public int Amount = 1;
        public int Hue = 0;
        public int Chance = 100;
    }

    public class QuestCreatorSkillReward
    {
        public string SkillName = "";
        public double Amount = 1.0;
        public double CapIncrease = 0;
    }

    public class QuestCreatorSkillReq
    {
        public string SkillName = "";
        public double Value = 0;
    }

    public class QuestCreatorItemReq
    {
        public string Type = "";
        public string Name = "";
        public int Amount = 1;
        public int Hue = 0;
        public bool Consume = false;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  QuestCreatorData — estado do assistente + JSON + carregamento p/ edição
    // ═══════════════════════════════════════════════════════════════════════

    public class QuestCreatorData
    {
        // ── Identidade ─────────────────────────────────────────────────────
        public string Id = "";
        public string Name = "";
        public string Description = "";
        public string Category = "Side";
        public string Difficulty = "Normal";
        public string ChainId = "";
        public string ChainOrderStr = "0";
        public string NextQuestId = "";
        public string CompletionMessage = "";

        // ── NPCs ───────────────────────────────────────────────────────────
        public string GiverNpcId = "";
        public string GiverNpcName = "";
        public string TurnInNpcId = "";
        public string TurnInNpcName = "";

        // ── Configurações ──────────────────────────────────────────────────
        public bool Repeatable = false;
        public bool Shareable = false;
        public bool AutoStart = false;
        public string CooldownStr = "0";
        public string MaxCompletionsStr = "0";

        // ── Limite de tempo ────────────────────────────────────────────────
        public bool TimeLimitEnabled = false;
        public string TimeLimitMinutesStr = "0";
        public string OnTimeExpire = "Fail";
        public string TimeWarningsStr = "";

        // ── Objetivos ──────────────────────────────────────────────────────
        public bool SequentialObjectives = false;
        public List<QuestCreatorObjective> Objectives = new List<QuestCreatorObjective>();

        // ── Pré-requisitos ─────────────────────────────────────────────────
        public string RequiredQuestsStr = "";
        public string MinLevelStr = "0";
        public string MinFameStr = "0";
        public string MinKarmaStr = "";
        public string MaxKarmaStr = "";
        public string RequiredFaction = "";
        public string RequiredGuild = "";
        public string AllowedRacesStr = "";
        public string FailMessage = "";
        public List<QuestCreatorSkillReq> RequiredSkills = new List<QuestCreatorSkillReq>();
        public List<QuestCreatorItemReq> RequiredItems = new List<QuestCreatorItemReq>();

        // ── Recompensas ────────────────────────────────────────────────────
        public string RewardGold = "0";
        public string RewardFame = "0";
        public string RewardKarma = "0";
        public string RewardExperience = "0";
        public string RewardTitlesStr = "";
        public bool BankDeposit = false;
        public List<QuestCreatorRewardItem> RewardItems = new List<QuestCreatorRewardItem>();
        public List<QuestCreatorSkillReward> RewardSkills = new List<QuestCreatorSkillReward>();

        // ── Diálogo ────────────────────────────────────────────────────────
        public string DialogueOffer = "";
        public string DialogueAccept = "";
        public string DialogueDecline = "";
        public string DialogueProgress = "";
        public string DialogueComplete = "";

        // ── Edição ─────────────────────────────────────────────────────────
        public bool IsEditing = false;
        public string SourceFile = "";

        // ═══════════════════════════════════════════════════════════════
        //  VALIDAÇÃO — impede gravar uma quest impossível
        // ═══════════════════════════════════════════════════════════════

        public List<string> Validate()
        {
            var problems = new List<string>();

            if (string.IsNullOrWhiteSpace(Id))
                problems.Add("ID vazio.");

            if (string.IsNullOrWhiteSpace(Name))
                problems.Add("Nome vazio.");

            if (Objectives.Count == 0)
            {
                problems.Add("Nenhum objetivo — a quest nao pode ser concluida.");
                return problems;
            }

            for (int i = 0; i < Objectives.Count; i++)
            {
                var obj = Objectives[i];
                if (obj == null) continue;

                string label = "Objetivo " + (i + 1) + " (" + obj.Type + ")";

                if (string.IsNullOrWhiteSpace(obj.Description))
                    problems.Add(label + ": sem descricao (o jogador nao sabe o que fazer).");

                ValidateObjective(obj, label, problems);
            }

            // Pré-requisitos inalcançáveis
            foreach (string questId in SplitList(RequiredQuestsStr))
            {
                if (questId.Equals(Id, StringComparison.OrdinalIgnoreCase))
                    problems.Add("A quest exige a si mesma como pre-requisito (loop impossivel).");
                else if (QuestRepository.Instance.GetQuest(questId) == null && !IsEditing)
                    problems.Add("Pre-requisito '" + questId + "' nao existe no repositorio.");
            }

            if (!string.IsNullOrWhiteSpace(NextQuestId) &&
                QuestRepository.Instance.GetQuest(NextQuestId) == null && !IsEditing)
            {
                problems.Add("Proxima quest '" + NextQuestId + "' nao existe (a cadeia quebra aqui).");
            }

            if (TimeLimitEnabled && GumpForms.ParseInt(TimeLimitMinutesStr, 0) <= 0)
                problems.Add("Limite de tempo ativo sem minutos validos.");

            if (string.IsNullOrWhiteSpace(GiverNpcId) && string.IsNullOrWhiteSpace(GiverNpcName))
                problems.Add("Sem NPC giver: nenhuma NPC vai oferecer esta quest.");

            if (string.IsNullOrWhiteSpace(TurnInNpcId) && string.IsNullOrWhiteSpace(TurnInNpcName))
            {
                if (!string.IsNullOrWhiteSpace(GiverNpcId) || !string.IsNullOrWhiteSpace(GiverNpcName))
                {
                    // Sem turn-in declarado o sistema usa o giver — só avisa
                    TurnInNpcId = GiverNpcId;
                    TurnInNpcName = GiverNpcName;
                }
                else
                {
                    problems.Add("Sem NPC de entrega: a quest nunca podera ser finalizada.");
                }
            }

            return problems;
        }

        private static void ValidateObjective(QuestCreatorObjective obj, string label, List<string> problems)
        {
            string type = (obj.Type ?? "").ToLowerInvariant();

            int required = GumpForms.ParseInt(obj.Get("required"), 1);

            switch (type)
            {
                case "kill":
                    if (string.IsNullOrWhiteSpace(obj.Get("targetType")) &&
                        string.IsNullOrWhiteSpace(obj.Get("targetTypes")) &&
                        string.IsNullOrWhiteSpace(obj.Get("targetName")))
                        problems.Add(label + ": sem alvo (targetType/targetName) — nunca progredira.");
                    break;

                case "collect":
                case "deliver":
                case "craft":
                case "useitem":
                    if (string.IsNullOrWhiteSpace(obj.Get("itemType")) &&
                        string.IsNullOrWhiteSpace(obj.Get("itemName")))
                        problems.Add(label + ": sem itemType/itemName — nunca progredira.");
                    if (required <= 0)
                        problems.Add(label + ": quantidade deve ser >= 1.");
                    break;

                case "talkto":
                    if (string.IsNullOrWhiteSpace(obj.Get("targetNpcId")) &&
                        string.IsNullOrWhiteSpace(obj.Get("targetNpcName")))
                        problems.Add(label + ": sem NPC alvo — nunca progredira.");
                    break;

                case "escort":
                    bool hasCoords = GumpForms.ParseInt(obj.Get("destinationX"), 0) != 0 ||
                                     GumpForms.ParseInt(obj.Get("destinationY"), 0) != 0;

                    if (!hasCoords && string.IsNullOrWhiteSpace(obj.Get("destinationRegion")))
                        problems.Add(label + ": sem destino (coordenadas ou regiao) — a escolta nunca termina.");
                    break;

                case "defend":
                    if (string.IsNullOrWhiteSpace(obj.Get("enemyType")))
                        problems.Add(label + ": sem enemyType — nenhuma onda sera gerada.");
                    break;

                case "explore":
                    bool hasLoc = GumpForms.ParseInt(obj.Get("locationX"), 0) != 0 ||
                                  GumpForms.ParseInt(obj.Get("locationY"), 0) != 0;

                    if (!hasLoc && string.IsNullOrWhiteSpace(obj.Get("locationName")))
                        problems.Add(label + ": sem local (X/Y ou nome) — nunca progredira.");
                    break;

                case "useskill":
                case "skillgain":
                    if (string.IsNullOrWhiteSpace(obj.Get("skill")))
                        problems.Add(label + ": sem skill — nunca progredira.");
                    break;

                case "destroy":
                case "interact":
                    if (string.IsNullOrWhiteSpace(obj.Get("targetType")) &&
                        string.IsNullOrWhiteSpace(obj.Get("targetSerials")) &&
                        string.IsNullOrWhiteSpace(obj.Get("targetName")))
                        problems.Add(label + ": sem alvo — nunca progredira.");
                    break;

                case "survive":
                    if (GumpForms.ParseInt(obj.Get("durationSeconds"), 0) <= 0)
                        problems.Add(label + ": duracao deve ser > 0 segundos.");
                    break;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  RESUMOS
        // ═══════════════════════════════════════════════════════════════

        public string PrerequisitesSummary()
        {
            var parts = new List<string>();

            string[] quests = SplitList(RequiredQuestsStr);
            if (quests.Length > 0)
                parts.Add(quests.Length + " quest(s)");

            foreach (var s in RequiredSkills)
                parts.Add(s.SkillName + " " + s.Value);

            foreach (var i in RequiredItems)
                parts.Add(i.Amount + "x " + i.Type);

            if (GumpForms.ParseInt(MinLevelStr, 0) > 0) parts.Add("nivel " + MinLevelStr);
            if (GumpForms.ParseInt(MinFameStr, 0) > 0) parts.Add("fama " + MinFameStr);
            if (!string.IsNullOrWhiteSpace(MinKarmaStr)) parts.Add("karma >= " + MinKarmaStr);
            if (!string.IsNullOrWhiteSpace(MaxKarmaStr)) parts.Add("karma <= " + MaxKarmaStr);
            if (!string.IsNullOrWhiteSpace(RequiredFaction)) parts.Add("faccao " + RequiredFaction);
            if (!string.IsNullOrWhiteSpace(RequiredGuild)) parts.Add("guilda " + RequiredGuild);
            if (!string.IsNullOrWhiteSpace(AllowedRacesStr)) parts.Add("racas " + AllowedRacesStr);

            return parts.Count == 0 ? "nenhum" : string.Join(", ", parts.ToArray());
        }

        public string RewardsSummary()
        {
            var parts = new List<string>();

            if (GumpForms.ParseInt(RewardGold, 0) > 0) parts.Add(RewardGold + " ouro");
            if (GumpForms.ParseInt(RewardFame, 0) > 0) parts.Add(RewardFame + " fama");
            if (GumpForms.ParseInt(RewardKarma, 0) != 0) parts.Add(RewardKarma + " karma");
            if (GumpForms.ParseInt(RewardExperience, 0) > 0) parts.Add(RewardExperience + " exp");
            if (RewardItems.Count > 0) parts.Add(RewardItems.Count + " item(ns)");
            if (RewardSkills.Count > 0) parts.Add(RewardSkills.Count + " skill(s)");
            if (!string.IsNullOrWhiteSpace(RewardTitlesStr)) parts.Add("titulo(s)");

            return parts.Count == 0 ? "nenhuma" : string.Join(", ", parts.ToArray());
        }

        private static string[] SplitList(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new string[0];

            var result = new List<string>();

            foreach (string part in text.Split(new[] { ',', '|', ';' }))
            {
                string trimmed = part.Trim();
                if (trimmed.Length > 0)
                    result.Add(trimmed);
            }

            return result.ToArray();
        }

        // ═══════════════════════════════════════════════════════════════
        //  GERAR JSON
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Gera o JSON no formato que o QuestLoader lê.
        /// Cada tipo de objetivo emite seus próprios campos, com os nomes exatos
        /// usados pelo loader — é isso que torna as quests criadas em jogo
        /// realmente concluíveis.
        /// </summary>
        public string ToJson()
        {
            var sb = new StringBuilder();

            sb.AppendLine("{");

            // ── identity ───────────────────────────────────────────────
            sb.AppendLine("  \"identity\": {");
            Line(sb, "id", Id, true);
            Line(sb, "name", Name, true);
            Line(sb, "description", Description, true);
            Line(sb, "category", DefaultIfEmpty(Category, "Side"), true);
            Line(sb, "difficulty", DefaultIfEmpty(Difficulty, "Normal"), true);
            if (!string.IsNullOrWhiteSpace(ChainId))
                Line(sb, "chainId", ChainId, true);
            sb.Append("    \"chainOrder\": ").Append(SafeInt(ChainOrderStr)).AppendLine();
            sb.AppendLine("  },");

            // ── npcs ───────────────────────────────────────────────────
            sb.AppendLine("  \"npcs\": {");
            Line(sb, "giverId", GiverNpcId, true);
            Line(sb, "giverName", GiverNpcName, true);
            Line(sb, "turnInId", TurnInNpcId, true);
            sb.Append("    \"turnInName\": \"").Append(Esc(TurnInNpcName)).AppendLine("\"");
            sb.AppendLine("  },");

            // ── settings ───────────────────────────────────────────────
            sb.AppendLine("  \"settings\": {");
            sb.Append("    \"repeatable\": ").Append(Bool(Repeatable)).AppendLine(",");
            sb.Append("    \"cooldownHours\": ").Append(SafeInt(CooldownStr)).AppendLine(",");
            sb.Append("    \"maxCompletions\": ").Append(SafeInt(MaxCompletionsStr)).AppendLine(",");
            sb.Append("    \"shareableWithParty\": ").Append(Bool(Shareable)).AppendLine(",");
            sb.Append("    \"autoStart\": ").Append(Bool(AutoStart)).AppendLine();
            sb.AppendLine("  },");

            // ── timeLimit ──────────────────────────────────────────────
            if (TimeLimitEnabled)
            {
                sb.AppendLine("  \"timeLimit\": {");
                sb.AppendLine("    \"enabled\": true,");
                sb.Append("    \"minutes\": ").Append(SafeInt(TimeLimitMinutesStr)).AppendLine(",");
                Line(sb, "onExpire", DefaultIfEmpty(OnTimeExpire, "Fail"), true);

                string[] warnings = SplitList(TimeWarningsStr);
                if (warnings.Length > 0)
                {
                    sb.Append("    \"warnings\": [");
                    for (int i = 0; i < warnings.Length; i++)
                    {
                        sb.Append(SafeInt(warnings[i]));
                        if (i < warnings.Length - 1) sb.Append(", ");
                    }
                    sb.AppendLine("]");
                }
                else
                {
                    sb.AppendLine("    \"warnings\": []");
                }

                sb.AppendLine("  },");
            }

            // ── prerequisites ──────────────────────────────────────────
            sb.AppendLine("  \"prerequisites\": {");

            string[] requiredQuests = SplitList(RequiredQuestsStr);
            sb.Append("    \"questIds\": ");
            JsonStringArray(sb, requiredQuests);
            sb.AppendLine(",");

            sb.Append("    \"minLevel\": ").Append(SafeInt(MinLevelStr)).AppendLine(",");
            sb.Append("    \"minFame\": ").Append(SafeInt(MinFameStr)).AppendLine(",");

            if (!string.IsNullOrWhiteSpace(MinKarmaStr))
                sb.Append("    \"minKarma\": ").Append(SafeInt(MinKarmaStr)).AppendLine(",");

            if (!string.IsNullOrWhiteSpace(MaxKarmaStr))
                sb.Append("    \"maxKarma\": ").Append(SafeInt(MaxKarmaStr)).AppendLine(",");

            if (!string.IsNullOrWhiteSpace(RequiredFaction))
            {
                sb.Append("    \"requiredFaction\": \"").Append(Esc(RequiredFaction)).AppendLine("\",");
            }

            if (!string.IsNullOrWhiteSpace(RequiredGuild))
            {
                sb.Append("    \"requiredGuild\": \"").Append(Esc(RequiredGuild)).AppendLine("\",");
            }

            string[] races = SplitList(AllowedRacesStr);
            sb.Append("    \"allowedRaces\": ");
            JsonStringArray(sb, races);
            sb.AppendLine(",");

            if (!string.IsNullOrWhiteSpace(FailMessage))
            {
                sb.Append("    \"failMessage\": \"").Append(Esc(FailMessage)).AppendLine("\",");
            }

            sb.AppendLine("    \"requiredSkills\": [");
            for (int i = 0; i < RequiredSkills.Count; i++)
            {
                var req = RequiredSkills[i];
                sb.AppendLine("      {");
                sb.Append("        \"skill\": \"").Append(Esc(req.SkillName)).AppendLine("\",");
                sb.Append("        \"value\": ").Append(Num(req.Value)).AppendLine();
                sb.Append("      }").AppendLine(i < RequiredSkills.Count - 1 ? "," : "");
            }
            sb.AppendLine("    ],");

            sb.AppendLine("    \"requiredItems\": [");
            for (int i = 0; i < RequiredItems.Count; i++)
            {
                var req = RequiredItems[i];
                sb.AppendLine("      {");
                sb.Append("        \"itemType\": \"").Append(Esc(req.Type)).AppendLine("\",");
                if (!string.IsNullOrWhiteSpace(req.Name))
                    sb.Append("        \"itemName\": \"").Append(Esc(req.Name)).AppendLine("\",");
                if (req.Hue > 0)
                    sb.Append("        \"hue\": ").Append(req.Hue).AppendLine(",");
                sb.Append("        \"amount\": ").Append(req.Amount).AppendLine();
                sb.Append("      }").AppendLine(i < RequiredItems.Count - 1 ? "," : "");
            }
            sb.AppendLine("    ]");

            sb.AppendLine("  },");

            // ── objectives ─────────────────────────────────────────────
            sb.AppendLine("  \"objectives\": [");
            for (int i = 0; i < Objectives.Count; i++)
            {
                WriteObjective(sb, Objectives[i], i == Objectives.Count - 1);
            }
            sb.AppendLine("  ],");

            // ── rewards ────────────────────────────────────────────────
            sb.AppendLine("  \"rewards\": {");
            sb.Append("    \"gold\": ").Append(SafeInt(RewardGold)).AppendLine(",");
            sb.Append("    \"fame\": ").Append(SafeInt(RewardFame)).AppendLine(",");
            sb.Append("    \"karma\": ").Append(SafeInt(RewardKarma)).AppendLine(",");
            sb.Append("    \"experience\": ").Append(SafeInt(RewardExperience)).AppendLine(",");
            sb.Append("    \"bankDeposit\": ").Append(Bool(BankDeposit)).AppendLine(",");

            sb.AppendLine("    \"items\": [");
            for (int i = 0; i < RewardItems.Count; i++)
            {
                var item = RewardItems[i];
                sb.AppendLine("      {");
                sb.Append("        \"type\": \"").Append(Esc(item.Type)).AppendLine("\",");
                if (!string.IsNullOrWhiteSpace(item.Name))
                    sb.Append("        \"name\": \"").Append(Esc(item.Name)).AppendLine("\",");
                if (item.Hue > 0)
                    sb.Append("        \"hue\": ").Append(item.Hue).AppendLine(",");
                if (item.Chance > 0 && item.Chance < 100)
                    sb.Append("        \"chance\": ").Append(item.Chance).AppendLine(",");
                sb.Append("        \"amount\": ").Append(item.Amount).AppendLine();
                sb.Append("      }").AppendLine(i < RewardItems.Count - 1 ? "," : "");
            }
            sb.AppendLine("    ],");

            sb.AppendLine("    \"skillGains\": [");
            for (int i = 0; i < RewardSkills.Count; i++)
            {
                var reward = RewardSkills[i];
                sb.AppendLine("      {");
                sb.Append("        \"skill\": \"").Append(Esc(reward.SkillName)).AppendLine("\",");
                if (reward.CapIncrease > 0)
                    sb.Append("        \"capIncrease\": ").Append(Num(reward.CapIncrease)).AppendLine(",");
                sb.Append("        \"amount\": ").Append(Num(reward.Amount)).AppendLine();
                sb.Append("      }").AppendLine(i < RewardSkills.Count - 1 ? "," : "");
            }
            sb.AppendLine("    ],");

            string[] titles = SplitList(RewardTitlesStr);
            sb.Append("    \"titles\": ");
            JsonStringArray(sb, titles);
            sb.AppendLine();

            sb.AppendLine("  },");

            // ── dialogue ───────────────────────────────────────────────
            sb.AppendLine("  \"dialogue\": {");
            sb.Append("    \"onOffer\": "); JsonStringArray(sb, SplitLines(DialogueOffer)); sb.AppendLine(",");
            sb.Append("    \"onAccept\": "); JsonStringArray(sb, SplitLines(DialogueAccept)); sb.AppendLine(",");
            sb.Append("    \"onDecline\": "); JsonStringArray(sb, SplitLines(DialogueDecline)); sb.AppendLine(",");
            sb.Append("    \"onProgress\": "); JsonStringArray(sb, SplitLines(DialogueProgress)); sb.AppendLine(",");
            sb.Append("    \"onComplete\": "); JsonStringArray(sb, SplitLines(DialogueComplete)); sb.AppendLine();
            sb.AppendLine("  }");

            if (!string.IsNullOrWhiteSpace(NextQuestId))
            {
                sb.AppendLine(",");
                sb.Append("  \"nextQuest\": \"").Append(Esc(NextQuestId)).AppendLine("\"");
            }

            if (!string.IsNullOrWhiteSpace(CompletionMessage))
            {
                sb.AppendLine(",");
                sb.Append("  \"completionMessage\": \"").Append(Esc(CompletionMessage)).AppendLine("\"");
            }

            sb.AppendLine("}");

            return sb.ToString();
        }

        private void WriteObjective(StringBuilder sb, QuestCreatorObjective obj, bool last)
        {
            // Os campos são acumulados e depois juntados com vírgula, para que o
            // último nunca receba vírgula trailing (JSON inválido).
            var lines = new List<string>();

            lines.Add("      \"type\": \"" + Esc(obj.Type) + "\"");

            if (!string.IsNullOrWhiteSpace(obj.Get("description")))
                lines.Add("      \"description\": \"" + Esc(obj.Get("description")) + "\"");

            int required = GumpForms.ParseInt(obj.Get("required"), 1);
            lines.Add("      \"required\": " + (required > 0 ? required : 1));

            if (!string.IsNullOrWhiteSpace(obj.Get("locationHint")))
                lines.Add("      \"locationHint\": \"" + Esc(obj.Get("locationHint")) + "\"");

            if (obj.IsOptional)
                lines.Add("      \"optional\": true");

            if (obj.Get("hidden").Equals("true", StringComparison.OrdinalIgnoreCase))
                lines.Add("      \"hidden\": true");

            // Campos específicos do tipo, com os nomes exatos que o loader lê
            foreach (ObjectiveFields.Field f in ObjectiveFields.GetFields(obj.Type))
            {
                if (f.Key.Equals("required", StringComparison.OrdinalIgnoreCase))
                    continue;   // já emitido acima

                string value = obj.Get(f.Key);
                if (string.IsNullOrWhiteSpace(value))
                    continue;

                if (f.Key.Equals("grantItems", StringComparison.OrdinalIgnoreCase))
                {
                    string grant = WriteGrantItems(value);
                    if (!string.IsNullOrEmpty(grant))
                        lines.Add(grant);
                    continue;
                }

                if (f.Key.Equals("targetTypes", StringComparison.OrdinalIgnoreCase) ||
                    f.Key.Equals("fromMobTypes", StringComparison.OrdinalIgnoreCase) ||
                    f.Key.Equals("targetSerials", StringComparison.OrdinalIgnoreCase))
                {
                    var arr = new StringBuilder();
                    JsonStringArray(arr, SplitList(value));
                    lines.Add("      \"" + f.Key + "\": " + arr);
                    continue;
                }

                switch (f.Kind)
                {
                    case ObjectiveFields.BOOL:
                        lines.Add("      \"" + f.Key + "\": " +
                                  Bool(value.Equals("true", StringComparison.OrdinalIgnoreCase)));
                        break;

                    case ObjectiveFields.INT:
                        lines.Add("      \"" + f.Key + "\": " + IntOrRaw(value));
                        break;

                    case ObjectiveFields.NUM:
                        lines.Add("      \"" + f.Key + "\": " +
                                  Num(GumpForms.ParseDouble(value, 0)));
                        break;

                    default:
                        lines.Add("      \"" + f.Key + "\": \"" + Esc(value) + "\"");
                        break;
                }
            }

            sb.AppendLine("    {");

            for (int i = 0; i < lines.Count; i++)
                sb.AppendLine(lines[i] + (i < lines.Count - 1 ? "," : ""));

            sb.Append("    }").AppendLine(last ? "" : ",");
        }

        /// <summary>
        /// grantItems é digitado como "Tipo|Nome|Qtd;Tipo2|Nome2|Qtd2".
        /// Retorna o trecho JSON pronto (ou null se não houver nada).
        /// </summary>
        private static string WriteGrantItems(string raw)
        {
            var grants = new List<string[]>();

            foreach (string entry in raw.Split(';'))
            {
                if (string.IsNullOrWhiteSpace(entry))
                    continue;

                string[] parts = entry.Split('|');

                string type = parts.Length > 0 ? parts[0].Trim() : "";
                string name = parts.Length > 1 ? parts[1].Trim() : "";
                string amount = parts.Length > 2 ? parts[2].Trim() : "1";

                if (string.IsNullOrWhiteSpace(type) && string.IsNullOrWhiteSpace(name))
                    continue;

                grants.Add(new[] { type, name, amount });
            }

            if (grants.Count == 0)
                return null;

            var sb = new StringBuilder();
            sb.AppendLine("      \"grantItems\": [");

            for (int i = 0; i < grants.Count; i++)
            {
                var lines = new List<string>();

                if (!string.IsNullOrWhiteSpace(grants[i][0]))
                    lines.Add("          \"itemType\": \"" + Esc(grants[i][0]) + "\"");

                if (!string.IsNullOrWhiteSpace(grants[i][1]))
                    lines.Add("          \"itemName\": \"" + Esc(grants[i][1]) + "\"");

                lines.Add("          \"amount\": " + SafeInt(grants[i][2]));

                sb.AppendLine("        {");
                for (int j = 0; j < lines.Count; j++)
                    sb.AppendLine(lines[j] + (j < lines.Count - 1 ? "," : ""));
                sb.Append("        }").AppendLine(i < grants.Count - 1 ? "," : "");
            }

            sb.Append("      ]");

            return sb.ToString();
        }

        // ═══════════════════════════════════════════════════════════════
        //  CARREGAR QUEST EXISTENTE PARA EDIÇÃO
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Reconstrói o formulário a partir de uma QuestDefinition já carregada.
        /// É o que permite editar quests existentes sem recriá-las do zero.
        /// </summary>
        public static QuestCreatorData FromDefinition(QuestDefinition q)
        {
            var d = new QuestCreatorData();

            if (q == null)
                return d;

            d.IsEditing = true;
            d.SourceFile = FindSourceFile(q.Id);

            d.Id = q.Id ?? "";
            d.Name = q.Name ?? "";
            d.Description = q.Description ?? "";
            d.Category = q.Category.ToString();
            d.Difficulty = q.Difficulty.ToString();
            d.ChainId = q.ChainId ?? "";
            d.ChainOrderStr = q.ChainOrder.ToString();
            d.NextQuestId = q.NextQuestId ?? "";
            d.CompletionMessage = q.CompletionMessage ?? "";

            d.GiverNpcId = q.GiverNpcId ?? "";
            d.GiverNpcName = q.GiverNpcName ?? "";
            d.TurnInNpcId = q.TurnInNpcId ?? "";
            d.TurnInNpcName = q.TurnInNpcName ?? "";

            d.Repeatable = q.Repeatable;
            d.Shareable = q.Shareable;
            d.AutoStart = q.AutoStart;
            d.CooldownStr = q.CooldownHours.ToString();
            d.MaxCompletionsStr = q.MaxCompletions.ToString();

            d.TimeLimitEnabled = q.HasTimeLimit;
            d.TimeLimitMinutesStr = q.TimeLimitMinutes.ToString();
            d.OnTimeExpire = q.OnTimeExpire ?? "Fail";

            if (q.TimeWarnings != null && q.TimeWarnings.Count > 0)
            {
                var warns = new List<string>();
                foreach (int w in q.TimeWarnings) warns.Add(w.ToString());
                d.TimeWarningsStr = string.Join("|", warns.ToArray());
            }

            d.SequentialObjectives = q.SequentialObjectives;

            if (q.Objectives != null)
            {
                foreach (var objective in q.Objectives)
                {
                    var created = FromObjective(objective);
                    if (created != null)
                        d.Objectives.Add(created);
                }
            }

            if (q.Prerequisites != null)
            {
                var p = q.Prerequisites;

                if (p.RequiredQuests != null && p.RequiredQuests.Count > 0)
                    d.RequiredQuestsStr = string.Join(", ", p.RequiredQuests.ToArray());

                d.MinLevelStr = p.MinLevel.ToString();
                d.MinFameStr = p.MinFame.ToString();
                d.MinKarmaStr = p.MinKarma.HasValue ? p.MinKarma.Value.ToString() : "";
                d.MaxKarmaStr = p.MaxKarma.HasValue ? p.MaxKarma.Value.ToString() : "";
                d.RequiredFaction = p.RequiredFaction ?? "";
                d.RequiredGuild = p.RequiredGuild ?? "";
                d.FailMessage = p.FailMessage ?? "";

                if (p.AllowedRaces != null && p.AllowedRaces.Count > 0)
                    d.AllowedRacesStr = string.Join(", ", p.AllowedRaces.ToArray());

                if (p.RequiredSkills != null)
                {
                    foreach (var req in p.RequiredSkills)
                    {
                        if (req == null) continue;

                        d.RequiredSkills.Add(new QuestCreatorSkillReq
                        {
                            SkillName = string.IsNullOrWhiteSpace(req.SkillName) ? req.Skill.ToString() : req.SkillName,
                            Value = req.Value
                        });
                    }
                }

                if (p.RequiredItems != null)
                {
                    foreach (var req in p.RequiredItems)
                    {
                        if (req == null || string.IsNullOrWhiteSpace(req.Type)) continue;

                        d.RequiredItems.Add(new QuestCreatorItemReq
                        {
                            Type = req.Type,
                            Name = req.Name ?? "",
                            Amount = req.Amount > 0 ? req.Amount : 1,
                            Hue = req.Hue,
                            Consume = req.Consume
                        });
                    }
                }
            }

            if (q.Rewards != null)
            {
                var r = q.Rewards;

                d.RewardGold = r.Gold.ToString();
                d.RewardFame = r.Fame.ToString();
                d.RewardKarma = r.Karma.ToString();
                d.RewardExperience = r.Experience.ToString();
                d.BankDeposit = r.BankDeposit;

                if (r.Items != null)
                {
                    foreach (var item in r.Items)
                    {
                        if (item == null) continue;

                        d.RewardItems.Add(new QuestCreatorRewardItem
                        {
                            Type = item.Type ?? "",
                            Name = item.Name ?? "",
                            Amount = item.Amount > 0 ? item.Amount : 1,
                            Hue = item.Hue,
                            Chance = item.Chance > 0 ? item.Chance : 100
                        });
                    }
                }

                if (r.SkillGains != null)
                {
                    foreach (var reward in r.SkillGains)
                    {
                        if (reward == null) continue;

                        d.RewardSkills.Add(new QuestCreatorSkillReward
                        {
                            SkillName = string.IsNullOrWhiteSpace(reward.SkillName)
                                ? reward.Skill.ToString() : reward.SkillName,
                            Amount = reward.Amount,
                            CapIncrease = reward.CapIncrease
                        });
                    }
                }

                if (r.Titles != null && r.Titles.Count > 0)
                {
                    var titles = new List<string>();
                    foreach (var t in r.Titles)
                    {
                        if (t != null && !string.IsNullOrWhiteSpace(t.Title))
                            titles.Add(t.Title);
                    }
                    d.RewardTitlesStr = string.Join("|", titles.ToArray());
                }
            }

            if (q.Dialogue != null)
            {
                d.DialogueOffer = JoinLines(q.Dialogue.Offer);
                d.DialogueAccept = JoinLines(q.Dialogue.Accept);
                d.DialogueDecline = JoinLines(q.Dialogue.Decline);
                d.DialogueProgress = JoinLines(q.Dialogue.Progress);
                d.DialogueComplete = JoinLines(q.Dialogue.Complete);
            }

            return d;
        }

        private static string JoinLines(DialogueNode node)
        {
            if (node == null || node.Lines == null || node.Lines.Count == 0)
                return "";

            var result = new List<string>();

            foreach (DialogueLine line in node.Lines)
            {
                if (line != null && !string.IsNullOrWhiteSpace(line.Text))
                    result.Add(line.Text);
            }

            return string.Join("|", result.ToArray());
        }

        /// <summary>Descobre em qual arquivo a quest foi carregada.</summary>
        private static string FindSourceFile(string questId)
        {
            try
            {
                string path = QuestLoader.QuestsPath;

                if (!Directory.Exists(path))
                    return "";

                foreach (string file in Directory.GetFiles(path, "*.json", SearchOption.AllDirectories))
                {
                    try
                    {
                        var loaded = QuestLoader.LoadQuestsFromFile(file);

                        foreach (var q in loaded)
                        {
                            if (q != null && string.Equals(q.Id, questId, StringComparison.OrdinalIgnoreCase))
                                return Path.GetFileName(file);
                        }
                    }
                    catch
                    {
                        // arquivo inválido não impede a edição das demais
                    }
                }
            }
            catch
            {
                // sem acesso ao disco: salva como arquivo novo
            }

            return "";
        }

        /// <summary>Converte um objetivo carregado de volta para o formulário.</summary>
        private static QuestCreatorObjective FromObjective(QuestObjective o)
        {
            if (o == null)
                return null;

            var obj = new QuestCreatorObjective(o.Type.ToString());

            obj.Set("description", o.Description);
            obj.Set("locationHint", o.LocationHint);
            obj.Set("required", o.Required > 0 ? o.Required.ToString() : "1");

            if (o.IsOptional) obj.Set("optional", "true");
            if (o.IsHidden) obj.Set("hidden", "true");

            var kill = o as KillObjective;
            if (kill != null)
            {
                obj.Set("targetType", kill.TargetType);
                obj.Set("targetName", kill.TargetName);
                obj.Set("requiredRegion", kill.RequiredRegion);
                if (kill.CountPetKills) obj.Set("countPetKills", "true");
                if (kill.CountPartyKills) obj.Set("countPartyKills", "true");
                if (kill.TargetTypes != null && kill.TargetTypes.Count > 0)
                    obj.Set("targetTypes", string.Join(", ", kill.TargetTypes.ToArray()));
                return obj;
            }

            var collect = o as CollectObjective;
            if (collect != null)
            {
                obj.Set("itemType", collect.ItemType);
                obj.Set("itemName", collect.ItemName);
                if (collect.ItemHue > 0) obj.Set("itemHue", collect.ItemHue.ToString());
                if (collect.ConsumeOnComplete) obj.Set("consumeOnComplete", "true");
                if (collect.MarkAsQuestItem) obj.Set("markAsQuestItem", "true");
                if (collect.DropChance > 0) obj.Set("dropChance", collect.DropChance.ToString());
                if (collect.FromMobTypes != null && collect.FromMobTypes.Count > 0)
                    obj.Set("fromMobTypes", string.Join(", ", collect.FromMobTypes.ToArray()));
                return obj;
            }

            var deliver = o as DeliverObjective;
            if (deliver != null)
            {
                obj.Set("itemType", deliver.ItemType);
                obj.Set("itemName", deliver.ItemName);
                if (deliver.ItemHue > 0) obj.Set("itemHue", deliver.ItemHue.ToString());
                if (deliver.GiveItemOnAccept) obj.Set("giveItemOnAccept", "true");
                obj.Set("targetNpcId", deliver.TargetNpcId);
                obj.Set("targetNpcName", deliver.TargetNpcName);
                obj.Set("onItemLostAction", deliver.OnItemLostAction);
                return obj;
            }

            var talk = o as TalkToObjective;
            if (talk != null)
            {
                obj.Set("targetNpcId", talk.TargetNpcId);
                obj.Set("targetNpcName", talk.TargetNpcName);
                obj.Set("speechTrigger", talk.SpeechTrigger);

                if (talk.GrantItems != null && talk.GrantItems.Count > 0)
                {
                    var parts = new List<string>();

                    foreach (var g in talk.GrantItems)
                    {
                        if (g == null) continue;

                        parts.Add((g.ItemType ?? "") + "|" + (g.ItemName ?? "") + "|" +
                                  (g.Amount > 0 ? g.Amount.ToString() : "1"));
                    }

                    obj.Set("grantItems", string.Join(";", parts.ToArray()));
                }

                return obj;
            }

            var escort = o as EscortObjective;
            if (escort != null)
            {
                obj.Set("escortNpcId", escort.EscortNpcId);
                obj.Set("escortNpcName", escort.EscortNpcName);
                if (!escort.SpawnOnAccept) obj.Set("spawnOnAccept", "false");
                if (escort.SpawnX != 0) obj.Set("spawnX", escort.SpawnX.ToString());
                if (escort.SpawnY != 0) obj.Set("spawnY", escort.SpawnY.ToString());
                if (escort.SpawnZ != 0) obj.Set("spawnZ", escort.SpawnZ.ToString());
                obj.Set("spawnMap", escort.SpawnMap);
                obj.Set("destinationName", escort.DestinationName);
                obj.Set("destinationRegion", escort.DestinationRegion);
                if (escort.DestinationX != 0) obj.Set("destinationX", escort.DestinationX.ToString());
                if (escort.DestinationY != 0) obj.Set("destinationY", escort.DestinationY.ToString());
                if (escort.DestinationZ != 0) obj.Set("destinationZ", escort.DestinationZ.ToString());
                obj.Set("destinationMap", escort.DestinationMap);
                if (escort.RadiusToComplete > 0) obj.Set("radiusToComplete", escort.RadiusToComplete.ToString());
                obj.Set("onEscortDead", escort.OnEscortDead);
                if (escort.EscortBody > 0) obj.Set("escortBody", escort.EscortBody.ToString());
                if (escort.EscortHue > 0) obj.Set("escortHue", escort.EscortHue.ToString());
                if (escort.EscortHits > 0) obj.Set("escortHits", escort.EscortHits.ToString());
                if (escort.DefendsItself) obj.Set("defendsItself", "true");
                if (!escort.TeleportWhenStuck) obj.Set("teleportWhenStuck", "false");
                if (escort.MaxDistanceFromOwner > 0) obj.Set("maxDistanceFromOwner", escort.MaxDistanceFromOwner.ToString());
                if (escort.StuckTimeoutSeconds > 0) obj.Set("stuckTimeoutSeconds", escort.StuckTimeoutSeconds.ToString());
                return obj;
            }

            var defend = o as DefendObjective;
            if (defend != null)
            {
                obj.Set("enemyType", defend.EnemyType);
                if (defend.EnemiesPerWave > 0) obj.Set("enemiesPerWave", defend.EnemiesPerWave.ToString());
                if (defend.TotalWaves > 0) obj.Set("totalWaves", defend.TotalWaves.ToString());
                if (defend.TimeBetweenWavesSeconds > 0) obj.Set("timeBetweenWavesSeconds", defend.TimeBetweenWavesSeconds.ToString());
                obj.Set("locationName", defend.LocationName);
                if (defend.LocationX != 0) obj.Set("locationX", defend.LocationX.ToString());
                if (defend.LocationY != 0) obj.Set("locationY", defend.LocationY.ToString());
                obj.Set("mapName", defend.MapName);
                if (defend.RadiusTiles > 0) obj.Set("radiusTiles", defend.RadiusTiles.ToString());
                return obj;
            }

            var destroy = o as DestroyObjective;
            if (destroy != null)
            {
                obj.Set("targetType", destroy.TargetType);
                obj.Set("targetName", destroy.TargetName);
                if (destroy.RespawnOnFail) obj.Set("respawnOnFail", "true");
                if (destroy.TargetSerials != null && destroy.TargetSerials.Count > 0)
                {
                    var parts = new List<string>();
                    foreach (var serial in destroy.TargetSerials) parts.Add(serial.Value.ToString());
                    obj.Set("targetSerials", string.Join(", ", parts.ToArray()));
                }
                return obj;
            }

            var explore = o as ExploreObjective;
            if (explore != null)
            {
                obj.Set("locationName", explore.LocationName);
                if (explore.LocationX != 0) obj.Set("locationX", explore.LocationX.ToString());
                if (explore.LocationY != 0) obj.Set("locationY", explore.LocationY.ToString());
                if (explore.LocationZ != 0) obj.Set("locationZ", explore.LocationZ.ToString());
                obj.Set("mapName", explore.MapName);
                if (explore.RadiusTiles > 0) obj.Set("radiusTiles", explore.RadiusTiles.ToString());
                if (explore.StayDurationSeconds > 0) obj.Set("stayDurationSeconds", explore.StayDurationSeconds.ToString());
                if (!explore.TriggerOnEnter) obj.Set("triggerOnEnter", "false");
                return obj;
            }

            var craft = o as CraftObjective;
            if (craft != null)
            {
                obj.Set("itemType", craft.ItemType);
                obj.Set("itemName", craft.ItemName);
                obj.Set("craftSkill", craft.CraftSkill);
                if (craft.RequireExceptional) obj.Set("requireExceptional", "true");
                if (craft.ConsumeOnComplete) obj.Set("consumeOnComplete", "true");
                return obj;
            }

            var useSkill = o as UseSkillObjective;
            if (useSkill != null)
            {
                obj.Set("skill", string.IsNullOrWhiteSpace(useSkill.SkillName) ? useSkill.Skill.ToString() : useSkill.SkillName);
                if (useSkill.RequireSuccess) obj.Set("requireSuccess", "true");
                return obj;
            }

            var gain = o as SkillGainObjective;
            if (gain != null)
            {
                obj.Set("skill", string.IsNullOrWhiteSpace(gain.SkillName) ? gain.Skill.ToString() : gain.SkillName);
                if (gain.TargetValue > 0) obj.Set("targetValue", gain.TargetValue.ToString());
                if (gain.StartValue > 0) obj.Set("startValue", gain.StartValue.ToString());
                obj.Set("mode", gain.Mode);
                return obj;
            }

            var interact = o as InteractObjective;
            if (interact != null)
            {
                obj.Set("targetType", interact.TargetType);
                obj.Set("targetName", interact.TargetName);
                obj.Set("interactionType", interact.InteractionType);
                obj.Set("speechTrigger", interact.SpeechTrigger);
                if (interact.TargetSerials != null && interact.TargetSerials.Count > 0)
                {
                    var parts = new List<string>();
                    foreach (var serial in interact.TargetSerials) parts.Add(serial.Value.ToString());
                    obj.Set("targetSerials", string.Join(", ", parts.ToArray()));
                }
                return obj;
            }

            var useItem = o as UseItemObjective;
            if (useItem != null)
            {
                obj.Set("itemType", useItem.ItemType);
                obj.Set("itemName", useItem.ItemName);
                if (useItem.ItemHue > 0) obj.Set("itemHue", useItem.ItemHue.ToString());
                if (useItem.ConsumeOnUse) obj.Set("consumeOnUse", "true");
                return obj;
            }

            var survive = o as SurviveObjective;
            if (survive != null)
            {
                if (survive.DurationSeconds > 0) obj.Set("durationSeconds", survive.DurationSeconds.ToString());
                obj.Set("requiredRegion", survive.RequiredRegion);
                if (!survive.FailOnDeath) obj.Set("failOnDeath", "false");
                return obj;
            }

            return obj;
        }

        // ═══════════════════════════════════════════════════════════════
        //  HELPERS DE JSON
        // ═══════════════════════════════════════════════════════════════

        private static string[] SplitLines(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new string[0];

            var result = new List<string>();

            foreach (string part in text.Split('|'))
            {
                string trimmed = part.Trim();
                if (trimmed.Length > 0)
                    result.Add(trimmed);
            }

            return result.ToArray();
        }

        private static void JsonStringArray(StringBuilder sb, string[] values)
        {
            sb.Append("[");

            if (values != null)
            {
                for (int i = 0; i < values.Length; i++)
                {
                    sb.Append("\"").Append(Esc(values[i])).Append("\"");
                    if (i < values.Length - 1) sb.Append(", ");
                }
            }

            sb.Append("]");
        }

        private static void Line(StringBuilder sb, string key, string value, bool comma)
        {
            sb.Append("    \"").Append(key).Append("\": \"").Append(Esc(value)).Append("\"");
            if (comma) sb.Append(",");
            sb.AppendLine();
        }

        private static string Bool(bool value)
        {
            return value ? "true" : "false";
        }

        private static string Num(double value)
        {
            return value.ToString("0.####", CultureInfo.InvariantCulture);
        }

        private static string IntOrRaw(string value)
        {
            int parsed;
            if (int.TryParse(value, out parsed))
                return parsed.ToString();

            // 0x190 e afins
            try
            {
                if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    return Convert.ToInt32(value.Substring(2), 16).ToString();
            }
            catch
            {
                // cai no valor textual
            }

            return "\"" + Esc(value) + "\"";
        }

        private static string SafeInt(string s)
        {
            int val;

            if (int.TryParse(s, out val))
                return val.ToString();

            try
            {
                if (!string.IsNullOrEmpty(s) && s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    return Convert.ToInt32(s.Substring(2), 16).ToString();
            }
            catch
            {
                // ignora
            }

            return "0";
        }

        private static string DefaultIfEmpty(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private static string Esc(string s)
        {
            if (string.IsNullOrEmpty(s))
                return "";

            return s.Replace("\\", "\\\\").Replace("\"", "\\\"")
                    .Replace("\n", "\\n").Replace("\r", "").Replace("\t", " ");
        }
    }
}
