using Hkmp.Api.Command.Server;

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