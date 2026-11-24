using Hkmp.Networking.Packet;

namespace HKMP.Rounds.Networking.Packets
{
    internal sealed class RoundSettingsRequestPacket : IPacketData
    {
        public bool IsQuery { get; private set; }
        public RoundSettingKey Key { get; private set; }
        public bool BoolValue { get; private set; }
        public int IntValue { get; private set; }

        public bool IsReliable { get { return true; } }
        public bool DropReliableDataIfNewerExists { get { return false; } }

        public RoundSettingsRequestPacket()
        {
        }

        public RoundSettingsRequestPacket(bool isQuery, RoundSettingKey key, bool boolValue, int intValue)
        {
            IsQuery = isQuery;
            Key = NormalizeKey(key);
            BoolValue = boolValue;
            IntValue = intValue;
        }

        public void WriteData(IPacket packet)
        {
            packet.Write(IsQuery);
            packet.Write((byte)Key);
            packet.Write(BoolValue);
            packet.Write(IntValue);
        }

        public void ReadData(IPacket packet)
        {
            IsQuery = packet.ReadBool();
            Key = NormalizeKey((RoundSettingKey)packet.ReadByte());
            BoolValue = packet.ReadBool();
            IntValue = packet.ReadInt();
        }

        private static RoundSettingKey NormalizeKey(RoundSettingKey key)
        {
            switch (key)
            {
                case RoundSettingKey.RoundSoul:
                case RoundSettingKey.DisableEnemies:
                case RoundSettingKey.DebugDisableShade:
                case RoundSettingKey.TimerIntegration:
                case RoundSettingKey.ReadyRequiredCount:
                case RoundSettingKey.IndividualTeamWins:
                case RoundSettingKey.DisablePvpBeforeRound:
                case RoundSettingKey.GiveLifebloodMasks:
                case RoundSettingKey.VignetteEnabled:
                case RoundSettingKey.IndependentRoundsPerRoom:
                case RoundSettingKey.AutoStart:
                case RoundSettingKey.HidePhantomMantisBackground:
                    return key;
                default:
                    return RoundSettingKey.Invalid;
            }
        }
    }
}
