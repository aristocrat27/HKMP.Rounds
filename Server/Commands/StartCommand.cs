using Hkmp.Api.Command.Server;
using Hkmp.Api.Command;

namespace HKMP.Rounds.Server.Commands
{
    internal sealed class StartCommand : IServerCommand
    {
        private readonly RoundManager _roundManager;

        public StartCommand(
            RoundManager roundManager)
        {
            _roundManager = roundManager;
        }

        public string Trigger
        {
            get
            {
                return "/start";
            }
        }

        public string[] Aliases
        {
            get
            {
                return new[] { "/round" };
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
            IPlayerCommandSender playerSender = sender as IPlayerCommandSender;
            if (playerSender != null)
            {
                using (_roundManager.EnterPlayerContext(playerSender.Id))
                {
                    ExecuteInCurrentContext(sender, arguments);
                }
                return;
            }

            ExecuteInCurrentContext(sender, arguments);
        }

        private void ExecuteInCurrentContext(ICommandSender sender, string[] arguments)
        {
            if (arguments != null && arguments.Length > 1)
            {
                sender.SendMessage("Round administration is available in the HKMP.Rounds settings UI.");
                return;
            }

            if (_roundManager.IsRoundActive ||
                _roundManager.IsWaitingForPreparation)
            {
                return;
            }

            if (!_roundManager.StartRound())
            {
                return;
            }

            sender.SendMessage(
                "Round started."
            );
        }
    }
}
