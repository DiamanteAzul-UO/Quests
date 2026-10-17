/*****************************************************
 * QuestNPC.cs
 * NPC Base para Sistema de Quests
 * 
 * Sistema de conversa NATURAL via Say
 * Jogador fala normalmente, NPC responde naturalmente
 * 
 * Versão: 3.0.0
 * Compatível com: ServUO
 *****************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Custom.QuestSystem.Conversation;
using Server.Custom.QuestSystem.Dialogue;
using Server.Custom.QuestSystem.Quests;
using Server.Network;

namespace Server.Custom.QuestSystem.NPCs
{
    public class QuestNPC : BaseVendor, IQuestNpcIdentity
    {
        #region Constants

        private const int NPC_SPEECH_HUE = 0;
        private const int HINT_HUE = 0x40;
        private const int SUCCESS_HUE = 0x40;
        private const int WARNING_HUE = 0x151;

        private const int DELAY_SHORT = 3000;
        private const int DELAY_MEDIUM = 2500;
        private const int DELAY_LONG = 3500;

        private const int CONVERSATION_TIMEOUT = 120; // 2 minutos

        #endregion

        #region Speech Keywords

        // Palavras para iniciar conversa
        private static readonly string[] GREET_WORDS = { "ola", "oi", "hi", "hello", "bom dia", "boa tarde", "boa noite", "saudacoes", "hey" };

        // Palavras para perguntar quem é
        private static readonly string[] BIO_WORDS = { "quem e voce", "quem es tu", "seu nome", "como se chama", "quem é", "quem es", "apresente", "conte sobre voce", "fale de voce" };

        // Palavras para perguntar sobre o local
        private static readonly string[] LOCATION_WORDS = { "onde estou", "que lugar", "sobre este lugar", "esse lugar", "esta cidade", "essa cidade", "onde fica", "o que e aqui", "fale do local" };

        // Palavras para pedir missões
        private static readonly string[] QUEST_WORDS = { "missao", "missoes", "quest", "quests", "trabalho", "tarefa", "aventura", "ajuda", "precisa de ajuda", "o que posso fazer", "tem algo" };

        // Palavras para entregar missão
        private static readonly string[] TURNIN_WORDS = { "entregar", "completei", "terminei", "acabei", "fiz a missao", "voltei", "consegui", "pronto", "feito" };

        // Palavras para aceitar
        private static readonly string[] ACCEPT_WORDS = { "sim", "aceito", "claro", "com certeza", "pode ser", "vamos la", "topo", "ok", "beleza", "fechado", "combinado", "yes" };

        // Palavras para recusar
        private static readonly string[] DECLINE_WORDS = { "nao", "no", "agora nao", "depois", "talvez depois", "outra hora", "recuso", "nao quero", "passa" };

        // Palavras para despedir
        private static readonly string[] BYE_WORDS = { "tchau", "adeus", "ate logo", "ate mais", "bye", "goodbye", "falou", "flw", "vlw" };

        // Palavras para voltar
        private static readonly string[] BACK_WORDS = { "voltar", "volta", "outro", "outra", "mais opcoes", "o que mais", "menu" };

        #endregion

        #region Fields

        private NPCBehaviorConfig _behaviorConfig;
        private List<string> _questIds;
        private bool _isQuestGiver;
        private string _questNpcId;
        private Dictionary<Serial, ConversationState> _conversations;
        private DialogueData _dialogueData;

        #endregion

        #region Properties

        public NPCBehaviorConfig BehaviorConfig
        {
            get
            {
                if (_behaviorConfig == null)
                    _behaviorConfig = new NPCBehaviorConfig();
                return _behaviorConfig;
            }
            set => _behaviorConfig = value;
        }

        public List<string> QuestIds
        {
            get
            {
                if (_questIds == null)
                    _questIds = new List<string>();
                return _questIds;
            }
            set => _questIds = value;
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public bool IsQuestGiver
        {
            get => _isQuestGiver;
            set => _isQuestGiver = value;
        }

        /// <summary>
        /// Identificador estável do NPC usado pelos JSON de quest
        /// (ex.: "npc_capita_maelis", "npc_enfermeira_sera").
        ///
        /// Correção crítica: sem este campo, os objetivos TalkTo/Deliver e a
        /// entrega de quest só podiam casar por serial do NPC — que muda a cada
        /// respawn — ou por nome de exibição exato. Como consequência, quests
        /// como "Fale com Olin nas Docas" nunca recebiam progresso.
        /// </summary>
        [CommandProperty(AccessLevel.GameMaster)]
        public string QuestNpcId
        {
            get => _questNpcId;
            set => _questNpcId = value;
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public string Bio
        {
            get => BehaviorConfig.Bio;
            set => BehaviorConfig.Bio = value;
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public string LocationInfo
        {
            get => BehaviorConfig.LocationDescription;
            set => BehaviorConfig.LocationDescription = value;
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public int TalkRange
        {
            get => BehaviorConfig.MaxInteractionRange;
            set => BehaviorConfig.MaxInteractionRange = value;
        }

        public DialogueData DialogueData
        {
            get
            {
                if (_dialogueData == null)
                    _dialogueData = DialogueData.CreateDefault();
                return _dialogueData;
            }
            set => _dialogueData = value;
        }

        /// <summary>
        /// QuestNPC não vende nada — mas BaseVendor lê SBInfos a cada interação
        /// de compra/venda. Devolver "new List<SBInfo>()" alocava uma lista nova
        /// em CADA acesso (e o comprador iterava sobre ela). O padrão canônico do
        /// ServUO é uma instância readonly vazia e reutilizada.
        /// </summary>
        private readonly List<SBInfo> m_SBInfos = new List<SBInfo>();

        protected override List<SBInfo> SBInfos => m_SBInfos;

        #endregion

        #region Constructors

        public QuestNPC() : base(null)
        {
            InitializeQuestNPC();
        }

        public QuestNPC(string title) : base(title)
        {
            InitializeQuestNPC();
        }

        public QuestNPC(Serial serial) : base(serial)
        {
        }

        private void InitializeQuestNPC()
        {
            _behaviorConfig = new NPCBehaviorConfig();
            _questIds = new List<string>();
            _isQuestGiver = true;
            _conversations = new Dictionary<Serial, ConversationState>();
            _dialogueData = DialogueData.CreateDefault();
        }

        public override void InitSBInfo()
        {
        }

        #endregion

        #region Double Click

        public override void OnDoubleClick(Mobile from)
        {
            if (!(from is PlayerMobile player))
                return;

            if (!from.InRange(this.Location, BehaviorConfig.MaxInteractionRange))
            {
                SayTo(player, "*olha na sua direcao*");
                return;
            }

            if (!CanInteractWith(player))
                return;

            StartConversation(player);
        }

        #endregion

        #region Speech Handler

        public override void OnSpeech(SpeechEventArgs e)
        {
            base.OnSpeech(e);

            if (e.Handled)
                return;

            if (!(e.Mobile is PlayerMobile player))
                return;

            if (!CanInteractWith(player))
                return;

            if (!player.InRange(this.Location, BehaviorConfig.MaxInteractionRange + 3))
                return;

            string speech = NormalizeSpeech(e.Speech);

            // Verifica se tem conversa ativa
            var state = GetActiveConversation(player);

            if (state != null)
            {
                // Processa resposta
                ProcessSpeech(player, state, speech);
                e.Handled = true;
            }
            else
            {
                // Verifica se é saudação para iniciar conversa
                if (ContainsAny(speech, GREET_WORDS))
                {
                    StartConversation(player);
                    e.Handled = true;
                }
            }
        }

        #endregion

        #region Speech Processing

        private string NormalizeSpeech(string speech)
        {
            if (string.IsNullOrEmpty(speech))
                return "";

            // Remove acentos e converte para minúsculo
            speech = speech.ToLower().Trim();
            speech = speech.Replace("á", "a").Replace("à", "a").Replace("ã", "a").Replace("â", "a");
            speech = speech.Replace("é", "e").Replace("ê", "e");
            speech = speech.Replace("í", "i");
            speech = speech.Replace("ó", "o").Replace("ô", "o").Replace("õ", "o");
            speech = speech.Replace("ú", "u");
            speech = speech.Replace("ç", "c");
            speech = speech.Replace("?", "").Replace("!", "").Replace(".", "").Replace(",", "");

            return speech;
        }

        /// <summary>
        /// Verifica se a fala contém alguma das palavras-chave.
        ///
        /// Correção crítica: a versão anterior usava <c>speech.Contains(keyword)</c>,
        /// que é busca por substring pura. Consequências reais:
        ///  - "assim" continha "sim"      → o jogador aceitava a quest sem querer;
        ///  - "não sei" continha "nao"    → recusava sem querer;
        ///  - "onde fico" continha "fico" → disparava resposta errada.
        ///
        /// Agora a comparação é por PALAVRA (ou sequência de palavras), usando os
        /// tokens da fala. "sim" casa com "sim" e com "sim, aceito", mas não com
        /// "assim". Palavras compostas ("com certeza", "agora nao") continuam
        /// funcionando porque são tratadas como sequência de tokens.
        /// </summary>
        private bool ContainsAny(string speech, string[] keywords)
        {
            if (string.IsNullOrWhiteSpace(speech) || keywords == null)
                return false;

            string[] tokens = Tokenize(speech);

            if (tokens.Length == 0)
                return false;

            foreach (string keyword in keywords)
            {
                if (string.IsNullOrWhiteSpace(keyword))
                    continue;

                string[] wanted = Tokenize(NormalizeSpeech(keyword));

                if (wanted.Length == 0)
                    continue;

                if (ContainsSequence(tokens, wanted))
                    return true;
            }

            return false;
        }

        private static string[] Tokenize(string text)
        {
            return text.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static bool ContainsSequence(string[] tokens, string[] wanted)
        {
            if (wanted.Length > tokens.Length)
                return false;

            for (int i = 0; i + wanted.Length <= tokens.Length; i++)
            {
                bool ok = true;

                for (int j = 0; j < wanted.Length; j++)
                {
                    if (!string.Equals(tokens[i + j], wanted[j], StringComparison.Ordinal))
                    {
                        ok = false;
                        break;
                    }
                }

                if (ok)
                    return true;
            }

            return false;
        }

        private void ProcessSpeech(PlayerMobile player, ConversationState state, string speech)
        {
            // Reset timeout
            state.StartedAt = DateTime.UtcNow;

            // Despedida sempre funciona
            if (ContainsAny(speech, BYE_WORDS))
            {
                EndConversation(player);
                return;
            }

            // Processa baseado no estado atual
            switch (state.CurrentStep)
            {
                case ConversationStep.Greet:
                    ProcessGreetResponse(player, state, speech);
                    break;

                case ConversationStep.ShowBio:
                case ConversationStep.ShowLocation:
                    ProcessInfoResponse(player, state, speech);
                    break;

                case ConversationStep.ShowQuests:
                    ProcessQuestListResponse(player, state, speech);
                    break;

                case ConversationStep.QuestDetails:
                    ProcessQuestDetailResponse(player, state, speech);
                    break;

                case ConversationStep.TurnIn:
                    ProcessTurnInResponse(player, state, speech);
                    break;

                default:
                    ProcessGreetResponse(player, state, speech);
                    break;
            }
        }

        #endregion

        #region Conversation Management

        private ConversationState GetActiveConversation(PlayerMobile player)
        {
            if (_conversations.TryGetValue(player.Serial, out ConversationState state))
            {
                if ((DateTime.UtcNow - state.StartedAt).TotalSeconds > CONVERSATION_TIMEOUT)
                {
                    _conversations.Remove(player.Serial);
                    return null;
                }
                return state;
            }
            return null;
        }

        private ConversationState GetOrCreateState(PlayerMobile player)
        {
            if (!_conversations.TryGetValue(player.Serial, out ConversationState state))
            {
                state = new ConversationState(player);
                _conversations[player.Serial] = state;
            }
            state.StartedAt = DateTime.UtcNow;
            return state;
        }

        protected virtual bool CanInteractWith(PlayerMobile player)
        {
            if (player == null || player.Deleted || !player.Alive)
                return false;

            if (this.Deleted || !this.Alive)
                return false;

            if (player.NetState == null)
                return false;

            return true;
        }

        #endregion

        #region Start Conversation

        public void StartConversation(PlayerMobile player)
        {
            if (BehaviorConfig.FacePlayerOnTalk)
            {
                this.Direction = this.GetDirectionTo(player);
            }

            if (BehaviorConfig.PlaySoundOnGreet && BehaviorConfig.GreetSound > 0)
            {
                this.PlaySound(BehaviorConfig.GreetSound);
            }

            // Notifica objetivo de TalkTo
            QuestManager.Instance.OnTalkToNpc(player, this, this.Serial.ToString());

            var state = GetOrCreateState(player);
            state.CurrentStep = ConversationStep.Greet;

            // Saudação natural
            string[] greetings = new string[]
            {
                $"Ola, {player.Name}! Como posso ajudar?",
                $"Bem-vindo, {player.Name}! O que deseja?",
                $"Saudacoes, viajante! Em que posso ser util?",
                $"Ah, {player.Name}! Bom te ver por aqui."
            };

            SayTo(player, greetings[Utility.Random(greetings.Length)]);

            // Dica sutil
            Timer.DelayCall(TimeSpan.FromMilliseconds(DELAY_SHORT), delegate
            {
                SendHint(player, "Voce pode perguntar sobre mim, sobre este lugar, ou sobre missoes, aceitar ou entregar missao.");
            });
        }

        #endregion

        #region Greet Response

        private void ProcessGreetResponse(PlayerMobile player, ConversationState state, string speech)
        {
            if (ContainsAny(speech, BIO_WORDS))
            {
                ShowBio(player, state);
            }
            else if (ContainsAny(speech, LOCATION_WORDS))
            {
                ShowLocation(player, state);
            }
            else if (ContainsAny(speech, QUEST_WORDS))
            {
                ShowQuests(player, state);
            }
            else if (ContainsAny(speech, TURNIN_WORDS))
            {
                ShowTurnIn(player, state);
            }
            else if (ContainsAny(speech, GREET_WORDS))
            {
                // Já saudou, responde amigavelmente
                SayTo(player, "Ola novamente! O que gostaria de saber?");
            }
            else
            {
                // Não entendeu
                string[] confused = new string[]
                {
                    "Hmm, nao entendi muito bem...",
                    "Como disse?",
                    "Pode repetir de outra forma?"
                };
                SayTo(player, confused[Utility.Random(confused.Length)]);

                SendHint(player, "Tente perguntar: 'Quem e voce?' ou 'Tem missoes pra mim?'");
            }
        }

        #endregion

        #region Bio

        public void ShowBio(PlayerMobile player, ConversationState state)
        {
            state.CurrentStep = ConversationStep.ShowBio;

            string bio = BehaviorConfig.Bio ?? "Sou apenas mais um habitante deste mundo.";

            // NPC fala sua bio naturalmente
            string[] lines = WrapText(bio, 55);

            int delay = 0;
            foreach (string line in lines)
            {
                string capturedLine = line;
                Timer.DelayCall(TimeSpan.FromMilliseconds(delay), delegate
                {
                    SayTo(player, capturedLine);
                });
                delay += DELAY_MEDIUM;
            }

            Timer.DelayCall(TimeSpan.FromMilliseconds(delay), delegate
            {
                SendHint(player, "Pergunte mais algo ou diga 'tchau' para encerrar.");
            });
        }

        #endregion

        #region Location

        public void ShowLocation(PlayerMobile player, ConversationState state)
        {
            state.CurrentStep = ConversationStep.ShowLocation;

            string location = BehaviorConfig.LocationDescription ?? "Este e um lugar interessante.";

            string[] lines = WrapText(location, 55);

            int delay = 0;
            foreach (string line in lines)
            {
                string capturedLine = line;
                Timer.DelayCall(TimeSpan.FromMilliseconds(delay), delegate
                {
                    SayTo(player, capturedLine);
                });
                delay += DELAY_MEDIUM;
            }

            Timer.DelayCall(TimeSpan.FromMilliseconds(delay), delegate
            {
                SendHint(player, "Pergunte mais algo ou diga 'tchau' para encerrar.");
            });
        }

        private void ProcessInfoResponse(PlayerMobile player, ConversationState state, string speech)
        {
            // Pode perguntar outra coisa
            ProcessGreetResponse(player, state, speech);
        }

        #endregion

        #region Quest List

        public void ShowQuests(PlayerMobile player, ConversationState state)
        {
            state.CurrentStep = ConversationStep.ShowQuests;
            state.DisplayedQuests.Clear();

            if (QuestIds.Count == 0)
            {
                SayTo(player, "No momento nao tenho nenhuma tarefa para voce.");

                Timer.DelayCall(TimeSpan.FromMilliseconds(DELAY_MEDIUM), delegate
                {
                    SayTo(player, "Volte mais tarde, talvez surja algo.");
                    state.CurrentStep = ConversationStep.Greet;
                });
                return;
            }

            // Verifica quais quests estão disponíveis
            var availableQuests = new List<Tuple<string, QuestDefinition>>();

            foreach (string questId in QuestIds)
            {
                var definition = QuestRepository.Instance.GetQuest(questId);
                if (definition != null)
                {
                    var status = player.GetQuestStatus(questId);
                    if (status == QuestStatus.Available || status == QuestStatus.Active)
                    {
                        availableQuests.Add(new Tuple<string, QuestDefinition>(questId, definition));
                    }
                }
            }

            if (availableQuests.Count == 0)
            {
                SayTo(player, "Voce ja esta fazendo todas as minhas missoes, ou ja completou.");
                state.CurrentStep = ConversationStep.Greet;
                return;
            }

            if (availableQuests.Count == 1)
            {
                // Apenas uma quest - oferece direto
                var quest = availableQuests[0];
                state.RegisterDisplayedQuest(1, quest.Item1);
                state.SelectedQuestId = quest.Item1;

                SayTo(player, $"Tenho uma tarefa para voce: {quest.Item2.Name}");

                Timer.DelayCall(TimeSpan.FromMilliseconds(DELAY_MEDIUM), delegate
                {
                    ShowQuestDetails(player, state, quest.Item1, 1);
                });
            }
            else
            {
                // Múltiplas quests
                SayTo(player, $"Tenho {availableQuests.Count} tarefas disponiveis.");

                Timer.DelayCall(TimeSpan.FromMilliseconds(DELAY_MEDIUM), delegate
                {
                    player.SendMessage(HINT_HUE, "");
                    player.SendMessage(HINT_HUE, "Missoes disponiveis:");

                    int index = 1;
                    foreach (var quest in availableQuests)
                    {
                        state.RegisterDisplayedQuest(index, quest.Item1);
                        string statusTag = GetQuestStatusText(player, quest.Item1);
                        player.SendMessage(HINT_HUE, $"  • {quest.Item2.Name} {statusTag}");
                        index++;
                    }

                    player.SendMessage(HINT_HUE, "");
                });

                Timer.DelayCall(TimeSpan.FromMilliseconds(DELAY_MEDIUM + DELAY_SHORT), delegate
                {
                    SayTo(player, "Qual delas te interessa?");
                    SendHint(player, "Diga o nome da missao que quer saber mais.");
                });
            }
        }

        private void ProcessQuestListResponse(PlayerMobile player, ConversationState state, string speech)
        {
            // Procura qual quest o jogador mencionou
            foreach (var kvp in state.DisplayedQuests)
            {
                var definition = QuestRepository.Instance.GetQuest(kvp.Value);
                if (definition != null)
                {
                    string questName = NormalizeSpeech(definition.Name);
                    if (speech.Contains(questName) || questName.Contains(speech))
                    {
                        ShowQuestDetails(player, state, kvp.Value, kvp.Key);
                        return;
                    }

                    // Verifica palavras-chave do nome
                    string[] words = questName.Split(' ');
                    foreach (string word in words)
                    {
                        if (word.Length > 3 && speech.Contains(word))
                        {
                            ShowQuestDetails(player, state, kvp.Value, kvp.Key);
                            return;
                        }
                    }
                }
            }

            // Verifica outras intenções
            if (ContainsAny(speech, BACK_WORDS) || ContainsAny(speech, BIO_WORDS) ||
                ContainsAny(speech, LOCATION_WORDS) || ContainsAny(speech, TURNIN_WORDS))
            {
                state.CurrentStep = ConversationStep.Greet;
                ProcessGreetResponse(player, state, speech);
                return;
            }

            // Não encontrou
            SayTo(player, "Qual missao voce quer saber mais?");
            SendHint(player, "Diga o nome da missao.");
        }

        private string GetQuestStatusText(PlayerMobile player, string questId)
        {
            var status = player.GetQuestStatus(questId);

            switch (status)
            {
                case QuestStatus.Active:
                    return "(em andamento)";
                case QuestStatus.ReadyToTurnIn:
                    return "(pronta para entregar!)";
                default:
                    return "";
            }
        }

        #endregion

        #region Quest Details

        public void ShowQuestDetails(PlayerMobile player, ConversationState state, string questId, int index)
        {
            state.CurrentStep = ConversationStep.QuestDetails;
            state.SelectedQuestId = questId;
            state.SelectedQuestIndex = index;

            var definition = QuestRepository.Instance.GetQuest(questId);

            if (definition == null)
            {
                SayTo(player, "Hmm, parece que houve um engano.");
                state.CurrentStep = ConversationStep.Greet;
                return;
            }

            // Verifica se já está fazendo
            var status = player.GetQuestStatus(questId);
            if (status == QuestStatus.Active)
            {
                SayTo(player, "Voce ja esta fazendo essa missao!");

                Timer.DelayCall(TimeSpan.FromMilliseconds(DELAY_SHORT), delegate
                {
                    ShowQuestProgress(player, state, questId);
                });
                return;
            }

            // Verifica se pode aceitar
            if (!QuestManager.Instance.CanAcceptQuest(player, questId, out string reason))
            {
                SayTo(player, reason);
                state.CurrentStep = ConversationStep.ShowQuests;

                Timer.DelayCall(TimeSpan.FromMilliseconds(DELAY_MEDIUM), delegate
                {
                    SayTo(player, "Posso ajudar com outra coisa?");
                    state.CurrentStep = ConversationStep.Greet;
                });
                return;
            }

            // Descreve a quest
            SayTo(player, $"Deixe-me explicar sobre '{definition.Name}'...");

            Timer.DelayCall(TimeSpan.FromMilliseconds(DELAY_MEDIUM), delegate
            {
                string desc = definition.Description ?? "Uma tarefa importante.";
                SayTo(player, desc);
            });

            Timer.DelayCall(TimeSpan.FromMilliseconds(DELAY_MEDIUM * 2), delegate
            {
                // Mostra objetivos
                player.SendMessage(HINT_HUE, "");
                player.SendMessage(HINT_HUE, "O que precisa fazer:");

                foreach (var obj in definition.Objectives)
                {
                    player.SendMessage(WARNING_HUE, $"  • {obj.Description}");
                    if (!string.IsNullOrEmpty(obj.LocationHint))
                        player.SendMessage(HINT_HUE, $"    Dica: {obj.LocationHint}");
                }

                // Mostra recompensas
                if (definition.Rewards != null)
                {
                    player.SendMessage(HINT_HUE, "");
                    player.SendMessage(HINT_HUE, "Recompensas:");

                    if (definition.Rewards.Gold > 0)
                        player.SendMessage(SUCCESS_HUE, $"  • {definition.Rewards.Gold} moedas de ouro");
                    if (definition.Rewards.Fame > 0)
                        player.SendMessage(SUCCESS_HUE, $"  • {definition.Rewards.Fame} de fama");

                    foreach (var item in definition.Rewards.Items)
                    {
                        player.SendMessage(SUCCESS_HUE, $"  • {item.Amount}x {item.Name ?? item.Type}");
                    }
                }

                player.SendMessage(HINT_HUE, "");
            });

            Timer.DelayCall(TimeSpan.FromMilliseconds(DELAY_MEDIUM * 3), delegate
            {
                SayTo(player, "Entao, aceita me ajudar?");
                SendHint(player, "Diga 'sim' para aceitar ou 'nao' para recusar.");
            });
        }

        private void ShowQuestProgress(PlayerMobile player, ConversationState state, string questId)
        {
            var playerData = QuestManager.Instance.GetPlayerData(player);
            var activeQuest = playerData?.GetActiveQuest(questId);
            var definition = QuestRepository.Instance.GetQuest(questId);

            if (activeQuest == null || definition == null)
                return;

            SayTo(player, "Como esta indo a missao?");

            Timer.DelayCall(TimeSpan.FromMilliseconds(DELAY_SHORT), delegate
            {
                player.SendMessage(HINT_HUE, "");
                player.SendMessage(HINT_HUE, "Seu progresso:");

                for (int i = 0; i < definition.Objectives.Count && i < activeQuest.Progress.Count; i++)
                {
                    var obj = definition.Objectives[i];
                    var prog = activeQuest.Progress[i];

                    string status = prog.IsComplete ? "[COMPLETO]" : $"[{prog.Current}/{prog.Required}]";
                    int hue = prog.IsComplete ? SUCCESS_HUE : WARNING_HUE;

                    player.SendMessage(hue, $"  {obj.Description} {status}");
                }

                player.SendMessage(HINT_HUE, "");
            });

            Timer.DelayCall(TimeSpan.FromMilliseconds(DELAY_MEDIUM), delegate
            {
                if (activeQuest.Status == QuestStatus.ReadyToTurnIn)
                {
                    SayTo(player, "Parece que voce completou tudo! Diga 'entregar' quando estiver pronto.");
                }
                else
                {
                    SayTo(player, "Continue assim! Volte quando terminar.");
                }

                state.CurrentStep = ConversationStep.Greet;
            });
        }

        private void ProcessQuestDetailResponse(PlayerMobile player, ConversationState state, string speech)
        {
            // RECUSA é testada ANTES da aceitação.
            //
            // Antes a ordem era invertida, e como "nao aceito" / "nao quero"
            // contém tanto uma palavra de recusa quanto uma de aceitação
            // ("aceito", "quero"), o jogador que dizia NÃO acabava com a quest
            // aceita. A ordem correta é: negação vence.
            if (ContainsAny(speech, DECLINE_WORDS))
            {
                DeclineQuest(player, state);
            }
            else if (ContainsAny(speech, ACCEPT_WORDS))
            {
                AcceptQuest(player, state);
            }
            else if (ContainsAny(speech, BACK_WORDS) || ContainsAny(speech, QUEST_WORDS))
            {
                ShowQuests(player, state);
            }
            else
            {
                SayTo(player, "Entao, aceita ou nao?");
            }
        }

        #endregion

        #region Accept/Decline Quest

        public void AcceptQuest(PlayerMobile player, ConversationState state)
        {
            string questId = state.SelectedQuestId;

            if (string.IsNullOrEmpty(questId))
            {
                SayTo(player, "Aceitar o que?");
                state.CurrentStep = ConversationStep.Greet;
                return;
            }

            if (QuestManager.Instance.StartQuest(player, questId, out string message))
            {
                var definition = QuestRepository.Instance.GetQuest(questId);

                // Falas de aceitação do JSON
                if (definition?.Dialogue?.Accept?.Lines != null && definition.Dialogue.Accept.Lines.Count > 0)
                {
                    int delay = 0;
                    foreach (var line in definition.Dialogue.Accept.Lines)
                    {
                        string text = line.Text;
                        Timer.DelayCall(TimeSpan.FromMilliseconds(delay), delegate
                        {
                            SayTo(player, text);
                        });
                        delay += DELAY_MEDIUM;
                    }
                }
                else
                {
                    string[] acceptPhrases = new string[]
                    {
                        "Otimo! Sabia que podia contar com voce!",
                        "Excelente! Boa sorte na sua jornada!",
                        "Muito bem! Volte quando completar."
                    };
                    SayTo(player, acceptPhrases[Utility.Random(acceptPhrases.Length)]);
                }

                // Notificação
                player.PlaySound(0x5B6);
                player.SendMessage(SUCCESS_HUE, "");
                player.SendMessage(SUCCESS_HUE, $"*** Missao aceita: {definition?.Name ?? questId} ***");
                player.SendMessage(SUCCESS_HUE, "");

                state.SelectedQuestId = null;
                state.CurrentStep = ConversationStep.Greet;
            }
            else
            {
                SayTo(player, message);
                state.CurrentStep = ConversationStep.Greet;
            }
        }

        public void DeclineQuest(PlayerMobile player, ConversationState state)
        {
            string[] declinePhrases = new string[]
            {
                "Tudo bem, entendo. Fica pra proxima.",
                "Sem problemas. Se mudar de ideia, estou aqui.",
                "Ok, talvez outra hora entao."
            };

            SayTo(player, declinePhrases[Utility.Random(declinePhrases.Length)]);

            state.SelectedQuestId = null;
            state.CurrentStep = ConversationStep.Greet;
        }

        #endregion

        #region Turn In

        public void ShowTurnIn(PlayerMobile player, ConversationState state)
        {
            state.CurrentStep = ConversationStep.TurnIn;
            state.DisplayedQuests.Clear();

            // ── NOVO: Processa entrega de itens Deliver pendentes ─────────────
            bool deliverProcessed = QuestManager.Instance.OnDeliverItem(
                player, this, this.Serial.ToString(), null);

            if (deliverProcessed)
            {
                // Item foi entregue — agora verifica quests prontas para turn in
                Timer.DelayCall(TimeSpan.FromMilliseconds(500), delegate
                {
                    ShowTurnIn_CheckReady(player, state);
                });
                return;
            }

            ShowTurnIn_CheckReady(player, state);
        }

        /// <summary>
        /// Verifica quests prontas para entregar (após processar Deliver)
        /// </summary>
        private void ShowTurnIn_CheckReady(PlayerMobile player, ConversationState state)
        {
            var readyQuests = player.GetQuestsReadyToTurnIn(this);

            if (readyQuests.Count == 0)
            {
                // Verifica se tem quest ativa mas com Deliver incompleto
                var playerData = QuestManager.Instance.GetPlayerData(player);
                bool hasActiveDeliver = false;

                if (playerData != null)
                {
                    foreach (var active in playerData.ActiveQuests)
                    {
                        if (active == null)
                            continue;

                        var def = QuestRepository.Instance.GetQuest(active.QuestId);
                        if (def == null || def.Objectives == null) continue;

                        // Itera por índice: a versão anterior usava
                        // def.Objectives.IndexOf(obj) para indexar active.Progress,
                        // o que estourava (ArgumentOutOfRange) em saves cujo
                        // progresso foi gravado com outra quantidade de objetivos.
                        for (int i = 0; i < def.Objectives.Count; i++)
                        {
                            if (!(def.Objectives[i] is DeliverObjective))
                                continue;

                            if (i >= active.Progress.Count || active.Progress[i] == null ||
                                !active.Progress[i].IsComplete)
                            {
                                hasActiveDeliver = true;
                                break;
                            }
                        }

                        if (hasActiveDeliver)
                            break;
                    }
                }

                if (hasActiveDeliver)
                    SayTo(player, "Voce ainda nao tem o item que preciso.");
                else
                    SayTo(player, "Voce nao tem nenhuma missao minha para entregar.");

                state.CurrentStep = ConversationStep.Greet;
                return;
            }

            if (readyQuests.Count == 1)
            {
                var quest = readyQuests[0];
                SayTo(player, $"Ah, voce completou '{quest.QuestName}'! Deixe-me ver...");
                state.SelectedQuestId = quest.QuestId;

                Timer.DelayCall(TimeSpan.FromMilliseconds(DELAY_MEDIUM), delegate
                {
                    ProcessTurnIn(player, state, quest.QuestId);
                });
            }
            else
            {
                SayTo(player, $"Vejo que completou {readyQuests.Count} missoes!");

                Timer.DelayCall(TimeSpan.FromMilliseconds(DELAY_SHORT), delegate
                {
                    player.SendMessage(HINT_HUE, "");
                    player.SendMessage(HINT_HUE, "Missoes prontas para entregar:");

                    int index = 1;
                    foreach (var quest in readyQuests)
                    {
                        state.RegisterDisplayedQuest(index, quest.QuestId);
                        player.SendMessage(SUCCESS_HUE, $"  • {quest.QuestName}");
                        index++;
                    }
                    player.SendMessage(HINT_HUE, "");
                });

                Timer.DelayCall(TimeSpan.FromMilliseconds(DELAY_MEDIUM), delegate
                {
                    SayTo(player, "Qual voce quer entregar primeiro?");
                    SendHint(player, "Diga o nome da missao.");
                });
            }
        }
        private void ProcessTurnInResponse(PlayerMobile player, ConversationState state, string speech)
        {
            // Procura qual quest o jogador mencionou
            foreach (var kvp in state.DisplayedQuests)
            {
                var definition = QuestRepository.Instance.GetQuest(kvp.Value);
                if (definition != null)
                {
                    string questName = NormalizeSpeech(definition.Name);
                    if (speech.Contains(questName) || questName.Contains(speech))
                    {
                        ProcessTurnIn(player, state, kvp.Value);
                        return;
                    }

                    // Verifica palavras-chave
                    string[] words = questName.Split(' ');
                    foreach (string word in words)
                    {
                        if (word.Length > 3 && speech.Contains(word))
                        {
                            ProcessTurnIn(player, state, kvp.Value);
                            return;
                        }
                    }
                }
            }

            // Verifica outras intenções
            if (ContainsAny(speech, BACK_WORDS))
            {
                state.CurrentStep = ConversationStep.Greet;
                SayTo(player, "Claro, o que mais posso fazer por voce?");
                return;
            }

            SayTo(player, "Qual missao voce quer entregar?");
        }

        public void ProcessTurnIn(PlayerMobile player, ConversationState state, string questId)
        {
            if (QuestManager.Instance.CompleteQuest(player, questId, out string message))
            {
                var definition = QuestRepository.Instance.GetQuest(questId);

                // Falas de conclusão do JSON
                if (definition?.Dialogue?.Complete?.Lines != null && definition.Dialogue.Complete.Lines.Count > 0)
                {
                    int delay = 0;
                    foreach (var line in definition.Dialogue.Complete.Lines)
                    {
                        string text = line.Text;
                        Timer.DelayCall(TimeSpan.FromMilliseconds(delay), delegate
                        {
                            SayTo(player, text);
                        });
                        delay += DELAY_MEDIUM;
                    }
                }
                else
                {
                    string[] completePhrases = new string[]
                    {
                        "Muito bem! Aqui esta sua recompensa.",
                        "Excelente trabalho! Voce merece isso.",
                        "Perfeito! Obrigado pela ajuda."
                    };
                    SayTo(player, completePhrases[Utility.Random(completePhrases.Length)]);
                }

                // Notificação
                player.PlaySound(0x5B5);
                player.SendMessage(SUCCESS_HUE, "");
                player.SendMessage(SUCCESS_HUE, $"*** Missao completa: {definition?.Name ?? questId} ***");
                player.SendMessage(SUCCESS_HUE, "");

                state.CurrentStep = ConversationStep.Greet;

                // Verifica se tem mais para entregar
                Timer.DelayCall(TimeSpan.FromMilliseconds(DELAY_LONG), delegate
                {
                    var remaining = player.GetQuestsReadyToTurnIn(this);
                    if (remaining.Count > 0)
                    {
                        SayTo(player, "Voce tem mais alguma missao para entregar?");
                        state.CurrentStep = ConversationStep.TurnIn;
                    }
                });
            }
            else
            {
                SayTo(player, message);
                state.CurrentStep = ConversationStep.Greet;
            }
        }

        #endregion

        #region Drag Drop

        public override bool OnDragDrop(Mobile from, Item dropped)
        {
            if (!(from is PlayerMobile player))
                return base.OnDragDrop(from, dropped);

            if (!CanInteractWith(player))
                return base.OnDragDrop(from, dropped);

            if (!from.InRange(this.Location, BehaviorConfig.MaxInteractionRange))
            {
                SayTo(player, "Chegue mais perto!");
                return false;
            }

            // Tenta processar como entrega de quest
            bool delivered = QuestManager.Instance.OnDeliverItem(
                player, this, this.Serial.ToString(), dropped);

            if (delivered)
            {
                // Notifica o NPC da entrega
                if (BehaviorConfig.FacePlayerOnTalk)
                    this.Direction = this.GetDirectionTo(player);

                // Inicia conversa se não estava ativa (para processar turn in)
                var state = GetOrCreateState(player);

                Timer.DelayCall(TimeSpan.FromMilliseconds(800), delegate
                {
                    ShowTurnIn_CheckReady(player, state);
                });

                return true; // item foi consumido
            }

            // Item não é de quest — devolve
            SayTo(player, "Nao preciso disto.");
            return false;
        }

        #endregion

        #region End Conversation

        public void EndConversation(PlayerMobile player)
        {
            if (player == null)
                return;

            _conversations.Remove(player.Serial);

            string[] byePhrases = new string[]
            {
                "Ate mais, aventureiro!",
                "Boa sorte em suas jornadas!",
                "Va com cuidado!",
                "Volte sempre!"
            };

            SayTo(player, byePhrases[Utility.Random(byePhrases.Length)]);
        }

        #endregion

        #region Helper Methods

        private void SendHint(PlayerMobile player, string hint)
        {
            if (player != null && !player.Deleted && player.NetState != null)
            {
                player.SendMessage(HINT_HUE, $"[{hint}]");
            }
        }

        private string[] WrapText(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text))
                return new[] { "" };

            if (text.Length <= maxLength)
                return new[] { text };

            var parts = new List<string>();
            var words = text.Split(' ');
            var currentPart = "";

            foreach (string word in words)
            {
                if (currentPart.Length + word.Length + 1 > maxLength)
                {
                    if (!string.IsNullOrEmpty(currentPart))
                    {
                        parts.Add(currentPart.Trim());
                        currentPart = "";
                    }
                }
                currentPart += word + " ";
            }

            if (!string.IsNullOrEmpty(currentPart.Trim()))
                parts.Add(currentPart.Trim());

            return parts.ToArray();
        }

        #endregion

        #region Speech Helper

        /// <summary>
        /// Faz o NPC falar texto puro (sem Cliloc)
        /// </summary>
        private void SayTo(PlayerMobile player, string text)
        {
            if (player == null || player.Deleted || string.IsNullOrEmpty(text))
                return;

            // Remove caracteres especiais que podem causar problemas
            text = SanitizeText(text);

            // Usa PublicOverheadMessage para garantir texto puro
            this.PublicOverheadMessage(MessageType.Regular, NPC_SPEECH_HUE, false, text);
        }

        /// <summary>
        /// Faz o NPC falar apenas para um jogador específico
        /// </summary>
        private void PrivateSay(PlayerMobile player, string text)
        {
            if (player == null || player.Deleted || string.IsNullOrEmpty(text))
                return;

            text = SanitizeText(text);

            this.PrivateOverheadMessage(MessageType.Regular, NPC_SPEECH_HUE, false, text, player.NetState);
        }

        /// <summary>
        /// Remove caracteres problemáticos
        /// </summary>
        private string SanitizeText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "";

            // Substitui caracteres acentuados
            text = text.Replace("á", "a").Replace("à", "a").Replace("ã", "a").Replace("â", "a");
            text = text.Replace("é", "e").Replace("ê", "e");
            text = text.Replace("í", "i");
            text = text.Replace("ó", "o").Replace("ô", "o").Replace("õ", "o");
            text = text.Replace("ú", "u");
            text = text.Replace("ç", "c");
            text = text.Replace("Á", "A").Replace("À", "A").Replace("Ã", "A").Replace("Â", "A");
            text = text.Replace("É", "E").Replace("Ê", "E");
            text = text.Replace("Í", "I");
            text = text.Replace("Ó", "O").Replace("Ô", "O").Replace("Õ", "O");
            text = text.Replace("Ú", "U");
            text = text.Replace("Ç", "C");

            return text;
        }

        #endregion

        #region Quest Management

        public void AddQuest(string questId)
        {
            if (!QuestIds.Contains(questId))
                QuestIds.Add(questId);
        }

        public void RemoveQuest(string questId)
        {
            QuestIds.Remove(questId);
        }

        public bool HasQuest(string questId)
        {
            return QuestIds.Contains(questId);
        }

        #endregion

        #region Serialization

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(6); // Versão

            writer.Write(_isQuestGiver);
            writer.Write(_questIds?.Count ?? 0);

            if (_questIds != null)
            {
                foreach (string id in _questIds)
                    writer.Write(id);
            }

            writer.Write(BehaviorConfig.Bio ?? "");
            writer.Write(BehaviorConfig.LocationDescription ?? "");
            writer.Write(BehaviorConfig.SpeechHue);
            writer.Write(BehaviorConfig.MaxInteractionRange);
            writer.Write(BehaviorConfig.FacePlayerOnTalk);
            writer.Write(BehaviorConfig.PlaySoundOnGreet);
            writer.Write(BehaviorConfig.GreetSound);

            // v6: identificador estável do NPC usado pelos JSON de quest
            writer.Write(_questNpcId ?? "");
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            int version = reader.ReadInt();

            _behaviorConfig = new NPCBehaviorConfig();
            _questIds = new List<string>();
            _conversations = new Dictionary<Serial, ConversationState>();
            _dialogueData = DialogueData.CreateDefault();

            if (version >= 1)
            {
                _isQuestGiver = reader.ReadBool();
                int count = reader.ReadInt();

                for (int i = 0; i < count; i++)
                    _questIds.Add(reader.ReadString());
            }

            if (version >= 2)
            {
                BehaviorConfig.Bio = reader.ReadString();
                BehaviorConfig.LocationDescription = reader.ReadString();
                BehaviorConfig.SpeechHue = reader.ReadInt();
                BehaviorConfig.MaxInteractionRange = reader.ReadInt();
                BehaviorConfig.FacePlayerOnTalk = reader.ReadBool();
                BehaviorConfig.PlaySoundOnGreet = reader.ReadBool();
                BehaviorConfig.GreetSound = reader.ReadInt();
            }

            if (version >= 6)
            {
                _questNpcId = reader.ReadString();

                if (string.IsNullOrWhiteSpace(_questNpcId))
                    _questNpcId = null;
            }
        }

        #endregion
    }
}