namespace HKMP.Rounds.Networking
{
    internal enum BenchSettingsAction : byte
    {
        RequestState = 0,
        Discover = 1,
        SetHere = 2,
        SetSelected = 3,
        ToggleLock = 4,
        HostSatAtBench = 7
    }
}
