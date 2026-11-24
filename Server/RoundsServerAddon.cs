using Hkmp.Api.Server;
using HKMP.Rounds.Server.Commands;
using HKMP.Rounds.Server.Stats;
using HKMP.Rounds.Settings;
using Modding;

namespace HKMP.Rounds.Server
{
    internal sealed class RoundsServerAddon : ServerAddon
    {
        private readonly GlobalSettingsStore _settings;
        private RoundManager _roundManager;
        private RoundServerNetManager _network;

        public RoundsServerAddon(GlobalSettingsStore settings)
        {
            _settings = settings ?? new GlobalSettingsStore();
        }

        protected override string Name
        {
            get { return RoundsConstants.Name; }
        }

        protected override string Version
        {
            get { return RoundsConstants.Version; }
        }

        public override bool NeedsNetwork
        {
            get { return true; }
        }

        public override void Initialize(IServerApi serverApi)
        {
            StatsManager statsManager = new StatsManager();
            HkmpGlobalSettingsSaveGuard.Initialize();
            _roundManager = new RoundManager(serverApi, statsManager, _settings);
            _network = new RoundServerNetManager(this, serverApi, _roundManager, _settings, statsManager);
            _roundManager.SetNetwork(_network);
            _roundManager.SetTimerIntegration(new RoundsTimerIntegration(_roundManager, _settings));

            serverApi.CommandManager.RegisterCommand(new StartCommand(_roundManager));
            serverApi.CommandManager.RegisterCommand(new ReadyCommand(_roundManager));

            serverApi.ServerManager.PlayerConnectEvent += OnPlayerConnect;
            serverApi.ServerManager.PlayerDisconnectEvent += OnPlayerDisconnect;
            serverApi.ServerManager.PlayerEnterSceneEvent += OnPlayerEnterScene;

            Modding.Logger.Log(
                "[HKMP.Rounds] Server initialized. " +
                "AutoStart=" + _settings.AutoStart +
                " RoundSoul=" + _settings.RoundSoul +
                " ReadyRequiredCount=" + _settings.ReadyRequiredCount +
                " DisableEnemies=" + _settings.DisableEnemies +
                " DebugDisableShade=" + _settings.DebugDisableShade +
                " TimerIntegration=" + _settings.TimerIntegrationEnabled +
                " SharedBench=" + (_settings.HasSharedBench ? _settings.SharedBenchScene + "/" + _settings.SharedBenchMarker : "unset"));

            foreach (IServerPlayer player in serverApi.ServerManager.Players)
            {
                if (player != null)
                {
                    _network.SendBenchSettings(player.Id, string.Empty);
                    _network.SendPvpModeState(player.Id, string.Empty);
                    _network.SendRoundSettingsState(player.Id, string.Empty);
                    _network.SendRoundStats(player.Id);
                    _network.SendMapTravelState(player.Id, 0, string.Empty);
                }
            }
        }

        private void OnPlayerConnect(IServerPlayer player)
        {
            if (player == null)
            {
                return;
            }

            _network.NotifyPlayerConnected(player);
            _roundManager.OnPlayerConnect(player);
            _network.SendBenchSettings(player.Id, string.Empty);
            _network.SendPvpModeState(player.Id, string.Empty);
            _network.SendRoundSettingsState(player.Id, string.Empty);
            _network.SendRoundStats(player.Id);
            _network.SendMapTravelState(player.Id, 0, string.Empty);
            _network.BroadcastRoundSettingsState(string.Empty);
        }

        private void OnPlayerEnterScene(IServerPlayer player)
        {
            if (player == null)
            {
                return;
            }

            _roundManager.OnPlayerEnterScene(player);
            _network.BroadcastMapTravelState(string.Empty);
        }

        private void OnPlayerDisconnect(IServerPlayer player)
        {
            if (player == null)
            {
                return;
            }

            _roundManager.OnPlayerDisconnect(player.Id, player.Username);
            _network.NotifyPlayerDisconnected(player);
            _network.BroadcastRoundSettingsState(string.Empty);
        }
    }
}
