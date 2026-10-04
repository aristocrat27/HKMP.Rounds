using System.Collections.Generic;
using UnityEngine;
using Modding;
using Hkmp.Api.Client;
using Hkmp.Api.Server;
using HKMP.Rounds.Client;
using HKMP.Rounds.Server;
using HKMP.Rounds.Settings;

namespace HKMP.Rounds
{
    public sealed class HKMPRounds : Mod, IMenuMod
    {
        internal static GlobalSettingsStore GlobalSettings
        {
            get;
            private set;
        }

        public override string GetVersion()
        {
            return RoundsConstants.Version;
        }

        public bool ToggleButtonInsideMenu
        {
            get
            {
                return false;
            }
        }

        public override void Initialize(
            Dictionary<string, Dictionary<string, GameObject>> preloadedObjects)
        {
            Modding.Logger.Log(
                "Initializing HKMP.Rounds " +
                RoundsConstants.Version);

            GlobalSettings =
                new GlobalSettingsStore();

            ServerAddon.RegisterAddon(
                new RoundsServerAddon(
                    GlobalSettings));

            ClientAddon.RegisterAddon(
                new RoundsClientAddon());

            Modding.Logger.Log(
                "HKMP.Rounds initialized.");
        }

        public List<IMenuMod.MenuEntry> GetMenuData(
            IMenuMod.MenuEntry? toggleButtonEntry)
        {
            return new List<IMenuMod.MenuEntry>
            {
                new IMenuMod.MenuEntry
                {
                    Name =
                        "Auto Start",

                    Description =
                        "Start a round automatically when all connected players use /rd.",

                    Values =
                        new[]
                        {
                            "Off",
                            "On"
                        },

                    Saver =
                        value =>
                        {
                            GetSettings().SetAutoStart(
                                value == 1);
                        },

                    Loader =
                        () =>
                            GetSettings().AutoStart
                                ? 1
                                : 0
                },

                new IMenuMod.MenuEntry
                {
                    Name =
                        "Timer Integration",

                    Description =
                        "Automatically start and stop the HKMP.Timer countdown or stopwatch with rounds.",

                    Values =
                        new[]
                        {
                            "Off",
                            "On"
                        },

                    Saver =
                        value =>
                        {
                            GetSettings().SetTimerIntegration(
                                value == 1);
                        },

                    Loader =
                        () =>
                            GetSettings().TimerIntegrationEnabled
                                ? 1
                                : 0
                },

                new IMenuMod.MenuEntry
                {
                    Name =
                        "Disable Mobs",

                    Description =
                        "Disable regular enemy spawns at all times.",

                    Values =
                        new[]
                        {
                            "Off",
                            "On"
                        },

                    Saver =
                        value =>
                        {
                            bool enabled = value == 1;

                            GetSettings().SetDisableEnemies(
                                enabled);

                            RoundEnemyController.SetPersistentDisabled(
                                enabled);
                        },

                    Loader =
                        () =>
                            GetSettings().DisableEnemies
                                ? 1
                                : 0
                },

                new IMenuMod.MenuEntry
                {
                    Name =
                        "Disable Shade",

                    Description =
                        "Prevent the Shade from spawning after death at all times.",

                    Values =
                        new[]
                        {
                            "Off",
                            "On"
                        },

                    Saver =
                        value =>
                        {
                            bool enabled = value == 1;

                            GetSettings().SetDebugDisableShade(
                                enabled);

                            DebugShadeController.SetPersistentDisabled(
                                enabled);
                        },

                    Loader =
                        () =>
                            GetSettings().DebugDisableShade
                                ? 1
                                : 0
                }
            };
        }

        private static GlobalSettingsStore GetSettings()
        {
            if (GlobalSettings == null)
            {
                GlobalSettings =
                    new GlobalSettingsStore();
            }

            return GlobalSettings;
        }
    }
}
