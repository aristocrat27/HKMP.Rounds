using GlobalEnums;
using HKMP.Rounds.Client.Combat;
using Modding;
using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HKMP.Rounds.Client
{
    internal static class RoundPreparation
    {
        private const int DefaultRoundSoul = 198;
        private const int BenchLookupTimeoutFrames = 600;
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

        public static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

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
            uint roundId)
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

            _lastBenchMarkerName = null;
            _lastBenchSceneName = null;
            _lastBenchRespawnType = 0;
            _lastBenchMapZone =
                GlobalEnums.MapZone.NONE;

            CancelRoundCoroutines();

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

        public static void NotifyRoundEnded(
            uint roundId)
        {
            if (roundId != _roundId ||
                _returnToBenchPending)
            {
                return;
            }

            CaptureLastBenchState();

            RoundRoomExitBlocker.Release();

            ObserveMantisWallState();
            PreserveMantisLordsBreakableWall();
            EnforceMantisWallState();

            _returnToBenchPending = true;

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

            _lastBenchMarkerName = null;
            _lastBenchSceneName = null;
            _lastBenchRespawnType = 0;
            _lastBenchMapZone =
                GlobalEnums.MapZone.NONE;

            CancelRoundCoroutines();

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
            PlayerData data =
                PlayerData.instance;

            if (data == null)
            {
                return;
            }

            if (!data.atBench &&
                data.respawnType != 1)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(
                    data.respawnScene) ||
                string.IsNullOrWhiteSpace(
                    data.respawnMarkerName))
            {
                return;
            }

            _lastBenchSceneName =
                data.respawnScene;

            _lastBenchMarkerName =
                data.respawnMarkerName;

            _lastBenchRespawnType =
                1;

            _lastBenchMapZone =
                data.mapZone;

            _lastBenchStateCaptured = true;
        }

        public static void ApplyRoundStartHealth()
        {
            SetFullHealth();

            StopCoroutineSafe(
                ref _roundStartHealthCoroutine);

            _roundStartHealthCoroutine =
                StartCoroutine(
                    RefreshRoundStartHealthVisuals());
        }

        private static IEnumerator RefreshRoundStartHealthVisuals()
        {
            yield return null;
            yield return null;

            if (RoundClientManager.IsRoundActive)
            {
                SetFullHealth();
                ClientHealthTracker.ReportCurrentHealth();
            }

            _roundStartHealthCoroutine = null;
        }

        private static void SetFullHealth()
        {
            PlayerData data =
                PlayerData.instance;

            if (data == null)
            {
                return;
            }

            int maxHealth =
                Math.Max(
                    0,
                    data.maxHealth);

            data.health =
                maxHealth;

            data.healthBlue = 0;
            data.joniHealthBlue = 0;
            data.damagedBlue = false;

            HeroController hero =
                HeroController.instance;

            if (hero != null)
            {
                hero.TakeHealth(0);
            }
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
            int remainingFrames =
                BenchLookupTimeoutFrames;

            while (_returnToBenchPending &&
                   generation == _roundGeneration &&
                   remainingFrames-- > 0)
            {
                HeroController hero =
                    HeroController.instance;

                PlayerData data =
                    PlayerData.instance;

                GameManager gameManager =
                    GameManager.instance;

                if (hero == null ||
                    data == null ||
                    gameManager == null)
                {
                    yield return null;
                    continue;
                }

                if (hero.cState != null &&
                    hero.cState.dead)
                {
                    yield return null;
                    continue;
                }

                if (_localDeathStarted &&
                    !_localDeathCompleted)
                {
                    yield return null;
                    continue;
                }

                if (!_lastBenchStateCaptured)
                {
                    CaptureLastBenchState();
                }

                if (!_lastBenchStateCaptured)
                {
                    Modding.Logger.LogError(
                        "[HKMP.Rounds] Last bench respawn state is unavailable. " +
                        "RoundId=" +
                        _roundId);

                    break;
                }

                data.respawnScene =
                    _lastBenchSceneName;

                data.respawnMarkerName =
                    _lastBenchMarkerName;

                data.respawnType =
                    1;

                data.mapZone =
                    _lastBenchMapZone;

                data.atBench = false;

                StopCoroutineSafe(
                    ref _lastBenchTrackerCoroutine);

                hero.AffectedByGravity(
                    false);

                hero.transitionState =
                    HeroTransitionState.EXITING_SCENE;

                if (hero.cState != null)
                {
                    if (hero.cState.onConveyor ||
                        hero.cState.onConveyorV ||
                        hero.cState.inConveyorZone)
                    {
                        ConveyorMovementHero conveyor =
                            hero.GetComponent<
                                ConveyorMovementHero>();

                        if (conveyor != null)
                        {
                            conveyor.StopConveyorMove();
                        }

                        hero.cState.inConveyorZone =
                            false;

                        hero.cState.onConveyor =
                            false;

                        hero.cState.onConveyorV =
                            false;
                    }

                    hero.cState.nearBench =
                        false;
                }

                try
                {
                    gameManager.cameraCtrl.FadeOut(
                        CameraFadeType.LEVEL_TRANSITION);
                }
                catch
                {
                }

                yield return new WaitForSecondsRealtime(
                    0.5f);

                try
                {
                    gameManager.SetPlayerDataBool(
                        nameof(PlayerData.atBench),
                        false);
                }
                catch
                {
                    data.atBench = false;
                }

                if (hero.cState != null)
                {
                    hero.cState.superDashing = false;
                    hero.cState.spellQuake = false;
                }

                _forceCapturedBenchRespawnType = true;

                try
                {
                    gameManager.ReadyForRespawn(
                        false);
                }
                catch (Exception exception)
                {
                    Modding.Logger.LogError(
                        "[HKMP.Rounds] Failed to start vanilla respawn at the last bench. " +
                        "RoundId=" +
                        _roundId +
                        " Exception=" +
                        exception);

                    _forceCapturedBenchRespawnType = false;
                    break;
                }

                yield return new WaitWhile(
                    () =>
                        GameManager.instance != null &&
                        GameManager.instance.IsInSceneTransition);

                _forceCapturedBenchRespawnType = false;

                try
                {
                    EventRegister.SendEvent(
                        "UPDATE BLUE HEALTH");
                }
                catch
                {
                }

                Time.timeScale = 1f;

                try
                {
                    gameManager.FadeSceneIn();
                }
                catch
                {
                }

                gameManager.isPaused = false;

                try
                {
                    GameCameras.instance.ResumeCameraShake();
                }
                catch
                {
                }

                try
                {
                    hero.UnPause();
                }
                catch
                {
                }

                TimeController.GenericTimeScale = 1f;

                _returnToBenchPending = false;
                _localDeathStarted = false;
                _localDeathCompleted = false;
                _returnToBenchCoroutine = null;

                RoundEnemyController.Disable();
                DebugShadeController.Disable();

                yield break;
            }

            _returnToBenchPending = false;
            _localDeathStarted = false;
            _localDeathCompleted = false;
            _forceCapturedBenchRespawnType = false;
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