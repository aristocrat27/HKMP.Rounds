using System;
using System.Collections.Generic;
using HKMP.Rounds.Settings;
namespace HKMP.Rounds.Server
{
    internal sealed class OneShotManaManager
    {
        public const int InitialMana = PvpModeRules.OneShotStartingMana;

        private readonly Dictionary<ushort, int> _mana = new Dictionary<ushort, int>();
        private readonly HashSet<ushort> _deadPlayers = new HashSet<ushort>();

        public void Initialize(IEnumerable<ushort> participants)
        {
            Clear();
            if (participants == null) return;
            foreach (ushort id in participants)
            {
                _mana[id] = InitialMana;
            }
        }

        public int GetMana(ushort playerId)
        {
            int value;
            return _mana.TryGetValue(playerId, out value) ? value : 0;
        }

        public bool IsDead(ushort playerId)
        {
            return _deadPlayers.Contains(playerId);
        }

        public int Spend(ushort playerId, int requestedAmount)
        {
            if (requestedAmount <= 0 || IsDead(playerId)) return GetMana(playerId);
            int current;
            if (!_mana.TryGetValue(playerId, out current)) return 0;
            _mana[playerId] = Math.Max(0, current - requestedAmount);
            return _mana[playerId];
        }

        public bool TryResolveKill(ushort killerId, ushort victimId, out int killerMana, out int victimMana)
        {
            killerMana = GetMana(killerId);
            victimMana = GetMana(victimId);
            if (killerId == victimId ||
                !_mana.ContainsKey(killerId) ||
                !_mana.ContainsKey(victimId) ||
                _deadPlayers.Contains(killerId) ||
                _deadPlayers.Contains(victimId))
            {
                return false;
            }

            int reward = Math.Max(InitialMana, victimMana);
            _deadPlayers.Add(victimId);
            _mana[victimId] = 0;
            _mana[killerId] = killerMana > int.MaxValue - reward ? int.MaxValue : killerMana + reward;
            killerMana = _mana[killerId];
            victimMana = 0;
            return true;
        }

        public int MarkDead(ushort playerId)
        {
            if (_mana.ContainsKey(playerId))
            {
                _deadPlayers.Add(playerId);
                _mana[playerId] = 0;
            }

            return GetMana(playerId);
        }

        public void RemovePlayer(ushort playerId)
        {
            _mana.Remove(playerId);
            _deadPlayers.Remove(playerId);
        }

        public void Clear()
        {
            _mana.Clear();
            _deadPlayers.Clear();
        }
    }
}
