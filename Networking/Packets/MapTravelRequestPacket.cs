using System;
using Hkmp.Networking.Packet;
using HKMP.Rounds.Networking;

namespace HKMP.Rounds.Networking.Packets
{
    internal sealed class MapTravelRequestPacket : IPacketData
    {
        private const int MaximumCatalogLength = 48000;
        internal const int ExplicitNormalMapId = -1;

        public MapTravelAction Action { get; private set; }
        public bool Enabled { get; private set; }
        public int PhantomMapId { get; private set; }
        public int MapPage { get; private set; }
        public string SceneName { get; private set; }
        public string EntryGate { get; private set; }
        public string CurrentScene { get; private set; }
        public string DestinationsJson { get; private set; }
        public int PresetMapId { get; private set; }
        public bool IncludeFullCatalog { get; private set; }

        public bool IsReliable { get { return true; } }
        public bool DropReliableDataIfNewerExists { get { return false; } }

        public MapTravelRequestPacket()
        {
            SceneName = string.Empty;
            EntryGate = string.Empty;
            CurrentScene = string.Empty;
            DestinationsJson = "[]";
        }

        public MapTravelRequestPacket(
            MapTravelAction action,
            bool enabled,
            int phantomMapId,
            string sceneName,
            string entryGate,
            string currentScene,
            string destinationsJson,
            int mapPage = 0,
            int presetMapId = 0,
            bool includeFullCatalog = false)
        {
            Action = action;
            Enabled = enabled;
            PhantomMapId = action == MapTravelAction.RequestState && phantomMapId == ExplicitNormalMapId
                ? ExplicitNormalMapId
                : phantomMapId < 0 ? 0 : phantomMapId;
            MapPage = Math.Max(0, mapPage);
            SceneName = Normalize(sceneName, 160);
            EntryGate = Normalize(entryGate, 128);
            CurrentScene = Normalize(currentScene, 160);
            DestinationsJson = NormalizeCatalog(destinationsJson);
            PresetMapId = Math.Max(0, presetMapId);
            IncludeFullCatalog = includeFullCatalog;
        }

        public void WriteData(IPacket packet)
        {
            packet.Write((byte)Action);
            packet.Write(Enabled);
            packet.Write(PhantomMapId);
            packet.Write(MapPage);
            packet.Write(SceneName ?? string.Empty);
            packet.Write(EntryGate ?? string.Empty);
            packet.Write(CurrentScene ?? string.Empty);
            packet.Write(DestinationsJson ?? "[]");
            packet.Write(PresetMapId);
            packet.Write(IncludeFullCatalog);
        }

        public void ReadData(IPacket packet)
        {
            Action = (MapTravelAction)packet.ReadByte();
            Enabled = packet.ReadBool();
            PhantomMapId = packet.ReadInt();
            MapPage = packet.ReadInt();
            SceneName = Normalize(packet.ReadString(), 160);
            EntryGate = Normalize(packet.ReadString(), 128);
            CurrentScene = Normalize(packet.ReadString(), 160);
            DestinationsJson = NormalizeCatalog(packet.ReadString());
            PresetMapId = Math.Max(0, packet.ReadInt());
            IncludeFullCatalog = packet.ReadBool();
            if (PhantomMapId < 0 && !(Action == MapTravelAction.RequestState && PhantomMapId == ExplicitNormalMapId))
            {
                PhantomMapId = 0;
            }
            if (MapPage < 0) MapPage = 0;
        }

        private static string NormalizeCatalog(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > MaximumCatalogLength)
            {
                return "[]";
            }

            return value.Trim();
        }

        private static string Normalize(string value, int maximumLength)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            string result = value.Trim();
            return result.Length <= maximumLength ? result : result.Substring(0, maximumLength);
        }
    }
}
