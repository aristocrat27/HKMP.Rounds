using Hkmp.Api.Command.Server;
using HKMP.Rounds.Server.Stats;

namespace HKMP.Rounds.Server.Commands
{
    internal sealed class StatsCommand : IServerCommand
    {
        private readonly StatsManager _stats;

        public StatsCommand(
            StatsManager stats)
        {
            _stats = stats;
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

            var data =
                _stats.GetSortedStats();

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
