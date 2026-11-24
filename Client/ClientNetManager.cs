using System;
using Hkmp.Api.Client.Networking;
using Hkmp.Networking.Packet;
using HKMP.Rounds.Networking;
using HKMP.Rounds.Networking.Packets;
using HKMP.Rounds.Client.Combat;
using HKMP.Rounds.Settings;
using UnityEngine;

namespace HKMP.Rounds.Client
{
    internal sealed class ClientNetManager
    {
        private readonly IClientAddonNetworkSender<RoundsServerPacketId> _netSender;

        public event Action<BenchSettingsPacket> BenchSettingsReceived;
        public event Action<PvpModePacket> PvpModeReceived;
        public event Action<RoundSettingsPacket> RoundSettingsReceived;
        public event Action<RoundStatsPacket> RoundStatsReceived;
        public event Action<MapTravelStatePacket> MapTravelStateReceived;
        public event Action<MapTravelCommandPacket> MapTravelCommandReceived;
        public event Action<MapPresetCommandPacket> MapPresetCommandReceived;

        public ClientNetManager(
            RoundsClientAddon addon,
            INetClient netClient)
        {
            _netSender = netClient.GetNetworkSender<RoundsServerPacketId>(addon);

            IClientAddonNetworkReceiver<RoundsClientPacketId> netReceiver =
                netClient.GetNetworkReceiver<RoundsClientPacketId>(addon, InstantiatePacket);

            netReceiver.RegisterPacketHandler<RoundStartPacket>(
                RoundsClientPacketId.PrepareRound,
                OnPrepareRound);

            netReceiver.RegisterPacketHandler<RoundStartPacket>(
                RoundsClientPacketId.RoundStart,
                OnRoundStart);

            netReceiver.RegisterPacketHandler<PlayerDeathPacket>(
                RoundsClientPacketId.PlayerDeath,
                OnPlayerDeath);

            netReceiver.RegisterPacketHandler<RoundEndPacket>(
                RoundsClientPacketId.RoundEnd,
                OnRoundEnd);

            netReceiver.RegisterPacketHandler<DeathResolutionPacket>(
                RoundsClientPacketId.DeathResolution,
                OnDeathResolution);

            netReceiver.RegisterPacketHandler<BenchSettingsPacket>(
                RoundsClientPacketId.BenchSettings,
                OnBenchSettings);

            netReceiver.RegisterPacketHandler<PvpModePacket>(
                RoundsClientPacketId.PvpMode,
                OnPvpModeReceivedInternal);

            netReceiver.RegisterPacketHandler<ManaUpdatePacket>(
                RoundsClientPacketId.ManaUpdate,
                OnManaUpdateReceived);

            netReceiver.RegisterPacketHandler<RoundSettingsPacket>(
                RoundsClientPacketId.RoundSettings,
                OnRoundSettingsReceivedInternal);

            netReceiver.RegisterPacketHandler<RoundStatsPacket>(
                RoundsClientPacketId.RoundStats,
                OnRoundStatsReceivedInternal);

            netReceiver.RegisterPacketHandler<EmergencyWarpPacket>(
                RoundsClientPacketId.EmergencyWarp,
                OnEmergencyWarpReceived);

            netReceiver.RegisterPacketHandler<SelectedBenchWarpPacket>(
                RoundsClientPacketId.WarpToBench,
                OnSelectedBenchWarpReceived);

            netReceiver.RegisterPacketHandler<MapTravelStatePacket>(
                RoundsClientPacketId.MapTravelState,
                OnMapTravelStateReceivedInternal);

            netReceiver.RegisterPacketHandler<MapTravelCommandPacket>(
                RoundsClientPacketId.MapTravelCommand,
                OnMapTravelCommandReceivedInternal);

            netReceiver.RegisterPacketHandler<MapPresetCommandPacket>(
                RoundsClientPacketId.MapPresetCommand,
                OnMapPresetCommandReceivedInternal);

            netReceiver.RegisterPacketHandler<GameFreezePacket>(
                RoundsClientPacketId.GameFreezeState,
                OnGameFreezeStateReceived);
        }

        private static IPacketData InstantiatePacket(RoundsClientPacketId packetId)
        {
            switch (packetId)
            {
                case RoundsClientPacketId.PrepareRound:
                case RoundsClientPacketId.RoundStart:
                    return new RoundStartPacket();

                case RoundsClientPacketId.PlayerDeath:
                    return new PlayerDeathPacket();

                case RoundsClientPacketId.RoundEnd:
                    return new RoundEndPacket();

                case RoundsClientPacketId.DeathResolution:
                    return new DeathResolutionPacket();

                case RoundsClientPacketId.BenchSettings:
                    return new BenchSettingsPacket();

                case RoundsClientPacketId.PvpMode:
                    return new PvpModePacket();

                case RoundsClientPacketId.ManaUpdate:
                    return new ManaUpdatePacket();

                case RoundsClientPacketId.RoundSettings:
                    return new RoundSettingsPacket();

                case RoundsClientPacketId.RoundStats:
                    return new RoundStatsPacket();

                case RoundsClientPacketId.EmergencyWarp:
                    return new EmergencyWarpPacket();

                case RoundsClientPacketId.WarpToBench:
                    return new SelectedBenchWarpPacket();

                case RoundsClientPacketId.MapTravelState:
                    return new MapTravelStatePacket();

                case RoundsClientPacketId.MapTravelCommand:
                    return new MapTravelCommandPacket();

                case RoundsClientPacketId.MapPresetCommand:
                    return new MapPresetCommandPacket();

                case RoundsClientPacketId.GameFreezeState:
                    return new GameFreezePacket();

                default:
                    return null;
            }
        }

        private static void OnGameFreezeStateReceived(GameFreezePacket packet)
        {
            if (packet != null)
            {
                GameFreezeController.SetFrozen(packet.IsFrozen);
            }
        }

        private void OnMapTravelStateReceivedInternal(MapTravelStatePacket packet)
        {
            if (packet == null) return;
            Action<MapTravelStatePacket> handler = MapTravelStateReceived;
            if (handler != null) handler(packet);
        }

        private void OnMapTravelCommandReceivedInternal(MapTravelCommandPacket packet)
        {
            if (packet == null) return;
            Action<MapTravelCommandPacket> handler = MapTravelCommandReceived;
            if (handler != null) handler(packet);
        }

        private void OnMapPresetCommandReceivedInternal(MapPresetCommandPacket packet)
        {
            if (packet == null) return;
            Action<MapPresetCommandPacket> handler = MapPresetCommandReceived;
            if (handler != null) handler(packet);
        }

        public void RequestMapTravelState(
            string currentScene,
            string destinationsJson,
            int page = 0,
            bool includeFullCatalog = false)
        {
            SendMapTravelRequest(
                MapTravelAction.RequestState,
                false,
                0,
                string.Empty,
                string.Empty,
                currentScene,
                destinationsJson,
                page,
                0,
                includeFullCatalog);
        }

        public void RequestMapTravelState(
            string currentScene,
            string destinationsJson,
            int page,
            bool includeFullCatalog,
            int phantomMapId)
        {
            SendMapTravelRequest(
                MapTravelAction.RequestState,
                false,
                phantomMapId > 0 ? phantomMapId : MapTravelRequestPacket.ExplicitNormalMapId,
                string.Empty,
                string.Empty,
                currentScene,
                destinationsJson,
                page,
                0,
                includeFullCatalog);
        }

        public void SetMapTravelEnabled(bool enabled, string currentScene, string destinationsJson)
        {
            SendMapTravelRequest(
                MapTravelAction.SetEnabled,
                enabled,
                0,
                string.Empty,
                string.Empty,
                currentScene,
                destinationsJson,
                0);
        }

        public void CreatePhantomMantis(string currentScene, string destinationsJson)
        {
            SendMapTravelRequest(
                MapTravelAction.CreatePhantomMantis,
                false,
                0,
                string.Empty,
                string.Empty,
                currentScene,
                destinationsJson,
                0);
        }

        public void TravelToPhantomMantis(int phantomMapId, string currentScene, string destinationsJson, int page)
        {
            SendMapTravelRequest(
                MapTravelAction.TravelToPhantomMantis,
                false,
                phantomMapId,
                string.Empty,
                string.Empty,
                currentScene,
                destinationsJson,
                page);
        }

        public void DeletePhantomMantis(int phantomMapId, string currentScene, string destinationsJson, int page)
        {
            SendMapTravelRequest(
                MapTravelAction.DeletePhantomMantis,
                false,
                phantomMapId,
                string.Empty,
                string.Empty,
                currentScene,
                destinationsJson,
                page);
        }

        public void TravelToPresetMap(int presetMapId, string currentScene)
        {
            SendMapTravelRequest(
                MapTravelAction.TravelToPresetMap,
                false,
                0,
                string.Empty,
                string.Empty,
                currentScene,
                "[]",
                0,
                presetMapId);
        }

        private void SendMapTravelRequest(
            MapTravelAction action,
            bool enabled,
            int phantomMapId,
            string sceneName,
            string entryGate,
            string currentScene,
            string destinationsJson,
            int page,
            int presetMapId = 0,
            bool includeFullCatalog = false)
        {
            try
            {
                _netSender.SendSingleData(
                    RoundsServerPacketId.MapTravelRequest,
                    new MapTravelRequestPacket(
                        action,
                        enabled,
                        phantomMapId,
                        sceneName,
                        entryGate,
                        currentScene,
                        destinationsJson,
                        page,
                        presetMapId,
                        includeFullCatalog));
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning(
                    "[HKMP.Rounds] Could not send map travel request: " + exception.Message);
            }
        }

        private void OnPrepareRound(RoundStartPacket packet)
        {
            if (packet == null)
            {
                return;
            }

            uint roundId = packet.RoundId;
            RoundPreparation.Prepare(
                roundId,
                packet.RoundSoul,
                packet.DisableEnemies,
                packet.DebugDisableShade,
                () => SendPrepared(roundId));
        }

        private void SendPrepared(uint roundId)
        {
            _netSender.SendSingleData(
                RoundsServerPacketId.Prepared,
                new PreparedPacket(roundId));
        }

        private static void OnRoundStart(RoundStartPacket packet)
        {
            if (packet == null)
            {
                return;
            }

            PvpModeClientController.StartRound(packet.PvpMode, packet.RoundId);
            RoundClientManager.StartRound(packet.RoundId, packet.GiveLifebloodMasks);
            RoundEnemyController.SetEnabled(packet.DisableEnemies);
            DebugShadeController.SetDisabled(packet.DebugDisableShade);
            RoundPreparation.ApplyRoundStartHealth(packet.GiveLifebloodMasks);
            ClientHealthTracker.ReportCurrentHealth();
        }

        private static void OnPlayerDeath(PlayerDeathPacket packet)
        {
            if (packet == null)
            {
                return;
            }

            RoundClientManager.MarkPlayerDead(packet.RoundId, packet.PlayerId);
        }

        private static void OnDeathResolution(DeathResolutionPacket packet)
        {
            if (packet == null)
            {
                return;
            }

            RoundPreparation.NotifyDeathResolution(
                packet.RoundId,
                packet.AlivePlayersRemaining,
                packet.RoundEnded);
        }

        private static void OnRoundEnd(RoundEndPacket packet)
        {
            if (packet == null)
            {
                return;
            }

            bool wasActive = RoundClientManager.IsRoundActive &&
                             RoundClientManager.CurrentRoundId == packet.RoundId;

            RoundPreparation.NotifyRoundEnded(packet.RoundId);
            PvpModeClientController.EndRound(packet.RoundId);
            RoundClientManager.EndRound(packet.RoundId);

            if (!wasActive)
            {
                RoundPreparation.CancelPreparation(packet.RoundId);
            }
        }

        private void OnPvpModeReceivedInternal(PvpModePacket packet)
        {
            if (packet == null) return;
            PvpModeClientController.SetMode(packet.Mode);
            Action<PvpModePacket> handler = PvpModeReceived;
            if (handler != null) handler(packet);
        }

        private static void OnManaUpdateReceived(ManaUpdatePacket packet)
        {
            PvpModeClientController.ApplyManaUpdate(packet);
        }

        private void OnRoundSettingsReceivedInternal(RoundSettingsPacket packet)
        {
            if (packet == null) return;

            RoundRoomExitBlocker.SetLockedRoundScenes(packet.LockedRoundScenesJson);

            GlobalSettingsStore settings = HKMPRounds.GlobalSettings;
            if (settings != null)
            {
                settings.ApplyServerState(
                    packet.AutoStart,
                    packet.RoundSoul,
                    packet.DisableEnemies,
                    packet.DebugDisableShade,
                    packet.TimerIntegrationEnabled,
                    packet.ReadyRequiredCount,
                    packet.IndividualTeamWins,
                    packet.IndependentRoundsPerRoom,
                    packet.DisablePvpBeforeRound,
                    packet.GiveLifebloodMasks,
                    packet.VignetteEnabled,
                    packet.HidePhantomMantisBackground);
                PhantomMapClientController.RefreshBackgroundVisibility();
            }

            RoundEnemyController.SetPersistentDisabled(packet.DisableEnemies);
            DebugShadeController.SetPersistentDisabled(packet.DebugDisableShade);

            Action<RoundSettingsPacket> handler = RoundSettingsReceived;
            if (handler != null) handler(packet);
        }

        public void RequestRoundSettings()
        {
            SendRoundSettingsRequest(true, RoundSettingKey.AutoStart, false, 0);
        }

        public void SetRoundSetting(RoundSettingKey key, bool boolValue, int intValue)
        {
            SendRoundSettingsRequest(false, key, boolValue, intValue);
        }

        private void SendRoundSettingsRequest(bool isQuery, RoundSettingKey key, bool boolValue, int intValue)
        {
            try
            {
                _netSender.SendSingleData(
                    RoundsServerPacketId.RoundSettingsRequest,
                    new RoundSettingsRequestPacket(isQuery, key, boolValue, intValue));
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning(
                    "[HKMP.Rounds] Could not send round settings request: " + exception.Message);
            }
        }


        private void OnEmergencyWarpReceived(EmergencyWarpPacket packet)
        {
            if (packet == null)
            {
                return;
            }

            string message;
            if (!EmergencyBenchWarpController.TryWarpToNearestBench(out message))
            {
                Modding.Logger.Log("[HKMP.Rounds] Server emergency warp could not be completed: " + message);
            }
            else
            {
                Modding.Logger.Log("[HKMP.Rounds] Server emergency warp requested: " + message);
            }
        }

        private void OnSelectedBenchWarpReceived(SelectedBenchWarpPacket packet)
        {
            if (packet == null || string.IsNullOrWhiteSpace(packet.SceneName) ||
                string.IsNullOrWhiteSpace(packet.MarkerName))
            {
                return;
            }

            BenchLocation target = new BenchLocation(
                packet.SceneName,
                packet.MarkerName,
                packet.MapZone,
                "Host-selected bench",
                packet.PhantomMapId);
            if (!BenchPolicyController.CanUseBenchLocation(target))
            {
                Modding.Logger.LogWarn(
                    "[HKMP.Rounds] The server-selected bench is unavailable on this client: " +
                    target.SceneName + " / " + target.MarkerName + ".");
                return;
            }

            string message;
            if (!EmergencyBenchWarpController.TryWarpToSelectedBench(target, out message))
            {
                Modding.Logger.LogWarn("[HKMP.Rounds] Selected bench warp could not be completed: " + message);
            }
            else
            {
                Modding.Logger.Log("[HKMP.Rounds] Selected bench warp requested: " + message);
            }
        }

        private void OnRoundStatsReceivedInternal(RoundStatsPacket packet)
        {
            if (packet == null)
            {
                return;
            }

            Action<RoundStatsPacket> handler = RoundStatsReceived;
            if (handler != null)
            {
                handler(packet);
            }
        }

        public void RequestRoundStats()
        {
            SendRoundAdminAction(RoundAdminAction.RequestStatistics, 0);
        }

        public void SendRoundAdminAction(RoundAdminAction action, int value)
        {
            try
            {
                _netSender.SendSingleData(
                    RoundsServerPacketId.RoundAdminAction,
                    new RoundAdminActionPacket(action, value));
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning(
                    "[HKMP.Rounds] Could not send round UI action: " + exception.Message);
            }
        }

        public void SendBenchState(bool atBench, bool returnToBenchPending, bool heroAlive, bool deathStarted = false)
        {
            try
            {
                _netSender.SendSingleData(
                    RoundsServerPacketId.PlayerBenchState,
                    new PlayerBenchStatePacket(atBench, returnToBenchPending, heroAlive, deathStarted));
            }
            catch
            {
            }
        }

        public void RequestPvpMode()
        {
            SendPvpModeRequest(PvpMode.Quick, true);
        }

        public void SetPvpMode(PvpMode mode)
        {
            SendPvpModeRequest(mode, false);
        }

        private void SendPvpModeRequest(PvpMode mode, bool isQuery)
        {
            try
            {
                _netSender.SendSingleData(
                    RoundsServerPacketId.PvpModeRequest,
                    new PvpModeRequestPacket(mode, isQuery));
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning(
                    "[HKMP.Rounds] Could not send PvP mode request: " + exception.Message);
            }
        }

        private void OnBenchSettings(BenchSettingsPacket packet)
        {
            if (packet == null)
            {
                return;
            }

            Action<BenchSettingsPacket> handler = BenchSettingsReceived;
            if (handler != null)
            {
                handler(packet);
            }
        }

        public void RequestBenchSettings(string knownBenchesJson, bool includeFullCatalog = false)
        {
            SendBenchSettingsRequest(
                BenchSettingsAction.RequestState,
                null,
                knownBenchesJson,
                false,
                includeFullCatalog);
        }

        public void DiscoverBenches(string knownBenchesJson)
        {
            SendBenchSettingsRequest(
                BenchSettingsAction.Discover,
                null,
                knownBenchesJson,
                false);
        }

        public void SetSharedBenchHere(BenchLocation location, bool isAtBench, string knownBenchesJson)
        {
            SendBenchSettingsRequest(
                BenchSettingsAction.SetHere,
                location,
                knownBenchesJson,
                isAtBench);
        }

        public void SetSelectedBench(BenchLocation location, string knownBenchesJson)
        {
            SendBenchSettingsRequest(
                BenchSettingsAction.SetSelected,
                location,
                knownBenchesJson,
                false);
        }

        public void ReportHostBenchSat(BenchLocation location)
        {
            if (location == null || !location.IsValid)
            {
                return;
            }

            SendBenchSettingsRequest(
                BenchSettingsAction.HostSatAtBench,
                location,
                "[]",
                true);
        }

        public void ToggleSharedBenchLock(string knownBenchesJson)
        {
            SendBenchSettingsRequest(
                BenchSettingsAction.ToggleLock,
                null,
                knownBenchesJson,
                false);
        }

        private void SendBenchSettingsRequest(
            BenchSettingsAction action,
            BenchLocation location,
            string knownBenchesJson,
            bool isAtBench,
            bool includeFullCatalog = false)
        {
            try
            {
                string catalogJson = "[]";
                if ((action == BenchSettingsAction.RequestState && includeFullCatalog) ||
                    action == BenchSettingsAction.Discover)
                {
                    catalogJson = knownBenchesJson ?? "[]";
                }
                else if (action == BenchSettingsAction.SetHere ||
                         action == BenchSettingsAction.HostSatAtBench)
                {
                    catalogJson = BenchCatalogScanner.ScanCurrentSceneJson();
                }

                _netSender.SendSingleData(
                    RoundsServerPacketId.BenchSettingsRequest,
                    new BenchSettingsRequestPacket(
                        action,
                        location == null ? string.Empty : location.SceneName,
                        location == null ? string.Empty : location.MarkerName,
                        location == null ? 0 : location.MapZone,
                        location == null ? 0 :
                            (action == BenchSettingsAction.SetHere || action == BenchSettingsAction.HostSatAtBench
                                ? PhantomMapClientController.GetPhantomMapIdForScene(location.SceneName)
                                : location.PhantomMapId),
                        isAtBench,
                        catalogJson,
                        includeFullCatalog));
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning(
                    "[HKMP.Rounds] Could not send bench settings request: " + exception.Message);
            }
        }

        public void SendManaSpent(uint roundId, int amount)
        {
            if (amount <= 0) return;
            try
            {
                _netSender.SendSingleData(
                    RoundsServerPacketId.ManaSpent,
                    new ManaSpentPacket(roundId, amount));
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning(
                    "[HKMP.Rounds] Could not send OneShot mana consumption: " + exception.Message);
            }
        }

        public void SendDeathReport(ushort killerId)
        {
            _netSender.SendSingleData(
                RoundsServerPacketId.DeathReport,
                new DeathReportPacket(RoundClientManager.CurrentRoundId, killerId));
        }

        public void SendNonPvpDeathReport(uint roundId)
        {
            _netSender.SendSingleData(
                RoundsServerPacketId.DeathReport,
                new DeathReportPacket(roundId));
        }

        public void SendHealthReport(
            uint roundId,
            int health,
            int maxHealth,
            int blueHealth,
            int soul)
        {
            _netSender.SendSingleData(
                RoundsServerPacketId.HealthReport,
                new HealthReportPacket(
                    roundId,
                    health,
                    maxHealth,
                    blueHealth,
                    soul));
        }
    }
}
