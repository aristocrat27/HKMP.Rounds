using Hkmp.Api.Server;
using Hkmp.Api.Server.Networking;
using Hkmp.Networking.Packet;
using HKMP.Rounds.Networking;
using HKMP.Rounds.Networking.Packets;

namespace HKMP.Rounds.Server
{
    internal sealed class RoundServerNetManager
    {
        private readonly RoundManager _roundManager;

        private readonly IServerAddonNetworkSender<RoundsClientPacketId>
            _netSender;

        private readonly IServerAddonNetworkReceiver<RoundsServerPacketId>
            _netReceiver;

        public RoundServerNetManager(
            RoundsServerAddon addon,
            IServerApi serverApi,
            RoundManager roundManager)
        {
            _roundManager =
                roundManager;

            _netSender =
                serverApi.NetServer.GetNetworkSender<
                    RoundsClientPacketId>(
                    addon);

            _netReceiver =
                serverApi.NetServer.GetNetworkReceiver<
                    RoundsServerPacketId>(
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
        }

        private static IPacketData InstantiatePacket(
            RoundsServerPacketId packetId)
        {
            switch (packetId)
            {
                case RoundsServerPacketId.Prepared:
                    return new PreparedPacket();

                case RoundsServerPacketId.DeathReport:
                    return new DeathReportPacket();

                case RoundsServerPacketId.HealthReport:
                    return new HealthReportPacket();

                default:
                    return null;
            }
        }

        private void OnPrepared(
            ushort playerId,
            PreparedPacket packet)
        {
            if (packet == null)
            {
                return;
            }

            _roundManager.OnPlayerPrepared(
                playerId,
                packet.RoundId);
        }

        private void OnDeathReport(
            ushort playerId,
            DeathReportPacket packet)
        {
            if (packet == null ||
                !_roundManager.IsRoundActive ||
                packet.RoundId != _roundManager.CurrentRoundId ||
                !_roundManager.IsAlive(playerId))
            {
                return;
            }

            if (!packet.HasKiller)
            {
                _roundManager.OnPlayerDeath(
                    playerId);

                return;
            }

            ushort killerId =
                packet.KillerId;

            if (killerId == playerId ||
                !_roundManager.IsAlive(killerId))
            {
                return;
            }

            _roundManager.OnPlayerDeath(
                playerId);
        }

        private void OnHealthReport(
            ushort playerId,
            HealthReportPacket packet)
        {
            if (packet == null)
            {
                return;
            }

            _roundManager.UpdatePlayerHealth(
                playerId,
                packet.RoundId,
                packet.Health,
                packet.MaxHealth,
                packet.BlueHealth,
                packet.Soul);
        }

        public void SendPrepareRound(
            ushort playerId,
            uint roundId)
        {
            _netSender.SendSingleData(
                RoundsClientPacketId.PrepareRound,
                new RoundStartPacket(
                    roundId,
                    _roundManager.RoundSoul,
                    _roundManager.DisableEnemies,
                    _roundManager.DebugDisableShade),
                playerId);
        }

        public void SendRoundStart(
            ushort playerId,
            uint roundId)
        {
            _netSender.SendSingleData(
                RoundsClientPacketId.RoundStart,
                new RoundStartPacket(
                    roundId,
                    _roundManager.RoundSoul,
                    _roundManager.DisableEnemies,
                    _roundManager.DebugDisableShade),
                playerId);
        }

        public void BroadcastPlayerDeath(
            ushort playerId)
        {
            _netSender.BroadcastSingleData(
                RoundsClientPacketId.PlayerDeath,
                new PlayerDeathPacket(
                    _roundManager.CurrentRoundId,
                    playerId));
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

        public void BroadcastRoundEnd(
            uint roundId)
        {
            _netSender.BroadcastSingleData(
                RoundsClientPacketId.RoundEnd,
                new RoundEndPacket(
                    roundId));
        }

        public void BroadcastRoundEnd(
            uint roundId,
            ushort winnerId)
        {
            _netSender.BroadcastSingleData(
                RoundsClientPacketId.RoundEnd,
                new RoundEndPacket(
                    roundId,
                    winnerId));
        }

        public void BroadcastTeamRoundEnd(
            uint roundId,
            byte winnerTeam)
        {
            _netSender.BroadcastSingleData(
                RoundsClientPacketId.RoundEnd,
                new RoundEndPacket(
                    roundId,
                    winnerTeam));
        }
    }
}