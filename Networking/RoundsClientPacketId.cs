namespace HKMP.Rounds.Networking
{
    internal enum RoundsClientPacketId : byte
    {
        PrepareRound = 0,
        RoundStart = 1,
        PlayerDeath = 2,
        RoundEnd = 3,
        DeathResolution = 4,
        BenchSettings = 5,
        PvpMode = 6,
        ManaUpdate = 7,
        RoundSettings = 8,
        RoundStats = 9,
        EmergencyWarp = 10,
        WarpToBench = 11,
        MapTravelState = 12,
        MapTravelCommand = 13,
        MapPresetCommand = 14,
        GameFreezeState = 15
    }
}
