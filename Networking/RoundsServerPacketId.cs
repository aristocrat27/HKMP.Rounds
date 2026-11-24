namespace HKMP.Rounds.Networking
{
    internal enum RoundsServerPacketId : byte
    {
        Prepared = 0,
        DeathReport = 1,
        HealthReport = 2,
        BenchSettingsRequest = 3,
        PvpModeRequest = 4,
        ManaSpent = 5,
        RoundSettingsRequest = 6,
        RoundAdminAction = 7,
        PlayerBenchState = 8,
        MapTravelRequest = 9
    }
}
