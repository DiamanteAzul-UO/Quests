/*****************************************************
 * DialogueData.cs
 * Dados completos de diálogo para um NPC ou Quest
 * Etapa 3 - DialogueFlow
 * 
 * Versão: 1.0.0
 * Compatível com: ServUO
 *****************************************************/

using System;
using System.Collections.Generic;
using Server;
using Server.Mobiles;

namespace Server.Custom.QuestSystem.Dialogue
{
    /// <summary>
    /// Contém todos os diálogos de um NPC
    /// </summary>
    public class DialogueData
    {
        #region Properties

        /// <summary>
        /// ID único do diálogo
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Diálogo de saudação
        /// </summary>
        public DialogueNode Greeting { get; set; }

        /// <summary>
        /// Diálogo de biografia
        /// </summary>
        public DialogueNode Bio { get; set; }

        /// <summary>
        /// Diálogo sobre o local
        /// </summary>
        public DialogueNode Location { get; set; }

        /// <summary>
        /// Diálogo de lista de quests
        /// </summary>
        public DialogueNode QuestList { get; set; }

        /// <summary>
        /// Diálogo de despedida
        /// </summary>
        public DialogueNode Farewell { get; set; }

        /// <summary>
        /// Diálogo quando NPC está ocupado
        /// </summary>
        public DialogueNode Busy { get; set; }

        /// <summary>
        /// Diálogo quando jogador está longe
        /// </summary>
        public DialogueNode OutOfRange { get; set; }

        /// <summary>
        /// Diálogos customizados por nome
        /// </summary>
        public Dictionary<string, DialogueNode> CustomNodes { get; set; }

        /// <summary>
        /// Diálogos específicos de quests
        /// </summary>
        public Dictionary<string, QuestDialogue> QuestDialogues { get; set; }

        #endregion

        #region Constructor

        public DialogueData()
        {
            CustomNodes = new Dictionary<string, DialogueNode>();
            QuestDialogues = new Dictionary<string, QuestDialogue>();

            // Cria nós padrão
            CreateDefaultNodes();
        }

        public DialogueData(string id) : this()
        {
            Id = id;
        }

        #endregion

        #region Default Nodes

        private void CreateDefaultNodes()
        {
            // Saudação padrão
            Greeting = new DialogueNode
            {
                Lines = new List<DialogueLine>
                {
                    new DialogueLine("Ola, {playerName}!"),
                    new DialogueLine("Em que posso ajudar?")
                },
                Options = new List<DialogueOption>
                {
                    new DialogueOption(1, "Quem e voce?", DialogueAction.ShowBio),
                    new DialogueOption(2, "Sobre este lugar", DialogueAction.ShowLocation),
                    new DialogueOption(3, "Missoes disponiveis", DialogueAction.ShowQuests),
                    new DialogueOption(4, "Entregar missao", DialogueAction.TurnIn),
                    new DialogueOption(5, "Tchau", DialogueAction.Exit)
                }
            };

            // Bio padrão
            Bio = new DialogueNode
            {
                Lines = new List<DialogueLine>
                {
                    new DialogueLine("Sou apenas um habitante deste mundo.")
                },
                Options = new List<DialogueOption>
                {
                    DialogueOption.Back(1)
                }
            };

            // Local padrão
            Location = new DialogueNode
            {
                Lines = new List<DialogueLine>
                {
                    new DialogueLine("Este e um lugar como qualquer outro.")
                },
                Options = new List<DialogueOption>
                {
                    DialogueOption.Back(1)
                }
            };

            // Despedida padrão
            Farewell = new DialogueNode
            {
                Lines = new List<DialogueLine>
                {
                    new DialogueLine("Ate logo, {playerName}!"),
                    new DialogueLine("Boa sorte em suas aventuras!")
                }
            };

            // Sem quests
            QuestList = new DialogueNode
            {
                Lines = new List<DialogueLine>
                {
                    new DialogueLine("Nao tenho missoes para voce no momento."),
                    new DialogueLine("Volte mais tarde!")
                },
                Options = new List<DialogueOption>
                {
                    DialogueOption.Back(0)
                }
            };
        }

        #endregion

        #region Methods

        /// <summary>
        /// Obtém diálogo de uma quest
        /// </summary>
        public QuestDialogue GetQuestDialogue(string questId)
        {
            if (QuestDialogues.TryGetValue(questId, out QuestDialogue dialogue))
                return dialogue;

            return null;
        }

        /// <summary>
        /// Define diálogo de uma quest
        /// </summary>
        public void SetQuestDialogue(string questId, QuestDialogue dialogue)
        {
            QuestDialogues[questId] = dialogue;
        }

        /// <summary>
        /// Obtém nó customizado
        /// </summary>
        public DialogueNode GetCustomNode(string name)
        {
            if (CustomNodes.TryGetValue(name, out DialogueNode node))
                return node;

            return null;
        }

        /// <summary>
        /// Define nó customizado
        /// </summary>
        public void SetCustomNode(string name, DialogueNode node)
        {
            CustomNodes[name] = node;
        }

        #endregion

        #region Static Factory

        /// <summary>
        /// Cria DialogueData padrão
        /// </summary>
        public static DialogueData CreateDefault()
        {
            return new DialogueData();
        }

        /// <summary>
        /// Cria DialogueData para NPC de quest
        /// </summary>
        public static DialogueData CreateQuestGiver(string npcName, string bio, string location)
        {
            var data = new DialogueData();
            
            data.Greeting.Lines = new List<DialogueLine>
            {
                new DialogueLine($"Ola, {{playerName}}! Eu sou {npcName}."),
                new DialogueLine("Em que posso ajudar?")
            };

            data.Bio.Lines = new List<DialogueLine>
            {
                new DialogueLine(bio)
            };

            data.Location.Lines = new List<DialogueLine>
            {
                new DialogueLine(location)
            };

            return data;
        }

        #endregion
    }

    /// <summary>
    /// Um nó de diálogo contendo falas e opções
    /// </summary>
    public class DialogueNode
    {
        /// <summary>
        /// Linhas de fala do NPC
        /// </summary>
        public List<DialogueLine> Lines { get; set; }

        /// <summary>
        /// Opções de resposta
        /// </summary>
        public List<DialogueOption> Options { get; set; }

        /// <summary>
        /// Ação a executar antes das falas
        /// </summary>
        public string PreAction { get; set; }

        /// <summary>
        /// Ação a executar após as falas
        /// </summary>
        public string PostAction { get; set; }

        public DialogueNode()
        {
            Lines = new List<DialogueLine>();
            Options = new List<DialogueOption>();
        }

        /// <summary>
        /// Adiciona linha de fala
        /// </summary>
        public void AddLine(string text, int delayAfter = 2000)
        {
            Lines.Add(new DialogueLine(text, delayAfter));
        }

        /// <summary>
        /// Adiciona opção
        /// </summary>
        public void AddOption(int key, string label, DialogueAction action)
        {
            Options.Add(new DialogueOption(key, label, action));
        }

        /// <summary>
        /// Calcula tempo total de falas
        /// </summary>
        public int GetTotalDuration()
        {
            int total = 0;
            foreach (var line in Lines)
            {
                total += line.DelayBefore + line.DelayAfter;
            }
            return total;
        }
    }

    /// <summary>
    /// Diálogos específicos de uma quest
    /// </summary>
    public class QuestDialogue
    {
        /// <summary>
        /// ID da quest
        /// </summary>
        public string QuestId { get; set; }

        /// <summary>
        /// Nome da quest (para exibição)
        /// </summary>
        public string QuestName { get; set; }

        /// <summary>
        /// Descrição curta
        /// </summary>
        public string ShortDescription { get; set; }

        /// <summary>
        /// Diálogo ao oferecer a quest
        /// </summary>
        public DialogueNode Offer { get; set; }

        /// <summary>
        /// Diálogo ao aceitar a quest
        /// </summary>
        public DialogueNode Accept { get; set; }

        /// <summary>
        /// Diálogo ao recusar a quest
        /// </summary>
        public DialogueNode Decline { get; set; }

        /// <summary>
        /// Diálogo de progresso (quest em andamento)
        /// </summary>
        public DialogueNode Progress { get; set; }

        /// <summary>
        /// Diálogo ao completar a quest
        /// </summary>
        public DialogueNode Complete { get; set; }

        /// <summary>
        /// Diálogo se quest já foi completada
        /// </summary>
        public DialogueNode AlreadyComplete { get; set; }

        /// <summary>
        /// Diálogo se não pode aceitar (pré-requisitos)
        /// </summary>
        public DialogueNode CannotAccept { get; set; }

        /// <summary>
        /// Resumo das recompensas
        /// </summary>
        public string RewardSummary { get; set; }

        /// <summary>
        /// Objetivos formatados
        /// </summary>
        public List<string> ObjectiveDescriptions { get; set; }

        public QuestDialogue()
        {
            ObjectiveDescriptions = new List<string>();
            CreateDefaults();
        }

        public QuestDialogue(string questId, string questName) : this()
        {
            QuestId = questId;
            QuestName = questName;
        }

        private void CreateDefaults()
        {
            Offer = new DialogueNode();
            Offer.AddLine("Esta e uma missao importante.");
            Offer.AddLine("Voce aceita o desafio?");
            Offer.AddOption(1, "Aceitar", DialogueAction.AcceptQuest);
            Offer.AddOption(2, "Recusar", DialogueAction.DeclineQuest);
            Offer.AddOption(0, "Voltar", DialogueAction.Back);

            Accept = new DialogueNode();
            Accept.AddLine("Excelente! Boa sorte, {playerName}!");

            Decline = new DialogueNode();
            Decline.AddLine("Tudo bem, volte quando mudar de ideia.");

            Progress = new DialogueNode();
            Progress.AddLine("Como vai a missao?");
            Progress.AddLine("Continue assim!");

            Complete = new DialogueNode();
            Complete.AddLine("Muito bem, {playerName}!");
            Complete.AddLine("Voce completou a missao!");
            Complete.AddLine("Aqui esta sua recompensa!");

            AlreadyComplete = new DialogueNode();
            AlreadyComplete.AddLine("Voce ja completou esta missao!");

            CannotAccept = new DialogueNode();
            CannotAccept.AddLine("Voce ainda nao pode aceitar esta missao.");
        }
    }
}