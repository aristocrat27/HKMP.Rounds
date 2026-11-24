using Hkmp.Api.Command.Server;
using Hkmp.Api.Server;
using HKMP.Rounds.Settings;
using HKMP.Rounds.Server.Stats;

namespace HKMP.Rounds.Server.Commands
{
    internal sealed class StatsCommand : IServerCommand
    {
        private readonly StatsManager _stats;
        private readonly GlobalSettingsStore _settings;
        private readonly IServerApi _serverApi;

        public StatsCommand(
            StatsManager stats,
            GlobalSettingsStore settings,
            IServerApi serverApi)
        {
            _stats = stats;
            _settings = settings;
            _serverApi = serverApi;
        }

        public string Trigger
        {
            get
            {
                return "/stats";
            }
        }

        public string[] Aliases
        {
            get
            {
                return new string[0];
            }
        }

        public bool AuthorizedOnly
        {
            get
            {
                return false;
            }
        }

        public void Execute(
            ICommandSender sender,
            string[] arguments)
        {
            sender.SendMessage(
                "=== HKMP.Rounds ===");

            if (_stats == null)
            {
                sender.SendMessage(
                    "Statistics are unavailable.");
                return;
            }

            bool teamsEnabled =
                _serverApi != null &&
                _serverApi.ServerManager != null &&
                _serverApi.ServerManager.ServerSettings != null &&
                _serverApi.ServerManager.ServerSettings.TeamsEnabled;

            bool individualTeamWins =
                _settings != null && _settings.IndividualTeamWins;

            var data =
                _stats.GetSortedStats(teamsEnabled, individualTeamWins);

            if (data.Count == 0)
            {
                sender.SendMessage(
                    "Statistics are empty.");
                return;
            }

            for (int i = 0;
                 i < data.Count;
                 i++)
            {
                sender.SendMessage(
                    (i + 1) +
                    ". " +
                    data[i].PlayerName +
                    " — " +
                    data[i].Wins +
                    " wins");
            }
        }
    }
}
