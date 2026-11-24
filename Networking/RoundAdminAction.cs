namespace HKMP.Rounds.Networking
{
    internal enum RoundAdminAction : byte
    {
        EndRound = 0,
        SetWinTarget = 1,
        ResetStatistics = 2,
        RequestStatistics = 3,
        ToggleReady = 4,
        WarpPlayerToBench = 5,
        WarpSelfToNearestBench = 6,
        ToggleGameFreeze = 7
    }
}
