using System;
using System.Collections.Generic;
using System.Globalization;
using Hkmp.Api.Server;
using Hkmp.Api.Server.Networking;
using Hkmp.Networking.Packet;
using HKMP.Rounds.Networking;
using HKMP.Rounds.Networking.Packets;
using HKMP.Rounds.Settings;
using HKMP.Rounds.Server.Stats;
using System.Linq;
namespace HKMP.Rounds.Server
{
    internal sealed class RoundServerNetManager
    {
        private readonly IServerApi _serverApi;
        private readonly RoundManager _roundManager;
        private readonly GlobalSettingsStore _settings;
        private readonly StatsManager _statsManager;
        private readonly IServerAddonNetworkSender<RoundsClientPacketId> _netSender;
        private readonly IServerAddonNetworkReceiver<RoundsServerPacketId> _netReceiver;
        private readonly Dictionary<uint, HashSet<ushort>> _roundParticipants = new Dictionary<uint, HashSet<ushort>>();
        private readonly Dictionary<ushort, int> _playerPhantomMapIds = new Dictionary<ushort, int>();
        private readonly object _playerPhantomMapSync = new object();
        private ushort? _hostPlayerId;
        private BenchLocation _lastHostSatBench;
        private bool _gameFrozen;

        public RoundServerNetManager(
            RoundsServerAddon addon,
            IServerApi serverApi,
            RoundManager roundManager,
            GlobalSettingsStore settings,
            StatsManager statsManager)
        {
            _serverApi = serverApi;
            _roundManager = roundManager;
            _settings = settings;
            _statsManager = statsManager;
            _roundManager.SetPhantomMapIdResolver(GetPlayerPhantomMapId);

            if (serverApi.ServerManager.Players != null)
            {
                foreach (IServerPlayer player in serverApi.ServerManager.Players)
                {
                    if (player != null)
                    {
                        _hostPlayerId = player.Id;
                        break;
                    }
                }
            }

            if (_settings != null && _settings.HasSharedBench)
            {
                _lastHostSatBench = new BenchLocation(
                    _settings.SharedBenchScene,
                    _settings.SharedBenchMarker,
                    _settings.SharedBenchMapZone,
                    "Last host bench",
                    _settings.SharedBenchPhantomMapId);
            }

            _netSender = serverApi.NetServer.GetNetworkSender<RoundsClientPacketId>(addon);
            _netReceiver = serverApi.NetServer.GetNetworkReceiver<RoundsServerPacketId>(
                addon,
                InstantiatePacket);

            _netReceiver.RegisterPacketHandler<PreparedPacket>(
                RoundsServerPacketId.Prepared,
                OnPrepared);

            _netReceiver.RegisterPacketHandler<DeathReportPacket>(
                RoundsServerPacketId.DeathReport,
                OnDeathReport);

            _netReceiver.RegisterPacketHandler<HealthReportPacket>(
                RoundsServerPacketId.HealthReport,
                OnHealthReport);

            _netReceiver.RegisterPacketHandler<BenchSettingsRequestPacket>(
                RoundsServerPacketId.BenchSettingsRequest,
                OnBenchSettingsRequest);

            _netReceiver.RegisterPacketHandler<PvpModeRequestPacket>(
                RoundsServerPacketId.PvpModeRequest,
                OnPvpModeRequest);

            _netReceiver.RegisterPacketHandler<ManaSpentPacket>(
                RoundsServerPacketId.ManaSpent,
                OnManaSpent);

            _netReceiver.RegisterPacketHandler<RoundSettingsRequestPacket>(
                RoundsServerPacketId.RoundSettingsRequest,
                OnRoundSettingsRequest);

            _netReceiver.RegisterPacketHandler<RoundAdminActionPacket>(
                RoundsServerPacketId.RoundAdminAction,
                OnRoundAdminAction);

            _netReceiver.RegisterPacketHandler<PlayerBenchStatePacket>(
                RoundsServerPacketId.PlayerBenchState,
                OnPlayerBenchState);

            _netReceiver.RegisterPacketHandler<MapTravelRequestPacket>(
                RoundsServerPacketId.MapTravelRequest,
                OnMapTravelRequest);
        }

        private static IPacketData InstantiatePacket(RoundsServerPacketId packetId)
        {
            switch (packetId)
            {
                case RoundsServerPacketId.Prepared:
                    return new PreparedPacket();

                case RoundsServerPacketId.DeathReport:
                    return new DeathReportPacket();

                case RoundsServerPacketId.HealthReport:
                    return new HealthReportPacket();

                case RoundsServerPacketId.BenchSettingsRequest:
                    return new BenchSettingsRequestPacket();

                case RoundsServerPacketId.PvpModeRequest:
                    return new PvpModeRequestPacket();

                case RoundsServerPacketId.ManaSpent:
                    return new ManaSpentPacket();

                case RoundsServerPacketId.RoundSettingsRequest:
                    return new RoundSettingsRequestPacket();

                case RoundsServerPacketId.RoundAdminAction:
                    return new RoundAdminActionPacket();

                case RoundsServerPacketId.PlayerBenchState:
                    return new PlayerBenchStatePacket();

                case RoundsServerPacketId.MapTravelRequest:
                    return new MapTravelRequestPacket();

                default:
                    return null;
            }
        }


        private void OnMapTravelRequest(ushort playerId, MapTravelRequestPacket packet)
        {
            if (packet == null)
            {
                return;
            }

            IServerPlayer player;
            if (!_serverApi.ServerManager.TryGetPlayer(playerId, out player) || player == null)
            {
                return;
            }

            bool canManage = CanManageBench(player);
            if (!string.IsNullOrWhiteSpace(packet.DestinationsJson) &&
                !string.Equals(packet.DestinationsJson, "[]", StringComparison.Ordinal))
            {
                if (packet.IncludeFullCatalog)
                {
                    _settings.MergeMapDestinationsJson(packet.DestinationsJson);
                }
                else if (!string.IsNullOrWhiteSpace(player.CurrentScene))
                {
                    _settings.MergeMapDestinationsJson(packet.DestinationsJson, player.CurrentScene);
                }
            }

            switch (packet.Action)
            {
                case MapTravelAction.RequestState:
                    string serverScene = (player.CurrentScene ?? string.Empty).Trim();
                    string clientScene = (packet.CurrentScene ?? string.Empty).Trim();
                    bool hasKnownScene = serverScene.Length > 0 || clientScene.Length > 0;
                    bool serverInMantis = IsMantisScene(serverScene);
                    bool clientInMantis = IsMantisScene(clientScene);

                    if (serverInMantis && clientInMantis)
                    {
                        int requestedMapId = packet.PhantomMapId;
                        int currentMapId = GetPlayerPhantomMapId(playerId);
                        int targetMapId = requestedMapId > 0
                            ? requestedMapId
                            : requestedMapId == MapTravelRequestPacket.ExplicitNormalMapId
                                ? 0
                                : currentMapId;

                        if (targetMapId > 0 && !_settings.ContainsPhantomMantis(targetMapId))
                        {
                            SendMapTravelState(playerId, packet.MapPage,
                                "The requested phantom Mantis map no longer exists.", packet.IncludeFullCatalog);
                            return;
                        }

                        if (targetMapId != currentMapId)
                        {
                            string targetRoomKey = targetMapId > 0
                                ? "Hkmp.Rounds_Mantis_" + targetMapId.ToString(CultureInfo.InvariantCulture)
                                : serverScene;
                            if (IsMapTravelBlocked(player, targetRoomKey, false))
                            {
                                SendMapTravelState(playerId, packet.MapPage,
                                    "A room with an active round cannot be entered or left.", packet.IncludeFullCatalog);
                                return;
                            }

                            SetPlayerPhantomMap(playerId, targetMapId);
                        }
                    }
                    else if (hasKnownScene && !serverInMantis && !clientInMantis)
                    {
                        SetPlayerPhantomMap(playerId, 0);
                    }

                    SendMapTravelState(playerId, packet.MapPage, string.Empty, packet.IncludeFullCatalog);
                    return;

                case MapTravelAction.SetEnabled:
                    if (!canManage)
                    {
                        SendMapTravelState(playerId, packet.MapPage,
                            "Only the host or an authorized admin can change map travel.");
                        return;
                    }

                    _settings.SetMapTravelEnabled(packet.Enabled);
                    BroadcastMapTravelState(packet.Enabled
                        ? "Map travel has been enabled."
                        : "Map travel has been disabled.");
                    return;

                case MapTravelAction.CreatePhantomMantis:
                    if (!canManage)
                    {
                        SendMapTravelState(playerId, packet.MapPage,
                            "Only the host or an authorized admin can create a phantom Mantis map.");
                        return;
                    }

                    PhantomMantisMap created = _settings.CreatePhantomMantis();
                    if (created == null)
                    {
                        SendMapTravelState(playerId, packet.MapPage,
                            "A new phantom Mantis ID could not be allocated.");
                        return;
                    }

                    BroadcastBenchSettings(string.Empty);
                    BroadcastMapTravelState("Created " + created.DisplayName + ".");
                    return;

                case MapTravelAction.DeletePhantomMantis:
                    if (!canManage)
                    {
                        SendMapTravelState(playerId, packet.MapPage,
                            "Only the host or an authorized admin can delete a phantom Mantis map.");
                        return;
                    }

                    if (!_settings.ContainsPhantomMantis(packet.PhantomMapId))
                    {
                        SendMapTravelState(playerId, packet.MapPage,
                            "That phantom Mantis map no longer exists.");
                        return;
                    }

                    string deletedRoomKey = "Hkmp.Rounds_Mantis_" + packet.PhantomMapId.ToString(CultureInfo.InvariantCulture);
                    if (_roundManager.IsRoundRoomLocked(deletedRoomKey))
                    {
                        SendMapTravelState(playerId, packet.MapPage,
                            "A room with an active round cannot be entered or left.");
                        return;
                    }

                    ReturnPlayersFromPhantomMantis(packet.PhantomMapId);
                    if (!_settings.RemovePhantomMantis(packet.PhantomMapId))
                    {
                        SendMapTravelState(playerId, packet.MapPage,
                            "That phantom Mantis map could not be deleted.");
                        return;
                    }

                    BroadcastBenchSettings(string.Empty, true);
                    BroadcastMapTravelState("Deleted phantom Mantis #" + packet.PhantomMapId + ".");
                    return;

                case MapTravelAction.TravelToPhantomMantis:
                    if (!_settings.MapTravelEnabled && !canManage)
                    {
                        SendMapTravelState(playerId, packet.MapPage,
                            "Map travel is disabled. Only the host can move the group.");
                        return;
                    }

                    if (!_settings.ContainsPhantomMantis(packet.PhantomMapId))
                    {
                        SendMapTravelState(playerId, packet.MapPage, "That phantom Mantis map no longer exists.");
                        return;
                    }

                    string phantomRoomKey = "Hkmp.Rounds_Mantis_" + packet.PhantomMapId.ToString(CultureInfo.InvariantCulture);
                    if (IsMapTravelBlocked(player, phantomRoomKey, !_settings.MapTravelEnabled))
                    {
                        SendMapTravelState(playerId, packet.MapPage,
                            "A room with an active round cannot be entered or left.");
                        return;
                    }

                    MapTravelCommandPacket phantomCommand = new MapTravelCommandPacket("Fungus2_31", "RestBench", packet.PhantomMapId);
                    if (_settings.MapTravelEnabled)
                    {
                        SetPlayerPhantomMap(playerId, packet.PhantomMapId);
                        _netSender.SendSingleData(RoundsClientPacketId.MapTravelCommand, phantomCommand, playerId);
                        SendMapTravelState(playerId, packet.MapPage,
                            "Travel requested to phantom Mantis #" + packet.PhantomMapId + ".");
                    }
                    else
                    {
                        SetAllPlayersPhantomMap(packet.PhantomMapId);
                        _netSender.BroadcastSingleData(RoundsClientPacketId.MapTravelCommand, phantomCommand);
                        BroadcastMapTravelState("Moving all connected players to phantom Mantis #" + packet.PhantomMapId + ".");
                    }
                    return;

                case MapTravelAction.TravelToPresetMap:
                    if (!_settings.MapTravelEnabled && !canManage)
                    {
                        SendMapTravelState(playerId, packet.MapPage,
                            "Map travel is disabled. Only the host can move the group.");
                        return;
                    }

                    MapPresetDefinition presetMap;
                    if (!MapPresetCatalog.TryGetAvailableMap(packet.PresetMapId, out presetMap))
                    {
                        SendMapTravelState(playerId, packet.MapPage,
                            "That built-in map preset is unavailable.");
                        return;
                    }

                    if (IsMapTravelBlocked(player, presetMap.SceneName, !_settings.MapTravelEnabled))
                    {
                        SendMapTravelState(playerId, packet.MapPage,
                            "A room with an active round cannot be entered or left.");
                        return;
                    }

                    MapPresetCommandPacket presetCommand = new MapPresetCommandPacket(presetMap.Id);
                    if (_settings.MapTravelEnabled)
                    {
                        SetPlayerPhantomMap(playerId, 0);
                        _netSender.SendSingleData(RoundsClientPacketId.MapPresetCommand, presetCommand, playerId);
                        SendMapTravelState(playerId, packet.MapPage, "Travel requested: " + presetMap.DisplayName + ".");
                    }
                    else
                    {
                        SetAllPlayersPhantomMap(0);
                        _netSender.BroadcastSingleData(RoundsClientPacketId.MapPresetCommand, presetCommand);
                        BroadcastMapTravelState("Moving all connected players to " + presetMap.DisplayName + ".");
                    }
                    return;

                default:
                    SendMapTravelState(playerId, packet.MapPage, "Unsupported map travel action.");
                    return;
            }
        }

        public void SendMapTravelState(ushort playerId, int page, string message, bool includeCatalog = false)
        {
            IServerPlayer player;
            if (!_serverApi.ServerManager.TryGetPlayer(playerId, out player) || player == null)
            {
                return;
            }

            const int pageSize = 24;
            int pageCount = _settings.GetPhantomMantisMapPageCount(pageSize);
            page = Math.Max(0, Math.Min(pageCount - 1, page));
            _netSender.SendSingleData(
                RoundsClientPacketId.MapTravelState,
                new MapTravelStatePacket(
                    _settings.MapTravelEnabled,
                    CanManageBench(player),
                    _settings.GetPhantomMantisMapPageJson(page, pageSize),
                    page,
                    pageCount,
                    includeCatalog ? _settings.GetMapDestinationsJson() : "[]",
                    message,
                    includeCatalog,
                    BuildPlayerPhantomMapsString(player.CurrentScene)),
                playerId);
        }


        public void BroadcastMapTravelState(string message)
        {
            IReadOnlyCollection<IServerPlayer> players = _serverApi.ServerManager.Players;
            if (players == null)
            {
                return;
            }

            foreach (IServerPlayer player in players)
            {
                if (player != null)
                {
                    SendMapTravelState(player.Id, 0, message);
                }
            }
        }

        private void OnPlayerBenchState(ushort playerId, PlayerBenchStatePacket packet)
        {
            if (packet == null)
            {
                return;
            }

            IServerPlayer player;
            if (!_serverApi.ServerManager.TryGetPlayer(playerId, out player) || player == null)
            {
                return;
            }

            _roundManager.OnPlayerBenchStateChanged(
                playerId,
                player.Username,
                packet.AtBench,
                packet.ReturnToBenchPending,
                packet.HeroAlive,
                packet.DeathStarted);
        }

        private void OnRoundAdminAction(ushort playerId, RoundAdminActionPacket packet)
        {
            using (_roundManager.EnterPlayerContext(playerId))
            {
                OnRoundAdminActionInPlayerContext(playerId, packet);
            }
        }

        private void OnRoundAdminActionInPlayerContext(ushort playerId, RoundAdminActionPacket packet)
        {
            if (packet == null)
            {
                return;
            }

            IServerPlayer player;
            if (!_serverApi.ServerManager.TryGetPlayer(playerId, out player) || player == null)
            {
                return;
            }

            if (packet.Action == RoundAdminAction.RequestStatistics)
            {
                if (CanManageBench(player))
                {
                    BroadcastRoundStats();
                }
                else
                {
                    SendRoundStats(playerId);
                }
                return;
            }

            if (packet.Action == RoundAdminAction.ToggleReady)
            {
                string readyMessage;
                if (!_roundManager.ToggleReady(playerId, out readyMessage))
                {
                    SendRoundSettingsState(playerId, readyMessage);
                }
                return;
            }

            if (packet.Action == RoundAdminAction.WarpSelfToNearestBench)
            {
                _netSender.SendSingleData(
                    RoundsClientPacketId.EmergencyWarp,
                    new EmergencyWarpPacket(),
                    playerId);
                SendRoundSettingsState(
                    playerId,
                    "Nearest bench warp requested.");
                return;
            }

            if (!CanManageBench(player))
            {
                _serverApi.ServerManager.SendMessage(
                    playerId,
                    "Only the host or an authorized admin can manage round controls.");
                return;
            }

            if (packet.Action == RoundAdminAction.ToggleGameFreeze)
            {
                _gameFrozen = !_gameFrozen;
                BroadcastGameFreezeState();
                _serverApi.ServerManager.BroadcastMessage(
                    _gameFrozen ? "Game frozen." : "Game unfrozen.");
                return;
            }

            string message;
            switch (packet.Action)
            {
                case RoundAdminAction.EndRound:
                    if (!_roundManager.EndRoundManually())
                    {
                        message = "There is no active round to end.";
                        SendRoundSettingsState(playerId, message);
                        return;
                    }

                    BroadcastRoundSettingsStateForCurrentContext("Round ended from the settings UI.");
                    break;

                case RoundAdminAction.SetWinTarget:
                    if (packet.Value < 0 || packet.Value > 1000)
                    {
                        SendRoundSettingsState(playerId, "Win target must be between 0 and 1000; 0 disables the target.");
                        return;
                    }

                    if (!_roundManager.SetWinTarget(packet.Value))
                    {
                        SendRoundSettingsState(playerId, "Win target can only be changed between rounds.");
                        return;
                    }

                    message = packet.Value == 0
                        ? "Match win target disabled."
                        : "Match win target set to " + packet.Value + ".";
                    BroadcastRoundSettingsStateForCurrentContext(message);
                    break;

                case RoundAdminAction.ResetStatistics:
                    if (_roundManager.HasAnyRoundInProgress)
                    {
                        SendRoundSettingsState(playerId, "Statistics can only be reset between rounds.");
                        return;
                    }

                    if (_statsManager != null)
                    {
                        _statsManager.Reset();
                    }
                    _roundManager.ResetAllMatchScores();
                    BroadcastRoundSettingsState("Round statistics have been reset.");
                    BroadcastRoundStats();
                    break;

                case RoundAdminAction.WarpPlayerToBench:
                    {
                        if (packet.Value < 0 || packet.Value > ushort.MaxValue)
                        {
                            SendRoundSettingsState(playerId, "The selected player ID is invalid.");
                            return;
                        }

                        IServerPlayer targetPlayer;
                        if (!_serverApi.ServerManager.TryGetPlayer((ushort)packet.Value, out targetPlayer) ||
                            targetPlayer == null)
                        {
                            SendRoundSettingsState(playerId, "The selected player is not connected.");
                            return;
                        }

                        if (!_settings.HasSharedBench)
                        {
                            SendRoundSettingsState(playerId, "Select a shared bench before teleporting a player.");
                            return;
                        }

                        _netSender.SendSingleData(
                            RoundsClientPacketId.WarpToBench,
                            new SelectedBenchWarpPacket(
                                _settings.SharedBenchScene,
                                _settings.SharedBenchMarker,
                                _settings.SharedBenchMapZone,
                                _settings.SharedBenchPhantomMapId),
                            targetPlayer.Id);
                        SendRoundSettingsState(
                            playerId,
                            "Warp requested for " + targetPlayer.Username + " to " +
                            _settings.SharedBenchScene + " / " + _settings.SharedBenchMarker + ".");
                        return;
                    }

                default:
                    SendRoundSettingsState(playerId, "Unsupported round UI action.");
                    break;
            }
        }

        public void SendRoundStats(ushort playerId)
        {
            IServerPlayer player;
            if (!_serverApi.ServerManager.TryGetPlayer(playerId, out player) || player == null)
            {
                return;
            }

            _netSender.SendSingleData(
                RoundsClientPacketId.RoundStats,
                CreateRoundStatsPacket(),
                playerId);
        }

        public void BroadcastRoundStats()
        {
            IReadOnlyCollection<IServerPlayer> players = _serverApi.ServerManager.Players;
            if (players == null)
            {
                return;
            }

            foreach (IServerPlayer player in players)
            {
                if (player != null)
                {
                    _netSender.SendSingleData(
                        RoundsClientPacketId.RoundStats,
                        CreateRoundStatsPacket(),
                        player.Id);
                }
            }
        }

        private RoundStatsPacket CreateRoundStatsPacket()
        {
            if (_statsManager == null)
            {
                return new RoundStatsPacket("Statistics are unavailable.");
            }

            bool teamsEnabled =
                _serverApi.ServerManager.ServerSettings != null &&
                _serverApi.ServerManager.ServerSettings.TeamsEnabled;
            bool individualTeamWins = _settings != null && _settings.IndividualTeamWins;
            var stats = _statsManager.GetSortedStats(teamsEnabled, individualTeamWins);
            if (stats == null || stats.Count == 0)
            {
                return new RoundStatsPacket("Statistics are empty.");
            }

            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            builder.AppendLine(teamsEnabled && !individualTeamWins ? "TEAM STATISTICS" : "PLAYER STATISTICS");
            for (int i = 0; i < stats.Count; i++)
            {
                builder.Append(i + 1)
                    .Append(". ")
                    .Append(stats[i].PlayerName)
                    .Append(" — ")
                    .Append(stats[i].Wins)
                    .AppendLine(" wins");
            }

            return new RoundStatsPacket(builder.ToString());
        }

        private void OnPrepared(ushort playerId, PreparedPacket packet)
        {
            if (packet == null)
            {
                return;
            }

            using (_roundManager.EnterRoundContext(packet.RoundId))
            {
                _roundManager.OnPlayerPrepared(playerId, packet.RoundId);
            }
        }

        private void OnDeathReport(ushort playerId, DeathReportPacket packet)
        {
            if (packet == null)
            {
                return;
            }

            using (_roundManager.EnterRoundContext(packet.RoundId))
            {
                OnDeathReportInRoundContext(playerId, packet);
            }
        }

        private void OnDeathReportInRoundContext(ushort playerId, DeathReportPacket packet)
        {
            if (packet == null ||
                !_roundManager.IsRoundActive ||
                packet.RoundId == 0 ||
                packet.RoundId != _roundManager.CurrentRoundId ||
                !_roundManager.IsAliveInCurrentContext(playerId))
            {
                return;
            }

            ushort? killerId = null;
            if (packet.HasKiller && packet.KillerId != playerId &&
                _roundManager.IsAliveInCurrentContext(packet.KillerId))
            {
                killerId = packet.KillerId;
            }

            _roundManager.OnPlayerDeathInCurrentContext(playerId, killerId);
        }

        private void OnManaSpent(ushort playerId, ManaSpentPacket packet)
        {
            if (packet == null)
            {
                return;
            }

            using (_roundManager.EnterRoundContext(packet.RoundId))
            {
                _roundManager.HandleManaSpent(playerId, packet);
            }
        }

        private void OnPvpModeRequest(ushort playerId, PvpModeRequestPacket packet)
        {
            if (packet == null)
            {
                return;
            }

            IServerPlayer player;
            if (!_serverApi.ServerManager.TryGetPlayer(playerId, out player) || player == null)
            {
                return;
            }

            if (packet.IsQuery)
            {
                SendPvpModeState(playerId, string.Empty);
                return;
            }

            if (!CanManageBench(player))
            {
                SendPvpModeState(playerId, "Only the host or an authorized admin can change the PvP mode.");
                return;
            }

            string message;
            if (!_roundManager.SetPvpMode(packet.Mode, out message))
            {
                SendPvpModeState(playerId, message);
                return;
            }

            BroadcastPvpMode(message);
        }

        private void OnRoundSettingsRequest(ushort playerId, RoundSettingsRequestPacket packet)
        {
            using (_roundManager.EnterPlayerContext(playerId))
            {
                OnRoundSettingsRequestInPlayerContext(playerId, packet);
            }
        }

        private void OnRoundSettingsRequestInPlayerContext(ushort playerId, RoundSettingsRequestPacket packet)
        {
            if (packet == null) return;

            IServerPlayer player;
            if (!_serverApi.ServerManager.TryGetPlayer(playerId, out player) || player == null) return;

            if (packet.IsQuery)
            {
                SendRoundSettingsState(playerId, string.Empty);
                return;
            }

            if (!CanManageBench(player))
            {
                SendRoundSettingsState(playerId, "Only the host or an authorized admin can change round settings.");
                return;
            }

            if (_roundManager.HasAnyRoundInProgress &&
                packet.Key != RoundSettingKey.VignetteEnabled &&
                packet.Key != RoundSettingKey.HidePhantomMantisBackground &&
                packet.Key != RoundSettingKey.IndependentRoundsPerRoom)
            {
                SendRoundSettingsState(playerId, "Round settings can only be changed between rounds.");
                return;
            }

            bool changed = false;
            string message = string.Empty;
            switch (packet.Key)
            {
                case RoundSettingKey.AutoStart:
                    _roundManager.SetAutoStart(packet.BoolValue);
                    changed = true;
                    break;

                case RoundSettingKey.RoundSoul:
                    changed = _roundManager.SetRoundSoul(packet.IntValue);
                    message = changed ? string.Empty : "Round SOUL must be between 0 and 198.";
                    break;

                case RoundSettingKey.DisableEnemies:
                    _roundManager.SetDisableEnemies(packet.BoolValue);
                    changed = true;
                    break;

                case RoundSettingKey.DebugDisableShade:
                    _roundManager.SetDebugDisableShade(packet.BoolValue);
                    changed = true;
                    break;

                case RoundSettingKey.TimerIntegration:
                    _settings.SetTimerIntegration(packet.BoolValue);
                    changed = true;
                    break;

                case RoundSettingKey.ReadyRequiredCount:
                    changed = _roundManager.SetReadyRequiredCount(packet.IntValue);
                    message = changed ? string.Empty : "Ready requirement must be between 0 and 1000.";
                    break;

                case RoundSettingKey.IndividualTeamWins:
                    _settings.SetIndividualTeamWins(packet.BoolValue);
                    changed = true;
                    break;

                case RoundSettingKey.DisablePvpBeforeRound:
                    changed = _roundManager.SetDisablePvpBeforeRound(packet.BoolValue, out message);
                    break;

                case RoundSettingKey.GiveLifebloodMasks:
                    _settings.SetGiveLifebloodMasks(packet.BoolValue);
                    changed = true;
                    break;

                case RoundSettingKey.VignetteEnabled:
                    _settings.SetVignetteEnabled(packet.BoolValue);
                    changed = true;
                    break;

                case RoundSettingKey.HidePhantomMantisBackground:
                    _settings.SetHidePhantomMantisBackground(packet.BoolValue);
                    changed = true;
                    break;

                case RoundSettingKey.IndependentRoundsPerRoom:
                    {
                        bool wasIndependent = _settings.IndependentRoundsPerRoom;
                        changed = _roundManager.SetIndependentRoundsPerRoom(packet.BoolValue, out message);
                        if (changed && wasIndependent && !packet.BoolValue && !_settings.SharedBenchLocked &&
                            _lastHostSatBench != null && _lastHostSatBench.IsValid)
                        {
                            _settings.SetSharedBench(_lastHostSatBench);
                            BroadcastBenchSettings("Shared bench updated to the last bench used by the host.");
                        }
                        break;
                    }

                default:
                    message = "Unsupported round setting.";
                    break;
            }

            if (!changed && string.IsNullOrWhiteSpace(message))
            {
                message = "The round setting was not changed.";
            }

            if (changed)
            {
                if (string.IsNullOrWhiteSpace(message))
                {
                    message = "Round settings updated.";
                }
                BroadcastRoundSettingsState(message);
            }
            else
            {
                SendRoundSettingsState(playerId, message);
            }
        }

        private void OnHealthReport(ushort playerId, HealthReportPacket packet)
        {
            if (packet == null)
            {
                return;
            }

            using (_roundManager.EnterRoundContext(packet.RoundId))
            {
                _roundManager.UpdatePlayerHealth(
                    playerId,
                    packet.RoundId,
                    packet.Health,
                    packet.MaxHealth,
                    packet.BlueHealth,
                    packet.Soul);
            }
        }

        private void OnBenchSettingsRequest(ushort playerId, BenchSettingsRequestPacket packet)
        {
            if (packet == null)
            {
                return;
            }

            IServerPlayer player;
            if (!_serverApi.ServerManager.TryGetPlayer(playerId, out player) || player == null)
            {
                return;
            }

            switch (packet.Action)
            {
                case BenchSettingsAction.RequestState:
                    {
                        if (packet.IncludeFullCatalog)
                        {
                            MergeCatalogForPlayerScene(player, packet.BenchCatalogJson, true);
                        }

                        SendBenchSettings(playerId, string.Empty, packet.IncludeFullCatalog);
                        return;
                    }

                case BenchSettingsAction.Discover:
                    {
                        bool catalogChanged = MergeCatalogForPlayerScene(player, packet.BenchCatalogJson);
                        if (catalogChanged)
                        {
                            BroadcastBenchSettings(string.Empty);
                        }
                        else
                        {
                            SendBenchSettings(playerId, string.Empty);
                        }
                        return;
                    }

                case BenchSettingsAction.SetHere:
                    MergeCatalogForPlayerScene(player, packet.BenchCatalogJson);
                    HandleSetBenchHere(player, packet);
                    return;

                case BenchSettingsAction.SetSelected:
                    MergeCatalogForPlayerScene(player, packet.BenchCatalogJson);
                    HandleSetSelectedBench(player, packet);
                    return;

                case BenchSettingsAction.HostSatAtBench:
                    MergeCatalogForPlayerScene(player, packet.BenchCatalogJson);
                    HandleHostSatAtBench(player, packet);
                    return;

                case BenchSettingsAction.ToggleLock:
                    {
                        if (!CanManageBench(player))
                        {
                            SendBenchSettings(playerId, "Only the host or an authorized admin can lock the shared bench.");
                            return;
                        }

                        bool nextLockState = !_settings.SharedBenchLocked;
                        if (!_settings.SetSharedBenchLocked(nextLockState))
                        {
                            SendBenchSettings(playerId, "Select a shared bench before locking it.");
                            return;
                        }

                        if (!nextLockState && !_settings.IndependentRoundsPerRoom &&
                            _lastHostSatBench != null && _lastHostSatBench.IsValid)
                        {
                            _settings.SetSharedBench(_lastHostSatBench);
                        }

                        BroadcastBenchSettings(nextLockState
                            ? "The shared bench is locked. Round-end warps will use the host-selected bench."
                            : "The shared bench is unlocked. Players without a personal selection use the host-selected bench; personal selections are respected.");
                        return;
                    }

                default:
                    SendBenchSettings(playerId, "Unsupported bench settings request.");
                    return;
            }
        }

        private bool MergeCatalogForPlayerScene(
            IServerPlayer player,
            string catalogJson,
            bool includeAllScenes = false)
        {
            if (player == null || string.IsNullOrWhiteSpace(catalogJson) || catalogJson.Length > 45000)
            {
                return false;
            }

            bool importAllScenes = includeAllScenes && CanManageBench(player);
            if (!importAllScenes && string.IsNullOrWhiteSpace(player.CurrentScene))
            {
                return false;
            }

            try
            {
                System.Collections.Generic.List<BenchLocation> reported =
                    Newtonsoft.Json.JsonConvert.DeserializeObject<System.Collections.Generic.List<BenchLocation>>(catalogJson);
                if (reported == null)
                {
                    return false;
                }

                if (importAllScenes)
                {
                    return _settings.MergeKnownBenches(
                        reported.Where(location => IsAuthoritativeCatalogLocation(player, location)).ToList());
                }

                System.Collections.Generic.List<BenchLocation> currentSceneBenches =
                    new System.Collections.Generic.List<BenchLocation>();

                foreach (BenchLocation location in reported)
                {
                    if (location != null && location.IsValid &&
                        string.Equals(
                            location.SceneName,
                            player.CurrentScene,
                            StringComparison.OrdinalIgnoreCase) &&
                        IsAuthoritativeCatalogLocation(player, location))
                    {
                        currentSceneBenches.Add(location);
                    }
                }

                return _settings.MergeKnownBenches(currentSceneBenches);
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning(
                    "[HKMP.Rounds] Rejected a malformed bench catalog from player " +
                    player.Id + ": " + exception.Message);
                return false;
            }
        }

        private bool IsAuthoritativeCatalogLocation(IServerPlayer player, BenchLocation location)
        {
            return location != null && location.IsValid;
        }

        private void HandleHostSatAtBench(IServerPlayer player, BenchSettingsRequestPacket packet)
        {
            if (!CanManageBench(player) || packet == null || !packet.IsAtBench ||
                string.IsNullOrWhiteSpace(player.CurrentScene) ||
                !string.Equals(packet.SceneName, player.CurrentScene, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            BenchLocation location = new BenchLocation(
                packet.SceneName,
                packet.MarkerName,
                packet.MapZone,
                string.Empty,
                IsMantisScene(packet.SceneName) ? packet.PhantomMapId : 0);
            if (!location.IsValid || !_settings.ContainsKnownBench(location))
            {
                return;
            }

            _lastHostSatBench = location.Copy();
            if (!_settings.IndependentRoundsPerRoom && !_settings.SharedBenchLocked && _settings.SetSharedBench(location))
            {
                BroadcastBenchSettings("Shared bench updated to the last bench used by the host.");
            }
        }

        private void HandleSetBenchHere(IServerPlayer player, BenchSettingsRequestPacket packet)
        {
            if (!CanManageBench(player))
            {
                SendBenchSettings(player.Id, "Only the host or an authorized admin can set the shared bench.");
                return;
            }

            if (string.IsNullOrWhiteSpace(packet.SceneName) || string.IsNullOrWhiteSpace(packet.MarkerName) ||
                !string.Equals(packet.SceneName, player.CurrentScene, StringComparison.OrdinalIgnoreCase))
            {
                SendBenchSettings(player.Id, "Stand on or near a bench before using Set Here.");
                return;
            }

            BenchLocation location = new BenchLocation(
                packet.SceneName,
                packet.MarkerName,
                packet.MapZone,
                string.Empty,
                IsMantisScene(packet.SceneName) ? packet.PhantomMapId : 0);
            if (!location.IsValid || !_settings.ContainsKnownBench(location))
            {
                SendBenchSettings(player.Id, "The selected bench has not been verified in the discovered bench catalog.");
                return;
            }

            if (!_settings.SetSharedBench(location))
            {
                SendBenchSettings(player.Id, "Could not save the shared bench.");
                return;
            }

            BroadcastBenchSettings("Shared bench set to " + location.DisplayName + ".");
        }

        private void HandleSetSelectedBench(IServerPlayer player, BenchSettingsRequestPacket packet)
        {
            if (!CanManageBench(player))
            {
                SendBenchSettings(player.Id, "Only the host or an authorized admin can set the shared bench.");
                return;
            }

            BenchLocation location = new BenchLocation(
                packet.SceneName,
                packet.MarkerName,
                packet.MapZone,
                string.Empty,
                IsMantisScene(packet.SceneName) ? packet.PhantomMapId : 0);
            if (!location.IsValid || !_settings.ContainsKnownBench(location))
            {
                SendBenchSettings(player.Id, "That bench is not in the discovered bench catalog.");
                return;
            }

            if (!_settings.SetSharedBench(location))
            {
                SendBenchSettings(player.Id, "Could not save the shared bench.");
                return;
            }

            BroadcastBenchSettings("Shared bench set to " + location.DisplayName + ".");
        }



        private bool CanManageBench(IServerPlayer player)
        {
            return player != null &&
                   (player.IsAuthorized ||
                    (_hostPlayerId.HasValue && player.Id == _hostPlayerId.Value));
        }

        public void SendGameFreezeState(ushort playerId)
        {
            _netSender.SendSingleData(
                RoundsClientPacketId.GameFreezeState,
                new GameFreezePacket(_gameFrozen),
                playerId);
        }

        private void BroadcastGameFreezeState()
        {
            _netSender.BroadcastSingleData(
                RoundsClientPacketId.GameFreezeState,
                new GameFreezePacket(_gameFrozen));
        }

        public int GetPlayerPhantomMapId(ushort playerId)
        {
            lock (_playerPhantomMapSync)
            {
                int mapId;
                return _playerPhantomMapIds.TryGetValue(playerId, out mapId) ? mapId : 0;
            }
        }

        private string BuildPlayerPhantomMapsString(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                return string.Empty;
            }

            IReadOnlyCollection<IServerPlayer> players = _serverApi.ServerManager.Players;
            if (players == null || players.Count == 0)
            {
                return string.Empty;
            }

            List<KeyValuePair<ushort, int>> snapshot = new List<KeyValuePair<ushort, int>>();
            lock (_playerPhantomMapSync)
            {
                foreach (IServerPlayer player in players)
                {
                    if (player == null || !string.Equals(
                        player.CurrentScene,
                        sceneName,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    int mapId;
                    if (_playerPhantomMapIds.TryGetValue(player.Id, out mapId) && mapId > 0)
                    {
                        snapshot.Add(new KeyValuePair<ushort, int>(player.Id, mapId));
                    }
                }
            }

            if (snapshot.Count == 0)
            {
                return string.Empty;
            }

            snapshot.Sort((left, right) => left.Key.CompareTo(right.Key));
            return string.Join(",", snapshot.Select(entry =>
                entry.Key.ToString(CultureInfo.InvariantCulture) + ":" +
                entry.Value.ToString(CultureInfo.InvariantCulture)));
        }

        private bool IsMapTravelBlocked(IServerPlayer requester, string targetRoomKey, bool groupTravel)
        {
            if (_roundManager.IsRoundRoomLocked(targetRoomKey) ||
                (requester != null && _roundManager.IsRoundRoomLocked(_roundManager.GetEffectiveRoomKey(requester))))
            {
                return true;
            }

            if (!groupTravel)
            {
                return false;
            }

            IReadOnlyCollection<IServerPlayer> players = _serverApi.ServerManager.Players;
            if (players == null)
            {
                return false;
            }

            foreach (IServerPlayer player in players)
            {
                if (player != null &&
                    _roundManager.IsRoundRoomLocked(_roundManager.GetEffectiveRoomKey(player)))
                {
                    return true;
                }
            }

            return false;
        }

        private void SetPlayerPhantomMap(ushort playerId, int phantomMapId)
        {
            bool changed;
            lock (_playerPhantomMapSync)
            {
                if (phantomMapId <= 0)
                {
                    changed = _playerPhantomMapIds.Remove(playerId);
                }
                else
                {
                    int current;
                    changed = !_playerPhantomMapIds.TryGetValue(playerId, out current) || current != phantomMapId;
                    _playerPhantomMapIds[playerId] = phantomMapId;
                }
            }

            if (!changed)
            {
                return;
            }

            IServerPlayer movedPlayer;
            if (_serverApi.ServerManager.TryGetPlayer(playerId, out movedPlayer) && movedPlayer != null)
            {
                _roundManager.OnPlayerEnterScene(movedPlayer);
            }

            BroadcastMapTravelState(string.Empty);
        }

        private void SetAllPlayersPhantomMap(int phantomMapId)
        {
            IReadOnlyCollection<IServerPlayer> connectedPlayers = _serverApi.ServerManager.Players;
            if (connectedPlayers == null)
            {
                return;
            }

            List<IServerPlayer> players = connectedPlayers.Where(player => player != null).ToList();
            List<IServerPlayer> changedPlayers = new List<IServerPlayer>();
            lock (_playerPhantomMapSync)
            {
                foreach (IServerPlayer player in players)
                {
                    bool playerChanged;
                    if (phantomMapId <= 0)
                    {
                        playerChanged = _playerPhantomMapIds.Remove(player.Id);
                    }
                    else
                    {
                        int current;
                        playerChanged = !_playerPhantomMapIds.TryGetValue(player.Id, out current) || current != phantomMapId;
                        _playerPhantomMapIds[player.Id] = phantomMapId;
                    }

                    if (playerChanged)
                    {
                        changedPlayers.Add(player);
                    }
                }
            }

            for (int i = 0; i < changedPlayers.Count; i++)
            {
                _roundManager.OnPlayerEnterScene(changedPlayers[i]);
            }

            if (changedPlayers.Count > 0)
            {
                BroadcastMapTravelState(string.Empty);
            }
        }

        private void ReturnPlayersFromPhantomMantis(int phantomMapId)
        {
            IReadOnlyCollection<IServerPlayer> connectedPlayers = _serverApi.ServerManager.Players;
            if (connectedPlayers == null)
            {
                return;
            }

            List<IServerPlayer> changedPlayers = new List<IServerPlayer>();
            List<IServerPlayer> players = connectedPlayers.Where(player => player != null).ToList();
            lock (_playerPhantomMapSync)
            {
                foreach (IServerPlayer player in players)
                {
                    int trackedMapId;
                    if (_playerPhantomMapIds.TryGetValue(player.Id, out trackedMapId) && trackedMapId == phantomMapId)
                    {
                        _playerPhantomMapIds.Remove(player.Id);
                        changedPlayers.Add(player);
                    }
                }
            }

            for (int i = 0; i < changedPlayers.Count; i++)
            {
                IServerPlayer player = changedPlayers[i];
                _roundManager.OnPlayerEnterScene(player);

                string currentScene = player.CurrentScene ?? string.Empty;
                if (IsMantisScene(currentScene))
                {
                    _netSender.SendSingleData(
                        RoundsClientPacketId.MapTravelCommand,
                        new MapTravelCommandPacket(currentScene, "door", 0),
                        player.Id);
                }
            }

            if (changedPlayers.Count > 0)
            {
                BroadcastMapTravelState(string.Empty);
            }
        }

        public void NotifyPlayerConnected(IServerPlayer player)
        {
            if (player == null)
            {
                return;
            }

            SendGameFreezeState(player.Id);
            if (!_hostPlayerId.HasValue && player != null)
            {
                _hostPlayerId = player.Id;
            }
        }

        public void NotifyPlayerDisconnected(IServerPlayer player)
        {
            if (player == null)
            {
                return;
            }

            bool removedPhantomMap;
            lock (_playerPhantomMapSync)
            {
                removedPhantomMap = _playerPhantomMapIds.Remove(player.Id);
            }

            if (removedPhantomMap)
            {
                BroadcastMapTravelState(string.Empty);
            }
            if (!_hostPlayerId.HasValue || player.Id != _hostPlayerId.Value)
            {
                return;
            }

            _hostPlayerId = null;
            IReadOnlyCollection<IServerPlayer> players = _serverApi.ServerManager.Players;
            if (players == null) return;

            foreach (IServerPlayer candidate in players)
            {
                if (candidate != null && candidate.Id != player.Id)
                {
                    _hostPlayerId = candidate.Id;
                    break;
                }
            }
        }

        public void SendPvpModeState(ushort playerId, string message)
        {
            IServerPlayer player;
            if (!_serverApi.ServerManager.TryGetPlayer(playerId, out player) || player == null)
            {
                return;
            }

            _netSender.SendSingleData(
                RoundsClientPacketId.PvpMode,
                new PvpModePacket(_roundManager.CurrentPvpMode, CanManageBench(player), message),
                playerId);
        }

        public void BroadcastPvpMode(string message)
        {
            IReadOnlyCollection<IServerPlayer> players = _serverApi.ServerManager.Players;
            if (players == null) return;

            foreach (IServerPlayer player in players)
            {
                if (player != null)
                {
                    SendPvpModeState(player.Id, message);
                }
            }
        }

        public void SendManaUpdate(ushort playerId, uint roundId, int mana)
        {
            _netSender.SendSingleData(
                RoundsClientPacketId.ManaUpdate,
                new ManaUpdatePacket(roundId, mana),
                playerId);
        }

        public void SendRoundSettingsState(ushort playerId, string message)
        {
            IServerPlayer player;
            if (!_serverApi.ServerManager.TryGetPlayer(playerId, out player) || player == null) return;

            using (_roundManager.EnterPlayerContext(playerId))
            {
                _netSender.SendSingleData(
                    RoundsClientPacketId.RoundSettings,
                    CreateRoundSettingsPacket(player, message),
                    playerId);
            }
        }

        public void BroadcastRoundSettingsState(string message)
        {
            IReadOnlyCollection<IServerPlayer> players = _serverApi.ServerManager.Players;
            if (players == null) return;

            foreach (IServerPlayer player in players)
            {
                if (player != null) SendRoundSettingsState(player.Id, message);
            }
        }

        public void BroadcastRoundSettingsStateForCurrentContext(string message)
        {
            string sceneName = _roundManager.CurrentContextSceneName;
            if (_settings.IndependentRoundsPerRoom && string.IsNullOrWhiteSpace(sceneName))
            {
                return;
            }

            IReadOnlyCollection<IServerPlayer> players = _serverApi.ServerManager.Players;
            if (players == null)
            {
                return;
            }

            foreach (IServerPlayer player in players)
            {
                if (player == null)
                {
                    continue;
                }

                if (_settings.IndependentRoundsPerRoom && !string.IsNullOrWhiteSpace(sceneName))
                {
                    using (_roundManager.EnterPlayerContext(player.Id))
                    {
                        if (!string.Equals(_roundManager.CurrentContextSceneName, sceneName, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }
                    }
                }

                SendRoundSettingsState(player.Id, message);
            }
        }

        private RoundSettingsPacket CreateRoundSettingsPacket(IServerPlayer player, string message)
        {
            return new RoundSettingsPacket(
                _settings.AutoStart,
                _settings.RoundSoul,
                _settings.DisableEnemies,
                _settings.DebugDisableShade,
                _settings.TimerIntegrationEnabled,
                _settings.ReadyRequiredCount,
                _settings.IndividualTeamWins,
                _settings.IndependentRoundsPerRoom,
                _settings.DisablePvpBeforeRound,
                _settings.GiveLifebloodMasks,
                _roundManager.WinTarget,
                _roundManager.IsRoundActive,
                _roundManager.IsWaitingForPreparation,
                CanManageBench(player),
                _hostPlayerId.HasValue && player != null && player.Id == _hostPlayerId.Value,
                message,
                _roundManager.ReadyPlayerCount,
                _roundManager.EffectiveReadyRequiredCount,
                _roundManager.ConnectedPlayerCount,
                _roundManager.IsReady(player.Id),
                _settings.VignetteEnabled,
                _roundManager.GetLockedRoundScenesJson(),
                _settings.HidePhantomMantisBackground);
        }

        public void SendBenchSettings(ushort playerId, string message, bool includeCatalog = false)
        {
            IServerPlayer player;
            if (!_serverApi.ServerManager.TryGetPlayer(playerId, out player) || player == null)
            {
                return;
            }

            BenchSettingsPacket packet = CreateBenchSettingsPacket(player, message, includeCatalog);
            _netSender.SendSingleData(
                RoundsClientPacketId.BenchSettings,
                packet,
                playerId);
        }

        public void BroadcastBenchSettings(string message, bool includeCatalog = false)
        {
            if (_serverApi.ServerManager.Players == null)
            {
                return;
            }

            foreach (IServerPlayer player in _serverApi.ServerManager.Players)
            {
                if (player != null)
                {
                    SendBenchSettings(player.Id, message, includeCatalog);
                }
            }
        }

        private static bool IsMantisScene(string sceneName)
        {
            return string.Equals(sceneName, "Fungus2_15", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(sceneName, "Fungus2_31", StringComparison.OrdinalIgnoreCase);
        }

        private BenchSettingsPacket CreateBenchSettingsPacket(
            IServerPlayer player,
            string message,
            bool includeCatalog)
        {
            bool hasSharedBench = _settings.HasSharedBench;
            string catalog = includeCatalog ? GetBenchCatalogForPlayer() : "[]";

            return new BenchSettingsPacket(
                hasSharedBench ? _settings.SharedBenchScene : string.Empty,
                hasSharedBench ? _settings.SharedBenchMarker : string.Empty,
                hasSharedBench ? _settings.SharedBenchMapZone : 0,
                hasSharedBench ? _settings.SharedBenchPhantomMapId : 0,
                hasSharedBench,
                catalog,
                CanManageBench(player),
                message,
                includeCatalog,
                _settings.SharedBenchLocked);
        }

        private string GetBenchCatalogForPlayer()
        {
            List<BenchLocation> locations = _settings.GetKnownBenches();
            try
            {
                string json = Newtonsoft.Json.JsonConvert.SerializeObject(locations, Newtonsoft.Json.Formatting.None);
                return json.Length <= 45000 ? json : "[]";
            }
            catch
            {
                return "[]";
            }
        }

        public void SendPrepareRound(ushort playerId, uint roundId)
        {
            _netSender.SendSingleData(
                RoundsClientPacketId.PrepareRound,
                new RoundStartPacket(
                    roundId,
                    _roundManager.RoundSoul,
                    _roundManager.DisableEnemies,
                    _roundManager.DebugDisableShade,
                    _roundManager.CurrentPvpMode,
                    _settings.GiveLifebloodMasks),
                playerId);
        }

        public void SendRoundStart(ushort playerId, uint roundId)
        {
            _netSender.SendSingleData(
                RoundsClientPacketId.RoundStart,
                new RoundStartPacket(
                    roundId,
                    _roundManager.RoundSoul,
                    _roundManager.DisableEnemies,
                    _roundManager.DebugDisableShade,
                    _roundManager.CurrentPvpMode,
                    _settings.GiveLifebloodMasks),
                playerId);
        }

        public void RegisterRoundParticipants(uint roundId, IEnumerable<ushort> participantIds)
        {
            if (roundId == 0 || participantIds == null)
            {
                return;
            }

            _roundParticipants[roundId] = new HashSet<ushort>(participantIds);
        }

        public void BroadcastPlayerDeath(ushort playerId)
        {
            uint roundId = _roundManager.CurrentRoundId;
            PlayerDeathPacket packet = new PlayerDeathPacket(roundId, playerId);
            HashSet<ushort> recipients;
            if (_settings.IndependentRoundsPerRoom &&
                _roundParticipants.TryGetValue(roundId, out recipients) &&
                recipients.Count > 0)
            {
                _netSender.SendSingleData(RoundsClientPacketId.PlayerDeath, packet, recipients.ToArray());
                return;
            }

            _netSender.BroadcastSingleData(RoundsClientPacketId.PlayerDeath, packet);
        }

        public void SendDeathResolution(
            ushort playerId,
            int alivePlayersRemaining,
            bool roundEnded)
        {
            _netSender.SendSingleData(
                RoundsClientPacketId.DeathResolution,
                new DeathResolutionPacket(
                    _roundManager.CurrentRoundId,
                    alivePlayersRemaining,
                    roundEnded),
                playerId);
        }

        public void BroadcastRoundEnd(uint roundId)
        {
            SendRoundEnd(roundId, new RoundEndPacket(roundId));
        }

        public void BroadcastRoundEnd(uint roundId, ushort winnerId)
        {
            SendRoundEnd(roundId, new RoundEndPacket(roundId, winnerId));
        }

        public void BroadcastTeamRoundEnd(uint roundId, byte winnerTeam)
        {
            SendRoundEnd(roundId, new RoundEndPacket(roundId, winnerTeam));
        }

        private void SendRoundEnd(uint roundId, RoundEndPacket packet)
        {
            HashSet<ushort> recipients;
            if (_settings.IndependentRoundsPerRoom &&
                _roundParticipants.TryGetValue(roundId, out recipients) &&
                recipients.Count > 0)
            {
                _netSender.SendSingleData(RoundsClientPacketId.RoundEnd, packet, recipients.ToArray());
            }
            else
            {
                _netSender.BroadcastSingleData(RoundsClientPacketId.RoundEnd, packet);
            }

            _roundParticipants.Remove(roundId);
        }
    }
}
