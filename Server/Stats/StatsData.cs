namespace HKMP.Rounds.Server.Stats
{
    internal sealed class StatsData
    {
        public string PlayerName
        {
            get;
            private set;
        }

        public bool IsTeam
        {
            get;
            private set;
        }

        public int Wins
        {
            get;
            private set;
        }

        public StatsData(
            string playerName)
            : this(
                playerName,
                false)
        {
        }

        public StatsData(
            string playerName,
            bool isTeam)
        {
            PlayerName =
                playerName == null
                    ? ""
                    : playerName.Trim();

            IsTeam =
                isTeam;
        }

        public void AddWin()
        {
            Wins++;
        }
    }
}
