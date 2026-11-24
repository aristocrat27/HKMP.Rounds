using System;
using System.Collections.Generic;
using GlobalEnums;
using HutongGames.PlayMaker;
using Modding;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HKMP.Rounds.Client
{
    internal static class MantisVillageProgressController
    {
        private static readonly Dictionary<string, string[]> PersistentStates =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                { "Fungus2_31", new[] { "Mantis Lever" } },
                {
                    "Fungus2_15",
                    new[]
                    {
                        "Mantis Lever",
                        "Mantis Lever (1)",
                        "Mantis Lever (2)",
                        "Mantis Lever (3)",
                        "Mantis Lever (4)"
                    }
                }
            };

        private static bool _hasOriginalDefeatedState;
        private static bool _originalDefeatedState;

        internal static void CaptureOriginalProgress()
        {
            if (_hasOriginalDefeatedState)
            {
                return;
            }

            PlayerData data = PlayerData.instance;
            if (data == null)
            {
                return;
            }

            _originalDefeatedState = data.GetBool("defeatedMantisLords");
            _hasOriginalDefeatedState = true;
        }

        internal static void RestoreOriginalProgress()
        {
            if (!_hasOriginalDefeatedState)
            {
                return;
            }

            PlayerData data = PlayerData.instance;
            GameManager manager = GameManager.instance;
            if (data != null)
            {
                try
                {
                    data.SetBool("defeatedMantisLords", _originalDefeatedState);
                }
                catch (Exception exception)
                {
                    Modding.Logger.LogError("[HKMP.Rounds] Could not restore the original Mantis Lords progress: " + exception);
                }
            }

            if (manager != null)
            {
                try
                {
                    manager.SetPlayerDataBool("defeatedMantisLords", _originalDefeatedState);
                }
                catch (Exception exception)
                {
                    Modding.Logger.LogError("[HKMP.Rounds] Could not restore the GameManager Mantis Lords progress: " + exception);
                }
            }

            _hasOriginalDefeatedState = false;
            _originalDefeatedState = false;
        }

        internal static void ApplyProgressToScene(string sceneName, Scene targetScene)
        {
            if (!targetScene.IsValid() || !targetScene.isLoaded || string.IsNullOrWhiteSpace(sceneName))
            {
                return;
            }

            string[] states;
            if (!PersistentStates.TryGetValue(sceneName, out states))
            {
                return;
            }

            if (string.Equals(sceneName, "Fungus2_15", StringComparison.OrdinalIgnoreCase))
            {
                CaptureOriginalProgress();
                PlayerData data = PlayerData.instance;
                GameManager manager = GameManager.instance;
                try
                {
                    if (data != null)
                    {
                        data.SetBool("defeatedMantisLords", true);
                    }
                    if (manager != null)
                    {
                        manager.SetPlayerDataBool("defeatedMantisLords", true);
                    }
                }
                catch (Exception exception)
                {
                    Modding.Logger.LogError("[HKMP.Rounds] Could not apply the temporary Mantis Lords progress to the phantom room: " + exception);
                }
            }

            GameManager gameManager = GameManager.instance;
            HashSet<string> stateIds = new HashSet<string>(states, StringComparer.OrdinalIgnoreCase);
            GameObject[] roots = targetScene.GetRootGameObjects();
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
                    if (item == null || item.persistentBoolData == null ||
                        !stateIds.Contains(item.persistentBoolData.id ?? string.Empty))
                    {
                        continue;
                    }

                    item.persistentBoolData.sceneName = sceneName;
                    item.persistentBoolData.activated = true;
                    item.persistentBoolData.semiPersistent = false;
                }

                PlayMakerFSM[] fsms = root.GetComponentsInChildren<PlayMakerFSM>(true);
                for (int j = 0; j < fsms.Length; j++)
                {
                    PlayMakerFSM fsm = fsms[j];
                    if (fsm == null || fsm.gameObject == null ||
                        !string.Equals(fsm.FsmName, "Switch Control", StringComparison.OrdinalIgnoreCase) ||
                        fsm.gameObject.name.IndexOf("Mantis Lever", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    try
                    {
                        FsmBool activated = fsm.FsmVariables.FindFsmBool("Activated");
                        if (activated != null)
                        {
                            activated.Value = true;
                        }
                        fsm.SetState("Activated");
                    }
                    catch (Exception exception)
                    {
                        Modding.Logger.LogError("[HKMP.Rounds] Could not activate phantom Mantis lever " + fsm.gameObject.name + ": " + exception);
                    }
                }
            }

            PhantomMapDiagnostics.Record("mantis-progress.phantom-scene.applied",
                "Scene=" + sceneName + " CloneScene=" + targetScene.name);
        }
    }
}
