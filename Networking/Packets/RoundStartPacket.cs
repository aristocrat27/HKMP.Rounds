using Hkmp.Networking.Packet;
using HKMP.Rounds.Settings;

namespace HKMP.Rounds.Networking.Packets
{
    internal sealed class RoundStartPacket : IPacketData
    {
        public uint RoundId { get; private set; }

        public int RoundSoul { get; private set; }

        public bool DisableEnemies { get; private set; }

        public bool DebugDisableShade { get; private set; }

        public PvpMode PvpMode { get; private set; }

        public bool GiveLifebloodMasks { get; private set; }

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
            PvpMode = HKMP.Rounds.Settings.PvpMode.Quick;
        }

        public RoundStartPacket(
            uint roundId,
            int roundSoul,
            bool disableEnemies,
            bool debugDisableShade,
            PvpMode pvpMode,
            bool giveLifebloodMasks)
        {
            RoundId = roundId;
            RoundSoul = roundSoul;
            DisableEnemies = disableEnemies;
            DebugDisableShade = debugDisableShade;
            PvpMode = pvpMode == HKMP.Rounds.Settings.PvpMode.Long || pvpMode == HKMP.Rounds.Settings.PvpMode.OneShot ? pvpMode : HKMP.Rounds.Settings.PvpMode.Quick;
            GiveLifebloodMasks = giveLifebloodMasks;
        }

        public void WriteData(
            IPacket packet)
        {
            packet.Write(RoundId);
            packet.Write(RoundSoul);
            packet.Write(DisableEnemies);
            packet.Write(DebugDisableShade);
            packet.Write((byte)PvpMode);
            packet.Write(GiveLifebloodMasks);
        }

        public void ReadData(
            IPacket packet)
        {
            RoundId = packet.ReadUInt();
            RoundSoul = packet.ReadInt();
            DisableEnemies = packet.ReadBool();
            DebugDisableShade = packet.ReadBool();
            PvpMode mode = (PvpMode)packet.ReadByte();
            PvpMode = mode == HKMP.Rounds.Settings.PvpMode.Long || mode == HKMP.Rounds.Settings.PvpMode.OneShot ? mode : HKMP.Rounds.Settings.PvpMode.Quick;
            GiveLifebloodMasks = packet.ReadBool();
        }
    }
}
