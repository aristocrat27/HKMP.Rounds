using System;
using System.IO;
using Newtonsoft.Json;
using HKMP.Rounds.Settings;
using UnityEngine;

namespace HKMP.Rounds.Client
{
    internal static class ClientPreferencesStore
    {
        private sealed class PreferencesData
        {
            public int SchemaVersion = 12;
            public string MenuKey = "F6";
            public string ReadyKey = "Alpha1";
            public string StatsKey = "Alpha2";
            public string ReadyHudKey = "Alpha3";
            public string FreezeKey = "F8";
            public bool ShowPersonalReadyStatus = true;
            public bool ShowReadyCount = true;
            public string UiLanguage = string.Empty;
            public float ReadyHudOffsetX;
            public float ReadyHudOffsetY;
            public float ReadyHudScale = 1f;
            public float PersonalReadyOffsetX;
            public float PersonalReadyOffsetY;
            public float PersonalReadyScale = 1f;
            public string LastRealBenchScene = string.Empty;
            public string LastRealBenchMarker = string.Empty;
            public int LastRealBenchMapZone;
            public int LastRealBenchPhantomMapId;
            public string PersonalBenchScene = string.Empty;
            public string PersonalBenchMarker = string.Empty;
            public int PersonalBenchMapZone;
            public int PersonalBenchPhantomMapId;
            public bool PreferSharedBench;
            public bool HidePhantomMantisBackground = true;
        }

        private static readonly string FilePath = Path.Combine(
            Application.persistentDataPath,
            "HKMP.Rounds.ClientPreferences.json");

        private static PreferencesData _data;

        static ClientPreferencesStore()
        {
            Load();
        }

        private static KeyCode _lastSynchronizedMenuKey = KeyCode.None;

        public static KeyCode MenuKey
        {
            get { return HKMP.Rounds.HKMPRounds.MenuKeyCode; }
        }

        public static void SynchronizeMenuKeyConflicts()
        {
            KeyCode menuKey = MenuKey;
            if (menuKey == _lastSynchronizedMenuKey)
            {
                return;
            }

            _lastSynchronizedMenuKey = menuKey;
            if (!IsBindableKey(menuKey))
            {
                return;
            }

            KeyCode readyKey = ReadyKey;
            KeyCode statsKey = StatsKey;
            KeyCode readyHudKey = ReadyHudKey;
            KeyCode freezeKey = FreezeKey;
            bool changed = false;

            if (readyKey == menuKey || readyKey == freezeKey || readyKey == statsKey || readyKey == readyHudKey)
            {
                readyKey = FirstUnusedKey(KeyCode.Alpha1, menuKey, statsKey, readyHudKey, freezeKey);
                changed = true;
            }

            if (statsKey == menuKey || statsKey == readyKey || statsKey == readyHudKey || statsKey == freezeKey)
            {
                statsKey = FirstUnusedKey(KeyCode.Alpha2, menuKey, readyKey, readyHudKey, freezeKey);
                changed = true;
            }

            if (readyHudKey == menuKey || readyHudKey == readyKey || readyHudKey == statsKey || readyHudKey == freezeKey)
            {
                readyHudKey = FirstUnusedKey(KeyCode.Alpha3, menuKey, readyKey, statsKey, freezeKey);
                changed = true;
            }

            if (freezeKey == menuKey || freezeKey == readyKey || freezeKey == statsKey || freezeKey == readyHudKey)
            {
                freezeKey = FirstUnusedKey(KeyCode.F8, menuKey, readyKey, statsKey, readyHudKey);
                changed = true;
            }

            if (changed)
            {
                _data.MenuKey = menuKey.ToString();
                _data.ReadyKey = readyKey.ToString();
                _data.StatsKey = statsKey.ToString();
                _data.ReadyHudKey = readyHudKey.ToString();
                _data.FreezeKey = freezeKey.ToString();
                Save();
            }
        }

        public static KeyCode ReadyKey
        {
            get { return ParseKey(_data.ReadyKey, KeyCode.Alpha1); }
        }

        public static KeyCode StatsKey
        {
            get { return ParseKey(_data.StatsKey, KeyCode.Alpha2); }
        }

        public static KeyCode ReadyHudKey
        {
            get { return ParseKey(_data.ReadyHudKey, KeyCode.Alpha3); }
        }

        public static KeyCode FreezeKey
        {
            get { return ParseKey(_data.FreezeKey, KeyCode.F8); }
        }

        public static string FormatKeyCode(KeyCode key)
        {
            if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9)
            {
                return ((int)key - (int)KeyCode.Alpha0).ToString();
            }

            if (key >= KeyCode.Keypad0 && key <= KeyCode.Keypad9)
            {
                return "Num " + ((int)key - (int)KeyCode.Keypad0).ToString();
            }

            switch (key)
            {
                case KeyCode.Return: return "Enter";
                case KeyCode.KeypadEnter: return "Num Enter";
                case KeyCode.LeftControl: return "L Ctrl";
                case KeyCode.RightControl: return "R Ctrl";
                case KeyCode.LeftAlt: return "L Alt";
                case KeyCode.RightAlt: return "R Alt";
                case KeyCode.LeftShift: return "L Shift";
                case KeyCode.RightShift: return "R Shift";
                case KeyCode.LeftCommand: return "L Cmd";
                case KeyCode.RightCommand: return "R Cmd";
                case KeyCode.Backspace: return "Backspace";
                case KeyCode.Delete: return "Delete";
                case KeyCode.Escape: return "Esc";
                case KeyCode.Space: return "Space";
                case KeyCode.PageUp: return "Page Up";
                case KeyCode.PageDown: return "Page Down";
                case KeyCode.Insert: return "Insert";
                case KeyCode.Home: return "Home";
                case KeyCode.End: return "End";
                case KeyCode.UpArrow: return "Up";
                case KeyCode.DownArrow: return "Down";
                case KeyCode.LeftArrow: return "Left";
                case KeyCode.RightArrow: return "Right";
                case KeyCode.KeypadPlus: return "Num +";
                case KeyCode.KeypadMinus: return "Num -";
                case KeyCode.KeypadMultiply: return "Num *";
                case KeyCode.KeypadDivide: return "Num /";
                case KeyCode.KeypadPeriod: return "Num .";
                case KeyCode.None: return "Unassigned";
                default: return key.ToString().Replace("Alpha", string.Empty).Replace("Keypad", "Num ");
            }
        }

        public static bool ShowPersonalReadyStatus
        {
            get { return _data.ShowPersonalReadyStatus; }
        }

        public static bool ShowReadyCount
        {
            get { return _data.ShowReadyCount; }
        }

        public static bool HidePhantomMantisBackground
        {
            get { return _data.HidePhantomMantisBackground; }
        }



        public static string GetUiLanguage(string fallback)
        {
            string saved = _data.UiLanguage;
            if (string.Equals(saved, "ru", StringComparison.OrdinalIgnoreCase))
            {
                return "ru";
            }
            if (string.Equals(saved, "en", StringComparison.OrdinalIgnoreCase))
            {
                return "en";
            }
            return string.Equals(fallback, "ru", StringComparison.OrdinalIgnoreCase) ? "ru" : "en";
        }

        public static void SetUiLanguage(string language)
        {
            string normalized = string.Equals(language, "ru", StringComparison.OrdinalIgnoreCase) ? "ru" : "en";
            if (string.Equals(_data.UiLanguage, normalized, StringComparison.Ordinal))
            {
                return;
            }
            _data.UiLanguage = normalized;
            Save();
        }



        public static Vector2 ReadyHudOffset
        {
            get { return new Vector2(_data.ReadyHudOffsetX, _data.ReadyHudOffsetY); }
        }

        public static float ReadyHudScale
        {
            get { return Mathf.Clamp(_data.ReadyHudScale, 0.5f, 3f); }
        }

        public static Vector2 PersonalReadyOffset
        {
            get { return new Vector2(_data.PersonalReadyOffsetX, _data.PersonalReadyOffsetY); }
        }

        public static float PersonalReadyScale
        {
            get { return Mathf.Clamp(_data.PersonalReadyScale, 0.5f, 3f); }
        }

        public static bool TrySetReadyKey(KeyCode key)
        {
            if (!IsBindableKey(key) || key == MenuKey || key == StatsKey || key == ReadyHudKey || key == FreezeKey)
            {
                return false;
            }

            _data.ReadyKey = key.ToString();
            Save();
            return true;
        }

        public static bool TrySetStatsKey(KeyCode key)
        {
            if (!IsBindableKey(key) || key == MenuKey || key == ReadyKey || key == ReadyHudKey || key == FreezeKey)
            {
                return false;
            }

            _data.StatsKey = key.ToString();
            Save();
            return true;
        }

        public static bool TrySetReadyHudKey(KeyCode key)
        {
            if (!IsBindableKey(key) || key == MenuKey || key == ReadyKey || key == StatsKey || key == FreezeKey)
            {
                return false;
            }

            _data.ReadyHudKey = key.ToString();
            Save();
            return true;
        }

        public static bool TrySetFreezeKey(KeyCode key)
        {
            if (!IsBindableKey(key) || key == MenuKey || key == ReadyKey || key == StatsKey || key == ReadyHudKey)
            {
                return false;
            }

            _data.FreezeKey = key.ToString();
            Save();
            return true;
        }

        public static void SetShowPersonalReadyStatus(bool value)
        {
            _data.ShowPersonalReadyStatus = value;
            Save();
        }

        public static void SetShowReadyCount(bool value)
        {
            _data.ShowReadyCount = value;
            Save();
        }

        public static void SetHidePhantomMantisBackground(bool value)
        {
            if (_data.HidePhantomMantisBackground == value)
            {
                return;
            }

            _data.HidePhantomMantisBackground = value;
            Save();
        }

        public static void SetReadyHudLayout(Vector2 offset, float scale)
        {
            float x = IsFinite(offset.x) ? Mathf.Clamp(offset.x, -100f, 100f) : 0f;
            float y = IsFinite(offset.y) ? Mathf.Clamp(offset.y, -100f, 100f) : 0f;
            float normalizedScale = IsFinite(scale) ? Mathf.Clamp(scale, 0.5f, 3f) : 1f;

            _data.ReadyHudOffsetX = x;
            _data.ReadyHudOffsetY = y;
            _data.ReadyHudScale = normalizedScale;
            Save();
        }

        public static void SetPersonalReadyLayout(Vector2 offset, float scale)
        {
            float x = IsFinite(offset.x) ? Mathf.Clamp(offset.x, -100f, 100f) : 0f;
            float y = IsFinite(offset.y) ? Mathf.Clamp(offset.y, -100f, 100f) : 0f;
            float normalizedScale = IsFinite(scale) ? Mathf.Clamp(scale, 0.5f, 3f) : 1f;

            _data.PersonalReadyOffsetX = x;
            _data.PersonalReadyOffsetY = y;
            _data.PersonalReadyScale = normalizedScale;
            Save();
        }

        public static bool TryGetLastRealBench(
            out string sceneName,
            out string markerName,
            out int mapZone)
        {
            int phantomMapId;
            return TryGetLastRealBench(out sceneName, out markerName, out mapZone, out phantomMapId);
        }

        public static bool TryGetLastRealBench(
            out string sceneName,
            out string markerName,
            out int mapZone,
            out int phantomMapId)
        {
            sceneName = _data.LastRealBenchScene ?? string.Empty;
            markerName = _data.LastRealBenchMarker ?? string.Empty;
            mapZone = _data.LastRealBenchMapZone;
            phantomMapId = Math.Max(0, _data.LastRealBenchPhantomMapId);
            return !string.IsNullOrWhiteSpace(sceneName) &&
                   !string.IsNullOrWhiteSpace(markerName);
        }

        public static void RememberLastRealBench(
            string sceneName,
            string markerName,
            int mapZone,
            int phantomMapId = 0)
        {
            if (string.IsNullOrWhiteSpace(sceneName) ||
                string.IsNullOrWhiteSpace(markerName))
            {
                return;
            }

            string normalizedScene = sceneName.Trim();
            string normalizedMarker = markerName.Trim();
            int normalizedPhantomMapId = Math.Max(0, phantomMapId);
            if (string.Equals(_data.LastRealBenchScene, normalizedScene, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(_data.LastRealBenchMarker, normalizedMarker, StringComparison.OrdinalIgnoreCase) &&
                _data.LastRealBenchMapZone == mapZone &&
                _data.LastRealBenchPhantomMapId == normalizedPhantomMapId)
            {
                return;
            }

            _data.LastRealBenchScene = normalizedScene;
            _data.LastRealBenchMarker = normalizedMarker;
            _data.LastRealBenchMapZone = mapZone;
            _data.LastRealBenchPhantomMapId = normalizedPhantomMapId;
            Save();
        }

        public static bool PreferSharedBench
        {
            get { return _data.PreferSharedBench; }
        }



        public static void SetPreferSharedBench(bool value)
        {
            if (_data.PreferSharedBench == value)
            {
                return;
            }

            _data.PreferSharedBench = value;
            Save();
        }

        public static bool TryGetPersonalBench(out BenchLocation location)
        {
            location = null;
            if (string.IsNullOrWhiteSpace(_data.PersonalBenchScene) ||
                string.IsNullOrWhiteSpace(_data.PersonalBenchMarker))
            {
                return false;
            }

            location = new BenchLocation(
                _data.PersonalBenchScene,
                _data.PersonalBenchMarker,
                _data.PersonalBenchMapZone,
                "Personal selection",
                _data.PersonalBenchPhantomMapId);
            return location.IsValid;
        }

        public static void SetPersonalBench(BenchLocation location)
        {
            if (location == null || !location.IsValid)
            {
                return;
            }

            SetPersonalBenchSelection(location);
        }



        private static void SetPersonalBenchSelection(BenchLocation location)
        {
            if (location == null || !location.IsValid)
            {
                return;
            }

            string sceneName = BenchLocation.Normalize(location.SceneName);
            string markerName = BenchLocation.Normalize(location.MarkerName);
            bool unchanged = string.Equals(_data.PersonalBenchScene, sceneName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(_data.PersonalBenchMarker, markerName, StringComparison.OrdinalIgnoreCase) &&
                _data.PersonalBenchMapZone == location.MapZone &&
                _data.PersonalBenchPhantomMapId == location.PhantomMapId;
            if (unchanged)
            {
                if (_data.PreferSharedBench)
                {
                    _data.PreferSharedBench = false;
                    Save();
                }
                return;
            }

            _data.PreferSharedBench = false;
            _data.PersonalBenchScene = sceneName;
            _data.PersonalBenchMarker = markerName;
            _data.PersonalBenchMapZone = location.MapZone;
            _data.PersonalBenchPhantomMapId = location.PhantomMapId;
            Save();
        }







        public static void ClearPersonalBench()
        {
            if (string.IsNullOrEmpty(_data.PersonalBenchScene) &&
                string.IsNullOrEmpty(_data.PersonalBenchMarker) &&
                _data.PersonalBenchMapZone == 0 &&
                _data.PersonalBenchPhantomMapId == 0)
            {
                return;
            }

            _data.PersonalBenchScene = string.Empty;
            _data.PersonalBenchMarker = string.Empty;
            _data.PersonalBenchMapZone = 0;
            _data.PersonalBenchPhantomMapId = 0;
            Save();
        }



        public static bool IsBindableKey(KeyCode key)
        {
            int value = (int)key;
            return key != KeyCode.None &&
                   key != KeyCode.Escape &&
                   key != KeyCode.Mouse0 &&
                   value > 0 &&
                   value < (int)KeyCode.Mouse0 &&
                   Enum.IsDefined(typeof(KeyCode), key);
        }

        private static KeyCode ParseKey(string value, KeyCode fallback)
        {
            KeyCode parsed;
            if (Enum.TryParse(value, true, out parsed) && IsBindableKey(parsed))
            {
                return parsed;
            }

            return fallback;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static void Load()
        {
            _data = new PreferencesData();

            try
            {
                if (!File.Exists(FilePath))
                {
                    Save();
                    return;
                }

                PreferencesData loaded = JsonConvert.DeserializeObject<PreferencesData>(
                    File.ReadAllText(FilePath));

                if (loaded != null)
                {
                    _data = loaded;
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[HKMP.Rounds] Could not load local client preferences: " + exception.Message);
            }

            if (string.Equals(_data.ReadyKey, "R", StringComparison.OrdinalIgnoreCase))
            {
                _data.ReadyKey = KeyCode.Alpha1.ToString();
            }

            if (string.Equals(_data.ReadyHudKey, "F9", StringComparison.OrdinalIgnoreCase))
            {
                _data.ReadyHudKey = KeyCode.Alpha3.ToString();
            }

            KeyCode menuKey = ParseKey(_data.MenuKey, KeyCode.F6);
            KeyCode readyKey = ParseKey(_data.ReadyKey, KeyCode.Alpha1);
            KeyCode statsKey = ParseKey(_data.StatsKey, KeyCode.Alpha2);
            KeyCode readyHudKey = ParseKey(_data.ReadyHudKey, KeyCode.Alpha3);
            KeyCode freezeKey = ParseKey(_data.FreezeKey, KeyCode.F8);

            if (readyKey == menuKey || readyKey == freezeKey)
            {
                readyKey = FirstUnusedKey(KeyCode.R, menuKey, freezeKey);
            }

            if (statsKey == menuKey || statsKey == readyKey || statsKey == freezeKey)
            {
                statsKey = FirstUnusedKey(KeyCode.Alpha2, menuKey, readyKey, freezeKey);
            }

            if (readyHudKey == menuKey || readyHudKey == readyKey || readyHudKey == statsKey || readyHudKey == freezeKey)
            {
                readyHudKey = FirstUnusedKey(KeyCode.Alpha3, menuKey, readyKey, statsKey, freezeKey);
            }

            if (freezeKey == menuKey || freezeKey == readyKey || freezeKey == statsKey || freezeKey == readyHudKey)
            {
                freezeKey = FirstUnusedKey(KeyCode.F8, menuKey, readyKey, statsKey, readyHudKey);
            }

            _data.MenuKey = menuKey.ToString();
            _data.ReadyKey = readyKey.ToString();
            _data.StatsKey = statsKey.ToString();
            _data.ReadyHudKey = readyHudKey.ToString();
            _data.FreezeKey = freezeKey.ToString();
            _data.SchemaVersion = 13;
            _data.UiLanguage = _data.UiLanguage ?? string.Empty;
            _data.ReadyHudOffsetX = IsFinite(_data.ReadyHudOffsetX)
                ? Mathf.Clamp(_data.ReadyHudOffsetX, -100f, 100f)
                : 0f;
            _data.ReadyHudOffsetY = IsFinite(_data.ReadyHudOffsetY)
                ? Mathf.Clamp(_data.ReadyHudOffsetY, -100f, 100f)
                : 0f;
            _data.ReadyHudScale = IsFinite(_data.ReadyHudScale)
                ? Mathf.Clamp(_data.ReadyHudScale, 0.5f, 3f)
                : 1f;
            _data.PersonalReadyOffsetX = IsFinite(_data.PersonalReadyOffsetX)
                ? Mathf.Clamp(_data.PersonalReadyOffsetX, -100f, 100f)
                : 0f;
            _data.PersonalReadyOffsetY = IsFinite(_data.PersonalReadyOffsetY)
                ? Mathf.Clamp(_data.PersonalReadyOffsetY, -100f, 100f)
                : 0f;
            _data.PersonalReadyScale = IsFinite(_data.PersonalReadyScale)
                ? Mathf.Clamp(_data.PersonalReadyScale, 0.5f, 3f)
                : 1f;
            _data.LastRealBenchScene = _data.LastRealBenchScene ?? string.Empty;
            _data.LastRealBenchMarker = _data.LastRealBenchMarker ?? string.Empty;
            _data.LastRealBenchPhantomMapId = Math.Max(0, _data.LastRealBenchPhantomMapId);
            _data.PersonalBenchScene = _data.PersonalBenchScene ?? string.Empty;
            _data.PersonalBenchMarker = _data.PersonalBenchMarker ?? string.Empty;
            _data.PersonalBenchPhantomMapId = Math.Max(0, _data.PersonalBenchPhantomMapId);
            if (string.Equals(_data.LastRealBenchMarker, "DeployedBench", StringComparison.OrdinalIgnoreCase))
            {
                _data.LastRealBenchScene = string.Empty;
                _data.LastRealBenchMarker = string.Empty;
                _data.LastRealBenchMapZone = 0;
                _data.LastRealBenchPhantomMapId = 0;
            }
            if (string.Equals(_data.PersonalBenchMarker, "DeployedBench", StringComparison.OrdinalIgnoreCase))
            {
                _data.PersonalBenchScene = string.Empty;
                _data.PersonalBenchMarker = string.Empty;
                _data.PersonalBenchMapZone = 0;
                _data.PersonalBenchPhantomMapId = 0;
            }
            if (string.IsNullOrWhiteSpace(_data.PersonalBenchScene) ||
                string.IsNullOrWhiteSpace(_data.PersonalBenchMarker))
            {
                _data.PersonalBenchScene = string.Empty;
                _data.PersonalBenchMarker = string.Empty;
                _data.PersonalBenchMapZone = 0;
                _data.PersonalBenchPhantomMapId = 0;
            }
            _data.SchemaVersion = 13;
            Save();

        }

        private static KeyCode FirstUnusedKey(KeyCode preferred, params KeyCode[] reserved)
        {
            if (IsBindableKey(preferred) && Array.IndexOf(reserved, preferred) < 0)
            {
                return preferred;
            }

            Array keys = Enum.GetValues(typeof(KeyCode));
            foreach (object value in keys)
            {
                KeyCode key = (KeyCode)value;
                if (IsBindableKey(key) && Array.IndexOf(reserved, key) < 0)
                {
                    return key;
                }
            }

            return KeyCode.F9;
        }

        private static void Save()
        {
            try
            {
                string directory = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(
                    FilePath,
                    JsonConvert.SerializeObject(_data, Formatting.Indented));
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[HKMP.Rounds] Could not save local client preferences: " + exception.Message);
            }
        }
    }
}
