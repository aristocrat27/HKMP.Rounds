using Hkmp.Networking.Packet;

namespace HKMP.Rounds.Networking.Packets
{
    internal sealed class SelectedBenchWarpPacket : IPacketData
    {
        public string SceneName { get; private set; }
        public string MarkerName { get; private set; }
        public int MapZone { get; private set; }
        public int PhantomMapId { get; private set; }

        public bool IsReliable { get { return true; } }
        public bool DropReliableDataIfNewerExists { get { return false; } }

        public SelectedBenchWarpPacket()
        {
            SceneName = string.Empty;
            MarkerName = string.Empty;
        }

        public SelectedBenchWarpPacket(string sceneName, string markerName, int mapZone, int phantomMapId)
        {
            SceneName = Normalize(sceneName, 256);
            MarkerName = Normalize(markerName, 256);
            MapZone = mapZone;
            PhantomMapId = phantomMapId > 0 ? phantomMapId : 0;
        }

        public void WriteData(IPacket packet)
        {
            packet.Write(SceneName ?? string.Empty);
            packet.Write(MarkerName ?? string.Empty);
            packet.Write(MapZone);
            packet.Write(PhantomMapId);
        }

        public void ReadData(IPacket packet)
        {
            SceneName = Normalize(packet.ReadString(), 256);
            MarkerName = Normalize(packet.ReadString(), 256);
            MapZone = packet.ReadInt();
            PhantomMapId = packet.ReadInt();
            if (PhantomMapId < 0) PhantomMapId = 0;
        }

        private static string Normalize(string value, int maximumLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string normalized = value.Trim();
            return normalized.Length <= maximumLength
                ? normalized
                : normalized.Substring(0, maximumLength);
        }
    }
}
