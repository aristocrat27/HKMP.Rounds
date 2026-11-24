using System;
using System.Linq;
using HutongGames.PlayMaker;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace HKMP.Rounds.Client
{
    internal static class MapTravelProgressController
    {
        private static readonly string[] PassageTokens =
        {
            "gate",
            "door",
            "passage",
            "lever",
            "switch",
            "bridge",
            "barrier"
        };

        private static readonly string[] SecretWallTokens =
        {
            "breakable wall",
            "secret wall",
            "hidden wall",
            "secretwall",
            "hiddenwall"
        };

        private static readonly string[] OpenStateNames =
        {
            "Open",
            "Opened",
            "Activated",
            "Up",
            "On",
            "Unlocked"
        };

        private static readonly string[] LeverStateNames =
        {
            "Activated",
            "On",
            "Up",
            "Open",
            "Opened"
        };

        private static readonly string[] ActivatedBoolNames =
        {
            "Activated",
            "IsActivated",
            "Open",
            "Opened",
            "IsOpen",
            "IsOpened",
            "Used",
            "IsUsed",
            "On",
            "IsOn",
            "Unlocked",
            "IsUnlocked"
        };

        public static void ApplyCurrentScene()
        {
            if (PhantomMapClientController.IsActive)
            {
                Scene phantomScene;
                string logicalSceneName;
                if (PhantomMapClientController.TryGetActivePhantomScene(out phantomScene, out logicalSceneName))
                {
                    ApplyPhantomScene(phantomScene, logicalSceneName);
                }
                return;
            }

            Scene scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!scene.IsValid() || string.IsNullOrWhiteSpace(scene.name))
            {
                return;
            }

            ApplyScene(scene, PhantomMapClientController.GetCurrentLogicalScene(), true);
        }

        internal static void ApplyPhantomScene(Scene scene, string logicalSceneName)
        {
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(logicalSceneName))
            {
                return;
            }

            MantisVillageProgressController.ApplyProgressToScene(logicalSceneName, scene);
            ApplyScene(scene, logicalSceneName, false);
        }

        private static void ApplyScene(Scene scene, string logicalSceneName, bool persistProgress)
        {
            GameManager gameManager = GameManager.instance;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                GameObject root = roots[i];
                if (root == null)
                {
                    continue;
                }

                PersistentBoolItem[] items = root.GetComponentsInChildren<PersistentBoolItem>(true);
                for (int j = 0; j < items.Length; j++)
                {
                    PersistentBoolItem item = items[j];
                    if (item == null || item.gameObject == null || item.persistentBoolData == null)
                    {
                        continue;
                    }

                    string id = item.persistentBoolData.id ?? string.Empty;
                    string objectName = item.gameObject.name ?? string.Empty;
                    if (IsSecretWall(id, objectName))
                    {
                        if (WasSecretWallBroken(gameManager, logicalSceneName, item))
                        {
                            SaveActivatedState(gameManager, logicalSceneName, item, persistProgress);
                            Object.Destroy(item.gameObject);
                        }
                        continue;
                    }

                    if (IsPassageOrLever(id, objectName))
                    {
                        SaveActivatedState(gameManager, logicalSceneName, item, persistProgress);
                    }
                }
            }

            for (int i = 0; i < roots.Length; i++)
            {
                GameObject root = roots[i];
                if (root == null)
                {
                    continue;
                }

                PlayMakerFSM[] fsms = root.GetComponentsInChildren<PlayMakerFSM>(true);
                for (int j = 0; j < fsms.Length; j++)
                {
                    PlayMakerFSM fsm = fsms[j];
                    if (fsm == null || fsm.gameObject == null)
                    {
                        continue;
                    }

                    string fsmName = fsm.FsmName ?? string.Empty;
                    string objectName = fsm.gameObject.name ?? string.Empty;
                    if (IsSecretWall(fsmName, objectName))
                    {
                        if (WasSecretWallBroken(gameManager, logicalSceneName, fsm))
                        {
                            SaveActivatedState(gameManager, logicalSceneName, fsm, persistProgress);
                            Object.Destroy(fsm.gameObject);
                        }
                        continue;
                    }

                    if (IsPassageOrLever(fsmName, objectName))
                    {
                        ActivateFsm(fsm);
                        SaveActivatedState(gameManager, logicalSceneName, fsm, persistProgress);
                    }
                }
            }
        }

        private static bool IsPassageOrLever(string id, string objectName)
        {
            string value = (id + " " + objectName).ToLowerInvariant();
            if (IsSecretWall(id, objectName))
            {
                return false;
            }

            return PassageTokens.Any(value.Contains);
        }

        private static bool IsSecretWall(string id, string objectName)
        {
            string value = (id + " " + objectName).ToLowerInvariant();
            return SecretWallTokens.Any(value.Contains);
        }

        private static bool WasSecretWallBroken(GameManager gameManager, string sceneName, PersistentBoolItem item)
        {
            if (item != null && item.persistentBoolData != null && item.persistentBoolData.activated)
            {
                return true;
            }

            return IsPersistentlyActivated(gameManager, sceneName, item == null || item.persistentBoolData == null
                ? string.Empty
                : item.persistentBoolData.id);
        }

        private static bool WasSecretWallBroken(GameManager gameManager, string sceneName, PlayMakerFSM fsm)
        {
            if (fsm == null)
            {
                return false;
            }

            PersistentBoolItem item = fsm.GetComponent<PersistentBoolItem>();
            if (item == null)
            {
                item = fsm.GetComponentInChildren<PersistentBoolItem>(true);
            }
            if (item == null)
            {
                item = fsm.GetComponentInParent<PersistentBoolItem>();
            }

            if (WasSecretWallBroken(gameManager, sceneName, item))
            {
                return true;
            }

            return IsPersistentlyActivated(gameManager, sceneName, fsm.gameObject.name);
        }

        private static bool IsPersistentlyActivated(GameManager gameManager, string sceneName, string id)
        {
            if (gameManager == null || gameManager.sceneData == null || string.IsNullOrWhiteSpace(id))
            {
                return false;
            }

            try
            {
                PersistentBoolData saved = gameManager.sceneData.FindMyState(new PersistentBoolData
                {
                    id = id,
                    sceneName = sceneName
                });
                return saved != null && saved.activated;
            }
            catch
            {
                return false;
            }
        }

        private static void SaveActivatedState(GameManager gameManager, string sceneName, PersistentBoolItem item, bool persistProgress)
        {
            if (item == null || item.persistentBoolData == null)
            {
                return;
            }

            item.persistentBoolData.sceneName = sceneName;
            item.persistentBoolData.activated = true;
            item.persistentBoolData.semiPersistent = false;
            if (!persistProgress || gameManager == null || gameManager.sceneData == null)
            {
                return;
            }

            try
            {
                gameManager.sceneData.SaveMyState(item.persistentBoolData);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[HKMP.Rounds] Could not persist map progression state " +
                    item.persistentBoolData.id + ": " + exception.Message);
            }
        }

        private static void SaveActivatedState(GameManager gameManager, string sceneName, PlayMakerFSM fsm, bool persistProgress)
        {
            if (fsm == null)
            {
                return;
            }

            PersistentBoolItem item = fsm.GetComponent<PersistentBoolItem>();
            if (item == null)
            {
                item = fsm.GetComponentInChildren<PersistentBoolItem>(true);
            }
            if (item == null)
            {
                item = fsm.GetComponentInParent<PersistentBoolItem>();
            }
            if (item != null)
            {
                SaveActivatedState(gameManager, sceneName, item, persistProgress);
            }
        }

        private static void ActivateFsm(PlayMakerFSM fsm)
        {
            if (fsm == null || fsm.Fsm == null)
            {
                return;
            }

            for (int i = 0; i < ActivatedBoolNames.Length; i++)
            {
                try
                {
                    FsmBool value = fsm.FsmVariables.FindFsmBool(ActivatedBoolNames[i]);
                    if (value != null)
                    {
                        value.Value = true;
                    }
                }
                catch
                {
                }
            }

            FsmState[] states = fsm.Fsm.States;
            if (states == null || states.Length == 0)
            {
                return;
            }

            string identity = ((fsm.FsmName ?? string.Empty) + " " + (fsm.gameObject.name ?? string.Empty)).ToLowerInvariant();
            bool isLever = identity.Contains("lever") || identity.Contains("switch");
            string[] preferredStates = isLever ? LeverStateNames : OpenStateNames;
            for (int i = 0; i < preferredStates.Length; i++)
            {
                string stateName = preferredStates[i];
                if (!states.Any(state => state != null &&
                    string.Equals(state.Name, stateName, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                try
                {
                    fsm.SetState(stateName);
                }
                catch
                {
                }
                return;
            }
        }
    }
}