using System;
using Hkmp.Networking.Packet;

namespace HKMP.Rounds.Networking.Packets
{
    internal sealed class MapPresetCommandPacket : IPacketData
    {
        public int PresetMapId { get; private set; }
        public bool IsReliable { get { return true; } }
        public bool DropReliableDataIfNewerExists { get { return false; } }

        public MapPresetCommandPacket()
        {
        }

        public MapPresetCommandPacket(int presetMapId)
        {
            PresetMapId = Math.Max(0, presetMapId);
        }

        public void WriteData(IPacket packet)
        {
            packet.Write(PresetMapId);
        }

        public void ReadData(IPacket packet)
        {
            PresetMapId = Math.Max(0, packet.ReadInt());
        }
    }
}
