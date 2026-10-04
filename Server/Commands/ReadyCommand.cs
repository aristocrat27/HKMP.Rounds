using Hkmp.Api.Command;
using Hkmp.Api.Command.Server;

namespace HKMP.Rounds.Server.Commands
{
    internal sealed class ReadyCommand : IServerCommand
    {
        private readonly RoundManager _roundManager;

        public ReadyCommand(
            RoundManager roundManager)
        {
            _roundManager = roundManager;
        }

        public string Trigger
        {
            get
            {
                return "/rd";
            }
        }
          
        public string[] Aliases
        {
            get
            {
                return new[]
                {
                    "/рд",
                    "/ready"
                };
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
            IPlayerCommandSender playerSender =
                sender as IPlayerCommandSender;

            if (playerSender == null)
            {
                sender.SendMessage(
                    "/rd can only be used by a player."
                );

                return;
            }

            if (!_roundManager.AutoStartEnabled)
            {
                sender.SendMessage(
                    "Autostart is disabled."
                );

                return;
            }

            if (_roundManager.IsRoundActive ||
                _roundManager.IsWaitingForPreparation)
            {
                sender.SendMessage(
                    "A round is already active or preparing."
                );

                return;
            }

            bool markedReady =
                _roundManager.MarkReady(
                    playerSender.Id
                );

            if (!markedReady)
            {
                sender.SendMessage(
                    "Could not mark you as ready."
                );

                return;
            }

            sender.SendMessage(
                "You are ready for the next round."
            );
        }
    }
}