using Hkmp.Networking.Packet;

namespace HKMP.Rounds.Networking.Packets
{
    internal sealed class RoundStartPacket : IPacketData
    {
        public uint RoundId { get; private set; }

        public int RoundSoul { get; private set; }

        public bool DisableEnemies { get; private set; }

        public bool DebugDisableShade { get; private set; }

        public bool IsReliable
        {
            get
            {
                return true;
            }
        }

        public bool DropReliableDataIfNewerExists
        {
            get
            {
                return false;
            }
        }

        public RoundStartPacket()
        {
            RoundSoul = 198;
        }

        public RoundStartPacket(
            uint roundId,
            int roundSoul,
            bool disableEnemies,
            bool debugDisableShade)
        {
            RoundId = roundId;
            RoundSoul = roundSoul;
            DisableEnemies = disableEnemies;
            DebugDisableShade = debugDisableShade;
        }

        public void WriteData(
            IPacket packet)
        {
            packet.Write(RoundId);
            packet.Write(RoundSoul);
            packet.Write(DisableEnemies);
            packet.Write(DebugDisableShade);
        }

        public void ReadData(
            IPacket packet)
        {
            RoundId = packet.ReadUInt();
            RoundSoul = packet.ReadInt();
            DisableEnemies = packet.ReadBool();
            DebugDisableShade = packet.ReadBool();
        }
    }
}
