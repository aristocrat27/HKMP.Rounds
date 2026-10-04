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

        private static bool _initialized;
        private static bool _blocked;
        private static Coroutine _reblockCoroutine;

        public static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            ModHooks.SceneChanged +=
                OnSceneChanged;
        }

        public static void BlockCurrentRoom()
        {
            Initialize();

            if (!RoundClientManager.IsRoundActive)
            {
                return;
            }

            StopReblockCoroutine();
            RestoreChangedColliders();

            TransitionPoint[] transitionPoints =
                Object.FindObjectsOfType<TransitionPoint>();

            if (transitionPoints == null ||
                transitionPoints.Length == 0)
            {
                _blocked = false;
                return;
            }

            int changedCount = 0;

            for (int i = 0;
                 i < transitionPoints.Length;
                 i++)
            {
                TransitionPoint transitionPoint =
                    transitionPoints[i];

                if (transitionPoint == null)
                {
                    continue;
                }

                Collider2D[] colliders =
                    transitionPoint.GetComponentsInChildren<Collider2D>(true);

                for (int j = 0;
                     j < colliders.Length;
                     j++)
                {
                    Collider2D collider =
                        colliders[j];

                    if (collider == null ||
                        !collider.enabled ||
                        !collider.isTrigger)
                    {
                        continue;
                    }

                    int instanceId =
                        collider.GetInstanceID();

                    if (!_changedColliderIds.Add(instanceId))
                    {
                        continue;
                    }

                    _changedColliders.Add(
                        new ColliderState(collider));

                    collider.isTrigger = false;
                    changedCount++;
                }
            }

            _blocked =
                changedCount > 0;

            if (_blocked)
            {
                Physics2D.SyncTransforms();
            }
        }

        public static void Release()
        {
            Initialize();

            StopReblockCoroutine();
            RestoreChangedColliders();
            _blocked = false;
        }

        private static void OnSceneChanged(
            string sceneName)
        {
            if (!RoundClientManager.IsRoundActive)
            {
                return;
            }

            StopReblockCoroutine();

            _reblockCoroutine =
                StartCoroutine(
                    ReblockAfterSceneChange());
        }

        private static IEnumerator ReblockAfterSceneChange()
        {
            yield return null;
            yield return null;

            _reblockCoroutine = null;

            if (RoundClientManager.IsRoundActive)
            {
                BlockCurrentRoom();
            }
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
