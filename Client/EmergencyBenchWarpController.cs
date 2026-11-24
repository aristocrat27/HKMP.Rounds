using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HKMP.Rounds.Settings;
using Modding;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace HKMP.Rounds.Client
{
    internal sealed class EmergencyBenchWarpController : MonoBehaviour
    {
        private const float SceneTransitionTimeout = 45f;
        private const float MarkerLookupTimeout = 8f;
        private const float SameSceneRespawnSettleDelay = 1f;
        private const float BenchProximityTolerance = 3f;
        private static EmergencyBenchWarpController _instance;

        private bool _warpRunning;

        public static bool IsWarpRunning
        {
            get { return _instance != null && _instance._warpRunning; }
        }

        public static bool TryWarpToNearestBench(out string message)
        {
            message = string.Empty;
            HeroController hero = HeroController.instance;
            GameManager manager = GameManager.instance;
            PlayerData data = PlayerData.instance;
            if (hero == null || manager == null || data == null)
            {
                message = "The game world is not ready for a bench warp.";
                return false;
            }

            if (!BenchwarpIntegration.TryGetWarpReady(out message))
            {
                return false;
            }

            BenchPolicyController.RestoreLastRealRespawn();
            string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            BenchLocation selectedTarget;
            bool exactSelection;
            string resolutionMessage;
            if (BenchPolicyController.TryResolveWarpTarget(
                out selectedTarget,
                out exactSelection,
                out resolutionMessage))
            {
                if (selectedTarget != null)
                {
                    return StartWarp(selectedTarget, out message);
                }
            }
            else if (exactSelection)
            {
                message = resolutionMessage;
                return false;
            }

            BenchLocation nearest = FindNearestCurrentSceneBench(hero, currentScene);
            if (nearest != null)
            {
                return StartWarp(nearest, out message);
            }

            BenchLocation phantomTarget;
            if (PhantomMapClientController.TryGetActivePhantomBenchTarget(out phantomTarget))
            {
                return StartWarp(phantomTarget, out message);
            }

            BenchLocation fallback = ResolveFallbackBench(data, currentScene);
            if (fallback == null || !fallback.IsValid)
            {
                message = "No usable bench was found for the current respawn location.";
                return false;
            }

            return StartWarp(fallback, out message);
        }

        public static bool TryWarpToBench(BenchLocation target, out string message)
        {
            return TryWarpToBench(target, "door", true, out message);
        }

        public static bool TryWarpToSelectedBench(BenchLocation target, out string message)
        {
            message = string.Empty;
            if (target == null || !target.IsValid)
            {
                message = "Select a valid bench before warping.";
                return false;
            }

            if (!BenchPolicyController.CanUseBenchLocation(target))
            {
                message = "The selected bench is not available in the current game scene.";
                return false;
            }

            if (GameManager.instance == null || HeroController.instance == null || PlayerData.instance == null)
            {
                message = "The game world is not ready for a bench warp.";
                return false;
            }

            return StartWarp(target.Copy(), true, out message);
        }

        public static bool TryWarpToBench(BenchLocation target, string entryGate, out string message)
        {
            return TryWarpToBench(target, entryGate, true, out message);
        }

        public static bool TryWarpToBench(BenchLocation target, bool sitAtBench, out string message)
        {
            return TryWarpToBench(target, "door", sitAtBench, out message);
        }

        public static bool TryWarpToBench(
            BenchLocation target,
            string entryGate,
            bool sitAtBench,
            out string message)
        {
            message = string.Empty;
            if (target == null || !target.IsValid)
            {
                message = "The selected bench is invalid.";
                return false;
            }

            if (!BenchPolicyController.CanUseBenchLocation(target))
            {
                message = "The selected bench is not available in the current game scene.";
                return false;
            }

            if (GameManager.instance == null || HeroController.instance == null || PlayerData.instance == null)
            {
                message = "The game world is not ready for a bench warp.";
                return false;
            }

            return StartWarp(target.Copy(), out message);
        }

        private static bool StartWarp(BenchLocation target, out string message)
        {
            return StartWarp(target, false, out message);
        }

        private static bool StartWarp(BenchLocation target, bool cancelPendingPhantomWarps, out string message)
        {
            message = string.Empty;
            if (target == null || !target.IsValid)
            {
                message = "The selected bench is invalid.";
                return false;
            }

            if (!BenchwarpIntegration.TryGetWarpReady(out message))
            {
                return false;
            }

            EmergencyBenchWarpController controller = EnsureInstance();
            if (controller == null)
            {
                message = "The bench warp controller could not be initialized.";
                return false;
            }

            if (controller._warpRunning)
            {
                message = "A bench warp is already running.";
                return false;
            }

            GameManager manager = GameManager.instance;
            if (manager == null || manager.IsInSceneTransition)
            {
                message = "A gameplay transition is already in progress.";
                return false;
            }

            if (cancelPendingPhantomWarps)
            {
                PhantomMapClientController.CancelPendingAutomaticBenchWarps();
            }

            if (!PhantomMapClientController.PrepareForBenchWarp(target))
            {
                message = "The selected bench has an invalid phantom map.";
                return false;
            }

            bool traceWarp = PhantomMapDiagnostics.ShouldTraceScene(target.SceneName);
            if (traceWarp)
            {
                PhantomMapDiagnostics.Record("emergency-warp.requested",
                    "Target=" + target.SceneName + "/" + target.MarkerName + " Display=" + target.DisplayName +
                    " HeroPosition=" + HeroController.instance.transform.position.ToString("F3"), true);
            }
            controller._warpRunning = true;
            RoundsSettingsUI.CloseForGameplayTransition();
            controller.StartCoroutine(controller.WarpToBenchRoutine(target.Copy()));
            message = "Warp started to " + target.DisplayName + ".";
            return true;
        }

        private static EmergencyBenchWarpController EnsureInstance()
        {
            if (_instance != null)
            {
                return _instance;
            }

            GameObject controllerObject = new GameObject("[HKMP.Rounds] Bench Warp Controller");
            Object.DontDestroyOnLoad(controllerObject);
            _instance = controllerObject.AddComponent<EmergencyBenchWarpController>();
            return _instance;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private IEnumerator WarpToBenchRoutine(BenchLocation target)
        {
            BenchwarpIntegration.WarpSettingsSnapshot settingsSnapshot = null;
            IDisposable transitionScope = null;
            bool completed = false;
            bool warpInvoked = false;
            bool traceWarp = PhantomMapDiagnostics.ShouldTraceScene(target.SceneName);
            if (traceWarp)
            {
                PhantomMapDiagnostics.Record("emergency-warp.routine.begin", "Target=" + target.SceneName + "/" + target.MarkerName, true);
            }
            try
            {
                GameManager manager = GameManager.instance;
                HeroController hero = HeroController.instance;
                if (manager == null || hero == null || PlayerData.instance == null ||
                    manager.IsInSceneTransition)
                {
                    Modding.Logger.Log("[HKMP.Rounds] The game world was not ready when the bench warp began.");
                    yield break;
                }

                string prepareMessage;
                if (!BenchwarpIntegration.TryPrepareWarpTarget(target, out settingsSnapshot, out prepareMessage))
                {
                    Modding.Logger.LogError("[HKMP.Rounds] Could not prepare the bench destination: " + prepareMessage);
                    yield break;
                }

                Scene sourceScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                if (traceWarp)
                {
                    PhantomMapDiagnostics.Record("emergency-warp.prepare", "Source=" + sourceScene.name + "#" + sourceScene.handle +
                        " Target=" + target.SceneName + "/" + target.MarkerName + " Respawn=" + PlayerData.instance.respawnScene + "/" + PlayerData.instance.respawnMarkerName);
                }
                transitionScope = RoundRoomExitBlocker.AcquireProgrammaticTransition();
                string warpMessage;
                if (!BenchwarpIntegration.TryInvokeWarpToRespawn(out warpMessage))
                {
                    Modding.Logger.LogError("[HKMP.Rounds] Could not start the respawn transition: " + warpMessage);
                    yield break;
                }

                warpInvoked = true;
                if (traceWarp)
                {
                    PhantomMapDiagnostics.Record("emergency-warp.invoked", "Message=" + warpMessage + " ProviderRunning=" + _warpRunning, true);
                }
                float warpStartedAt = Time.realtimeSinceStartup;
                float transitionDeadline = warpStartedAt + SceneTransitionTimeout;
                bool transitionObserved = false;
                bool arrived = false;
                while (Time.realtimeSinceStartup < transitionDeadline)
                {
                    manager = GameManager.instance;
                    Scene activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                    hero = HeroController.instance;
                    if (manager != null &&
                        (manager.IsInSceneTransition ||
                         (activeScene.IsValid() && sourceScene.IsValid() && activeScene.handle != sourceScene.handle)))
                    {
                        transitionObserved = true;
                    }

                    bool targetSceneReady = manager != null && !manager.IsInSceneTransition &&
                        activeScene.IsValid() &&
                        string.Equals(activeScene.name, target.SceneName, StringComparison.OrdinalIgnoreCase) &&
                        hero != null && (hero.cState == null || !hero.cState.dead);
                    if (targetSceneReady && transitionObserved)
                    {
                        arrived = true;
                        break;
                    }

                    if (targetSceneReady &&
                        Time.realtimeSinceStartup - warpStartedAt >= SameSceneRespawnSettleDelay &&
                        IsHeroNearBench(hero, target))
                    {
                        arrived = true;
                        break;
                    }

                    yield return null;
                }

                if (!arrived)
                {
                    if (traceWarp)
                    {
                        PhantomMapDiagnostics.Record("emergency-warp.timeout",
                            "Target=" + target.SceneName + " Source=" + sourceScene.name + " TransitionObserved=" + transitionObserved, true);
                    }
                    Modding.Logger.LogError(
                        "[HKMP.Rounds] The scene transition did not complete for " + target.SceneName + ".");
                    yield break;
                }

                float markerDeadline = Time.realtimeSinceStartup + MarkerLookupTimeout;
                Vector3 markerPosition = Vector3.zero;
                bool markerFound = false;
                while (Time.realtimeSinceStartup < markerDeadline)
                {
                    if (BenchCatalogScanner.TryGetMarkerPosition(target, out markerPosition))
                    {
                        markerFound = true;
                        break;
                    }

                    yield return null;
                }

                if (!markerFound)
                {
                    Modding.Logger.LogError(
                        "[HKMP.Rounds] The destination marker was not available: " +
                        target.SceneName + " / " + target.MarkerName + ".");
                    yield break;
                }

                yield return null;
                yield return new WaitForEndOfFrame();
                hero = HeroController.instance;
                manager = GameManager.instance;
                if (hero == null || manager == null || manager.IsInSceneTransition ||
                    (hero.cState != null && hero.cState.dead) || !IsHeroNearBench(hero, target))
                {
                    Modding.Logger.LogError(
                        "[HKMP.Rounds] The player was not placed at the requested bench: " +
                        target.SceneName + " / " + target.MarkerName + ".");
                    yield break;
                }

                if (traceWarp)
                {
                    PhantomMapDiagnostics.Record("emergency-warp.positioned",
                        "Target=" + target.SceneName + "/" + target.MarkerName + " MarkerPosition=" + markerPosition.ToString("F3") +
                        " HeroPosition=" + hero.transform.position.ToString("F3") + " NearBench=" + IsHeroNearBench(hero, target));
                }
                MapTravelProgressController.ApplyCurrentScene();
                SceneTravelCatalog.ObserveCurrentScene();
                completed = true;
            }
            finally
            {
                try
                {
                    BenchwarpIntegration.CompleteWarpTarget(settingsSnapshot, completed);
                }
                catch (Exception exception)
                {
                    Modding.Logger.LogError(
                        "[HKMP.Rounds] Could not restore respawn settings after the bench warp: " + exception.Message);
                }
                finally
                {
                    try
                    {
                        if (transitionScope != null)
                        {
                            transitionScope.Dispose();
                        }
                    }
                    finally
                    {
                        if (traceWarp)
                        {
                            PhantomMapDiagnostics.Record("emergency-warp.routine.end",
                                "Target=" + target.SceneName + "/" + target.MarkerName + " Completed=" + completed +
                                " WarpInvoked=" + warpInvoked + " HeroPosition=" +
                                (HeroController.instance == null ? "<null>" : HeroController.instance.transform.position.ToString("F3")), true);
                        }
                        _warpRunning = false;
                        if (completed)
                        {
                            PhantomMapClientController.NotifyBenchWarpCompleted();
                        }
                        if (warpInvoked)
                        {
                            RestoreHeroControlAfterWarp();
                        }
                    }
                }
            }
        }

        private static void RestoreHeroControlAfterWarp()
        {
            GameManager manager = GameManager.instance;
            HeroController hero = HeroController.instance;
            if (manager == null || hero == null || manager.IsInSceneTransition ||
                (hero.cState != null && hero.cState.dead))
            {
                return;
            }

            if (hero.cState != null)
            {
                if (hero.cState.onConveyor || hero.cState.onConveyorV || hero.cState.inConveyorZone)
                {
                    ConveyorMovementHero conveyor = hero.GetComponent<ConveyorMovementHero>();
                    if (conveyor != null)
                    {
                        conveyor.StopConveyorMove();
                    }
                    hero.cState.inConveyorZone = false;
                    hero.cState.onConveyor = false;
                    hero.cState.onConveyorV = false;
                }

                hero.cState.nearBench = false;
                hero.cState.superDashing = false;
                hero.cState.spellQuake = false;
            }

            hero.AffectedByGravity(true);
            PlayerData data = PlayerData.instance;
            if (data != null && data.atBench)
            {
                data.atBench = false;
                manager.SetPlayerDataBool(nameof(PlayerData.atBench), false);
            }
            Time.timeScale = 1f;
            TimeController.GenericTimeScale = 1f;
            manager.isPaused = false;
            if (manager.actorSnapshotUnpaused != null)
            {
                manager.actorSnapshotUnpaused.TransitionTo(0f);
            }
            if (manager.ui != null)
            {
                manager.ui.AudioGoToGameplay(0.2f);
            }
            if (GameCameras.instance != null)
            {
                GameCameras.instance.ResumeCameraShake();
            }
            hero.UnPause();
        }

        private static BenchLocation FindNearestCurrentSceneBench(HeroController hero, string currentScene)
        {
            if (hero == null || string.IsNullOrWhiteSpace(currentScene))
            {
                return null;
            }

            List<BenchLocation> locations = BenchCatalogScanner.ScanCurrentScene();
            BenchLocation nearest = null;
            float nearestDistance = float.MaxValue;
            for (int i = 0; i < locations.Count; i++)
            {
                BenchLocation location = locations[i];
                if (location == null ||
                    !string.Equals(location.SceneName, currentScene, StringComparison.OrdinalIgnoreCase) ||
                    !BenchPolicyController.CanUseBenchLocation(location))
                {
                    continue;
                }

                Vector3 position;
                if (!BenchCatalogScanner.TryGetMarkerPosition(location, out position))
                {
                    continue;
                }

                float distance = (position - hero.transform.position).sqrMagnitude;
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = location;
                }
            }

            return nearest;
        }

        private static BenchLocation ResolveFallbackBench(PlayerData data, string currentScene)
        {
            if (BenchPolicyController.HasSharedBench &&
                !string.IsNullOrWhiteSpace(BenchPolicyController.SharedBenchScene) &&
                !string.IsNullOrWhiteSpace(BenchPolicyController.SharedBenchMarker) &&
                !string.Equals(BenchPolicyController.SharedBenchScene, currentScene, StringComparison.OrdinalIgnoreCase))
            {
                BenchLocation shared = new BenchLocation(
                    BenchPolicyController.SharedBenchScene,
                    BenchPolicyController.SharedBenchMarker,
                    BenchPolicyController.SharedBenchMapZone,
                    "Shared bench",
                    BenchPolicyController.SharedBenchPhantomMapId);
                if (BenchPolicyController.CanUseBenchLocation(shared))
                {
                    return shared;
                }
            }

            string savedScene;
            string savedMarker;
            int savedZone;
            int savedPhantomMapId;
            if (ClientPreferencesStore.TryGetLastRealBench(out savedScene, out savedMarker, out savedZone, out savedPhantomMapId) &&
                !string.Equals(savedScene, currentScene, StringComparison.OrdinalIgnoreCase))
            {
                return new BenchLocation(savedScene, savedMarker, savedZone, "Last real bench", savedPhantomMapId);
            }

            if (data != null && data.respawnType == 1 &&
                !string.IsNullOrWhiteSpace(data.respawnScene) &&
                !string.IsNullOrWhiteSpace(data.respawnMarkerName) &&
                !string.Equals(data.respawnScene, currentScene, StringComparison.OrdinalIgnoreCase) &&
                BenchPolicyController.CanUseBenchLocation(new BenchLocation(
                    data.respawnScene,
                    data.respawnMarkerName,
                    (int)data.mapZone,
                    string.Empty,
                    PhantomMapClientController.GetPhantomMapIdForScene(data.respawnScene))))
            {
                return new BenchLocation(
                    data.respawnScene,
                    data.respawnMarkerName,
                    (int)data.mapZone,
                    "Saved respawn bench",
                    PhantomMapClientController.GetPhantomMapIdForScene(data.respawnScene));
            }
    



            List<BenchLocation> benches = BenchCatalog.GetBuiltInCatalog()
                .Where(location => location != null && location.IsValid &&
                    !string.Equals(location.SceneName, currentScene, StringComparison.OrdinalIgnoreCase))
                .OrderBy(location => location.SceneName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(location => location.MarkerName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            int preferredZone = data != null ? (int)data.mapZone : savedZone;
            return benches.FirstOrDefault(location => location.MapZone == preferredZone) ?? benches.FirstOrDefault();
        }

        public static bool IsHeroNearBench(HeroController hero, BenchLocation target)
        {
            if (hero == null || target == null || !target.IsValid ||
                !string.Equals(PhantomMapClientController.GetCurrentLogicalScene(), target.SceneName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            Vector3 markerPosition;
            if (!BenchCatalogScanner.TryGetMarkerPosition(target, out markerPosition))
            {
                return false;
            }

            return (hero.transform.position - markerPosition).sqrMagnitude <=
                   BenchProximityTolerance * BenchProximityTolerance;
        }
    }
}
