using Hkmp.Api.Command.Server;

namespace HKMP.Rounds.Server.Commands
{
    internal sealed class AutoStartCommand : IServerCommand
    {
        private readonly RoundManager _roundManager;

        public AutoStartCommand(
            RoundManager roundManager)
        {
            _roundManager = roundManager;
        }

        public string Trigger
        {
            get
            {
                return "/autostart";
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
            bool newValue =
                !_roundManager.AutoStartEnabled;

            _roundManager.SetAutoStart(
                newValue
            );

            sender.SendMessage(
                "Автостарт " +
                (
                    _roundManager.AutoStartEnabled
                        ? "включён."
                        : "выключен."
                )
            );
        }
    }
}