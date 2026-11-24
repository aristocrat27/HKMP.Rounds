using System;
using System.Collections.Generic;
using Hkmp.Api.Client;
using Hkmp.Api.Server;
using HKMP.Rounds.Client;
using HKMP.Rounds.Server;
using HKMP.Rounds.Settings;
using InControl;
using Modding;
using Modding.Converters;
using Newtonsoft.Json;
using Satchel.BetterMenus;
using UnityEngine;

namespace HKMP.Rounds
{
    public sealed class HKMPRoundsMenuActions : PlayerActionSet
    {
        public PlayerAction OpenSettings;

        public HKMPRoundsMenuActions()
        {
            OpenSettings = CreatePlayerAction("Open HKMP.Rounds Settings");
            OpenSettings.AddDefaultBinding(Key.F6);
        }
    }

    public sealed class HKMPRoundsMenuSettings
    {
        [JsonConverter(typeof(PlayerActionSetConverter))]
        public HKMPRoundsMenuActions Binds = new HKMPRoundsMenuActions();
    }

    public sealed class HKMPRounds : Mod, IGlobalSettings<HKMPRoundsMenuSettings>, ICustomMenuMod
    {
        internal static GlobalSettingsStore GlobalSettings { get; private set; }
        internal static Dictionary<string, Dictionary<string, GameObject>> PreloadedObjects { get; private set; }
        internal static HKMPRoundsMenuSettings MenuSettings { get; private set; } = new HKMPRoundsMenuSettings();

        private Menu _menu;

        public bool ToggleButtonInsideMenu
        {
            get { return false; }
        }

        internal static PlayerAction MenuOpenAction
        {
            get { return MenuSettings.Binds.OpenSettings; }
        }

        internal static KeyCode MenuKeyCode
        {
            get
            {
                var binding = MenuOpenAction.GetKeyOrMouseBinding();
                string keyName = binding.Key.ToString();
                if (!string.Equals(keyName, "None", StringComparison.OrdinalIgnoreCase))
                {
                    if (keyName.Length == 2 && keyName[0] == 'D' && char.IsDigit(keyName[1]))
                    {
                        keyName = "Alpha" + keyName.Substring(1);
                    }
                    else if (keyName.StartsWith("NumPad", StringComparison.OrdinalIgnoreCase))
                    {
                        keyName = "Keypad" + keyName.Substring(6);
                    }

                    KeyCode key;
                    if (Enum.TryParse(keyName, true, out key))
                    {
                        return key;
                    }
                }

                string mouseName = binding.Mouse.ToString();
                switch (mouseName)
                {
                    case "LeftButton": return KeyCode.Mouse0;
                    case "RightButton": return KeyCode.Mouse1;
                    case "MiddleButton": return KeyCode.Mouse2;
                    case "Button4": return KeyCode.Mouse3;
                    case "Button5": return KeyCode.Mouse4;
                    default: return KeyCode.None;
                }
            }
        }

        internal static string MenuHotkeyDisplayName
        {
            get
            {
                var binding = MenuOpenAction.GetKeyOrMouseBinding();
                string keyName = binding.Key.ToString();
                if (!string.Equals(keyName, "None", StringComparison.OrdinalIgnoreCase))
                {
                    if (keyName.Length == 2 && keyName[0] == 'D' && char.IsDigit(keyName[1]))
                    {
                        return keyName.Substring(1);
                    }

                    return keyName;
                }

                switch (binding.Mouse.ToString())
                {
                    case "LeftButton": return "Mouse 1";
                    case "RightButton": return "Mouse 2";
                    case "MiddleButton": return "Mouse 3";
                    case "Button4": return "Mouse 4";
                    case "Button5": return "Mouse 5";
                    default: return "Mods keybind";
                }
            }
        }

        public void OnLoadGlobal(HKMPRoundsMenuSettings settings)
        {
            MenuSettings = settings ?? new HKMPRoundsMenuSettings();
            if (MenuSettings.Binds == null || MenuSettings.Binds.OpenSettings == null)
            {
                MenuSettings.Binds = new HKMPRoundsMenuActions();
            }
            _menu = null;
        }

        public HKMPRoundsMenuSettings OnSaveGlobal()
        {
            return MenuSettings;
        }

        public MenuScreen GetMenuScreen(MenuScreen modListMenu, ModToggleDelegates? toggleDelegates)
        {
            if (_menu == null)
            {
                const int mainMenuFontSize = 24;
                TextPanel controlsPanel = new TextPanel("Controls", fontSize: mainMenuFontSize);
                KeyBind openSettingsBinding = new KeyBind("Open Settings", MenuSettings.Binds.OpenSettings);
                _menu = new Menu(
                    "HKMP.Rounds",
                    new Element[]
                    {
                        controlsPanel,
                        openSettingsBinding
                    });
                _menu.OnBuilt += (sender, args) =>
                {
                    UnityEngine.UI.Text headerText = controlsPanel.gameObject == null
                        ? null
                        : controlsPanel.gameObject.GetComponent<UnityEngine.UI.Text>();
                    if (openSettingsBinding.gameObject == null)
                    {
                        return;
                    }

                    UnityEngine.UI.Text[] bindingTexts =
                        openSettingsBinding.gameObject.GetComponentsInChildren<UnityEngine.UI.Text>(true);
                    for (int i = 0; i < bindingTexts.Length; i++)
                    {
                        UnityEngine.UI.Text bindingText = bindingTexts[i];
                        if (bindingText == null)
                        {
                            continue;
                        }

                        bindingText.fontSize = mainMenuFontSize;
                        bindingText.resizeTextForBestFit = false;
                        bindingText.resizeTextMinSize = mainMenuFontSize;
                        bindingText.resizeTextMaxSize = mainMenuFontSize;
                        if (headerText != null)
                        {
                            bindingText.font = headerText.font;
                            bindingText.color = headerText.color;
                        }
                        bindingText.SetAllDirty();
                    }
                };
            }

            return _menu.GetMenuScreen(modListMenu);
        }

        public override string GetVersion()
        {
            return RoundsConstants.Version;
        }

        public override List<(string, string)> GetPreloadNames()
        {
            return new List<(string, string)>
            {
                ("GG_Atrium", "GG_Challenge_Door")
            };
        }

        public override void Initialize(
            Dictionary<string, Dictionary<string, GameObject>> preloadedObjects)
        {
            PreloadedObjects = preloadedObjects;

            Modding.Logger.Log(
                "Initializing HKMP.Rounds " +
                RoundsConstants.Version);

            GlobalSettings = new GlobalSettingsStore();

            ServerAddon.RegisterAddon(
                new RoundsServerAddon(GlobalSettings));

            ClientAddon.RegisterAddon(
                new RoundsClientAddon());

            Modding.Logger.Log("HKMP.Rounds initialized.");
        }
    }
}
