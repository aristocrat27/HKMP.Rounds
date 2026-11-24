using System;
using Newtonsoft.Json;

namespace HKMP.Rounds.Settings
{
    [Serializable]
    internal sealed class PhantomMantisMap
    {
        [JsonProperty("i")]
        public int Id { get; set; }

        [JsonProperty("n")]
        public string Name { get; set; }

        [JsonProperty("t")]
        public long CreatedUtcTicks { get; set; }

        [JsonIgnore]
        public string DisplayName
        {
            get
            {
                return string.IsNullOrWhiteSpace(Name)
                    ? "Phantom Mantis Village #" + Id
                    : Name;
            }
        }

        public PhantomMantisMap Copy()
        {
            return new PhantomMantisMap
            {
                Id = Id,
                Name = Name ?? string.Empty,
                CreatedUtcTicks = CreatedUtcTicks
            };
        }

        public bool IsValid
        {
            get { return Id > 0; }
        }
    }

    [Serializable]
    internal sealed class SceneTravelDestination
    {
        [JsonProperty("s")]
        public string SourceScene { get; set; }

        [JsonProperty("d")]
        public string SceneName { get; set; }

        [JsonProperty("g")]
        public string EntryGate { get; set; }

        [JsonProperty("a")]
        public string Area { get; set; }

        [JsonProperty("c")]
        public bool IsCustom { get; set; }

        [JsonProperty("k")]
        public bool IsCustomKnown { get; set; }

        public SceneTravelDestination Copy()
        {
            return new SceneTravelDestination
            {
                SourceScene = SourceScene ?? string.Empty,
                SceneName = SceneName ?? string.Empty,
                EntryGate = EntryGate ?? string.Empty,
                Area = Area ?? string.Empty,
                IsCustom = IsCustom,
                IsCustomKnown = IsCustomKnown
            };
        }

        public bool IsValid
        {
            get
            {
                return IsSafeName(SourceScene, 160) &&
                       IsSafeName(SceneName, 160) &&
                       IsSafeName(EntryGate, 128);
            }
        }

        private static bool IsSafeName(string value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > maxLength)
            {
                return false;
            }

            for (int i = 0; i < value.Length; i++)
            {
                if (char.IsControl(value[i]))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
