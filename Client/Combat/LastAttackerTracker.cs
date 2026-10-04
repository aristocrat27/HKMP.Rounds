using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

using GlobalEnums;
using Modding;
using MonoMod.RuntimeDetour;
using UnityEngine;

namespace HKMP.Rounds.Client.Combat
{
    internal static class LastAttackerTracker
    {
        private static bool _initialized;

        private static ushort? _lastAttackerId;

        private static float _lastAttackTime =
            -Mathf.Infinity;

        private const float LastAttackFallbackWindow =
            2.0f;

        private static readonly Dictionary<int, ushort>
            _attackOwners =
                new Dictionary<int, ushort>();

        private static readonly List<Hook>
            _hkmpHooks =
                new List<Hook>();

        private static readonly string[]
            _hkmpAttackTypes =
            {
                "Hkmp.Animation.Effects.VengefulSpirit",
                "Hkmp.Animation.Effects.ShadeSoul",
                "Hkmp.Animation.Effects.Slash",
                "Hkmp.Animation.Effects.AltSlash",
                "Hkmp.Animation.Effects.DownSlash",
                "Hkmp.Animation.Effects.UpSlash",
                "Hkmp.Animation.Effects.WallSlash"
            };

        public static bool HasAttacker
        {
            get
            {
                return _lastAttackerId.HasValue;
            }
        }

        public static ushort LastAttackerId
        {
            get
            {
                if (_lastAttackerId.HasValue)
                {
                    return _lastAttackerId.Value;
                }

                return ushort.MaxValue;
            }
        }

        public static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            _lastAttackerId = null;

            _lastAttackTime =
                -Mathf.Infinity;

            _attackOwners.Clear();

            On.HeroController.TakeDamage +=
                OnHeroTakeDamage;

            InstallHkmpAttackHooks();
        }

        private static void InstallHkmpAttackHooks()
        {
            Assembly hkmpAssembly =
                FindHkmpAssembly();

            if (hkmpAssembly == null)
            {
                Modding.Logger.LogError(
                    "[HKMP.Rounds] HKMP assembly could not be resolved through ModHooks.GetMod(\"HKMP\").");

                return;
            }

            for (
                int i = 0;
                i < _hkmpAttackTypes.Length;
                i++
            )
            {
                InstallHkmpPlayHook(
                    hkmpAssembly,
                    _hkmpAttackTypes[i]);
            }
        }

        private static Assembly FindHkmpAssembly()
        {
            try
            {
                Mod hkmpMod =
                    (Mod)ModHooks.GetMod(
                        "HKMP");

                if (hkmpMod == null)
                {
                    return null;
                }

                Type hkmpType =
                    hkmpMod.GetType();

                if (hkmpType == null)
                {
                    return null;
                }

                return hkmpType.Assembly;
            }
            catch (Exception exception)
            {
                Modding.Logger.LogError(
                    "[HKMP.Rounds] Failed to resolve HKMP assembly through ModHooks. " +
                    "Exception=" +
                    exception);

                return null;
            }
        }


        private static void InstallHkmpPlayHook(
            Assembly hkmpAssembly,
            string typeName)
        {
            Type effectType =
                hkmpAssembly.GetType(
                    typeName,
                    false);

            if (effectType == null)
            {
                return;
            }

            MethodInfo playMethod =
                effectType.GetMethod(
                    "Play",
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic,
                    null,
                    new Type[]
                    {
                        typeof(GameObject),
                        typeof(bool[])
                    },
                    null);

            if (playMethod == null ||
                playMethod.ReturnType != typeof(void))
            {
                return;
            }

            try
            {
                Type selfType =
                    effectType;

                Type origDelegateType =
                    Expression.GetDelegateType(
                        selfType,
                        typeof(GameObject),
                        typeof(bool[]),
                        typeof(void));

                Type hookDelegateType =
                    Expression.GetDelegateType(
                        origDelegateType,
                        selfType,
                        typeof(GameObject),
                        typeof(bool[]),
                        typeof(void));

                MethodInfo genericHookMethod =
                    typeof(LastAttackerTracker).GetMethod(
                        "OnHkmpPlayGeneric",
                        BindingFlags.Static |
                        BindingFlags.NonPublic);

                if (genericHookMethod == null)
                {
                    return;
                }

                MethodInfo closedHookMethod =
                    genericHookMethod.MakeGenericMethod(
                        origDelegateType,
                        selfType);

                Delegate hookDelegate =
                    Delegate.CreateDelegate(
                        hookDelegateType,
                        closedHookMethod);

                Hook hook =
                    new Hook(
                        playMethod,
                        hookDelegate);

                _hkmpHooks.Add(
                    hook);
            }
            catch (Exception exception)
            {
                Modding.Logger.LogError(
                    "[HKMP.Rounds] Failed to install HKMP Play hook: " +
                    typeName +
                    " Exception=" +
                    exception);
            }
        }

        private static void OnHkmpPlayGeneric<TOrig, TSelf>(
            TOrig orig,
            TSelf self,
            GameObject playerObject,
            bool[] effectInfo)
            where TOrig : class
        {
            HashSet<int> existingDamageHeroes =
                CaptureDamageHeroIds();

            ushort attackerId;

            bool attackerResolved =
                TryGetPlayerIdFromObject(
                    playerObject,
                    out attackerId);

            if (
                attackerResolved &&
                RoundClientManager.IsRoundActive)
            {
                _lastAttackerId =
                    attackerId;

            }

            origDynamic(
                orig,
                self,
                playerObject,
                effectInfo);

            if (!attackerResolved ||
                !RoundClientManager.IsRoundActive)
            {
                return;
            }

            RegisterNewDamageHeroes(
                existingDamageHeroes,
                attackerId,
                typeof(TSelf).Name);
        }

        private static void origDynamic<TOrig, TSelf>(
            TOrig orig,
            TSelf self,
            GameObject playerObject,
            bool[] effectInfo)
            where TOrig : class
        {
            Delegate delegateValue =
                orig as Delegate;

            if (delegateValue == null)
            {
                throw new InvalidOperationException(
                    "[HKMP.Rounds] HKMP original delegate is null.");
            }

            delegateValue.DynamicInvoke(
                self,
                playerObject,
                effectInfo);
        }

        private static HashSet<int> CaptureDamageHeroIds()
        {
            HashSet<int> result =
                new HashSet<int>();

            DamageHero[] damageHeroes =
                UnityEngine.Object.FindObjectsOfType<DamageHero>();

            if (damageHeroes == null)
            {
                return result;
            }

            for (
                int i = 0;
                i < damageHeroes.Length;
                i++)
            {
                DamageHero damageHero =
                    damageHeroes[i];

                if (damageHero == null ||
                    damageHero.gameObject == null)
                {
                    continue;
                }

                result.Add(
                    damageHero.gameObject.GetInstanceID());
            }

            return result;
        }

        private static void RegisterNewDamageHeroes(
            HashSet<int> existingDamageHeroes,
            ushort attackerId,
            string attackType)
        {
            if (!RoundClientManager.IsRoundActive)
            {
                return;
            }

            DamageHero[] damageHeroes =
                UnityEngine.Object.FindObjectsOfType<DamageHero>();

            if (damageHeroes == null)
            {
                return;
            }

            for (
                int i = 0;
                i < damageHeroes.Length;
                i++)
            {
                DamageHero damageHero =
                    damageHeroes[i];

                if (damageHero == null ||
                    damageHero.gameObject == null)
                {
                    continue;
                }

                int instanceId =
                    damageHero.gameObject.GetInstanceID();

                if (existingDamageHeroes.Contains(instanceId))
                {
                    continue;
                }

                _attackOwners[
                    instanceId] = attackerId;
            }
        }

        private static void OnHeroTakeDamage(
            On.HeroController.orig_TakeDamage orig,
            HeroController self,
            GameObject go,
            CollisionSide damageSide,
            int damageAmount,
            int hazardType)
        {
            ushort attackerId;

            bool attackerFound =
                TryResolveAttacker(
                    go,
                    out attackerId);

            if (attackerFound)
            {
                _lastAttackerId =
                    attackerId;

            }
            else if (
                RoundClientManager.IsRoundActive &&
                HasDamageHeroInHierarchy(go) &&
                TryGetRecentAttacker(out attackerId))
            {
                attackerFound = true;
            }

            orig(
                self,
                go,
                damageSide,
                damageAmount,
                hazardType);

            if (!attackerFound ||
                !RoundClientManager.IsRoundActive ||
                self == null ||
                HeroController.instance == null ||
                self != HeroController.instance ||
                self.playerData == null)
            {
                return;
            }

            int health =
                self.playerData.GetInt(
                    "health");

            if (health > 0)
            {
                return;
            }

            ClientDeathTracker.ReportPvpDeath(
                attackerId);
        }

        private static bool TryResolveAttacker(
            GameObject source,
            out ushort attackerId)
        {
            attackerId = 0;

            if (source == null)
            {
                return false;
            }

            int instanceId =
                source.GetInstanceID();

            ushort registeredId;

            if (_attackOwners.TryGetValue(
                instanceId,
                out registeredId))
            {
                attackerId =
                    registeredId;

                return true;
            }

            Transform current =
                source.transform;

            while (current != null)
            {
                if (TryParsePlayerContainer(
                    current.name,
                    out attackerId))
                {
                    return true;
                }

                current =
                    current.parent;
            }

            return false;
        }

        private static bool HasDamageHeroInHierarchy(
            GameObject source)
        {
            if (source == null)
            {
                return false;
            }

            Transform current =
                source.transform;

            while (current != null)
            {
                if (current.GetComponent<DamageHero>() != null)
                {
                    return true;
                }

                current =
                    current.parent;
            }

            return false;
        }

        private static bool TryGetRecentAttacker(
            out ushort playerId)
        {
            playerId = 0;

            if (!_lastAttackerId.HasValue ||
                !RoundClientManager.IsRoundActive ||
                float.IsNegativeInfinity(_lastAttackTime))
            {
                return false;
            }

            float age =
                Time.time -
                _lastAttackTime;

            if (age < 0f ||
                age > LastAttackFallbackWindow)
            {
                return false;
            }

            playerId =
                _lastAttackerId.Value;

            return true;
        }

        private static bool TryGetPlayerIdFromObject(
            GameObject playerObject,
            out ushort playerId)
        {
            playerId = 0;

            if (playerObject == null)
            {
                return false;
            }

            Transform current =
                playerObject.transform;

            while (current != null)
            {
                if (TryParsePlayerContainer(
                    current.name,
                    out playerId))
                {
                    return true;
                }

                current =
                    current.parent;
            }

            return false;
        }

        private static bool TryParsePlayerContainer(
            string objectName,
            out ushort playerId)
        {
            playerId = 0;

            if (string.IsNullOrEmpty(objectName))
            {
                return false;
            }

            const string prefix =
                "Player Container ";

            if (!objectName.StartsWith(
                prefix,
                StringComparison.Ordinal))
            {
                return false;
            }

            string idText =
                objectName.Substring(
                    prefix.Length);

            ushort parsedId;

            if (!ushort.TryParse(
                idText,
                out parsedId))
            {
                return false;
            }

            playerId =
                parsedId;

            return true;
        }

        public static bool TryGetLastAttacker(
            out ushort playerId)
        {
            if (!_lastAttackerId.HasValue)
            {
                playerId = 0;
                return false;
            }

            playerId =
                _lastAttackerId.Value;

            return true;
        }

        public static void Clear()
        {
            _lastAttackerId = null;

            _lastAttackTime =
                -Mathf.Infinity;

            _attackOwners.Clear();
        }
    }
}
