using System;
using UnityEngine;

namespace HKMP.Rounds.Client.Combat
{
    internal static class ClientHealthTracker
    {
        private static bool _initialized;
        private static ClientNetManager _network;

        private static int _lastHealth =
            int.MinValue;

        private static int _lastMaxHealth =
            int.MinValue;

        private static int _lastBlueHealth =
            int.MinValue;

        private static int _lastSoul =
            int.MinValue;

        private static GameObject _reportObject;

        private static HealthReportBehaviour _reportBehaviour;

        private const float ReportIntervalSeconds =
            0.10f;

        public static void Initialize(
            ClientNetManager network)
        {
            if (_initialized)
            {
                _network = network;
                return;
            }

            _initialized = true;
            _network = network;

            CreateReportBehaviour();

            On.HeroController.TakeDamage +=
                OnTakeDamage;

            On.HeroController.TakeHealth +=
                OnTakeHealth;
        }

        private static void OnTakeDamage(
            On.HeroController.orig_TakeDamage orig,
            HeroController self,
            UnityEngine.GameObject go,
            GlobalEnums.CollisionSide damageSide,
            int damageAmount,
            int hazardType)
        {
            orig(
                self,
                go,
                damageSide,
                damageAmount,
                hazardType);

            if (self == HeroController.instance)
            {
                ReportCurrentHealth();
            }
        }

        private static void OnTakeHealth(
            On.HeroController.orig_TakeHealth orig,
            HeroController self,
            int amount)
        {
            orig(
                self,
                amount);

            if (self == HeroController.instance)
            {
                ReportCurrentHealth();
            }
        }

        public static void ReportCurrentHealth()
        {
            if (_network == null ||
                !RoundClientManager.IsRoundActive ||
                PlayerData.instance == null)
            {
                return;
            }

            int health =
                Math.Max(
                    0,
                    PlayerData.instance.health);

            int maxHealth =
                GetEffectiveMaxHealth(
                    PlayerData.instance);

            int blueHealth =
                Math.Max(
                    0,
                    PlayerData.instance.healthBlue);

            int soul =
                GetCurrentSoul(
                    PlayerData.instance);

            if (maxHealth > 0 &&
                health > maxHealth)
            {
                health = maxHealth;
            }

            if (health == _lastHealth &&
                maxHealth == _lastMaxHealth &&
                blueHealth == _lastBlueHealth &&
                soul == _lastSoul)
            {
                return;
            }

            _lastHealth = health;
            _lastMaxHealth = maxHealth;
            _lastBlueHealth = blueHealth;
            _lastSoul = soul;

            _network.SendHealthReport(
                RoundClientManager.CurrentRoundId,
                health,
                maxHealth,
                blueHealth,
                soul);
        }


        private static int GetCurrentSoul(
            PlayerData data)
        {
            if (data == null)
            {
                return 0;
            }

            int soul =
                Math.Max(
                    0,
                    data.MPCharge) +
                Math.Max(
                    0,
                    data.MPReserve);

            return Math.Min(
                198,
                soul);
        }

        private static void CreateReportBehaviour()
        {
            if (_reportBehaviour != null)
            {
                return;
            }

            _reportObject =
                GameObject.Find(
                    "HKMP.Rounds.HealthReporter");

            if (_reportObject == null)
            {
                _reportObject =
                    new GameObject(
                        "HKMP.Rounds.HealthReporter");

                UnityEngine.Object.DontDestroyOnLoad(
                    _reportObject);
            }

            _reportBehaviour =
                _reportObject.GetComponent<
                    HealthReportBehaviour>();

            if (_reportBehaviour == null)
            {
                _reportBehaviour =
                    _reportObject.AddComponent<
                        HealthReportBehaviour>();
            }
        }

        private sealed class HealthReportBehaviour :
            MonoBehaviour
        {
            private float _nextReportTime;

            private void OnEnable()
            {
                _nextReportTime =
                    Time.unscaledTime;
            }

            private void Update()
            {
                if (!RoundClientManager.IsRoundActive ||
                    PlayerData.instance == null)
                {
                    return;
                }

                if (Time.unscaledTime <
                    _nextReportTime)
                {
                    return;
                }

                _nextReportTime =
                    Time.unscaledTime +
                    ReportIntervalSeconds;

                ReportCurrentHealth();
            }
        }

        private static int GetEffectiveMaxHealth(
            PlayerData data)
        {
            if (data == null)
            {
                return 0;
            }

            int maxHealth =
                Math.Max(
                    0,
                    data.maxHealth);

            int maxHealthBase =
                Math.Max(
                    0,
                    data.maxHealthBase);

            maxHealth =
                Math.Max(
                    maxHealth,
                    maxHealthBase);

            bool heartEquipped = false;

            try
            {
                heartEquipped =
                    data.GetBool(
                        nameof(PlayerData.equippedCharm_23));
            }
            catch
            {
            }

            if (heartEquipped &&
                maxHealthBase > 0)
            {
                maxHealth =
                    Math.Max(
                        maxHealth,
                        maxHealthBase + 2);
            }

            return maxHealth;
        }

        public static void Reset()
        {
            _lastHealth =
                int.MinValue;

            _lastMaxHealth =
                int.MinValue;

            _lastBlueHealth =
                int.MinValue;

            _lastSoul =
                int.MinValue;
        }

        public static void Clear()
        {
            Reset();
        }
    }
}