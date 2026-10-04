using Hkmp.Networking.Packet;

namespace HKMP.Rounds.Networking.Packets
{
    internal sealed class DeathResolutionPacket : IPacketData
    {
        public uint RoundId
        {
            get;
            private set;
        }

        public int AlivePlayersRemaining
        {
            get;
            private set;
        }

        public bool RoundEnded
        {
            get;
            private set;
        }

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

        public DeathResolutionPacket()
        {
        }

        public DeathResolutionPacket(
            uint roundId,
            int alivePlayersRemaining,
            bool roundEnded)
        {
            RoundId =
                roundId;

            AlivePlayersRemaining =
                alivePlayersRemaining;

            RoundEnded =
                roundEnded;
        }

        public void WriteData(
            IPacket packet)
        {
            packet.Write(
                RoundId);

            packet.Write(
                AlivePlayersRemaining);

            packet.Write(
                RoundEnded);
        }

        public void ReadData(
            IPacket packet)
        {
            RoundId =
                packet.ReadUInt();

            AlivePlayersRemaining =
                packet.ReadInt();

            RoundEnded =
                packet.ReadBool();
        }
    }
}