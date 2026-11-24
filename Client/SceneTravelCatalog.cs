using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HKMP.Rounds.Settings;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace HKMP.Rounds.Client
{
    internal static class SceneTravelCatalog
    {
        private const int MaximumCatalogEntries = 1200;
        private const int MaximumCatalogJsonLength = 46000;

        private static readonly Dictionary<string, SceneTravelDestination> Destinations =
            new Dictionary<string, SceneTravelDestination>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> VanillaScenes =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static bool _benchwarpCatalogLoaded;

        public static string BuildFullCatalogJson()
        {
            LoadBenchwarpCatalog();
            ObserveCurrentScene();
            List<SceneTravelDestination> destinations = Destinations.Values
                .GroupBy(item => MakeTravelChoiceKey(item.SceneName, item.EntryGate), StringComparer.OrdinalIgnoreCase)
                .Select(group => group
                    .OrderByDescending(item => item.IsCustomKnown)
                    .ThenByDescending(item => !string.IsNullOrWhiteSpace(item.Area))
                    .First())
                .OrderBy(item => item.SceneName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.EntryGate, StringComparer.OrdinalIgnoreCase)
                .ToList();
            return SerializeCatalog(destinations);
        }

        public static string BuildCurrentSceneJson()
        {
            ObserveCurrentScene();
            string currentScene = SafeCurrentScene();
            List<SceneTravelDestination> current = Destinations.Values
                .Where(item => string.Equals(item.SourceScene, currentScene, StringComparison.OrdinalIgnoreCase))
                .OrderBy(item => item.SceneName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.EntryGate, StringComparer.OrdinalIgnoreCase)
                .ToList();
            return SerializeCatalog(current);
        }

        public static void ObserveCurrentScene()
        {
            string sourceScene = SafeCurrentScene();
            if (string.IsNullOrWhiteSpace(sourceScene))
            {
                return;
            }

            TransitionPoint[] points;
            try
            {
                points = Object.FindObjectsOfType<TransitionPoint>();
            }
            catch
            {
                points = new TransitionPoint[0];
            }

            string area = string.Empty;
            try
            {
                if (GameManager.instance != null && GameManager.instance.sm != null)
                {
                    area = GameManager.instance.sm.mapZone.ToString();
                }
            }
            catch
            {
            }

            if (points == null)
            {
                return;
            }

            foreach (TransitionPoint point in points)
            {
                if (point == null || !point.gameObject.activeInHierarchy ||
                    string.IsNullOrWhiteSpace(point.targetScene))
                {
                    continue;
                }

                string gate = string.IsNullOrWhiteSpace(point.entryPoint)
                    ? "door"
                    : point.entryPoint.Trim();
                Add(new SceneTravelDestination
                {
                    SourceScene = sourceScene,
                    SceneName = point.targetScene.Trim(),
                    EntryGate = gate,
                    Area = area,
                    IsCustom = VanillaScenes.Count > 0 && !VanillaScenes.Contains(point.targetScene.Trim()),
                    IsCustomKnown = VanillaScenes.Count > 0
                });
            }
        }

        private static void LoadBenchwarpCatalog()
        {
            if (_benchwarpCatalogLoaded)
            {
                return;
            }

            Type doorWarpType = null;
            try
            {
                doorWarpType = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(assembly => assembly.GetType("Benchwarp.DoorWarp", false))
                    .FirstOrDefault(type => type != null);
            }
            catch
            {
            }

            if (doorWarpType == null)
            {
                return;
            }

            try
            {
                object entries = ReadStaticMember(doorWarpType, "Doors");
                IEnumerable items = entries as IEnumerable;
                if (items == null)
                {
                    return;
                }

                foreach (object item in items)
                {
                    if (item == null)
                    {
                        continue;
                    }

                    object self = ReadMember(item, "Self");
                    object target = ReadMember(item, "Target");
                    string area = Convert.ToString(ReadMember(item, "Area")) ?? string.Empty;
                    string sourceScene;
                    string sourceGate;
                    string targetScene;
                    string targetGate;

                    if (!TryReadDoorTarget(self, out sourceScene, out sourceGate) ||
                        !TryReadDoorTarget(target, out targetScene, out targetGate))
                    {
                        continue;
                    }

                    VanillaScenes.Add(sourceScene);
                    VanillaScenes.Add(targetScene);
                    Add(new SceneTravelDestination
                    {
                        SourceScene = sourceScene,
                        SceneName = targetScene,
                        EntryGate = targetGate,
                        Area = area,
                        IsCustom = string.IsNullOrWhiteSpace(area),
                        IsCustomKnown = true
                    });
                }

                _benchwarpCatalogLoaded = true;
            }
            catch (Exception exception)
            {
                Modding.Logger.Log(
                    "[HKMP.Rounds] Could not read Benchwarp's door catalog: " + exception.Message);
            }
        }

        private static bool TryReadDoorTarget(
            object value,
            out string scene,
            out string gate)
        {
            scene = string.Empty;
            gate = string.Empty;
            if (value == null)
            {
                return false;
            }

            object roomValue = ReadMember(value, "room");
            object gateValue = ReadMember(value, "door");
            scene = Convert.ToString(roomValue) ?? string.Empty;
            gate = Convert.ToString(gateValue) ?? string.Empty;

            if (string.IsNullOrWhiteSpace(scene) || string.IsNullOrWhiteSpace(gate))
            {
                string valueText;
                try
                {
                    valueText = value.ToString();
                }
                catch
                {
                    valueText = string.Empty;
                }

                int opening = valueText.IndexOf('[');
                int closing = valueText.LastIndexOf(']');
                if (opening <= 0 || closing <= opening + 1)
                {
                    return false;
                }

                scene = valueText.Substring(0, opening).Trim();
                gate = valueText.Substring(opening + 1, closing - opening - 1).Trim();
            }

            return !string.Equals(scene, "None", StringComparison.OrdinalIgnoreCase) &&
                   !string.IsNullOrWhiteSpace(scene) &&
                   !string.IsNullOrWhiteSpace(gate);
        }

        private static object ReadStaticMember(Type type, string name)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            FieldInfo field = type.GetField(name, flags);
            if (field != null) return field.GetValue(null);
            PropertyInfo property = type.GetProperty(name, flags);
            return property == null ? null : property.GetValue(null, null);
        }

        private static object ReadMember(object source, string name)
        {
            if (source == null) return null;
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            Type type = source.GetType();
            FieldInfo field = type.GetField(name, flags);
            if (field != null) return field.GetValue(source);
            PropertyInfo property = type.GetProperty(name, flags);
            return property == null ? null : property.GetValue(source, null);
        }

        private static void Add(SceneTravelDestination destination)
        {
            if (destination == null || !destination.IsValid || Destinations.Count >= MaximumCatalogEntries)
            {
                return;
            }

            string key = MakeKey(destination.SourceScene, destination.SceneName, destination.EntryGate);
            SceneTravelDestination existing;
            if (Destinations.TryGetValue(key, out existing))
            {
                if (string.IsNullOrWhiteSpace(existing.Area) && !string.IsNullOrWhiteSpace(destination.Area))
                {
                    existing.Area = destination.Area;
                }
                if (destination.IsCustomKnown)
                {
                    existing.IsCustomKnown = true;
                    existing.IsCustom = destination.IsCustom;
                }
                return;
            }

            Destinations[key] = destination.Copy();
        }

        private static string MakeKey(string source, string target, string gate)
        {
            return (source ?? string.Empty) + "\u001f" +
                   (target ?? string.Empty) + "\u001f" +
                   (gate ?? string.Empty);
        }

        private static string MakeTravelChoiceKey(string sceneName, string gateName)
        {
            return (sceneName ?? string.Empty) + "\u001f" + (gateName ?? string.Empty);
        }

        private static string SerializeCatalog(List<SceneTravelDestination> destinations)
        {
            try
            {
                string json = JsonConvert.SerializeObject(destinations ?? new List<SceneTravelDestination>(), Formatting.None);
                if (json.Length <= MaximumCatalogJsonLength)
                {
                    return json;
                }

                while (destinations != null && destinations.Count > 0)
                {
                    destinations.RemoveAt(destinations.Count - 1);
                    json = JsonConvert.SerializeObject(destinations, Formatting.None);
                    if (json.Length <= MaximumCatalogJsonLength)
                    {
                        return json;
                    }
                }
            }
            catch
            {
            }

            return "[]";
        }

        private static string SafeCurrentScene()
        {
            try
            {
                string sceneName = GameManager.instance != null
                    ? GameManager.instance.sceneName
                    : UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                return PhantomMapClientController.GetLogicalSceneName(sceneName);
            }
            catch
            {
                try
                {
                    return PhantomMapClientController.GetLogicalSceneName(
                        UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
                }
                catch
                {
                    return string.Empty;
                }
            }
        }
    }
}
