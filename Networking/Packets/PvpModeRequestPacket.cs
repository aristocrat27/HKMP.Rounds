using Hkmp.Networking.Packet;
using HKMP.Rounds.Settings;

namespace HKMP.Rounds.Networking.Packets
{
    internal sealed class PvpModeRequestPacket : IPacketData
    {
        public PvpMode Mode { get; private set; }
        public bool IsQuery { get; private set; }

        public bool IsReliable { get { return true; } }
        public bool DropReliableDataIfNewerExists { get { return false; } }

        public PvpModeRequestPacket()
        {
            Mode = PvpMode.Quick;
        }

        public PvpModeRequestPacket(PvpMode mode, bool isQuery)
        {
            Mode = NormalizeMode(mode);
            IsQuery = isQuery;
        }

        public void WriteData(IPacket packet)
        {
            packet.Write((byte)Mode);
            packet.Write(IsQuery);
        }

        public void ReadData(IPacket packet)
        {
            Mode = NormalizeMode((PvpMode)packet.ReadByte());
            IsQuery = packet.ReadBool();
        }

        private static PvpMode NormalizeMode(PvpMode mode)
        {
            return mode == PvpMode.Long || mode == PvpMode.OneShot ? mode : PvpMode.Quick;
        }
    }
}
