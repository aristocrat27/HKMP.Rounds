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
            IPlayerCommandSender playerSender =
                sender as IPlayerCommandSender;

            if (playerSender == null)
            {
                sender.SendMessage(
                    "/rd можно использовать только игроку."
                );

                return;
            }

            if (!_roundManager.AutoStartEnabled)
            {
                sender.SendMessage(
                    "Автостарт выключен."
                );

                return;
            }

            if (_roundManager.IsRoundActive ||
                _roundManager.IsWaitingForPreparation)
            {
                sender.SendMessage(
                    "Сейчас уже идёт раунд или его подготовка."
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
                    "Не удалось отметить готовность."
                );

                return;
            }

            sender.SendMessage(
                "Вы готовы к следующему раунду."
            );
        }
    }
}