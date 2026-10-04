using Modding;
using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HKMP.Rounds.Client
{
    internal static class DebugShadeController
    {
        private static bool _initialized;
        private static bool _roundDisabled;
        private static bool _persistentDisabled;
        private static bool _deathSuppressionPending;

        private static Coroutine _deathSuppressionCoroutine;

        public static bool IsDisabled
        {
            get
            {
                return
                    _roundDisabled ||
                    _persistentDisabled ||
                    _deathSuppressionPending;
            }
        }

        public static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            ModHooks.GetPlayerBoolHook +=
                GetPlayerBoolHook;

            ModHooks.BeforePlayerDeadHook +=
                BeforePlayerDead;

            ModHooks.AfterPlayerDeadHook +=
                AfterPlayerDead;

            ModHooks.SceneChanged +=
                OnSceneChanged;
        }

        public static void SetDisabled(
            bool disabled)
        {
            _roundDisabled =
                disabled;

            if (!IsDisabled)
            {
                StopDeathSuppression();
                return;
            }

            SuppressShadeState();
            DestroySpawnedShade();
            StartDeathSuppression();
        }

        public static void SetPersistentDisabled(
            bool disabled)
        {
            _persistentDisabled =
                disabled;

            if (!IsDisabled)
            {
                StopDeathSuppression();
                return;
            }

            SuppressShadeState();
            DestroySpawnedShade();
            StartDeathSuppression();
        }

        public static void Disable()
        {
            _roundDisabled =
                false;

            if (IsDisabled)
            {
                SuppressShadeState();
                DestroySpawnedShade();
                StartDeathSuppression();
                return;
            }

            StopDeathSuppression();
        }

        public static void BeginDeathSuppression()
        {
            if (!IsDisabled)
            {
                return;
            }

            _deathSuppressionPending =
                true;

            SuppressShadeState();
            DestroySpawnedShade();
            StartDeathSuppression();
        }

        private static void OnSceneChanged(
            string sceneName)
        {
            if (!IsDisabled)
            {
                return;
            }

            SuppressShadeState();
            DestroySpawnedShade();
        }

        private static bool GetPlayerBoolHook(
            string name,
            bool original)
        {
            if (IsDisabled &&
                string.Equals(
                    name,
                    "soulLimited",
                    StringComparison.Ordinal))
            {
                return false;
            }

            return original;
        }

        private static void BeforePlayerDead()
        {
            if (!IsDisabled)
            {
                return;
            }

            _deathSuppressionPending =
                true;

            SuppressShadeState();
            DestroySpawnedShade();
            StartDeathSuppression();
        }

        private static void AfterPlayerDead()
        {
            if (!_deathSuppressionPending)
            {
                return;
            }

            try
            {
                PlayerData data =
                    PlayerData.instance;

                if (data != null)
                {
                    HeroController hero =
                        HeroController.instance;

                    int geoPool =
                        data.GetInt(
                            nameof(PlayerData.geoPool));

                    if (hero != null &&
                        geoPool > 0)
                    {
                        hero.AddGeoQuietly(
                            geoPool);
                    }

                    data.EndSoulLimiter();

                    data.SetInt(
                        nameof(PlayerData.geoPool),
                        0);
                }
            }
            catch (Exception exception)
            {
                Modding.Logger.LogError(
                    "[HKMP.Rounds] Failed to clear Shade death penalty. " +
                    "Exception=" +
                    exception);
            }

            SuppressShadeState();
            DestroySpawnedShade();

            _deathSuppressionPending =
                false;

            if (IsDisabled)
            {
                StartDeathSuppression();
            }
            else
            {
                StopDeathSuppression();
            }
        }

        private static void SuppressShadeState()
        {
            try
            {
                PlayerData data =
                    PlayerData.instance;

                if (data == null)
                {
                    return;
                }

                data.SetString(
                    "shadeScene",
                    "None");
            }
            catch (Exception exception)
            {
                Modding.Logger.LogError(
                    "[HKMP.Rounds] Failed to suppress Shade state. " +
                    "Exception=" +
                    exception);
            }
        }

        private static void StartDeathSuppression()
        {
            if (!IsDisabled ||
                _deathSuppressionCoroutine != null)
            {
                return;
            }

            System.Collections.IEnumerator routine =
                SuppressShadeDuringDeath();

            HeroController hero =
                HeroController.instance;

            if (hero != null)
            {
                _deathSuppressionCoroutine =
                    hero.StartCoroutine(
                        routine);

                return;
            }

            if (GameManager.instance != null)
            {
                _deathSuppressionCoroutine =
                    GameManager.instance.StartCoroutine(
                        routine);
            }
        }

        private static void StopDeathSuppression()
        {
            _deathSuppressionCoroutine =
                null;
        }

        private static System.Collections.IEnumerator SuppressShadeDuringDeath()
        {
            while (IsDisabled)
            {
                SuppressShadeState();
                DestroySpawnedShade();

                yield return null;
            }

            _deathSuppressionCoroutine =
                null;
        }

        private static void DestroySpawnedShade()
        {
            try
            {
                GameObject shade =
                    GameObject.Find(
                        "Shade");

                if (shade != null)
                {
                    Object.Destroy(
                        shade);
                }

                GameObject clone =
                    GameObject.Find(
                        "Shade(Clone)");

                if (clone != null)
                {
                    Object.Destroy(
                        clone);
                }
            }
            catch (Exception exception)
            {
                Modding.Logger.LogError(
                    "[HKMP.Rounds] Failed to remove spawned Shade. " +
                    "Exception=" +
                    exception);
            }
        }
    }
}