using Hkmp.Networking.Packet;

namespace HKMP.Rounds.Networking.Packets
{
    internal sealed class PlayerBenchStatePacket : IPacketData
    {
        public bool AtBench { get; private set; }
        public bool ReturnToBenchPending { get; private set; }
        public bool HeroAlive { get; private set; }
        public bool DeathStarted { get; private set; }

        public bool IsReliable { get { return true; } }
        public bool DropReliableDataIfNewerExists { get { return false; } }

        public PlayerBenchStatePacket()
        {
        }

        public PlayerBenchStatePacket(bool atBench, bool returnToBenchPending, bool heroAlive, bool deathStarted)
        {
            AtBench = atBench;
            ReturnToBenchPending = returnToBenchPending;
            HeroAlive = heroAlive;
            DeathStarted = deathStarted;
        }

        public void WriteData(IPacket packet)
        {
            packet.Write(AtBench);
            packet.Write(ReturnToBenchPending);
            packet.Write(HeroAlive);
            packet.Write(DeathStarted);
        }

        public void ReadData(IPacket packet)
        {
            AtBench = packet.ReadBool();
            ReturnToBenchPending = packet.ReadBool();
            HeroAlive = packet.ReadBool();
            DeathStarted = packet.ReadBool();
        }
    }
}
