/*****************************************************
 * QuestObjective.cs
 * Define os tipos de objetivos de quest
 * Etapa 4 - QuestDefinition
 * 
 * Versão: 1.0.0
 * Compatível com: ServUO
 *****************************************************/

using System;
using System.Collections.Generic;
using Server;

namespace Server.Custom.QuestSystem.Quests
{
    /// <summary>
    /// Classe base para todos os objetivos de quest
    /// </summary>
    public abstract class QuestObjective
    {
        #region Properties

        /// <summary>
        /// Tipo do objetivo
        /// </summary>
        public ObjectiveType Type { get; set; }

        /// <summary>
        /// Descrição do objetivo para o jogador
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Quantidade necessária
        /// </summary>
        public int Required { get; set; }

        /// <summary>
        /// Progresso atual (usado no PlayerQuestData)
        /// </summary>
        public int Current { get; set; }

        /// <summary>
        /// Objetivo completo
        /// </summary>
        public bool IsComplete => Current >= Required;

        /// <summary>
        /// Dica de localização
        /// </summary>
        public string LocationHint { get; set; }

        /// <summary>
        /// Índice do objetivo na lista
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// Objetivo opcional
        /// </summary>
        public bool IsOptional { get; set; }

        /// <summary>
        /// Objetivo oculto até ser descoberto
        /// </summary>
        public bool IsHidden { get; set; }

        #endregion

        #region Constructor

        protected QuestObjective()
        {
            Required = 1;
            Current = 0;
            IsOptional = false;
            IsHidden = false;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Retorna descrição formatada com progresso
        /// </summary>
        public virtual string GetProgressDescription()
        {
            return $"{Description} ({Current}/{Required})";
        }

        /// <summary>
        /// Retorna porcentagem de progresso
        /// </summary>
        public float GetProgressPercent()
        {
            if (Required <= 0) return 100f;
            return Math.Min(100f, (float)Current / Required * 100f);
        }

        /// <summary>
        /// Incrementa progresso
        /// </summary>
        public virtual bool AddProgress(int amount = 1)
        {
            if (IsComplete) return false;

            Current = Math.Min(Current + amount, Required);
            return IsComplete;
        }

        /// <summary>
        /// Reseta progresso
        /// </summary>
        public virtual void Reset()
        {
            Current = 0;
        }

        /// <summary>
        /// Clona o objetivo
        /// </summary>
        public abstract QuestObjective Clone();

        /// <summary>
        /// Serializa para dicionário (para JSON)
        /// </summary>
        public virtual Dictionary<string, object> ToDictionary()
        {
            return new Dictionary<string, object>
            {
                { "type", Type.ToString() },
                { "description", Description },
                { "required", Required },
                { "current", Current },
                { "locationHint", LocationHint },
                { "isOptional", IsOptional },
                { "isHidden", IsHidden }
            };
        }

        #endregion
    }

    #region Kill Objective

    /// <summary>
    /// Objetivo: Matar criaturas
    /// </summary>
    public class KillObjective : QuestObjective
    {
        public string TargetType { get; set; }
        public string TargetName { get; set; }
        public List<string> TargetTypes { get; set; }
        public string RequiredRegion { get; set; }
        public bool CountPetKills { get; set; }
        public bool CountPartyKills { get; set; }

        public KillObjective()
        {
            Type = ObjectiveType.Kill;
            TargetTypes = new List<string>();
            CountPetKills = false;
            CountPartyKills = true;
        }

        public override QuestObjective Clone()
        {
            return new KillObjective
            {
                Type = this.Type,
                Description = this.Description,
                Required = this.Required,
                Current = 0,
                LocationHint = this.LocationHint,
                Index = this.Index,
                IsOptional = this.IsOptional,
                IsHidden = this.IsHidden,
                TargetType = this.TargetType,
                TargetName = this.TargetName,
                TargetTypes = new List<string>(this.TargetTypes),
                RequiredRegion = this.RequiredRegion,
                CountPetKills = this.CountPetKills,
                CountPartyKills = this.CountPartyKills
            };
        }

        public override Dictionary<string, object> ToDictionary()
        {
            var dict = base.ToDictionary();
            dict["targetType"] = TargetType;
            dict["targetName"] = TargetName;
            dict["requiredRegion"] = RequiredRegion;
            return dict;
        }
    }

    #endregion

    #region Collect Objective

    /// <summary>
    /// Objetivo: Coletar itens
    /// </summary>
    public class CollectObjective : QuestObjective
    {
        public string ItemType { get; set; }
        public string ItemName { get; set; }
        public int ItemHue { get; set; }
        public bool ConsumeOnComplete { get; set; }
        public bool MarkAsQuestItem { get; set; }
        public int DropChance { get; set; }
        public List<string> FromMobTypes { get; set; }

        public CollectObjective()
        {
            Type = ObjectiveType.Collect;
            ConsumeOnComplete = true;
            MarkAsQuestItem = false;
            DropChance = 100;
            FromMobTypes = new List<string>();
        }

        public override QuestObjective Clone()
        {
            return new CollectObjective
            {
                Type = this.Type,
                Description = this.Description,
                Required = this.Required,
                Current = 0,
                LocationHint = this.LocationHint,
                Index = this.Index,
                IsOptional = this.IsOptional,
                IsHidden = this.IsHidden,
                ItemType = this.ItemType,
                ItemName = this.ItemName,
                ItemHue = this.ItemHue,
                ConsumeOnComplete = this.ConsumeOnComplete,
                MarkAsQuestItem = this.MarkAsQuestItem,
                DropChance = this.DropChance,
                FromMobTypes = new List<string>(this.FromMobTypes)
            };
        }

        public override Dictionary<string, object> ToDictionary()
        {
            var dict = base.ToDictionary();
            dict["itemType"] = ItemType;
            dict["itemName"] = ItemName;
            dict["consumeOnComplete"] = ConsumeOnComplete;
            return dict;
        }
    }

    #endregion

    #region Explore Objective

    /// <summary>
    /// Objetivo: Explorar/visitar local
    /// </summary>
    public class ExploreObjective : QuestObjective
    {
        public string LocationName { get; set; }
        public int LocationX { get; set; }
        public int LocationY { get; set; }
        public int LocationZ { get; set; }
        public string MapName { get; set; }
        public int RadiusTiles { get; set; }
        public bool TriggerOnEnter { get; set; }
        public int StayDurationSeconds { get; set; }

        public ExploreObjective()
        {
            Type = ObjectiveType.Explore;
            RadiusTiles = 5;
            TriggerOnEnter = true;
            StayDurationSeconds = 0;
        }

        public override QuestObjective Clone()
        {
            return new ExploreObjective
            {
                Type = this.Type,
                Description = this.Description,
                Required = this.Required,
                Current = 0,
                LocationHint = this.LocationHint,
                Index = this.Index,
                IsOptional = this.IsOptional,
                IsHidden = this.IsHidden,
                LocationName = this.LocationName,
                LocationX = this.LocationX,
                LocationY = this.LocationY,
                LocationZ = this.LocationZ,
                MapName = this.MapName,
                RadiusTiles = this.RadiusTiles,
                TriggerOnEnter = this.TriggerOnEnter,
                StayDurationSeconds = this.StayDurationSeconds
            };
        }

        public Point3D GetLocation()
        {
            return new Point3D(LocationX, LocationY, LocationZ);
        }

        public Map GetMap()
        {
            switch (MapName?.ToLower())
            {
                case "felucca": return Map.Felucca;
                case "trammel": return Map.Trammel;
                case "ilshenar": return Map.Ilshenar;
                case "malas": return Map.Malas;
                case "tokuno": return Map.Tokuno;
                default: return Map.Trammel;
            }
        }
    }

    #endregion

    #region Deliver Objective

    /// <summary>
    /// Objetivo: Entregar item para NPC
    /// </summary>
    public class DeliverObjective : QuestObjective
    {
        public string ItemType { get; set; }
        public string ItemName { get; set; }
        public int ItemHue { get; set; }
        public bool GiveItemOnAccept { get; set; }
        public string TargetNpcId { get; set; }
        public string TargetNpcName { get; set; }
        public string OnItemLostAction { get; set; }

        public DeliverObjective()
        {
            Type = ObjectiveType.Deliver;
            GiveItemOnAccept = true;
            OnItemLostAction = "RespawnItem";
        }

        public override QuestObjective Clone()
        {
            return new DeliverObjective
            {
                Type = this.Type,
                Description = this.Description,
                Required = this.Required,
                Current = 0,
                LocationHint = this.LocationHint,
                Index = this.Index,
                IsOptional = this.IsOptional,
                IsHidden = this.IsHidden,
                ItemType = this.ItemType,
                ItemName = this.ItemName,
                ItemHue = this.ItemHue,
                GiveItemOnAccept = this.GiveItemOnAccept,
                TargetNpcId = this.TargetNpcId,
                TargetNpcName = this.TargetNpcName,
                OnItemLostAction = this.OnItemLostAction
            };
        }
    }

    #endregion

    #region TalkTo Objective

    /// <summary>
    /// Item concedido ao completar o objetivo
    /// </summary>
    public class ObjectiveGrantItem
    {
        public string ItemType { get; set; }
        public string ItemName { get; set; }
        public int Amount { get; set; } = 1;
    }

    /// <summary>
    /// Objetivo: Falar com NPC
    /// </summary>
    public class TalkToObjective : QuestObjective
    {
        public string TargetNpcId { get; set; }
        public string TargetNpcName { get; set; }
        public string SpeechTrigger { get; set; }

        /// <summary>
        /// Itens entregues ao jogador quando este objetivo é concluído
        /// </summary>
        public List<ObjectiveGrantItem> GrantItems { get; set; } = new List<ObjectiveGrantItem>();


        public TalkToObjective()
        {
            Type = ObjectiveType.TalkTo;
        }

        public override QuestObjective Clone()
        {
            var grantCopy = new List<ObjectiveGrantItem>();

            foreach (var g in this.GrantItems)
                grantCopy.Add(new ObjectiveGrantItem { ItemType = g.ItemType, ItemName = g.ItemName, Amount = g.Amount });

            return new TalkToObjective
            {
                Type = this.Type,
                Description = this.Description,
                Required = this.Required,
                Current = 0,
                LocationHint = this.LocationHint,
                Index = this.Index,
                IsOptional = this.IsOptional,
                IsHidden = this.IsHidden,
                TargetNpcId = this.TargetNpcId,
                TargetNpcName = this.TargetNpcName,
                SpeechTrigger = this.SpeechTrigger,
                GrantItems = grantCopy
            };
        }
    }

    #endregion

    #region Escort Objective

    /// <summary>
    /// Objetivo: Escoltar NPC
    /// </summary>
    public class EscortObjective : QuestObjective
    {
        public string EscortNpcId { get; set; }
        public string EscortNpcName { get; set; }
        public bool SpawnOnAccept { get; set; }
        public int SpawnX { get; set; }
        public int SpawnY { get; set; }
        public int SpawnZ { get; set; }
        public string SpawnMap { get; set; }
        public string DestinationName { get; set; }
        public int DestinationX { get; set; }
        public int DestinationY { get; set; }
        public int DestinationZ { get; set; }
        public string DestinationMap { get; set; }

        /// <summary>
        /// Nome da região de destino (ex.: "Britain", "Cove", "Nujel'm").
        /// Alternativa a destinationX/destinationY: o centro da região é usado.
        /// </summary>
        public string DestinationRegion { get; set; }

        public int RadiusToComplete { get; set; }
        public string OnEscortDead { get; set; }

        /// <summary>Body do NPC (0x190 = humano masculino, 0x191 = feminino).</summary>
        public int EscortBody { get; set; }

        /// <summary>Hue do NPC. 0 = aleatório.</summary>
        public int EscortHue { get; set; }

        /// <summary>Vida do NPC. 0 = padrão (120-150).</summary>
        public int EscortHits { get; set; }

        /// <summary>Se o NPC revida quando atacado. Padrão false (só segue).</summary>
        public bool DefendsItself { get; set; }

        /// <summary>
        /// Teletransporta o NPC até o jogador quando ele fica preso no terreno.
        /// Padrão true — sem isto, uma porta ou um degrau travava a escolta para
        /// sempre e a quest ficava impossível de concluir.
        /// </summary>
        public bool TeleportWhenStuck { get; set; }

        /// <summary>Distância máxima tolerada entre NPC e jogador antes do resgate.</summary>
        public int MaxDistanceFromOwner { get; set; }

        /// <summary>Segundos sem avançar antes de considerar o NPC preso.</summary>
        public int StuckTimeoutSeconds { get; set; }

        public EscortObjective()
        {
            Type = ObjectiveType.Escort;
            SpawnOnAccept = true;
            RadiusToComplete = 5;
            OnEscortDead = "FailQuest";
            EscortBody = 0x190;
            EscortHue = 0;
            EscortHits = 0;
            DefendsItself = false;
            TeleportWhenStuck = true;
            MaxDistanceFromOwner = 24;
            StuckTimeoutSeconds = 20;
        }

        public override QuestObjective Clone()
        {
            return new EscortObjective
            {
                Type = this.Type,
                Description = this.Description,
                Required = this.Required,
                Current = 0,
                LocationHint = this.LocationHint,
                Index = this.Index,
                IsOptional = this.IsOptional,
                IsHidden = this.IsHidden,
                EscortNpcId = this.EscortNpcId,
                EscortNpcName = this.EscortNpcName,
                SpawnOnAccept = this.SpawnOnAccept,
                SpawnX = this.SpawnX,
                SpawnY = this.SpawnY,
                SpawnZ = this.SpawnZ,
                SpawnMap = this.SpawnMap,
                DestinationName = this.DestinationName,
                DestinationX = this.DestinationX,
                DestinationY = this.DestinationY,
                DestinationZ = this.DestinationZ,
                DestinationMap = this.DestinationMap,
                DestinationRegion = this.DestinationRegion,
                RadiusToComplete = this.RadiusToComplete,
                OnEscortDead = this.OnEscortDead,
                EscortBody = this.EscortBody,
                EscortHue = this.EscortHue,
                EscortHits = this.EscortHits,
                DefendsItself = this.DefendsItself,
                TeleportWhenStuck = this.TeleportWhenStuck,
                MaxDistanceFromOwner = this.MaxDistanceFromOwner,
                StuckTimeoutSeconds = this.StuckTimeoutSeconds
            };
        }
    }

    #endregion

    #region Craft Objective

    /// <summary>
    /// Objetivo: Fabricar item
    /// </summary>
    public class CraftObjective : QuestObjective
    {
        public string ItemType { get; set; }
        public string ItemName { get; set; }
        public string CraftSkill { get; set; }
        public bool RequireExceptional { get; set; }
        public bool ConsumeOnComplete { get; set; }

        public CraftObjective()
        {
            Type = ObjectiveType.Craft;
            RequireExceptional = false;
            ConsumeOnComplete = true;
        }

        public override QuestObjective Clone()
        {
            return new CraftObjective
            {
                Type = this.Type,
                Description = this.Description,
                Required = this.Required,
                Current = 0,
                LocationHint = this.LocationHint,
                Index = this.Index,
                IsOptional = this.IsOptional,
                IsHidden = this.IsHidden,
                ItemType = this.ItemType,
                ItemName = this.ItemName,
                CraftSkill = this.CraftSkill,
                RequireExceptional = this.RequireExceptional,
                ConsumeOnComplete = this.ConsumeOnComplete
            };
        }
    }

    #endregion

    #region UseSkill Objective

    /// <summary>
    /// Objetivo: Usar skill
    /// </summary>
    public class UseSkillObjective : QuestObjective
    {
        public string SkillName { get; set; }
        public SkillName Skill { get; set; }
        public bool RequireSuccess { get; set; }

        public UseSkillObjective()
        {
            Type = ObjectiveType.UseSkill;
            RequireSuccess = false;
        }

        public override QuestObjective Clone()
        {
            return new UseSkillObjective
            {
                Type = this.Type,
                Description = this.Description,
                Required = this.Required,
                Current = 0,
                LocationHint = this.LocationHint,
                Index = this.Index,
                IsOptional = this.IsOptional,
                IsHidden = this.IsHidden,
                SkillName = this.SkillName,
                Skill = this.Skill,
                RequireSuccess = this.RequireSuccess
            };
        }
    }

    #endregion

    #region SkillGain Objective

    /// <summary>
    /// Objetivo: Ganhar skill
    /// </summary>
    public class SkillGainObjective : QuestObjective
    {
        public string SkillName { get; set; }
        public SkillName Skill { get; set; }
        public double TargetValue { get; set; }
        public double StartValue { get; set; }
        public string Mode { get; set; } // "ReachValue" ou "GainAmount"

        public SkillGainObjective()
        {
            Type = ObjectiveType.SkillGain;
            Mode = "ReachValue";
        }

        public override QuestObjective Clone()
        {
            return new SkillGainObjective
            {
                Type = this.Type,
                Description = this.Description,
                Required = this.Required,
                Current = 0,
                LocationHint = this.LocationHint,
                Index = this.Index,
                IsOptional = this.IsOptional,
                IsHidden = this.IsHidden,
                SkillName = this.SkillName,
                Skill = this.Skill,
                TargetValue = this.TargetValue,
                StartValue = this.StartValue,
                Mode = this.Mode
            };
        }
    }

    #endregion

    #region Interact Objective

    /// <summary>
    /// Objetivo: Interagir com objeto/NPC
    /// </summary>
    public class InteractObjective : QuestObjective
    {
        public string TargetType { get; set; }
        public string TargetName { get; set; }
        public List<Serial> TargetSerials { get; set; }
        public string InteractionType { get; set; } // "DoubleClick", "Speech"
        public string SpeechTrigger { get; set; }

        public InteractObjective()
        {
            Type = ObjectiveType.Interact;
            TargetSerials = new List<Serial>();
            InteractionType = "DoubleClick";
        }

        public override QuestObjective Clone()
        {
            return new InteractObjective
            {
                Type = this.Type,
                Description = this.Description,
                Required = this.Required,
                Current = 0,
                LocationHint = this.LocationHint,
                Index = this.Index,
                IsOptional = this.IsOptional,
                IsHidden = this.IsHidden,
                TargetType = this.TargetType,
                TargetName = this.TargetName,
                TargetSerials = new List<Serial>(this.TargetSerials),
                InteractionType = this.InteractionType,
                SpeechTrigger = this.SpeechTrigger
            };
        }
    }

    #endregion

    #region Defend Objective

    /// <summary>
    /// Objetivo: Defender local
    /// </summary>
    public class DefendObjective : QuestObjective
    {
        public string LocationName { get; set; }
        public int LocationX { get; set; }
        public int LocationY { get; set; }
        public string MapName { get; set; }
        public int RadiusTiles { get; set; }
        public int TotalWaves { get; set; }
        public int CurrentWave { get; set; }
        public string EnemyType { get; set; }
        public int EnemiesPerWave { get; set; }
        public int TimeBetweenWavesSeconds { get; set; }

        public DefendObjective()
        {
            Type = ObjectiveType.Defend;
            RadiusTiles = 15;
            TotalWaves = 3;
            CurrentWave = 0;
            EnemiesPerWave = 5;
            TimeBetweenWavesSeconds = 30;
        }

        public override QuestObjective Clone()
        {
            return new DefendObjective
            {
                Type = this.Type,
                Description = this.Description,
                Required = this.Required,
                Current = 0,
                LocationHint = this.LocationHint,
                Index = this.Index,
                IsOptional = this.IsOptional,
                IsHidden = this.IsHidden,
                LocationName = this.LocationName,
                LocationX = this.LocationX,
                LocationY = this.LocationY,
                MapName = this.MapName,
                RadiusTiles = this.RadiusTiles,
                TotalWaves = this.TotalWaves,
                CurrentWave = 0,
                EnemyType = this.EnemyType,
                EnemiesPerWave = this.EnemiesPerWave,
                TimeBetweenWavesSeconds = this.TimeBetweenWavesSeconds
            };
        }
    }

    #endregion

    #region Destroy Objective

    /// <summary>
    /// Objetivo: Destruir objeto/estrutura
    /// </summary>
    public class DestroyObjective : QuestObjective
    {
        public string TargetType { get; set; }
        public string TargetName { get; set; }
        public List<Serial> TargetSerials { get; set; }
        public bool RespawnOnFail { get; set; }

        public DestroyObjective()
        {
            Type = ObjectiveType.Destroy;
            TargetSerials = new List<Serial>();
            RespawnOnFail = true;
        }

        public override QuestObjective Clone()
        {
            return new DestroyObjective
            {
                Type = this.Type,
                Description = this.Description,
                Required = this.Required,
                Current = 0,
                LocationHint = this.LocationHint,
                Index = this.Index,
                IsOptional = this.IsOptional,
                IsHidden = this.IsHidden,
                TargetType = this.TargetType,
                TargetName = this.TargetName,
                TargetSerials = new List<Serial>(this.TargetSerials),
                RespawnOnFail = this.RespawnOnFail
            };
        }
    }

    #endregion

    #region UseItem Objective

    /// <summary>
    /// Objetivo: Usar (duplo clique) um item especifico.
    /// Alimentado por EventSink.OnItemUse via QuestManager.OnItemUsed.
    /// </summary>
    public class UseItemObjective : QuestObjective
    {
        /// <summary>Classe C# do item ou tag virtual (ex.: "SeloEmissario").</summary>
        public string ItemType { get; set; }

        /// <summary>Nome amigavel do item (ex.: "Selo do Emissario").</summary>
        public string ItemName { get; set; }

        public int ItemHue { get; set; }

        /// <summary>Consome o item ao contar o progresso.</summary>
        public bool ConsumeOnUse { get; set; }

        public UseItemObjective()
        {
            Type = ObjectiveType.UseItem;
            ConsumeOnUse = false;
        }

        public override QuestObjective Clone()
        {
            return new UseItemObjective
            {
                Type = this.Type,
                Description = this.Description,
                Required = this.Required,
                Current = 0,
                LocationHint = this.LocationHint,
                Index = this.Index,
                IsOptional = this.IsOptional,
                IsHidden = this.IsHidden,
                ItemType = this.ItemType,
                ItemName = this.ItemName,
                ItemHue = this.ItemHue,
                ConsumeOnUse = this.ConsumeOnUse
            };
        }

        public override Dictionary<string, object> ToDictionary()
        {
            var dict = base.ToDictionary();
            dict["itemType"] = ItemType;
            dict["itemName"] = ItemName;
            dict["consumeOnUse"] = ConsumeOnUse;
            return dict;
        }
    }

    #endregion

    #region Survive Objective

    /// <summary>
    /// Objetivo: Sobreviver por um periodo de tempo.
    /// O progresso e medido em segundos desde o inicio do objetivo e
    /// avancado por QuestSurviveTimer (veja QuestObjectiveDrivers).
    /// </summary>
    public class SurviveObjective : QuestObjective
    {
        /// <summary>Segundos que o jogador precisa permanecer vivo.</summary>
        public int DurationSeconds { get; set; }

        /// <summary>Se verdadeiro, a morte do jogador falha a quest.</summary>
        public bool FailOnDeath { get; set; }

        /// <summary>Regiao (nome) onde o jogador precisa permanecer. Vazio = qualquer lugar.</summary>
        public string RequiredRegion { get; set; }

        public SurviveObjective()
        {
            Type = ObjectiveType.Survive;
            DurationSeconds = 60;
            FailOnDeath = true;
        }

        public override QuestObjective Clone()
        {
            return new SurviveObjective
            {
                Type = this.Type,
                Description = this.Description,
                Required = this.Required,
                Current = 0,
                LocationHint = this.LocationHint,
                Index = this.Index,
                IsOptional = this.IsOptional,
                IsHidden = this.IsHidden,
                DurationSeconds = this.DurationSeconds,
                FailOnDeath = this.FailOnDeath,
                RequiredRegion = this.RequiredRegion
            };
        }

        public override Dictionary<string, object> ToDictionary()
        {
            var dict = base.ToDictionary();
            dict["durationSeconds"] = DurationSeconds;
            dict["failOnDeath"] = FailOnDeath;
            dict["requiredRegion"] = RequiredRegion;
            return dict;
        }
    }

    #endregion
}