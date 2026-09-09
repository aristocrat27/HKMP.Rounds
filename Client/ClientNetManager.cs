using Hkmp.Api.Client.Networking;
using Hkmp.Networking.Packet;

using HKMP.Rounds.Networking;
using HKMP.Rounds.Networking.Packets;

namespace HKMP.Rounds.Client
{
    internal sealed class ClientNetManager
    {
        private readonly IClientAddonNetworkSender<RoundsServerPacketId>
            _netSender;

        public ClientNetManager(
            RoundsClientAddon addon,
            INetClient netClient)
        {
            _netSender =
                netClient.GetNetworkSender<
                    RoundsServerPacketId>(
                        addon
                    );

            IClientAddonNetworkReceiver<RoundsClientPacketId>
                netReceiver =
                    netClient.GetNetworkReceiver<
                        RoundsClientPacketId>(
                            addon,
                            InstantiatePacket
                        );

            netReceiver.RegisterPacketHandler<RoundStartPacket>(
                RoundsClientPacketId.PrepareRound,
                OnPrepareRound
            );

            netReceiver.RegisterPacketHandler<RoundStartPacket>(
                RoundsClientPacketId.RoundStart,
                OnRoundStart
            );

            netReceiver.RegisterPacketHandler<PlayerDeathPacket>(
                RoundsClientPacketId.PlayerDeath,
                OnPlayerDeath
            );

            netReceiver.RegisterPacketHandler<RoundEndPacket>(
                RoundsClientPacketId.RoundEnd,
                OnRoundEnd
            );

            Modding.Logger.Log(
                "[HKMP.Rounds] ClientNetManager initialized."
            );
        }

        private static IPacketData InstantiatePacket(
            RoundsClientPacketId packetId)
        {
            switch (packetId)
            {
                case RoundsClientPacketId.PrepareRound:
                    return new RoundStartPacket();

                case RoundsClientPacketId.RoundStart:
                    return new RoundStartPacket();

                case RoundsClientPacketId.PlayerDeath:
                    return new PlayerDeathPacket();

                case RoundsClientPacketId.RoundEnd:
                    return new RoundEndPacket();

                default:
                    return null;
            }
        }

        private void OnPrepareRound(
            RoundStartPacket packet)
        {
            if (packet == null)
            {
                return;
            }

            uint roundId =
                packet.RoundId;

            RoundPreparation.Prepare(
                roundId,
                () =>
                {
                    SendPrepared(
                        roundId
                    );
                }
            );
        }

        private void SendPrepared(
            uint roundId)
        {
            _netSender.SendSingleData(
                RoundsServerPacketId.Prepared,
                new PreparedPacket(
                    roundId
                )
            );
        }

        private static void OnRoundStart(
            RoundStartPacket packet)
        {
            if (packet == null)
            {
                return;
            }

            RoundClientManager.StartRound(
                packet.RoundId
            );
        }

        private static void OnPlayerDeath(
            PlayerDeathPacket packet)
        {
            if (packet == null)
            {
                return;
            }

            RoundClientManager.MarkPlayerDead(
                packet.RoundId,
                packet.PlayerId
            );
        }

        private static void OnRoundEnd(
            RoundEndPacket packet)
        {
            if (packet == null)
            {
                return;
            }

            RoundClientManager.EndRound(
                packet.RoundId
            );
        }

        public void SendDeathReport(
            ushort killerId)
        {
            _netSender.SendSingleData(
                RoundsServerPacketId.DeathReport,
                new DeathReportPacket(
                    RoundClientManager.CurrentRoundId,
                    killerId
                )
            );
        }
    }
}