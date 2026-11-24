using System;
using System.Collections.Generic;
using Hkmp.Networking.Packet;
using Newtonsoft.Json;

namespace HKMP.Rounds.Networking.Packets
{
    internal sealed class RoundSettingsPacket : IPacketData
    {
        private const int MaximumMessageLength = 512;
        private const int MaximumLockedScenesJsonLength = 45000;

        public bool AutoStart { get; private set; }
        public int RoundSoul { get; private set; }
        public bool DisableEnemies { get; private set; }
        public bool DebugDisableShade { get; private set; }
        public bool TimerIntegrationEnabled { get; private set; }
        public bool IndividualTeamWins { get; private set; }
        public bool IndependentRoundsPerRoom { get; private set; }
        public bool DisablePvpBeforeRound { get; private set; }
        public bool GiveLifebloodMasks { get; private set; }
        public int ReadyRequiredCount { get; private set; }
        public int WinTarget { get; private set; }
        public bool RoundActive { get; private set; }
        public bool WaitingForPreparation { get; private set; }
        public bool CanManage { get; private set; }
        public bool IsHost { get; private set; }
        public int ReadyPlayerCount { get; private set; }
        public int EffectiveReadyRequiredCount { get; private set; }
        public int ConnectedPlayerCount { get; private set; }
        public bool LocalPlayerReady { get; private set; }
        public bool VignetteEnabled { get; private set; }
        public string LockedRoundScenesJson { get; private set; }
        public bool HidePhantomMantisBackground { get; private set; }
        public string Message { get; private set; }

        public bool IsReliable { get { return true; } }
        public bool DropReliableDataIfNewerExists { get { return true; } }

        public RoundSettingsPacket()
        {
            RoundSoul = 198;
            HidePhantomMantisBackground = true;
            Message = string.Empty;
        }

        public RoundSettingsPacket(
            bool autoStart,
            int roundSoul,
            bool disableEnemies,
            bool debugDisableShade,
            bool timerIntegrationEnabled,
            int readyRequiredCount,
            bool individualTeamWins,
            bool independentRoundsPerRoom,
            bool disablePvpBeforeRound,
            bool giveLifebloodMasks,
            int winTarget,
            bool roundActive,
            bool waitingForPreparation,
            bool canManage,
            bool isHost,
            string message,
            int readyPlayerCount,
            int effectiveReadyRequiredCount,
            int connectedPlayerCount,
            bool localPlayerReady,
            bool vignetteEnabled,
            string lockedRoundScenesJson = "[]",
            bool hidePhantomMantisBackground = true)
        {
            AutoStart = autoStart;
            RoundSoul = roundSoul < 0 ? 0 : roundSoul > 198 ? 198 : roundSoul;
            DisableEnemies = disableEnemies;
            DebugDisableShade = debugDisableShade;
            TimerIntegrationEnabled = timerIntegrationEnabled;
            IndividualTeamWins = individualTeamWins;
            IndependentRoundsPerRoom = independentRoundsPerRoom;
            DisablePvpBeforeRound = disablePvpBeforeRound;
            GiveLifebloodMasks = giveLifebloodMasks;
            ReadyRequiredCount = readyRequiredCount < 0 ? 0 : readyRequiredCount > 1000 ? 1000 : readyRequiredCount;
            WinTarget = winTarget < 0 ? 0 : winTarget > 1000 ? 0 : winTarget;
            RoundActive = roundActive;
            WaitingForPreparation = waitingForPreparation;
            CanManage = canManage;
            IsHost = isHost;
            ReadyPlayerCount = Clamp(readyPlayerCount, 0, 1000);
            EffectiveReadyRequiredCount = Clamp(effectiveReadyRequiredCount, 0, 1000);
            ConnectedPlayerCount = Clamp(connectedPlayerCount, 0, 1000);
            LocalPlayerReady = localPlayerReady;
            VignetteEnabled = vignetteEnabled;
            LockedRoundScenesJson = NormalizeLockedScenes(lockedRoundScenesJson);
            HidePhantomMantisBackground = hidePhantomMantisBackground;
            Message = NormalizeMessage(message);
        }

        public void WriteData(IPacket packet)
        {
            packet.Write(AutoStart);
            packet.Write(RoundSoul);
            packet.Write(DisableEnemies);
            packet.Write(DebugDisableShade);
            packet.Write(TimerIntegrationEnabled);
            packet.Write(ReadyRequiredCount);
            packet.Write(IndividualTeamWins);
            packet.Write(IndependentRoundsPerRoom);
            packet.Write(DisablePvpBeforeRound);
            packet.Write(GiveLifebloodMasks);
            packet.Write(WinTarget);
            packet.Write(RoundActive);
            packet.Write(WaitingForPreparation);
            packet.Write(CanManage);
            packet.Write(IsHost);
            packet.Write(Message ?? string.Empty);
            packet.Write(ReadyPlayerCount);
            packet.Write(EffectiveReadyRequiredCount);
            packet.Write(ConnectedPlayerCount);
            packet.Write(LocalPlayerReady);
            packet.Write(VignetteEnabled);
            packet.Write(LockedRoundScenesJson ?? "[]");
            packet.Write(HidePhantomMantisBackground);
        }

        public void ReadData(IPacket packet)
        {
            AutoStart = packet.ReadBool();
            RoundSoul = packet.ReadInt();
            DisableEnemies = packet.ReadBool();
            DebugDisableShade = packet.ReadBool();
            TimerIntegrationEnabled = packet.ReadBool();
            ReadyRequiredCount = packet.ReadInt();
            IndividualTeamWins = packet.ReadBool();
            IndependentRoundsPerRoom = packet.ReadBool();
            DisablePvpBeforeRound = packet.ReadBool();
            GiveLifebloodMasks = packet.ReadBool();
            WinTarget = packet.ReadInt();
            RoundActive = packet.ReadBool();
            WaitingForPreparation = packet.ReadBool();
            CanManage = packet.ReadBool();
            IsHost = packet.ReadBool();
            Message = NormalizeMessage(packet.ReadString());
            ReadyPlayerCount = packet.ReadInt();
            EffectiveReadyRequiredCount = packet.ReadInt();
            ConnectedPlayerCount = packet.ReadInt();
            LocalPlayerReady = packet.ReadBool();
            VignetteEnabled = packet.ReadBool();
            LockedRoundScenesJson = NormalizeLockedScenes(packet.ReadString());
            HidePhantomMantisBackground = true;
            try
            {
                HidePhantomMantisBackground = packet.ReadBool();
            }
            catch
            {
                HidePhantomMantisBackground = true;
            }
            if (RoundSoul < 0 || RoundSoul > 198) RoundSoul = 198;
            if (ReadyRequiredCount < 0 || ReadyRequiredCount > 1000) ReadyRequiredCount = 0;
            if (WinTarget < 0 || WinTarget > 1000) WinTarget = 0;
            ReadyPlayerCount = Clamp(ReadyPlayerCount, 0, 1000);
            EffectiveReadyRequiredCount = Clamp(EffectiveReadyRequiredCount, 0, 1000);
            ConnectedPlayerCount = Clamp(ConnectedPlayerCount, 0, 1000);
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            return value < minimum ? minimum : value > maximum ? maximum : value;
        }

        private static string NormalizeLockedScenes(string json)
        {
            try
            {
                List<string> source = JsonConvert.DeserializeObject<List<string>>(json ?? "[]");
                if (source == null)
                {
                    return "[\"*\"]";
                }

                List<string> normalized = new List<string>();
                HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                string result = "[]";

                for (int i = 0; i < source.Count; i++)
                {
                    string scene = (source[i] ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(scene) || !seen.Add(scene))
                    {
                        continue;
                    }

                    normalized.Add(scene);
                    string candidate = JsonConvert.SerializeObject(normalized);
                    if (candidate.Length > MaximumLockedScenesJsonLength)
                    {
                        return "[\"*\"]";
                    }

                    result = candidate;
                }

                return result;
            }
            catch
            {
                return "[\"*\"]";
            }
        }

        private static string NormalizeMessage(string message)
        {
            string value = message ?? string.Empty;
            return value.Length <= MaximumMessageLength ? value : value.Substring(0, MaximumMessageLength);
        }
    }
}
