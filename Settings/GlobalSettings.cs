using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;

namespace HKMP.Rounds.Settings
{
    internal sealed class GlobalSettingsStore
    {
        private sealed class SettingsData
        {
            public bool AutoStart;
            public int RoundSoul = 198;
            public bool DisableEnemies;
            public bool DebugDisableShade;
            public bool TimerIntegrationEnabled;
            public bool IndividualTeamWins;
            public bool IndependentRoundsPerRoom;
            public bool DisablePvpBeforeRound = true;
            public bool GiveLifebloodMasks;
            public bool VignetteEnabled;
            public bool HidePhantomMantisBackground = true;
            public int ReadyRequiredCount;
            public string SharedBenchScene = string.Empty;
            public string SharedBenchMarker = string.Empty;
            public int SharedBenchMapZone;
            public int SharedBenchPhantomMapId;
            public bool SharedBenchLocked;
            public string KnownBenchesJson = "[]";
            public bool MapTravelEnabled;
            public string PhantomMantisMapsJson = "[]";
            public string MapDestinationsJson = "[]";
            public string UiLanguage = "en";
            public PvpMode PvpMode = HKMP.Rounds.Settings.PvpMode.Quick;
        }

        private const int DefaultRoundSoul = 198;
        private const int MaximumRoundSoul = 198;
        private const int DefaultReadyRequiredCount = 0;
        private const int MaximumReadyRequiredCount = 1000;
        private const int MaximumKnownBenches = 256;
        private const int MaximumKnownBenchesJsonLength = 45000;
        private const int MaximumMapDestinations = 1200;
        private const int MaximumMapDestinationsJsonLength = 48000;
        private const int MaximumMapDestinationsStoreJsonLength = 2000000;

        private readonly string _filePath;
        private readonly List<BenchLocation> _knownBenches = new List<BenchLocation>();
        private readonly List<PhantomMantisMap> _phantomMantisMaps = new List<PhantomMantisMap>();
        private readonly List<SceneTravelDestination> _mapDestinations = new List<SceneTravelDestination>();

        public event Action AutoStartDisabled;
        public event Action ReadyRequiredCountChanged;

        public bool AutoStart { get; private set; }
        public int RoundSoul { get; private set; }
        public bool DisableEnemies { get; private set; }
        public bool DebugDisableShade { get; private set; }
        public bool TimerIntegrationEnabled { get; private set; }
        public bool IndividualTeamWins { get; private set; }
        public bool IndependentRoundsPerRoom { get; private set; }
        public bool DisablePvpBeforeRound { get; private set; }
        public bool GiveLifebloodMasks { get; private set; }
        public bool VignetteEnabled { get; private set; }
        public bool HidePhantomMantisBackground { get; private set; }
        public int ReadyRequiredCount { get; private set; }
        public string SharedBenchScene { get; private set; }
        public string SharedBenchMarker { get; private set; }
        public int SharedBenchMapZone { get; private set; }
        public int SharedBenchPhantomMapId { get; private set; }
        public bool SharedBenchLocked { get; private set; }
        public string UiLanguage { get; private set; }
        public PvpMode PvpMode { get; private set; }
        public bool MapTravelEnabled { get; private set; }

        public bool HasSharedBench
        {
            get
            {
                return !string.IsNullOrWhiteSpace(SharedBenchScene) &&
                       !string.IsNullOrWhiteSpace(SharedBenchMarker);
            }
        }

        public GlobalSettingsStore()
        {
            _filePath = Path.Combine(
                Application.persistentDataPath,
                "HKMP.Rounds.GlobalSettings.json");
            Load();
        }

        public void SetAutoStart(bool enabled)
        {
            AutoStart = enabled;
            Save();
            if (!enabled && AutoStartDisabled != null)
            {
                AutoStartDisabled();
            }
        }

        public bool SetRoundSoul(int amount)
        {
            if (amount < 0 || amount > MaximumRoundSoul)
            {
                return false;
            }

            RoundSoul = amount;
            Save();
            return true;
        }

        public bool SetReadyRequiredCount(int count)
        {
            if (count < 0 || count > MaximumReadyRequiredCount)
            {
                return false;
            }

            bool changed = ReadyRequiredCount != count;
            ReadyRequiredCount = count;
            Save();
            if (changed && ReadyRequiredCountChanged != null) ReadyRequiredCountChanged();
            return true;
        }

        public void SetDisableEnemies(bool enabled)
        {
            DisableEnemies = enabled;
            Save();
        }

        public void SetDebugDisableShade(bool enabled)
        {
            DebugDisableShade = enabled;
            Save();
        }

        public void SetTimerIntegration(bool enabled)
        {
            TimerIntegrationEnabled = enabled;
            Save();
        }

        public void SetIndividualTeamWins(bool enabled)
        {
            IndividualTeamWins = enabled;
            Save();
        }

        public void SetIndependentRoundsPerRoom(bool enabled)
        {
            IndependentRoundsPerRoom = enabled;
            Save();
        }

        public void SetDisablePvpBeforeRound(bool enabled)
        {
            DisablePvpBeforeRound = enabled;
            Save();
        }

        public void SetGiveLifebloodMasks(bool enabled)
        {
            GiveLifebloodMasks = enabled;
            Save();
        }

        public void SetVignetteEnabled(bool enabled)
        {
            VignetteEnabled = enabled;
            Save();
        }

        public void SetHidePhantomMantisBackground(bool enabled)
        {
            HidePhantomMantisBackground = enabled;
            Save();
        }

        public void ApplyServerState(
            bool autoStart,
            int roundSoul,
            bool disableEnemies,
            bool debugDisableShade,
            bool timerIntegrationEnabled,
            int readyRequiredCount,
            bool individualTeamWins,
            bool independentRoundsPerRoom,
            bool disablePvpBeforeRound,
            bool giveLifebloodMasks,
            bool vignetteEnabled,
            bool hidePhantomMantisBackground)
        {
            bool wasAutoStart = AutoStart;
            int previousReadyRequiredCount = ReadyRequiredCount;
            AutoStart = autoStart;
            RoundSoul = Math.Max(0, Math.Min(MaximumRoundSoul, roundSoul));
            DisableEnemies = disableEnemies;
            DebugDisableShade = debugDisableShade;
            TimerIntegrationEnabled = timerIntegrationEnabled;
            IndividualTeamWins = individualTeamWins;
            IndependentRoundsPerRoom = independentRoundsPerRoom;
            DisablePvpBeforeRound = disablePvpBeforeRound;
            GiveLifebloodMasks = giveLifebloodMasks;
            VignetteEnabled = vignetteEnabled;
            HidePhantomMantisBackground = hidePhantomMantisBackground;
            ReadyRequiredCount = Math.Max(0, Math.Min(MaximumReadyRequiredCount, readyRequiredCount));
            Save();
            if (wasAutoStart && !AutoStart && AutoStartDisabled != null) AutoStartDisabled();
            if (previousReadyRequiredCount != ReadyRequiredCount && ReadyRequiredCountChanged != null) ReadyRequiredCountChanged();
        }

        public bool SetSharedBench(BenchLocation location)
        {
            if (location == null || !location.IsValid)
            {
                return false;
            }

            SharedBenchScene = BenchLocation.Normalize(location.SceneName);
            SharedBenchMarker = BenchLocation.Normalize(location.MarkerName);
            SharedBenchMapZone = location.MapZone;
            SharedBenchPhantomMapId = location.PhantomMapId;
            MergeKnownBenchesInternal(new[] { location }, false);
            Save();
            return true;
        }

        public bool SetSharedBenchLocked(bool locked)
        {
            if (locked && !HasSharedBench)
            {
                return false;
            }

            if (SharedBenchLocked == locked)
            {
                return true;
            }

            SharedBenchLocked = locked;
            Save();
            return true;
        }


        public void SetUiLanguage(string language)
        {
            UiLanguage = string.Equals(language, "ru", StringComparison.OrdinalIgnoreCase) ? "ru" : "en";
            Save();
        }







        public void SetMapTravelEnabled(bool enabled)
        {
            if (MapTravelEnabled == enabled)
            {
                return;
            }

            MapTravelEnabled = enabled;
            Save();
        }

        public PhantomMantisMap CreatePhantomMantis()
        {
            int id = 1;
            if (_phantomMantisMaps.Count > 0)
            {
                int maximum = _phantomMantisMaps.Max(map => map.Id);
                if (maximum < int.MaxValue)
                {
                    id = maximum + 1;
                }
                else
                {
                    var used = new HashSet<int>(_phantomMantisMaps.Select(map => map.Id));
                    while (used.Contains(id) && id < int.MaxValue)
                    {
                        id++;
                    }
                    if (used.Contains(id))
                    {
                        return null;
                    }
                }
            }

            PhantomMantisMap map = new PhantomMantisMap
            {
                Id = id,
                Name = "Phantom Mantis Village #" + id,
                CreatedUtcTicks = DateTime.UtcNow.Ticks
            };

            _phantomMantisMaps.Add(map);
            _phantomMantisMaps.Sort((first, second) => first.Id.CompareTo(second.Id));
            MergeKnownBenchesInternal(
                new[] { new BenchLocation("Fungus2_31", "RestBench", 13, "Phantom Mantis Village #" + id, id) },
                false);
            Save();
            return map.Copy();
        }

        public bool ContainsPhantomMantis(int id)
        {
            return id > 0 && _phantomMantisMaps.Any(map => map.Id == id);
        }

        public bool RemovePhantomMantis(int id)
        {
            if (id <= 0)
            {
                return false;
            }

            int removed = _phantomMantisMaps.RemoveAll(map => map != null && map.Id == id);
            if (removed == 0)
            {
                return false;
            }

            _knownBenches.RemoveAll(bench => bench != null && bench.PhantomMapId == id);
            if (SharedBenchPhantomMapId == id)
            {
                SharedBenchScene = string.Empty;
                SharedBenchMarker = string.Empty;
                SharedBenchMapZone = 0;
                SharedBenchPhantomMapId = 0;
                SharedBenchLocked = false;
            }

            _phantomMantisMaps.Sort((first, second) => first.Id.CompareTo(second.Id));
            Save();
            return true;
        }



        public int GetPhantomMantisMapPageCount(int pageSize)
        {
            pageSize = Math.Max(1, pageSize);
            return _phantomMantisMaps.Count == 0
                ? 1
                : (_phantomMantisMaps.Count + pageSize - 1) / pageSize;
        }

        public string GetPhantomMantisMapPageJson(int page, int pageSize)
        {
            pageSize = Math.Max(1, Math.Min(40, pageSize));
            int pageCount = GetPhantomMantisMapPageCount(pageSize);
            page = Math.Max(0, Math.Min(pageCount - 1, page));
            try
            {
                return JsonConvert.SerializeObject(
                    _phantomMantisMaps.Skip(page * pageSize).Take(pageSize).ToList(),
                    Formatting.None);
            }
            catch
            {
                return "[]";
            }
        }



        public string GetMapDestinationsJson()
        {
            try
            {
                StringBuilder json = new StringBuilder("[");
                bool hasDestination = false;

                foreach (SceneTravelDestination destination in _mapDestinations)
                {
                    string destinationJson = JsonConvert.SerializeObject(destination, Formatting.None);
                    int separatorLength = hasDestination ? 1 : 0;
                    if (json.Length + separatorLength + destinationJson.Length + 1 > MaximumMapDestinationsJsonLength)
                    {
                        break;
                    }

                    if (hasDestination)
                    {
                        json.Append(',');
                    }

                    json.Append(destinationJson);
                    hasDestination = true;
                }

                return json.Append(']').ToString();
            }
            catch
            {
                return "[]";
            }
        }

        public bool MergeMapDestinationsJson(string json, string allowedSourceScene = null)
        {
            if (string.IsNullOrWhiteSpace(json) || json.Length > MaximumMapDestinationsJsonLength)
            {
                return false;
            }

            try
            {
                List<SceneTravelDestination> destinations =
                    JsonConvert.DeserializeObject<List<SceneTravelDestination>>(json);
                return MergeMapDestinations(destinations, allowedSourceScene);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[HKMP.Rounds] Ignored invalid map destination catalog: " + exception.Message);
                return false;
            }
        }

        public bool MergeMapDestinations(
            IEnumerable<SceneTravelDestination> destinations,
            string allowedSourceScene = null)
        {
            if (destinations == null)
            {
                return false;
            }

            bool changed = false;
            foreach (SceneTravelDestination source in destinations)
            {
                if (source == null || !source.IsValid)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(allowedSourceScene) &&
                    !string.Equals(
                        source.SourceScene,
                        allowedSourceScene,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                SceneTravelDestination normalized = source.Copy();
                normalized.SourceScene = normalized.SourceScene.Trim();
                normalized.SceneName = normalized.SceneName.Trim();
                normalized.EntryGate = normalized.EntryGate.Trim();
                normalized.Area = string.IsNullOrWhiteSpace(normalized.Area)
                    ? string.Empty
                    : normalized.Area.Trim();

                int existingIndex = _mapDestinations.FindIndex(item =>
                    string.Equals(item.SceneName, normalized.SceneName, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(item.EntryGate, normalized.EntryGate, StringComparison.OrdinalIgnoreCase));

                if (existingIndex >= 0)
                {
                    SceneTravelDestination existing = _mapDestinations[existingIndex];
                    if (string.IsNullOrWhiteSpace(existing.Area) && !string.IsNullOrWhiteSpace(normalized.Area))
                    {
                        existing.Area = normalized.Area;
                        changed = true;
                    }
                    if (normalized.IsCustomKnown &&
                        (!existing.IsCustomKnown || existing.IsCustom != normalized.IsCustom))
                    {
                        existing.IsCustomKnown = true;
                        existing.IsCustom = normalized.IsCustom;
                        changed = true;
                    }
                    continue;
                }

                if (_mapDestinations.Count >= MaximumMapDestinations)
                {
                    break;
                }

                _mapDestinations.Add(normalized);
                changed = true;
            }

            if (changed)
            {
                _mapDestinations.Sort((first, second) =>
                {
                    int scene = StringComparer.OrdinalIgnoreCase.Compare(first.SceneName, second.SceneName);
                    if (scene != 0) return scene;
                    int gate = StringComparer.OrdinalIgnoreCase.Compare(first.EntryGate, second.EntryGate);
                    return gate != 0
                        ? gate
                        : StringComparer.OrdinalIgnoreCase.Compare(first.SourceScene, second.SourceScene);
                });
                Save();
            }

            return changed;
        }

        public bool SetPvpMode(PvpMode mode)
        {
            if (mode != HKMP.Rounds.Settings.PvpMode.Quick && mode != HKMP.Rounds.Settings.PvpMode.Long && mode != HKMP.Rounds.Settings.PvpMode.OneShot)
            {
                return false;
            }

            PvpMode = mode;
            Save();
            return true;
        }

        public List<BenchLocation> GetKnownBenches()
        {
            return _knownBenches.Select(bench => bench.Copy()).ToList();
        }

        public bool ContainsKnownBench(BenchLocation location)
        {
            return location != null && _knownBenches.Any(item => item.IsSameLocation(location));
        }



        public bool MergeKnownBenches(IEnumerable<BenchLocation> locations)
        {
            return MergeKnownBenchesInternal(locations, true);
        }

        private bool MergeKnownBenchesInternal(IEnumerable<BenchLocation> locations, bool save)
        {
            if (locations == null)
            {
                return false;
            }

            bool changed = false;
            foreach (BenchLocation source in locations)
            {
                if (source == null || !source.IsValid ||
                    string.Equals(source.MarkerName, "DeployedBench", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                BenchLocation normalized = new BenchLocation(
                    source.SceneName,
                    source.MarkerName,
                    source.MapZone,
                    source.BenchName,
                    source.PhantomMapId);

                int existingIndex = _knownBenches.FindIndex(item => item.IsSameLocation(normalized));
                if (existingIndex >= 0)
                {
                    if (_knownBenches[existingIndex].MapZone != normalized.MapZone)
                    {
                        _knownBenches[existingIndex].MapZone = normalized.MapZone;
                        changed = true;
                    }

                    if (string.IsNullOrWhiteSpace(_knownBenches[existingIndex].BenchName) &&
                        !string.IsNullOrWhiteSpace(normalized.BenchName))
                    {
                        _knownBenches[existingIndex].BenchName = normalized.BenchName;
                        changed = true;
                    }

                    continue;
                }

                if (_knownBenches.Count >= MaximumKnownBenches)
                {
                    break;
                }

                _knownBenches.Add(normalized);
                changed = true;
            }

            if (changed)
            {
                _knownBenches.Sort((first, second) =>
                {
                    int sceneCompare = StringComparer.OrdinalIgnoreCase.Compare(
                        first.SceneName, second.SceneName);
                    if (sceneCompare != 0) return sceneCompare;
                    int markerCompare = StringComparer.OrdinalIgnoreCase.Compare(first.MarkerName, second.MarkerName);
                    return markerCompare != 0 ? markerCompare : first.PhantomMapId.CompareTo(second.PhantomMapId);
                });

                if (save)
                {
                    Save();
                }
            }

            return changed;
        }

        private void Load()
        {
            AutoStart = false;
            RoundSoul = DefaultRoundSoul;
            DisableEnemies = false;
            DebugDisableShade = false;
            TimerIntegrationEnabled = false;
            IndividualTeamWins = false;
            IndependentRoundsPerRoom = false;
            DisablePvpBeforeRound = true;
            GiveLifebloodMasks = false;
            VignetteEnabled = false;
            HidePhantomMantisBackground = true;
            ReadyRequiredCount = DefaultReadyRequiredCount;
            SharedBenchScene = string.Empty;
            SharedBenchMarker = string.Empty;
            SharedBenchMapZone = 0;
            SharedBenchPhantomMapId = 0;
            SharedBenchLocked = false;
            UiLanguage = "en";
            PvpMode = HKMP.Rounds.Settings.PvpMode.Quick;
            MapTravelEnabled = false;
            _phantomMantisMaps.Clear();
            _mapDestinations.Clear();
            _knownBenches.Clear();
            MergeKnownBenchesInternal(BenchCatalog.GetBuiltInCatalog(), false);

            try
            {
                if (!File.Exists(_filePath))
                {
                    return;
                }

                string json = File.ReadAllText(_filePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return;
                }

                SettingsData data = JsonConvert.DeserializeObject<SettingsData>(json);
                if (data == null)
                {
                    return;
                }

                AutoStart = data.AutoStart;
                RoundSoul = data.RoundSoul;
                DisableEnemies = data.DisableEnemies;
                DebugDisableShade = data.DebugDisableShade;
                TimerIntegrationEnabled = data.TimerIntegrationEnabled;
                IndividualTeamWins = data.IndividualTeamWins;
                IndependentRoundsPerRoom = data.IndependentRoundsPerRoom;
                DisablePvpBeforeRound = data.DisablePvpBeforeRound;
                GiveLifebloodMasks = data.GiveLifebloodMasks;
                VignetteEnabled = data.VignetteEnabled;
                HidePhantomMantisBackground = data.HidePhantomMantisBackground;
                ReadyRequiredCount = data.ReadyRequiredCount;
                SharedBenchScene = BenchLocation.Normalize(data.SharedBenchScene);
                SharedBenchMarker = BenchLocation.Normalize(data.SharedBenchMarker);
                SharedBenchMapZone = data.SharedBenchMapZone;
                SharedBenchPhantomMapId = Math.Max(0, data.SharedBenchPhantomMapId);
                SharedBenchLocked = data.SharedBenchLocked;
                if (string.Equals(SharedBenchMarker, "DeployedBench", StringComparison.OrdinalIgnoreCase))
                {
                    SharedBenchScene = string.Empty;
                    SharedBenchMarker = string.Empty;
                    SharedBenchMapZone = 0;
                    SharedBenchPhantomMapId = 0;
                    SharedBenchLocked = false;
                }
                UiLanguage = string.Equals(data.UiLanguage, "ru", StringComparison.OrdinalIgnoreCase) ? "ru" : "en";
                PvpMode = data.PvpMode == HKMP.Rounds.Settings.PvpMode.Long || data.PvpMode == HKMP.Rounds.Settings.PvpMode.OneShot
                    ? data.PvpMode
                    : HKMP.Rounds.Settings.PvpMode.Quick;
                MapTravelEnabled = data.MapTravelEnabled;

                _phantomMantisMaps.Clear();
                if (!string.IsNullOrWhiteSpace(data.PhantomMantisMapsJson))
                {
                    try
                    {
                        List<PhantomMantisMap> loadedMaps = JsonConvert.DeserializeObject<List<PhantomMantisMap>>(data.PhantomMantisMapsJson);
                        if (loadedMaps != null)
                        {
                            foreach (PhantomMantisMap map in loadedMaps)
                            {
                                if (map != null && map.IsValid && !_phantomMantisMaps.Any(existing => existing.Id == map.Id))
                                {
                                    _phantomMantisMaps.Add(map.Copy());
                                }
                            }
                            _phantomMantisMaps.Sort((first, second) => first.Id.CompareTo(second.Id));
                        }
                    }
                    catch
                    {
                        _phantomMantisMaps.Clear();
                    }
                }

                _mapDestinations.Clear();
                if (!string.IsNullOrWhiteSpace(data.MapDestinationsJson) && data.MapDestinationsJson.Length <= MaximumMapDestinationsStoreJsonLength)
                {
                    try
                    {
                        List<SceneTravelDestination> loadedDestinations = JsonConvert.DeserializeObject<List<SceneTravelDestination>>(data.MapDestinationsJson);
                        if (loadedDestinations != null)
                        {
                            foreach (SceneTravelDestination destination in loadedDestinations)
                            {
                                if (destination != null && destination.IsValid && _mapDestinations.Count < MaximumMapDestinations)
                                {
                                    _mapDestinations.Add(destination.Copy());
                                }
                            }
                        }
                    }
                    catch
                    {
                        _mapDestinations.Clear();
                    }
                }
                if (RoundSoul < 0 || RoundSoul > MaximumRoundSoul)
                {
                    RoundSoul = DefaultRoundSoul;
                }

                if (ReadyRequiredCount < 0 || ReadyRequiredCount > MaximumReadyRequiredCount)
                {
                    ReadyRequiredCount = DefaultReadyRequiredCount;
                }

                if (!string.IsNullOrWhiteSpace(data.KnownBenchesJson) &&
                    data.KnownBenchesJson.Length <= MaximumKnownBenchesJsonLength)
                {
                    try
                    {
                        List<BenchLocation> locations =
                            JsonConvert.DeserializeObject<List<BenchLocation>>(data.KnownBenchesJson);
                        MergeKnownBenchesInternal(locations, false);
                    }
                    catch
                    {
                        _knownBenches.Clear();
                        MergeKnownBenchesInternal(BenchCatalog.GetBuiltInCatalog(), false);
                    }
                }


                if (!HasSharedBench)
                {
                    SharedBenchLocked = false;
                }

                bool addedPhantomBenches = false;
                for (int i = 0; i < _phantomMantisMaps.Count; i++)
                {
                    PhantomMantisMap map = _phantomMantisMaps[i];
                    if (map != null && map.IsValid &&
                        MergeKnownBenchesInternal(
                            new[] { new BenchLocation("Fungus2_31", "RestBench", 13, "Phantom Mantis Village #" + map.Id, map.Id) },
                            false))
                    {
                        addedPhantomBenches = true;
                    }
                }

                if (HasSharedBench)
                {
                    bool addedSharedBench = MergeKnownBenchesInternal(
                        new[] { new BenchLocation(SharedBenchScene, SharedBenchMarker, SharedBenchMapZone, string.Empty, SharedBenchPhantomMapId) },
                        false);
                    if (addedSharedBench || addedPhantomBenches)
                    {
                        Save();
                    }
                }
                else if (addedPhantomBenches)
                {
                    Save();
                }
            }
            catch (Exception exception)
            {
                AutoStart = false;
                RoundSoul = DefaultRoundSoul;
                DisableEnemies = false;
                DebugDisableShade = false;
                TimerIntegrationEnabled = false;
                IndividualTeamWins = false;
                IndependentRoundsPerRoom = false;
                DisablePvpBeforeRound = true;
                GiveLifebloodMasks = false;
                VignetteEnabled = false;
                HidePhantomMantisBackground = true;
                ReadyRequiredCount = DefaultReadyRequiredCount;
                SharedBenchScene = string.Empty;
                SharedBenchMarker = string.Empty;
                SharedBenchMapZone = 0;
                SharedBenchPhantomMapId = 0;
                SharedBenchLocked = false;
                UiLanguage = "en";
                PvpMode = HKMP.Rounds.Settings.PvpMode.Quick;
                MapTravelEnabled = false;
                _phantomMantisMaps.Clear();
                _mapDestinations.Clear();
                _knownBenches.Clear();
                MergeKnownBenchesInternal(BenchCatalog.GetBuiltInCatalog(), false);
                Modding.Logger.LogError(
                    "[HKMP.Rounds] Failed to load global settings. Exception=" + exception);
            }
        }

        private void Save()
        {
            try
            {
                SettingsData data = new SettingsData
                {
                    AutoStart = AutoStart,
                    RoundSoul = RoundSoul,
                    DisableEnemies = DisableEnemies,
                    DebugDisableShade = DebugDisableShade,
                    TimerIntegrationEnabled = TimerIntegrationEnabled,
                    IndividualTeamWins = IndividualTeamWins,
                    IndependentRoundsPerRoom = IndependentRoundsPerRoom,
                    DisablePvpBeforeRound = DisablePvpBeforeRound,
                    GiveLifebloodMasks = GiveLifebloodMasks,
                    VignetteEnabled = VignetteEnabled,
                    HidePhantomMantisBackground = HidePhantomMantisBackground,
                    ReadyRequiredCount = ReadyRequiredCount,
                    SharedBenchScene = SharedBenchScene ?? string.Empty,
                    SharedBenchMarker = SharedBenchMarker ?? string.Empty,
                    SharedBenchMapZone = SharedBenchMapZone,
                    SharedBenchPhantomMapId = SharedBenchPhantomMapId,
                    SharedBenchLocked = SharedBenchLocked,
                    UiLanguage = UiLanguage ?? "en",
                    KnownBenchesJson = JsonConvert.SerializeObject(_knownBenches, Formatting.None),
                    MapTravelEnabled = MapTravelEnabled,
                    PhantomMantisMapsJson = JsonConvert.SerializeObject(_phantomMantisMaps, Formatting.None),
                    MapDestinationsJson = JsonConvert.SerializeObject(_mapDestinations, Formatting.None),
                    PvpMode = PvpMode
                };

                string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                File.WriteAllText(_filePath, json);
            }
            catch (Exception exception)
            {
                Modding.Logger.LogError(
                    "[HKMP.Rounds] Failed to save global settings. Exception=" + exception);
            }
        }
    }
}
