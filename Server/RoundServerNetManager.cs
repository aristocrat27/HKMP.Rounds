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
                        addon
                    );

            _netReceiver =
                serverApi.NetServer.GetNetworkReceiver<
                    RoundsServerPacketId>(
                        addon,
                        InstantiatePacket
                    );

            _netReceiver.RegisterPacketHandler<PreparedPacket>(
                RoundsServerPacketId.Prepared,
                OnPrepared
            );

            _netReceiver.RegisterPacketHandler<DeathReportPacket>(
                RoundsServerPacketId.DeathReport,
                OnDeathReport
            );

            Modding.Logger.Log(
                "[HKMP.Rounds] RoundServerNetManager initialized."
            );
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
                packet.RoundId
            );
        }

        private void OnDeathReport(
            ushort playerId,
            DeathReportPacket packet)
        {
            if (packet == null)
            {
                return;
            }

            ushort victimId =
                playerId;

            ushort killerId =
                packet.KillerId;

            Modding.Logger.Log(
                "[HKMP.Rounds] Server received DeathReport. " +
                "RoundId=" +
                packet.RoundId +
                " CurrentRoundId=" +
                _roundManager.CurrentRoundId +
                " VictimId=" +
                victimId +
                " KillerId=" +
                killerId
            );

            if (!_roundManager.IsRoundActive)
            {
                return;
            }

            if (packet.RoundId !=
                _roundManager.CurrentRoundId)
            {
                return;
            }

            if (!_roundManager.IsAlive(
                victimId))
            {
                return;
            }

            if (!_roundManager.IsAlive(
                killerId))
            {
                return;
            }

            if (killerId == victimId)
            {
                return;
            }

            _roundManager.OnPlayerDeath(
                victimId
            );
        }

        public void SendPrepareRound(
            ushort playerId,
            uint roundId)
        {
            _netSender.SendSingleData(
                RoundsClientPacketId.PrepareRound,
                new RoundStartPacket(
                    roundId
                ),
                playerId
            );
        }

        public void SendRoundStart(
            ushort playerId,
            uint roundId)
        {
            _netSender.SendSingleData(
                RoundsClientPacketId.RoundStart,
                new RoundStartPacket(
                    roundId
                ),
                playerId
            );
        }

        public void BroadcastPlayerDeath(
            ushort playerId)
        {
            _netSender.BroadcastSingleData(
                RoundsClientPacketId.PlayerDeath,
                new PlayerDeathPacket(
                    _roundManager.CurrentRoundId,
                    playerId
                )
            );
        }

        public void BroadcastRoundEnd(
            uint roundId,
            ushort winnerId)
        {
            _netSender.BroadcastSingleData(
                RoundsClientPacketId.RoundEnd,
                new RoundEndPacket(
                    roundId,
                    winnerId
                )
            );
        }

        public void BroadcastTeamRoundEnd(
            uint roundId,
            byte winnerTeam)
        {
            _netSender.BroadcastSingleData(
                RoundsClientPacketId.RoundEnd,
                new RoundEndPacket(
                    roundId,
                    winnerTeam
                )
            );
        }
    }
}