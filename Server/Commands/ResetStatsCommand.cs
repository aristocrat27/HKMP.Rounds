using Hkmp.Api.Command.Server;
using HKMP.Rounds.Server.Stats;

namespace HKMP.Rounds.Server.Commands
{
    internal sealed class ResetStatsCommand : IServerCommand
    {
        private readonly StatsManager _stats;
        private readonly RoundManager _roundManager;

        public ResetStatsCommand(
            StatsManager stats,
            RoundManager roundManager)
        {
            _stats = stats;
            _roundManager = roundManager;
        }

        public string Trigger
        {
            get
            {
                return "/resetstats";
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
                return true;
            }
        }

        public void Execute(
            ICommandSender sender,
            string[] arguments)
        {
            _stats.Reset();
            _roundManager.ResetMatchScores();

            sender.SendMessage(
                "Statistics and current match scores have been reset.");
        }
    }
}
