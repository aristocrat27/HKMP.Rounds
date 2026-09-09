using Hkmp.Networking.Packet;

namespace HKMP.Rounds.Networking.Packets
{
    internal sealed class DeathReportPacket : IPacketData
    {
        public uint RoundId { get; private set; }
        public ushort KillerId { get; private set; }
        public bool IsReliable { get { return true; } }
        public bool DropReliableDataIfNewerExists { get { return false; } }

        public DeathReportPacket() { }
        public DeathReportPacket(uint roundId, ushort killerId)
        {
            RoundId = roundId;
            KillerId = killerId;
        }

        public void WriteData(IPacket packet)
        {
            packet.Write(RoundId);
            packet.Write(KillerId);
        }

        public void ReadData(IPacket packet)
        {
            RoundId = packet.ReadUInt();
            KillerId = packet.ReadUShort();
        }
    }
}
