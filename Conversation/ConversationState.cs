/*****************************************************
 * ConversationState.cs
 * Estado de uma conversa ativa
 * 
 * Versão: 1.6.0
 * Compatível com: ServUO
 *****************************************************/

using System;
using System.Collections.Generic;
using Server;
using Server.Mobiles;
using Server.Custom.QuestSystem;

namespace Server.Custom.QuestSystem.Conversation
{
    /// <summary>
    /// Estado de uma conversa ativa
    /// </summary>
    public class ConversationState
    {
        #region Properties

        public Mobile NPC { get; private set; }
        public PlayerMobile Player { get; private set; }
        public ConversationStep CurrentStep { get; set; }
        public string SelectedQuestId { get; set; }
        public int SelectedQuestIndex { get; set; }
        public Dictionary<int, string> DisplayedQuests { get; set; }
        public Dictionary<string, object> ExtraData { get; set; }
        public DateTime StartedAt { get; set; }

        #endregion

        #region Constructors

        public ConversationState(PlayerMobile player)
        {
            NPC = null;
            Player = player ?? throw new ArgumentNullException(nameof(player));
            Initialize();
        }

        public ConversationState(Mobile npc, PlayerMobile player)
        {
            NPC = npc;
            Player = player ?? throw new ArgumentNullException(nameof(player));
            Initialize();
        }

        private void Initialize()
        {
            CurrentStep = ConversationStep.None;
            SelectedQuestId = null;
            SelectedQuestIndex = -1;
            DisplayedQuests = new Dictionary<int, string>();
            ExtraData = new Dictionary<string, object>();
            StartedAt = DateTime.UtcNow;
        }

        #endregion

        #region Methods

        public void SetNPC(Mobile npc)
        {
            NPC = npc;
        }

        public void RegisterDisplayedQuest(int index, string questId)
        {
            DisplayedQuests[index] = questId;
        }

        public string GetQuestIdByIndex(int index)
        {
            return DisplayedQuests.TryGetValue(index, out string questId) ? questId : null;
        }

        public void SetData(string key, object value)
        {
            ExtraData[key] = value;
        }

        public T GetData<T>(string key, T defaultValue = default)
        {
            if (ExtraData.TryGetValue(key, out object value) && value is T typedValue)
                return typedValue;
            return defaultValue;
        }

        public void Clear()
        {
            CurrentStep = ConversationStep.None;
            SelectedQuestId = null;
            SelectedQuestIndex = -1;
            DisplayedQuests.Clear();
            ExtraData.Clear();
        }

        public void Reset()
        {
            Clear();
            StartedAt = DateTime.UtcNow;
        }

        public void RefreshTimeout()
        {
            StartedAt = DateTime.UtcNow;
        }

        #endregion
    }
}