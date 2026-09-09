namespace HKMP.Rounds.Server.Stats
{
    internal sealed class StatsData
    {
        public string PlayerName { get; private set; }
        public int Wins { get; private set; }

        public StatsData(string playerName)
        {
            PlayerName = playerName;
        }

        public void AddWin()
        {
            Wins++;
        }
    }
}
