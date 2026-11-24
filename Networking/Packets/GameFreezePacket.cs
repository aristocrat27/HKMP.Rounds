using Hkmp.Networking.Packet;

namespace HKMP.Rounds.Networking.Packets
{
    internal sealed class GameFreezePacket : IPacketData
    {
        public bool IsFrozen { get; private set; }
        public bool IsReliable { get { return true; } }
        public bool DropReliableDataIfNewerExists { get { return false; } }

        public GameFreezePacket()
        {
        }

        public GameFreezePacket(bool isFrozen)
        {
            IsFrozen = isFrozen;
        }

        public void WriteData(IPacket packet)
        {
            packet.Write(IsFrozen);
        }

        public void ReadData(IPacket packet)
        {
            IsFrozen = packet.ReadBool();
        }
    }
}
