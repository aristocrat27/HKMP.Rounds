using Hkmp.Networking.Packet;

namespace HKMP.Rounds.Networking.Packets
{
    internal sealed class ManaUpdatePacket : IPacketData
    {
        public uint RoundId { get; private set; }
        public int Mana { get; private set; }

        public bool IsReliable { get { return true; } }
        public bool DropReliableDataIfNewerExists { get { return true; } }

        public ManaUpdatePacket()
        {
        }

        public ManaUpdatePacket(uint roundId, int mana)
        {
            RoundId = roundId;
            Mana = mana < 0 ? 0 : mana;
        }

        public void WriteData(IPacket packet)
        {
            packet.Write(RoundId);
            packet.Write(Mana);
        }

        public void ReadData(IPacket packet)
        {
            RoundId = packet.ReadUInt();
            Mana = packet.ReadInt();
            if (Mana < 0) Mana = 0;
        }
    }
}
