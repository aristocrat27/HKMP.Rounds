using System.Collections.Generic;

using HKMP.Rounds.Client.Combat;

namespace HKMP.Rounds.Client
{
    internal static class RoundClientManager
    {
        private static bool _roundActive;

        private static uint _currentRoundId;

        private static readonly HashSet<ushort> _deadPlayers =
            new HashSet<ushort>();

        public static bool IsRoundActive
        {
            get
            {
                return _roundActive;
            }
        }

        public static uint CurrentRoundId
        {
            get
            {
                return _currentRoundId;
            }
        }

        public static void StartRound(
            uint roundId)
        {
            _roundActive = true;

            _currentRoundId =
                roundId;

            _deadPlayers.Clear();

            ClientDeathTracker.Reset();

            LastAttackerTracker.Clear();
        }

        public static void EndRound(
            uint roundId)
        {
            if (!_roundActive)
            {
                return;
            }

            if (roundId != _currentRoundId)
            {
                return;
            }

            _roundActive = false;

            _deadPlayers.Clear();

            ClientDeathTracker.Clear();

            LastAttackerTracker.Clear();
        }

        public static void MarkPlayerDead(
            uint roundId,
            ushort playerId)
        {
            if (!_roundActive)
            {
                return;
            }

            if (roundId != _currentRoundId)
            {
                return;
            }

            _deadPlayers.Add(
                playerId
            );
        }

        public static bool IsPlayerDead(
            ushort playerId)
        {
            return _deadPlayers.Contains(
                playerId
            );
        }

        public static void Clear()
        {
            _roundActive = false;

            _currentRoundId = 0;

            _deadPlayers.Clear();

            LastAttackerTracker.Clear();
        }
    }
}