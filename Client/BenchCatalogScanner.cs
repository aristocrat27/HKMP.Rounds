using System;
using System.Collections.Generic;
using System.Linq;
using GlobalEnums;
using HKMP.Rounds.Settings;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HKMP.Rounds.Client
{
    internal static class BenchCatalogScanner
    {
        private const float SetHereDistance = 7.5f;

        public static List<BenchLocation> ScanCurrentScene()
        {
            string sceneName = PhantomMapClientController.GetCurrentLogicalScene();
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                return new List<BenchLocation>();
            }

            int mapZone = GetCurrentMapZone();
            Dictionary<string, BenchLocation> result =
                new Dictionary<string, BenchLocation>(StringComparer.OrdinalIgnoreCase);

            List<RespawnMarker> markers = GetCurrentSceneBenchMarkers(sceneName);
            for (int i = 0; i < markers.Count; i++)
            {
                RespawnMarker marker = markers[i];
                if (marker == null || marker.gameObject == null)
                {
                    continue;
                }

                Add(result, CreateBenchLocation(sceneName, marker.gameObject.name, mapZone));
            }

            PlayerData data = PlayerData.instance;
            if (data != null &&
                data.atBench &&
                data.respawnType == 1 &&
                string.Equals(data.respawnScene, sceneName, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(data.respawnMarkerName) &&
                BenchPolicyController.CanUseBenchLocation(CreateBenchLocation(
                    data.respawnScene,
                    data.respawnMarkerName,
                    (int)data.mapZone)))
            {
                Add(result, CreateBenchLocation(sceneName, data.respawnMarkerName, (int)data.mapZone));
            }

            return result.Values
                .OrderBy(location => location.SceneName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(location => location.MarkerName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static string ScanCurrentSceneJson()
        {
            try
            {
                return Newtonsoft.Json.JsonConvert.SerializeObject(
                    ScanCurrentScene(),
                    Newtonsoft.Json.Formatting.None);
            }
            catch
            {
                return "[]";
            }
        }

        public static BenchLocation FindBenchForSetHere(out bool atBench)
        {
            atBench = false;
            string sceneName = PhantomMapClientController.GetCurrentLogicalScene();
            PlayerData data = PlayerData.instance;

            if (data != null &&
                data.atBench &&
                data.respawnType == 1 &&
                string.Equals(data.respawnScene, sceneName, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(data.respawnMarkerName) &&
                BenchPolicyController.CanUseBenchLocation(CreateBenchLocation(
                    data.respawnScene,
                    data.respawnMarkerName,
                    (int)data.mapZone)))
            {
                atBench = true;
                return CreateBenchLocation(
                    data.respawnScene,
                    data.respawnMarkerName,
                    (int)data.mapZone);
            }

            HeroController hero = HeroController.instance;
            if (hero == null)
            {
                return null;
            }

            List<RespawnMarker> markers = GetCurrentSceneBenchMarkers(sceneName);
            RespawnMarker nearest = null;
            float nearestDistance = SetHereDistance * SetHereDistance;

            for (int i = 0; i < markers.Count; i++)
            {
                RespawnMarker marker = markers[i];
                if (marker == null || marker.gameObject == null)
                {
                    continue;
                }

                float distance = (marker.transform.position - hero.transform.position).sqrMagnitude;
                if (distance <= nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = marker;
                }
            }

            if (nearest == null)
            {
                return null;
            }

            return CreateBenchLocation(sceneName, nearest.gameObject.name, GetCurrentMapZone());
        }

        public static bool TryGetMarkerPosition(BenchLocation location, out Vector3 position)
        {
            position = Vector3.zero;
            if (location == null || !location.IsValid ||
                !BenchPolicyController.CanUseBenchLocation(location) ||
                !string.Equals(
                    location.SceneName,
                    PhantomMapClientController.GetCurrentLogicalScene(),
                    StringComparison.OrdinalIgnoreCase) ||
                (IsMantisScene(location.SceneName) &&
                 location.PhantomMapId != PhantomMapClientController.CurrentPhantomMapId))
            {
                return false;
            }

            UnityEngine.SceneManagement.Scene sourceScene =
                UnityEngine.SceneManagement.SceneManager.GetSceneByName(location.SceneName);
            if (!sourceScene.IsValid() || !sourceScene.isLoaded)
            {
                UnityEngine.SceneManagement.Scene activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                if (activeScene.IsValid() && activeScene.isLoaded &&
                    string.Equals(PhantomMapClientController.GetCurrentLogicalScene(), location.SceneName, StringComparison.OrdinalIgnoreCase))
                {
                    sourceScene = activeScene;
                }
            }

            if (!sourceScene.IsValid() || !sourceScene.isLoaded)
            {
                return false;
            }

            RespawnMarker[] sourceMarkers = Object.FindObjectsOfType<RespawnMarker>();
            for (int i = 0; i < sourceMarkers.Length; i++)
            {
                RespawnMarker marker = sourceMarkers[i];
                if (marker != null && marker.gameObject != null && marker.gameObject.activeInHierarchy &&
                    marker.gameObject.scene.handle == sourceScene.handle &&
                    string.Equals(marker.gameObject.name, location.MarkerName, StringComparison.OrdinalIgnoreCase))
                {
                    position = marker.transform.position;
                    return true;
                }
            }

            return false;
        }


        public static RespawnMarker FindAssociatedMarker(PlayMakerFSM fsm, GameObject searchRoot = null)
        {
            if (fsm == null)
            {
                return null;
            }

            RespawnMarker direct = fsm.GetComponent<RespawnMarker>();
            if (direct != null)
            {
                return direct;
            }

            RespawnMarker child = fsm.GetComponentInChildren<RespawnMarker>(true);
            if (child != null)
            {
                return child;
            }

            RespawnMarker parent = fsm.GetComponentInParent<RespawnMarker>();
            if (parent != null)
            {
                return parent;
            }

            RespawnMarker[] markers = searchRoot != null
                ? searchRoot.GetComponentsInChildren<RespawnMarker>(true)
                : Object.FindObjectsOfType<RespawnMarker>();
            RespawnMarker nearest = null;
            float distanceLimit = 5f * 5f;
            int fsmSceneHandle = fsm.gameObject.scene.handle;
            for (int i = 0; i < markers.Length; i++)
            {
                RespawnMarker marker = markers[i];
                if (marker == null || marker.gameObject == null ||
                    marker.gameObject.scene.handle != fsmSceneHandle)
                {
                    continue;
                }

                float distance = (marker.transform.position - fsm.transform.position).sqrMagnitude;
                if (distance < distanceLimit)
                {
                    distanceLimit = distance;
                    nearest = marker;
                }
            }

            return nearest;
        }

        private static List<RespawnMarker> GetCurrentSceneBenchMarkers(string sceneName)
        {
            List<RespawnMarker> result = new List<RespawnMarker>();
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                return result;
            }

            UnityEngine.SceneManagement.Scene sourceScene =
                UnityEngine.SceneManagement.SceneManager.GetSceneByName(sceneName);
            if (!sourceScene.IsValid() || !sourceScene.isLoaded)
            {
                UnityEngine.SceneManagement.Scene activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                if (activeScene.IsValid() && activeScene.isLoaded &&
                    string.Equals(PhantomMapClientController.GetCurrentLogicalScene(), sceneName, StringComparison.OrdinalIgnoreCase))
                {
                    sourceScene = activeScene;
                }
            }

            if (!sourceScene.IsValid() || !sourceScene.isLoaded)
            {
                return result;
            }

            RespawnMarker[] markers = Object.FindObjectsOfType<RespawnMarker>();
            for (int i = 0; i < markers.Length; i++)
            {
                RespawnMarker marker = markers[i];
                if (marker != null && marker.gameObject != null && marker.gameObject.activeInHierarchy &&
                    marker.gameObject.scene.handle == sourceScene.handle &&
                    IsBenchMarker(marker) && !result.Contains(marker))
                {
                    result.Add(marker);
                }
            }

            PlayMakerFSM[] fsms = Object.FindObjectsOfType<PlayMakerFSM>();
            for (int i = 0; i < fsms.Length; i++)
            {
                PlayMakerFSM fsm = fsms[i];
                if (fsm == null || fsm.gameObject == null ||
                    !string.Equals(fsm.FsmName, "Bench Control", StringComparison.OrdinalIgnoreCase) ||
                    fsm.gameObject.scene.handle != sourceScene.handle)
                {
                    continue;
                }

                RespawnMarker marker = FindAssociatedMarker(fsm);
                if (marker != null && marker.gameObject != null && marker.gameObject.activeInHierarchy &&
                    marker.gameObject.scene.handle == sourceScene.handle && !result.Contains(marker))
                {
                    result.Add(marker);
                }
            }

            return result;
        }


        private static bool IsBenchMarker(RespawnMarker marker)
        {
            string name = marker.gameObject.name ?? string.Empty;
            if (name.IndexOf("bench", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            Transform current = marker.transform;
            for (int depth = 0; current != null && depth < 6; depth++, current = current.parent)
            {
                PlayMakerFSM[] ownFsms = current.GetComponents<PlayMakerFSM>();
                for (int i = 0; i < ownFsms.Length; i++)
                {
                    if (ownFsms[i] != null &&
                        string.Equals(ownFsms[i].FsmName, "Bench Control", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            PlayMakerFSM[] childFsms = marker.GetComponentsInChildren<PlayMakerFSM>(true);
            for (int i = 0; i < childFsms.Length; i++)
            {
                if (childFsms[i] != null &&
                    string.Equals(childFsms[i].FsmName, "Bench Control", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static int GetCurrentMapZone()
        {
            try
            {
                if (GameManager.instance != null && GameManager.instance.sm != null)
                {
                    return (int)GameManager.instance.sm.mapZone;
                }
            }
            catch
            {
            }

            return PlayerData.instance == null ? (int)MapZone.NONE : (int)PlayerData.instance.mapZone;
        }

        private static BenchLocation CreateBenchLocation(string sceneName, string markerName, int mapZone)
        {
            return new BenchLocation(
                sceneName,
                markerName,
                mapZone,
                string.Empty,
                PhantomMapClientController.GetPhantomMapIdForScene(sceneName));
        }

        private static bool IsMantisScene(string sceneName)
        {
            return string.Equals(sceneName, "Fungus2_15", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(sceneName, "Fungus2_31", StringComparison.OrdinalIgnoreCase);
        }

        private static void Add(IDictionary<string, BenchLocation> result, BenchLocation location)
        {
            if (result == null || location == null || !location.IsValid)
            {
                return;
            }

            if (!result.ContainsKey(location.Key))
            {
                result.Add(location.Key, location);
            }
        }
    }
}