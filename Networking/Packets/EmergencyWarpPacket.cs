using Hkmp.Networking.Packet;

namespace HKMP.Rounds.Networking.Packets
{
    internal sealed class EmergencyWarpPacket : IPacketData
    {
        public bool IsReliable { get { return true; } }
        public bool DropReliableDataIfNewerExists { get { return false; } }

        public void WriteData(IPacket packet)
        {
        }

        public void ReadData(IPacket packet)
        {
        }
    }
}
