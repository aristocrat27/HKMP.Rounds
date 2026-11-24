using System;
using Hkmp.Networking.Packet;

namespace HKMP.Rounds.Networking.Packets
{
    internal sealed class MapTravelStatePacket : IPacketData
    {
        private const int MaximumJsonLength = 48000;
        private const int MaximumPlayerPhantomMapsLength = 16000;

        public bool TravelEnabled { get; private set; }
        public bool CanManage { get; private set; }
        public string PhantomMapsJson { get; private set; }
        public int PhantomMapsPage { get; private set; }
        public int PhantomMapsPageCount { get; private set; }
        public string DestinationsJson { get; private set; }
        public bool HasDestinationsCatalog { get; private set; }
        public string Message { get; private set; }
        public string PlayerPhantomMaps { get; private set; }

        public bool IsReliable { get { return true; } }
        public bool DropReliableDataIfNewerExists { get { return false; } }

        public MapTravelStatePacket()
        {
            PhantomMapsJson = "[]";
            DestinationsJson = "[]";
            Message = string.Empty;
            PlayerPhantomMaps = string.Empty;
        }

        public MapTravelStatePacket(
            bool travelEnabled,
            bool canManage,
            string phantomMapsJson,
            int phantomMapsPage,
            int phantomMapsPageCount,
            string destinationsJson,
            string message,
            bool hasDestinationsCatalog = false,
            string playerPhantomMaps = "")
        {
            TravelEnabled = travelEnabled;
            CanManage = canManage;
            PhantomMapsJson = NormalizeJson(phantomMapsJson);
            PhantomMapsPage = Math.Max(0, phantomMapsPage);
            PhantomMapsPageCount = Math.Max(1, phantomMapsPageCount);
            if (PhantomMapsPage >= PhantomMapsPageCount) PhantomMapsPage = PhantomMapsPageCount - 1;
            DestinationsJson = NormalizeJson(destinationsJson);
            HasDestinationsCatalog = hasDestinationsCatalog;
            Message = Normalize(message, 220);
            PlayerPhantomMaps = Normalize(playerPhantomMaps, MaximumPlayerPhantomMapsLength);
        }


        public void WriteData(IPacket packet)
        {
            packet.Write(TravelEnabled);
            packet.Write(CanManage);
            packet.Write(PhantomMapsJson ?? "[]");
            packet.Write(PhantomMapsPage);
            packet.Write(PhantomMapsPageCount);
            packet.Write(DestinationsJson ?? "[]");
            packet.Write(Message ?? string.Empty);
            packet.Write(HasDestinationsCatalog);
            packet.Write(PlayerPhantomMaps ?? string.Empty);
        }

        public void ReadData(IPacket packet)
        {
            TravelEnabled = packet.ReadBool();
            CanManage = packet.ReadBool();
            PhantomMapsJson = NormalizeJson(packet.ReadString());
            PhantomMapsPage = Math.Max(0, packet.ReadInt());
            PhantomMapsPageCount = Math.Max(1, packet.ReadInt());
            if (PhantomMapsPage >= PhantomMapsPageCount) PhantomMapsPage = PhantomMapsPageCount - 1;
            DestinationsJson = NormalizeJson(packet.ReadString());
            Message = Normalize(packet.ReadString(), 220);
            HasDestinationsCatalog = packet.ReadBool();
            PlayerPhantomMaps = Normalize(packet.ReadString(), MaximumPlayerPhantomMapsLength);
        }

        private static string NormalizeJson(string value)
        {
            return string.IsNullOrWhiteSpace(value) || value.Length > MaximumJsonLength
                ? "[]"
                : value.Trim();
        }

        private static string Normalize(string value, int maximumLength)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            string result = value.Trim();
            return result.Length <= maximumLength ? result : result.Substring(0, maximumLength);
        }
    }
}
