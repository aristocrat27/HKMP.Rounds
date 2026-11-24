using Hkmp.Networking.Packet;

namespace HKMP.Rounds.Networking.Packets
{
    internal sealed class BenchSettingsPacket : IPacketData
    {
        private const int MaximumCatalogLength = 45000;
        private const int MaximumMessageLength = 512;

        public string SceneName { get; private set; }
        public string MarkerName { get; private set; }
        public int MapZone { get; private set; }
        public int PhantomMapId { get; private set; }
        public bool HasSharedBench { get; private set; }
        public string BenchCatalogJson { get; private set; }
        public bool HasBenchCatalog { get; private set; }
        public bool SharedBenchLocked { get; private set; }
        public bool CanManageBench { get; private set; }
        public string Message { get; private set; }

        public bool IsReliable { get { return true; } }
        public bool DropReliableDataIfNewerExists { get { return true; } }

        public BenchSettingsPacket()
        {
            SceneName = string.Empty;
            MarkerName = string.Empty;
            BenchCatalogJson = "[]";
            Message = string.Empty;
        }

        public BenchSettingsPacket(
            string sceneName,
            string markerName,
            int mapZone,
            int phantomMapId,
            bool hasSharedBench,
            string benchCatalogJson,
            bool canManageBench,
            string message,
            bool hasBenchCatalog = false,
            bool sharedBenchLocked = false)
        {
            SceneName = Normalize(sceneName, 256);
            MarkerName = Normalize(markerName, 256);
            MapZone = mapZone;
            PhantomMapId = phantomMapId > 0 ? phantomMapId : 0;
            HasSharedBench = hasSharedBench;
            BenchCatalogJson = NormalizeCatalog(benchCatalogJson);
            HasBenchCatalog = hasBenchCatalog;
            SharedBenchLocked = sharedBenchLocked;
            CanManageBench = canManageBench;
            Message = Normalize(message, MaximumMessageLength);
        }

        public void WriteData(IPacket packet)
        {
            packet.Write(SceneName ?? string.Empty);
            packet.Write(MarkerName ?? string.Empty);
            packet.Write(MapZone);
            packet.Write(PhantomMapId);
            packet.Write(HasSharedBench);
            packet.Write(BenchCatalogJson ?? "[]");
            packet.Write(CanManageBench);
            packet.Write(Message ?? string.Empty);
            packet.Write(HasBenchCatalog);
            packet.Write(SharedBenchLocked);
        }

        public void ReadData(IPacket packet)
        {
            SceneName = Normalize(packet.ReadString(), 256);
            MarkerName = Normalize(packet.ReadString(), 256);
            MapZone = packet.ReadInt();
            PhantomMapId = packet.ReadInt();
            if (PhantomMapId < 0) PhantomMapId = 0;
            HasSharedBench = packet.ReadBool();
            BenchCatalogJson = NormalizeCatalog(packet.ReadString());
            CanManageBench = packet.ReadBool();
            Message = Normalize(packet.ReadString(), MaximumMessageLength);
            HasBenchCatalog = packet.ReadBool();
            SharedBenchLocked = packet.ReadBool();
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
