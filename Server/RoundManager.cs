using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Hkmp.Api.Server;
using Hkmp.Game;
using HKMP.Rounds.Server.Stats;
using HKMP.Rounds.Settings;
using HKMP.Rounds.Networking.Packets;

namespace HKMP.Rounds.Server
{
    internal sealed class RoundManager
    {
        private readonly IServerApi _serverApi;
        private readonly StatsManager _statsManager;
        private readonly GlobalSettingsStore _settings;

        private RoundServerNetManager _network;
        private Func<ushort, int> _phantomMapIdResolver;

        private RoundsTimerIntegration _timerIntegration;
        private readonly PvpModeServerSettings _pvpModeServerSettings;
        private readonly object _syncRoot = new object();
        private readonly RoundContext _globalContext = new RoundContext(string.Empty);
        private readonly Dictionary<string, RoundContext> _sceneContexts =
            new Dictionary<string, RoundContext>(StringComparer.OrdinalIgnoreCase);
        private RoundContext _activeContext;
        private uint _roundIdSequence;

        private RoundContext CurrentContext
        {
            get
            {
                return _settings.IndependentRoundsPerRoom
                    ? _activeContext ?? _globalContext
                    : _globalContext;
            }
        }

        private OneShotManaManager _oneShotMana { get { return CurrentContext.OneShotMana; } }
        private HashSet<ushort> _participants { get { return CurrentContext.Participants; } }
        private HashSet<ushort> _alivePlayers { get { return CurrentContext.AlivePlayers; } }
        private HashSet<ushort> _preparedPlayers { get { return CurrentContext.PreparedPlayers; } }
        private HashSet<ushort> _readyPlayers { get { return CurrentContext.ReadyPlayers; } }
        private Dictionary<string, PostRoundReadyGate> _postRoundReadyGates { get { return CurrentContext.PostRoundReadyGates; } }
        private Dictionary<ushort, string> _roundParticipantNames { get { return CurrentContext.RoundParticipantNames; } }
        private List<ushort> _readyOrder { get { return CurrentContext.ReadyOrder; } }
        private Dictionary<string, int> _matchPlayerWins { get { return CurrentContext.MatchPlayerWins; } }
        private Dictionary<byte, int> _matchTeamWins { get { return CurrentContext.MatchTeamWins; } }
        private Dictionary<ushort, PlayerHealthState> _playerHealth { get { return CurrentContext.PlayerHealth; } }
        private Dictionary<ushort, byte> _participantTeams { get { return CurrentContext.ParticipantTeams; } }
        private Dictionary<byte, TeamSnapshot> _teamSnapshots { get { return CurrentContext.TeamSnapshots; } }
        private bool _roundActive { get { return CurrentContext.RoundActive; } set { CurrentContext.RoundActive = value; } }
        private bool _waitingForPreparation { get { return CurrentContext.WaitingForPreparation; } set { CurrentContext.WaitingForPreparation = value; } }
        private uint _currentRoundId { get { return CurrentContext.CurrentRoundId; } set { CurrentContext.CurrentRoundId = value; } }
        private int _winTarget { get { return CurrentContext.WinTarget; } set { CurrentContext.WinTarget = value; } }
        private bool _winTargetArmed { get { return CurrentContext.WinTargetArmed; } set { CurrentContext.WinTargetArmed = value; } }

        private sealed class RoundContext
        {
            public readonly string SceneName;
            public readonly OneShotManaManager OneShotMana = new OneShotManaManager();
            public readonly HashSet<ushort> Participants = new HashSet<ushort>();
            public readonly HashSet<ushort> AlivePlayers = new HashSet<ushort>();
            public readonly HashSet<ushort> PreparedPlayers = new HashSet<ushort>();
            public readonly HashSet<ushort> ReadyPlayers = new HashSet<ushort>();
            public readonly Dictionary<string, PostRoundReadyGate> PostRoundReadyGates =
                new Dictionary<string, PostRoundReadyGate>(StringComparer.OrdinalIgnoreCase);
            public readonly Dictionary<ushort, string> RoundParticipantNames = new Dictionary<ushort, string>();
            public readonly Dictionary<ushort, string> ParticipantScenes = new Dictionary<ushort, string>();
            public readonly List<ushort> ReadyOrder = new List<ushort>();
            public readonly Dictionary<string, int> MatchPlayerWins =
                new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            public readonly Dictionary<byte, int> MatchTeamWins = new Dictionary<byte, int>();
            public readonly Dictionary<ushort, PlayerHealthState> PlayerHealth = new Dictionary<ushort, PlayerHealthState>();
            public readonly Dictionary<ushort, byte> ParticipantTeams = new Dictionary<ushort, byte>();
            public readonly Dictionary<byte, TeamSnapshot> TeamSnapshots = new Dictionary<byte, TeamSnapshot>();
            public bool RoundActive;
            public bool WaitingForPreparation;
            public uint CurrentRoundId;
            public int WinTarget;
            public bool WinTargetArmed;

            public RoundContext(string sceneName)
            {
                SceneName = sceneName ?? string.Empty;
            }
        }

        private sealed class ContextScope : IDisposable
        {
            private RoundManager _owner;
            private readonly RoundContext _previous;

            public ContextScope(RoundManager owner, RoundContext next)
            {
                Monitor.Enter(owner._syncRoot);
                _owner = owner;
                _previous = owner._activeContext;
                owner._activeContext = next;
            }

            public ContextScope(RoundManager owner, Func<RoundContext> resolveContext)
            {
                Monitor.Enter(owner._syncRoot);
                try
                {
                    _owner = owner;
                    _previous = owner._activeContext;
                    owner._activeContext = resolveContext();
                }
                catch
                {
                    Monitor.Exit(owner._syncRoot);
                    throw;
                }
            }

            public void Dispose()
            {
                RoundManager owner = _owner;
                if (owner == null)
                {
                    return;
                }

                try
                {
                    bool previousContextStillExists =
                        _previous == owner._globalContext ||
                        owner._sceneContexts.Values.Contains(_previous);
                    owner._activeContext = previousContextStillExists
                        ? _previous
                        : owner._globalContext;
                }
                finally
                {
                    _owner = null;
                    Monitor.Exit(owner._syncRoot);
                }
            }
        }

        private sealed class PlayerHealthState
        {
            public int Health;
            public int MaxHealth;
            public int BlueHealth;
            public int Soul;
        }

        private sealed class TeamSnapshot
        {
            public byte Id;
            public string Name;
            public HashSet<ushort> Members =
                new HashSet<ushort>();
            public Dictionary<ushort, string> MemberNames =
                new Dictionary<ushort, string>();
        }

        private sealed class PostRoundReadyGate
        {
            public bool ReturnPendingObserved;
            public bool BenchObserved;
            public bool AliveAtBenchObserved;
        }

        private enum RoundEndKind
        {
            None,
            NoWinner,
            Player,
            Team
        }

        public bool IsRoundActive
        {
            get
            {
                lock (_syncRoot)
                {
                    return _roundActive;
                }
            }
        }

        public bool IsWaitingForPreparation
        {
            get
            {
                lock (_syncRoot)
                {
                    return _waitingForPreparation;
                }
            }
        }

        public uint CurrentRoundId
        {
            get
            {
                lock (_syncRoot)
                {
                    return _currentRoundId;
                }
            }
        }

        public int ReadyPlayerCount
        {
            get
            {
                lock (_syncRoot)
                {
                    return _readyPlayers.Count;
                }
            }
        }

        public int ConnectedPlayerCount
        {
            get
            {
                lock (_syncRoot)
                {
                    return GetPlayersInCurrentContext().Count;
                }
            }
        }

        public bool IndependentRoundsPerRoom
        {
            get
            {
                lock (_syncRoot)
                {
                    return _settings.IndependentRoundsPerRoom;
                }
            }
        }

        public string CurrentContextSceneName
        {
            get
            {
                lock (_syncRoot)
                {
                    return CurrentContext.SceneName ?? string.Empty;
                }
            }
        }

        public bool HasAnyRoundInProgress
        {
            get
            {
                lock (_syncRoot)
                {
                    return GetAllContexts().Any(context => context.RoundActive || context.WaitingForPreparation);
                }
            }
        }

        public int EffectiveReadyRequiredCount
        {
            get
            {
                lock (_syncRoot)
                {
                    if (_settings.ReadyRequiredCount == 0)
                    {
                        return GetPlayersInCurrentContext().Count;
                    }

                    return Math.Max(2, _settings.ReadyRequiredCount);
                }
            }
        }

        public bool IsReady(ushort playerId)
        {
            using (EnterPlayerContext(playerId))
            {
                return _readyPlayers.Contains(playerId);
            }
        }

        public bool AutoStartEnabled
        {
            get
            {
                lock (_syncRoot)
                {
                    return _settings.AutoStart;
                }
            }
        }

        public int WinTarget
        {
            get
            {
                lock (_syncRoot)
                {
                    return _winTargetArmed ? _winTarget : 0;
                }
            }
        }

        public PvpMode CurrentPvpMode
        {
            get
            {
                lock (_syncRoot)
                {
                    return _settings.PvpMode;
                }
            }
        }

        public int RoundSoul
        {
            get
            {
                return _settings.RoundSoul;
            }
        }

        public bool DisableEnemies
        {
            get
            {
                return _settings.DisableEnemies;
            }
        }

        public bool DebugDisableShade
        {
            get
            {
                return _settings.DebugDisableShade;
            }
        }

        public RoundManager(
            IServerApi serverApi,
            StatsManager statsManager,
            GlobalSettingsStore settings)
        {
            _serverApi = serverApi;
            _statsManager = statsManager;
            _settings = settings;
            _activeContext = _globalContext;
            _pvpModeServerSettings = new PvpModeServerSettings(serverApi);

            string modeMessage;
            if (!_pvpModeServerSettings.Configure(
                    _settings.PvpMode,
                    false,
                    _settings.DisablePvpBeforeRound,
                    out modeMessage))
            {
                Modding.Logger.LogError(
                    "[HKMP.Rounds] Could not initialize PvP mode: " +
                    modeMessage);
            }
        }

        public IDisposable EnterPlayerContext(ushort playerId)
        {
            return new ContextScope(this, () => ResolveContextForPlayer(playerId));
        }

        public IDisposable EnterRoundContext(uint roundId)
        {
            return new ContextScope(this, () => FindContextByRoundId(roundId) ?? _globalContext);
        }

        public string GetLockedRoundScenesJson()
        {
            lock (_syncRoot)
            {
                List<string> sceneNames = new List<string>();
                foreach (RoundContext context in GetAllContexts())
                {
                    if (context == null || (!context.RoundActive && !context.WaitingForPreparation))
                    {
                        continue;
                    }

                    if (_settings.IndependentRoundsPerRoom && !string.IsNullOrWhiteSpace(context.SceneName))
                    {
                        AddSceneName(sceneNames, context.SceneName);
                        continue;
                    }

                    foreach (ushort participantId in context.Participants)
                    {
                        string sceneName;
                        if (context.ParticipantScenes.TryGetValue(participantId, out sceneName) &&
                            !IsPhantomMantisScene(sceneName))
                        {
                            AddSceneName(sceneNames, sceneName);
                        }

                        IServerPlayer player;
                        if (_serverApi.ServerManager.TryGetPlayer(participantId, out player) && player != null)
                        {
                            AddSceneName(sceneNames, GetEffectiveRoomKey(player));
                        }
                    }
                }

                sceneNames.Sort(StringComparer.OrdinalIgnoreCase);
                try
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(sceneNames);
                }
                catch
                {
                    return "[]";
                }
            }
        }

        private static bool IsPhantomMantisScene(string sceneName)
        {
            return string.Equals(sceneName, "Fungus2_15", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(sceneName, "Fungus2_31", StringComparison.OrdinalIgnoreCase);
        }

        private static void AddSceneName(List<string> sceneNames, string sceneName)
        {
            string normalized = (sceneName ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(normalized) &&
                !sceneNames.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                sceneNames.Add(normalized);
            }
        }

        public bool SetIndependentRoundsPerRoom(bool enabled, out string message)
        {
            lock (_syncRoot)
            {
                message = string.Empty;
                if (HasAnyRoundInProgress || GetAllContexts().Any(context => context.PostRoundReadyGates.Count > 0))
                {
                    message = "Separate rounds per room can only be changed when no room has an active or preparing round and all post-round bench returns are complete.";
                    return false;
                }

                if (_settings.IndependentRoundsPerRoom == enabled)
                {
                    return true;
                }

                int preservedWinTarget = CurrentContext.WinTarget;
                bool preservedWinTargetArmed = CurrentContext.WinTargetArmed;

                foreach (RoundContext context in GetAllContexts())
                {
                    ClearContextState(context);
                }

                _sceneContexts.Clear();
                _globalContext.WinTarget = preservedWinTarget;
                _globalContext.WinTargetArmed = preservedWinTargetArmed;
                _settings.SetIndependentRoundsPerRoom(enabled);
                _activeContext = _globalContext;
                message = enabled
                    ? "Separate rounds per room enabled. Readiness and round state are now isolated by scene."
                    : "Separate rounds per room disabled. Readiness and round state are shared again.";
                return true;
            }
        }

        public void OnPlayerEnterScene(IServerPlayer player)
        {
            lock (_syncRoot)
            {
                if (player == null || !_settings.IndependentRoundsPerRoom)
                {
                    return;
                }

                RoundContext destination = ResolveContextForPlayer(player.Id);
                List<RoundContext> previousContexts = _sceneContexts.Values
                    .Where(context => context != destination &&
                        (context.ReadyPlayers.Contains(player.Id) ||
                         context.Participants.Contains(player.Id) ||
                         context.PreparedPlayers.Contains(player.Id)))
                    .ToList();

                try
                {
                    for (int i = 0; i < previousContexts.Count; i++)
                    {
                        RoundContext previous = previousContexts[i];
                        _activeContext = previous;

                        if (previous.RoundActive && previous.AlivePlayers.Contains(player.Id))
                        {
                            OnPlayerDeath(player.Id);
                        }
                        else if (previous.WaitingForPreparation && previous.Participants.Contains(player.Id))
                        {
                            previous.Participants.Remove(player.Id);
                            previous.PreparedPlayers.Remove(player.Id);
                            previous.ParticipantTeams.Remove(player.Id);
                            previous.RoundParticipantNames.Remove(player.Id);
                            previous.ParticipantScenes.Remove(player.Id);
                            if (previous.Participants.Count < 2)
                            {
                                AbortPreparation();
                            }
                            else
                            {
                                TryActivateRound();
                            }
                        }
                        else
                        {
                            previous.ReadyPlayers.Remove(player.Id);
                            previous.ReadyOrder.Remove(player.Id);
                        }

                        _network?.BroadcastRoundSettingsState(string.Empty);
                    }
                }
                finally
                {
                    _activeContext = destination;
                }
                _network?.BroadcastRoundSettingsState(string.Empty);
                _network?.SendRoundSettingsState(player.Id, string.Empty);
            }
        }

        private RoundContext ResolveContextForPlayer(ushort playerId)
        {
            if (!_settings.IndependentRoundsPerRoom)
            {
                return _globalContext;
            }

            IServerPlayer player;
            if (_serverApi.ServerManager.TryGetPlayer(playerId, out player) && player != null)
            {
                return ResolveContextForScene(GetEffectiveRoomKey(player));
            }

            return FindContextContainingPlayer(playerId) ?? _globalContext;
        }

        public bool IsRoundRoomLocked(string roomKey)
        {
            lock (_syncRoot)
            {
                if (!_settings.IndependentRoundsPerRoom)
                {
                    return _globalContext.RoundActive || _globalContext.WaitingForPreparation;
                }

                string normalized = (roomKey ?? string.Empty).Trim();
                if (normalized.Length == 0)
                {
                    return false;
                }

                RoundContext context;
                return _sceneContexts.TryGetValue(normalized, out context) && context != null &&
                       (context.RoundActive || context.WaitingForPreparation);
            }
        }

        private RoundContext ResolveContextForScene(string sceneName)
        {
            string normalized = (sceneName ?? string.Empty).Trim();
            if (!_settings.IndependentRoundsPerRoom || string.IsNullOrWhiteSpace(normalized))
            {
                return _globalContext;
            }

            RoundContext context;
            if (!_sceneContexts.TryGetValue(normalized, out context))
            {
                context = new RoundContext(normalized);
                context.WinTarget = _globalContext.WinTarget;
                context.WinTargetArmed = _globalContext.WinTargetArmed;
                _sceneContexts.Add(normalized, context);
            }

            return context;
        }

        private RoundContext FindContextContainingPlayer(ushort playerId)
        {
            foreach (RoundContext context in GetAllContexts())
            {
                if (context.Participants.Contains(playerId) ||
                    context.ReadyPlayers.Contains(playerId) ||
                    context.PreparedPlayers.Contains(playerId) ||
                    context.AlivePlayers.Contains(playerId))
                {
                    return context;
                }
            }

            return null;
        }

        private RoundContext FindContextByRoundId(uint roundId)
        {
            if (roundId == 0)
            {
                return null;
            }

            foreach (RoundContext context in GetAllContexts())
            {
                if (context.CurrentRoundId == roundId &&
                    (context.RoundActive || context.WaitingForPreparation || context.Participants.Count > 0))
                {
                    return context;
                }
            }

            return null;
        }

        private IEnumerable<RoundContext> GetAllContexts()
        {
            yield return _globalContext;
            foreach (RoundContext context in _sceneContexts.Values)
            {
                yield return context;
            }
        }

        private List<IServerPlayer> GetPlayersInCurrentContext()
        {
            IReadOnlyCollection<IServerPlayer> players = _serverApi.ServerManager.Players;
            if (players == null)
            {
                return new List<IServerPlayer>();
            }

            if (!_settings.IndependentRoundsPerRoom)
            {
                return players.Where(player => player != null).ToList();
            }

            if (string.IsNullOrWhiteSpace(CurrentContext.SceneName))
            {
                return new List<IServerPlayer>();
            }

            return players.Where(player => player != null &&
                string.Equals(
                    GetEffectiveRoomKey(player),
                    CurrentContext.SceneName,
                    StringComparison.OrdinalIgnoreCase)).ToList();
        }

        private bool IsPlayerInCurrentContext(IServerPlayer player)
        {
            return player != null &&
                   (!_settings.IndependentRoundsPerRoom ||
                    (!string.IsNullOrWhiteSpace(CurrentContext.SceneName) &&
                     string.Equals(
                         GetEffectiveRoomKey(player),
                         CurrentContext.SceneName,
                         StringComparison.OrdinalIgnoreCase)));
        }

        private bool IsConnectedPlayer(ushort playerId)
        {
            IServerPlayer player;
            return _serverApi.ServerManager.TryGetPlayer(playerId, out player) &&
                   IsPlayerInCurrentContext(player);
        }

        private const int MaximumRoundChatMessageLength = 255;

        private void BroadcastRoundMessage(string message)
        {
            List<string> messages = SplitRoundChatMessage(message);
            if (!_settings.IndependentRoundsPerRoom)
            {
                foreach (string line in messages)
                {
                    _serverApi.ServerManager.BroadcastMessage(line);
                }
                return;
            }

            if (string.IsNullOrWhiteSpace(CurrentContext.SceneName))
            {
                return;
            }

            foreach (IServerPlayer player in GetPlayersInCurrentContext())
            {
                foreach (string line in messages)
                {
                    _serverApi.ServerManager.SendMessage(player.Id, line);
                }
            }
        }

        private static List<string> SplitRoundChatMessage(string message)
        {
            List<string> result = new List<string>();
            if (string.IsNullOrEmpty(message))
            {
                return result;
            }

            string[] lines = message.Replace("\r", string.Empty).Split('\n');
            foreach (string sourceLine in lines)
            {
                string remaining = sourceLine ?? string.Empty;
                if (remaining.Length == 0)
                {
                    continue;
                }

                while (remaining.Length > MaximumRoundChatMessageLength)
                {
                    int splitAt = remaining.LastIndexOf(' ', MaximumRoundChatMessageLength - 1, MaximumRoundChatMessageLength);
                    if (splitAt <= 0)
                    {
                        splitAt = MaximumRoundChatMessageLength;
                    }

                    result.Add(remaining.Substring(0, splitAt).TrimEnd());
                    remaining = remaining.Substring(splitAt).TrimStart();
                }

                if (remaining.Length > 0)
                {
                    result.Add(remaining);
                }
            }

            return result;
        }

        private bool SetPvpRoundActiveForCurrentContext(bool currentRoomActive, out string message)
        {
            bool anyOtherRoomActive = GetAllContexts().Any(context =>
                context != CurrentContext && (context.RoundActive || context.WaitingForPreparation));
            return _pvpModeServerSettings.SetRoundActive(
                currentRoomActive || anyOtherRoomActive,
                out message);
        }

        private void ClearContextState(RoundContext context)
        {
            if (context == null)
            {
                return;
            }

            context.OneShotMana.Clear();
            context.Participants.Clear();
            context.AlivePlayers.Clear();
            context.PreparedPlayers.Clear();
            context.ReadyPlayers.Clear();
            context.PostRoundReadyGates.Clear();
            context.RoundParticipantNames.Clear();
            context.ParticipantScenes.Clear();
            context.ReadyOrder.Clear();
            context.MatchPlayerWins.Clear();
            context.MatchTeamWins.Clear();
            context.PlayerHealth.Clear();
            context.ParticipantTeams.Clear();
            context.TeamSnapshots.Clear();
            context.RoundActive = false;
            context.WaitingForPreparation = false;
            context.CurrentRoundId = 0;
            context.WinTarget = 0;
            context.WinTargetArmed = false;
        }

        public void SetNetwork(
            RoundServerNetManager network)
        {
            _network = network;
        }

        public void SetPhantomMapIdResolver(Func<ushort, int> resolver)
        {
            _phantomMapIdResolver = resolver;
        }

        public string GetEffectiveRoomKey(IServerPlayer player)
        {
            if (player == null)
            {
                return string.Empty;
            }

            string scene = (player.CurrentScene ?? string.Empty).Trim();
            if (_phantomMapIdResolver != null &&
                (string.Equals(scene, "Fungus2_15", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(scene, "Fungus2_31", StringComparison.OrdinalIgnoreCase)))
            {
                int phantomMapId = _phantomMapIdResolver(player.Id);
                if (phantomMapId > 0)
                {
                    return "Hkmp.Rounds_Mantis_" + phantomMapId.ToString(CultureInfo.InvariantCulture);
                }
            }

            return scene;
        }

        public void SetTimerIntegration(
            RoundsTimerIntegration timerIntegration)
        {
            _timerIntegration =
                timerIntegration;
        }

        public bool SetPvpMode(PvpMode mode, out string message)
        {
            lock (_syncRoot)
            {
                message = string.Empty;
                if (mode != PvpMode.Quick && mode != PvpMode.Long && mode != PvpMode.OneShot)
                {
                    message = "Invalid PvP mode.";
                    return false;
                }

                if (HasAnyRoundInProgress)
                {
                    message = "PvP mode can only be changed when no room has a round active or preparing.";
                    return false;
                }

                if (mode == _settings.PvpMode)
                {
                    message = "PvP mode is already " + GetModeDisplayName(mode) + ".";
                    return true;
                }

                if (!_pvpModeServerSettings.SetMode(mode, out message))
                {
                    return false;
                }

                if (!_settings.SetPvpMode(mode))
                {
                    string restoreMessage;
                    _pvpModeServerSettings.SetMode(_settings.PvpMode, out restoreMessage);
                    message = "Could not persist the selected PvP mode.";
                    if (!string.IsNullOrWhiteSpace(restoreMessage))
                    {
                        message += " " + restoreMessage;
                    }
                    return false;
                }

                _oneShotMana.Clear();
                message = "PvP mode changed to " + GetModeDisplayName(mode) + ".";
                return true;
            }
        }

        public bool SetDisablePvpBeforeRound(
            bool enabled,
            out string message)
        {
            lock (_syncRoot)
            {
                message = string.Empty;

                if (HasAnyRoundInProgress)
                {
                    message = "PvP protection can only be changed when no room has a round active or preparing.";
                    return false;
                }

                if (_settings.DisablePvpBeforeRound == enabled)
                {
                    return true;
                }

                if (!_pvpModeServerSettings.SetDisablePvpBeforeRound(
                        enabled,
                        out message))
                {
                    return false;
                }

                _settings.SetDisablePvpBeforeRound(enabled);
                message = enabled
                    ? "PvP damage is disabled before rounds."
                    : "PvP damage is enabled between rounds.";
                return true;
            }
        }

        public bool HandleManaSpent(ushort playerId, ManaSpentPacket packet)
        {
            lock (_syncRoot)
            {
                if (packet == null)
                {
                    return false;
                }

                return HandleManaSpentInCurrentContext(playerId, packet);
            }
        }

        private bool HandleManaSpentInCurrentContext(ushort playerId, ManaSpentPacket packet)
        {
            if (packet == null ||
                !_roundActive ||
                _settings.PvpMode != PvpMode.OneShot ||
                packet.RoundId != _currentRoundId ||
                !_participants.Contains(playerId) ||
                !_alivePlayers.Contains(playerId) ||
                _oneShotMana.IsDead(playerId) ||
                packet.Amount <= 0)
            {
                return false;
            }

            int mana = _oneShotMana.Spend(playerId, packet.Amount);
            _network?.SendManaUpdate(playerId, _currentRoundId, mana);
            return true;
        }

        private static string GetModeDisplayName(PvpMode mode)
        {
            switch (mode)
            {
                case PvpMode.Long:
                    return "Long";
                case PvpMode.OneShot:
                    return "OneShot";
                default:
                    return "Quick";
            }
        }

        public bool IsAliveInCurrentContext(ushort playerId)
        {
            return _roundActive && _alivePlayers.Contains(playerId);
        }

        public void OnPlayerDeathInCurrentContext(ushort playerId, ushort? killerId)
        {
            OnPlayerDeathInCurrentContextInternal(playerId, killerId);
        }

        public void OnPlayerConnect(
            IServerPlayer player)
        {
            lock (_syncRoot)
            {
                if (player == null)
                {
                    return;
                }

                _statsManager.RegisterPlayer(player.Username);
            }
        }

        public void OnPlayerDisconnect(
            ushort playerId,
            string username = null)
        {
            lock (_syncRoot)
            {
                bool removedPostRoundGate = false;
                if (!string.IsNullOrWhiteSpace(username))
                {
                    string normalizedUsername = username.Trim();
                    foreach (RoundContext knownContext in GetAllContexts())
                    {
                        removedPostRoundGate |= knownContext.PostRoundReadyGates.Remove(normalizedUsername);
                    }
                }

                RoundContext context = FindContextContainingPlayer(playerId) ?? ResolveContextForPlayer(playerId);
                using (new ContextScope(this, context))
                {
                    OnPlayerDisconnectInCurrentContext(playerId);
                }

                if (removedPostRoundGate)
                {
                    _network?.BroadcastRoundSettingsState(string.Empty);
                }
            }
        }

        private void OnPlayerDisconnectInCurrentContext(ushort playerId)
        {
            _readyPlayers.Remove(playerId);
            _readyOrder.Remove(playerId);

            if (_waitingForPreparation)
            {
                if (!_participants.Remove(playerId))
                {
                    return;
                }

                _preparedPlayers.Remove(playerId);
                _participantTeams.Remove(playerId);
                _roundParticipantNames.Remove(playerId);
                CurrentContext.ParticipantScenes.Remove(playerId);

                if (_participants.Count < 2)
                {
                    AbortPreparation();
                    return;
                }

                TryActivateRound();
                return;
            }

            if (!_roundActive)
            {
                return;
            }

            if (!_alivePlayers.Contains(playerId))
            {
                _oneShotMana.RemovePlayer(playerId);
                _participants.Remove(playerId);
                _preparedPlayers.Remove(playerId);
                _participantTeams.Remove(playerId);
                _roundParticipantNames.Remove(playerId);
                CurrentContext.ParticipantScenes.Remove(playerId);
                return;
            }

            if (_settings.PvpMode == PvpMode.OneShot)
            {
                int mana = _oneShotMana.MarkDead(playerId);
                _network?.SendManaUpdate(playerId, _currentRoundId, mana);
            }

            if (!_alivePlayers.Remove(playerId))
            {
                return;
            }

            _oneShotMana.RemovePlayer(playerId);
            _playerHealth.Remove(playerId);
            CheckRoundEnd();
        }

        private bool MarkReadyInCurrentContext(ushort playerId)
        {
            if (_roundActive || _waitingForPreparation)
            {
                return false;
            }

            IServerPlayer player;
            if (!_serverApi.ServerManager.TryGetPlayer(playerId, out player) || player == null ||
                (_settings.IndependentRoundsPerRoom && string.IsNullOrWhiteSpace(GetEffectiveRoomKey(player))))
            {
                return false;
            }

            if (IsAwaitingPostRoundBenchRelease(player.Username))
            {
                return false;
            }

            _statsManager.RegisterPlayer(player.Username);
            if (_readyPlayers.Add(playerId))
            {
                _readyOrder.Add(playerId);
            }

            CheckAutoStart();
            _network?.BroadcastRoundSettingsState(string.Empty);
            return true;
        }

        public bool ToggleReady(
            ushort playerId,
            out string message)
        {
            lock (_syncRoot)
            {
                using (EnterPlayerContext(playerId))
                {
                    return ToggleReadyInCurrentContext(playerId, out message);
                }
            }
        }

        private bool ToggleReadyInCurrentContext(ushort playerId, out string message)
        {
            message = string.Empty;

            if (_roundActive || _waitingForPreparation)
            {
                message = "Readiness can only be changed between rounds.";
                return false;
            }

            IServerPlayer player;
            if (!_serverApi.ServerManager.TryGetPlayer(playerId, out player) || player == null)
            {
                message = "Could not find your connected player session.";
                return false;
            }

            if (_settings.IndependentRoundsPerRoom && string.IsNullOrWhiteSpace(GetEffectiveRoomKey(player)))
            {
                message = "Your room is not synchronized yet. Try /rd again after the room finishes loading.";
                return false;
            }

            if (IsAwaitingPostRoundBenchRelease(player.Username))
            {
                message = "You must finish returning to the bench and get up before marking ready.";
                return false;
            }

            if (_readyPlayers.Remove(playerId))
            {
                _readyOrder.Remove(playerId);
                message = "You are no longer ready.";
                _network?.BroadcastRoundSettingsState(string.Empty);
                return true;
            }

            _statsManager.RegisterPlayer(player.Username);
            _readyPlayers.Add(playerId);
            _readyOrder.Remove(playerId);
            _readyOrder.Add(playerId);

            int connectedCount = ConnectedPlayerCount;
            int requiredCount = EffectiveReadyRequiredCount;

            if (!AutoStartEnabled)
            {
                message = "You are ready. Automatic round start is disabled.";
                _network?.BroadcastRoundSettingsState(string.Empty);
                return true;
            }

            if (connectedCount < 2)
            {
                message = "You are ready. At least two players must be connected to start a round.";
                _network?.BroadcastRoundSettingsState(string.Empty);
                return true;
            }

            if (requiredCount > 0 && _readyPlayers.Count >= requiredCount)
            {
                List<ushort> participantIds = _readyOrder
                    .Where(id => _readyPlayers.Contains(id) && IsConnectedPlayer(id))
                    .Take(requiredCount)
                    .ToList();
                List<ushort> readySnapshot = _readyOrder
                    .Where(id => _readyPlayers.Contains(id) && IsConnectedPlayer(id))
                    .ToList();

                if (participantIds.Count >= 2 && StartRound(participantIds))
                {
                    message = "The ready requirement was reached. The round is preparing.";
                    return true;
                }

                for (int i = 0; i < readySnapshot.Count; i++)
                {
                    ushort readyPlayerId = readySnapshot[i];
                    if (IsConnectedPlayer(readyPlayerId) && _readyPlayers.Add(readyPlayerId))
                    {
                        _readyOrder.Add(readyPlayerId);
                    }
                }

                message = "The ready requirement was reached, but the round could not start. Your ready status was kept.";
                _network?.BroadcastRoundSettingsState(string.Empty);
                return true;
            }

            message = "You are ready (" + _readyPlayers.Count + "/" + requiredCount + ").";
            _network?.BroadcastRoundSettingsState(string.Empty);
            return true;
        }

        private bool ClearReadyInCurrentContext(ushort playerId)
        {
            if (_roundActive || _waitingForPreparation)
            {
                return false;
            }

            bool removed = _readyPlayers.Remove(playerId);
            _readyOrder.Remove(playerId);
            if (removed)
            {
                _network?.BroadcastRoundSettingsState(string.Empty);
            }

            return removed;
        }

        private void ClearReadyVotes()
        {
            _readyPlayers.Clear();
            _readyOrder.Clear();
        }

        public bool StartRound()
        {
            lock (_syncRoot)
            {
                if (_settings.IndependentRoundsPerRoom && string.IsNullOrWhiteSpace(CurrentContext.SceneName))
                {
                    IServerPlayer firstPlayer = _serverApi.ServerManager.Players == null
                        ? null
                        : _serverApi.ServerManager.Players.FirstOrDefault(player =>
                            player != null && !string.IsNullOrWhiteSpace(GetEffectiveRoomKey(player)));
                    if (firstPlayer == null)
                    {
                        return false;
                    }

                    using (EnterPlayerContext(firstPlayer.Id))
                    {
                        return !string.IsNullOrWhiteSpace(CurrentContext.SceneName) && StartRound();
                    }
                }

                IReadOnlyCollection<IServerPlayer> players =
                    GetPlayersInCurrentContext();

                if (players == null)
                {
                    return false;
                }

                List<ushort> participantIds =
                    new List<ushort>();

                foreach (IServerPlayer player in players)
                {
                    if (player != null)
                    {
                        participantIds.Add(player.Id);
                    }
                }

                return StartRound(participantIds);
            }
        }

        public bool StartRound(
            IReadOnlyCollection<ushort> participantIds)
        {
            lock (_syncRoot)
            {
                if (_roundActive ||
                    _waitingForPreparation ||
                    _network == null ||
                    participantIds == null)
                {
                    return false;
                }

                if (_settings.IndependentRoundsPerRoom && string.IsNullOrWhiteSpace(CurrentContext.SceneName))
                {
                    IServerPlayer firstRequested = _serverApi.ServerManager.Players == null
                        ? null
                        : _serverApi.ServerManager.Players.FirstOrDefault(player =>
                            player != null && participantIds.Contains(player.Id) &&
                            !string.IsNullOrWhiteSpace(GetEffectiveRoomKey(player)));
                    if (firstRequested == null)
                    {
                        return false;
                    }

                    using (EnterPlayerContext(firstRequested.Id))
                    {
                        return !string.IsNullOrWhiteSpace(CurrentContext.SceneName) && StartRound(participantIds);
                    }
                }

                if (_settings.TimerIntegrationEnabled && GetAllContexts().Any(context =>
                    context != CurrentContext && (context.RoundActive || context.WaitingForPreparation)))
                {
                    BroadcastRoundMessage("A second room cannot start while another round uses the single HKMP.Timer integration. Disable timer integration to run simultaneous room rounds.");
                    return false;
                }

                IReadOnlyCollection<IServerPlayer> players =
                    GetPlayersInCurrentContext();

                if (players == null ||
                    players.Count(player => player != null) < 2)
                {
                    return false;
                }

                HashSet<ushort> requestedParticipantIds =
                    new HashSet<ushort>(participantIds);

                if (requestedParticipantIds.Count < 2)
                {
                    return false;
                }

                _participants.Clear();
                _preparedPlayers.Clear();
                _alivePlayers.Clear();
                _playerHealth.Clear();
                _participantTeams.Clear();
                _teamSnapshots.Clear();
                _roundParticipantNames.Clear();
                CurrentContext.ParticipantScenes.Clear();

                foreach (IServerPlayer player in players)
                {
                    if (player == null ||
                        !requestedParticipantIds.Contains(player.Id))
                    {
                        continue;
                    }

                _participants.Add(player.Id);
                _roundParticipantNames[player.Id] = player.Username ?? string.Empty;
                CurrentContext.ParticipantScenes[player.Id] = GetEffectiveRoomKey(player);
                _statsManager.RegisterPlayer(player.Username);
                }

                if (_participants.Count < 2)
                {
                    _participants.Clear();
                    _roundParticipantNames.Clear();
                    CurrentContext.ParticipantScenes.Clear();
                    return false;
                }

                ClearReadyVotes();

                if (_serverApi.ServerManager.ServerSettings.TeamsEnabled)
                {
                    SnapshotParticipantTeams(players);
                }

                _currentRoundId =
                    StartNewRoundId();
                _network.RegisterRoundParticipants(_currentRoundId, _participants);

                _waitingForPreparation = true;
                _roundActive = false;

                string pvpMessage;
                if (!SetPvpRoundActiveForCurrentContext(false, out pvpMessage))
                {
                    BroadcastRoundMessage(
                        "Round preparation was cancelled because PvP settings could not be applied: " +
                        pvpMessage);
                    AbortPreparation();
                    return false;
                }

                BroadcastRoundMessage(
                    "Round " +
                    _currentRoundId +
                    " preparation started.");
                _network?.BroadcastRoundSettingsState(string.Empty);

                foreach (ushort playerId in _participants)
                {
                    _network.SendPrepareRound(
                        playerId,
                        _currentRoundId);
                }

                return true;
            }
        }

        public void OnPlayerPrepared(
            ushort playerId,
            uint roundId)
        {
            lock (_syncRoot)
            {
                OnPlayerPreparedInCurrentContext(playerId, roundId);
            }
        }

        private void OnPlayerPreparedInCurrentContext(ushort playerId, uint roundId)
        {
            if (!_waitingForPreparation ||
                roundId != _currentRoundId ||
                !_participants.Contains(playerId))
            {
                return;
            }

            _preparedPlayers.Add(playerId);
            TryActivateRound();
        }

        private void TryActivateRound()
        {
            if (!_waitingForPreparation)
            {
                return;
            }

            List<ushort> disconnectedPlayers =
                new List<ushort>();

            foreach (ushort playerId in _participants)
            {
                IServerPlayer player;

                if (!_serverApi.ServerManager.TryGetPlayer(
                        playerId,
                        out player) ||
                    player == null)
                {
                    disconnectedPlayers.Add(playerId);
                }
            }

            for (int i = 0;
                 i < disconnectedPlayers.Count;
                 i++)
            {
                ushort playerId = disconnectedPlayers[i];

                _participants.Remove(playerId);
                _preparedPlayers.Remove(playerId);
                _participantTeams.Remove(playerId);
            }

            if (_participants.Count < 2)
            {
                AbortPreparation();
                return;
            }

            if (_preparedPlayers.Count < _participants.Count)
            {
                return;
            }

            _alivePlayers.Clear();
            _playerHealth.Clear();

            foreach (ushort playerId in _participants)
            {
                _alivePlayers.Add(playerId);
            }

            _waitingForPreparation = false;
            _roundActive = true;

            string pvpMessage;
            if (!SetPvpRoundActiveForCurrentContext(true, out pvpMessage))
            {
                BroadcastRoundMessage(
                    "Round could not start because PvP settings could not be applied: " +
                    pvpMessage);
                AbortPreparation();
                return;
            }

            if (_settings.PvpMode == PvpMode.OneShot)
            {
                _oneShotMana.Initialize(_participants);
            }
            else
            {
                _oneShotMana.Clear();
            }

            if (_timerIntegration != null &&
                !_timerIntegration.StartRound(
                    _currentRoundId))
            {
                BroadcastRoundMessage(
                    "Round could not start because HKMP.Timer integration could not be started.");

                AbortPreparation();

                return;
            }

            BroadcastRoundMessage(
                "Round " +
                _currentRoundId +
                " started.");
            _network?.BroadcastRoundSettingsState(string.Empty);

            foreach (ushort playerId in _participants)
            {
                _network.SendRoundStart(
                    playerId,
                    _currentRoundId);
            }

            if (_settings.PvpMode == PvpMode.OneShot)
            {
                foreach (ushort playerId in _participants)
                {
                    _network.SendManaUpdate(playerId, _currentRoundId, _oneShotMana.GetMana(playerId));
                }
            }
        }

        private void AbortPreparation()
        {
            uint roundId = _currentRoundId;

            _waitingForPreparation = false;
            _roundActive = false;
            _oneShotMana.Clear();

            string pvpRestoreMessage;
            if (!SetPvpRoundActiveForCurrentContext(false, out pvpRestoreMessage))
            {
                Modding.Logger.LogError(
                    "[HKMP.Rounds] Could not restore PvP state after aborted preparation: " +
                    pvpRestoreMessage);
            }

            _participants.Clear();
            _preparedPlayers.Clear();
            _alivePlayers.Clear();
            _playerHealth.Clear();
            _participantTeams.Clear();
            _teamSnapshots.Clear();
            _roundParticipantNames.Clear();
            CurrentContext.ParticipantScenes.Clear();

            if (roundId != 0)
            {
                _network?.BroadcastRoundEnd(roundId);
            }

            _network?.BroadcastRoundSettingsState(string.Empty);
        }

        private void CheckAutoStart()
        {
            if (!AutoStartEnabled ||
                _roundActive ||
                _waitingForPreparation)
            {
                return;
            }

            List<IServerPlayer> players = GetPlayersInCurrentContext();

            if (players.Count(player => player != null) < 2)
            {
                return;
            }

            int requiredCount = EffectiveReadyRequiredCount;
            if (requiredCount < 2 || _readyPlayers.Count < requiredCount)
            {
                return;
            }

            List<ushort> participantIds = _readyOrder
                .Where(id => _readyPlayers.Contains(id) && IsConnectedPlayer(id))
                .Take(requiredCount)
                .ToList();

            if (participantIds.Count >= 2)
            {
                StartRound(participantIds);
            }
        }

        public void OnPlayerDeath(ushort playerId)
        {
            lock (_syncRoot)
            {
                OnPlayerDeath(playerId, null);
            }
        }

        public void OnPlayerDeath(ushort playerId, ushort? killerId)
        {
            lock (_syncRoot)
            {
                RoundContext context = FindContextContainingPlayer(playerId);
                if (context == null)
                {
                    return;
                }

                using (new ContextScope(this, context))
                {
                    OnPlayerDeathInCurrentContextInternal(playerId, killerId);
                }
            }
        }

        private void OnPlayerDeathInCurrentContextInternal(ushort playerId, ushort? killerId)
        {
            if (!_roundActive || !_alivePlayers.Contains(playerId))
            {
                return;
            }

            if (_settings.PvpMode == PvpMode.OneShot)
            {
                int killerMana = 0;
                int victimMana = 0;
                bool rewarded = killerId.HasValue &&
                                killerId.Value != playerId &&
                                _alivePlayers.Contains(killerId.Value) &&
                                _oneShotMana.TryResolveKill(
                                    killerId.Value,
                                    playerId,
                                    out killerMana,
                                    out victimMana);

                if (!rewarded)
                {
                    victimMana = _oneShotMana.MarkDead(playerId);
                }
                else
                {
                    _network?.SendManaUpdate(killerId.Value, _currentRoundId, killerMana);
                }

                _network?.SendManaUpdate(playerId, _currentRoundId, victimMana);
            }

            if (!_alivePlayers.Remove(playerId))
            {
                return;
            }

            _playerHealth.Remove(playerId);
            _network?.BroadcastPlayerDeath(playerId);

            ushort winnerId;
            TeamSnapshot winningTeam;
            RoundEndKind outcome = DetermineRoundEnd(out winnerId, out winningTeam);
            _network?.SendDeathResolution(
                playerId,
                _alivePlayers.Count,
                outcome != RoundEndKind.None);

            CheckRoundEnd();
        }

        public bool EndRoundByTimer(
            uint roundId)
        {
            lock (_syncRoot)
            {
                using (EnterRoundContext(roundId))
                {
                    return EndRoundByTimerInCurrentContext(roundId);
                }
            }
        }

        private bool EndRoundByTimerInCurrentContext(uint roundId)
        {
            if (!_roundActive ||
                roundId == 0 ||
                roundId != _currentRoundId)
            {
                return false;
            }

            List<string> survivors =
                GetSurvivingPlayerHealthDescriptions();

            _roundActive = false;

            BroadcastRoundMessage(
                BuildRoundResultMessage(
                    "ROUND TIME EXPIRED",
                    "The round ended when the timer reached zero.",
                    survivors));

            _network?.BroadcastRoundEnd(
                roundId);

            FinishRound(
                roundId);

            return true;
        }

        public bool EndRoundManually()
        {
            lock (_syncRoot)
            {
                if (!_roundActive)
                {
                    return false;
                }

                uint roundId = _currentRoundId;

                List<string> survivors =
                    GetSurvivingPlayerHealthDescriptions();

                _roundActive = false;

                BroadcastRoundMessage(
                    BuildRoundResultMessage(
                        "ROUND ENDED",
                        "The round was ended by an administrator.",
                        survivors));

                _network?.BroadcastRoundEnd(roundId);

                FinishRound(roundId);
                return true;
            }
        }

        public bool SetWinTarget(
            int target)
        {
            lock (_syncRoot)
            {
                if (target < 0 ||
                    target > 1000 ||
                    _roundActive ||
                    _waitingForPreparation)
                {
                    return false;
                }

                _winTarget = target;
                _winTargetArmed = target > 0;
                ResetMatchScores();

                return true;
            }
        }

        public bool SetRoundSoul(
            int amount)
        {
            lock (_syncRoot)
            {
                if (HasAnyRoundInProgress)
                {
                    return false;
                }

                return _settings.SetRoundSoul(amount);
            }
        }

        public void SetAutoStart(bool enabled)
        {
            lock (_syncRoot)
            {
                _settings.SetAutoStart(enabled);
                IEnumerable<RoundContext> contexts = GetAllContexts();
                if (!enabled)
                {
                    foreach (RoundContext context in contexts)
                    {
                        context.ReadyPlayers.Clear();
                        context.ReadyOrder.Clear();
                    }
                    _network?.BroadcastRoundSettingsState(string.Empty);
                    return;
                }

                foreach (RoundContext context in contexts)
                {
                    using (new ContextScope(this, context))
                    {
                        CheckAutoStart();
                    }
                }
            }
        }

        public bool SetReadyRequiredCount(int count)
        {
            lock (_syncRoot)
            {
                if (_roundActive || _waitingForPreparation)
                {
                    return false;
                }

                if (count == 1)
                {
                    count = 2;
                }

                bool changed = _settings.SetReadyRequiredCount(count);
                if (changed)
                {
                    ClearReadyVotes();
                    _network?.BroadcastRoundSettingsState(string.Empty);
                }

                return changed;
            }
        }

        public void SetDisableEnemies(
            bool enabled)
        {
            _settings.SetDisableEnemies(enabled);
        }

        public void SetDebugDisableShade(
            bool enabled)
        {
            _settings.SetDebugDisableShade(enabled);
        }

        public void ResetMatchScores()
        {
            _matchPlayerWins.Clear();
            _matchTeamWins.Clear();
        }

        public void ResetAllMatchScores()
        {
            lock (_syncRoot)
            {
                foreach (RoundContext context in GetAllContexts())
                {
                    context.MatchPlayerWins.Clear();
                    context.MatchTeamWins.Clear();
                }
            }
        }

        public void Reset()
        {
            lock (_syncRoot)
            {
                if (_timerIntegration != null &&
                    _currentRoundId != 0)
                {
                    _timerIntegration.EndRound(
                        _currentRoundId);
                }

                _roundActive = false;
                _waitingForPreparation = false;

                string pvpRestoreMessage;
                if (!SetPvpRoundActiveForCurrentContext(false, out pvpRestoreMessage))
                {
                    Modding.Logger.LogError(
                        "[HKMP.Rounds] Could not restore PvP state during reset: " +
                        pvpRestoreMessage);
                }

                _participants.Clear();
                _preparedPlayers.Clear();
                _alivePlayers.Clear();
                ClearReadyVotes();
                _playerHealth.Clear();
                _participantTeams.Clear();
                _teamSnapshots.Clear();
                _roundParticipantNames.Clear();
                CurrentContext.ParticipantScenes.Clear();
                _postRoundReadyGates.Clear();
                _oneShotMana.Clear();

                ResetMatchState();
                _network?.BroadcastRoundSettingsState(string.Empty);
            }
        }

        public void UpdatePlayerHealth(
            ushort playerId,
            uint roundId,
            int health,
            int maxHealth,
            int blueHealth,
            int soul)
        {
            lock (_syncRoot)
            {
                UpdatePlayerHealthInCurrentContext(playerId, roundId, health, maxHealth, blueHealth, soul);
            }
        }

        private void UpdatePlayerHealthInCurrentContext(
            ushort playerId,
            uint roundId,
            int health,
            int maxHealth,
            int blueHealth,
            int soul)
        {
            if (!_roundActive ||
                roundId != _currentRoundId ||
                !_participants.Contains(playerId) ||
                !_alivePlayers.Contains(playerId))
            {
                return;
            }

            health =
                Math.Max(
                    0,
                    health);

            maxHealth =
                Math.Max(
                    0,
                    maxHealth);

            blueHealth =
                Math.Max(
                    0,
                    blueHealth);

            soul =
                Math.Max(
                    0,
                    Math.Min(
                        RoundSoul,
                        soul));

            if (maxHealth > 0 &&
                health > maxHealth)
            {
                health = maxHealth;
            }

            _playerHealth[playerId] =
                new PlayerHealthState
                {
                    Health = health,
                    MaxHealth = maxHealth,
                    BlueHealth = blueHealth,
                    Soul = soul
                };
        }

        private uint StartNewRoundId()
        {
            if (_roundIdSequence == uint.MaxValue)
            {
                _roundIdSequence = 1;
            }
            else
            {
                _roundIdSequence++;
            }

            if (_roundIdSequence == 0)
            {
                _roundIdSequence = 1;
            }

            return _roundIdSequence;
        }

        private RoundEndKind DetermineRoundEnd(
            out ushort winnerId,
            out TeamSnapshot winningTeam)
        {
            winnerId = 0;
            winningTeam = null;

            if (!_roundActive)
            {
                return RoundEndKind.None;
            }

            if (_alivePlayers.Count == 0)
            {
                return RoundEndKind.NoWinner;
            }

            bool teamsEnabled =
                _serverApi.ServerManager.ServerSettings.TeamsEnabled;

            if (!teamsEnabled)
            {
                if (_alivePlayers.Count != 1)
                {
                    return RoundEndKind.None;
                }

                winnerId =
                    _alivePlayers.First();

                return RoundEndKind.Player;
            }

            HashSet<byte> aliveTeams =
                new HashSet<byte>();

            List<ushort> aliveIndividualCompetitors =
                new List<ushort>();

            foreach (ushort playerId in _alivePlayers)
            {
                byte teamId;

                if (_participantTeams.TryGetValue(
                        playerId,
                        out teamId) &&
                    IsValidTeam(
                        teamId))
                {
                    aliveTeams.Add(
                        teamId);
                }
                else
                {
                    aliveIndividualCompetitors.Add(
                        playerId);
                }
            }

            int survivorGroups =
                aliveTeams.Count +
                aliveIndividualCompetitors.Count;

            if (survivorGroups > 1)
            {
                return RoundEndKind.None;
            }

            if (aliveTeams.Count == 1)
            {
                byte teamId =
                    aliveTeams.First();

                TeamSnapshot team;

                if (_teamSnapshots.TryGetValue(
                        teamId,
                        out team) &&
                    team != null)
                {
                    winningTeam = team;
                    return RoundEndKind.Team;
                }

                return RoundEndKind.None;
            }

            winnerId =
                aliveIndividualCompetitors[0];

            return RoundEndKind.Player;
        }

        private void CheckRoundEnd()
        {
            if (!_roundActive)
            {
                return;
            }

            ushort winnerId;
            TeamSnapshot winningTeam;

            RoundEndKind outcome =
                DetermineRoundEnd(
                    out winnerId,
                    out winningTeam);

            switch (outcome)
            {
                case RoundEndKind.NoWinner:
                    EndNoWinnerRound();
                    return;

                case RoundEndKind.Player:
                    EndPlayerRound(winnerId);
                    return;

                case RoundEndKind.Team:
                    EndTeamRound(winningTeam);
                    return;

                case RoundEndKind.None:
                default:
                    return;
            }
        }

        private void EndNoWinnerRound()
        {
            if (!_roundActive)
            {
                return;
            }

            uint roundId =
                _currentRoundId;

            List<string> survivors = GetSurvivingPlayerHealthDescriptions();
            _roundActive = false;

            BroadcastRoundMessage(
                BuildRoundResultMessage(
                    "ROUND DRAW",
                    "No winner was determined.",
                    survivors));

            _network?.BroadcastRoundEnd(
                roundId);

            FinishRound(
                roundId);
        }

        private void EndPlayerRound(
            ushort winnerId)
        {
            if (!_roundActive)
            {
                return;
            }

            uint roundId = _currentRoundId;
            List<string> survivors = GetSurvivingPlayerHealthDescriptions();
            _roundActive = false;

            IServerPlayer winner;
            string winnerName = "Player " + winnerId;
            int matchScore = 0;
            if (_serverApi.ServerManager.TryGetPlayer(winnerId, out winner) && winner != null)
            {
                winnerName = winner.Username;
                matchScore = AddPlayerMatchWin(winner.Username);
                _statsManager.AddWin(winner.Username);
                _network?.BroadcastRoundStats();
            }

            BroadcastRoundMessage(
                BuildRoundResultMessage(
                    "ROUND END",
                    "WINNER: " + winnerName,
                    survivors));

            if (WinTarget > 0 && matchScore >= WinTarget)
            {
                AnnounceTargetReachedForPlayer(winnerName, matchScore);
                CompleteWinTarget();
            }

            _network?.BroadcastRoundEnd(roundId, winnerId);
            FinishRound(roundId);
        }

        private void EndTeamRound(
            TeamSnapshot winningTeam)
        {
            if (!_roundActive || winningTeam == null)
            {
                return;
            }

            uint roundId = _currentRoundId;
            List<string> survivors = GetSurvivingPlayerHealthDescriptions();
            _roundActive = false;

            int matchScore = AddTeamMatchWin(winningTeam.Id);
            if (_settings.IndividualTeamWins)
            {
                foreach (KeyValuePair<ushort, string> member in winningTeam.MemberNames.OrderBy(pair => pair.Key))
                {
                    _statsManager.AddWin(member.Value);
                }
            }
            else
            {
                _statsManager.AddTeamWin(winningTeam.Name);
            }

            _network?.BroadcastRoundStats();
            BroadcastRoundMessage(
                BuildRoundResultMessage(
                    "TEAM ROUND WON",
                    "WINNING TEAM: " + winningTeam.Name,
                    survivors));

            if (WinTarget > 0 && matchScore >= WinTarget)
            {
                AnnounceTargetReachedForTeam(winningTeam.Name, matchScore);
                CompleteWinTarget();
            }

            _network?.BroadcastTeamRoundEnd(roundId, winningTeam.Id);
            FinishRound(roundId);
        }

        private int AddPlayerMatchWin(string playerName)
        {
            if (string.IsNullOrWhiteSpace(playerName))
            {
                return 0;
            }

            string key = playerName.Trim();
            int score;
            if (!_matchPlayerWins.TryGetValue(key, out score))
            {
                score = 0;
            }

            score++;
            _matchPlayerWins[key] = score;
            return score;
        }

        private int AddTeamMatchWin(byte team)
        {
            int score;
            if (!_matchTeamWins.TryGetValue(team, out score))
            {
                score = 0;
            }

            score++;
            _matchTeamWins[team] = score;
            return score;
        }

        private void AnnounceTargetReachedForPlayer(
            string playerName,
            int score)
        {
            BroadcastRoundMessage(
                "Match win target reached by " +
                playerName +
                " at " +
                score +
                " wins. Match counting is now disabled until a new target is set.");
        }

        private void AnnounceTargetReachedForTeam(
            string teamName,
            int score)
        {
            BroadcastRoundMessage(
                "Match win target reached by team " +
                teamName +
                " at " +
                score +
                " wins. Match counting is now disabled until a new target is set.");
        }

        private List<string> GetSurvivingPlayerHealthDescriptions()
        {
            SortedDictionary<string, List<string>> survivorsByTeam =
                new SortedDictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (ushort playerId in _alivePlayers.OrderBy(id => id))
            {
                IServerPlayer player;
                string playerName;
                if (_serverApi.ServerManager.TryGetPlayer(playerId, out player) && player != null)
                {
                    playerName = player.Username;
                }
                else
                {
                    TeamSnapshot snapshot = GetTeamSnapshot(playerId);
                    string savedName;
                    playerName = snapshot != null && snapshot.MemberNames.TryGetValue(playerId, out savedName) &&
                                 !string.IsNullOrWhiteSpace(savedName)
                        ? savedName
                        : "Player " + playerId;
                }

                TeamSnapshot team = GetTeamSnapshot(playerId);
                string groupName = team != null && !string.IsNullOrWhiteSpace(team.Name)
                    ? team.Name.Trim()
                    : "FREE-FOR-ALL";
                List<string> teamSurvivors;
                if (!survivorsByTeam.TryGetValue(groupName, out teamSurvivors))
                {
                    teamSurvivors = new List<string>();
                    survivorsByTeam.Add(groupName, teamSurvivors);
                }

                teamSurvivors.Add("  " + playerName + "   " + GetHealthDescription(playerId));
            }

            List<string> survivors = new List<string>();
            foreach (KeyValuePair<string, List<string>> team in survivorsByTeam)
            {
                if (!string.Equals(team.Key, "FREE-FOR-ALL", StringComparison.OrdinalIgnoreCase))
                {
                    survivors.Add("◆ " + team.Key);
                }

                survivors.AddRange(team.Value);
            }

            return survivors;
        }

        private string GetHealthDescription(
            ushort playerId)
        {
            PlayerHealthState state;
            bool hasHealth = _playerHealth.TryGetValue(playerId, out state) && state != null;
            bool oneShot = _settings.PvpMode == PvpMode.OneShot;
            int remainingSoul = oneShot ? _oneShotMana.GetMana(playerId) : hasHealth ? Math.Max(0, state.Soul) : 0;
            if (!hasHealth)
            {
                return oneShot
                    ? "♥ HP unknown    ◆ " + remainingSoul + " MP"
                    : "♥ HP unknown    ◆ MP unknown";
            }

            int health = Math.Max(0, state.Health);
            string healthValue = state.MaxHealth > 0
                ? health + "/" + state.MaxHealth
                : health + "/?";
            if (state.BlueHealth > 0)
            {
                healthValue += " +" + state.BlueHealth;
            }

            string soulValue = oneShot
                ? remainingSoul + " MP"
                : remainingSoul + "/" + Math.Max(0, RoundSoul) + " MP";
            return "♥ " + healthValue + "    ◆ " + soulValue;
        }

        private static string BuildRoundResultMessage(
            string heading,
            string outcome,
            List<string> survivors)
        {
            string title = string.IsNullOrWhiteSpace(heading) ? "ROUND RESULT" : heading.Trim();
            List<string> lines = new List<string>();

            if (!string.IsNullOrWhiteSpace(outcome))
            {
                lines.Add(outcome.Trim());
            }

            int survivorCount = survivors == null
                ? 0
                : survivors.Count(line => !string.IsNullOrEmpty(line) && line.StartsWith("  ", StringComparison.Ordinal));
            lines.Add("SURVIVORS  ·  " + survivorCount);
            if (survivorCount == 0)
            {
                lines.Add("  None");
            }
            else
            {
                lines.AddRange(survivors.Where(line => !string.IsNullOrWhiteSpace(line)));
            }

            int titleSeparatorLength = Math.Max(2, Math.Min(8, title.Length / 2));
            string titleRule = new string('━', titleSeparatorLength);
            string titleLine = titleRule + " " + title + " " + titleRule;
            int bottomRuleLength = Math.Max(2, titleSeparatorLength * 2 + title.Length - 1);
            string bottomRule = new string('━', bottomRuleLength);
            lines.Insert(0, titleLine);
            lines.Add(bottomRule);
            return string.Join("\n", lines);
        }

        private void FinishRound(
            uint roundId)
        {
            if (roundId != _currentRoundId)
            {
                return;
            }

            if (_timerIntegration != null)
            {
                _timerIntegration.EndRound(
                    roundId);
            }

            foreach (KeyValuePair<ushort, string> participant in _roundParticipantNames)
            {
                string username = participant.Value;
                if (!string.IsNullOrWhiteSpace(username))
                {
                    _postRoundReadyGates[username.Trim()] = new PostRoundReadyGate();
                }
            }

            _participants.Clear();
            _preparedPlayers.Clear();
            _alivePlayers.Clear();
            _playerHealth.Clear();
            _participantTeams.Clear();
            _teamSnapshots.Clear();
            _roundParticipantNames.Clear();
            CurrentContext.ParticipantScenes.Clear();
            ClearReadyVotes();

            _waitingForPreparation = false;
            _roundActive = false;
            _oneShotMana.Clear();

            string pvpRestoreMessage;
            if (!SetPvpRoundActiveForCurrentContext(false, out pvpRestoreMessage))
            {
                Modding.Logger.LogError(
                    "[HKMP.Rounds] Could not restore PvP state after round: " +
                    pvpRestoreMessage);
            }

            _network?.BroadcastRoundSettingsState(string.Empty);
        }

        private void CompleteWinTarget()
        {
            _winTarget = 0;
            _winTargetArmed = false;

            ResetMatchScores();
        }

        public void OnPlayerBenchStateChanged(
            ushort playerId,
            string username,
            bool atBench,
            bool returnToBenchPending,
            bool heroAlive,
            bool deathStarted)
        {
            lock (_syncRoot)
            {
                if (string.IsNullOrWhiteSpace(username))
                {
                    return;
                }

                string normalizedUsername = username.Trim();
                if (deathStarted)
                {
                    RoundContext context = GetAllContexts().FirstOrDefault(
                        known => known.PostRoundReadyGates.ContainsKey(normalizedUsername));
                    context = context ?? FindContextContainingPlayer(playerId) ?? ResolveContextForPlayer(playerId);
                    using (new ContextScope(this, context))
                    {
                        PostRoundReadyGate gate;
                        if (!_postRoundReadyGates.TryGetValue(normalizedUsername, out gate) || gate == null)
                        {
                            gate = new PostRoundReadyGate();
                            _postRoundReadyGates[normalizedUsername] = gate;
                        }
                        gate.ReturnPendingObserved = false;
                        gate.BenchObserved = false;
                        gate.AliveAtBenchObserved = false;
                        _network?.BroadcastRoundSettingsState(string.Empty);
                    }
                    return;
                }

                foreach (RoundContext context in GetAllContexts())
                {
                    if (context.PostRoundReadyGates.ContainsKey(normalizedUsername))
                    {
                        using (new ContextScope(this, context))
                        {
                            OnPlayerBenchStateChangedInCurrentContext(
                                normalizedUsername,
                                atBench,
                                returnToBenchPending,
                                heroAlive);
                        }
                        return;
                    }
                }
            }
        }

        private void OnPlayerBenchStateChangedInCurrentContext(
            string username,
            bool atBench,
            bool returnToBenchPending,
            bool heroAlive)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return;
            }

            string key = username.Trim();
            PostRoundReadyGate gate;
            if (!_postRoundReadyGates.TryGetValue(key, out gate) || gate == null)
            {
                return;
            }

            if (returnToBenchPending)
            {
                gate.ReturnPendingObserved = true;
                return;
            }

            if (!heroAlive)
            {
                gate.BenchObserved = false;
                gate.AliveAtBenchObserved = false;
                return;
            }

            if (atBench)
            {
                gate.BenchObserved = true;
                gate.AliveAtBenchObserved = true;
                return;
            }

            if ((gate.BenchObserved && gate.AliveAtBenchObserved) || gate.ReturnPendingObserved)
            {
                _postRoundReadyGates.Remove(key);
                _network?.BroadcastRoundSettingsState(string.Empty);
            }
        }

        private bool IsAwaitingPostRoundBenchRelease(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return false;
            }

            string key = username.Trim();
            return GetAllContexts().Any(context => context.PostRoundReadyGates.ContainsKey(key));
        }

        private void ResetMatchState()
        {
            ResetMatchScores();

            _winTarget = 0;
            _winTargetArmed = false;
        }

        private TeamSnapshot GetTeamSnapshot(
            ushort playerId)
        {
            byte teamId;

            if (!_participantTeams.TryGetValue(
                    playerId,
                    out teamId))
            {
                return null;
            }

            TeamSnapshot snapshot;

            return
                _teamSnapshots.TryGetValue(
                    teamId,
                    out snapshot)
                    ? snapshot
                    : null;
        }

        private void SnapshotParticipantTeams(
        IReadOnlyCollection<IServerPlayer> players)
        {
            _participantTeams.Clear();
            _teamSnapshots.Clear();

            if (players == null)
            {
                return;
            }

            foreach (IServerPlayer player in players)
            {
                if (player == null ||
                    !_participants.Contains(
                        player.Id))
                {
                    continue;
                }
                Team team =
                    player.Team;

                if (team == Team.None)
                {
                    continue;
                }

                byte teamId =
                    (byte)team;

                _participantTeams[
                    player.Id] =
                    teamId;

                TeamSnapshot snapshot;

                if (!_teamSnapshots.TryGetValue(
                        teamId,
                        out snapshot))
                {
                    snapshot =
                        new TeamSnapshot
                        {
                            Id = teamId,
                            Name = GetTeamName(
                                teamId)
                        };

                    _teamSnapshots[
                        teamId] =
                        snapshot;
                }

                snapshot.Members.Add(
                    player.Id);
                snapshot.MemberNames[player.Id] = player.Username ?? string.Empty;
                _statsManager.RegisterPlayer(player.Username);
            }
        }

        private string GetTeamName(
            byte teamId)
        {
            Team team =
                (Team)teamId;

            switch (team)
            {
                case Team.Moss:
                    return "Moss";

                case Team.Hive:
                    return "Hive";

                case Team.Grimm:
                    return "Grimm";

                case Team.Lifeblood:
                    return "Lifeblood";

                case Team.None:
                default:
                    return "Team " + teamId;
            }
        }

        private bool IsValidTeam(
            byte teamId)
        {
            TeamSnapshot snapshot;

            if (!_teamSnapshots.TryGetValue(
                    teamId,
                    out snapshot) ||
                snapshot == null)
            {
                return false;
            }

            int participantCount = 0;

            foreach (ushort memberId in snapshot.Members)
            {
                if (!_participants.Contains(memberId))
                {
                    continue;
                }

                participantCount++;

                if (participantCount >= 2)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
