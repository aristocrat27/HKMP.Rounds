using Modding;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HKMP.Rounds.Client
{
    internal static class RoundRoomExitBlocker
    {
        private sealed class ColliderState
        {
            public Collider2D Collider;
            public bool WasTrigger;

            public ColliderState(
                Collider2D collider)
            {
                Collider = collider;
                WasTrigger = collider.isTrigger;
            }
        }

        private static readonly List<ColliderState> _changedColliders =
            new List<ColliderState>();

        private static readonly HashSet<int> _changedColliderIds =
            new HashSet<int>();

        private static readonly HashSet<string> _lockedRoundScenes =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static bool _initialized;
        private static bool _blocked;
        private static bool _phantomRoomLocked;
        private static Coroutine _reblockCoroutine;
        private static string _arenaSceneName = string.Empty;
        private static int _authorizedTransitionDepth;

        private sealed class ProgrammaticTransitionScope : IDisposable
        {
            private bool _disposed;

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _authorizedTransitionDepth = Math.Max(0, _authorizedTransitionDepth - 1);
            }
        }

        public static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            ModHooks.SceneChanged +=
                OnSceneChanged;
            On.GameManager.BeginSceneTransition -= OnBeginSceneTransition;
            On.GameManager.BeginSceneTransition += OnBeginSceneTransition;
            On.GameManager.LoadScene -= OnLoadScene;
            On.GameManager.LoadScene += OnLoadScene;
            On.GameManager.GetCurrentMapZone -= OnGetCurrentMapZone;
            On.GameManager.GetCurrentMapZone += OnGetCurrentMapZone;
        }


        public static void SetPhantomRoomLocked(bool locked)
        {
            Initialize();
            if (PhantomMapDiagnostics.IsRelevantScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name) ||
                PhantomMapClientController.CurrentPhantomMapId > 0)
            {
                PhantomMapDiagnostics.Record("exit-blocker.set-phantom-lock", "Previous=" + _phantomRoomLocked + " Requested=" + locked);
            }
            if (_phantomRoomLocked == locked)
            {
                if (locked)
                {
                    BlockCurrentRoom();
                }
                return;
            }

            _phantomRoomLocked = locked;
            BlockCurrentRoom();
        }

        public static void SetLockedRoundScenes(string json)
        {
            HashSet<string> replacement = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                string[] scenes = Newtonsoft.Json.JsonConvert.DeserializeObject<string[]>(json ?? "[]");
                if (scenes == null)
                {
                    replacement.Add("*");
                }
                else
                {
                    for (int i = 0; i < scenes.Length; i++)
                    {
                        string scene = (scenes[i] ?? string.Empty).Trim();
                        if (!string.IsNullOrWhiteSpace(scene))
                        {
                            replacement.Add(scene);
                        }
                    }
                }
            }
            catch
            {
                replacement.Clear();
                replacement.Add("*");
            }

            if (_lockedRoundScenes.SetEquals(replacement))
            {
                return;
            }

            _lockedRoundScenes.Clear();
            foreach (string scene in replacement)
            {
                _lockedRoundScenes.Add(scene);
            }

            BlockCurrentRoom();
        }

        public static void ClearServerLockedScenes()
        {
            _lockedRoundScenes.Clear();
            BlockCurrentRoom();
        }

        private static bool IsSceneLocked(string sceneName)
        {
            if (_lockedRoundScenes.Contains("*"))
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(sceneName))
            {
                return false;
            }

            string normalizedScene = sceneName.Trim();
            return _lockedRoundScenes.Contains(normalizedScene) ||
                   _lockedRoundScenes.Contains(PhantomMapClientController.GetRoundRoomKey(normalizedScene));
        }

        public static void BlockCurrentRoom()
        {
            Initialize();

            string activeSceneName = PhantomMapClientController.GetCurrentLogicalScene();
            bool sceneIsLocked = IsSceneLocked(activeSceneName);
            bool roundLocked = RoundClientManager.IsRoundActive || sceneIsLocked;
            bool phantomLocked = _phantomRoomLocked && PhantomMapClientController.IsActive;
            bool diagnosticScene = PhantomMapDiagnostics.IsRelevantScene(activeSceneName) ||
                PhantomMapClientController.CurrentPhantomMapId > 0;

            if (!roundLocked && !phantomLocked && _lockedRoundScenes.Count == 0)
            {
                StopReblockCoroutine();
                RestoreChangedColliders();
                _blocked = false;
                _arenaSceneName = string.Empty;
                return;
            }

            StopReblockCoroutine();
            RestoreChangedColliders();
            if (!string.IsNullOrWhiteSpace(activeSceneName))
            {
                _arenaSceneName = activeSceneName;
            }

            UnityEngine.SceneManagement.Scene activeScene =
                UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || !activeScene.isLoaded)
            {
                _blocked = false;
                return;
            }

            TransitionPoint[] transitions = Object.FindObjectsOfType<TransitionPoint>();
            if (transitions == null || transitions.Length == 0)
            {
                _blocked = false;
                return;
            }

            int changedCount = 0;
            for (int i = 0; i < transitions.Length; i++)
            {
                TransitionPoint transitionPoint = transitions[i];
                if (transitionPoint == null || transitionPoint.gameObject == null ||
                    transitionPoint.gameObject.scene.handle != activeScene.handle)
                {
                    continue;
                }

                bool targetLocked = IsSceneLocked(transitionPoint.targetScene);
                bool isPairedRoomTransition = phantomLocked && !roundLocked && !targetLocked &&
                    PhantomMapClientController.IsMantisPairTransition(
                        activeSceneName,
                        transitionPoint.targetScene);
                bool shouldBlock = roundLocked || targetLocked || (phantomLocked && !isPairedRoomTransition);
                if (!shouldBlock)
                {
                    continue;
                }

                Collider2D[] colliders = transitionPoint.GetComponentsInChildren<Collider2D>(true);
                for (int j = 0; j < colliders.Length; j++)
                {
                    Collider2D collider = colliders[j];
                    if (collider == null || !collider.enabled || !collider.isTrigger)
                    {
                        continue;
                    }

                    int instanceId = collider.GetInstanceID();
                    if (!_changedColliderIds.Add(instanceId))
                    {
                        continue;
                    }

                    _changedColliders.Add(new ColliderState(collider));
                    collider.isTrigger = false;
                    changedCount++;
                }
            }

            _blocked = changedCount > 0;
            if (_blocked)
            {
                Physics2D.SyncTransforms();
            }

            if (diagnosticScene)
            {
                PhantomMapDiagnostics.Record(
                    roundLocked ? "exit-blocker.round-transitions.blocked" : "exit-blocker.phantom-exits.blocked",
                    "Scene=" + activeSceneName + " Changed=" + changedCount + " PairedRoomAllowed=" + phantomLocked,
                    true);
            }
        }

        public static void Release()
        {
            Initialize();

            BlockCurrentRoom();
        }


        public static IDisposable AcquireProgrammaticTransition()
        {
            Initialize();
            _authorizedTransitionDepth++;
            return new ProgrammaticTransitionScope();
        }


        private static void OnBeginSceneTransition(
            On.GameManager.orig_BeginSceneTransition orig,
            GameManager self,
            GameManager.SceneLoadInfo info)
        {
            string sourceScene = GetLoadedSourceScene(self);
            string targetScene = info == null ? string.Empty : info.SceneName ?? string.Empty;
            bool diagnosticScene = PhantomMapDiagnostics.IsRelevantScene(sourceScene) ||
                PhantomMapDiagnostics.IsRelevantScene(targetScene) ||
                PhantomMapClientController.CurrentPhantomMapId > 0;

            if (ShouldBlockTransition(sourceScene, targetScene))
            {
                PhantomMapDiagnostics.Record(
                    "exit-blocker.begin-scene-transition.blocked",
                    "Source=" + sourceScene + " Target=" + targetScene +
                    " AuthorizedDepth=" + _authorizedTransitionDepth,
                    true);
                Modding.Logger.Log(
                    "[HKMP.Rounds] Blocked scene transition because its source or destination room is locked. " +
                    "Source=" + sourceScene + ", Target=" + targetScene);
                return;
            }

            if (diagnosticScene)
            {
                PhantomMapDiagnostics.Record(
                    "exit-blocker.begin-scene-transition.allowed",
                    "Source=" + sourceScene + " Target=" + targetScene +
                    " AuthorizedDepth=" + _authorizedTransitionDepth,
                    true);
            }

            orig(self, info);
        }

        private static void OnLoadScene(
            On.GameManager.orig_LoadScene orig,
            GameManager self,
            string destinationScene)
        {
            string sourceScene = GetLoadedSourceScene(self);
            string targetScene = destinationScene ?? string.Empty;
            if (ShouldBlockTransition(sourceScene, targetScene))
            {
                PhantomMapDiagnostics.Record(
                    "exit-blocker.load-scene.blocked",
                    "Source=" + sourceScene + " Target=" + targetScene,
                    true);
                return;
            }

            if (PhantomMapDiagnostics.ShouldTraceScene(sourceScene) ||
                PhantomMapDiagnostics.ShouldTraceScene(targetScene))
            {
                PhantomMapDiagnostics.Record(
                    "exit-blocker.load-scene.allowed",
                    "Source=" + sourceScene + " Target=" + targetScene);
            }

            orig(self, destinationScene);
        }

        private static bool ShouldBlockTransition(string sourceScene, string targetScene)
        {
            if (string.IsNullOrWhiteSpace(targetScene) ||
                string.Equals(sourceScene, targetScene, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (_authorizedTransitionDepth > 0)
            {
                return false;
            }

            if (RoundClientManager.IsRoundActive || IsSceneLocked(sourceScene) || IsSceneLocked(targetScene))
            {
                return true;
            }

            return _phantomRoomLocked && PhantomMapClientController.IsActive &&
                   !PhantomMapClientController.IsMantisPairTransition(sourceScene, targetScene);
        }

        private static string GetLoadedSourceScene(GameManager manager)
        {
            string logicalSceneName = PhantomMapClientController.GetCurrentLogicalScene();
            if (!string.IsNullOrWhiteSpace(logicalSceneName))
            {
                return logicalSceneName;
            }

            return manager != null
                ? PhantomMapClientController.GetLogicalSceneName(manager.sceneName)
                : string.Empty;
        }

        private static string OnGetCurrentMapZone(
            On.GameManager.orig_GetCurrentMapZone orig,
            GameManager self)
        {
            if (self != null && self.sm != null)
            {
                return orig(self);
            }

            PlayerData playerData = PlayerData.instance;
            string fallback = playerData != null ? playerData.mapZone.ToString() : string.Empty;
            if (PhantomMapDiagnostics.IsRelevantScene(self == null ? string.Empty : self.sceneName))
            {
                PhantomMapDiagnostics.Record("game-manager.map-zone-fallback",
                    "Scene=" + (self == null ? "<null>" : self.sceneName) + " Zone=" + fallback);
            }
            return fallback;
        }

        private static void OnSceneChanged(
            string sceneName)
        {
            string currentScene = PhantomMapClientController.GetLogicalSceneName(sceneName);
            if (string.IsNullOrWhiteSpace(currentScene))
            {
                currentScene = PhantomMapClientController.GetCurrentLogicalScene();
            }

            if (PhantomMapDiagnostics.IsRelevantScene(currentScene) || PhantomMapClientController.CurrentPhantomMapId > 0)
            {
                PhantomMapDiagnostics.Record("exit-blocker.scene-changed",
                    "Scene=" + currentScene + " PhantomLocked=" + _phantomRoomLocked + " RoundActive=" + RoundClientManager.IsRoundActive +
                    " SceneLocked=" + IsSceneLocked(currentScene));
            }
            if (!string.IsNullOrWhiteSpace(currentScene))
            {
                _arenaSceneName = currentScene;
            }

            StopReblockCoroutine();
            _reblockCoroutine = StartCoroutine(ReblockAfterSceneChange());
        }


        private static IEnumerator ReblockAfterSceneChange()
        {
            yield return null;
            yield return null;

            _reblockCoroutine = null;

            BlockCurrentRoom();
        }

        private static void StopReblockCoroutine()
        {
            if (_reblockCoroutine == null)
            {
                return;
            }

            try
            {
                if (GameManager.instance != null)
                {
                    GameManager.instance.StopCoroutine(
                        _reblockCoroutine);
                }
                else if (HeroController.instance != null)
                {
                    HeroController.instance.StopCoroutine(
                        _reblockCoroutine);
                }
            }
            catch
            {
            }

            _reblockCoroutine = null;
        }

        private static void RestoreChangedColliders()
        {
            for (int i = _changedColliders.Count - 1;
                 i >= 0;
                 i--)
            {
                ColliderState state =
                    _changedColliders[i];

                if (state != null &&
                    state.Collider != null)
                {
                    try
                    {
                        state.Collider.isTrigger =
                            state.WasTrigger;
                    }
                    catch (Exception exception)
                    {
                        Modding.Logger.LogError(
                            "[HKMP.Rounds] Failed to restore room exit collider. " +
                            "Exception=" +
                            exception);
                    }
                }
            }

            _changedColliders.Clear();
            _changedColliderIds.Clear();

            Physics2D.SyncTransforms();
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
