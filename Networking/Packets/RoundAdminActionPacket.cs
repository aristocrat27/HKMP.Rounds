using Hkmp.Networking.Packet;
using HKMP.Rounds.Networking;

namespace HKMP.Rounds.Networking.Packets
{
    internal sealed class RoundAdminActionPacket : IPacketData
    {
        public RoundAdminAction Action { get; private set; }
        public int Value { get; private set; }

        public bool IsReliable { get { return true; } }
        public bool DropReliableDataIfNewerExists { get { return false; } }

        public RoundAdminActionPacket()
        {
        }

        public RoundAdminActionPacket(RoundAdminAction action, int value)
        {
            Action = NormalizeAction(action);
            Value = value;
        }

        public void WriteData(IPacket packet)
        {
            packet.Write((byte)Action);
            packet.Write(Value);
        }

        public void ReadData(IPacket packet)
        {
            Action = NormalizeAction((RoundAdminAction)packet.ReadByte());
            Value = packet.ReadInt();
        }

        private static RoundAdminAction NormalizeAction(RoundAdminAction action)
        {
            switch (action)
            {
                case RoundAdminAction.EndRound:
                case RoundAdminAction.SetWinTarget:
                case RoundAdminAction.ResetStatistics:
                case RoundAdminAction.RequestStatistics:
                case RoundAdminAction.ToggleReady:
                case RoundAdminAction.WarpPlayerToBench:
                case RoundAdminAction.WarpSelfToNearestBench:
                case RoundAdminAction.ToggleGameFreeze:
                    return action;
                default:
                    return (RoundAdminAction)255;
            }
        }
    }
}
