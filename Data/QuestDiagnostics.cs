/*****************************************************
 * QuestDiagnostics.cs
 * Comando [questdiag — diagnostico de alcançabilidade
 *
 * Por que existe:
 *   "Algumas missões não podem ser concluídas" tem poucas causas raiz, e todas
 *   são detectáveis estaticamente a partir da definição + do mundo:
 *
 *     a) objetivo sem alvo declarado (targetType/itemType/npcId vazios) — o
 *        evento acontece, mas nada casa, e o progresso fica em 0 para sempre;
 *     b) tipo de item/criatura que não existe no ServUO nem é item virtual de
 *        quest — ScriptCompiler.FindTypeByName devolve null e o crédito é
 *        silently descartado;
 *     c) NPC giver/turn-in/alvo sem binding — o JSON diz "npc_sera", mas nenhum
 *        Mobile no mundo carrega esse QuestNpcId, então ninguém oferece nem
 *        recebe a quest;
 *     d) escolta sem destino resolvível — o NPC anda para sempre;
 *     e) pré-requisito apontando para quest que não existe — a quest fica
 *        permanentemente inalcançável;
 *     f) required <= 0, ID duplicado, cadeia quebrada.
 *
 *   Este comando roda TODAS essas checagens e imprime o motivo exato, em vez de
 *   deixar o GM adivinhar olhando o JSON.
 *
 * Uso:
 *   [questdiag              diagnóstico completo
 *   [questdiag <questId>    só uma quest (detalhado)
 *   [questdiag npc          só o relatório de binding de NPCs
 *
 * Versao: 1.0.0
 * Compativel com: ServUO
 *****************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Commands;

namespace Server.Custom.QuestSystem.Quests
{
    /// <summary>
    /// Diagnóstico estático das quests carregadas + do binding de NPCs no mundo.
    /// </summary>
    public static class QuestDiagnostics
    {
        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized)
                return;

            _initialized = true;

            CommandSystem.Register("questdiag", AccessLevel.GameMaster, QuestDiag_OnCommand);
        }

        #region Comando

        [Usage("questdiag [questId|npc]")]
        [Description("Diagnostica por que uma quest nao pode ser concluida.")]
        private static void QuestDiag_OnCommand(CommandEventArgs e)
        {
            Mobile from = e.Mobile;

            if (from == null)
                return;

            string filter = e.Arguments != null && e.Arguments.Length > 0 ? e.Arguments[0] : null;

            if (filter != null && filter.Equals("npc", StringComparison.OrdinalIgnoreCase))
            {
                ReportNpcBinding(from);
                return;
            }

            Run(from, filter);
        }

        #endregion

        #region Relatório principal

        /// <summary>
        /// Executa o diagnóstico e imprime no chat do GM.
        /// </summary>
        public static void Run(Mobile from, string questIdFilter = null)
        {
            var quests = QuestRepository.Instance.GetAllQuests() ?? new List<QuestDefinition>();

            if (!string.IsNullOrWhiteSpace(questIdFilter))
            {
                quests = quests.Where(q => q != null &&
                    q.Id.IndexOf(questIdFilter, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            }

            from.SendMessage(0x480, "━━━━━━━━━━ [questdiag] ━━━━━━━━━━");
            from.SendMessage(0x480, "Quests analisadas: " + quests.Count);

            int blocking = 0;
            int warnings = 0;
            int healthy = 0;

            // 1) Avisos do carregador (JSON malformado, campos ignorados...)
            var loaderWarnings = QuestLoader.Warnings;

            if (loaderWarnings != null && loaderWarnings.Count > 0)
            {
                from.SendMessage(0x26, "");
                from.SendMessage(0x26, "── Avisos do carregador (" + loaderWarnings.Count + ") ──");

                int shown = 0;

                foreach (string w in loaderWarnings)
                {
                    if (string.IsNullOrWhiteSpace(w))
                        continue;

                    // No filtro por quest, só mostra o que a menciona
                    if (!string.IsNullOrWhiteSpace(questIdFilter) &&
                        w.IndexOf(questIdFilter, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    if (shown++ >= 40)
                    {
                        from.SendMessage(0x26, "  ... e mais " + (loaderWarnings.Count - shown) + " aviso(s).");
                        break;
                    }

                    from.SendMessage(0x26, "  • " + Trunc(w, 150));
                }
            }

            // 2) Validação estrutural (IDs duplicados, required <= 0, cadeias)
            var structural = QuestLoader.ValidateQuests(quests);

            if (structural != null && structural.Count > 0)
            {
                from.SendMessage(0x22, "");
                from.SendMessage(0x22, "── Problemas estruturais (" + structural.Count + ") ──");

                for (int i = 0; i < structural.Count && i < 40; i++)
                    from.SendMessage(0x22, "  ✗ " + Trunc(structural[i], 150));

                blocking += structural.Count;
            }

            // 3) Binding de NPCs no mundo (índice único para todas as quests)
            var bound = CollectBoundNpcs();

            // 4) Análise de alcançabilidade por quest
            from.SendMessage(0x480, "");
            from.SendMessage(0x480, "── Alcançabilidade ──");

            foreach (var quest in quests)
            {
                if (quest == null)
                    continue;

                var report = AnalyzeQuest(quest, bound);

                if (report.Blocking.Count == 0 && report.Warnings.Count == 0)
                {
                    healthy++;
                    continue;   // não polui o chat com o que está saudável
                }

                blocking += report.Blocking.Count;
                warnings += report.Warnings.Count;

                int hue = report.Blocking.Count > 0 ? 0x22 : 0x26;

                from.SendMessage(hue, "");
                from.SendMessage(hue, "▸ " + quest.Id + " — " + quest.Name);

                foreach (string b in report.Blocking)
                    from.SendMessage(0x22, "    ✗ IMPOSSIVEL: " + Trunc(b, 140));

                foreach (string w in report.Warnings)
                    from.SendMessage(0x26, "    ! " + Trunc(w, 140));

                foreach (string h in report.Hints)
                    from.SendMessage(0x3B1, "    → " + Trunc(h, 140));
            }

            // 5) Resumo
            from.SendMessage(0x480, "");
            from.SendMessage(0x480, "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            if (blocking == 0 && warnings == 0)
                from.SendMessage(0x40, "  ✔ " + healthy + " quest(s) OK — nenhuma impossivel de concluir.");
            else
            {
                from.SendMessage(0x22, "  ✗ " + blocking + " bloqueante(s)");
                from.SendMessage(0x26, "  ! " + warnings + " aviso(s)");
                from.SendMessage(0x40, "  ✔ " + healthy + " quest(s) saudaveis");
            }

            from.SendMessage(0x480, "  NPCs com binding: " + bound.Count);
            from.SendMessage(0x480, "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            Console.WriteLine("[questdiag] blocking=" + blocking + " warnings=" + warnings +
                              " healthy=" + healthy + " npcsBound=" + bound.Count);
        }

        #endregion

        #region Análise de uma quest

        /// <summary>Resultado do diagnóstico de uma quest.</summary>
        public class QuestReport
        {
            public readonly List<string> Blocking = new List<string>();
            public readonly List<string> Warnings = new List<string>();
            public readonly List<string> Hints = new List<string>();
        }

        /// <summary>
        /// Analisa uma definição e devolve o que impede (ou pode impedir) a conclusão.
        /// Público para que testes/outras ferramentas reaproveitem a mesma lógica.
        /// </summary>
        public static QuestReport AnalyzeQuest(QuestDefinition quest, HashSet<string> boundNpcs)
        {
            var report = new QuestReport();

            if (quest == null)
            {
                report.Blocking.Add("Definição nula.");
                return report;
            }

            boundNpcs = boundNpcs ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // ── Identidade ────────────────────────────────────────────────
            if (string.IsNullOrWhiteSpace(quest.Id))
                report.Blocking.Add("Sem ID — o repositório descarta a quest.");

            if (string.IsNullOrWhiteSpace(quest.Name))
                report.Warnings.Add("Sem nome — aparece em branco no log.");

            // ── Objetivos ─────────────────────────────────────────────────
            if (quest.Objectives == null || quest.Objectives.Count == 0)
            {
                report.Blocking.Add("Nenhum objetivo — nunca pode ser concluída.");
            }
            else
            {
                for (int i = 0; i < quest.Objectives.Count; i++)
                    AnalyzeObjective(quest, quest.Objectives[i], i, report, boundNpcs);
            }

            // Se todos os objetivos são opcionais, CheckCompletion nunca fecha
            if (quest.Objectives != null && quest.Objectives.Count > 0 &&
                quest.Objectives.All(o => o != null && o.IsOptional))
            {
                report.Blocking.Add("Todos os objetivos são opcionais — nada é obrigatório, " +
                                    "a conclusão nunca dispara.");
            }

            // ── NPCs de oferecimento e entrega ────────────────────────────
            AnalyzeNpcBinding(quest.GiverNpcId, quest.GiverNpcName, "Giver", boundNpcs, report);
            AnalyzeNpcBinding(quest.TurnInNpcId, quest.TurnInNpcName, "Entrega", boundNpcs, report);

            if (string.IsNullOrWhiteSpace(quest.GiverNpcId) &&
                string.IsNullOrWhiteSpace(quest.GiverNpcName) && !quest.AutoStart)
            {
                report.Blocking.Add("Sem NPC giver e sem autoStart — nenhum jogador consegue pegá-la.");
            }

            // ── Pré-requisitos ────────────────────────────────────────────
            if (quest.Prerequisites != null)
                AnalyzePrerequisites(quest, report);

            // ── Cadeia ────────────────────────────────────────────────────
            if (!string.IsNullOrWhiteSpace(quest.NextQuestId) &&
                QuestRepository.Instance.GetQuest(quest.NextQuestId) == null)
            {
                report.Warnings.Add("nextQuest '" + quest.NextQuestId +
                                    "' não está carregado — a cadeia para aqui.");
            }

            // ── Limite de tempo ───────────────────────────────────────────
            if (quest.HasTimeLimit && quest.TimeLimitMinutes <= 0)
                report.Blocking.Add("Limite de tempo ativo com 0 minutos — expira no ato de aceitar.");

            if (quest.HasTimeLimit && quest.Objectives != null)
            {
                // Escolta/defesa/sobrevivência com prazo curto demais é armadilha
                foreach (var o in quest.Objectives)
                {
                    var survive = o as SurviveObjective;

                    if (survive != null && survive.DurationSeconds > quest.TimeLimitMinutes * 60)
                    {
                        report.Blocking.Add("Survive exige " + survive.DurationSeconds +
                                            "s mas o prazo é de " + quest.TimeLimitMinutes +
                                            "min — matematicamente impossível.");
                    }
                }
            }

            return report;
        }

        #endregion

        #region Análise por tipo de objetivo

        private static void AnalyzeObjective(QuestDefinition quest, QuestObjective o, int index,
                                             QuestReport report, HashSet<string> boundNpcs)
        {
            if (o == null)
            {
                report.Blocking.Add("Objetivo " + (index + 1) + " é nulo.");
                return;
            }

            string tag = "Obj " + (index + 1) + " [" + o.Type + "]";

            if (o.Required <= 0)
                report.Blocking.Add(tag + ": required=" + o.Required + " — nunca atinge a meta.");

            if (string.IsNullOrWhiteSpace(o.Description))
                report.Warnings.Add(tag + ": sem descrição — o jogador não sabe o que fazer.");

            var kill = o as KillObjective;
            if (kill != null)
            {
                bool any = !string.IsNullOrWhiteSpace(kill.TargetType) ||
                           !string.IsNullOrWhiteSpace(kill.TargetName) ||
                           (kill.TargetTypes != null && kill.TargetTypes.Count > 0);

                if (!any)
                {
                    report.Warnings.Add(tag + ": sem alvo declarado — QUALQUER criatura conta.");
                    return;
                }

                CheckCreatureType(kill.TargetType, tag + " targetType", report);

                if (kill.TargetTypes != null)
                    foreach (string t in kill.TargetTypes)
                        CheckCreatureType(t, tag + " targetTypes", report);

                return;
            }

            var collect = o as CollectObjective;
            if (collect != null)
            {
                if (CheckItemType(collect.ItemType, collect.ItemName, tag + " itemType", report))
                    CheckVirtualItemHasSource(quest, collect.ItemType, tag + " itemType", report);

                if (collect.FromMobTypes != null && collect.FromMobTypes.Count > 0)
                {
                    foreach (string mob in collect.FromMobTypes)
                        CheckCreatureType(mob, tag + " fromMobTypes", report);

                    if (collect.DropChance <= 0)
                        report.Warnings.Add(tag + ": fromMobTypes declarado com dropChance=0 — nunca dropa.");
                }

                return;
            }

            var deliver = o as DeliverObjective;
            if (deliver != null)
            {
                // giveItemOnAccept cria o item virtual ao aceitar; sem isso ele
                // precisa vir de outra etapa da quest.
                if (!deliver.GiveItemOnAccept &&
                    CheckItemType(deliver.ItemType, deliver.ItemName, tag + " itemType", report))
                {
                    CheckVirtualItemHasSource(quest, deliver.ItemType, tag + " itemType", report);
                }

                if (string.IsNullOrWhiteSpace(deliver.TargetNpcId) &&
                    string.IsNullOrWhiteSpace(deliver.TargetNpcName))
                {
                    report.Blocking.Add(tag + ": sem NPC de destino — não há a quem entregar.");
                }
                else
                {
                    AnalyzeNpcBinding(deliver.TargetNpcId, deliver.TargetNpcName,
                                      tag + " NPC alvo", boundNpcs, report);
                }

                return;
            }

            var talk = o as TalkToObjective;
            if (talk != null)
            {
                if (string.IsNullOrWhiteSpace(talk.TargetNpcId) &&
                    string.IsNullOrWhiteSpace(talk.TargetNpcName))
                {
                    report.Blocking.Add(tag + ": sem NPC alvo — a fala nunca casa.");
                }
                else
                {
                    AnalyzeNpcBinding(talk.TargetNpcId, talk.TargetNpcName,
                                      tag + " NPC alvo", boundNpcs, report);
                }

                return;
            }

            var escort = o as EscortObjective;
            if (escort != null)
            {
                AnalyzeEscort(escort, tag, report);
                return;
            }

            var defend = o as DefendObjective;
            if (defend != null)
            {
                CheckCreatureType(defend.EnemyType, tag + " enemyType", report);

                if (defend.TotalWaves <= 0)
                    report.Blocking.Add(tag + ": totalWaves=" + defend.TotalWaves + " — nenhuma onda é gerada.");

                if (defend.EnemiesPerWave <= 0)
                    report.Blocking.Add(tag + ": enemiesPerWave=" + defend.EnemiesPerWave +
                                        " — as ondas nascem vazias.");

                if (defend.LocationX == 0 && defend.LocationY == 0 &&
                    string.IsNullOrWhiteSpace(defend.LocationName))
                {
                    report.Warnings.Add(tag + ": sem local — as ondas nascem onde o jogador estiver.");
                }

                return;
            }

            var destroy = o as DestroyObjective;
            if (destroy != null)
            {
                bool hasSerials = destroy.TargetSerials != null && destroy.TargetSerials.Count > 0;

                if (!hasSerials)
                    CheckItemType(destroy.TargetType, destroy.TargetName, tag + " targetType", report);

                if (!hasSerials && string.IsNullOrWhiteSpace(destroy.TargetType) &&
                    string.IsNullOrWhiteSpace(destroy.TargetName))
                {
                    report.Blocking.Add(tag + ": sem alvo (nem tipo, nem seriais) — nada será destruído.");
                }

                return;
            }

            var explore = o as ExploreObjective;
            if (explore != null)
            {
                bool hasCoords = explore.LocationX != 0 || explore.LocationY != 0;
                bool hasRegion = !string.IsNullOrWhiteSpace(explore.LocationName);

                if (!hasCoords && !hasRegion)
                    report.Blocking.Add(tag + ": sem coordenadas nem região — nunca dispara.");
                else if (hasRegion)
                {
                    CheckRegion(explore.LocationName, tag + " locationName", report);

                    // Armadilha silenciosa: com coordenadas E locationName,
                    // IsInsideExploreArea exige OS DOIS (AND, não OR). E o parser
                    // aceita "locationHint" como alias de locationName — então um
                    // GM que escreve uma dica de texto ali transforma a dica em
                    // exigência de região, e o objetivo nunca completa.
                    if (hasCoords)
                    {
                        report.Warnings.Add(tag + ": coordenadas E locationName '" +
                                            explore.LocationName + "' declarados juntos — o motor " +
                                            "exige os DOIS (AND). Se locationName não for uma região " +
                                            "real, o objetivo nunca completa. Cuidado: 'locationHint' " +
                                            "é alias de 'locationName' em Explore/Defend.");
                    }
                }

                if (explore.RadiusTiles <= 0 && hasCoords)
                    report.Warnings.Add(tag + ": radiusTiles=0 — exige pisar no tile exato.");

                return;
            }

            var craft = o as CraftObjective;
            if (craft != null)
            {
                CheckItemType(craft.ItemType, craft.ItemName, tag + " itemType", report);
                CheckCraftSkill(craft.CraftSkill, tag + " craftSkill", report);

                return;
            }

            var useSkill = o as UseSkillObjective;
            if (useSkill != null)
            {
                CheckSkill(useSkill.SkillName, useSkill.Skill, tag + " skill", report);
                return;
            }

            var gain = o as SkillGainObjective;
            if (gain != null)
            {
                CheckSkill(gain.SkillName, gain.Skill, tag + " skill", report);

                if (gain.Mode != null &&
                    gain.Mode.Equals("ReachValue", StringComparison.OrdinalIgnoreCase) &&
                    gain.TargetValue <= 0)
                {
                    report.Blocking.Add(tag + ": mode=ReachValue com targetValue=0 — completa no ato " +
                                        "(ou nunca, se o valor inicial já for maior).");
                }

                return;
            }

            var interact = o as InteractObjective;
            if (interact != null)
            {
                bool hasSerials = interact.TargetSerials != null && interact.TargetSerials.Count > 0;

                if (!hasSerials && string.IsNullOrWhiteSpace(interact.TargetType) &&
                    string.IsNullOrWhiteSpace(interact.TargetName) &&
                    string.IsNullOrWhiteSpace(interact.SpeechTrigger))
                {
                    report.Blocking.Add(tag + ": sem alvo de interação — nada será detectado.");
                }
                else if (!hasSerials)
                {
                    CheckItemType(interact.TargetType, interact.TargetName, tag + " targetType", report);
                }

                return;
            }

            var useItem = o as UseItemObjective;
            if (useItem != null)
            {
                if (CheckItemType(useItem.ItemType, useItem.ItemName, tag + " itemType", report))
                    CheckVirtualItemHasSource(quest, useItem.ItemType, tag + " itemType", report);
                return;
            }

            var survive = o as SurviveObjective;
            if (survive != null)
            {
                if (survive.DurationSeconds <= 0)
                    report.Blocking.Add(tag + ": durationSeconds=" + survive.DurationSeconds +
                                        " — nunca acumula tempo.");

                if (!string.IsNullOrWhiteSpace(survive.RequiredRegion))
                    CheckRegion(survive.RequiredRegion, tag + " requiredRegion", report);

                return;
            }

            report.Warnings.Add(tag + ": tipo sem checagem específica no diagnóstico.");
        }

        /// <summary>
        /// Escolta é o objetivo mais frágil do sistema: sem destino o NPC anda
        /// para sempre, e sem spawn o objetivo nem começa.
        /// </summary>
        private static void AnalyzeEscort(EscortObjective escort, string tag, QuestReport report)
        {
            bool hasCoords = escort.DestinationX != 0 || escort.DestinationY != 0;
            bool hasRegion = !string.IsNullOrWhiteSpace(escort.DestinationRegion);

            if (!hasCoords && !hasRegion)
            {
                report.Blocking.Add(tag + ": SEM DESTINO (nem coordenadas, nem destinationRegion) — " +
                                    "o NPC escoltado nunca chega e a quest nunca completa.");
                report.Hints.Add("Defina destinationRegion (ex.: \"Britain\") ou destinationX/Y/Z.");
            }
            else if (hasRegion && !hasCoords)
            {
                CheckRegion(escort.DestinationRegion, tag + " destinationRegion", report);
            }

            if (hasCoords && escort.RadiusToComplete <= 0)
                report.Warnings.Add(tag + ": radiusToComplete=0 — exige chegar ao tile exato.");

            if (string.IsNullOrWhiteSpace(escort.EscortNpcName) &&
                string.IsNullOrWhiteSpace(escort.EscortNpcId))
            {
                report.Warnings.Add(tag + ": sem nome para o escoltado — aparece como \"(sem nome)\".");
            }

            if (!escort.SpawnOnAccept && (escort.SpawnX == 0 && escort.SpawnY == 0))
                report.Warnings.Add(tag + ": spawnOnAccept=false sem NPC existente no mundo — " +
                                    "verifique se o escoltado já está spawnado.");

            if (escort.StuckTimeoutSeconds > 0 && !escort.TeleportWhenStuck)
            {
                report.Warnings.Add(tag + ": teleportWhenStuck=false — se o NPC travar em geometria, " +
                                    "a quest fica parada até o jogador desistir.");
            }
        }

        #endregion

        #region Checagens reutilizáveis

        /// <summary>
        /// Um tipo de criatura que não existe no shard só casa por nome exibido,
        /// o que é frágil (boss renomeado, traduções). Vale avisar.
        /// </summary>
        private static void CheckCreatureType(string typeSpec, string label, QuestReport report)
        {
            if (string.IsNullOrWhiteSpace(typeSpec))
                return;

            // ResolveAnyType, não ResolveType: este último descarta tudo que não
            // for Item, então devolveria null para toda criatura.
            Type resolved = QuestItemMatcher.ResolveAnyType(typeSpec);

            if (resolved == null)
            {
                report.Warnings.Add(label + " '" + typeSpec + "' não resolve para nenhuma classe — " +
                                    "só casará pelo NOME exibido da criatura.");
                return;
            }

            if (!typeof(Mobile).IsAssignableFrom(resolved))
            {
                report.Warnings.Add(label + " '" + typeSpec + "' resolve para " + resolved.Name +
                                    ", que não é uma criatura — o abate nunca será creditado.");
            }
        }

        /// <summary>
        /// Itens de quest virtuais (SeloFortaleza, CartaLacradaOlin...) NÃO têm
        /// classe C# e são criados pelo próprio sistema — esses são válidos, mas
        /// só existem se ALGUÉM os entregar ao jogador. Um item virtual sem fonte
        /// de obtenção torna o objetivo impossível, e é isso que se checa aqui.
        /// </summary>
        /// <returns>True quando o typeSpec é um item virtual (sem classe C#).</returns>
        private static bool CheckItemType(string typeSpec, string displayName, string label,
                                          QuestReport report)
        {
            bool hasType = !string.IsNullOrWhiteSpace(typeSpec);
            bool hasName = !string.IsNullOrWhiteSpace(displayName);

            if (!hasType && !hasName)
            {
                report.Blocking.Add(label + ": vazio — o objetivo não tem o que procurar.");
                return false;
            }

            if (!hasType)
            {
                report.Warnings.Add(label + ": sem itemType, só itemName '" + displayName +
                                    "' — casa apenas pelo nome, e ignora itens com nome parecido.");
                return false;
            }

            if (QuestItemMatcher.ResolveType(typeSpec) != null)
                return false;   // classe real de Item — tudo certo

            if (QuestItemMatcher.ResolveAnyType(typeSpec) != null)
            {
                report.Blocking.Add(label + " '" + typeSpec + "' existe no shard mas NÃO é um Item — " +
                                    "nunca poderá estar na mochila do jogador.");
                return false;
            }

            // Item virtual: válido, mas depende de uma fonte de obtenção
            return true;
        }

        /// <summary>
        /// Um item virtual precisa chegar à mochila do jogador por algum caminho
        /// declarado na própria quest; senão o objetivo Collect/Deliver/UseItem
        /// fica impossível de concluir.
        /// </summary>
        private static void CheckVirtualItemHasSource(QuestDefinition quest, string typeSpec,
                                                      string label, QuestReport report)
        {
            if (quest == null || quest.Objectives == null || string.IsNullOrWhiteSpace(typeSpec))
                return;

            string wanted = QuestItemMatcher.Normalize(typeSpec);

            foreach (var o in quest.Objectives)
            {
                // 1) Outra etapa da quest concede o item ao falar com o NPC
                var talk = o as TalkToObjective;

                if (talk != null && talk.GrantItems != null)
                {
                    foreach (var grant in talk.GrantItems)
                    {
                        if (grant == null)
                            continue;

                        if (SameVirtual(granted: grant.ItemType, displayName: grant.ItemName, wanted: wanted))
                            return;
                    }
                }

                // 2) O objetivo é de entrega e o sistema dá o item ao aceitar
                var deliver = o as DeliverObjective;

                if (deliver != null && deliver.GiveItemOnAccept &&
                    SameVirtual(deliver.ItemType, deliver.ItemName, wanted))
                    return;

                // 3) O item dropa de mobs declarados num Collect
                var collect = o as CollectObjective;

                if (collect != null && collect.FromMobTypes != null && collect.FromMobTypes.Count > 0 &&
                    collect.DropChance > 0 && SameVirtual(collect.ItemType, collect.ItemName, wanted))
                    return;
            }

            // 4) Recompensa de outra quest? Fora do escopo — só avisamos.
            report.Blocking.Add(label + " '" + typeSpec + "' é um item virtual (não existe no shard) e " +
                                "nenhuma etapa desta quest o entrega ao jogador — impossível de obter. " +
                                "Use grantItems num TalkTo, giveItemOnAccept num Deliver, ou fromMobTypes " +
                                "num Collect.");
        }

        private static bool SameVirtual(string granted, string displayName, string wanted)
        {
            if (string.IsNullOrWhiteSpace(wanted))
                return false;

            if (!string.IsNullOrWhiteSpace(granted) &&
                QuestItemMatcher.Normalize(granted) == wanted)
                return true;

            if (!string.IsNullOrWhiteSpace(displayName) &&
                QuestItemMatcher.Normalize(displayName) == wanted)
                return true;

            return false;
        }

        /// <summary>
        /// Em runtime o "sistema de craft" comparado é o nome da CLASSE DA
        /// FERRAMENTA (SewingKit, SmithyHammer...), não o nome do ofício.
        /// CraftSystemMatches aceita os dois, mas erro de grafia nos dois lados
        /// continua sendo objetivo impossível — e é isso que se aponta aqui.
        /// </summary>
        private static void CheckCraftSkill(string craftSkill, string label, QuestReport report)
        {
            if (string.IsNullOrWhiteSpace(craftSkill))
            {
                report.Warnings.Add(label + ": vazio — qualquer ofício conta.");
                return;
            }

            if (QuestManager.IsKnownCraftSkill(craftSkill))
                return;

            report.Blocking.Add(label + " '" + craftSkill + "' não é um ofício conhecido nem uma " +
                                "classe de ferramenta existente — o craft nunca será creditado. " +
                                "Use o ofício (Tailoring, Blacksmith, Carpentry, Fletching, Alchemy, " +
                                "Inscription, Tinkering, Cartography) ou a ferramenta (SewingKit, " +
                                "SmithyHammer, DovetailSaw, FletcherTools, MortarPestle, ScribesPen, " +
                                "TinkersTools, MapmakersPen).");
        }

        private static void CheckSkill(string skillName, SkillName skill, string label,
                                       QuestReport report)
        {
            if (!string.IsNullOrWhiteSpace(skillName))
            {
                SkillName parsed;

                if (Enum.TryParse(skillName, true, out parsed) && Enum.IsDefined(typeof(SkillName), parsed))
                    return;

                report.Blocking.Add(label + " '" + skillName + "' não é uma skill válida do ServUO — " +
                                    "o evento nunca dispara.");
                return;
            }

            if (skill == SkillName.Alchemy)
            {
                report.Warnings.Add(label + ": caiu no padrão Alchemy. Se a intenção era outra skill, " +
                                    "declare \"skill\": \"NomeExato\".");
            }
        }

        private static void CheckRegion(string regionName, string label, QuestReport report)
        {
            if (string.IsNullOrWhiteSpace(regionName))
                return;

            foreach (Map map in Map.Maps)
            {
                if (map == null || map.Regions == null)
                    continue;

                Region found;

                if (map.Regions.TryGetValue(regionName, out found) && found != null)
                    return;

                foreach (var pair in map.Regions)
                {
                    if (pair.Value != null && pair.Value.IsPartOf(regionName))
                        return;
                }
            }

            report.Warnings.Add(label + " '" + regionName + "' não foi encontrada em nenhum mapa — " +
                                "a condição de região nunca será satisfeita.");
        }

        /// <summary>
        /// O erro mais comum de todos: o JSON referencia um NPC lógico que
        /// nenhum Mobile no mundo carrega.
        /// </summary>
        private static void AnalyzeNpcBinding(string npcId, string npcName, string role,
                                              HashSet<string> boundNpcs, QuestReport report)
        {
            if (string.IsNullOrWhiteSpace(npcId))
            {
                if (!string.IsNullOrWhiteSpace(npcName))
                {
                    report.Warnings.Add(role + " identificado só pelo NOME '" + npcName +
                                        "'. Se o NPC for renomeado/recriado, a quest quebra. " +
                                        "Use [questnpcset npcid <id>.");
                }

                return;
            }

            if (boundNpcs.Contains(npcId))
                return;

            // Sem binding por ID: ainda pode funcionar por nome
            if (!string.IsNullOrWhiteSpace(npcName))
            {
                report.Blocking.Add(role + " '" + npcId + "' NÃO tem nenhum NPC vinculado no mundo. " +
                                    "Sobrevive só pelo nome '" + npcName + "'.");
                report.Hints.Add("Localize o NPC e rode: [questnpcset npcid " + npcId);
                return;
            }

            report.Blocking.Add(role + " '" + npcId + "' NÃO tem nenhum NPC vinculado no mundo — " +
                                "ninguém oferece/recebe esta quest.");
            report.Hints.Add("Localize o NPC e rode: [questnpcset npcid " + npcId);
        }

        private static void AnalyzePrerequisites(QuestDefinition quest, QuestReport report)
        {
            var p = quest.Prerequisites;

            if (p.RequiredQuests != null)
            {
                foreach (string required in p.RequiredQuests)
                {
                    if (string.IsNullOrWhiteSpace(required))
                        continue;

                    if (string.Equals(required, quest.Id, StringComparison.OrdinalIgnoreCase))
                    {
                        report.Blocking.Add("Exige a si mesma como pré-requisito — loop impossível.");
                        continue;
                    }

                    if (QuestRepository.Instance.GetQuest(required) == null)
                    {
                        report.Blocking.Add("Pré-requisito '" + required + "' não existe — " +
                                            "a quest fica permanentemente inalcançável.");
                    }
                }
            }

            if (p.RequiredSkills != null)
            {
                foreach (var req in p.RequiredSkills)
                {
                    if (req == null)
                        continue;

                    if (string.IsNullOrWhiteSpace(req.SkillName))
                        continue;

                    SkillName parsed;

                    if (!Enum.TryParse(req.SkillName, true, out parsed) ||
                        !Enum.IsDefined(typeof(SkillName), parsed))
                    {
                        report.Blocking.Add("Pré-requisito de skill '" + req.SkillName +
                                            "' inválido — ninguém jamais satisfaz a condição.");
                    }
                }
            }

            if (p.RequiredItems != null)
            {
                foreach (var req in p.RequiredItems)
                {
                    if (req == null)
                        continue;

                    if (CheckItemType(req.Type, req.Name, "Pré-requisito de item", report))
                    {
                        // Pode vir de uma quest anterior da cadeia, então não é
                        // bloqueante — mas se não vier, a quest fica inalcançável.
                        report.Warnings.Add("Pré-requisito exige o item virtual '" + req.Type +
                                            "', que não existe no shard. Garanta que alguma quest " +
                                            "anterior o entregue, senão ninguém satisfaz a condição.");
                    }
                }
            }

            if (p.MinKarma.HasValue && p.MaxKarma.HasValue && p.MinKarma.Value > p.MaxKarma.Value)
                report.Blocking.Add("minKarma (" + p.MinKarma + ") > maxKarma (" + p.MaxKarma +
                                    ") — janela vazia, ninguém entra.");
        }

        #endregion

        #region Binding de NPCs no mundo

        /// <summary>
        /// Varre o mundo uma única vez e coleta todos os IDs lógicos de NPC.
        /// </summary>
        public static HashSet<string> CollectBoundNpcs()
        {
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                foreach (Mobile m in World.Mobiles.Values)
                {
                    if (m == null || m.Deleted)
                        continue;

                    var identity = m as IQuestNpcIdentity;

                    if (identity != null && !string.IsNullOrWhiteSpace(identity.QuestNpcId))
                        ids.Add(identity.QuestNpcId);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[questdiag] Erro ao varrer NPCs: " + ex.Message);
            }

            return ids;
        }

        /// <summary>
        /// Relatório focado em binding: quais IDs o mundo tem, e quais as quests
        /// querem mas ninguém carrega.
        /// </summary>
        public static void ReportNpcBinding(Mobile from)
        {
            var quests = QuestRepository.Instance.GetAllQuests() ?? new List<QuestDefinition>();

            var bound = CollectBoundNpcs();
            var wanted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var q in quests)
            {
                if (q == null)
                    continue;

                Add(wanted, q.GiverNpcId);
                Add(wanted, q.TurnInNpcId);

                if (q.Objectives == null)
                    continue;

                foreach (var o in q.Objectives)
                {
                    var deliver = o as DeliverObjective;
                    if (deliver != null) Add(wanted, deliver.TargetNpcId);

                    var talk = o as TalkToObjective;
                    if (talk != null) Add(wanted, talk.TargetNpcId);

                    var escort = o as EscortObjective;
                    if (escort != null) Add(wanted, escort.EscortNpcId);
                }
            }

            from.SendMessage(0x480, "━━━━━━ [questdiag npc] ━━━━━━");
            from.SendMessage(0x480, "NPCs vinculados no mundo: " + bound.Count);
            from.SendMessage(0x480, "IDs exigidos pelas quests: " + wanted.Count);

            var missing = wanted.Where(id => !bound.Contains(id)).OrderBy(id => id).ToList();

            if (missing.Count == 0)
            {
                from.SendMessage(0x40, "✔ Todo NPC exigido está vinculado.");
            }
            else
            {
                from.SendMessage(0x22, "");
                from.SendMessage(0x22, "✗ SEM BINDING (" + missing.Count + "):");

                foreach (string id in missing)
                {
                    from.SendMessage(0x22, "   " + id);
                    from.SendMessage(0x3B1, "      → localize o NPC e rode: [questnpcset npcid " + id);
                }
            }

            var unused = bound.Where(id => !wanted.Contains(id)).OrderBy(id => id).ToList();

            if (unused.Count > 0)
            {
                from.SendMessage(0x26, "");
                from.SendMessage(0x26, "! Vinculados mas nenhuma quest usa (" + unused.Count + "):");

                for (int i = 0; i < unused.Count && i < 25; i++)
                    from.SendMessage(0x26, "   " + unused[i]);
            }

            from.SendMessage(0x480, "━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        }

        private static void Add(HashSet<string> set, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                set.Add(value);
        }

        #endregion

        #region Helpers

        private static string Trunc(string s, int max)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= max)
                return s ?? "";

            return s.Substring(0, Math.Max(0, max - 3)) + "...";
        }

        #endregion
    }
}
