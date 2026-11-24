using System.Collections.Generic;
using System.Linq;
using GlobalEnums;
using UnityEngine;

namespace HKMP.Rounds.Settings
{
    internal enum MapPresetTravelKind
    {
        BuiltInBench,
        RoomWithBench
    }

    internal sealed class MapPresetDefinition
    {
        public int Id { get; private set; }
        public string DisplayName { get; private set; }
        public string SceneName { get; private set; }
        public string EntryGate { get; private set; }
        public string BenchMarker { get; private set; }
        public int MapZone { get; private set; }
        public MapPresetTravelKind TravelKind { get; private set; }
        public string ImageFileName { get; private set; }
        public bool KeepEntryPointPositionIfBenchMissing { get; private set; }

        public MapPresetDefinition(
            int id,
            string displayName,
            string sceneName,
            string entryGate,
            string benchMarker,
            MapZone mapZone,
            MapPresetTravelKind travelKind,
            string imageFileName,
            bool keepEntryPointPositionIfBenchMissing = false)
        {
            Id = id;
            DisplayName = displayName ?? string.Empty;
            SceneName = sceneName ?? string.Empty;
            EntryGate = string.IsNullOrWhiteSpace(entryGate) ? "door" : entryGate.Trim();
            BenchMarker = benchMarker ?? string.Empty;
            MapZone = (int)mapZone;
            TravelKind = travelKind;
            ImageFileName = imageFileName ?? string.Empty;
            KeepEntryPointPositionIfBenchMissing = keepEntryPointPositionIfBenchMissing;
        }
    }

    internal static class MapPresetCatalog
    {
        private static readonly MapPresetDefinition[] Definitions =
        {
            new MapPresetDefinition(1, "Mantis Village", "Fungus2_31", "left1", "RestBench", MapZone.WASTES, MapPresetTravelKind.BuiltInBench, "map_mantis_village.png"),
            new MapPresetDefinition(2, "Crystallized Mound", "Mines_35", "left1", string.Empty, MapZone.MINES, MapPresetTravelKind.RoomWithBench, "map_crystallized_mound.png", true),
            new MapPresetDefinition(3, "Queen's Station", "Fungus2_02", "door", "RestBench", MapZone.QUEENS_STATION, MapPresetTravelKind.BuiltInBench, "map_queens_station.png"),
            new MapPresetDefinition(4, "Howling Cliffs", "Room_nailmaster", "door", "RestBench", MapZone.CLIFFS, MapPresetTravelKind.BuiltInBench, "map_howling_cliffs_mato.png")
        };

        public static List<MapPresetDefinition> GetAvailableMaps()
        {
            return Definitions.ToList();
        }

        public static MapPresetDefinition GetById(int id)
        {
            return Definitions.FirstOrDefault(map => map.Id == id);
        }

        public static bool TryGetAvailableMap(int id, out MapPresetDefinition definition)
        {
            definition = GetById(id);
            return definition != null;
        }

        public static Color32 GetFallbackColor(MapPresetDefinition definition)
        {
            if (definition == null)
            {
                return new Color32(31, 36, 48, 255);
            }

            switch (definition.Id)
            {
                case 1: return new Color32(33, 70, 84, 255);
                case 2: return new Color32(31, 23, 44, 255);
                case 3: return new Color32(41, 61, 82, 255);
                case 4: return new Color32(47, 61, 103, 255);
                default: return new Color32(31, 36, 48, 255);
            }
        }
    }
}
