/*****************************************************
 * IQuestNpcIdentity.cs
 * Identidade estavel de NPC para o sistema de quests
 *
 * Por que existe:
 *   Os JSON usam "targetNpcId": "npc_enfermeira_sera", mas o sistema passava
 *   adiante o Serial.ToString() do NPC. Nunca casava — só o targetNpcName
 *   salvava o objetivo. Qualquer quest que dependesse só do ID, ou qualquer
 *   NPC recriado (serial novo), ficava impossivel de concluir.
 *
 *   Qualquer Mobile que implemente esta interface passa a ter um ID logico
 *   estavel que sobrevive a relog/recriacao do NPC.
 *
 * OBS.: fica no namespace raiz do sistema para que QuestManager, QuestNPC e
 *       scripts externos enxerguem a interface sem precisar de using extra.
 *
 * Versao: 1.0.0
 * Compativel com: ServUO
 *****************************************************/

namespace Server.Custom.QuestSystem
{
    /// <summary>
    /// Implementado por NPCs que possuem um ID logico declarado no JSON de quests.
    /// </summary>
    public interface IQuestNpcIdentity
    {
        /// <summary>
        /// ID logico do NPC (ex.: "npc_enfermeira_sera").
        /// Deve ser identico ao "giverId"/"turnInId"/"targetNpcId" do JSON.
        /// </summary>
        string QuestNpcId { get; set; }
    }
}
