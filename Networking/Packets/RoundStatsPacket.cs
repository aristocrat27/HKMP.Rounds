using Hkmp.Networking.Packet;

namespace HKMP.Rounds.Networking.Packets
{
    internal sealed class RoundStatsPacket : IPacketData
    {
        private const int MaximumTextLength = 6000;

        public string Text { get; private set; }

        public bool IsReliable { get { return true; } }
        public bool DropReliableDataIfNewerExists { get { return true; } }

        public RoundStatsPacket()
        {
            Text = string.Empty;
        }

        public RoundStatsPacket(string text)
        {
            Text = Normalize(text);
        }

        public void WriteData(IPacket packet)
        {
            packet.Write(Text ?? string.Empty);
        }

        public void ReadData(IPacket packet)
        {
            Text = Normalize(packet.ReadString());
        }

        private static string Normalize(string value)
        {
            value = value ?? string.Empty;
            return value.Length <= MaximumTextLength ? value : value.Substring(0, MaximumTextLength);
        }
    }
}
