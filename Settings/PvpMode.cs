namespace HKMP.Rounds.Settings
{
    internal enum PvpMode : byte
    {
        Quick = 0,
        Long = 1,
        OneShot = 2
    }

    internal static class PvpModeRules
    {
        public const int OneShotStartingMana = 33;
    }
}
