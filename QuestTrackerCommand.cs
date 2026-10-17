using System;
using Server;
using Server.Commands;
using Server.Gumps;
using Server.Mobiles;

namespace Server.Custom.QuestSystem
{
    public static class QuestTrackerCommand
    {
        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized)
                return;

            _initialized = true;

            CommandSystem.Register("missoes", AccessLevel.Player, OnCommand);
            CommandSystem.Register("quests", AccessLevel.Player, OnCommand);
            CommandSystem.Register("objetivos", AccessLevel.Player, OnCommand);

            Console.WriteLine("[QuestTracker] Comandos .missoes / .quests / .objetivos registrados.");
        }

        [Usage("missoes")]
        [Description("Mostra os objetivos das missoes ativas.")]
        private static void OnCommand(CommandEventArgs e)
        {
            PlayerMobile pm = e.Mobile as PlayerMobile;

            if (pm == null)
                return;

            MystQuestObjectiveGump.MostrarQuestsAtivas(pm);
        }
    }
}