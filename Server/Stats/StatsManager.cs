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


        public IReadOnlyList<StatsData> GetSortedStats()
        {
            return GetSortedStats(false, false);
        }

        public IReadOnlyList<StatsData> GetSortedStats(
            bool teamsEnabled,
            bool individualTeamWins)
        {
            IEnumerable<StatsData> source;
            if (teamsEnabled && !individualTeamWins)
            {
                source = _teams.Values;
            }
            else
            {
                source = _players.Values;
            }

            return source
                .OrderByDescending(x => x.Wins)
                .ThenBy(x => x.PlayerName, StringComparer.OrdinalIgnoreCase)
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
