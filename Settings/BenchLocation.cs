using System;

namespace HKMP.Rounds.Settings
{
    public sealed class BenchLocation : IEquatable<BenchLocation>
    {
        public string SceneName { get; set; }
        public string MarkerName { get; set; }
        public int MapZone { get; set; }
        public string BenchName { get; set; }
        public int PhantomMapId { get; set; }

        public BenchLocation()
        {
            SceneName = string.Empty;
            MarkerName = string.Empty;
            BenchName = string.Empty;
        }

        public BenchLocation(string sceneName, string markerName, int mapZone)
            : this(sceneName, markerName, mapZone, string.Empty, 0)
        {
        }

        public BenchLocation(string sceneName, string markerName, int mapZone, string benchName)
            : this(sceneName, markerName, mapZone, benchName, 0)
        {
        }

        public BenchLocation(string sceneName, string markerName, int mapZone, string benchName, int phantomMapId)
        {
            SceneName = Normalize(sceneName);
            MarkerName = Normalize(markerName);
            MapZone = mapZone;
            BenchName = Normalize(benchName);
            PhantomMapId = Math.Max(0, phantomMapId);
        }

        public bool IsValid
        {
            get
            {
                return !string.IsNullOrWhiteSpace(SceneName) &&
                       !string.IsNullOrWhiteSpace(MarkerName);
            }
        }

        public string DisplayName
        {
            get
            {
                string scene = SceneName ?? string.Empty;
                string marker = MarkerName ?? string.Empty;
                string name = Normalize(BenchName);
                string displayName = string.IsNullOrWhiteSpace(name)
                    ? scene + "  /  " + marker
                    : name + "  ·  " + scene + " / " + marker;
                if (PhantomMapId > 0)
                {
                    displayName = string.Equals(SceneName, "Fungus2_31", StringComparison.OrdinalIgnoreCase)
                        ? "Phantom Mantis Village #" + PhantomMapId + "  ·  " + scene + " / " + marker
                        : displayName + " (Phantom #" + PhantomMapId + ")";
                }
                return displayName;
            }
        }

        public string Key
        {
            get
            {
                return Normalize(SceneName) + "|" + Normalize(MarkerName) + "|" + PhantomMapId;
            }
        }

        public bool IsSameLocation(BenchLocation other)
        {
            return other != null &&
                   string.Equals(Normalize(SceneName), Normalize(other.SceneName), StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(Normalize(MarkerName), Normalize(other.MarkerName), StringComparison.OrdinalIgnoreCase) &&
                   PhantomMapId == other.PhantomMapId;
        }

        public bool Equals(BenchLocation other)
        {
            return IsSameLocation(other);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as BenchLocation);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = StringComparer.OrdinalIgnoreCase.GetHashCode(Normalize(SceneName));
                hash = (hash * 397) ^ StringComparer.OrdinalIgnoreCase.GetHashCode(Normalize(MarkerName));
                hash = (hash * 397) ^ PhantomMapId;
                return hash;
            }
        }

        public BenchLocation Copy()
        {
            return new BenchLocation(SceneName, MarkerName, MapZone, BenchName, PhantomMapId);
        }

        public static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}
