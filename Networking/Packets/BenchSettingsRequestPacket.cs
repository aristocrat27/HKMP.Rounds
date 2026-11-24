using Hkmp.Networking.Packet;
using HKMP.Rounds.Networking;

namespace HKMP.Rounds.Networking.Packets
{
    internal sealed class BenchSettingsRequestPacket : IPacketData
    {
        private const int MaximumCatalogLength = 45000;

        public BenchSettingsAction Action { get; private set; }
        public string SceneName { get; private set; }
        public string MarkerName { get; private set; }
        public int MapZone { get; private set; }
        public int PhantomMapId { get; private set; }
        public bool IsAtBench { get; private set; }
        public string BenchCatalogJson { get; private set; }
        public bool IncludeFullCatalog { get; private set; }

        public bool IsReliable { get { return true; } }
        public bool DropReliableDataIfNewerExists { get { return false; } }

        public BenchSettingsRequestPacket()
        {
            SceneName = string.Empty;
            MarkerName = string.Empty;
            BenchCatalogJson = "[]";
        }

        public BenchSettingsRequestPacket(
            BenchSettingsAction action,
            string sceneName,
            string markerName,
            int mapZone,
            int phantomMapId,
            bool isAtBench,
            string benchCatalogJson,
            bool includeFullCatalog = false)
        {
            Action = action;
            SceneName = Normalize(sceneName, 256);
            MarkerName = Normalize(markerName, 256);
            MapZone = mapZone;
            PhantomMapId = phantomMapId > 0 ? phantomMapId : 0;
            IsAtBench = isAtBench;
            BenchCatalogJson = NormalizeCatalog(benchCatalogJson);
            IncludeFullCatalog = includeFullCatalog;
        }

        public void WriteData(IPacket packet)
        {
            packet.Write((byte)Action);
            packet.Write(SceneName ?? string.Empty);
            packet.Write(MarkerName ?? string.Empty);
            packet.Write(MapZone);
            packet.Write(PhantomMapId);
            packet.Write(IsAtBench);
            packet.Write(BenchCatalogJson ?? "[]");
            packet.Write(IncludeFullCatalog);
        }

        public void ReadData(IPacket packet)
        {
            Action = (BenchSettingsAction)packet.ReadByte();
            SceneName = Normalize(packet.ReadString(), 256);
            MarkerName = Normalize(packet.ReadString(), 256);
            MapZone = packet.ReadInt();
            PhantomMapId = packet.ReadInt();
            if (PhantomMapId < 0) PhantomMapId = 0;
            IsAtBench = packet.ReadBool();
            BenchCatalogJson = NormalizeCatalog(packet.ReadString());
            IncludeFullCatalog = packet.ReadBool();
        }

        private static string NormalizeCatalog(string value)
        {
            return string.IsNullOrWhiteSpace(value) || value.Length > MaximumCatalogLength
                ? "[]"
                : value.Trim();
        }

        private static string Normalize(string value, int maximumLength)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            string result = value.Trim();
            return result.Length > maximumLength ? result.Substring(0, maximumLength) : result;
        }
    }
}
