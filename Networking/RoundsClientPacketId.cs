namespace HKMP.Rounds.Networking
{
    internal enum RoundsClientPacketId : byte
    {
        PrepareRound = 0,
        RoundStart = 1,
        PlayerDeath = 2,
        RoundEnd = 3
    }
}
