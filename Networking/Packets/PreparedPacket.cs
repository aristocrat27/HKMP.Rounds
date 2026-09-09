using Hkmp.Networking.Packet;

namespace HKMP.Rounds.Networking.Packets
{
    internal sealed class PreparedPacket : IPacketData
    {
        public uint RoundId { get; private set; }
        public bool IsReliable { get { return true; } }
        public bool DropReliableDataIfNewerExists { get { return false; } }

        public PreparedPacket() { }
        public PreparedPacket(uint roundId) { RoundId = roundId; }

        public void WriteData(IPacket packet) { packet.Write(RoundId); }
        public void ReadData(IPacket packet) { RoundId = packet.ReadUInt(); }
    }
}
