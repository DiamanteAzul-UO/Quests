/*****************************************************
 * QuestEscortable.cs
 * NPC escoltavel gerado por objetivos do tipo Escort
 *
 * REAVALIACAO COMPLETA DO SISTEMA DE ESCOLTA (v2)
 * -----------------------------------------------
 * Escolta e um dos objetivos mais usados e historicamente o que mais quebra em
 * shards. Os modos de falha reais eram:
 *
 *   1. O NPC ficava preso em porta/degrau/terreno e nunca chegava ao destino
 *      => a quest ficava impossivel PARA SEMPRE (nem falhava, nem completava).
 *      Corrigido com deteccao de "preso" + resgate por teleporte.
 *
 *   2. O GM so sabe o NOME do lugar ("Britain"), nao as coordenadas. Sem
 *      destino valido, IsAtDestination() nunca era true.
 *      Corrigido com destinationRegion: o centro da regiao e resolvido em
 *      runtime, e chegar dentro da regiao tambem conta.
 *
 *   3. O NPC consumia 1 slot de seguidor. Jogador com pet no FollowersMax nao
 *      conseguia iniciar a escolta.
 *      Corrigido: ControlSlots = 0.
 *
 *   4. O NPC entrava em combate e abandonava o jogador.
 *      Corrigido: FightMode.None por padrao (defendsItself liga a reacao).
 *
 *   5. Nao acompanhava jogador correndo.
 *      Corrigido: ActiveSpeed ajustado e aceleracao quando fica para tras.
 *
 *   6. Depois de restart do servidor o vinculo se perdia e exigia varrer
 *      World.Mobiles inteiro a cada login.
 *      Corrigido: registro estatico indexado por quest+objetivo+dono.
 *
 * Versao: 2.0.0
 * Compativel com: ServUO
 *****************************************************/

using System;
using System.Collections.Generic;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Server.Custom.QuestSystem.NPCs
{
    public class QuestEscortable : BaseCreature, IQuestNpcIdentity
    {
        /// <summary>
        /// Registro de escoltas vivas, indexado por "questId|objetivo|serialDoDono".
        /// Permite religar o driver em O(1) depois de restart, sem varrer o mundo.
        /// </summary>
        private static readonly Dictionary<string, QuestEscortable> _registry =
            new Dictionary<string, QuestEscortable>(StringComparer.Ordinal);

        private string m_QuestNpcId;
        private string m_QuestId;
        private int m_ObjectiveIndex;
        private string m_DestinationName;
        private string m_DestinationRegion;
        private Point3D m_Destination;
        private Map m_DestinationMap;
        private int m_RadiusToComplete;
        private bool m_Arrived;
        private bool m_DefendsItself;
        private bool m_TeleportWhenStuck = true;
        private int m_MaxDistanceFromOwner = 24;
        private int m_StuckTimeoutSeconds = 20;

        // Controle de "preso no terreno"
        private Point3D m_LastProgressLocation;
        private double m_LastProgressDistance;
        private DateTime m_LastProgressAt;
        private int m_RescueCount;

        #region Properties

        [CommandProperty(AccessLevel.GameMaster)]
        public string QuestNpcId
        {
            get { return m_QuestNpcId; }
            set { m_QuestNpcId = value; }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public string QuestId
        {
            get { return m_QuestId; }
            set { m_QuestId = value; }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public int ObjectiveIndex
        {
            get { return m_ObjectiveIndex; }
            set { m_ObjectiveIndex = value; }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public string DestinationName
        {
            get { return m_DestinationName; }
            set { m_DestinationName = value; }
        }

        /// <summary>Região de destino (alternativa às coordenadas).</summary>
        [CommandProperty(AccessLevel.GameMaster)]
        public string DestinationRegion
        {
            get { return m_DestinationRegion; }
            set
            {
                m_DestinationRegion = value;

                // Re-resolve o centro da região quando o GM edita em runtime
                if (m_Destination == Point3D.Zero)
                    ResolveRegionDestination();
            }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public Point3D Destination
        {
            get { return m_Destination; }
            set { m_Destination = value; }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public Map DestinationMap
        {
            get { return m_DestinationMap; }
            set { m_DestinationMap = value; }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public int RadiusToComplete
        {
            get { return m_RadiusToComplete; }
            set { m_RadiusToComplete = value; }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public bool DefendsItself
        {
            get { return m_DefendsItself; }
            set
            {
                m_DefendsItself = value;
                FightMode = value ? FightMode.Aggressor : FightMode.None;
            }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public bool TeleportWhenStuck
        {
            get { return m_TeleportWhenStuck; }
            set { m_TeleportWhenStuck = value; }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public int MaxDistanceFromOwner
        {
            get { return m_MaxDistanceFromOwner; }
            set { m_MaxDistanceFromOwner = value < 4 ? 4 : value; }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public int StuckTimeoutSeconds
        {
            get { return m_StuckTimeoutSeconds; }
            set { m_StuckTimeoutSeconds = value < 5 ? 5 : value; }
        }

        /// <summary>Jogador que esta fazendo a escolta.</summary>
        public PlayerMobile QuestOwner { get; set; }

        /// <summary>True depois que o destino foi alcancado.</summary>
        [CommandProperty(AccessLevel.GameMaster)]
        public bool Arrived
        {
            get { return m_Arrived; }
            set { m_Arrived = value; }
        }

        /// <summary>Quantas vezes o NPC precisou ser resgatado.</summary>
        public int RescueCount { get { return m_RescueCount; } }

        #endregion

        #region Constructors

        [Constructable]
        public QuestEscortable() : base(AIType.AI_Melee, FightMode.None, 18, 2)
        {
            Body = 0x190;
            Hue = Utility.RandomSkinHue();
            Name = "Escolta de Missao";

            m_DestinationMap = Map.Trammel;
            m_RadiusToComplete = 5;
            m_LastProgressAt = DateTime.UtcNow;

            Female = false;

            SetStr(70, 90);
            SetDex(90, 110);
            SetInt(50, 70);

            SetHits(120, 150);
            SetStam(110, 130);

            SetDamage(5, 10);
            SetDamageType(ResistanceType.Physical, 100);

            SetResistance(ResistanceType.Physical, 20, 30);
            SetResistance(ResistanceType.Fire, 10, 20);
            SetResistance(ResistanceType.Cold, 10, 20);
            SetResistance(ResistanceType.Poison, 10, 20);
            SetResistance(ResistanceType.Energy, 10, 20);

            SetSkill(SkillName.Swords, 50.0, 70.0);
            SetSkill(SkillName.Tactics, 50.0, 70.0);
            SetSkill(SkillName.MagicResist, 60.0, 80.0);
            SetSkill(SkillName.Anatomy, 40.0, 60.0);

            Fame = 0;
            Karma = 500;

            Tamable = false;

            // NAO consome slot de seguidor: com ControlSlots = 1, um jogador
            // que ja tivesse pet no FollowersMax nao conseguia iniciar a escolta.
            ControlSlots = 0;

            // Acompanha jogador correndo. O padrao do BaseCreature e lento
            // demais para distancias longas de escolta.
            ActiveSpeed = 0.10;
            PassiveSpeed = 0.20;
        }

        public QuestEscortable(Serial serial) : base(serial)
        {
        }

        #endregion

        #region Registry

        private string RegistryKey
        {
            get
            {
                return RegistryKeyFor(m_QuestId, m_ObjectiveIndex,
                    QuestOwner != null ? QuestOwner.Serial : Serial.Zero);
            }
        }

        private static string RegistryKeyFor(string questId, int index, Serial owner)
        {
            return questId + "|" + index + "|" + owner.Value;
        }

        /// <summary>
        /// Localiza a escolta viva de um jogador/quest/objetivo.
        /// </summary>
        public static QuestEscortable Find(string questId, int objectiveIndex, PlayerMobile owner)
        {
            if (string.IsNullOrEmpty(questId))
                return null;

            QuestEscortable npc;

            if (owner != null &&
                _registry.TryGetValue(RegistryKeyFor(questId, objectiveIndex, owner.Serial), out npc) &&
                npc != null && !npc.Deleted)
            {
                return npc;
            }

            // Fallback: o registro pode ter sido perdido (NPC criado antes desta
            // versao, ou serial do dono ainda indisponivel durante o load).
            List<string> stale = null;

            foreach (var kvp in _registry)
            {
                QuestEscortable candidate = kvp.Value;

                if (candidate == null || candidate.Deleted)
                {
                    if (stale == null) stale = new List<string>();
                    stale.Add(kvp.Key);
                    continue;
                }

                if (candidate.m_QuestId != questId || candidate.m_ObjectiveIndex != objectiveIndex)
                    continue;

                if (owner != null && candidate.QuestOwner != null &&
                    candidate.QuestOwner.Serial != owner.Serial)
                    continue;

                return candidate;
            }

            if (stale != null)
            {
                for (int i = 0; i < stale.Count; i++)
                    _registry.Remove(stale[i]);
            }

            return null;
        }

        /// <summary>Registra esta escolta no indice.</summary>
        public void Register()
        {
            if (string.IsNullOrEmpty(m_QuestId))
                return;

            _registry[RegistryKey] = this;
        }

        private void Unregister()
        {
            if (string.IsNullOrEmpty(m_QuestId))
                return;

            QuestEscortable current;
            if (_registry.TryGetValue(RegistryKey, out current) && (current == null || current == this))
                _registry.Remove(RegistryKey);
        }

        #endregion

        #region Destination resolution

        /// <summary>
        /// Resolve o destino a partir do NOME da regiao quando o JSON não trouxe
        /// coordenadas. É o caso mais comum: o GM escreve "Leve-o até Britain".
        /// </summary>
        public bool ResolveRegionDestination()
        {
            if (m_Destination != Point3D.Zero)
                return true;

            if (string.IsNullOrWhiteSpace(m_DestinationRegion))
                return false;

            Region region = FindRegion(m_DestinationRegion, m_DestinationMap ?? Map);

            if (region == null)
                return false;

            Point3D center;
            Map map;

            if (!TryGetRegionCenter(region, out center, out map))
                return false;

            m_Destination = center;

            if (map != null)
                m_DestinationMap = map;

            if (string.IsNullOrWhiteSpace(m_DestinationName))
                m_DestinationName = region.Name;

            // Região é um alvo grande: um raio de 5 tiles quase nunca casa com o
            // centro geométrico. Usa um raio compatível com o tamanho da região.
            if (m_RadiusToComplete <= 5)
                m_RadiusToComplete = 24;

            return true;
        }

        private static Region FindRegion(string name, Map preferred)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            if (preferred != null)
            {
                Region region = FindRegionInMap(name, preferred);
                if (region != null)
                    return region;
            }

            foreach (Map map in Map.Maps)
            {
                if (map == null)
                    continue;

                Region region = FindRegionInMap(name, map);
                if (region != null)
                    return region;
            }

            return null;
        }

        private static Region FindRegionInMap(string name, Map map)
        {
            if (map == null || map.Regions == null)
                return null;

            Region exact;
            if (map.Regions.TryGetValue(name, out exact))
                return exact;

            foreach (var kvp in map.Regions)
            {
                if (kvp.Value == null)
                    continue;

                if (string.Equals(kvp.Key, name, StringComparison.OrdinalIgnoreCase))
                    return kvp.Value;

                if (kvp.Value.Name != null &&
                    kvp.Value.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return kvp.Value;
            }

            return null;
        }

        /// <summary>Centro aproximado de uma região (ponto médio da área).</summary>
        private static bool TryGetRegionCenter(Region region, out Point3D center, out Map map)
        {
            center = Point3D.Zero;
            map = null;

            if (region == null)
                return false;

            map = region.Map;

            // Region.Area e Rectangle3D[] (Server/Geometry.cs), com Start/End
            // absolutos — nao ha Width/Height.
            Rectangle3D[] areas = region.Area;

            if (areas == null || areas.Length == 0)
                return false;

            int minX = int.MaxValue, minY = int.MaxValue;
            int maxX = int.MinValue, maxY = int.MinValue;

            foreach (Rectangle3D rect in areas)
            {
                if (rect.Start.X < minX) minX = rect.Start.X;
                if (rect.Start.Y < minY) minY = rect.Start.Y;
                if (rect.End.X > maxX) maxX = rect.End.X;
                if (rect.End.Y > maxY) maxY = rect.End.Y;
            }

            if (minX > maxX || minY > maxY)
                return false;

            int x = (minX + maxX) / 2;
            int y = (minY + maxY) / 2;

            center = new Point3D(x, y, 0);

            // Ajusta o Z para o terreno real do ponto central
            if (map != null)
            {
                try
                {
                    // Assinatura real: GetAverageZ(int x, int y, ref int z, ref int avg, ref int top)
                    int z = 0, avg = 0, top = 0;
                    map.GetAverageZ(x, y, ref z, ref avg, ref top);
                    center = new Point3D(x, y, avg);
                }
                catch
                {
                    // mantém Z = 0 se o mapa não souber responder
                }
            }

            return true;
        }

        /// <summary>
        /// Verifica se o NPC chegou ao destino.
        /// Aceita coordenadas + raio OU estar dentro da região declarada.
        /// </summary>
        public bool IsAtDestination()
        {
            if (m_Arrived)
                return true;

            // Por região: estar dentro dela já conta como chegada.
            if (!string.IsNullOrWhiteSpace(m_DestinationRegion) && Region != null)
            {
                try
                {
                    if (Region.IsPartOf(m_DestinationRegion))
                        return true;
                }
                catch
                {
                    // região com nome inválido — cai na checagem por coordenadas
                }
            }

            if (m_Destination == Point3D.Zero)
                return false;

            if (m_DestinationMap != null && Map != null && Map != m_DestinationMap)
                return false;

            int radius = m_RadiusToComplete > 0 ? m_RadiusToComplete : 5;

            return InRange(m_Destination, radius);
        }

        /// <summary>Distância restante até o destino, ou -1 se desconhecida.</summary>
        public int DistanceToDestination()
        {
            if (m_Destination == Point3D.Zero)
                return -1;

            if (m_DestinationMap != null && Map != null && Map != m_DestinationMap)
                return -1;

            return (int)GetDistanceToSqrt(m_Destination);
        }

        #endregion

        #region Behavior

        public override bool CanAutoStable { get { return false; } }

        public override bool IsBondable { get { return false; } }

        public override bool CanBeRenamedBy(Mobile from) { return false; }

        /// <summary>Escolta de quest não dropa loot.</summary>
        public override void GenerateLoot() { }

        public override void GenerateLoot(bool respawn) { }

        /// <summary>Não deixa outro jogador "roubar" o controle da escolta.</summary>
        public override bool CheckControlChance(Mobile m)
        {
            if (QuestOwner != null && m != QuestOwner)
                return false;

            return base.CheckControlChance(m);
        }

        public override void OnDoubleClick(Mobile from)
        {
            if (from == null || from.Deleted)
                return;

            if (!from.InRange(this, 4))
                return;

            if (m_Arrived)
            {
                SayTo(from, "Chegamos. Obrigado pela escolta.");
                return;
            }

            int distance = DistanceToDestination();
            string place = !string.IsNullOrWhiteSpace(m_DestinationName)
                ? m_DestinationName
                : (!string.IsNullOrWhiteSpace(m_DestinationRegion) ? m_DestinationRegion : "meu destino");

            if (distance >= 0)
                SayTo(from, $"Leve-me ate {place}. Faltam cerca de {distance} passos.");
            else
                SayTo(from, $"Leve-me ate {place}, por favor.");
        }

        /// <summary>
        /// Chamado pelo timer de IA do BaseCreature.
        /// Aqui vive a detecção de "preso no terreno" e o resgate.
        /// </summary>
        public override void OnThink()
        {
            base.OnThink();

            if (Deleted || m_Arrived || !Alive)
                return;

            if (QuestOwner == null || QuestOwner.Deleted)
                return;

            // Sem destino conhecido não há o que monitorar
            if (m_Destination == Point3D.Zero && string.IsNullOrWhiteSpace(m_DestinationRegion))
                return;

            // Tenta resolver o destino por região ainda pendente
            if (m_Destination == Point3D.Zero)
                ResolveRegionDestination();

            if (m_Destination == Point3D.Zero)
                return;

            double distance = GetDistanceToSqrt(m_Destination);

            // Houve avanço real em direção ao destino?
            if (distance < m_LastProgressDistance - 1.0 ||
                (m_LastProgressLocation != Location && distance <= m_LastProgressDistance))
            {
                m_LastProgressDistance = distance;
                m_LastProgressLocation = Location;
                m_LastProgressAt = DateTime.UtcNow;
            }

            double secondsStuck = (DateTime.UtcNow - m_LastProgressAt).TotalSeconds;

            // Acelera quando fica para trás do jogador
            bool ownerFar = Map == QuestOwner.Map && !InRange(QuestOwner, 6);

            if (ownerFar)
                ActiveSpeed = 0.08;
            else
                ActiveSpeed = 0.10;

            if (secondsStuck < m_StuckTimeoutSeconds)
                return;

            // ── NPC está preso ───────────────────────────────────────────────
            m_LastProgressAt = DateTime.UtcNow;
            m_LastProgressDistance = distance;
            m_LastProgressLocation = Location;

            bool sameMap = Map == QuestOwner.Map;
            bool tooFar = !sameMap || !InRange(QuestOwner, m_MaxDistanceFromOwner);

            if (!m_TeleportWhenStuck)
            {
                if (tooFar)
                    Say("Espere por mim! Nao consigo acompanhar...");

                return;
            }

            // Sem teleporte quando já está perto do dono e apenas parado
            if (!tooFar && secondsStuck < m_StuckTimeoutSeconds * 2)
                return;

            m_RescueCount++;

            Point3D target = QuestOwner.Location;
            Map targetMap = QuestOwner.Map;

            // Evita cair exatamente em cima do jogador
            for (int attempt = 0; attempt < 8; attempt++)
            {
                int dx = Utility.Random(-2, 5);
                int dy = Utility.Random(-2, 5);

                Point3D candidate = new Point3D(target.X + dx, target.Y + dy, target.Z);

                if (targetMap != null && targetMap.CanFit(candidate.X, candidate.Y, candidate.Z, 16, false, false, true))
                {
                    target = candidate;
                    break;
                }
            }

            Effects.SendLocationParticles(this, 0x3728, 10, 10, 0, 0);
            PlaySound(0x1FE);

            MoveToWorld(target, targetMap);

            Effects.SendLocationParticles(this, 0x3728, 10, 10, 0, 0);
            PlaySound(0x1FE);

            Combatant = null;
            FocusMob = null;

            m_LastProgressDistance = GetDistanceToSqrt(m_Destination);
            m_LastProgressLocation = Location;
            m_LastProgressAt = DateTime.UtcNow;

            if (m_RescueCount == 1)
                Say("Espere! Me perdi no caminho.");
            else if (m_RescueCount % 3 == 0)
                Say("De novo? Vamos logo, por favor.");
        }

        /// <summary>Reage a dano: sem defesa propria, nao entra em combate.</summary>
        public override void OnDamagedBySpell(Mobile caster)
        {
            if (!m_DefendsItself && caster != QuestOwner)
                return;     // não persegue o agressor: continuaria seguindo

            base.OnDamagedBySpell(caster);
        }

        public override void OnGotMeleeAttack(Mobile attacker)
        {
            if (!m_DefendsItself && attacker != QuestOwner)
                return;

            base.OnGotMeleeAttack(attacker);
        }

        public override void OnDeath(Container c)
        {
            Unregister();

            Quests.QuestObjectiveDrivers.NotifyEscortDied(this);

            base.OnDeath(c);
        }

        public override void Delete()
        {
            Unregister();
            base.Delete();
        }

        #endregion

        #region Serialization

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(1); // version

            writer.Write(m_QuestNpcId);
            writer.Write(m_QuestId);
            writer.Write(m_ObjectiveIndex);
            writer.Write(m_DestinationName);
            writer.Write(m_DestinationRegion);
            writer.Write(m_Destination);
            writer.Write(m_DestinationMap);
            writer.Write(m_RadiusToComplete);
            writer.Write(m_Arrived);
            writer.Write(m_DefendsItself);
            writer.Write(m_TeleportWhenStuck);
            writer.Write(m_MaxDistanceFromOwner);
            writer.Write(m_StuckTimeoutSeconds);
            writer.Write(QuestOwner);
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            int version = reader.ReadInt();

            m_QuestNpcId = reader.ReadString();
            m_QuestId = reader.ReadString();
            m_ObjectiveIndex = reader.ReadInt();
            m_DestinationName = reader.ReadString();

            if (version >= 1)
                m_DestinationRegion = reader.ReadString();

            m_Destination = reader.ReadPoint3D();
            m_DestinationMap = reader.ReadMap();
            m_RadiusToComplete = reader.ReadInt();
            m_Arrived = reader.ReadBool();

            if (version >= 1)
            {
                m_DefendsItself = reader.ReadBool();
                m_TeleportWhenStuck = reader.ReadBool();
                m_MaxDistanceFromOwner = reader.ReadInt();
                m_StuckTimeoutSeconds = reader.ReadInt();
            }

            QuestOwner = reader.ReadMobile() as PlayerMobile;

            m_LastProgressAt = DateTime.UtcNow;
            m_LastProgressLocation = Location;
            m_LastProgressDistance = m_Destination != Point3D.Zero ? GetDistanceToSqrt(m_Destination) : 0;

            FightMode = m_DefendsItself ? FightMode.Aggressor : FightMode.None;

            if (m_Destination == Point3D.Zero)
                ResolveRegionDestination();

            // Reentra no indice para o driver religar no login do dono
            Register();
        }

        #endregion
    }
}
