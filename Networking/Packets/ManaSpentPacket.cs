using Hkmp.Networking.Packet;

namespace HKMP.Rounds.Networking.Packets
{
    internal sealed class ManaSpentPacket : IPacketData
    {
        public uint RoundId { get; private set; }
        public int Amount { get; private set; }

        public bool IsReliable { get { return true; } }
        public bool DropReliableDataIfNewerExists { get { return false; } }

        public ManaSpentPacket()
        {
        }

        public ManaSpentPacket(uint roundId, int amount)
        {
            RoundId = roundId;
            Amount = amount < 0 ? 0 : amount;
        }

        public void WriteData(IPacket packet)
        {
            packet.Write(RoundId);
            packet.Write(Amount);
        }

        public void ReadData(IPacket packet)
        {
            RoundId = packet.ReadUInt();
            Amount = packet.ReadInt();
            if (Amount < 0) Amount = 0;
        }
    }
}
