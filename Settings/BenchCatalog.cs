using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using GlobalEnums;
using Newtonsoft.Json;

namespace HKMP.Rounds.Settings
{
    internal static class BenchCatalog
    {
        private const string ResourceName = "HKMP.Rounds.Resources.benches.json";
        private static readonly object Sync = new object();
        private static List<BenchLocation> _cached;

        private sealed class CatalogEntry
        {
            public string Name { get; set; }
            public string AreaName { get; set; }
            public string SceneName { get; set; }
            public string RespawnMarker { get; set; }
            public string MapZone { get; set; }
        }

        public static List<BenchLocation> GetBuiltInCatalog()
        {
            lock (Sync)
            {
                if (_cached == null)
                {
                    _cached = LoadCatalog();
                }

                return _cached.Select(location => location.Copy()).ToList();
            }
        }

        private static List<BenchLocation> LoadCatalog()
        {
            List<BenchLocation> result = new List<BenchLocation>();

            try
            {
                Assembly assembly = typeof(BenchCatalog).Assembly;
                using (Stream stream = assembly.GetManifestResourceStream(ResourceName))
                {
                    if (stream == null)
                    {
                        return result;
                    }

                    using (StreamReader reader = new StreamReader(stream))
                    {
                        List<CatalogEntry> entries = JsonConvert.DeserializeObject<List<CatalogEntry>>(reader.ReadToEnd());
                        if (entries == null)
                        {
                            return result;
                        }

                        foreach (CatalogEntry entry in entries)
                        {
                            if (entry == null ||
                                string.IsNullOrWhiteSpace(entry.SceneName) ||
                                string.IsNullOrWhiteSpace(entry.RespawnMarker))
                            {
                                continue;
                            }

                            MapZone parsedZone;
                            int mapZone = Enum.TryParse(entry.MapZone, true, out parsedZone)
                                ? (int)parsedZone
                                : (int)MapZone.NONE;

                            result.Add(new BenchLocation(
                                entry.SceneName,
                                entry.RespawnMarker,
                                mapZone,
                                entry.Name));
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                Modding.Logger.LogError("[HKMP.Rounds] Failed to load the built-in bench catalog: " + exception);
                result.Clear();
            }

            return result
                .GroupBy(location => location.Key, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(location => location.SceneName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(location => location.MarkerName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
