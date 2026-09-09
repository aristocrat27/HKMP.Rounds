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

            Modding.Logger.Log(
                "[HKMP.Rounds] ClientDeathTracker initialized."
            );
        }

        public static void ReportPvpDeath(
            ushort killerId)
        {
            if (!_initialized)
            {
                return;
            }

            if (!RoundClientManager.IsRoundActive)
            {
                return;
            }

            if (_deathReported)
            {
                return;
            }

            if (_network == null)
            {
                return;
            }

            _deathReported = true;

            _network.SendDeathReport(
                killerId
            );
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