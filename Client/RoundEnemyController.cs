using System;
using GlobalEnums;
using Modding;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HKMP.Rounds.Client
{
    internal static class RoundEnemyController
    {
        private static bool _initialized;
        private static bool _enabled;
        private static bool _persistentDisabled;
        private static Coroutine _refreshCoroutine;

        public static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            ModHooks.OnEnableEnemyHook +=
                OnEnableEnemy;

            ModHooks.SceneChanged +=
                OnSceneChanged;
        }

        public static void SetEnabled(
            bool enabled)
        {
            _enabled =
                enabled;

            if (!IsDisabled)
            {
                StopRefreshCoroutine();
                return;
            }

            ScheduleExistingEnemyDisable();
        }

        public static void SetPersistentDisabled(
            bool disabled)
        {
            _persistentDisabled =
                disabled;

            if (!IsDisabled)
            {
                StopRefreshCoroutine();
                return;
            }

            ScheduleExistingEnemyDisable();
        }

        public static void Disable()
        {
            _enabled =
                false;

            if (!IsDisabled)
            {
                StopRefreshCoroutine();
            }
        }

        private static bool IsDisabled
        {
            get
            {
                return _persistentDisabled || _enabled;
            }
        }

        private static void OnSceneChanged(
            string sceneName)
        {
            if (!IsDisabled)
            {
                return;
            }

            ScheduleExistingEnemyDisable();
        }

        private static bool OnEnableEnemy(
            GameObject enemy,
            bool isAlreadyDead)
        {
            if (!IsDisabled ||
                enemy == null)
            {
                return isAlreadyDead;
            }

            HealthManager healthManager =
                enemy.GetComponent<HealthManager>();

            if (healthManager == null)
            {
                return isAlreadyDead;
            }

            if (!ShouldDisableEnemy(
                enemy,
                healthManager))
            {
                return isAlreadyDead;
            }

            return true;
        }

        private static void ScheduleExistingEnemyDisable()
        {
            if (!IsDisabled ||
                _refreshCoroutine != null)
            {
                return;
            }

            System.Collections.IEnumerator routine =
                DisableExistingEnemiesAfterSceneLoad();

            _refreshCoroutine =
                StartCoroutine(routine);

            if (_refreshCoroutine == null)
            {
                DisableExistingEnemies();
            }
        }

        private static System.Collections.IEnumerator DisableExistingEnemiesAfterSceneLoad()
        {
            yield return null;

            if (IsDisabled)
            {
                DisableExistingEnemies();
            }

            _refreshCoroutine =
                null;
        }

        private static void DisableExistingEnemies()
        {
            HealthManager[] healthManagers =
                Object.FindObjectsOfType<HealthManager>();

            if (healthManagers == null)
            {
                return;
            }

            int disabledCount =
                0;

            for (int i = 0;
                 i < healthManagers.Length;
                 i++)
            {
                HealthManager healthManager =
                    healthManagers[i];

                if (healthManager == null ||
                    healthManager.isDead)
                {
                    continue;
                }

                GameObject enemy =
                    healthManager.gameObject;

                if (!ShouldDisableEnemy(
                    enemy,
                    healthManager))
                {
                    continue;
                }

                try
                {
                    healthManager.Die(
                        null,
                        AttackTypes.Generic,
                        true);

                    disabledCount++;
                }
                catch (Exception exception)
                {
                    Modding.Logger.LogError(
                        "[HKMP.Rounds] Failed to disable existing enemy. " +
                        "Enemy=" +
                        (enemy == null ? "<null>" : enemy.name) +
                        " Exception=" +
                        exception);
                }
            }

            Modding.Logger.Log(
                "[HKMP.Rounds] Existing regular enemies disabled. " +
                "Count=" +
                disabledCount);
        }

        private static bool ShouldDisableEnemy(
            GameObject enemy,
            HealthManager healthManager)
        {
            if (enemy == null ||
                healthManager == null)
            {
                return false;
            }

            string enemyName =
                enemy.name ??
                "";

            string sceneName =
                enemy.scene.IsValid()
                    ? enemy.scene.name ?? ""
                    : "";

            if (healthManager.hp > 200)
            {
                return false;
            }

            if (string.Equals(
                enemyName,
                "Mega Moss Charger",
                StringComparison.Ordinal) ||
                string.Equals(
                    enemyName,
                    "Giant Fly",
                    StringComparison.Ordinal) ||
                string.Equals(
                    enemyName,
                    "False Knight New",
                    StringComparison.Ordinal) ||
                string.Equals(
                    enemyName,
                    "Mage Knight",
                    StringComparison.Ordinal) ||
                string.Equals(
                    enemyName,
                    "Mage Lord Phase2",
                    StringComparison.Ordinal) ||
                string.Equals(
                    enemyName,
                    "Head",
                    StringComparison.Ordinal) ||
                string.Equals(
                    enemyName,
                    "Mantis Lord S1",
                    StringComparison.Ordinal) ||
                string.Equals(
                    enemyName,
                    "Mantis Lord S2",
                    StringComparison.Ordinal) ||
                string.Equals(
                    enemyName,
                    "Ghost Warrior Xero",
                    StringComparison.Ordinal) ||
                string.Equals(
                    sceneName,
                    "Fungus3_23_boss",
                    StringComparison.Ordinal) ||
                string.Equals(
                    sceneName,
                    "Ruins2_11_boss",
                    StringComparison.Ordinal) ||
                string.Equals(
                    enemyName,
                    "Radiance",
                    StringComparison.Ordinal))
            {
                return false;
            }

            if (enemyName.IndexOf(
                "Fly",
                StringComparison.Ordinal) >= 0 &&
                string.Equals(
                    sceneName,
                    "Crossroads_04",
                    StringComparison.Ordinal))
            {
                return false;
            }

            if (enemyName.StartsWith(
                "Acid Walker",
                StringComparison.Ordinal))
            {
                return false;
            }

            if (sceneName.StartsWith(
                "Room_Colosseum",
                StringComparison.Ordinal))
            {
                return false;
            }

            return true;
        }

        private static void StopRefreshCoroutine()
        {
            if (_refreshCoroutine == null)
            {
                return;
            }

            try
            {
                if (GameManager.instance != null)
                {
                    GameManager.instance.StopCoroutine(
                        _refreshCoroutine);
                }
                else if (HeroController.instance != null)
                {
                    HeroController.instance.StopCoroutine(
                        _refreshCoroutine);
                }
            }
            catch
            {
            }

            _refreshCoroutine =
                null;
        }

        private static Coroutine StartCoroutine(
            System.Collections.IEnumerator routine)
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
