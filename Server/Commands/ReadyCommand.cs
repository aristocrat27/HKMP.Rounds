using Hkmp.Api.Command;
using Hkmp.Api.Command.Server;

namespace HKMP.Rounds.Server.Commands
{
    internal sealed class ReadyCommand : IServerCommand
    {
        private readonly RoundManager _roundManager;

        public ReadyCommand(RoundManager roundManager)
        {
            _roundManager = roundManager;
        }

        public string Trigger
        {
            get { return "/rd"; }
        }

        public string[] Aliases
        {
            get
            {
                return new[]
                {
                    "/рд",
                    "/РД",
                    "/RD",
                    "/ready"
                };
            }
        }

        public bool AuthorizedOnly
        {
            get { return false; }
        }

        public void Execute(ICommandSender sender, string[] arguments)
        {
            if (sender == null)
            {
                return;
            }

            if (arguments == null || arguments.Length != 1)
            {
                sender.SendMessage("Use /rd to toggle your ready status for the next round.");
                return;
            }

            IPlayerCommandSender playerSender = sender as IPlayerCommandSender;
            if (playerSender == null)
            {
                sender.SendMessage("Only a connected player can change ready status.");
                return;
            }

            string message;
            if (!_roundManager.ToggleReady(playerSender.Id, out message) &&
                string.IsNullOrWhiteSpace(message))
            {
                message = "Could not change your ready status.";
            }

            if (!string.IsNullOrWhiteSpace(message))
            {
                sender.SendMessage(message);
            }
        }
    }
}
