using Hkmp.Api.Command.Server;
using HKMP.Rounds.Server.Stats;

namespace HKMP.Rounds.Server.Commands
{
    internal sealed class ResetStatsCommand : IServerCommand
    {
        private readonly StatsManager _stats;
        public ResetStatsCommand(StatsManager stats) { _stats = stats; }
        public string Trigger { get { return "/resetstats"; } }
        public string[] Aliases { get { return new string[0]; } }
        public bool AuthorizedOnly { get { return true; } }

        public void Execute(ICommandSender sender, string[] arguments)
        {
            _stats.Reset();
            sender.SendMessage("Статистика сброшена.");
        }
    }
}
