using System;

using GlobalEnums;

using Modding;

using UnityEngine;

using Object = UnityEngine.Object;

namespace HKMP.Rounds.Client
{
    internal static class RoundPreparation
    {
        private const int RoundSoul = 198;

        public static void Prepare(
            uint roundId,
            Action prepared)
        {
            try
            {
                KillAllLocalEnemies();

                HeroController hero =
                    HeroController.instance;

                if (hero == null)
                {
                    prepared?.Invoke();
                    return;
                }

                hero.StartCoroutine(
                    FinishPreparation(
                        roundId,
                        prepared
                    )
                );
            }
            catch (Exception exception)
            {
                Modding.Logger.LogError(
                    "[HKMP.Rounds] Round preparation failed. " +
                    "RoundId=" +
                    roundId +
                    " Exception=" +
                    exception
                );

                prepared?.Invoke();
            }
        }

        private static System.Collections.IEnumerator FinishPreparation(
            uint roundId,
            Action prepared)
        {
            yield return null;

            try
            {
                SetFullRoundSoul();

                prepared?.Invoke();
            }
            catch (Exception exception)
            {
                Modding.Logger.LogError(
                    "[HKMP.Rounds] Round preparation completion failed. " +
                    "RoundId=" +
                    roundId +
                    " Exception=" +
                    exception
                );

                prepared?.Invoke();
            }
        }

        private static void KillAllLocalEnemies()
        {
            HealthManager[] healthManagers =
                Object.FindObjectsOfType<HealthManager>();

            if (healthManagers == null)
            {
                return;
            }

            int killedCount = 0;

            for (
                int i = 0;
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

                healthManager.Die(
                    null,
                    AttackTypes.Generic,
                    true
                );

                killedCount++;
            }

            Modding.Logger.Log(
                "[HKMP.Rounds] Local Kill All completed. " +
                "Killed=" +
                killedCount
            );
        }

        private static void SetFullRoundSoul()
        {
            if (PlayerData.instance == null ||
                HeroController.instance == null)
            {
                return;
            }

            PlayerData.instance.soulLimited = false;

            HeroController.instance.SetMPCharge(
                0
            );

            PlayerData.instance.MPCharge = 0;
            PlayerData.instance.MPReserve = 0;

            HeroController.instance.AddMPCharge(
                RoundSoul
            );

            Modding.Logger.Log(
                "[HKMP.Rounds] Round Soul set to " +
                RoundSoul +
                " MP (6 casts). " +
                "MPCharge=" +
                PlayerData.instance.MPCharge +
                " MPReserve=" +
                PlayerData.instance.MPReserve
            );
        }
    }
}