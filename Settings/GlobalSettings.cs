using System;
using System.IO;

using Newtonsoft.Json;

using UnityEngine;

namespace HKMP.Rounds.Settings
{
    internal sealed class GlobalSettingsStore
    {
        private sealed class SettingsData
        {
            public bool AutoStart;
        }

        private readonly string _filePath;

        public bool AutoStart
        {
            get;
            private set;
        }

        public GlobalSettingsStore()
        {
            _filePath = Path.Combine(
                Application.persistentDataPath,
                "HKMP.Rounds.GlobalSettings.json"
            );

            Load();
        }

        public void SetAutoStart(
            bool enabled)
        {
            AutoStart = enabled;

            Save();
        }

        private void Load()
        {
            AutoStart = false;

            try
            {
                if (!File.Exists(
                    _filePath))
                {
                    return;
                }

                string json =
                    File.ReadAllText(
                        _filePath
                    );

                if (string.IsNullOrWhiteSpace(
                    json))
                {
                    return;
                }

                SettingsData data =
                    JsonConvert.DeserializeObject<SettingsData>(
                        json
                    );

                if (data == null)
                {
                    return;
                }

                AutoStart =
                    data.AutoStart;
            }
            catch (Exception exception)
            {
                Modding.Logger.LogError(
                    "[HKMP.Rounds] " +
                    "Failed to load global settings. " +
                    "Exception=" +
                    exception
                );

                AutoStart = false;
            }
        }

        private void Save()
        {
            try
            {
                SettingsData data =
                    new SettingsData
                    {
                        AutoStart = AutoStart
                    };

                string json =
                    JsonConvert.SerializeObject(
                        data,
                        Formatting.Indented
                    );

                File.WriteAllText(
                    _filePath,
                    json
                );
            }
            catch (Exception exception)
            {
                Modding.Logger.LogError(
                    "[HKMP.Rounds] " +
                    "Failed to save global settings. " +
                    "Exception=" +
                    exception
                );
            }
        }
    }
}