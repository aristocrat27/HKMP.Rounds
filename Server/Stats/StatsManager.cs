using Modding;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HKMP.Rounds.Server.Stats
{
    internal sealed class StatsManager
    {
        private readonly Dictionary<string, StatsData> _players =
            new Dictionary<string, StatsData>(
                StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, StatsData> _teams =
            new Dictionary<string, StatsData>(
                StringComparer.OrdinalIgnoreCase);

        public int PlayerCount
        {
            get
            {
                return _players.Count;
            }
        }

        public int TeamCount
        {
            get
            {
                return _teams.Count;
            }
        }

        public void RegisterPlayer(
            string playerName)
        {
            if (string.IsNullOrWhiteSpace(playerName))
            {
                return;
            }

            string name =
                playerName.Trim();

            if (_players.ContainsKey(name))
            {
                return;
            }

            _players.Add(
                name,
                new StatsData(
                    name,
                    false));

            Modding.Logger.Log(
                "[HKMP.Rounds] Stats player registered: " +
                name);
        }

        public bool AddWin(
            string playerName)
        {
            if (string.IsNullOrWhiteSpace(playerName))
            {
                return false;
            }

            string name =
                playerName.Trim();

            StatsData stats;

            if (!_players.TryGetValue(
                name,
                out stats))
            {
                stats =
                    new StatsData(
                        name,
                        false);

                _players.Add(
                    name,
                    stats);
            }

            stats.AddWin();

            Modding.Logger.Log(
                "[HKMP.Rounds] Player win recorded: " +
                name +
                " = " +
                stats.Wins);

            return true;
        }

        public bool AddTeamWin(
            string teamName)
        {
            if (string.IsNullOrWhiteSpace(teamName))
            {
                return false;
            }

            string name =
                teamName.Trim();

            StatsData stats;

            if (!_teams.TryGetValue(
                name,
                out stats))
            {
                stats =
                    new StatsData(
                        name,
                        true);

                _teams.Add(
                    name,
                    stats);
            }

            stats.AddWin();

            Modding.Logger.Log(
                "[HKMP.Rounds] Team win recorded: " +
                name +
                " = " +
                stats.Wins);

            return true;
        }

        public bool TryGetStats(
            string name,
            out StatsData stats)
        {
            stats = null;

            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            string key =
                name.Trim();

            if (_players.TryGetValue(
                key,
                out stats))
            {
                return true;
            }

            return _teams.TryGetValue(
                key,
                out stats);
        }

        public IReadOnlyList<StatsData> GetSortedStats()
        {
            return _players.Values
                .Concat(_teams.Values)
                .OrderByDescending(
                    x => x.Wins)
                .ThenBy(
                    x => x.PlayerName,
                    StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public void Reset()
        {
            _players.Clear();
            _teams.Clear();

            Modding.Logger.Log(
                "[HKMP.Rounds] Statistics reset.");
        }
    }
}
