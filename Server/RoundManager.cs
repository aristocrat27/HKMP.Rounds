using System.Collections.Generic;
using System.Linq;

using Hkmp.Api.Server;

using HKMP.Rounds.Server.Stats;
using HKMP.Rounds.Settings;

namespace HKMP.Rounds.Server
{
    internal sealed class RoundManager
    {
        private readonly IServerApi _serverApi;
        private readonly StatsManager _statsManager;
        private readonly GlobalSettingsStore _settings;

        private RoundServerNetManager _network;

        private readonly HashSet<ushort> _participants =
            new HashSet<ushort>();

        private readonly HashSet<ushort> _alivePlayers =
            new HashSet<ushort>();

        private readonly HashSet<ushort> _preparedPlayers =
            new HashSet<ushort>();

        private readonly HashSet<ushort> _readyPlayers =
            new HashSet<ushort>();

        private bool _roundActive;
        private bool _waitingForPreparation;

        private uint _currentRoundId;

        public bool IsRoundActive
        {
            get
            {
                return _roundActive;
            }
        }

        public bool IsWaitingForPreparation
        {
            get
            {
                return _waitingForPreparation;
            }
        }

        public uint CurrentRoundId
        {
            get
            {
                return _currentRoundId;
            }
        }

        public int AlivePlayerCount
        {
            get
            {
                return _alivePlayers.Count;
            }
        }

        public int ParticipantCount
        {
            get
            {
                return _participants.Count;
            }
        }

        public int PreparedPlayerCount
        {
            get
            {
                return _preparedPlayers.Count;
            }
        }

        public int ReadyPlayerCount
        {
            get
            {
                return _readyPlayers.Count;
            }
        }

        public bool AutoStartEnabled
        {
            get
            {
                return _settings.AutoStart;
            }
        }

        public RoundManager(
            IServerApi serverApi,
            StatsManager statsManager,
            GlobalSettingsStore settings)
        {
            _serverApi = serverApi;
            _statsManager = statsManager;
            _settings = settings;
        }

        public void SetNetwork(
            RoundServerNetManager network)
        {
            _network = network;
        }

        public bool IsAlive(
            ushort playerId)
        {
            return _alivePlayers.Contains(
                playerId
            );
        }

        public bool IsParticipant(
            ushort playerId)
        {
            return _participants.Contains(
                playerId
            );
        }

        private uint StartNewRoundId()
        {
            if (_currentRoundId == uint.MaxValue)
            {
                _currentRoundId = 1;
            }
            else
            {
                _currentRoundId++;
            }

            if (_currentRoundId == 0)
            {
                _currentRoundId = 1;
            }

            return _currentRoundId;
        }

        public void OnPlayerConnect(
            IServerPlayer player)
        {
            if (player == null)
            {
                return;
            }

            _statsManager.RegisterPlayer(
                player.Username
            );
        }

        public void OnPlayerDisconnect(
            ushort playerId)
        {
            _readyPlayers.Remove(
                playerId
            );

            if (_waitingForPreparation)
            {
                if (_participants.Remove(
                    playerId))
                {
                    _preparedPlayers.Remove(
                        playerId
                    );

                    if (_participants.Count < 2)
                    {
                        AbortPreparation();
                        return;
                    }

                    TryActivateRound();
                }

                return;
            }

            if (!_roundActive)
            {
                return;
            }

            if (!_alivePlayers.Remove(
                playerId))
            {
                return;
            }

            CheckRoundEnd();
        }

        public void SetAutoStart(
            bool enabled)
        {
            _settings.SetAutoStart(
                enabled
            );

            if (!enabled)
            {
                _readyPlayers.Clear();
                return;
            }

            if (!_roundActive &&
                !_waitingForPreparation)
            {
                CheckAutoStart();
            }
        }

        public bool MarkReady(
            ushort playerId)
        {
            if (!AutoStartEnabled)
            {
                return false;
            }

            if (_roundActive ||
                _waitingForPreparation)
            {
                return false;
            }

            IServerPlayer player;

            if (!_serverApi.ServerManager.TryGetPlayer(
                playerId,
                out player))
            {
                return false;
            }

            if (player == null)
            {
                return false;
            }

            _statsManager.RegisterPlayer(
                player.Username
            );

            bool wasAlreadyReady =
                _readyPlayers.Contains(
                    playerId
                );

            _readyPlayers.Add(
                playerId
            );

            if (!wasAlreadyReady)
            {
                int playerCount =
                    _serverApi.ServerManager.Players.Count;

                _serverApi.ServerManager.BroadcastMessage(
                    "Готово " +
                    _readyPlayers.Count +
                    "/" +
                    playerCount +
                    " игроков"
                );
            }

            CheckAutoStart();

            return true;
        }

        public bool ClearReady(
            ushort playerId)
        {
            if (_roundActive ||
                _waitingForPreparation)
            {
                return false;
            }

            return _readyPlayers.Remove(
                playerId
            );
        }

        public bool StartRound()
        {
            if (_roundActive ||
                _waitingForPreparation)
            {
                return false;
            }

            IReadOnlyCollection<IServerPlayer> players =
                _serverApi.ServerManager.Players;

            if (players == null ||
                players.Count < 2)
            {
                return false;
            }

            _participants.Clear();
            _alivePlayers.Clear();
            _preparedPlayers.Clear();

            foreach (IServerPlayer player in players)
            {
                if (player == null)
                {
                    continue;
                }

                _participants.Add(
                    player.Id
                );

                _statsManager.RegisterPlayer(
                    player.Username
                );
            }

            if (_participants.Count < 2)
            {
                _participants.Clear();
                return false;
            }

            _currentRoundId =
                StartNewRoundId();

            _waitingForPreparation = true;
            _roundActive = false;

            foreach (ushort playerId in _participants)
            {
                if (_network == null)
                {
                    continue;
                }

                _network.SendPrepareRound(
                    playerId,
                    _currentRoundId
                );
            }

            return true;
        }

        public void OnPlayerPrepared(
            ushort playerId,
            uint roundId)
        {
            if (!_waitingForPreparation)
            {
                return;
            }

            if (roundId != _currentRoundId)
            {
                return;
            }

            if (!_participants.Contains(
                playerId))
            {
                return;
            }

            _preparedPlayers.Add(
                playerId
            );

            TryActivateRound();
        }

        private void TryActivateRound()
        {
            if (!_waitingForPreparation)
            {
                return;
            }

            List<ushort> disconnectedPlayers =
                new List<ushort>();

            foreach (ushort playerId in _participants)
            {
                IServerPlayer player;

                if (!_serverApi.ServerManager.TryGetPlayer(
                    playerId,
                    out player) ||
                    player == null)
                {
                    disconnectedPlayers.Add(
                        playerId
                    );
                }
            }

            for (
                int i = 0;
                i < disconnectedPlayers.Count;
                i++)
            {
                ushort playerId =
                    disconnectedPlayers[i];

                _participants.Remove(
                    playerId
                );

                _preparedPlayers.Remove(
                    playerId
                );

                _readyPlayers.Remove(
                    playerId
                );
            }

            if (_participants.Count < 2)
            {
                AbortPreparation();
                return;
            }

            if (_preparedPlayers.Count <
                _participants.Count)
            {
                return;
            }

            _alivePlayers.Clear();

            foreach (ushort playerId in _participants)
            {
                _alivePlayers.Add(
                    playerId
                );
            }

            _waitingForPreparation = false;
            _roundActive = true;

            _readyPlayers.Clear();

            foreach (ushort playerId in _participants)
            {
                if (_network == null)
                {
                    continue;
                }

                _network.SendRoundStart(
                    playerId,
                    _currentRoundId
                );
            }
        }

        private void AbortPreparation()
        {
            _waitingForPreparation = false;
            _roundActive = false;

            _participants.Clear();
            _preparedPlayers.Clear();
            _alivePlayers.Clear();
        }

        private void CheckAutoStart()
        {
            if (!AutoStartEnabled)
            {
                return;
            }

            if (_roundActive ||
                _waitingForPreparation)
            {
                return;
            }

            IReadOnlyCollection<IServerPlayer> players =
                _serverApi.ServerManager.Players;

            if (players == null ||
                players.Count < 2)
            {
                return;
            }

            foreach (IServerPlayer player in players)
            {
                if (player == null)
                {
                    return;
                }

                if (!_readyPlayers.Contains(
                    player.Id))
                {
                    return;
                }
            }

            StartRound();
        }

        public void OnPlayerDeath(
            ushort playerId)
        {
            if (!_roundActive)
            {
                return;
            }

            if (!_alivePlayers.Remove(
                playerId))
            {
                return;
            }

            CheckRoundEnd();
        }

        private void CheckRoundEnd()
        {
            if (_alivePlayers.Count == 1)
            {
                EndPlayerRound(
                    _alivePlayers.First()
                );

                return;
            }

            if (_alivePlayers.Count == 0)
            {
                _roundActive = false;
                return;
            }

            if (!_serverApi.ServerManager.ServerSettings.TeamsEnabled)
            {
                return;
            }

            object winningTeam;

            if (!TryGetTeamWinner(
                out winningTeam))
            {
                return;
            }

            EndTeamRound(
                winningTeam
            );
        }

        private bool TryGetTeamWinner(
            out object winningTeam)
        {
            winningTeam = null;

            HashSet<object> aliveTeams =
                new HashSet<object>();

            int unteamedPlayers = 0;

            foreach (ushort playerId in _alivePlayers)
            {
                IServerPlayer player;

                if (!_serverApi.ServerManager.TryGetPlayer(
                    playerId,
                    out player))
                {
                    continue;
                }

                if (player == null)
                {
                    continue;
                }

                object team =
                    player.Team;

                if (team == null ||
                    team.ToString() == "None")
                {
                    unteamedPlayers++;
                    continue;
                }

                aliveTeams.Add(
                    team
                );
            }

            if (unteamedPlayers > 0)
            {
                return false;
            }

            if (aliveTeams.Count == 0)
            {
                return false;
            }

            if (aliveTeams.Count > 1)
            {
                return false;
            }

            winningTeam =
                aliveTeams.First();

            return true;
        }

        private void EndPlayerRound(
            ushort winnerId)
        {
            if (!_roundActive)
            {
                return;
            }

            uint roundId =
                _currentRoundId;

            _roundActive = false;

            IServerPlayer winner;

            if (_serverApi.ServerManager.TryGetPlayer(
                winnerId,
                out winner) &&
                winner != null)
            {
                _serverApi.ServerManager.BroadcastMessage(
                    "Победитель раунда: " +
                    winner.Username
                );

                _statsManager.AddWin(
                    winner.Username
                );
            }

            if (_network != null)
            {
                _network.BroadcastRoundEnd(
                    roundId,
                    winnerId
                );
            }

            FinishRound(
                roundId
            );
        }

        private void EndTeamRound(
            object winningTeam)
        {
            if (!_roundActive)
            {
                return;
            }

            uint roundId =
                _currentRoundId;

            _roundActive = false;

            _serverApi.ServerManager.BroadcastMessage(
                "Победила команда: " +
                winningTeam
            );

            foreach (ushort playerId in _alivePlayers)
            {
                IServerPlayer player;

                if (!_serverApi.ServerManager.TryGetPlayer(
                    playerId,
                    out player))
                {
                    continue;
                }

                if (player == null)
                {
                    continue;
                }

                if (player.Team == null ||
                    !player.Team.Equals(
                        winningTeam))
                {
                    continue;
                }

                _statsManager.AddWin(
                    player.Username
                );
            }

            if (_network != null)
            {
                byte winnerTeam =
                    System.Convert.ToByte(
                        winningTeam
                    );

                _network.BroadcastTeamRoundEnd(
                    roundId,
                    winnerTeam
                );
            }

            FinishRound(
                roundId
            );
        }

        private void FinishRound(
            uint roundId)
        {
            _participants.Clear();
            _preparedPlayers.Clear();
            _alivePlayers.Clear();

            _waitingForPreparation = false;
            _roundActive = false;

            _readyPlayers.Clear();
        }

        public void Reset()
        {
            _roundActive = false;
            _waitingForPreparation = false;

            _participants.Clear();
            _preparedPlayers.Clear();
            _alivePlayers.Clear();
            _readyPlayers.Clear();
        }
    }
}