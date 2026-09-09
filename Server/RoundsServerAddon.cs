using Hkmp.Api.Server;

using HKMP.Rounds.Server.Commands;
using HKMP.Rounds.Server.Stats;
using HKMP.Rounds.Settings;

using Modding;

namespace HKMP.Rounds.Server
{
    internal sealed class RoundsServerAddon : ServerAddon
    {
        private RoundManager _roundManager;

        protected override string Name
        {
            get
            {
                return RoundsConstants.Name;
            }
        }

        protected override string Version
        {
            get
            {
                return RoundsConstants.Version;
            }
        }

        public override bool NeedsNetwork
        {
            get
            {
                return true;
            }
        }

        public override void Initialize(
            IServerApi serverApi)
        {
            StatsManager statsManager =
                new StatsManager();

            GlobalSettingsStore settings =
                new GlobalSettingsStore();

            _roundManager =
                new RoundManager(
                    serverApi,
                    statsManager,
                    settings
                );

            RoundServerNetManager network =
                new RoundServerNetManager(
                    this,
                    serverApi,
                    _roundManager
                );

            _roundManager.SetNetwork(
                network
            );

            serverApi.CommandManager.RegisterCommand(
                new StartCommand(
                    _roundManager
                )
            );

            serverApi.CommandManager.RegisterCommand(
                new ReadyCommand(
                    _roundManager
                )
            );

            serverApi.CommandManager.RegisterCommand(
                new AutoStartCommand(
                    _roundManager
                )
            );

            serverApi.CommandManager.RegisterCommand(
                new StatsCommand(
                    statsManager
                )
            );

            serverApi.CommandManager.RegisterCommand(
                new ResetStatsCommand(
                    statsManager
                )
            );

            serverApi.ServerManager.PlayerConnectEvent +=
                OnPlayerConnect;

            serverApi.ServerManager.PlayerDisconnectEvent +=
                OnPlayerDisconnect;

            Modding.Logger.Log(
                "[HKMP.Rounds] Server initialized. " +
                "AutoStart=" +
                settings.AutoStart
            );
        }

        private void OnPlayerConnect(
            IServerPlayer player)
        {
            if (player == null)
            {
                return;
            }

            _roundManager.OnPlayerConnect(
                player
            );
        }

        private void OnPlayerDisconnect(
            IServerPlayer player)
        {
            if (player == null)
            {
                return;
            }

            _roundManager.OnPlayerDisconnect(
                player.Id
            );
        }
    }
}