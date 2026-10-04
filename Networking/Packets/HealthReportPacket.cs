using Hkmp.Networking.Packet;

namespace HKMP.Rounds.Networking.Packets
{
    internal sealed class HealthReportPacket : IPacketData
    {
        public uint RoundId { get; private set; }

        public int Health { get; private set; }

        public int MaxHealth { get; private set; }

        public int BlueHealth { get; private set; }

        public int Soul { get; private set; }

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

        public HealthReportPacket()
        {
        }

        public HealthReportPacket(
            uint roundId,
            int health,
            int maxHealth,
            int blueHealth,
            int soul)
        {
            RoundId = roundId;
            Health = health;
            MaxHealth = maxHealth;
            BlueHealth = blueHealth;
            Soul = soul;
        }

        public void WriteData(
            IPacket packet)
        {
            packet.Write(RoundId);
            packet.Write(Health);
            packet.Write(MaxHealth);
            packet.Write(BlueHealth);
            packet.Write(Soul);
        }

        public void ReadData(
            IPacket packet)
        {
            RoundId = packet.ReadUInt();
            Health = packet.ReadInt();
            MaxHealth = packet.ReadInt();
            BlueHealth = packet.ReadInt();
            Soul = packet.ReadInt();
        }
    }
}
