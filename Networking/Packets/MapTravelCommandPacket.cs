using Hkmp.Networking.Packet;

namespace HKMP.Rounds.Networking.Packets
{
    internal sealed class MapTravelCommandPacket : IPacketData
    {
        public string SceneName { get; private set; }
        public string EntryGate { get; private set; }
        public int PhantomMapId { get; private set; }

        public bool IsReliable { get { return true; } }
        public bool DropReliableDataIfNewerExists { get { return false; } }

        public MapTravelCommandPacket()
        {
            SceneName = string.Empty;
            EntryGate = "door";
        }

        public MapTravelCommandPacket(string sceneName, string entryGate, int phantomMapId)
        {
            SceneName = Normalize(sceneName, 160);
            EntryGate = Normalize(entryGate, 128);
            PhantomMapId = phantomMapId < 0 ? 0 : phantomMapId;
        }

        public void WriteData(IPacket packet)
        {
            packet.Write(SceneName ?? string.Empty);
            packet.Write(EntryGate ?? "door");
            packet.Write(PhantomMapId);
        }

        public void ReadData(IPacket packet)
        {
            SceneName = Normalize(packet.ReadString(), 160);
            EntryGate = Normalize(packet.ReadString(), 128);
            PhantomMapId = packet.ReadInt();
            if (PhantomMapId < 0) PhantomMapId = 0;
        }

        private static string Normalize(string value, int maximumLength)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            string result = value.Trim();
            return result.Length <= maximumLength ? result : result.Substring(0, maximumLength);
        }
    }
}
