using Hkmp.Networking.Packet;
using HKMP.Rounds.Settings;

namespace HKMP.Rounds.Networking.Packets
{
    internal sealed class PvpModePacket : IPacketData
    {
        private const int MaximumMessageLength = 512;

        public PvpMode Mode { get; private set; }
        public bool CanManage { get; private set; }
        public string Message { get; private set; }

        public bool IsReliable { get { return true; } }
        public bool DropReliableDataIfNewerExists { get { return true; } }

        public PvpModePacket()
        {
            Mode = PvpMode.Quick;
            Message = string.Empty;
        }

        public PvpModePacket(PvpMode mode, bool canManage, string message)
        {
            Mode = NormalizeMode(mode);
            CanManage = canManage;
            Message = NormalizeMessage(message);
        }

        public void WriteData(IPacket packet)
        {
            packet.Write((byte)Mode);
            packet.Write(CanManage);
            packet.Write(Message ?? string.Empty);
        }

        public void ReadData(IPacket packet)
        {
            Mode = NormalizeMode((PvpMode)packet.ReadByte());
            CanManage = packet.ReadBool();
            Message = NormalizeMessage(packet.ReadString());
        }

        private static PvpMode NormalizeMode(PvpMode mode)
        {
            return mode == PvpMode.Long || mode == PvpMode.OneShot ? mode : PvpMode.Quick;
        }

        private static string NormalizeMessage(string message)
        {
            string value = message ?? string.Empty;
            return value.Length <= MaximumMessageLength ? value : value.Substring(0, MaximumMessageLength);
        }
    }
}
