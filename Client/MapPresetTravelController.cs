using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HKMP.Rounds.Networking.Packets;
using HKMP.Rounds.Settings;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace HKMP.Rounds.Client
{
    internal sealed class MapPresetTravelController : MonoBehaviour
    {
        private const float SceneTransitionTimeout = 35f;
        private const float BenchLookupTimeout = 8f;
        private const float BenchLookupPollInterval = 0.2f;
        private const float HeroReadyTimeout = 10f;

        private static MapPresetTravelController _instance;
        private ClientNetManager _network;
        private bool _initialized;
        private bool _travelInProgress;

        public void Initialize(ClientNetManager network)
        {
            _instance = this;
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            _network = network;
            if (_network != null)
            {
                _network.MapPresetCommandReceived -= OnMapPresetCommandReceived;
                _network.MapPresetCommandReceived += OnMapPresetCommandReceived;
            }
        }

        public static bool TryTravelLocally(int presetMapId, out string message)
        {
            message = string.Empty;
            MapPresetTravelController controller = _instance;
            if (controller == null)
            {
                message = "The local map travel controller is not ready.";
                return false;
            }

            if (controller._travelInProgress || EmergencyBenchWarpController.IsWarpRunning)
            {
                message = "A map or bench travel is already in progress.";
                return false;
            }

            if (GameManager.instance == null || HeroController.instance == null || PlayerData.instance == null)
            {
                message = "The game world is not ready for local travel.";
                return false;
            }

            MapPresetDefinition definition;
            if (!MapPresetCatalog.TryGetAvailableMap(presetMapId, out definition) || definition == null)
            {
                message = "This built-in map preset is unavailable.";
                return false;
            }

            if (definition.TravelKind == MapPresetTravelKind.BuiltInBench)
            {
                BenchLocation target = new BenchLocation(
                    definition.SceneName,
                    definition.BenchMarker,
                    definition.MapZone,
                    definition.DisplayName);
                if (!EmergencyBenchWarpController.TryWarpToBench(target, definition.EntryGate, true, out message))
                {
                    return false;
                }

                controller.StartCoroutine(controller.ApplyProgressAfterBenchWarp());
                return true;
            }

            controller.StartCoroutine(controller.TravelToRoomWithBench(definition));
            message = "Local travel started.";
            return true;
        }

        private void OnMapPresetCommandReceived(MapPresetCommandPacket packet)
        {
            if (packet == null || packet.PresetMapId <= 0 || _travelInProgress)
            {
                return;
            }

            MapPresetDefinition definition;
            if (!MapPresetCatalog.TryGetAvailableMap(packet.PresetMapId, out definition) || definition == null)
            {
                Debug.LogWarning("[HKMP.Rounds] Ignored a preset map command for an unavailable built-in map.");
                return;
            }

            if (definition.TravelKind == MapPresetTravelKind.BuiltInBench)
            {
                TravelToBuiltInBench(definition);
                return;
            }

            StartCoroutine(TravelToRoomWithBench(definition));
        }

        private void TravelToBuiltInBench(MapPresetDefinition definition)
        {
            BenchLocation location = new BenchLocation(
                definition.SceneName,
                definition.BenchMarker,
                definition.MapZone,
                definition.DisplayName);

            string message;
            if (!EmergencyBenchWarpController.TryWarpToBench(location, definition.EntryGate, true, out message))
            {
                Debug.LogWarning(
                    "[HKMP.Rounds] Could not travel to preset bench '" +
                    definition.DisplayName + "': " + message);
                return;
            }

            StartCoroutine(ApplyProgressAfterBenchWarp());
        }

        private IEnumerator ApplyProgressAfterBenchWarp()
        {
            float deadline = Time.unscaledTime + SceneTransitionTimeout + BenchLookupTimeout;
            while (EmergencyBenchWarpController.IsWarpRunning && Time.unscaledTime < deadline)
            {
                yield return null;
            }

            yield return null;
            yield return new WaitForEndOfFrame();
            MapTravelProgressController.ApplyCurrentScene();
            SceneTravelCatalog.ObserveCurrentScene();
        }

        private IEnumerator TravelToRoomWithBench(MapPresetDefinition definition)
        {
            _travelInProgress = true;
            bool arrived = false;
            try
            {
                GameManager manager = GameManager.instance;
                if (manager == null || HeroController.instance == null || PlayerData.instance == null)
                {
                    Debug.LogWarning("[HKMP.Rounds] The game world is not ready for preset map travel.");
                    yield break;
                }

                string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                bool forceEntryTransition = definition.KeepEntryPointPositionIfBenchMissing;
                if (forceEntryTransition ||
                    !string.Equals(currentScene, definition.SceneName, StringComparison.OrdinalIgnoreCase))
                {
                    PrepareForGameplayTransition();
                    Scene sourceScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                    IDisposable transitionScope = RoundRoomExitBlocker.AcquireProgrammaticTransition();
                    try
                    {
                        string transitionMessage;
                        if (!BenchwarpIntegration.TryChangeToScene(
                            definition.SceneName,
                            string.IsNullOrWhiteSpace(definition.EntryGate) ? "door" : definition.EntryGate,
                            0f,
                            out transitionMessage))
                        {
                            Debug.LogError("[HKMP.Rounds] Benchwarp could not start preset map travel: " + transitionMessage);
                            yield break;
                        }

                        float deadline = Time.realtimeSinceStartup + SceneTransitionTimeout;
                        bool transitionObserved = false;
                        while (Time.realtimeSinceStartup < deadline)
                        {
                            manager = GameManager.instance;
                            Scene activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                            if (manager != null &&
                                (manager.IsInSceneTransition ||
                                 (sourceScene.IsValid() && activeScene.IsValid() && sourceScene.handle != activeScene.handle)))
                            {
                                transitionObserved = true;
                            }

                            if (transitionObserved && manager != null && !manager.IsInSceneTransition &&
                                activeScene.IsValid() &&
                                string.Equals(activeScene.name, definition.SceneName, StringComparison.OrdinalIgnoreCase))
                            {
                                arrived = true;
                                break;
                            }

                            yield return null;
                        }
                    }
                    finally
                    {
                        transitionScope.Dispose();
                    }
                }
                else
                {
                    arrived = true;
                }

                if (!arrived)
                {
                    Debug.LogWarning(
                        "[HKMP.Rounds] Timed out while travelling to preset map '" +
                        definition.DisplayName + "'.");
                    yield break;
                }

                float heroReadyDeadline = Time.unscaledTime + HeroReadyTimeout;
                while (Time.unscaledTime < heroReadyDeadline &&
                       (HeroController.instance == null || PlayerData.instance == null ||
                        GameManager.instance == null || GameManager.instance.IsInSceneTransition ||
                        !string.Equals(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                            definition.SceneName, StringComparison.OrdinalIgnoreCase)))
                {
                    yield return null;
                }

                if (HeroController.instance == null || PlayerData.instance == null ||
                    GameManager.instance == null || GameManager.instance.IsInSceneTransition ||
                    !string.Equals(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                        definition.SceneName, StringComparison.OrdinalIgnoreCase))
                {
                    Debug.LogWarning("[HKMP.Rounds] The destination scene did not finish initializing for preset map '" +
                        definition.DisplayName + "'.");
                    yield break;
                }

                yield return null;
                yield return new WaitForEndOfFrame();
                MapTravelProgressController.ApplyCurrentScene();
                SceneTravelCatalog.ObserveCurrentScene();

                if (definition.KeepEntryPointPositionIfBenchMissing)
                {
                    yield break;
                }

                BenchLocation target = null;
                float benchSearchDeadline = Time.unscaledTime + BenchLookupTimeout;
                while (Time.unscaledTime < benchSearchDeadline)
                {
                    target = FindTargetBench(definition);
                    Vector3 currentMarkerPosition;
                    if (target != null && BenchCatalogScanner.TryGetMarkerPosition(target, out currentMarkerPosition))
                    {
                        break;
                    }

                    target = null;
                    yield return new WaitForSecondsRealtime(BenchLookupPollInterval);
                }

                if (target == null)
                {
                    Debug.LogWarning(
                        "[HKMP.Rounds] No real bench marker was found in '" + definition.SceneName +
                        "'. The player remains at the configured entry gate.");
                    yield break;
                }

                Vector3 markerPosition;
                if (target != null && BenchCatalogScanner.TryGetMarkerPosition(target, out markerPosition))
                {
                    string warpMessage;
                    if (!EmergencyBenchWarpController.TryWarpToBench(target, true, out warpMessage))
                    {
                        Debug.LogWarning(
                            "[HKMP.Rounds] Could not finish travel to the room's bench: " + warpMessage);
                    }
                    else
                    {
                        float warpDeadline = Time.unscaledTime + SceneTransitionTimeout + BenchLookupTimeout;
                        while (EmergencyBenchWarpController.IsWarpRunning && Time.unscaledTime < warpDeadline)
                        {
                            yield return null;
                        }

                        if (EmergencyBenchWarpController.IsWarpRunning)
                        {
                            Debug.LogError("[HKMP.Rounds] Timed out while positioning the player at the destination bench.");
                        }
                        else
                        {
                            HeroController warpedHero = HeroController.instance;
                            Vector3 finalMarkerPosition;
                            bool reachedBench = warpedHero != null &&
                                (warpedHero.cState == null || !warpedHero.cState.dead) &&
                                BenchCatalogScanner.TryGetMarkerPosition(target, out finalMarkerPosition) &&
                                EmergencyBenchWarpController.IsHeroNearBench(warpedHero, target);
                            if (!reachedBench)
                            {
                                Debug.LogError(
                                    "[HKMP.Rounds] The player did not reach a safe position at the destination bench.");
                            }
                        }
                    }
                }
                else
                {
                    Debug.LogWarning(
                        "[HKMP.Rounds] The destination room loaded, but no usable bench marker could be found.");
                }
            }
            finally
            {
                _travelInProgress = false;
            }
        }

        private static BenchLocation FindTargetBench(MapPresetDefinition definition)
        {
            List<BenchLocation> locations = BenchCatalogScanner.ScanCurrentScene();
            if (locations == null || locations.Count == 0)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(definition.BenchMarker))
            {
                BenchLocation exact = locations.FirstOrDefault(location =>
                    string.Equals(
                        location.MarkerName,
                        definition.BenchMarker,
                        StringComparison.OrdinalIgnoreCase));
                Vector3 exactPosition;
                if (exact != null && BenchCatalogScanner.TryGetMarkerPosition(exact, out exactPosition))
                {
                    return exact;
                }
            }

            HeroController hero = HeroController.instance;
            if (hero == null)
            {
                return locations[0];
            }

            return locations
                .Where(location =>
                {
                    Vector3 position;
                    return BenchCatalogScanner.TryGetMarkerPosition(location, out position);
                })
                .OrderBy(location =>
                {
                    Vector3 position;
                    return BenchCatalogScanner.TryGetMarkerPosition(location, out position)
                        ? (position - hero.transform.position).sqrMagnitude
                        : float.MaxValue;
                })
                .FirstOrDefault();
        }

        private static void PrepareForGameplayTransition()
        {
            RoundsSettingsUI.CloseForGameplayTransition();
        }

        private void OnDestroy()
        {
            if (_network != null)
            {
                _network.MapPresetCommandReceived -= OnMapPresetCommandReceived;
            }

            if (_instance == this)
            {
                _instance = null;
            }
        }
    }
}
