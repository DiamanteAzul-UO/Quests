/*****************************************************
 * QuestObjectiveDrivers.cs
 * Drivers de objetivos que precisam de processo continuo
 *
 * Cobrem os tipos de objetivo que existiam no enum e no loader mas NAO tinham
 * nenhuma logica de execucao — e por isso nunca podiam ser concluidos:
 *
 *   Escort   : spawna o NPC, faz seguir o jogador, detecta chegada ao destino
 *              e aplica OnEscortDead quando ele morre.
 *   Defend   : spawna as ondas de inimigos no ponto defendido e conta as mortes.
 *   Survive  : conta o tempo vivo (e na regiao, se configurada).
 *   Explore  : tempo de permanencia (StayDurationSeconds) dentro da area.
 *
 * O progresso gerado aqui passa sempre por QuestManager.UpdateProgress, entao
 * sobrevive a restart do servidor junto com o resto do PlayerQuestData.
 *
 * Versao: 1.0.0
 * Compativel com: ServUO
 *****************************************************/

using System;
using System.Collections.Generic;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Custom.QuestSystem.NPCs;

namespace Server.Custom.QuestSystem.Quests
{
    /// <summary>
    /// Gerencia timers e entidades ativas de objetivos "continuados".
    /// </summary>
    public static class QuestObjectiveDrivers
    {
        private static readonly Dictionary<string, StayTimer> _stays = new Dictionary<string, StayTimer>();
        private static readonly Dictionary<string, SurviveTimer> _survives = new Dictionary<string, SurviveTimer>();
        private static readonly Dictionary<string, EscortRun> _escorts = new Dictionary<string, EscortRun>();
        private static readonly Dictionary<string, DefendRun> _defends = new Dictionary<string, DefendRun>();

        private static string Key(PlayerMobile player, string questId, int index)
        {
            return Key((Mobile)player, questId, index);
        }

        private static string Key(Mobile player, string questId, int index)
        {
            return (player != null ? player.Serial.Value : 0) + "|" + questId + "|" + index;
        }

        #region Lifecycle

        /// <summary>
        /// Inicia os drivers necessarios para uma quest (aceita ou retomada no login).
        /// </summary>
        public static void StartForQuest(QuestManager manager, PlayerMobile player, QuestDefinition definition, ActiveQuest active)
        {
            if (manager == null || player == null || player.Deleted || definition == null || active == null)
                return;

            for (int i = 0; i < definition.Objectives.Count && i < active.Progress.Count; i++)
            {
                if (active.Progress[i].IsComplete)
                    continue;

                var objective = definition.Objectives[i];

                if (objective is EscortObjective)
                    StartEscort(manager, player, definition, active, i, (EscortObjective)objective);
                else if (objective is DefendObjective)
                    StartDefend(manager, player, definition, active, i, (DefendObjective)objective);
                else if (objective is SurviveObjective)
                    StartSurvive(manager, player, definition, i, (SurviveObjective)objective);
            }
        }

        /// <summary>Retoma os drivers de todas as quests ativas do jogador (login).</summary>
        public static void ResumeForPlayer(QuestManager manager, PlayerMobile player)
        {
            if (manager == null || player == null || player.Deleted)
                return;

            var data = manager.GetPlayerData(player);
            if (data == null)
                return;

            foreach (var active in data.GetAllActiveQuests())
            {
                if (active.Status != QuestStatus.Active && active.Status != QuestStatus.ReadyToTurnIn)
                    continue;

                var definition = QuestRepository.Instance.GetQuest(active.QuestId);
                if (definition == null)
                    continue;

                StartForQuest(manager, player, definition, active);
            }
        }

        /// <summary>
        /// Encerra todos os drivers ligados a uma quest (entrega, falha ou abandono).
        /// </summary>
        public static void StopForQuest(PlayerMobile player, string questId)
        {
            if (player == null || string.IsNullOrEmpty(questId))
                return;

            string prefix = player.Serial.Value + "|" + questId + "|";

            StopByPrefix(_stays, prefix);
            StopByPrefix(_survives, prefix);
            StopByPrefix(_escorts, prefix);
            StopByPrefix(_defends, prefix);
        }

        /// <summary>Encerra todos os drivers de um jogador (logout).</summary>
        public static void StopForPlayer(PlayerMobile player)
        {
            if (player == null)
                return;

            string prefix = player.Serial.Value + "|";

            StopByPrefix(_stays, prefix);
            StopByPrefix(_survives, prefix);
            StopByPrefix(_escorts, prefix);
            StopByPrefix(_defends, prefix);
        }

        private static void StopByPrefix<T>(Dictionary<string, T> table, string prefix) where T : IQuestDriver
        {
            List<string> doomed = null;

            foreach (var kvp in table)
            {
                if (kvp.Key.StartsWith(prefix, StringComparison.Ordinal))
                {
                    if (doomed == null)
                        doomed = new List<string>();

                    doomed.Add(kvp.Key);
                }
            }

            if (doomed == null)
                return;

            foreach (string key in doomed)
            {
                T driver;
                if (table.TryGetValue(key, out driver))
                    driver.Shutdown();

                table.Remove(key);
            }
        }

        #endregion

        #region Stay (Explore com permanencia)

        /// <summary>
        /// Inicia a contagem de permanencia na area de um objetivo Explore.
        /// </summary>
        public static void BeginStay(QuestManager manager, PlayerMobile player, string questId, int index, int seconds)
        {
            if (manager == null || player == null || player.Deleted || seconds <= 0)
                return;

            string key = Key(player, questId, index);

            if (_stays.ContainsKey(key))
                return;     // ja esta contando

            StayTimer timer = new StayTimer(manager, player, questId, index, seconds);
            _stays[key] = timer;
            timer.Start();
        }

        /// <summary>Cancela a contagem de permanencia (jogador saiu da area).</summary>
        public static void CancelStay(PlayerMobile player, string questId, int index)
        {
            if (player == null)
                return;

            string key = Key(player, questId, index);

            StayTimer timer;
            if (_stays.TryGetValue(key, out timer))
            {
                timer.Stop();
                _stays.Remove(key);
            }
        }

        private static void FinishStay(PlayerMobile player, string questId, int index)
        {
            _stays.Remove(Key(player, questId, index));
        }

        private class StayTimer : Timer, IQuestDriver
        {
            private readonly QuestManager _manager;
            private readonly PlayerMobile _player;
            private readonly string _questId;
            private readonly int _index;
            private readonly int _seconds;
            private int _elapsed;

            public StayTimer(QuestManager manager, PlayerMobile player, string questId, int index, int seconds)
                : base(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1))
            {
                _manager = manager;
                _player = player;
                _questId = questId;
                _index = index;
                _seconds = seconds;
            }

            protected override void OnTick()
            {
                if (_player == null || _player.Deleted || !_player.Alive || _player.NetState == null)
                {
                    Stop();
                    return;
                }

                _elapsed++;

                if (_elapsed < _seconds)
                    return;

                Stop();
                FinishStay(_player, _questId, _index);

                var data = _manager.GetPlayerData(_player);
                var active = data != null ? data.GetActiveQuest(_questId) : null;

                if (active == null || _index >= active.Progress.Count)
                    return;

                int needed = active.Progress[_index].Required - active.Progress[_index].Current;
                if (needed > 0)
                    _manager.UpdateProgress(_player, _questId, _index, needed);
            }

            public void Shutdown() { Stop(); }
        }

        #endregion

        #region Survive

        private static void StartSurvive(QuestManager manager, PlayerMobile player, QuestDefinition definition,
                                         int index, SurviveObjective survive)
        {
            string key = Key(player, definition.Id, index);

            if (_survives.ContainsKey(key))
                return;

            SurviveTimer timer = new SurviveTimer(manager, player, definition.Id, index, survive);
            _survives[key] = timer;
            timer.Start();
        }

        private class SurviveTimer : Timer, IQuestDriver
        {
            private readonly QuestManager _manager;
            private readonly PlayerMobile _player;
            private readonly string _questId;
            private readonly int _index;
            private readonly SurviveObjective _objective;
            private int _elapsed;

            public SurviveTimer(QuestManager manager, PlayerMobile player, string questId, int index, SurviveObjective objective)
                : base(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1))
            {
                _manager = manager;
                _player = player;
                _questId = questId;
                _index = index;
                _objective = objective;
            }

            protected override void OnTick()
            {
                if (_player == null || _player.Deleted)
                {
                    Stop();
                    return;
                }

                // Morto: nao conta. OnPlayerDied decide se a quest falha.
                if (!_player.Alive)
                    return;

                if (!string.IsNullOrWhiteSpace(_objective.RequiredRegion))
                {
                    Region region = _player.Region;

                    if (region == null)
                        return;

                    bool inside = (region.Name != null &&
                                   region.Name.Equals(_objective.RequiredRegion, StringComparison.OrdinalIgnoreCase));

                    if (!inside)
                    {
                        try { inside = region.IsPartOf(_objective.RequiredRegion); }
                        catch { inside = false; }
                    }

                    if (!inside)
                        return;
                }

                _elapsed++;

                int total = _objective.DurationSeconds > 0 ? _objective.DurationSeconds : _objective.Required;
                if (total <= 0)
                    total = 1;

                if (_elapsed < total)
                {
                    _manager.UpdateProgress(_player, _questId, _index, 1);
                    return;
                }

                var data = _manager.GetPlayerData(_player);
                var active = data != null ? data.GetActiveQuest(_questId) : null;

                if (active != null && _index < active.Progress.Count)
                {
                    int needed = active.Progress[_index].Required - active.Progress[_index].Current;
                    if (needed > 0)
                        _manager.UpdateProgress(_player, _questId, _index, needed);
                }

                Stop();
            }

            public void Shutdown()
            {
                Stop();
                _survives.Remove(Key(_player, _questId, _index));
            }
        }

        #endregion

        #region Escort

        /// <summary>
        /// Inicia (ou retoma) a escolta de um objetivo.
        ///
        /// Fluxo:
        ///   1. Resolve o destino — coordenadas do JSON ou centro da região
        ///      declarada em destinationRegion. Sem destino resolvível o
        ///      objetivo é impossível, então isto é avisado ao GM e ao jogador.
        ///   2. Reaproveita a escolta já viva (relog/restart) via registro
        ///      indexado, sem varrer World.Mobiles.
        ///   3. Cria o NPC, vincula ao jogador e manda seguir.
        ///   4. Um timer de 2s checa chegada, distância e "preso no terreno".
        /// </summary>
        private static void StartEscort(QuestManager manager, PlayerMobile player, QuestDefinition definition,
                                        ActiveQuest active, int index, EscortObjective escort)
        {
            if (player == null || player.Deleted || definition == null || active == null)
                return;

            if (!definition.IsObjectiveActive(index, active.Progress))
                return;

            string key = Key(player, definition.Id, index);

            // Já existe um driver ativo? Só religa o timer.
            EscortRun existing;
            if (_escorts.TryGetValue(key, out existing) && existing != null)
            {
                existing.Resume();
                return;
            }

            // Reaproveita a escolta viva (relog do jogador ou restart do servidor)
            QuestEscortable npc = QuestEscortable.Find(definition.Id, index, player);

            Map spawnMap = ResolveMap(escort.SpawnMap, player.Map);

            if (npc == null)
            {
                if (!escort.SpawnOnAccept)
                    return;     // o GM prefere spawnar o NPC manualmente no mundo

                Point3D spawnPoint = new Point3D(escort.SpawnX, escort.SpawnY, escort.SpawnZ);
                if (spawnPoint == Point3D.Zero)
                    spawnPoint = player.Location;

                npc = new QuestEscortable();

                if (!string.IsNullOrWhiteSpace(escort.EscortNpcName))
                    npc.Name = escort.EscortNpcName;

                if (escort.EscortBody > 0)
                {
                    npc.Body = escort.EscortBody;
                    npc.Female = escort.EscortBody == 0x191;
                }

                if (escort.EscortHue > 0)
                    npc.Hue = escort.EscortHue;

                if (escort.EscortHits > 0)
                {
                    npc.SetHits(escort.EscortHits, escort.EscortHits);
                    npc.Hits = escort.EscortHits;
                }

                npc.MoveToWorld(spawnPoint, spawnMap);
            }

            // ── Configuração do destino e do comportamento ───────────────────
            npc.QuestNpcId = escort.EscortNpcId;
            npc.QuestId = definition.Id;
            npc.ObjectiveIndex = index;
            npc.DestinationName = escort.DestinationName;
            npc.DestinationMap = ResolveMap(escort.DestinationMap, spawnMap);
            npc.RadiusToComplete = escort.RadiusToComplete > 0 ? escort.RadiusToComplete : 5;
            npc.DefendsItself = escort.DefendsItself;
            npc.TeleportWhenStuck = escort.TeleportWhenStuck;
            npc.MaxDistanceFromOwner = escort.MaxDistanceFromOwner > 0 ? escort.MaxDistanceFromOwner : 24;
            npc.StuckTimeoutSeconds = escort.StuckTimeoutSeconds > 0 ? escort.StuckTimeoutSeconds : 20;
            npc.QuestOwner = player;

            // Coordenadas explícitas têm prioridade sobre a região
            if (escort.DestinationX != 0 || escort.DestinationY != 0)
                npc.Destination = new Point3D(escort.DestinationX, escort.DestinationY, escort.DestinationZ);
            else
                npc.DestinationRegion = escort.DestinationRegion;

            // Sem destino resolvível, o objetivo nunca completa: avisa em vez de
            // deixar o jogador preso numa escolta sem fim.
            if (!npc.ResolveRegionDestination() && npc.Destination == Point3D.Zero)
            {
                Console.WriteLine("[QuestObjectiveDrivers] Escort de '" + definition.Id + "' objetivo " + index +
                                  " não tem destino válido (destinationX/Y ou destinationRegion). " +
                                  "Objetivo impossível — corrija o JSON.");

                player.SendMessage(QuestConstants.HueError,
                    "Esta escolta está mal configurada (sem destino). Avise um GM.");

                return;
            }

            npc.Register();

            // Vínculo e comando de seguir.
            // ControlMaster/Controlled são atribuídos diretamente porque
            // SetControlMaster() recusa quando Followers + ControlSlots excede
            // FollowersMax e ainda força ControlOrder = Come. Como a escolta usa
            // ControlSlots = 0, a atribuição direta é segura e não consome slot.
            npc.ControlMaster = player;
            npc.Controlled = true;
            npc.ControlTarget = player;
            npc.ControlOrder = OrderType.Follow;

            if (!npc.Deleted && npc.Map != player.Map)
                npc.MoveToWorld(player.Location, player.Map);

            npc.SayTo(player, "Vou te seguir. Precisamos chegar em " + DestinationLabelFor(npc, escort) + ".");

            EscortRun run = new EscortRun(manager, player, definition.Id, index, escort, npc);
            _escorts[key] = run;
            run.Start();
        }

        private static string DestinationLabelFor(QuestEscortable npc, EscortObjective escort)
        {
            if (npc != null && !string.IsNullOrWhiteSpace(npc.DestinationName))
                return npc.DestinationName;

            if (escort != null && !string.IsNullOrWhiteSpace(escort.DestinationRegion))
                return escort.DestinationRegion;

            return "o destino";
        }

        /// <summary>Chamado por QuestEscortable.OnDeath.</summary>
        public static void NotifyEscortDied(QuestEscortable npc)
        {
            if (npc == null || npc.QuestOwner == null || string.IsNullOrEmpty(npc.QuestId))
                return;

            string key = Key(npc.QuestOwner, npc.QuestId, npc.ObjectiveIndex);

            EscortRun run;
            if (!_escorts.TryGetValue(key, out run))
                return;

            _escorts.Remove(key);
            run.OnEscortDied();
        }

        private class EscortRun : Timer, IQuestDriver
        {
            private readonly QuestManager _manager;
            private readonly PlayerMobile _player;
            private readonly string _questId;
            private readonly int _index;
            private readonly EscortObjective _objective;
            private readonly QuestEscortable _npc;

            private DateTime _lastHint = DateTime.MinValue;
            private bool _stopped;

            public EscortRun(QuestManager manager, PlayerMobile player, string questId, int index,
                             EscortObjective objective, QuestEscortable npc)
                : base(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2))
            {
                _manager = manager;
                _player = player;
                _questId = questId;
                _index = index;
                _objective = objective;
                _npc = npc;
            }

            public QuestEscortable Npc { get { return _npc; } }

            public void Resume()
            {
                if (!_stopped && !Running)
                    Start();
            }

            protected override void OnTick()
            {
                if (_npc == null || _npc.Deleted || _player == null || _player.Deleted)
                {
                    Stop();
                    return;
                }

                // Jogador morto ou offline: a escolta espera, não falha.
                // Falhar por morte do jogador é decisão de SurviveObjective.
                if (!_player.Alive || _player.NetState == null)
                    return;

                if (!_npc.Alive)
                    return;     // OnDeath já aplicou a política de onEscortDead

                if (_npc.IsAtDestination())
                {
                    CompleteEscort();
                    return;
                }

                // Dica periódica de distância para o jogador
                if ((DateTime.UtcNow - _lastHint).TotalSeconds >= 45)
                {
                    _lastHint = DateTime.UtcNow;

                    int distance = _npc.DistanceToDestination();

                    if (distance >= 0)
                    {
                        _player.SendMessage(QuestConstants.HueProgress,
                            "Escolta: faltam cerca de {0} passos até {1}.",
                            distance, DestinationLabelFor(_npc, _objective));
                    }
                }
            }

            private void CompleteEscort()
            {
                _npc.Arrived = true;
                Stop();

                _escorts.Remove(Key(_player, _questId, _index));

                var data = _manager.GetPlayerData(_player);
                var active = data != null ? data.GetActiveQuest(_questId) : null;

                int needed = 1;
                if (active != null && _index < active.Progress.Count)
                    needed = active.Progress[_index].Required - active.Progress[_index].Current;

                if (needed > 0)
                    _manager.UpdateProgress(_player, _questId, _index, needed);

                _npc.Say("Chegamos! Obrigado pela escolta.");
                _npc.PlaySound(0x1FE);

                // Solta o vínculo antes de remover, para não contar como pet perdido
                _npc.Controlled = false;
                _npc.ControlMaster = null;

                Timer.DelayCall(TimeSpan.FromSeconds(5), delegate
                {
                    if (_npc != null && !_npc.Deleted)
                        _npc.Delete();
                });
            }

            /// <summary>
            /// Aplica a política declarada em "onEscortDead":
            /// Nothing | ResetObjective | Respawn | FailQuest (padrão).
            /// </summary>
            public void OnEscortDied()
            {
                Stop();

                string action = string.IsNullOrWhiteSpace(_objective.OnEscortDead)
                    ? "FailQuest" : _objective.OnEscortDead;

                if (action.Equals("Nothing", StringComparison.OrdinalIgnoreCase) ||
                    action.Equals("Ignore", StringComparison.OrdinalIgnoreCase))
                    return;

                if (_player == null || _player.Deleted)
                    return;

                if (action.Equals("ResetObjective", StringComparison.OrdinalIgnoreCase))
                {
                    var data = _manager.GetPlayerData(_player);
                    var active = data != null ? data.GetActiveQuest(_questId) : null;

                    if (active != null && _index < active.Progress.Count)
                    {
                        active.Progress[_index].Current = 0;
                        active.Progress[_index].IsComplete = false;
                    }

                    _player.SendMessage(QuestConstants.HueWarning,
                        "Sua escolta morreu. O objetivo reiniciou.");

                    var definition0 = QuestRepository.Instance.GetQuest(_questId);
                    if (definition0 != null && active != null)
                    {
                        Timer.DelayCall(TimeSpan.FromSeconds(3), delegate
                        {
                            if (_player != null && !_player.Deleted)
                                StartEscort(_manager, _player, definition0, active, _index, _objective);
                        });
                    }

                    return;
                }

                if (action.Equals("Respawn", StringComparison.OrdinalIgnoreCase))
                {
                    var definition = QuestRepository.Instance.GetQuest(_questId);
                    var data = _manager.GetPlayerData(_player);
                    var active = data != null ? data.GetActiveQuest(_questId) : null;

                    if (definition != null && active != null)
                    {
                        Timer.DelayCall(TimeSpan.FromSeconds(3), delegate
                        {
                            if (_player != null && !_player.Deleted)
                                StartEscort(_manager, _player, definition, active, _index, _objective);
                        });
                    }

                    _player.SendMessage(QuestConstants.HueWarning,
                        "Sua escolta morreu. Outro NPC sairá em instantes.");
                    return;
                }

                string message;
                _manager.FailQuest(_player, _questId, QuestFailReason.EscortDied, out message);
            }

            public void Shutdown()
            {
                // NAO remove o NPC e nao desfaz o vinculo: se o jogador
                // deslogar, a escolta continua no mundo e o driver e refeito no
                // login (ResumeForPlayer -> StartEscort -> QuestEscortable.Find).
                _stopped = true;
                Stop();
            }
        }

        #endregion


        #region Defend

        private static void StartDefend(QuestManager manager, PlayerMobile player, QuestDefinition definition,
                                        ActiveQuest active, int index, DefendObjective defend)
        {
            if (!definition.IsObjectiveActive(index, active.Progress))
                return;

            string key = Key(player, definition.Id, index);
            if (_defends.ContainsKey(key))
                return;

            // ResolveAnyType (nao ResolveType): ResolveType descarta tudo que nao
            // for Item, entao devolvia null para QUALQUER criatura e o driver de
            // defesa nunca era iniciado — o objetivo ficava parado para sempre.
            Type enemyType = QuestItemMatcher.ResolveAnyType(defend.EnemyType);

            if (string.IsNullOrWhiteSpace(defend.EnemyType) || enemyType == null ||
                !typeof(BaseCreature).IsAssignableFrom(enemyType))
            {
                Console.WriteLine("[QuestObjectiveDrivers] Defend de '" + definition.Id + "' objetivo " + index +
                                  ": enemyType '" + defend.EnemyType + "' nao e uma BaseCreature valida. Driver nao iniciado.");

                if (player != null && !player.Deleted)
                    player.SendMessage(QuestConstants.HueError,
                        "Este objetivo de defesa esta mal configurado. Avise um GM (enemyType invalido).");

                return;
            }

            DefendRun run = new DefendRun(manager, player, definition.Id, index, defend, enemyType, active);
            _defends[key] = run;
            run.Start();
        }

        /// <summary>Chamado pelo QuestManager a cada morte, para contar as ondas.</summary>
        public static void NotifyDefendKill(QuestManager manager, PlayerMobile player, ActiveQuest active,
                                            QuestDefinition definition, int index, DefendObjective defend,
                                            Mobile killed, string killedType)
        {
            if (player == null || definition == null)
                return;

            DefendRun run;
            if (!_defends.TryGetValue(Key(player, definition.Id, index), out run))
                return;

            run.RegisterKill(killed, killedType);
        }

        private class DefendRun : Timer, IQuestDriver
        {
            private readonly QuestManager _manager;
            private readonly PlayerMobile _player;
            private readonly string _questId;
            private readonly int _index;
            private readonly DefendObjective _objective;
            private readonly Type _enemyType;

            private int _wavesSpawned;
            private readonly List<BaseCreature> _alive = new List<BaseCreature>();

            public DefendRun(QuestManager manager, PlayerMobile player, string questId, int index,
                             DefendObjective objective, Type enemyType, ActiveQuest active)
                : base(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3))
            {
                _manager = manager;
                _player = player;
                _questId = questId;
                _index = index;
                _objective = objective;
                _enemyType = enemyType;

                // Retoma de onde parou depois de restart: as mortes ja feitas
                // estao no progresso persistido do objetivo.
                if (active != null && index < active.Progress.Count && EnemiesPerWave > 0)
                    _wavesSpawned = active.Progress[index].Current / EnemiesPerWave;
            }

            public int TotalWaves { get { return _objective.TotalWaves > 0 ? _objective.TotalWaves : 1; } }
            public int EnemiesPerWave { get { return _objective.EnemiesPerWave > 0 ? _objective.EnemiesPerWave : 1; } }

            protected override void OnTick()
            {
                if (_player == null || _player.Deleted)
                {
                    Stop();
                    return;
                }

                _alive.RemoveAll(m => m == null || m.Deleted || !m.Alive);

                // So spawna a proxima onda quando a anterior foi limpa
                if (_alive.Count > 0)
                    return;

                if (_wavesSpawned >= TotalWaves)
                {
                    Stop();
                    return;
                }

                SpawnWave();
            }

            private void SpawnWave()
            {
                Map map = ResolveMap(_objective.MapName, _player.Map);

                Point3D center = new Point3D(_objective.LocationX, _objective.LocationY, 0);
                if (center.X == 0 && center.Y == 0)
                    center = _player.Location;

                _wavesSpawned++;

                for (int i = 0; i < EnemiesPerWave; i++)
                {
                    try
                    {
                        BaseCreature creature = Activator.CreateInstance(_enemyType) as BaseCreature;
                        if (creature == null)
                            continue;

                        int dx = Utility.Random(-4, 9);
                        int dy = Utility.Random(-4, 9);

                        creature.MoveToWorld(new Point3D(center.X + dx, center.Y + dy, center.Z), map);
                        _alive.Add(creature);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("[QuestObjectiveDrivers] Falha ao spawnar onda de defesa: " + ex.Message);
                    }
                }

                if (!_player.Deleted)
                    _player.SendMessage(QuestConstants.HueWarning,
                        "Onda {0} de {1} chegou! Elimine {2} inimigo(s).", _wavesSpawned, TotalWaves, EnemiesPerWave);
            }

            public void RegisterKill(Mobile killed, string killedType)
            {
                if (killed == null || _player == null || _player.Deleted)
                    return;

                _alive.RemoveAll(m => m == null || m.Deleted || m == killed);

                bool matches = killedType != null && killedType.Equals(_enemyType.Name, StringComparison.OrdinalIgnoreCase);

                if (!matches)
                    matches = _enemyType.IsInstanceOfType(killed);

                if (!matches)
                    return;

                // O progresso persistido e a fonte de verdade da conclusao
                _manager.UpdateProgress(_player, _questId, _index, 1);
            }

            public void Shutdown()
            {
                Stop();
                _defends.Remove(Key(_player, _questId, _index));
            }
        }

        #endregion

        #region Map resolution

        /// <summary>Converte o nome do mapa escrito no JSON para o objeto Map.</summary>
        public static Map ResolveMap(string mapName, Map fallback)
        {
            if (string.IsNullOrWhiteSpace(mapName))
                return fallback ?? Map.Trammel;

            switch (mapName.Trim().ToLowerInvariant())
            {
                case "felucca": case "fel": return Map.Felucca;
                case "trammel": case "tram": return Map.Trammel;
                case "ilshenar": case "ils": return Map.Ilshenar;
                case "malas": case "mal": return Map.Malas;
                case "tokuno": case "tok": return Map.Tokuno;
                case "termur": case "ter": return Map.TerMur;
                default: return fallback ?? Map.Trammel;
            }
        }

        #endregion
    }

    /// <summary>Contrato interno dos drivers gerenciaveis.</summary>
    public interface IQuestDriver
    {
        void Shutdown();
    }
}
