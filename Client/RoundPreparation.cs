using GlobalEnums;
using HKMP.Rounds.Client.Combat;
using HKMP.Rounds.Settings;
using Modding;
using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace HKMP.Rounds.Client
{
    internal static class RoundPreparation
    {
        private const int DefaultRoundSoul = 198;
        private const int MantisWallSettleFrames = 8;

        private const string MantisLordsScene =
            "Fungus2_15";

        private const string MantisLordsBreakableWallId =
            "Breakable Wall";

        private static bool _initialized;
        private static bool _disableEnemies;
        private static bool _debugDisableShade;
        private static bool _returnToBenchPending;
        private static bool _localDeathStarted;
        private static bool _localDeathCompleted;
        private static bool _mantisWallWasBroken;
        private static bool _lastBenchStateCaptured;
        private static bool _forceCapturedBenchRespawnType;
        private static bool _giveLifebloodMasks;
        private static int _lifebloodMaskCount;
        private static bool _blueHealthHookInstalled;
        private static string _lastBenchSceneName;

        private static uint _preparingRoundId;
        private static uint _roundId;
        private static int _roundGeneration;
        private static int _preparationGeneration;

        private static string _lastBenchMarkerName;
        private static int _lastBenchRespawnType;
        private static GlobalEnums.MapZone _lastBenchMapZone;

        private static Coroutine _preparationCoroutine;
        private static Coroutine _roundStartHealthCoroutine;
        private static Coroutine _lastBenchTrackerCoroutine;
        private static Coroutine _mantisWallCoroutine;
        private static Coroutine _returnToBenchCoroutine;

        public static bool IsApplyingRoundStartHealth
        {
            get { return _roundStartHealthCoroutine != null; }
        }

        public static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            if (!_blueHealthHookInstalled)
            {
                ModHooks.BlueHealthHook -= GetRoundLifebloodBonus;
                ModHooks.BlueHealthHook += GetRoundLifebloodBonus;
                _blueHealthHookInstalled = true;
            }

            ModHooks.AfterPlayerDeadHook +=
                OnAfterPlayerDead;

            On.GameManager.OnNextLevelReady +=
                OnNextLevelReady;
        }

        public static void Prepare(
            uint roundId,
            int roundSoul,
            bool disableEnemies,
            bool debugDisableShade,
            Action prepared)
        {
            StopCoroutineSafe(
                ref _preparationCoroutine);

            _preparationGeneration++;

            int generation =
                _preparationGeneration;

            _preparingRoundId =
                roundId;

            _disableEnemies =
                disableEnemies;

            _debugDisableShade =
                debugDisableShade;

            RoundEnemyController.SetEnabled(
                disableEnemies);

            DebugShadeController.SetDisabled(
                debugDisableShade);

            IEnumerator routine =
                FinishPreparation(
                    roundId,
                    roundSoul,
                    generation,
                    prepared);

            _preparationCoroutine =
                StartCoroutine(
                    routine);

            if (_preparationCoroutine == null)
            {
                CompletePreparation(
                    roundId,
                    roundSoul,
                    generation,
                    prepared);
            }
        }

        private static IEnumerator FinishPreparation(
            uint roundId,
            int roundSoul,
            int generation,
            Action prepared)
        {
            yield return null;

            CompletePreparation(
                roundId,
                roundSoul,
                generation,
                prepared);
        }

        private static void CompletePreparation(
            uint roundId,
            int roundSoul,
            int generation,
            Action prepared)
        {
            if (_preparingRoundId != roundId ||
                _preparationGeneration != generation)
            {
                return;
            }

            _preparingRoundId = 0;
            _preparationCoroutine = null;

            try
            {
                SetFullRoundSoul(
                    roundSoul);

                prepared?.Invoke();
            }
            catch (Exception exception)
            {
                Modding.Logger.LogError(
                    "[HKMP.Rounds] Round preparation completion failed. " +
                    "RoundId=" +
                    roundId +
                    " Exception=" +
                    exception);

                prepared?.Invoke();
            }
        }

        public static void BeginRound(
            uint roundId,
            bool giveLifebloodMasks)
        {
            _roundGeneration++;
            _roundId =
                roundId;

            _preparingRoundId = 0;
            _returnToBenchPending = false;
            _localDeathStarted = false;
            _localDeathCompleted = false;
            _mantisWallWasBroken = false;
            _lastBenchStateCaptured = false;
            _forceCapturedBenchRespawnType = false;
            _giveLifebloodMasks = giveLifebloodMasks;
            _lifebloodMaskCount = giveLifebloodMasks ? 2 : 0;

            _lastBenchMarkerName = null;
            _lastBenchSceneName = null;
            _lastBenchRespawnType = 0;
            _lastBenchMapZone =
                GlobalEnums.MapZone.NONE;

            CancelRoundCoroutines();

            BenchPolicyController.RestoreLastRealRespawn();
            CaptureLastBenchState();
            RoundRoomExitBlocker.BlockCurrentRoom();

            StartLastBenchTracker(
                _roundGeneration);

            StartMantisWallWatcher(
                _roundGeneration);
        }

        public static void NotifyLocalDeathStarted()
        {
            if (_roundId == 0 ||
                !RoundClientManager.IsRoundActive)
            {
                return;
            }

            _localDeathStarted = true;
            _localDeathCompleted = false;

            CaptureLastBenchState();

            if (_debugDisableShade ||
                DebugShadeController.IsDisabled)
            {
                DebugShadeController.BeginDeathSuppression();
            }
        }

        public static void NotifyDeathResolution(
            uint roundId,
            int alivePlayersRemaining,
            bool roundEnded)
        {
            if (roundId != _roundId)
            {
                return;
            }
        }

        public static bool IsReturnToBenchPending
        {
            get { return _returnToBenchPending; }
        }

        public static bool IsGameplayTransitionPending
        {
            get
            {
                return _returnToBenchPending || (_localDeathStarted && !_localDeathCompleted);
            }
        }

        public static void NotifyRoundEnded(
            uint roundId)
        {
            if (roundId != _roundId || _returnToBenchPending)
            {
                return;
            }

            _returnToBenchPending = true;
            _lifebloodMaskCount = 0;
            RoundsSettingsUI.CloseForGameplayTransition();
            CaptureLastBenchState();

            RoundRoomExitBlocker.Release();

            ObserveMantisWallState();
            PreserveMantisLordsBreakableWall();
            EnforceMantisWallState();

            StartReturnToBench(
                _roundGeneration);
        }

        public static void CancelPreparation(
            uint roundId)
        {
            if (_preparingRoundId != roundId)
            {
                return;
            }

            _preparationGeneration++;
            _preparingRoundId = 0;

            StopCoroutineSafe(
                ref _preparationCoroutine);

            RoundEnemyController.Disable();
            DebugShadeController.Disable();
        }

        public static void Reset()
        {
            _roundGeneration++;
            _preparationGeneration++;

            _preparingRoundId = 0;
            _roundId = 0;

            _disableEnemies = false;
            _debugDisableShade = false;
            _returnToBenchPending = false;
            _localDeathStarted = false;
            _localDeathCompleted = false;
            _mantisWallWasBroken = false;
            _lastBenchStateCaptured = false;
            _forceCapturedBenchRespawnType = false;
            _giveLifebloodMasks = false;
            _lifebloodMaskCount = 0;

            _lastBenchMarkerName = null;
            _lastBenchSceneName = null;
            _lastBenchRespawnType = 0;
            _lastBenchMapZone =
                GlobalEnums.MapZone.NONE;

            CancelRoundCoroutines();
            PhantomMapClientController.ClearPendingPhantomDeathReturn();

            RoundRoomExitBlocker.Release();

            RoundEnemyController.Disable();
            DebugShadeController.Disable();
        }

        private static void OnAfterPlayerDead()
        {
            if (_roundId == 0)
            {
                return;
            }

            RoundsSettingsUI.CloseForGameplayTransition();
            _localDeathCompleted = true;

            if (_returnToBenchPending)
            {
                StartReturnToBench(
                    _roundGeneration);
            }
        }

        private static void StartLastBenchTracker(
            int generation)
        {
            StopCoroutineSafe(
                ref _lastBenchTrackerCoroutine);

            _lastBenchTrackerCoroutine =
                StartCoroutine(
                    TrackLastBench(
                        generation));
        }

        private static IEnumerator TrackLastBench(
            int generation)
        {
            while (_roundId != 0 &&
                   generation == _roundGeneration &&
                   RoundClientManager.IsRoundActive &&
                   !_returnToBenchPending)
            {
                CaptureLastBenchState();

                yield return null;
            }

            _lastBenchTrackerCoroutine = null;
        }

        private static void OnNextLevelReady(
            On.GameManager.orig_OnNextLevelReady orig,
            GameManager self)
        {
            if (_forceCapturedBenchRespawnType &&
                PlayerData.instance != null)
            {
                try
                {
                    HeroController hero =
                        HeroController.instance;

                    Transform spawnPoint =
                        hero == null
                            ? null
                            : hero.LocateSpawnPoint();

                    if (spawnPoint != null &&
                        spawnPoint.gameObject != null &&
                        spawnPoint.gameObject
                            .GetComponents<PlayMakerFSM>()
                            .Any(
                                fsm =>
                                    fsm != null &&
                                    fsm.FsmName == "Bench Control"))
                    {
                        PlayerData.instance.respawnType = 1;
                    }
                    else
                    {
                        PlayerData.instance.respawnType =
                            _lastBenchRespawnType;
                    }
                }
                catch
                {
                    PlayerData.instance.respawnType =
                        _lastBenchRespawnType;
                }
            }

            orig(self);
        }

        private static void CaptureLastBenchState()
        {
            PlayerData data = PlayerData.instance;
            if (data == null)
            {
                return;
            }

            if (!data.atBench && data.respawnType != 1)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(data.respawnScene) ||
                string.IsNullOrWhiteSpace(data.respawnMarkerName))
            {
                return;
            }

            ClientPreferencesStore.RememberLastRealBench(
                data.respawnScene,
                data.respawnMarkerName,
                (int)data.mapZone);

            _lastBenchSceneName = data.respawnScene;
            _lastBenchMarkerName = data.respawnMarkerName;
            _lastBenchRespawnType = 1;
            _lastBenchMapZone = data.mapZone;
            _lastBenchStateCaptured = true;
        }

        public static void ApplyRoundStartHealth(bool giveLifebloodMasks)
        {
            _giveLifebloodMasks = giveLifebloodMasks;
            StopCoroutineSafe(ref _roundStartHealthCoroutine);
            _roundStartHealthCoroutine = StartCoroutine(RefreshRoundStartHealthVisuals());
        }

        private static IEnumerator RefreshRoundStartHealthVisuals()
        {
            yield return null;
            yield return null;

            int generation = _roundGeneration;
            if (!RoundClientManager.IsRoundActive || _roundId == 0)
            {
                _roundStartHealthCoroutine = null;
                yield break;
            }

            if (PvpModeClientController.IsOneShotRound)
            {
                PvpModeClientController.ApplyOneShotHealth();
            }
            else
            {
                yield return HealMissingMasks(generation);
                if (!CanApplyRoundStartHealth(generation))
                {
                    _roundStartHealthCoroutine = null;
                    yield break;
                }

                if (_giveLifebloodMasks)
                {
                    yield return RebuildLifebloodMasks(2);
                }
            }

            if (generation == _roundGeneration && RoundClientManager.IsRoundActive)
            {
                RefreshHealthVisuals();
                ClientHealthTracker.ReportCurrentHealth();
            }

            _roundStartHealthCoroutine = null;
        }

        private static IEnumerator RebuildLifebloodMasks(int count)
        {
            int generation = _roundGeneration;
            if (PlayerData.instance == null ||
                count <= 0 ||
                !RoundClientManager.IsRoundActive ||
                PvpModeClientController.IsOneShotRound)
            {
                yield break;
            }

            ApplyLifebloodMasks(count);
            RefreshHealthVisuals();
            yield return new WaitForEndOfFrame();

            if (!CanApplyRoundStartHealth(generation))
            {
                yield break;
            }

            PlayerData data = PlayerData.instance;
            if (data != null && data.healthBlue >= count)
            {
                DestroyLifebloodCocoonsAndMites();
                yield break;
            }

            Modding.Logger.LogError(
                "[HKMP.Rounds] Lifeblood masks were not applied by the game's blue-health update; health cocoons were left intact.");
        }

        private static bool CanApplyRoundStartHealth(int generation)
        {
            return generation == _roundGeneration &&
                   _roundId != 0 &&
                   RoundClientManager.IsRoundActive &&
                   !PvpModeClientController.IsOneShotRound &&
                   !_returnToBenchPending;
        }

        private static int GetRoundLifebloodBonus()
        {
            if (_lifebloodMaskCount <= 0 ||
                !_giveLifebloodMasks ||
                !RoundClientManager.IsRoundActive ||
                PvpModeClientController.IsOneShotRound ||
                _returnToBenchPending ||
                _localDeathStarted)
            {
                return 0;
            }

            return _lifebloodMaskCount;
        }

        private static bool ApplyLifebloodMasks(int count)
        {
            PlayerData data = PlayerData.instance;
            if (data == null || count <= 0 ||
                !RoundClientManager.IsRoundActive ||
                PvpModeClientController.IsOneShotRound)
            {
                return false;
            }

            _giveLifebloodMasks = true;
            _lifebloodMaskCount = count;
            data.damagedBlue = false;

            try
            {
                data.UpdateBlueHealth();
                EventRegister.SendEvent("UPDATE BLUE HEALTH");
                PlayMakerFSM.BroadcastEvent("UPDATE BLUE HEALTH");
                return data.healthBlue >= count;
            }
            catch (Exception exception)
            {
                Modding.Logger.LogError(
                    "[HKMP.Rounds] Could not apply round lifeblood masks: " + exception.Message);
                return false;
            }
        }

        private static void RefreshHealthVisuals()
        {
            try
            {
                EventRegister.SendEvent("UPDATE HEALTH");
                EventRegister.SendEvent("UPDATE BLUE HEALTH");
                PlayMakerFSM.BroadcastEvent("UPDATE HEALTH");
                PlayMakerFSM.BroadcastEvent("UPDATE BLUE HEALTH");
            }
            catch (Exception exception)
            {
                Modding.Logger.LogError(
                    "[HKMP.Rounds] Could not refresh the health HUD after round setup: " + exception.Message);
            }
        }

        private static void DestroyLifebloodCocoonsAndMites()
        {
            Scene activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || !activeScene.isLoaded)
            {
                return;
            }

            bool activeIsMantis = string.Equals(activeScene.name, MantisLordsScene, StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(activeScene.name, "Fungus2_31", StringComparison.OrdinalIgnoreCase);
            HashSet<GameObject> targets = new HashSet<GameObject>();
            int sceneCount = UnityEngine.SceneManagement.SceneManager.sceneCount;

            for (int sceneIndex = 0; sceneIndex < sceneCount; sceneIndex++)
            {
                Scene scene;
                try
                {
                    scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(sceneIndex);
                }
                catch (Exception)
                {
                    continue;
                }

                if (!scene.IsValid() || !scene.isLoaded)
                {
                    continue;
                }

                bool isPairedMantisScene = activeIsMantis &&
                    (string.Equals(scene.name, MantisLordsScene, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(scene.name, "Fungus2_31", StringComparison.OrdinalIgnoreCase));
                if (scene.handle != activeScene.handle && !isPairedMantisScene)
                {
                    continue;
                }

                GameObject[] roots;
                try
                {
                    roots = scene.GetRootGameObjects();
                }
                catch (Exception)
                {
                    continue;
                }

                for (int i = 0; i < roots.Length; i++)
                {
                    GameObject root = roots[i];
                    if (root == null)
                    {
                        continue;
                    }

                    Component[] components = root.GetComponentsInChildren<Component>(true);
                    for (int j = 0; j < components.Length; j++)
                    {
                        Component component = components[j];
                        if (component == null || component.gameObject == null)
                        {
                            continue;
                        }

                        string typeName = component.GetType().Name;
                        string objectName = component.gameObject.name ?? string.Empty;
                        string normalizedName = objectName.ToLowerInvariant();
                        bool isCocoon = string.Equals(typeName, "HealthCocoon", StringComparison.Ordinal);
                        bool isScuttler = string.Equals(typeName, "ScuttlerControl", StringComparison.Ordinal);
                        bool isLifebloodObject = normalizedName.Contains("lifeblood cocoon") ||
                                                 normalizedName.Contains("health scuttler") ||
                                                 normalizedName.Contains("lifeblood scuttler") ||
                                                 normalizedName.Contains("lifeseed");

                        if (isCocoon || isScuttler || isLifebloodObject)
                        {
                            targets.Add(component.gameObject);
                        }
                    }
                }
            }

            foreach (GameObject target in targets)
            {
                if (target != null)
                {
                    target.SetActive(false);
                    Object.Destroy(target);
                }
            }
        }

        private static IEnumerator HealMissingMasks(int generation)
        {
            PlayerData data = PlayerData.instance;
            HeroController hero = HeroController.instance;
            if (data == null || hero == null || !CanApplyRoundStartHealth(generation))
            {
                yield break;
            }

            data.healthBlue = 0;
            data.joniHealthBlue = 0;
            data.damagedBlue = false;
            RefreshHealthVisuals();
            yield return new WaitForEndOfFrame();

            data = PlayerData.instance;
            hero = HeroController.instance;
            if (data == null || hero == null || !CanApplyRoundStartHealth(generation))
            {
                yield break;
            }

            int targetHealth = Math.Max(
                Math.Max(0, data.maxHealth),
                Math.Max(0, data.maxHealthBase));

            try
            {
                if (data.GetBool("equippedCharm_23"))
                {
                    targetHealth = Math.Max(
                        targetHealth,
                        Math.Max(0, data.maxHealthBase) + 2);
                }
            }
            catch
            {
            }

            int remaining = Math.Max(0, targetHealth - Math.Max(0, data.health));
            for (int i = 0; i < remaining; i++)
            {
                if (!CanApplyRoundStartHealth(generation))
                {
                    yield break;
                }

                data = PlayerData.instance;
                hero = HeroController.instance;
                if (data == null || hero == null || data.health >= targetHealth)
                {
                    break;
                }

                int previousHealth = data.health;
                hero.AddHealth(1);
                yield return new WaitForEndOfFrame();

                data = PlayerData.instance;
                if (data == null)
                {
                    yield break;
                }

                if (data.health <= previousHealth)
                {
                    Modding.Logger.LogError(
                        "[HKMP.Rounds] The game's AddHealth method did not increase health during round preparation. " +
                        "CurrentHealth=" + data.health +
                        " TargetHealth=" + targetHealth);
                    yield break;
                }

                RefreshHealthVisuals();
            }

            RefreshHealthVisuals();
        }

        private static void SetFullRoundSoul(
            int roundSoul)
        {
            if (PlayerData.instance == null ||
                HeroController.instance == null)
            {
                return;
            }

            roundSoul =
                Math.Max(
                    0,
                    Math.Min(
                        DefaultRoundSoul,
                        roundSoul));

            PlayerData.instance.soulLimited = false;

            HeroController.instance.SetMPCharge(
                0);

            PlayerData.instance.MPCharge = 0;
            PlayerData.instance.MPReserve = 0;

            HeroController.instance.AddMPCharge(
                roundSoul);
        }

        private static void StartReturnToBench(
            int generation)
        {
            if (!_returnToBenchPending ||
                _returnToBenchCoroutine != null)
            {
                return;
            }

            _returnToBenchCoroutine =
                StartCoroutine(
                    ReturnToLastBench(
                        generation));
        }

        private static IEnumerator ReturnToLastBench(
            int generation)
        {
            float readinessDeadline = Time.realtimeSinceStartup + 45f;
            while (_returnToBenchPending && generation == _roundGeneration &&
                   Time.realtimeSinceStartup < readinessDeadline)
            {
                HeroController hero = HeroController.instance;
                PlayerData data = PlayerData.instance;
                GameManager gameManager = GameManager.instance;
                if (hero == null || data == null || gameManager == null || gameManager.IsInSceneTransition ||
                    (hero.cState != null && hero.cState.dead) || (_localDeathStarted && !_localDeathCompleted))
                {
                    yield return null;
                    continue;
                }

                break;
            }

            if (!_returnToBenchPending || generation != _roundGeneration)
            {
                yield break;
            }

            HeroController readyHero = HeroController.instance;
            GameManager readyManager = GameManager.instance;
            if (readyHero == null || PlayerData.instance == null || readyManager == null ||
                readyManager.IsInSceneTransition || (readyHero.cState != null && readyHero.cState.dead))
            {
                Modding.Logger.LogError("[HKMP.Rounds] The player did not become ready for round-end bench return.");
                FinishRoundEndBenchReturn();
                yield break;
            }

            BenchLocation target;
            bool exactSelection;
            string resolutionMessage;
            bool resolved = BenchPolicyController.TryResolveWarpTarget(
                out target,
                out exactSelection,
                out resolutionMessage);
            if (!resolved && !exactSelection)
            {
                if (PhantomMapClientController.TryGetActivePhantomBenchTarget(out target))
                {
                    exactSelection = true;
                    resolutionMessage = "Using the paired phantom Mantis return bench.";
                }
                else
                {
                    target = ResolveRoundEndFallbackBench(null);
                }
            }

            if (target == null || !target.IsValid)
            {
                Modding.Logger.LogError(
                    "[HKMP.Rounds] Round-end warp has no valid destination. " + resolutionMessage);
                FinishRoundEndBenchReturn();
                yield break;
            }

            if (!BenchPolicyController.CanUseBenchLocation(target))
            {
                Modding.Logger.LogError(
                    "[HKMP.Rounds] The resolved round-end bench is unavailable on this client: " + target.DisplayName);
                FinishRoundEndBenchReturn();
                yield break;
            }

            StopCoroutineSafe(ref _lastBenchTrackerCoroutine);
            List<BenchLocation> targets = new List<BenchLocation> { target };
            if (!exactSelection)
            {
                BenchLocation fallback = ResolveRoundEndFallbackBench(target);
                if (fallback != null && fallback.IsValid)
                {
                    targets.Add(fallback);
                }
            }

            for (int i = 0; i < targets.Count; i++)
            {
                if (!_returnToBenchPending || generation != _roundGeneration)
                {
                    yield break;
                }

                BenchLocation currentTarget = targets[i];
                bool keepPhantomInstance = PhantomMapClientController.ShouldKeepActivePhantomInstanceForBench(currentTarget);
                if (!keepPhantomInstance)
                {
                    PhantomMapClientController.ClearPendingPhantomDeathReturn();
                    PhantomMapClientController.ReturnToNormalMantisInstance();
                }

                string message;
                if (!EmergencyBenchWarpController.TryWarpToBench(currentTarget, true, out message))
                {
                    Modding.Logger.LogError("[HKMP.Rounds] Round-end bench warp could not start for '" +
                        currentTarget.DisplayName + "': " + message);
                    continue;
                }

                float deadline = Time.realtimeSinceStartup + 45f;
                while (EmergencyBenchWarpController.IsWarpRunning &&
                       Time.realtimeSinceStartup < deadline &&
                       generation == _roundGeneration)
                {
                    yield return null;
                }

                if (EmergencyBenchWarpController.IsWarpRunning)
                {
                    Modding.Logger.LogError("[HKMP.Rounds] Round-end bench warp did not finish before the safety timeout.");
                    break;
                }

                if (IsRoundEndWarpComplete(currentTarget))
                {
                    FinishRoundEndBenchReturn();
                    yield break;
                }

                Modding.Logger.LogError("[HKMP.Rounds] Round-end bench warp did not reach a safe marker in '" +
                    currentTarget.SceneName + "'." + (i == 0 && targets.Count > 1
                        ? " Retrying at a built-in fallback bench."
                        : string.Empty));
            }

            if (generation == _roundGeneration)
            {
                FinishRoundEndBenchReturn();
            }
        }

        private static BenchLocation ResolveRoundEndFallbackBench(BenchLocation excluded)
        {
            List<BenchLocation> benches = BenchCatalog.GetBuiltInCatalog()
                .Where(location => location != null && location.IsValid &&
                    (excluded == null ||
                     !string.Equals(location.SceneName, excluded.SceneName, StringComparison.OrdinalIgnoreCase) ||
                     !string.Equals(location.MarkerName, excluded.MarkerName, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            return benches.FirstOrDefault(location =>
                       string.Equals(location.SceneName, "Town", StringComparison.OrdinalIgnoreCase) &&
                       string.Equals(location.MarkerName, "RestBench", StringComparison.OrdinalIgnoreCase)) ??
                   benches.FirstOrDefault();
        }

        private static bool IsRoundEndWarpComplete(BenchLocation target)
        {
            GameManager manager = GameManager.instance;
            HeroController hero = HeroController.instance;
            PlayerData data = PlayerData.instance;
            if (target == null || manager == null || manager.IsInSceneTransition || hero == null || data == null ||
                (hero.cState != null && hero.cState.dead) ||
                !string.Equals(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name, target.SceneName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return EmergencyBenchWarpController.IsHeroNearBench(hero, target);
        }

        private static void FinishRoundEndBenchReturn()
        {
            _returnToBenchPending = false;
            _localDeathStarted = false;
            _localDeathCompleted = false;
            _forceCapturedBenchRespawnType = false;
            PhantomMapClientController.ClearPendingPhantomDeathReturn();
            _returnToBenchCoroutine = null;
            RoundEnemyController.Disable();
            DebugShadeController.Disable();
        }

        private static void StartMantisWallWatcher(
            int generation)
        {
            StopCoroutineSafe(
                ref _mantisWallCoroutine);

            _mantisWallCoroutine =
                StartCoroutine(
                    WatchMantisWallState(
                        generation));
        }

        private static IEnumerator WatchMantisWallState(
            int generation)
        {
            string previousScene = null;
            int settleFrames = 0;

            while (_roundId != 0 &&
                   generation == _roundGeneration)
            {
                string sceneName =
                    GetCurrentSceneName();

                if (!string.Equals(
                        sceneName,
                        previousScene,
                        StringComparison.Ordinal))
                {
                    previousScene =
                        sceneName;

                    settleFrames =
                        MantisWallSettleFrames;
                }

                if (string.Equals(
                        sceneName,
                        MantisLordsScene,
                        StringComparison.Ordinal))
                {
                    if (settleFrames > 0)
                    {
                        settleFrames--;
                    }
                    else
                    {
                        ObserveMantisWallState();

                        if (_mantisWallWasBroken)
                        {
                            PreserveMantisLordsBreakableWall();
                            EnforceMantisWallState();
                        }
                    }
                }

                yield return null;
            }

            _mantisWallCoroutine = null;
        }

        private static void ObserveMantisWallState()
        {
            if (!string.Equals(
                    GetCurrentSceneName(),
                    MantisLordsScene,
                    StringComparison.Ordinal))
            {
                return;
            }

            try
            {
                GameManager gameManager =
                    GameManager.instance;

                if (gameManager != null &&
                    gameManager.sceneData != null)
                {
                    PersistentBoolData savedState =
                        gameManager.sceneData.FindMyState(
                            new PersistentBoolData
                            {
                                id =
                                    MantisLordsBreakableWallId,
                                sceneName =
                                    MantisLordsScene
                            });

                    if (savedState != null &&
                        savedState.activated)
                    {
                        _mantisWallWasBroken = true;
                        return;
                    }
                }

                GameObject breakableWall =
                    GameObject.Find(
                        MantisLordsBreakableWallId);

                if (breakableWall == null)
                {
                    _mantisWallWasBroken = true;
                }
            }
            catch (Exception exception)
            {
                Modding.Logger.LogError(
                    "[HKMP.Rounds] Failed to inspect Mantis Lords wall state. " +
                    "Exception=" +
                    exception);
            }
        }

        private static void PreserveMantisLordsBreakableWall()
        {
            if (!_mantisWallWasBroken)
            {
                return;
            }

            try
            {
                GameManager gameManager =
                    GameManager.instance;

                if (gameManager == null ||
                    gameManager.sceneData == null)
                {
                    return;
                }

                gameManager.sceneData.SaveMyState(
                    new PersistentBoolData
                    {
                        id =
                            MantisLordsBreakableWallId,
                        sceneName =
                            MantisLordsScene,
                        activated =
                            true,
                        semiPersistent =
                            false
                    });
            }
            catch (Exception exception)
            {
                Modding.Logger.LogError(
                    "[HKMP.Rounds] Failed to preserve Mantis Lords wall state. " +
                    "Exception=" +
                    exception);
            }
        }

        private static void EnforceMantisWallState()
        {
            if (!_mantisWallWasBroken ||
                !string.Equals(
                    GetCurrentSceneName(),
                    MantisLordsScene,
                    StringComparison.Ordinal))
            {
                return;
            }

            try
            {
                GameObject breakableWall =
                    GameObject.Find(
                        MantisLordsBreakableWallId);

                if (breakableWall != null)
                {
                    Object.Destroy(
                        breakableWall);
                }
            }
            catch (Exception exception)
            {
                Modding.Logger.LogError(
                    "[HKMP.Rounds] Failed to keep the broken Mantis Lords wall removed. " +
                    "Exception=" +
                    exception);
            }
        }

        private static string GetCurrentSceneName()
        {
            try
            {
                if (GameManager.instance != null &&
                    !string.IsNullOrWhiteSpace(
                        GameManager.instance.sceneName))
                {
                    return
                        GameManager.instance.sceneName;
                }
            }
            catch
            {
            }

            return string.Empty;
        }

        private static void CancelRoundCoroutines()
        {
            StopCoroutineSafe(
                ref _preparationCoroutine);

            StopCoroutineSafe(
                ref _roundStartHealthCoroutine);

            StopCoroutineSafe(
                ref _lastBenchTrackerCoroutine);

            StopCoroutineSafe(
                ref _mantisWallCoroutine);

            StopCoroutineSafe(
                ref _returnToBenchCoroutine);
        }

        private static void StopCoroutineSafe(
            ref Coroutine coroutine)
        {
            if (coroutine == null)
            {
                return;
            }

            try
            {
                if (GameManager.instance != null)
                {
                    GameManager.instance.StopCoroutine(
                        coroutine);
                }
                else if (HeroController.instance != null)
                {
                    HeroController.instance.StopCoroutine(
                        coroutine);
                }
            }
            catch
            {
            }

            coroutine = null;
        }

        private static Coroutine StartCoroutine(
            IEnumerator routine)
        {
            if (routine == null)
            {
                return null;
            }

            if (GameManager.instance != null)
            {
                return GameManager.instance.StartCoroutine(
                    routine);
            }

            HeroController hero =
                HeroController.instance;

            if (hero != null)
            {
                return hero.StartCoroutine(
                    routine);
            }

            return null;
        }
    }
}
