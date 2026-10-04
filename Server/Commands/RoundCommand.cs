using Hkmp.Api.Command.Server;
using System;

namespace HKMP.Rounds.Server.Commands
{
    internal sealed class RoundCommand : IServerCommand
    {
        private readonly RoundManager _roundManager;

        public RoundCommand(
            RoundManager roundManager)
        {
            _roundManager = roundManager;
        }

        public string Trigger
        {
            get
            {
                return "/round";
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
            if (arguments == null ||
                arguments.Length < 2)
            {
                if (_roundManager.IsRoundActive ||
                    _roundManager.IsWaitingForPreparation)
                {
                    sender.SendMessage(
                        "A round is already active or being prepared.");
                    return;
                }

                if (_roundManager.StartRound())
                {
                    sender.SendMessage(
                        "Round preparation started.");
                }
                else
                {
                    sender.SendMessage(
                        "Could not start a round.");
                }

                return;
            }

            string command =
                arguments[1];

            if (string.Equals(
                command,
                "end",
                StringComparison.OrdinalIgnoreCase))
            {
                if (!_roundManager.EndRoundManually())
                {
                    sender.SendMessage(
                        "There is no active round.");
                    return;
                }

                sender.SendMessage(
                    "Round ended.");
                return;
            }

            if (string.Equals(
                command,
                "wins",
                StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    command,
                    "win",
                    StringComparison.OrdinalIgnoreCase))
            {
                SetWinTarget(
                    sender,
                    arguments);
                return;
            }

            if (string.Equals(
                command,
                "mp",
                StringComparison.OrdinalIgnoreCase))
            {
                SetRoundSoul(
                    sender,
                    arguments);
                return;
            }

            if (string.Equals(
                command,
                "mobs",
                StringComparison.OrdinalIgnoreCase))
            {
                SetDisableEnemies(
                    sender,
                    arguments);
                return;
            }

            if (string.Equals(
                command,
                "debug",
                StringComparison.OrdinalIgnoreCase))
            {
                SetDebugOption(
                    sender,
                    arguments);
                return;
            }

            SendUsage(sender);
        }

        private void SetWinTarget(
            ICommandSender sender,
            string[] arguments)
        {
            if (arguments.Length < 3)
            {
                sender.SendMessage(
                    "Usage: /round wins <number|off>");
                return;
            }

            if (_roundManager.IsRoundActive ||
                _roundManager.IsWaitingForPreparation)
            {
                sender.SendMessage(
                    "The win target can only be changed before a round starts.");
                return;
            }

            string value =
                arguments[2];

            if (string.Equals(
                value,
                "off",
                StringComparison.OrdinalIgnoreCase))
            {
                if (_roundManager.SetWinTarget(0))
                {
                    sender.SendMessage(
                        "Match win target disabled.");
                }
                else
                {
                    sender.SendMessage(
                        "Could not change the match win target.");
                }

                return;
            }

            int target;

            if (!int.TryParse(
                value,
                out target) ||
                target < 1 ||
                target > 1000)
            {
                sender.SendMessage(
                    "Win target must be between 1 and 1000.");
                return;
            }

            if (!_roundManager.SetWinTarget(target))
            {
                sender.SendMessage(
                    "Could not set the match win target.");
                return;
            }

            sender.SendMessage(
                "Match win target set to " +
                target + ".");
        }

        private void SetRoundSoul(
            ICommandSender sender,
            string[] arguments)
        {
            if (arguments.Length < 3)
            {
                sender.SendMessage(
                    "Current round MP: " +
                    _roundManager.RoundSoul +
                    ". Usage: /round mp <0-198>");
                return;
            }

            if (_roundManager.IsRoundActive ||
                _roundManager.IsWaitingForPreparation)
            {
                sender.SendMessage(
                    "Round MP can only be changed before a round starts.");
                return;
            }

            int amount;

            if (!int.TryParse(
                arguments[2],
                out amount) ||
                amount < 0 ||
                amount > 198)
            {
                sender.SendMessage(
                    "Round MP must be between 0 and 198.");
                return;
            }

            if (!_roundManager.SetRoundSoul(amount))
            {
                sender.SendMessage(
                    "Could not set round MP.");
                return;
            }

            sender.SendMessage(
                "Round MP set to " +
                amount + ".");
        }

        private void SetDisableEnemies(
            ICommandSender sender,
            string[] arguments)
        {
            if (arguments.Length < 3)
            {
                sender.SendMessage(
                    "Enemy disabling: " +
                    (_roundManager.DisableEnemies ? "on" : "off") +
                    ". Usage: /round mobs <on|off>");
                return;
            }

            if (_roundManager.IsRoundActive ||
                _roundManager.IsWaitingForPreparation)
            {
                sender.SendMessage(
                    "Enemy disabling can only be changed before a round starts.");
                return;
            }

            bool enabled;

            if (!TryParseToggle(
                arguments[2],
                out enabled))
            {
                sender.SendMessage(
                    "Usage: /round mobs <on|off>");
                return;
            }

            _roundManager.SetDisableEnemies(
                enabled);

            sender.SendMessage(
                "Enemy disabling " +
                (enabled ? "enabled." : "disabled."));
        }

        private void SetDebugOption(
            ICommandSender sender,
            string[] arguments)
        {
            if (arguments.Length < 4 ||
                !string.Equals(
                    arguments[2],
                    "shade",
                    StringComparison.OrdinalIgnoreCase))
            {
                sender.SendMessage(
                    "Usage: /round debug shade <on|off>");
                return;
            }

            if (_roundManager.IsRoundActive ||
                _roundManager.IsWaitingForPreparation)
            {
                sender.SendMessage(
                    "Debug settings can only be changed before a round starts.");
                return;
            }

            bool enabled;

            if (!TryParseToggle(
                arguments[3],
                out enabled))
            {
                sender.SendMessage(
                    "Usage: /round debug shade <on|off>");
                return;
            }

            _roundManager.SetDebugDisableShade(
                enabled);

            sender.SendMessage(
                "Debug shade suppression " +
                (enabled ? "enabled." : "disabled."));
        }

        private static bool TryParseToggle(
            string value,
            out bool enabled)
        {
            enabled = false;

            if (string.Equals(
                value,
                "on",
                StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    value,
                    "true",
                    StringComparison.OrdinalIgnoreCase))
            {
                enabled = true;
                return true;
            }

            if (string.Equals(
                value,
                "off",
                StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    value,
                    "false",
                    StringComparison.OrdinalIgnoreCase))
            {
                enabled = false;
                return true;
            }

            return false;
        }

        private static void SendUsage(
            ICommandSender sender)
        {
            sender.SendMessage(
                "Usage: /round [end|wins <number|off>|mp <0-198>|mobs <on|off>|debug shade <on|off>]");
        }
    }
}
