using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using GlobalEnums;
using UnityEngine;
using HKMP.Rounds.Settings;

namespace HKMP.Rounds.Client
{
    internal static class BenchwarpIntegration
    {
        private sealed class RoundsSceneLoadInfo : GameManager.SceneLoadInfo
        {
        }

        internal sealed class WarpSettingsSnapshot
        {
            internal string RespawnScene;
            internal string RespawnMarkerName;
            internal int RespawnType;
            internal MapZone MapZone;
            internal bool AtBench;
        }

        public static bool TryGetWarpReady(out string message)
        {
            message = string.Empty;
            if (GameManager.instance == null || PlayerData.instance == null || HeroController.instance == null)
            {
                message = "The game world is not ready for a bench transition.";
                return false;
            }

            return true;
        }

        public static bool TryPrepareWarpTarget(BenchLocation target, out WarpSettingsSnapshot snapshot, out string message)
        {
            snapshot = null;
            message = string.Empty;
            if (target == null || !target.IsValid || PlayerData.instance == null)
            {
                message = "The selected vanilla bench is invalid or the player data is unavailable.";
                return false;
            }

            PlayerData data = PlayerData.instance;
            snapshot = new WarpSettingsSnapshot
            {
                RespawnScene = data.respawnScene,
                RespawnMarkerName = data.respawnMarkerName,
                RespawnType = data.respawnType,
                MapZone = data.mapZone,
                AtBench = data.atBench
            };

            data.respawnScene = target.SceneName;
            data.respawnMarkerName = target.MarkerName;
            data.respawnType = 1;
            data.mapZone = (MapZone)target.MapZone;
            data.atBench = false;
            GameManager.instance.SetPlayerDataBool(nameof(PlayerData.atBench), false);
            return true;
        }

        public static bool TryInvokeWarpToRespawn(out string message)
        {
            message = string.Empty;
            GameManager manager = GameManager.instance;
            if (manager == null || HeroController.instance == null || PlayerData.instance == null || manager.IsInSceneTransition)
            {
                message = "The game manager cannot start a respawn transition.";
                return false;
            }

            bool traceWarp = PhantomMapDiagnostics.ShouldTraceScene();
            MethodInfo benchwarpMethod = FindBenchwarpMethod("WarpToRespawn", Type.EmptyTypes);
            if (benchwarpMethod != null)
            {
                if (traceWarp)
                {
                    PhantomMapDiagnostics.Record("benchwarp.invoke-respawn", "Provider=Benchwarp Method=" + benchwarpMethod.DeclaringType.FullName + "." + benchwarpMethod.Name, true);
                }
                try
                {
                    benchwarpMethod.Invoke(null, null);
                    if (traceWarp)
                    {
                        PhantomMapDiagnostics.Record("benchwarp.invoke-respawn.result", "Provider=Benchwarp Result=Started");
                    }
                    return true;
                }
                catch (Exception exception)
                {
                    if (traceWarp)
                    {
                        PhantomMapDiagnostics.Record("benchwarp.invoke-respawn.failed", "Provider=Benchwarp Exception=" + Unwrap(exception), true);
                    }
                    Debug.LogWarning("[HKMP.Rounds] Benchwarp respawn entry point failed; using the native respawn path: " + Unwrap(exception).Message);
                }
            }
            else if (traceWarp)
            {
                PhantomMapDiagnostics.Record("benchwarp.invoke-respawn", "Provider=Native Reason=BenchwarpNotInstalled");
            }

            manager.StartCoroutine(NativeRespawn());
            return true;
        }

        public static bool TryChangeToScene(string sceneName, string gateName, float delay, out string message)
        {
            message = string.Empty;
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                message = "The destination scene is empty.";
                return false;
            }

            GameManager manager = GameManager.instance;
            if (manager == null || manager.IsInSceneTransition)
            {
                message = "The game manager is not ready for a scene transition.";
                return false;
            }

            bool traceTransition = PhantomMapDiagnostics.ShouldTraceScene(sceneName);
            MethodInfo benchwarpMethod = FindBenchwarpMethod("ChangeToScene", new[] { typeof(string), typeof(string), typeof(float) });
            if (benchwarpMethod != null)
            {
                if (traceTransition)
                {
                    PhantomMapDiagnostics.Record("benchwarp.change-scene", "Provider=Benchwarp Target=" + sceneName + " Gate=" + gateName + " Delay=" + delay.ToString("F3"), true);
                }
                try
                {
                    benchwarpMethod.Invoke(null, new object[] { sceneName, string.IsNullOrWhiteSpace(gateName) ? "door" : gateName, delay });
                    if (traceTransition)
                    {
                        PhantomMapDiagnostics.Record("benchwarp.change-scene.result", "Provider=Benchwarp Result=Started Target=" + sceneName);
                    }
                    return true;
                }
                catch (Exception exception)
                {
                    if (traceTransition)
                    {
                        PhantomMapDiagnostics.Record("benchwarp.change-scene.failed", "Provider=Benchwarp Target=" + sceneName + " Exception=" + Unwrap(exception), true);
                    }
                    Debug.LogWarning("[HKMP.Rounds] Benchwarp scene transition entry point failed; using the native transition path: " + Unwrap(exception).Message);
                }
            }
            else if (traceTransition)
            {
                PhantomMapDiagnostics.Record("benchwarp.change-scene", "Provider=Native Target=" + sceneName + " Gate=" + gateName + " Reason=BenchwarpNotInstalled", true);
            }

            return TryBeginNativeSceneTransition(sceneName, gateName, delay, out message);
        }

        public static void CompleteWarpTarget(WarpSettingsSnapshot snapshot, bool succeeded)
        {
            if (snapshot == null || succeeded || PlayerData.instance == null)
            {
                return;
            }

            PlayerData data = PlayerData.instance;
            data.respawnScene = snapshot.RespawnScene ?? string.Empty;
            data.respawnMarkerName = snapshot.RespawnMarkerName ?? string.Empty;
            data.respawnType = snapshot.RespawnType;
            data.mapZone = snapshot.MapZone;
            data.atBench = snapshot.AtBench;
            if (GameManager.instance != null)
            {
                GameManager.instance.SetPlayerDataBool(nameof(PlayerData.atBench), snapshot.AtBench);
            }
        }

        private static MethodInfo FindBenchwarpMethod(string name, Type[] parameters)
        {
            try
            {
                Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(candidate =>
                    candidate != null && string.Equals(candidate.GetName().Name, "Benchwarp", StringComparison.OrdinalIgnoreCase));
                Type type = assembly == null ? null : assembly.GetType("Benchwarp.ChangeScene", false);
                return type == null ? null : type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, parameters, null);
            }
            catch
            {
                return null;
            }
        }

        private static bool TryBeginNativeSceneTransition(string sceneName, string gateName, float delay, out string message)
        {
            message = string.Empty;
            GameManager manager = GameManager.instance;
            if (manager == null || manager.IsInSceneTransition)
            {
                message = "The game manager is not ready for a scene transition.";
                return false;
            }

            try
            {
                if (PhantomMapDiagnostics.ShouldTraceScene(sceneName))
                {
                    PhantomMapDiagnostics.Record("benchwarp.native-scene-transition.begin",
                        "Target=" + sceneName + " Gate=" + gateName + " Delay=" + delay.ToString("F3"), true);
                }
                if (UIManager.instance != null)
                {
                    UIManager.instance.UIClosePauseMenu();
                }
                Time.timeScale = 1f;
                manager.FadeSceneIn();
                manager.isPaused = false;
                if (GameCameras.instance != null)
                {
                    GameCameras.instance.ResumeCameraShake();
                }
                if (HeroController.SilentInstance != null)
                {
                    HeroController.instance.UnPause();
                    HeroController.instance.cState.nearBench = false;
                    if (HeroController.instance.cState.onConveyor || HeroController.instance.cState.onConveyorV || HeroController.instance.cState.inConveyorZone)
                    {
                        HeroController.instance.GetComponent<ConveyorMovementHero>()?.StopConveyorMove();
                        HeroController.instance.cState.inConveyorZone = false;
                        HeroController.instance.cState.onConveyor = false;
                        HeroController.instance.cState.onConveyorV = false;
                    }
                }
                MenuButtonList.ClearAllLastSelected();
                TimeController.GenericTimeScale = 1f;
                if (manager.actorSnapshotUnpaused != null)
                {
                    manager.actorSnapshotUnpaused.TransitionTo(0f);
                }
                if (manager.ui != null)
                {
                    manager.ui.AudioGoToGameplay(0.2f);
                }
                if (PlayerData.instance != null)
                {
                    PlayerData.instance.atBench = false;
                }

                if (PhantomMapDiagnostics.ShouldTraceScene(sceneName))
                {
                    PhantomMapDiagnostics.Record("benchwarp.native-scene-transition.invoke",
                        "Target=" + sceneName + " Gate=" + gateName + " LeaveDirection=" + GetGatePosition(gateName));
                }
                manager.BeginSceneTransition(new RoundsSceneLoadInfo
                {
                    IsFirstLevelForPlayer = false,
                    SceneName = sceneName,
                    HeroLeaveDirection = GetGatePosition(gateName),
                    EntryGateName = string.IsNullOrWhiteSpace(gateName) ? "door" : gateName,
                    EntryDelay = Mathf.Max(0f, delay),
                    PreventCameraFadeOut = false,
                    WaitForSceneTransitionCameraFade = true,
                    Visualization = GameManager.SceneLoadVisualizations.Default,
                    AlwaysUnloadUnusedAssets = false
                });
                return true;
            }
            catch (Exception exception)
            {
                message = Unwrap(exception).Message;
                return false;
            }
        }

        private static GatePosition GetGatePosition(string gateName)
        {
            string value = gateName ?? string.Empty;
            if (value.IndexOf("top", StringComparison.OrdinalIgnoreCase) >= 0) return GatePosition.top;
            if (value.IndexOf("bot", StringComparison.OrdinalIgnoreCase) >= 0) return GatePosition.bottom;
            if (value.IndexOf("left", StringComparison.OrdinalIgnoreCase) >= 0) return GatePosition.left;
            if (value.IndexOf("right", StringComparison.OrdinalIgnoreCase) >= 0) return GatePosition.right;
            if (value.IndexOf("door", StringComparison.OrdinalIgnoreCase) >= 0) return GatePosition.door;
            return GatePosition.unknown;
        }

        private static IEnumerator NativeRespawn()
        {
            GameManager manager = GameManager.instance;
            HeroController hero = HeroController.instance;
            PlayerData data = PlayerData.instance;
            if (manager == null || hero == null || data == null)
            {
                yield break;
            }

            manager.SaveGame();
            manager.TimePasses();
            if (UIManager.instance != null)
            {
                UIManager.instance.UIClosePauseMenu();
            }
            hero.TakeMPQuick(data.MPCharge);
            hero.SetMPCharge(0);
            data.MPReserve = 0;
            hero.ClearMP();
            PlayMakerFSM.BroadcastEvent("MP DRAIN");
            PlayMakerFSM.BroadcastEvent("MP LOSE");
            PlayMakerFSM.BroadcastEvent("MP RESERVE DOWN");
            hero.AffectedByGravity(false);
            hero.transitionState = HeroTransitionState.EXITING_SCENE;
            if (hero.cState.onConveyor || hero.cState.onConveyorV || hero.cState.inConveyorZone)
            {
                hero.GetComponent<ConveyorMovementHero>()?.StopConveyorMove();
                hero.cState.inConveyorZone = false;
                hero.cState.onConveyor = false;
                hero.cState.onConveyorV = false;
            }
            hero.cState.nearBench = false;
            if (manager.cameraCtrl != null)
            {
                manager.cameraCtrl.FadeOut(CameraFadeType.LEVEL_TRANSITION);
            }

            yield return new WaitForSecondsRealtime(0.5f);
            manager.SetPlayerDataBool(nameof(PlayerData.atBench), false);
            hero.cState.superDashing = false;
            hero.cState.spellQuake = false;
            manager.ReadyForRespawn(false);
            while (GameManager.instance != null && GameManager.instance.IsInSceneTransition)
            {
                yield return null;
            }

            EventRegister.SendEvent("UPDATE BLUE HEALTH");
            Time.timeScale = 1f;
            manager = GameManager.instance;
            if (manager != null)
            {
                manager.FadeSceneIn();
                manager.isPaused = false;
                if (manager.actorSnapshotUnpaused != null)
                {
                    manager.actorSnapshotUnpaused.TransitionTo(0f);
                }
                if (manager.ui != null)
                {
                    manager.ui.AudioGoToGameplay(0.2f);
                }
            }
            if (GameCameras.instance != null)
            {
                GameCameras.instance.ResumeCameraShake();
            }
            if (HeroController.SilentInstance != null)
            {
                HeroController.instance.UnPause();
            }
            MenuButtonList.ClearAllLastSelected();
            TimeController.GenericTimeScale = 1f;
        }

        private static Exception Unwrap(Exception exception)
        {
            while (exception is TargetInvocationException invocation && invocation.InnerException != null)
            {
                exception = invocation.InnerException;
            }
            return exception;
        }
    }
}
