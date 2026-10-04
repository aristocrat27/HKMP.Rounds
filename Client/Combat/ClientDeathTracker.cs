using Modding;

namespace HKMP.Rounds.Client.Combat
{
    internal static class ClientDeathTracker
    {
        private static bool _initialized;
        private static bool _deathReported;
        private static ClientNetManager _network;

        public static void Initialize(
            ClientNetManager network)
        {
            if (_initialized)
            {
                _network = network;
                return;
            }

            _initialized = true;
            _deathReported = false;
            _network = network;

            ModHooks.BeforePlayerDeadHook +=
                OnBeforePlayerDead;
        }

        public static void ReportPvpDeath(
            ushort killerId)
        {
            if (!_initialized ||
                !RoundClientManager.IsRoundActive ||
                _network == null)
            {
                return;
            }

            RoundPreparation.NotifyLocalDeathStarted();
            ClientHealthTracker.ReportCurrentHealth();

            if (_deathReported)
            {
                return;
            }

            _deathReported = true;

            _network.SendDeathReport(
                killerId);
        }

        public static void ReportNonPvpDeath()
        {
            if (!_initialized ||
                !RoundClientManager.IsRoundActive ||
                _network == null)
            {
                return;
            }

            RoundPreparation.NotifyLocalDeathStarted();
            ClientHealthTracker.ReportCurrentHealth();

            if (_deathReported)
            {
                return;
            }

            _deathReported = true;

            _network.SendNonPvpDeathReport(
                RoundClientManager.CurrentRoundId);
        }

        private static void OnBeforePlayerDead()
        {
            if (!_initialized ||
                !RoundClientManager.IsRoundActive ||
                _network == null)
            {
                return;
            }

            RoundPreparation.NotifyLocalDeathStarted();
            ClientHealthTracker.ReportCurrentHealth();

            if (_deathReported)
            {
                return;
            }

            uint roundId =
                RoundClientManager.CurrentRoundId;

            HeroController hero =
                HeroController.instance;

            if (hero == null)
            {
                ReportNonPvpDeath(
                    roundId);
                return;
            }

            hero.StartCoroutine(
                ReportNonPvpDeathAfterCurrentDeath(
                    roundId));
        }

        private static System.Collections.IEnumerator ReportNonPvpDeathAfterCurrentDeath(
            uint roundId)
        {
            yield return null;

            if (!RoundClientManager.IsRoundActive ||
                RoundClientManager.CurrentRoundId != roundId ||
                _deathReported ||
                _network == null)
            {
                yield break;
            }

            ReportNonPvpDeath(
                roundId);
        }

        private static void ReportNonPvpDeath(
            uint roundId)
        {
            if (!_initialized ||
                !RoundClientManager.IsRoundActive ||
                RoundClientManager.CurrentRoundId != roundId ||
                _network == null)
            {
                return;
            }

            RoundPreparation.NotifyLocalDeathStarted();
            ClientHealthTracker.ReportCurrentHealth();

            if (_deathReported)
            {
                return;
            }

            _deathReported = true;

            _network.SendNonPvpDeathReport(
                roundId);
        }

        public static void Reset()
        {
            _deathReported = false;
            LastAttackerTracker.Clear();
        }

        public static void Clear()
        {
            _deathReported = false;
            LastAttackerTracker.Clear();
        }
    }
}