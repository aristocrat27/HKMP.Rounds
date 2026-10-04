using Hkmp.Api.Client.Networking;
using Hkmp.Networking.Packet;
using HKMP.Rounds.Networking;
using HKMP.Rounds.Networking.Packets;
using HKMP.Rounds.Client.Combat;

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
                        addon);

            IClientAddonNetworkReceiver<RoundsClientPacketId>
                netReceiver =
                    netClient.GetNetworkReceiver<
                        RoundsClientPacketId>(
                            addon,
                            InstantiatePacket);

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

                case RoundsClientPacketId.DeathResolution:
                    return new DeathResolutionPacket();

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
                packet.RoundSoul,
                packet.DisableEnemies,
                packet.DebugDisableShade,
                () => SendPrepared(roundId));
        }

        private void SendPrepared(
            uint roundId)
        {
            _netSender.SendSingleData(
                RoundsServerPacketId.Prepared,
                new PreparedPacket(
                    roundId));
        }

        private static void OnRoundStart(
            RoundStartPacket packet)
        {
            if (packet == null)
            {
                return;
            }

            RoundClientManager.StartRound(
                packet.RoundId);

            RoundEnemyController.SetEnabled(
                packet.DisableEnemies);

            DebugShadeController.SetDisabled(
                packet.DebugDisableShade);

            RoundPreparation.ApplyRoundStartHealth();
            ClientHealthTracker.ReportCurrentHealth();
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
                packet.PlayerId);
        }

        private static void OnDeathResolution(
            DeathResolutionPacket packet)
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

        private static void OnRoundEnd(
            RoundEndPacket packet)
        {
            if (packet == null)
            {
                return;
            }

            bool wasActive =
                RoundClientManager.IsRoundActive &&
                RoundClientManager.CurrentRoundId == packet.RoundId;

            RoundPreparation.NotifyRoundEnded(
                packet.RoundId);

            RoundClientManager.EndRound(
                packet.RoundId);

            if (!wasActive)
            {
                RoundPreparation.CancelPreparation(
                    packet.RoundId);
            }
        }

        public void SendDeathReport(
            ushort killerId)
        {
            _netSender.SendSingleData(
                RoundsServerPacketId.DeathReport,
                new DeathReportPacket(
                    RoundClientManager.CurrentRoundId,
                    killerId));
        }

        public void SendNonPvpDeathReport(
            uint roundId)
        {
            _netSender.SendSingleData(
                RoundsServerPacketId.DeathReport,
                new DeathReportPacket(
                    roundId));
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