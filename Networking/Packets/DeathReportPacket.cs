using Hkmp.Networking.Packet;

namespace HKMP.Rounds.Networking.Packets
{
    internal sealed class DeathReportPacket : IPacketData
    {
        public uint RoundId { get; private set; }
        public ushort KillerId { get; private set; }
        public bool HasKiller { get; private set; }

        public bool IsReliable
        {
            get
            {
                return true;
            }
        }

        public bool DropReliableDataIfNewerExists
        {
            get
            {
                return false;
            }
        }

        public DeathReportPacket()
        {
            KillerId = 0;
            HasKiller = false;
        }

        public DeathReportPacket(
            uint roundId,
            ushort killerId)
        {
            RoundId = roundId;
            KillerId = killerId;
            HasKiller = true;
        }

        public DeathReportPacket(
            uint roundId)
        {
            RoundId = roundId;
            KillerId = 0;
            HasKiller = false;
        }

        public void WriteData(
            IPacket packet)
        {
            packet.Write(
                RoundId
            );

            packet.Write(
                HasKiller
            );

            packet.Write(
                KillerId
            );
        }

        public void ReadData(
            IPacket packet)
        {
            RoundId =
                packet.ReadUInt();

            HasKiller =
                packet.ReadBool();

            KillerId =
                packet.ReadUShort();
        }
    }
}