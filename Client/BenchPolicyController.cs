using System;
using System.Collections.Generic;
using System.Linq;
using Hkmp.Api.Client;
using HKMP.Rounds.Networking.Packets;
using HKMP.Rounds.Settings;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace HKMP.Rounds.Client
{
    internal sealed class BenchPolicyController : MonoBehaviour
    {
        private static BenchPolicyController _instance;

        private IClientApi _clientApi;
        private ClientNetManager _network;
        private bool _initialized;
        private bool _clientConnected;
        private bool _stateRequestPending;
        private float _nextDiscoveryAt;
        private string _lastSentCatalogJson = string.Empty;
        private string _lastHostSatBenchKey = string.Empty;
        private bool _discoveryPending;

        private static bool _canManageSharedBench;
        private static string _sharedBenchScene = string.Empty;
        private static string _sharedBenchMarker = string.Empty;
        private static int _sharedBenchMapZone;
        private static int _sharedBenchPhantomMapId;
        private static bool _hasSharedBench;
        private static bool _sharedBenchLocked;
        private static bool _hasServerState;

        public static string SharedBenchScene { get { return _sharedBenchScene; } }
        public static string SharedBenchMarker { get { return _sharedBenchMarker; } }
        public static int SharedBenchMapZone { get { return _sharedBenchMapZone; } }
        public static int SharedBenchPhantomMapId { get { return _sharedBenchPhantomMapId; } }
        public static bool HasSharedBench { get { return _hasSharedBench; } }
        public static bool SharedBenchLocked { get { return _sharedBenchLocked; } }
        public static bool IsConnected
        {
            get
            {
                BenchPolicyController instance = _instance;
                return instance != null &&
                       (instance._clientConnected ||
                        (instance._clientApi != null &&
                         instance._clientApi.NetClient != null &&
                         instance._clientApi.NetClient.IsConnected));
            }
        }

        public static void Initialize(IClientApi clientApi, ClientNetManager network)
        {
            if (_instance == null)
            {
                GameObject controllerObject = new GameObject("[HKMP.Rounds] Bench Discovery");
                Object.DontDestroyOnLoad(controllerObject);
                _instance = controllerObject.AddComponent<BenchPolicyController>();
            }

            _instance.Configure(clientApi, network);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            UnityEngine.SceneManagement.SceneManager.activeSceneChanged += OnActiveSceneChanged;
            Modding.ModHooks.BeforePlayerDeadHook += OnBeforePlayerDead;
        }

        private void Configure(IClientApi clientApi, ClientNetManager network)
        {
            if (_network != null)
            {
                _network.BenchSettingsReceived -= OnBenchSettingsReceived;
            }

            if (_clientApi != null && _clientApi.ClientManager != null)
            {
                _clientApi.ClientManager.ConnectEvent -= OnClientConnected;
                _clientApi.ClientManager.DisconnectEvent -= OnClientDisconnected;
            }

            _clientApi = clientApi;
            _network = network;
            if (_network != null)
            {
                _network.BenchSettingsReceived -= OnBenchSettingsReceived;
                _network.BenchSettingsReceived += OnBenchSettingsReceived;
            }

            if (_clientApi != null && _clientApi.ClientManager != null)
            {
                _clientApi.ClientManager.ConnectEvent += OnClientConnected;
                _clientApi.ClientManager.DisconnectEvent += OnClientDisconnected;
            }

            _clientConnected = _clientApi != null &&
                               _clientApi.NetClient != null &&
                               _clientApi.NetClient.IsConnected;
            _stateRequestPending = _clientConnected;
            _initialized = true;
            _lastSentCatalogJson = string.Empty;
            _lastHostSatBenchKey = string.Empty;
            _hasServerState = false;
            _discoveryPending = _clientConnected;
            _nextDiscoveryAt = Time.unscaledTime + (_clientConnected ? 0.75f : 1f);
        }

        private void Update()
        {
            CaptureCurrentBenchIfResting();

            if (!_initialized || !_clientConnected || _network == null)
            {
                return;
            }

            if (_stateRequestPending && Time.unscaledTime >= _nextDiscoveryAt)
            {
                _stateRequestPending = false;
                _nextDiscoveryAt = Time.unscaledTime + 2f;
                _lastSentCatalogJson = string.Empty;
                _network.RequestBenchSettings("[]");
                return;
            }

            if (_discoveryPending && Time.unscaledTime >= _nextDiscoveryAt)
            {
                _discoveryPending = false;
                SendDiscoveryIfChanged();
            }
        }

        private void CaptureCurrentBenchIfResting()
        {
            PlayerData data = PlayerData.instance;
            if (data == null || !data.atBench || data.respawnType != 1 ||
                string.IsNullOrWhiteSpace(data.respawnScene) ||
                string.IsNullOrWhiteSpace(data.respawnMarkerName))
            {
                return;
            }

            int phantomMapId = PhantomMapClientController.GetPhantomMapIdForScene(data.respawnScene);
            ClientPreferencesStore.RememberLastRealBench(
                data.respawnScene,
                data.respawnMarkerName,
                (int)data.mapZone,
                phantomMapId);

            if (_clientConnected && _canManageSharedBench && _network != null)
            {
                BenchLocation location = new BenchLocation(
                    data.respawnScene,
                    data.respawnMarkerName,
                    (int)data.mapZone,
                    string.Empty,
                    phantomMapId);
                bool hostBenchChanged = !string.Equals(
                    _lastHostSatBenchKey,
                    location.Key,
                    StringComparison.OrdinalIgnoreCase);
                if (location.IsValid && hostBenchChanged)
                {
                    _lastHostSatBenchKey = location.Key;
                    _network.ReportHostBenchSat(location);
                }
            }
        }

        private void OnBeforePlayerDead()
        {
            PlayerData data = PlayerData.instance;
            if (data == null)
            {
                return;
            }

            if (data.atBench && data.respawnType == 1 &&
                !string.IsNullOrWhiteSpace(data.respawnScene) &&
                !string.IsNullOrWhiteSpace(data.respawnMarkerName))
            {
                ClientPreferencesStore.RememberLastRealBench(
                    data.respawnScene,
                    data.respawnMarkerName,
                    (int)data.mapZone,
                    PhantomMapClientController.GetPhantomMapIdForScene(data.respawnScene));
            }

            RestoreLastRealRespawn(false);
        }





        private void OnActiveSceneChanged(Scene previous, Scene current)
        {
            _lastSentCatalogJson = string.Empty;
            _discoveryPending = _clientConnected;
            _nextDiscoveryAt = Time.unscaledTime + 0.75f;
            if (_clientConnected)
            {
                _stateRequestPending = true;
            }
        }

        private void OnClientConnected()
        {
            _clientConnected = true;
            _stateRequestPending = true;
            _lastSentCatalogJson = string.Empty;
            _lastHostSatBenchKey = string.Empty;
            _hasServerState = false;
            _discoveryPending = true;
            _nextDiscoveryAt = Time.unscaledTime + 0.75f;
        }

        private void OnClientDisconnected()
        {
            _clientConnected = false;
            _stateRequestPending = false;
            _discoveryPending = false;
            _lastSentCatalogJson = string.Empty;
            _lastHostSatBenchKey = string.Empty;
            ClearServerState();
        }

        private void OnBenchSettingsReceived(BenchSettingsPacket packet)
        {
            ApplyServerState(packet);
        }

        public static void ClearServerState()
        {
            _hasServerState = false;
            _canManageSharedBench = false;
            _hasSharedBench = false;
            _sharedBenchScene = string.Empty;
            _sharedBenchMarker = string.Empty;
            _sharedBenchMapZone = 0;
            _sharedBenchPhantomMapId = 0;
            _sharedBenchLocked = false;
        }

        public static void ApplyServerState(BenchSettingsPacket packet)
        {
            if (packet == null)
            {
                return;
            }

            bool receivedSharedBench = packet.HasSharedBench &&
                !string.IsNullOrWhiteSpace(packet.SceneName) &&
                !string.IsNullOrWhiteSpace(packet.MarkerName);
            _hasServerState = true;
            _canManageSharedBench = packet.CanManageBench;
            _hasSharedBench = receivedSharedBench;
            _sharedBenchLocked = packet.SharedBenchLocked;
            _sharedBenchScene = _hasSharedBench ? packet.SceneName.Trim() : string.Empty;
            _sharedBenchMarker = _hasSharedBench ? packet.MarkerName.Trim() : string.Empty;
            _sharedBenchMapZone = _hasSharedBench ? packet.MapZone : 0;
            _sharedBenchPhantomMapId = _hasSharedBench ? Math.Max(0, packet.PhantomMapId) : 0;
        }





        public static bool IsServerSharedBench(string sceneName, string markerName)
        {
            return IsServerSharedBench(
                sceneName,
                markerName,
                PhantomMapClientController.GetPhantomMapIdForScene(sceneName));
        }

        public static bool IsServerSharedBench(string sceneName, string markerName, int phantomMapId)
        {
            return _hasSharedBench &&
                   string.Equals(_sharedBenchScene, sceneName, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(_sharedBenchMarker, markerName, StringComparison.OrdinalIgnoreCase) &&
                   _sharedBenchPhantomMapId == Math.Max(0, phantomMapId);
        }



        public static bool CanUseBenchLocation(BenchLocation location)
        {
            return location != null && location.IsValid;
        }

        public static bool TryResolveWarpTarget(
            out BenchLocation target,
            out bool exactSelection,
            out string message)
        {
            target = null;
            exactSelection = false;
            message = string.Empty;

            if (IsConnected && !_hasServerState)
            {
                exactSelection = true;
                message = "Waiting for authoritative bench settings from the host.";
                return false;
            }

            if (_sharedBenchLocked)
            {
                exactSelection = true;
                if (!_hasSharedBench)
                {
                    message = "The host-locked shared bench is not available on this client.";
                    return false;
                }
                target = CreateSharedBenchLocation();
                return ValidateWarpTarget(target, out message);
            }

            if (ClientPreferencesStore.PreferSharedBench && _hasSharedBench)
            {
                target = CreateSharedBenchLocation();
                exactSelection = true;
                return ValidateWarpTarget(target, out message);
            }

            BenchLocation personal;
            if (ClientPreferencesStore.TryGetPersonalBench(out personal))
            {
                target = personal;
                exactSelection = true;
                return ValidateWarpTarget(target, out message);
            }

            if (_hasSharedBench)
            {
                target = CreateSharedBenchLocation();
                exactSelection = true;
                return ValidateWarpTarget(target, out message);
            }

            string sceneName;
            string markerName;
            int mapZone;
            int phantomMapId;
            if (ClientPreferencesStore.TryGetLastRealBench(out sceneName, out markerName, out mapZone, out phantomMapId))
            {
                target = new BenchLocation(sceneName, markerName, mapZone, "Last real bench", phantomMapId);
                return ValidateWarpTarget(target, out message);
            }

            PlayerData data = PlayerData.instance;
            if (data != null && data.respawnType == 1 &&
                !string.IsNullOrWhiteSpace(data.respawnScene) &&
                !string.IsNullOrWhiteSpace(data.respawnMarkerName))
            {
                target = new BenchLocation(
                    data.respawnScene,
                    data.respawnMarkerName,
                    (int)data.mapZone,
                    "Current respawn bench",
                    PhantomMapClientController.GetPhantomMapIdForScene(data.respawnScene));
                return ValidateWarpTarget(target, out message);
            }

            message = "No personal, shared, or saved vanilla bench has been selected.";
            return false;
        }

        private static BenchLocation CreateSharedBenchLocation()
        {
            return new BenchLocation(
                _sharedBenchScene,
                _sharedBenchMarker,
                _sharedBenchMapZone,
                "Host-selected shared bench",
                _sharedBenchPhantomMapId);
        }

        private static bool ValidateWarpTarget(BenchLocation target, out string message)
        {
            message = string.Empty;
            if (target == null || !target.IsValid || !CanUseBenchLocation(target))
            {
                message = "The selected vanilla bench is not available on this client.";
                return false;
            }
            return true;
        }

        public static void RestoreLastRealRespawn(bool captureCurrent = true)
        {
            PlayerData data = PlayerData.instance;
            if (data == null)
            {
                return;
            }

            bool currentRespawnIsValid = data.respawnType == 1 &&
                !string.IsNullOrWhiteSpace(data.respawnScene) &&
                !string.IsNullOrWhiteSpace(data.respawnMarkerName);

            string sceneName;
            string markerName;
            int mapZone;
            int phantomMapId;
            bool hasSavedLastRealBench = ClientPreferencesStore.TryGetLastRealBench(
                out sceneName,
                out markerName,
                out mapZone,
                out phantomMapId);

            if (captureCurrent && currentRespawnIsValid && (data.atBench || !hasSavedLastRealBench))
            {
                ClientPreferencesStore.RememberLastRealBench(
                    data.respawnScene,
                    data.respawnMarkerName,
                    (int)data.mapZone,
                    PhantomMapClientController.GetPhantomMapIdForScene(data.respawnScene));
                return;
            }

            if (!hasSavedLastRealBench && currentRespawnIsValid)
            {
                sceneName = data.respawnScene;
                markerName = data.respawnMarkerName;
                mapZone = (int)data.mapZone;
                phantomMapId = PhantomMapClientController.GetPhantomMapIdForScene(data.respawnScene);
                hasSavedLastRealBench = true;
                ClientPreferencesStore.RememberLastRealBench(sceneName, markerName, mapZone, phantomMapId);
            }

            if (!hasSavedLastRealBench)
            {
                BenchLocation fallback = ResolveBuiltInFallbackBench(data.respawnScene, (int)data.mapZone);
                if (fallback == null || !fallback.IsValid)
                {
                    data.respawnType = 0;
                    data.respawnMarkerName = string.Empty;
                    data.atBench = false;
                    return;
                }

                sceneName = fallback.SceneName;
                markerName = fallback.MarkerName;
                mapZone = fallback.MapZone;
                phantomMapId = fallback.PhantomMapId;
                ClientPreferencesStore.RememberLastRealBench(sceneName, markerName, mapZone, phantomMapId);
            }

            if (string.IsNullOrWhiteSpace(sceneName) || string.IsNullOrWhiteSpace(markerName))
            {
                return;
            }

            data.respawnScene = sceneName;
            data.respawnMarkerName = markerName;
            data.respawnType = 1;
            data.mapZone = (GlobalEnums.MapZone)mapZone;
            data.atBench = false;
            GameManager manager = GameManager.instance;
            if (manager != null)
            {
                manager.SetPlayerDataBool(nameof(PlayerData.atBench), false);
            }
        }

        private static BenchLocation ResolveBuiltInFallbackBench(string preferredScene, int preferredMapZone)
        {
            List<BenchLocation> benches = BenchCatalog.GetBuiltInCatalog()
                .Where(location => location != null && location.IsValid)
                .ToList();

            if (!string.IsNullOrWhiteSpace(preferredScene))
            {
                BenchLocation sameScene = benches.FirstOrDefault(location =>
                    string.Equals(location.SceneName, preferredScene, StringComparison.OrdinalIgnoreCase));
                if (sameScene != null)
                {
                    return sameScene;
                }
            }

            BenchLocation sameZone = benches.FirstOrDefault(location => location.MapZone == preferredMapZone);
            return sameZone ?? benches.FirstOrDefault();
        }

        private void SendDiscoveryIfChanged()
        {
            if (!_clientConnected || _network == null)
            {
                return;
            }

            string json = BenchCatalogScanner.ScanCurrentSceneJson();
            if (string.IsNullOrWhiteSpace(json))
            {
                json = "[]";
            }

            if (string.Equals(json, _lastSentCatalogJson, StringComparison.Ordinal))
            {
                return;
            }

            _lastSentCatalogJson = json;
            _network.DiscoverBenches(json);
        }

        private void OnDestroy()
        {
            if (_network != null)
            {
                _network.BenchSettingsReceived -= OnBenchSettingsReceived;
            }

            if (_clientApi != null && _clientApi.ClientManager != null)
            {
                _clientApi.ClientManager.ConnectEvent -= OnClientConnected;
                _clientApi.ClientManager.DisconnectEvent -= OnClientDisconnected;
            }

            UnityEngine.SceneManagement.SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            Modding.ModHooks.BeforePlayerDeadHook -= OnBeforePlayerDead;
            if (_instance == this)
            {
                _instance = null;
            }
        }
    }
}
