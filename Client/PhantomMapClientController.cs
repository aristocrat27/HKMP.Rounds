using GlobalEnums;
using Hkmp.Api.Client;
using HKMP.Rounds.Networking.Packets;
using HKMP.Rounds.Client.Combat;
using HKMP.Rounds.Settings;
using Modding;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HKMP.Rounds.Client
{
    internal sealed class PhantomMapClientController : MonoBehaviour
    {
        private static readonly HashSet<string> MantisVillageScenes =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Fungus2_15", "Fungus2_31" };
        private const float PlayerVisibilityRefreshInterval = 0.25f;

        private static PhantomMapClientController _instance;
        private IClientApi _clientApi;
        private ClientNetManager _network;
        private int _activePhantomMapId;
        private int _forcedNextMapId;
        private bool _hasForcedNextMapId;
        private string _forcedNextScene = string.Empty;
        private int _pendingPhantomBenchWarpId;
        private bool _phantomBenchWarpRunning;
        private int _pendingPhantomDeathBenchWarpId;
        private int _phantomDeathReturnMapId;
        private bool _phantomDeathBenchWarpRunning;
        private bool _sceneTransitionRunning;
        private MapTravelCommandPacket _pendingMapTravelCommand;
        private bool _pendingMapTravelCommandRunning;
        private IDisposable _sceneTransitionScope;
        private bool _mapTravelProgressPending;
        private string _mapTravelProgressScene = string.Empty;
        private readonly Dictionary<Renderer, bool> _backgroundRendererStates = new Dictionary<Renderer, bool>();
        private readonly Dictionary<ushort, int> _remotePlayerMaps = new Dictionary<ushort, int>();
        private float _nextPlayerVisibilityCheck;
        private bool _initialized;

        internal static int CurrentPhantomMapId => _instance == null ? 0 : _instance._activePhantomMapId;

        public static bool IsDeathReturnPending
        {
            get
            {
                PhantomMapClientController c = _instance;
                return c != null && (c._pendingPhantomDeathBenchWarpId > 0 ||
                    c._phantomDeathReturnMapId > 0 || c._phantomDeathBenchWarpRunning);
            }
        }

        public static bool IsActive
        {
            get
            {
                PhantomMapClientController c = _instance;
                return c != null && c._activePhantomMapId > 0 && MantisVillageScenes.Contains(SafeCurrentScene());
            }
        }

        public static void RefreshBackgroundVisibility()
        {
            if (_instance != null)
            {
                _instance.ApplyBackgroundVisibilityToActiveScene();
            }
        }

        public static bool IsMantisPairTransition(string sourceScene, string targetScene)
        {
            string source = GetLogicalSceneName(sourceScene);
            string target = GetLogicalSceneName(targetScene);
            return (string.Equals(source, "Fungus2_15", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(target, "Fungus2_31", StringComparison.OrdinalIgnoreCase)) ||
                   (string.Equals(source, "Fungus2_31", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(target, "Fungus2_15", StringComparison.OrdinalIgnoreCase));
        }

        internal static bool TryGetActivePhantomScene(out Scene phantomScene, out string logicalSceneName)
        {
            phantomScene = default(Scene);
            logicalSceneName = string.Empty;
            if (!IsActive) return false;

            Scene active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!active.IsValid() || !active.isLoaded) return false;

            phantomScene = active;
            logicalSceneName = SafeCurrentScene();
            return true;
        }

        public static bool ShouldKeepActivePhantomInstanceForBench(BenchLocation target)
        {
            return IsActive && target != null && target.IsValid &&
                   MantisVillageScenes.Contains(target.SceneName) &&
                   target.PhantomMapId == CurrentPhantomMapId;
        }

        internal static int GetPhantomMapIdForScene(string sceneName)
        {
            if (_instance == null || _instance._activePhantomMapId <= 0 || string.IsNullOrWhiteSpace(sceneName))
            {
                return 0;
            }

            string logicalScene = GetLogicalSceneName(sceneName).Trim();
            return MantisVillageScenes.Contains(logicalScene) ? _instance._activePhantomMapId : 0;
        }

        internal static string GetRoundRoomKey(string sceneName)
        {
            string logicalScene = GetLogicalSceneName(sceneName).Trim();
            if (logicalScene.Length == 0)
            {
                return string.Empty;
            }

            const string phantomPrefix = "Hkmp.Rounds_Mantis_";
            if (logicalScene.StartsWith(phantomPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return logicalScene;
            }

            if (!MantisVillageScenes.Contains(logicalScene))
            {
                return logicalScene;
            }

            int phantomMapId = GetPhantomMapIdForScene(logicalScene);
            PhantomMapClientController controller = _instance;
            if (phantomMapId <= 0 && controller != null && controller._hasForcedNextMapId &&
                controller._forcedNextMapId > 0 &&
                string.Equals(controller._forcedNextScene, logicalScene, StringComparison.OrdinalIgnoreCase))
            {
                phantomMapId = controller._forcedNextMapId;
            }

            return phantomMapId > 0
                ? phantomPrefix + phantomMapId.ToString(CultureInfo.InvariantCulture)
                : logicalScene;
        }

        public static bool IsPlayerInSamePhantomMap(ushort playerId)
        {
            if (!MantisVillageScenes.Contains(SafeCurrentScene()))
            {
                return true;
            }

            PhantomMapClientController controller = _instance;
            if (controller == null)
            {
                return false;
            }

            int remoteMapId;
            if (!controller._remotePlayerMaps.TryGetValue(playerId, out remoteMapId))
            {
                remoteMapId = 0;
            }

            return controller._activePhantomMapId == remoteMapId;
        }

        public static bool PrepareForBenchWarp(BenchLocation target)
        {
            PhantomMapClientController controller = _instance;
            if (target == null || !target.IsValid ||
                (target.PhantomMapId > 0 && !MantisVillageScenes.Contains(target.SceneName)))
            {
                return false;
            }

            bool targetIsMantis = MantisVillageScenes.Contains(target.SceneName);
            Scene activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            bool activeIsMantis = activeScene.IsValid() && activeScene.isLoaded && MantisVillageScenes.Contains(activeScene.name);
            if (targetIsMantis)
            {
                if (controller == null)
                {
                    return target.PhantomMapId == 0;
                }

                controller._forcedNextMapId = target.PhantomMapId;
                controller._forcedNextScene = target.SceneName;
                controller._hasForcedNextMapId = true;
                if (activeIsMantis)
                {
                    controller.ChangeCurrentInstance(target.PhantomMapId, false);
                }
                else
                {
                    controller._activePhantomMapId = target.PhantomMapId;
                }
                return true;
            }

            if (controller != null)
            {
                if (activeIsMantis && controller._activePhantomMapId > 0)
                {
                    controller.ChangeCurrentInstance(0, false);
                }
                else if (!activeIsMantis)
                {
                    controller._activePhantomMapId = 0;
                }
                controller._hasForcedNextMapId = false;
                controller._forcedNextScene = string.Empty;
            }

            return true;
        }

        public static bool TryGetActivePhantomBenchTarget(out BenchLocation target)
        {
            target = null;
            if (!IsActive) return false;
            target = new BenchLocation("Fungus2_31", "RestBench", (int)MapZone.WASTES, "Phantom Mantis Village", CurrentPhantomMapId);
            return true;
        }


        public static bool ReturnToNormalMantisInstance()
        {
            PhantomMapClientController c = _instance;
            if (c == null || !MantisVillageScenes.Contains(SafeCurrentScene()) || IsDeathReturnPending)
                return false;
            if (c._activePhantomMapId == 0 || !c.ChangeCurrentInstance(0, false))
                return false;

            c.SendCatalogRequest(false, 0);
            return true;
        }

        public void Initialize(IClientApi clientApi, ClientNetManager network)
        {
            if (_initialized) return;
            _initialized = true;
            _instance = this;
            _clientApi = clientApi;
            _network = network;

            if (_network != null)
            {
                _network.MapTravelCommandReceived -= OnMapTravelCommandReceived;
                _network.MapTravelCommandReceived += OnMapTravelCommandReceived;
                _network.MapTravelStateReceived -= OnMapTravelStateReceived;
                _network.MapTravelStateReceived += OnMapTravelStateReceived;
            }

            if (_clientApi != null && _clientApi.ClientManager != null)
            {
                _clientApi.ClientManager.ConnectEvent -= OnConnected;
                _clientApi.ClientManager.ConnectEvent += OnConnected;
                _clientApi.ClientManager.DisconnectEvent -= OnDisconnected;
                _clientApi.ClientManager.DisconnectEvent += OnDisconnected;
                _clientApi.ClientManager.PlayerConnectEvent -= OnPlayerEnterScene;
                _clientApi.ClientManager.PlayerConnectEvent += OnPlayerEnterScene;
                _clientApi.ClientManager.PlayerDisconnectEvent -= OnPlayerEnterScene;
                _clientApi.ClientManager.PlayerDisconnectEvent += OnPlayerEnterScene;
                _clientApi.ClientManager.PlayerEnterSceneEvent -= OnPlayerEnterScene;
                _clientApi.ClientManager.PlayerEnterSceneEvent += OnPlayerEnterScene;
                _clientApi.ClientManager.PlayerLeaveSceneEvent -= OnPlayerEnterScene;
                _clientApi.ClientManager.PlayerLeaveSceneEvent += OnPlayerEnterScene;
            }

            UnityEngine.SceneManagement.SceneManager.activeSceneChanged += OnActiveSceneChanged;
            ModHooks.BeforeSceneLoadHook += OnBeforeSceneLoad;
            ModHooks.BeforePlayerDeadHook += OnBeforePlayerDead;
            On.GameManager.OnNextLevelReady += OnNextLevelReady;
            ModHooks.AfterPlayerDeadHook += OnAfterPlayerDead;

            SceneTravelCatalog.ObserveCurrentScene();
            SendCatalogRequest(false, 0);
        }

        private void OnPlayerEnterScene(IClientPlayer player)
        {
            UpdateRemotePlayersVisibility();
        }

        private void OnConnected() => SendCatalogRequest(false, 0);

        private void OnDisconnected()
        {
            _remotePlayerMaps.Clear();
            SetAllRemotePlayersVisible(true);
            _pendingPhantomBenchWarpId = 0;
            _pendingPhantomDeathBenchWarpId = 0;
            _phantomDeathReturnMapId = 0;
            _pendingMapTravelCommand = null;
            _mapTravelProgressPending = false;
            _mapTravelProgressScene = string.Empty;
            if (_activePhantomMapId > 0 && MantisVillageScenes.Contains(SafeCurrentScene()))
                ChangeCurrentInstance(0, false);

            RestoreAllBackgroundRenderers();
            MantisVillageProgressController.RestoreOriginalProgress();
            _activePhantomMapId = 0;
            _hasForcedNextMapId = false;
            _forcedNextScene = string.Empty;
            RoundRoomExitBlocker.SetPhantomRoomLocked(false);
        }

        public static void NotifyBenchWarpCompleted()
        {
            PhantomMapClientController controller = _instance;
            if (controller == null)
            {
                return;
            }

            controller.UpdateRemotePlayersVisibility();
            controller.SendCatalogRequest(false, 0);
        }

        private void SendCatalogRequest(bool includeFullCatalog, int page)
        {
            if (_network == null || _clientApi == null || _clientApi.NetClient == null || !_clientApi.NetClient.IsConnected)
                return;

            SceneTravelCatalog.ObserveCurrentScene();
            string sceneName = SafeCurrentScene();
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                return;
            }
            string catalog = includeFullCatalog
                ? SceneTravelCatalog.BuildFullCatalogJson()
                : SceneTravelCatalog.BuildCurrentSceneJson();
            _network.RequestMapTravelState(sceneName, catalog, page, includeFullCatalog, _activePhantomMapId);
        }
        private IEnumerator ProcessPendingMapTravelCommand()
        {
            try
            {
                while (_pendingMapTravelCommand != null)
                {
                    if (_clientApi == null || _clientApi.NetClient == null || !_clientApi.NetClient.IsConnected)
                    {
                        _pendingMapTravelCommand = null;
                        yield break;
                    }

                    GameManager manager = GameManager.instance;
                    if (_sceneTransitionRunning || manager == null || manager.IsInSceneTransition ||
                        EmergencyBenchWarpController.IsWarpRunning || _phantomBenchWarpRunning)
                    {
                        yield return null;
                        continue;
                    }

                    MapTravelCommandPacket pending = _pendingMapTravelCommand;
                    _pendingMapTravelCommand = null;
                    OnMapTravelCommandReceived(pending);
                    yield return null;
                }
            }
            finally
            {
                _pendingMapTravelCommandRunning = false;
            }
        }

        private void OnMapTravelStateReceived(MapTravelStatePacket packet)
        {
            if (packet == null)
            {
                return;
            }

            Dictionary<ushort, int> snapshot = new Dictionary<ushort, int>();
            string serializedMaps = packet.PlayerPhantomMaps ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(serializedMaps))
            {
                string[] pairs = serializedMaps.Split(',');
                for (int i = 0; i < pairs.Length; i++)
                {
                    string[] parts = pairs[i].Split(':');
                    ushort playerId;
                    int mapId;
                    if (parts.Length != 2 ||
                        !ushort.TryParse(parts[0].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out playerId) ||
                        !int.TryParse(parts[1].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out mapId) ||
                        mapId <= 0 || snapshot.ContainsKey(playerId))
                    {
                        PhantomMapDiagnostics.Record("client.map-state.invalid-player-snapshot",
                            "Entry=" + pairs[i], true);
                        return;
                    }

                    snapshot.Add(playerId, mapId);
                }
            }

            _remotePlayerMaps.Clear();
            foreach (KeyValuePair<ushort, int> entry in snapshot)
            {
                _remotePlayerMaps.Add(entry.Key, entry.Value);
            }

            UpdateRemotePlayersVisibility();
        }
        private void OnMapTravelCommandReceived(MapTravelCommandPacket packet)
        {
            if (packet == null || string.IsNullOrWhiteSpace(packet.SceneName) ||
                string.IsNullOrWhiteSpace(packet.EntryGate))
            {
                return;
            }

            if (packet.PhantomMapId > 0 && !MantisVillageScenes.Contains(packet.SceneName))
            {
                Modding.Logger.LogError("[HKMP.Rounds] Rejected invalid phantom Mantis travel command.");
                return;
            }

            GameManager manager = GameManager.instance;
            if (_sceneTransitionRunning || manager == null || manager.IsInSceneTransition ||
                EmergencyBenchWarpController.IsWarpRunning || _phantomBenchWarpRunning)
            {
                _pendingMapTravelCommand = packet;
                if (!_pendingMapTravelCommandRunning)
                {
                    _pendingMapTravelCommandRunning = true;
                    StartCoroutine(ProcessPendingMapTravelCommand());
                }
                return;
            }
            string currentScene = SafeCurrentScene();
            if (_activePhantomMapId > 0 && MantisVillageScenes.Contains(currentScene))
            {
                string pairedScene = string.Equals(currentScene, "Fungus2_31", StringComparison.OrdinalIgnoreCase)
                ? "Fungus2_15"
                : "Fungus2_31";
                bool staysInCurrentRoom = string.Equals(packet.SceneName, currentScene, StringComparison.OrdinalIgnoreCase);
                bool entersPairedRoom = string.Equals(packet.SceneName, pairedScene, StringComparison.OrdinalIgnoreCase);
                bool staysInActivePhantom = packet.PhantomMapId == _activePhantomMapId && (staysInCurrentRoom || entersPairedRoom);
                bool switchesToAnotherPhantom = packet.PhantomMapId > 0 && packet.PhantomMapId != _activePhantomMapId && (staysInCurrentRoom || entersPairedRoom);
                bool leavesPhantomInstance = packet.PhantomMapId == 0;
                if (!staysInActivePhantom && !switchesToAnotherPhantom && !leavesPhantomInstance)
                {
                    Modding.Logger.LogError("[HKMP.Rounds] Rejected invalid phantom Mantis destination.");
                    return;
                }
                if (staysInCurrentRoom && staysInActivePhantom)
                {
                    MapTravelProgressController.ApplyCurrentScene();
                    SendCatalogRequest(false, 0);
                    return;
                }
            }
            if (_pendingPhantomDeathBenchWarpId > 0 && !MantisVillageScenes.Contains(packet.SceneName))
                _pendingPhantomDeathBenchWarpId = 0;
            _mapTravelProgressPending = true;
            _mapTravelProgressScene = packet.SceneName;
            bool changingInstanceInCurrentMantisScene =
            string.Equals(currentScene, packet.SceneName, StringComparison.OrdinalIgnoreCase) &&
            MantisVillageScenes.Contains(currentScene) &&
            _activePhantomMapId != packet.PhantomMapId;
            _pendingPhantomBenchWarpId = (packet.PhantomMapId > 0 && string.Equals(packet.SceneName, "Fungus2_31", StringComparison.OrdinalIgnoreCase))
            ? packet.PhantomMapId
            : 0;
            if (changingInstanceInCurrentMantisScene)
            {
                if (packet.PhantomMapId > 0 && string.Equals(packet.SceneName, "Fungus2_31", StringComparison.OrdinalIgnoreCase))
                {
                    MantisVillageProgressController.CaptureOriginalProgress();
                    _activePhantomMapId = packet.PhantomMapId;
                    _forcedNextMapId = packet.PhantomMapId;
                    _forcedNextScene = packet.SceneName;
                    _hasForcedNextMapId = true;
                    _pendingPhantomBenchWarpId = 0;
                    StartCoroutine(WarpToInitialPhantomBench(packet.PhantomMapId));
                    return;
                }

                if (!ChangeCurrentInstance(packet.PhantomMapId))
                {
                    _pendingPhantomBenchWarpId = 0;
                    _mapTravelProgressPending = false;
                    _mapTravelProgressScene = string.Empty;
                    return;
                }
                if (packet.PhantomMapId == 0)
                    MapTravelProgressController.ApplyCurrentScene();
                _mapTravelProgressPending = false;
                _mapTravelProgressScene = string.Empty;
                return;
            }
            if (packet.PhantomMapId > 0 &&
            string.Equals(packet.SceneName, "Fungus2_31", StringComparison.OrdinalIgnoreCase) &&
            (string.Equals(packet.EntryGate, "RestBench", StringComparison.OrdinalIgnoreCase) ||
            _activePhantomMapId <= 0 || !MantisVillageScenes.Contains(currentScene)))
            {
                MantisVillageProgressController.CaptureOriginalProgress();
                _activePhantomMapId = packet.PhantomMapId;
                _forcedNextMapId = packet.PhantomMapId;
                _forcedNextScene = packet.SceneName;
                _hasForcedNextMapId = true;
                StartCoroutine(WarpToInitialPhantomBench(packet.PhantomMapId));
                return;
            }
            _forcedNextMapId = packet.PhantomMapId;
            _forcedNextScene = packet.SceneName;
            _hasForcedNextMapId = true;
            RoundsSettingsUI.CloseForGameplayTransition();
            StartCoroutine(TransitionToMapScene(
            packet.SceneName,
            string.IsNullOrWhiteSpace(packet.EntryGate) ? "door" : packet.EntryGate));
        }
        private IEnumerator TransitionToMapScene(string sceneName, string entryGate)
        {
            _sceneTransitionRunning = true;
            Scene sourceScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            bool transitionObserved = false;
            bool arrived = false;
            try
            {
                _sceneTransitionScope = RoundRoomExitBlocker.AcquireProgrammaticTransition();
                if (!BenchwarpIntegration.TryChangeToScene(sceneName, entryGate, 0f, out string transitionMessage))
                {
                    _hasForcedNextMapId = false;
                    _forcedNextScene = string.Empty;
                    _pendingPhantomBenchWarpId = 0;
                    _mapTravelProgressPending = false;
                    _mapTravelProgressScene = string.Empty;
                    Modding.Logger.LogError("[HKMP.Rounds] Benchwarp map transition failed: " + transitionMessage);
                    yield break;
                }
                float deadline = Time.realtimeSinceStartup + 45f;
                while (Time.realtimeSinceStartup < deadline)
                {
                    GameManager manager = GameManager.instance;
                    Scene activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                    if (manager != null && (manager.IsInSceneTransition || (sourceScene.IsValid() && activeScene.IsValid() && sourceScene.handle != activeScene.handle)))
                        transitionObserved = true;
                    if (transitionObserved && manager != null && !manager.IsInSceneTransition &&
                    activeScene.IsValid() && string.Equals(activeScene.name, sceneName, StringComparison.OrdinalIgnoreCase))
                    {
                        arrived = true;
                        break;
                    }
                    yield return null;
                }
                if (!arrived)
                {
                    Modding.Logger.LogError("[HKMP.Rounds] Benchwarp map transition timed out for " + sceneName + ".");
                    _hasForcedNextMapId = false;
                    _forcedNextScene = string.Empty;
                    _pendingPhantomBenchWarpId = 0;
                    _mapTravelProgressPending = false;
                    _mapTravelProgressScene = string.Empty;
                    yield break;
                }
                yield return null;
                yield return new WaitForEndOfFrame();
            }
            finally
            {
                _sceneTransitionScope?.Dispose();
                _sceneTransitionScope = null;
                _sceneTransitionRunning = false;
            }
        }
        private string OnBeforeSceneLoad(string targetScene)
        {
            string target = targetScene ?? string.Empty;
            if (_hasForcedNextMapId && string.Equals(target, _forcedNextScene, StringComparison.OrdinalIgnoreCase))
            {
                if (_activePhantomMapId > 0 && _forcedNextMapId == 0)
                    MantisVillageProgressController.RestoreOriginalProgress();
                _activePhantomMapId = _forcedNextMapId;
                _hasForcedNextMapId = false;
                _forcedNextScene = string.Empty;
                return targetScene;
            }
            if (_pendingPhantomDeathBenchWarpId > 0 &&
                string.Equals(target, "Fungus2_31", StringComparison.OrdinalIgnoreCase))
            {
                _activePhantomMapId = _pendingPhantomDeathBenchWarpId;
                return targetScene;
            }
            if (_activePhantomMapId > 0 &&
                MantisVillageScenes.Contains(SafeCurrentScene()) &&
                MantisVillageScenes.Contains(target))
            {
                _forcedNextMapId = _activePhantomMapId;
                _forcedNextScene = target;
                _hasForcedNextMapId = true;
                return targetScene;
            }
            if (_activePhantomMapId > 0 && !MantisVillageScenes.Contains(target) &&
                !EmergencyBenchWarpController.IsWarpRunning)
            {
                MantisVillageProgressController.RestoreOriginalProgress();
                _activePhantomMapId = 0;
                _pendingPhantomBenchWarpId = 0;
                if (!IsDeathReturnPending) _pendingPhantomDeathBenchWarpId = 0;
                _hasForcedNextMapId = false;
                _forcedNextScene = string.Empty;
            }
            return targetScene;
        }
        private void OnNextLevelReady(On.GameManager.orig_OnNextLevelReady orig, GameManager self)
        {
            orig(self);
            ConfigureActiveSceneAfterLoad();
        }
        private void ApplyForcedMapForScene(string sceneName)
        {
            if (!_hasForcedNextMapId || !string.Equals(sceneName, _forcedNextScene, StringComparison.OrdinalIgnoreCase))
                return;
            if (_activePhantomMapId > 0 && _forcedNextMapId == 0)
                MantisVillageProgressController.RestoreOriginalProgress();
            _activePhantomMapId = _forcedNextMapId;
            _hasForcedNextMapId = false;
            _forcedNextScene = string.Empty;
        }
        private void ConfigureActiveSceneAfterLoad()
        {
            Scene scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!scene.IsValid()) return;
            ApplyForcedMapForScene(scene.name);
            bool restoringPhantomDeath = _pendingPhantomDeathBenchWarpId > 0 &&
                string.Equals(scene.name, "Fungus2_31", StringComparison.OrdinalIgnoreCase);
            if (restoringPhantomDeath)
            {
                _activePhantomMapId = _pendingPhantomDeathBenchWarpId;
            }
            _backgroundRendererStates.Clear();
            if (!MantisVillageScenes.Contains(scene.name))
            {
                if (_activePhantomMapId > 0 && !EmergencyBenchWarpController.IsWarpRunning)
                {
                    MantisVillageProgressController.RestoreOriginalProgress();
                    _activePhantomMapId = 0;
                }
                SetAllRemotePlayersVisible(true);
                RoundRoomExitBlocker.SetPhantomRoomLocked(false);
                SceneTravelCatalog.ObserveCurrentScene();
                SendCatalogRequest(false, 0);
                return;
            }
            if (_activePhantomMapId > 0)
            {
                MantisVillageProgressController.CaptureOriginalProgress();
                ApplyBackgroundVisibilityToActiveScene();
                MapTravelProgressController.ApplyCurrentScene();
            }
            else if (_mapTravelProgressPending && string.Equals(scene.name, _mapTravelProgressScene, StringComparison.OrdinalIgnoreCase))
            {
                MapTravelProgressController.ApplyCurrentScene();
            }
            RoundRoomExitBlocker.SetPhantomRoomLocked(
                _activePhantomMapId > 0 && MantisVillageScenes.Contains(SafeCurrentScene()));
            UpdateRemotePlayersVisibility();
            _mapTravelProgressPending = false;
            _mapTravelProgressScene = string.Empty;
            SceneTravelCatalog.ObserveCurrentScene();
            SendCatalogRequest(false, 0);
            if (restoringPhantomDeath)
            {
                _pendingPhantomDeathBenchWarpId = 0;
            }
            if (_pendingPhantomBenchWarpId > 0 && _activePhantomMapId == _pendingPhantomBenchWarpId &&
            string.Equals(scene.name, "Fungus2_31", StringComparison.OrdinalIgnoreCase))
            {
                StartCoroutine(WarpToPhantomBenchAfterLoad(_pendingPhantomBenchWarpId));
            }
        }
        private IEnumerator WarpToInitialPhantomBench(int mapId)
        {
            _sceneTransitionRunning = true;
            bool completed = false;
            BenchLocation target = new BenchLocation("Fungus2_31", "RestBench", (int)MapZone.WASTES, "Phantom Mantis Village", mapId);
            try
            {
                _pendingPhantomBenchWarpId = 0;
                _mapTravelProgressPending = true;
                _mapTravelProgressScene = target.SceneName;
                RoundsSettingsUI.CloseForGameplayTransition();
                if (!EmergencyBenchWarpController.TryWarpToBench(target, true, out string message))
                {
                    Modding.Logger.LogError("[HKMP.Rounds] Could not start phantom Mantis bench warp: " + message);
                    yield break;
                }
                float deadline = Time.realtimeSinceStartup + 60f;
                while (EmergencyBenchWarpController.IsWarpRunning && Time.realtimeSinceStartup < deadline)
                    yield return null;
                if (EmergencyBenchWarpController.IsWarpRunning)
                {
                    Modding.Logger.LogError("[HKMP.Rounds] Phantom Mantis bench warp timed out.");
                    yield break;
                }
                yield return null;
                yield return new WaitForEndOfFrame();
                HeroController hero = HeroController.instance;
                bool arrived = _activePhantomMapId == mapId &&
                string.Equals(SafeCurrentScene(), target.SceneName, StringComparison.OrdinalIgnoreCase) &&
                hero != null && EmergencyBenchWarpController.IsHeroNearBench(hero, target);
                if (!arrived)
                {
                    Modding.Logger.LogError("[HKMP.Rounds] Initial phantom Mantis warp did not reach RestBench in Fungus2_31.");
                    yield break;
                }
                _pendingPhantomBenchWarpId = 0;
                _mapTravelProgressPending = false;
                _mapTravelProgressScene = string.Empty;
                RoundRoomExitBlocker.SetPhantomRoomLocked(true);
                ApplyBackgroundVisibilityToActiveScene();
                UpdateRemotePlayersVisibility();
                MapTravelProgressController.ApplyCurrentScene();
                SceneTravelCatalog.ObserveCurrentScene();
                SendCatalogRequest(false, 0);
                completed = true;
            }
            finally
            {
                if (!completed)
                {
                    _pendingPhantomBenchWarpId = 0;
                    _mapTravelProgressPending = false;
                    _mapTravelProgressScene = string.Empty;
                    if (_hasForcedNextMapId && _forcedNextMapId == mapId)
                    {
                        _hasForcedNextMapId = false;
                        _forcedNextMapId = 0;
                        _forcedNextScene = string.Empty;
                    }
                    if (_activePhantomMapId == mapId)
                    {
                        _activePhantomMapId = 0;
                        MantisVillageProgressController.RestoreOriginalProgress();
                        RestoreAllBackgroundRenderers();
                        RoundRoomExitBlocker.SetPhantomRoomLocked(false);
                    }
                }
                _sceneTransitionRunning = false;
            }
        }
        private IEnumerator WarpToPhantomBenchAfterLoad(int mapId)
        {
            if (_phantomBenchWarpRunning) yield break;
            _phantomBenchWarpRunning = true;
            try
            {
                float deadline = Time.unscaledTime + 20f;
                while (Time.unscaledTime < deadline)
                {
                    if (_activePhantomMapId != mapId || _pendingPhantomBenchWarpId != mapId ||
                    !string.Equals(SafeCurrentScene(), "Fungus2_31", StringComparison.OrdinalIgnoreCase))
                        yield break;
                    if (HeroController.instance != null && PlayerData.instance != null &&
                    GameManager.instance != null && !GameManager.instance.IsInSceneTransition)
                    {
                        BenchLocation target = new BenchLocation("Fungus2_31", "RestBench", (int)MapZone.WASTES, "Mantis Village", mapId);
                        if (EmergencyBenchWarpController.TryWarpToBench(target, true, out _))
                        {
                            float warpDeadline = Time.unscaledTime + 40f;
                            while (EmergencyBenchWarpController.IsWarpRunning && Time.unscaledTime < warpDeadline)
                                yield return null;
                            if (!EmergencyBenchWarpController.IsWarpRunning && _pendingPhantomBenchWarpId == mapId &&
                            EmergencyBenchWarpController.IsHeroNearBench(HeroController.instance, target))
                            {
                                _pendingPhantomBenchWarpId = 0;
                            }
                            yield break;
                        }
                    }
                    yield return new WaitForSecondsRealtime(0.25f);
                }
            }
            finally
            {
                _phantomBenchWarpRunning = false;
            }
        }
        private void OnBeforePlayerDead()
        {
            if (_activePhantomMapId > 0 && MantisVillageScenes.Contains(SafeCurrentScene()))
                _pendingPhantomDeathBenchWarpId = _activePhantomMapId;
        }
        private void OnAfterPlayerDead()
        {
            int mapId = _pendingPhantomDeathBenchWarpId > 0 ? _pendingPhantomDeathBenchWarpId : _activePhantomMapId;
            if (mapId <= 0) return;
            _pendingPhantomDeathBenchWarpId = mapId;
            _phantomDeathReturnMapId = mapId;
            PlayerData data = PlayerData.instance;
            if (data != null)
            {
                data.respawnScene = "Fungus2_31";
                data.respawnMarkerName = "RestBench";
                data.respawnType = 1;
                data.mapZone = MapZone.WASTES;
                data.atBench = false;
                try { GameManager.instance?.SetPlayerDataBool(nameof(PlayerData.atBench), false); } catch { }
            }
            if (!_phantomDeathBenchWarpRunning)
                StartCoroutine(ReturnToPhantomBenchAfterDeath(mapId));
        }
        public static void ClearPendingPhantomDeathReturn()
        {
            if (_instance != null)
            {
                _instance._pendingPhantomDeathBenchWarpId = 0;
                _instance._phantomDeathReturnMapId = 0;
            }
        }

        public static void CancelPendingAutomaticBenchWarps()
        {
            PhantomMapClientController controller = _instance;
            if (controller == null)
            {
                return;
            }

            controller._pendingPhantomBenchWarpId = 0;
            controller._pendingPhantomDeathBenchWarpId = 0;
            controller._phantomDeathReturnMapId = 0;
            controller._pendingMapTravelCommand = null;
        }
        private IEnumerator ReturnToPhantomBenchAfterDeath(int mapId)
        {
            if (_phantomDeathBenchWarpRunning) yield break;
            _phantomDeathBenchWarpRunning = true;
            try
            {
                float readyDeadline = Time.realtimeSinceStartup + 45f;
                while (Time.realtimeSinceStartup < readyDeadline)
                {
                    if (_phantomDeathReturnMapId != mapId || RoundPreparation.IsReturnToBenchPending)
                        yield break;
                    HeroController hero = HeroController.instance;
                    GameManager manager = GameManager.instance;
                    if (!RoundPreparation.IsGameplayTransitionPending && hero != null && PlayerData.instance != null &&
                    manager != null && !manager.IsInSceneTransition && (hero.cState == null || !hero.cState.dead))
                        break;
                    yield return null;
                }
                HeroController readyHero = HeroController.instance;
                GameManager readyManager = GameManager.instance;
                if (Time.realtimeSinceStartup >= readyDeadline || readyHero == null || PlayerData.instance == null ||
                readyManager == null || readyManager.IsInSceneTransition || (readyHero.cState != null && readyHero.cState.dead) ||
                RoundPreparation.IsGameplayTransitionPending || RoundPreparation.IsReturnToBenchPending ||
                _phantomDeathReturnMapId != mapId)
                    yield break;
                if (MantisVillageScenes.Contains(SafeCurrentScene()) && _activePhantomMapId != mapId &&
                !ChangeCurrentInstance(mapId, false))
                    yield break;
                float existingWarpDeadline = Time.realtimeSinceStartup + 45f;
                while (EmergencyBenchWarpController.IsWarpRunning && Time.realtimeSinceStartup < existingWarpDeadline)
                    yield return null;
                if (EmergencyBenchWarpController.IsWarpRunning || _phantomDeathReturnMapId != mapId)
                    yield break;
                BenchLocation target = new BenchLocation("Fungus2_31", "RestBench", (int)MapZone.WASTES, "Mantis Village", mapId);
                if (!EmergencyBenchWarpController.TryWarpToBench(target, true, out _))
                    yield break;
                float warpDeadline = Time.realtimeSinceStartup + 45f;
                while (EmergencyBenchWarpController.IsWarpRunning && Time.realtimeSinceStartup < warpDeadline)
                    yield return null;
                if (!EmergencyBenchWarpController.IsWarpRunning && _phantomDeathReturnMapId == mapId &&
                EmergencyBenchWarpController.IsHeroNearBench(HeroController.instance, target))
                {
                    _pendingPhantomDeathBenchWarpId = 0;
                    _phantomDeathReturnMapId = 0;
                }
            }
            finally
            {
                _phantomDeathBenchWarpRunning = false;
            }
        }
        private void OnActiveSceneChanged(Scene previous, Scene current)
        {
            string currentSceneName = current.IsValid() ? current.name : string.Empty;
            if (!string.Equals(previous.name, currentSceneName, StringComparison.OrdinalIgnoreCase))
            {
                _remotePlayerMaps.Clear();
            }

            string currentLogicalScene = GetLogicalSceneName(currentSceneName);
            if (current.IsValid()) ApplyForcedMapForScene(currentLogicalScene);
            bool currentIsMantisRoom = current.IsValid() && MantisVillageScenes.Contains(currentLogicalScene);
            if (!currentIsMantisRoom)
            {
                SetAllRemotePlayersVisible(true);
                RestoreAllBackgroundRenderers();
                MantisVillageProgressController.RestoreOriginalProgress();
                if (!EmergencyBenchWarpController.IsWarpRunning || _activePhantomMapId <= 0)
                {
                    _activePhantomMapId = 0;
                    _pendingPhantomBenchWarpId = 0;
                    if (_phantomDeathReturnMapId <= 0)
                    {
                        _pendingPhantomDeathBenchWarpId = 0;
                    }
                }
            }
            RoundRoomExitBlocker.SetPhantomRoomLocked(
                _activePhantomMapId > 0 && MantisVillageScenes.Contains(SafeCurrentScene()));
            UpdateRemotePlayersVisibility();
            SceneTravelCatalog.ObserveCurrentScene();
            StartCoroutine(DiscoverSceneAfterLoad());
        }
        private IEnumerator DiscoverSceneAfterLoad()
        {
            yield return null;
            yield return new WaitForEndOfFrame();
            SceneTravelCatalog.ObserveCurrentScene();
            SendCatalogRequest(false, 0);
        }
        private bool ChangeCurrentInstance(int mapId, bool requestUpdatedCatalog = true)
        {
            Scene scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (mapId < 0 || !scene.IsValid() || !MantisVillageScenes.Contains(scene.name))
                return false;
            if (mapId == 0)
            {
                _activePhantomMapId = 0;
                RestoreAllBackgroundRenderers();
                MantisVillageProgressController.RestoreOriginalProgress();
            }
            else
            {
                MantisVillageProgressController.CaptureOriginalProgress();
                _activePhantomMapId = mapId;
                ApplyBackgroundVisibilityToActiveScene();
                MapTravelProgressController.ApplyCurrentScene();
            }
            RoundRoomExitBlocker.SetPhantomRoomLocked(
                _activePhantomMapId > 0 && MantisVillageScenes.Contains(SafeCurrentScene()));
            UpdateRemotePlayersVisibility();
            SceneTravelCatalog.ObserveCurrentScene();
            if (requestUpdatedCatalog) SendCatalogRequest(false, 0);
            return true;
        }
        private void UpdateRemotePlayersVisibility()
        {
            LastAttackerTracker.FilterSpawnedAttacks();
            IClientManager clientManager = _clientApi == null ? null : _clientApi.ClientManager;
            if (clientManager == null)
            {
                return;
            }

            IReadOnlyCollection<IClientPlayer> players = clientManager.Players;
            if (players == null)
            {
                return;
            }

            bool inMantis = MantisVillageScenes.Contains(SafeCurrentScene());
            foreach (IClientPlayer player in players)
            {
                if (player == null || !player.IsInLocalScene)
                {
                    continue;
                }

                try
                {
                    GameObject container = player.PlayerContainer;
                    if (container == null)
                    {
                        continue;
                    }

                    bool shouldBeVisible = !inMantis || IsPlayerInSamePhantomMap(player.Id);
                    SetPlayerContainerVisible(container, shouldBeVisible);
                }
                catch (MissingReferenceException exception)
                {
                    PhantomMapDiagnostics.Record(
                        "client.remote-player.visibility.missing-reference",
                        exception.Message);
                }
                catch (NullReferenceException exception)
                {
                    PhantomMapDiagnostics.Record(
                        "client.remote-player.visibility.null-reference",
                        exception.Message);
                }
            }
        }
        private static void SetPlayerContainerVisible(GameObject container, bool visible)
        {
            if (container == null || container.activeSelf == visible) return;
            if (!visible)
            {
                ParticleSystem[] particles = container.GetComponentsInChildren<ParticleSystem>(true);
                for (int i = 0; i < particles.Length; i++)
                {
                    if (particles[i] != null)
                    {
                        particles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    }
                }
            }
            container.SetActive(visible);
        }
        private void SetAllRemotePlayersVisible(bool visible)
        {
            IReadOnlyCollection<IClientPlayer> players = _clientApi?.ClientManager?.Players;
            if (players == null)
            {
                return;
            }

            foreach (IClientPlayer player in players)
            {
                if (player == null)
                {
                    continue;
                }

                try
                {
                    GameObject container = player.PlayerContainer;
                    if (container != null)
                    {
                        SetPlayerContainerVisible(container, visible);
                    }
                }
                catch (MissingReferenceException exception)
                {
                    PhantomMapDiagnostics.Record(
                        "client.remote-player.visibility.missing-reference",
                        exception.Message);
                }
                catch (NullReferenceException exception)
                {
                    PhantomMapDiagnostics.Record(
                        "client.remote-player.visibility.null-reference",
                        exception.Message);
                }
            }
        }
        private void LateUpdate()
        {
            if (!MantisVillageScenes.Contains(SafeCurrentScene()) ||
                Time.unscaledTime < _nextPlayerVisibilityCheck)
            {
                return;
            }

            _nextPlayerVisibilityCheck = Time.unscaledTime + PlayerVisibilityRefreshInterval;
            UpdateRemotePlayersVisibility();
        }
        private void ApplyBackgroundVisibilityToActiveScene()
        {
            if (_activePhantomMapId <= 0 || !MantisVillageScenes.Contains(SafeCurrentScene()) ||
            HKMPRounds.GlobalSettings == null || !HKMPRounds.GlobalSettings.HidePhantomMantisBackground)
            {
                RestoreAllBackgroundRenderers();
                return;
            }
            Scene activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || !activeScene.isLoaded) return;
            GameObject[] roots = activeScene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                GameObject root = roots[i];
                if (root == null || IsNetworkPlayerRoot(root) || root.GetComponent<HeroController>() != null)
                    continue;
                Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
                for (int j = 0; j < renderers.Length; j++)
                {
                    Renderer r = renderers[j];
                    if (r != null && IsPhantomBackgroundRenderer(r, root.transform))
                    {
                        if (!_backgroundRendererStates.ContainsKey(r))
                            _backgroundRendererStates.Add(r, r.enabled);
                        r.enabled = false;
                    }
                }
            }
        }
        private void RestoreAllBackgroundRenderers()
        {
            foreach (KeyValuePair<Renderer, bool> state in _backgroundRendererStates.ToArray())
            {
                if (state.Key != null) state.Key.enabled = state.Value;
            }
            _backgroundRendererStates.Clear();
        }
        private static bool IsPhantomBackgroundRenderer(Renderer renderer, Transform root)
        {
            if (renderer.transform.position.z > 0.5f) return true;
            string layer = renderer.sortingLayerName ?? string.Empty;
            if (layer.IndexOf("background", StringComparison.OrdinalIgnoreCase) >= 0 ||
            layer.IndexOf("parallax", StringComparison.OrdinalIgnoreCase) >= 0 ||
            string.Equals(layer, "bg", StringComparison.OrdinalIgnoreCase) ||
            layer.StartsWith("bg_", StringComparison.OrdinalIgnoreCase) ||
            layer.EndsWith("_bg", StringComparison.OrdinalIgnoreCase))
                return true;
            if (renderer is SpriteRenderer sr && sr.sortingOrder <= -100)
                return true;
            Transform current = renderer.transform;
            while (current != null)
            {
                if (ContainsBackgroundToken(current.name)) return true;
                if (current == root) break;
                current = current.parent;
            }
            return false;
        }
        private static bool ContainsBackgroundToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            return value.IndexOf("background", StringComparison.OrdinalIgnoreCase) >= 0 ||
            value.IndexOf("backdrop", StringComparison.OrdinalIgnoreCase) >= 0 ||
            value.IndexOf("parallax", StringComparison.OrdinalIgnoreCase) >= 0 ||
            value.IndexOf("darkness", StringComparison.OrdinalIgnoreCase) >= 0 ||
            value.IndexOf("dark room", StringComparison.OrdinalIgnoreCase) >= 0 ||
            value.IndexOf("blurplane", StringComparison.OrdinalIgnoreCase) >= 0 ||
            string.Equals(value, "bg", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("bg_", StringComparison.OrdinalIgnoreCase) ||
            value.EndsWith("_bg", StringComparison.OrdinalIgnoreCase);
        }
        private static bool IsNetworkPlayerRoot(GameObject root)
        {
            if (root == null) return false;
            string name = root.name ?? string.Empty;
            return name.IndexOf("Player Container", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("HKMP Player", StringComparison.OrdinalIgnoreCase) >= 0;
        }
        internal static string GetCurrentLogicalScene() => SafeCurrentScene();
        internal static string GetLogicalSceneName(string sceneName)
        {
            return sceneName ?? string.Empty;
        }
        private static string SafeCurrentScene()
        {
            try
            {
                Scene active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                if (active.IsValid() && active.isLoaded && !string.IsNullOrWhiteSpace(active.name))
                    return GetLogicalSceneName(active.name);
                return GameManager.instance != null ? GetLogicalSceneName(GameManager.instance.sceneName) : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
        private void OnDestroy()
        {
            _sceneTransitionScope?.Dispose();
            _sceneTransitionScope = null;
            _sceneTransitionRunning = false;
            UnityEngine.SceneManagement.SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            ModHooks.BeforeSceneLoadHook -= OnBeforeSceneLoad;
            ModHooks.BeforePlayerDeadHook -= OnBeforePlayerDead;
            On.GameManager.OnNextLevelReady -= OnNextLevelReady;
            ModHooks.AfterPlayerDeadHook -= OnAfterPlayerDead;
            if (_network != null)
            {
                _network.MapTravelCommandReceived -= OnMapTravelCommandReceived;
                _network.MapTravelStateReceived -= OnMapTravelStateReceived;
            }
            if (_clientApi?.ClientManager != null)
            {
                _clientApi.ClientManager.ConnectEvent -= OnConnected;
                _clientApi.ClientManager.DisconnectEvent -= OnDisconnected;
                _clientApi.ClientManager.PlayerConnectEvent -= OnPlayerEnterScene;
                _clientApi.ClientManager.PlayerDisconnectEvent -= OnPlayerEnterScene;
                _clientApi.ClientManager.PlayerEnterSceneEvent -= OnPlayerEnterScene;
                _clientApi.ClientManager.PlayerLeaveSceneEvent -= OnPlayerEnterScene;
            }
            SetAllRemotePlayersVisible(true);
            RestoreAllBackgroundRenderers();
            MantisVillageProgressController.RestoreOriginalProgress();
            RoundRoomExitBlocker.SetPhantomRoomLocked(false);
            if (_instance == this) _instance = null;
        }
    }
}