/*****************************************************
 * QuestNPCCommands.cs
 * Comandos GM para NPCs de Quest
 * 
 * Versão: 1.1.0
 * Compatível com: ServUO
 *****************************************************/

using System;
using Server;
using Server.Commands;
using Server.Mobiles;
using Server.Targeting;
using Server.Custom.QuestSystem.Conversation;
using Server.Custom.QuestSystem.NPCs;

namespace Server.Custom.QuestSystem
{
    /// <summary>
    /// Comandos para NPCs de Quest
    /// </summary>
    public static class QuestNPCCommands
    {
        /// <summary>
        /// Registra comandos de NPC
        /// </summary>
        public static void Initialize()
        {
            CommandSystem.Register("addquestnpc", AccessLevel.GameMaster, AddQuestNPC_OnCommand);
            CommandSystem.Register("questnpcset", AccessLevel.GameMaster, QuestNPCSet_OnCommand);
            CommandSystem.Register("questnpcaddquest", AccessLevel.GameMaster, QuestNPCAddQuest_OnCommand);
            CommandSystem.Register("questnpctalk", AccessLevel.GameMaster, QuestNPCTalk_OnCommand);
        }

        #region Commands

        /// <summary>
        /// [addquestnpc - Adiciona um QuestNPC
        /// </summary>
        [Usage("addquestnpc [nome]")]
        [Description("Adiciona um NPC de Quest no local clicado.")]
        private static void AddQuestNPC_OnCommand(CommandEventArgs e)
        {
            Mobile from = e.Mobile;
            string name = e.ArgString.Trim();

            if (string.IsNullOrEmpty(name))
                name = "Mestre das Quests";

            from.SendMessage(QuestConstants.HueInfo, "Clique no local para adicionar o NPC...");
            from.Target = new AddQuestNPCTarget(name);
        }

        /// <summary>
        /// [questnpcset - Configura propriedades do NPC
        /// </summary>
        [Usage("questnpcset <propriedade> <valor>")]
        [Description("Configura uma propriedade do QuestNPC. Propriedades: bio, location, range")]
        private static void QuestNPCSet_OnCommand(CommandEventArgs e)
        {
            if (e.Arguments.Length < 2)
            {
                e.Mobile.SendMessage(QuestConstants.HueWarning, "Uso: [questnpcset <propriedade> <valor>");
                e.Mobile.SendMessage(QuestConstants.HueInfo, "Propriedades: bio, location, range, speechhue");
                return;
            }

            string property = e.Arguments[0].ToLower();
            string value = e.ArgString.Substring(property.Length).Trim();

            e.Mobile.SendMessage(QuestConstants.HueInfo, "Clique no QuestNPC para configurar...");
            e.Mobile.Target = new SetQuestNPCPropertyTarget(property, value);
        }

        /// <summary>
        /// [questnpcaddquest - Adiciona quest ao NPC
        /// </summary>
        [Usage("questnpcaddquest <questId>")]
        [Description("Adiciona uma quest ao NPC.")]
        private static void QuestNPCAddQuest_OnCommand(CommandEventArgs e)
        {
            if (e.Arguments.Length < 1)
            {
                e.Mobile.SendMessage(QuestConstants.HueWarning, "Uso: [questnpcaddquest <questId>");
                return;
            }

            string questId = e.Arguments[0];
            e.Mobile.SendMessage(QuestConstants.HueInfo, "Clique no QuestNPC...");
            e.Mobile.Target = new AddQuestToNPCTarget(questId);
        }

        /// <summary>
        /// [questnpctalk - Testa conversa com NPC
        /// </summary>
        [Usage("questnpctalk")]
        [Description("Inicia conversa de teste com um QuestNPC.")]
        private static void QuestNPCTalk_OnCommand(CommandEventArgs e)
        {
            e.Mobile.SendMessage(QuestConstants.HueInfo, "Clique no QuestNPC para conversar...");
            e.Mobile.Target = new TalkToNPCTarget();
        }

        #endregion

        #region Targets

        /// <summary>
        /// Target para adicionar QuestNPC
        /// </summary>
        private class AddQuestNPCTarget : Target
        {
            private string _name;

            public AddQuestNPCTarget(string name) : base(-1, true, TargetFlags.None)
            {
                _name = name;
            }

            protected override void OnTarget(Mobile from, object targeted)
            {
                IPoint3D p = targeted as IPoint3D;
                if (p == null)
                    return;

                if (p is Item)
                    p = ((Item)p).GetWorldTop();

                Point3D loc = new Point3D(p);

                QuestNPC npc = new QuestNPC("Quest Giver")
                {
                    Name = _name,
                    Location = loc,
                    Map = from.Map
                };

                // Configuração padrão
                npc.BehaviorConfig.Bio = $"Sou {_name}, guardião de missões e aventuras.";
                npc.BehaviorConfig.LocationDescription = "Este é um lugar de encontros entre aventureiros e suas missões.";

                from.SendMessage(QuestConstants.HueSuccess, $"QuestNPC '{_name}' criado com sucesso!");
                from.SendMessage(QuestConstants.HueInfo, "Comandos úteis:");
                from.SendMessage(QuestConstants.HueInfo, "  [questnpcset bio <texto> - Define biografia");
                from.SendMessage(QuestConstants.HueInfo, "  [questnpcaddquest <id> - Adiciona quest");
            }
        }

        /// <summary>
        /// Target para configurar propriedade
        /// </summary>
        private class SetQuestNPCPropertyTarget : Target
        {
            private string _property;
            private string _value;

            public SetQuestNPCPropertyTarget(string property, string value) : base(-1, false, TargetFlags.None)
            {
                _property = property;
                _value = value;
            }

            protected override void OnTarget(Mobile from, object targeted)
            {
                if (!(targeted is QuestNPC npc))
                {
                    from.SendMessage(QuestConstants.HueError, "Alvo deve ser um QuestNPC.");
                    return;
                }

                switch (_property)
                {
                    case "bio":
                        npc.BehaviorConfig.Bio = _value;
                        from.SendMessage(QuestConstants.HueSuccess, $"Bio definida: {_value}");
                        break;

                    case "location":
                        npc.BehaviorConfig.LocationDescription = _value;
                        from.SendMessage(QuestConstants.HueSuccess, $"Descrição do local definida: {_value}");
                        break;

                    case "range":
                        if (int.TryParse(_value, out int range))
                        {
                            npc.BehaviorConfig.MaxInteractionRange = range;
                            from.SendMessage(QuestConstants.HueSuccess, $"Range definido: {range}");
                        }
                        else
                        {
                            from.SendMessage(QuestConstants.HueError, "Valor de range inválido.");
                        }
                        break;

                    case "speechhue":
                        if (int.TryParse(_value, out int hue))
                        {
                            npc.BehaviorConfig.SpeechHue = hue;
                            from.SendMessage(QuestConstants.HueSuccess, $"Cor da fala definida: {hue}");
                        }
                        else
                        {
                            from.SendMessage(QuestConstants.HueError, "Valor de hue inválido.");
                        }
                        break;

                    default:
                        from.SendMessage(QuestConstants.HueWarning, $"Propriedade desconhecida: {_property}");
                        from.SendMessage(QuestConstants.HueInfo, "Propriedades: bio, location, range, speechhue");
                        break;
                }
            }
        }

        /// <summary>
        /// Target para adicionar quest ao NPC
        /// </summary>
        private class AddQuestToNPCTarget : Target
        {
            private string _questId;

            public AddQuestToNPCTarget(string questId) : base(-1, false, TargetFlags.None)
            {
                _questId = questId;
            }

            protected override void OnTarget(Mobile from, object targeted)
            {
                if (!(targeted is QuestNPC npc))
                {
                    from.SendMessage(QuestConstants.HueError, "Alvo deve ser um QuestNPC.");
                    return;
                }

                if (npc.HasQuest(_questId))
                {
                    from.SendMessage(QuestConstants.HueWarning, $"NPC já possui a quest '{_questId}'.");
                    return;
                }

                npc.AddQuest(_questId);
                from.SendMessage(QuestConstants.HueSuccess, $"Quest '{_questId}' adicionada ao NPC.");
                from.SendMessage(QuestConstants.HueInfo, $"Total de quests: {npc.QuestIds.Count}");
            }
        }

        /// <summary>
        /// Target para conversar com NPC
        /// </summary>
        private class TalkToNPCTarget : Target
        {
            public TalkToNPCTarget() : base(-1, false, TargetFlags.None) { }

            protected override void OnTarget(Mobile from, object targeted)
            {
                if (!(targeted is QuestNPC npc))
                {
                    from.SendMessage(QuestConstants.HueError, "Alvo deve ser um QuestNPC.");
                    return;
                }

                if (!(from is PlayerMobile player))
                    return;

                npc.StartConversation(player);
            }
        }

        #endregion
    }
}