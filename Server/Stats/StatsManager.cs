using Modding;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HKMP.Rounds.Server.Stats
{
    internal sealed class StatsManager
    {
        private readonly Dictionary<string, StatsData> _players =
            new Dictionary<string, StatsData>(StringComparer.OrdinalIgnoreCase);

        public int PlayerCount { get { return _players.Count; } }

        public void RegisterPlayer(string playerName)
        {
            if (string.IsNullOrWhiteSpace(playerName)) return;

            string name = playerName.Trim();
            if (_players.ContainsKey(name)) return;

            _players.Add(name, new StatsData(name));
            Modding.Logger.Log("[HKMP.Rounds] Stats player registered: " + name);
        }

        public bool AddWin(string playerName)
        {
            if (string.IsNullOrWhiteSpace(playerName)) return false;

            string name = playerName.Trim();
            StatsData stats;

            if (!_players.TryGetValue(name, out stats))
            {
                stats = new StatsData(name);
                _players.Add(name, stats);
            }

            stats.AddWin();
            Modding.Logger.Log("[HKMP.Rounds] Win recorded: " + name + " = " + stats.Wins);
            return true;
        }

        public IReadOnlyList<StatsData> GetSortedStats()
        {
            return _players.Values
                .OrderByDescending(x => x.Wins)
                .ThenBy(x => x.PlayerName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public void Reset()
        {
            _players.Clear();
            Modding.Logger.Log("[HKMP.Rounds] Statistics reset.");
        }
    }
}
