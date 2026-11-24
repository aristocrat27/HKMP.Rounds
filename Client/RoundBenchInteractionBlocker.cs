using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HKMP.Rounds.Client
{
    internal sealed class RoundBenchInteractionBlocker : MonoBehaviour
    {
        private readonly Dictionary<FsmState, FsmTransition[]> _originalTransitions =
            new Dictionary<FsmState, FsmTransition[]>();
        private bool _scanCompletedForRound;
        private bool _hookInstalled;
        private bool _wasRoundActive;

        public void Initialize()
        {
            if (_hookInstalled)
            {
                return;
            }

            On.PlayMakerFSM.OnEnable += OnPlayMakerFsmEnable;
            _hookInstalled = true;
        }

        private void Update()
        {
            bool roundActive = RoundClientManager.IsRoundActive;
            if (!roundActive)
            {
                _wasRoundActive = false;
                _scanCompletedForRound = false;
                RestoreBenchTransitions();
                return;
            }

            if (!_wasRoundActive)
            {
                _wasRoundActive = true;
                _scanCompletedForRound = false;
            }

            if (_scanCompletedForRound)
            {
                return;
            }

            _scanCompletedForRound = true;
            UnityEngine.SceneManagement.Scene[] scenes = UnityEngine.SceneManagement.SceneManager.GetAllScenes();
            for (int sceneIndex = 0; sceneIndex < scenes.Length; sceneIndex++)
            {
                UnityEngine.SceneManagement.Scene scene = scenes[sceneIndex];
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    continue;
                }

                GameObject[] roots = scene.GetRootGameObjects();
                for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
                {
                    PlayMakerFSM[] fsms = roots[rootIndex].GetComponentsInChildren<PlayMakerFSM>(true);
                    for (int fsmIndex = 0; fsmIndex < fsms.Length; fsmIndex++)
                    {
                        DisableBenchEntry(fsms[fsmIndex]);
                    }
                }
            }
        }

        private void OnPlayMakerFsmEnable(On.PlayMakerFSM.orig_OnEnable orig, PlayMakerFSM fsm)
        {
            orig(fsm);
            if (RoundClientManager.IsRoundActive)
            {
                DisableBenchEntry(fsm);
            }
        }

        private void DisableBenchEntry(PlayMakerFSM fsm)
        {
            if (fsm == null || !fsm.enabled ||
                !string.Equals(fsm.FsmName, "Bench Control", StringComparison.Ordinal))
            {
                return;
            }

            FsmState[] states = fsm.FsmStates;
            if (states == null)
            {
                return;
            }

            for (int i = 0; i < states.Length; i++)
            {
                FsmState state = states[i];
                if (state == null)
                {
                    continue;
                }

                FsmTransition[] transitions = state.Transitions;
                if (transitions == null || transitions.Length == 0)
                {
                    continue;
                }

                List<FsmTransition> allowed = new List<FsmTransition>(transitions.Length);
                bool removedBenchEntry = false;
                for (int transitionIndex = 0; transitionIndex < transitions.Length; transitionIndex++)
                {
                    FsmTransition transition = transitions[transitionIndex];
                    bool enteringBenchRange = transition != null &&
                        string.Equals(state.Name, "Idle", StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(transition.EventName, "IN RANGE", StringComparison.OrdinalIgnoreCase);
                    bool entersRestBurst = transition != null &&
                        (string.Equals(transition.ToState, "Rest Burst", StringComparison.OrdinalIgnoreCase) ||
                         transition.ToFsmState != null &&
                         string.Equals(transition.ToFsmState.Name, "Rest Burst", StringComparison.OrdinalIgnoreCase));
                    bool enteringBenchRest = transition != null &&
                        !string.Equals(state.Name, "Rest Burst", StringComparison.OrdinalIgnoreCase) &&
                        entersRestBurst;

                    if (enteringBenchRange || enteringBenchRest)
                    {
                        removedBenchEntry = true;
                    }
                    else
                    {
                        allowed.Add(transition);
                    }
                }

                if (!removedBenchEntry)
                {
                    continue;
                }

                if (!_originalTransitions.ContainsKey(state))
                {
                    _originalTransitions.Add(state, transitions);
                }

                state.Transitions = allowed.ToArray();
            }

            if (string.Equals(fsm.ActiveStateName, "In Range", StringComparison.OrdinalIgnoreCase))
            {
                fsm.SendEvent("OUT OF RANGE");
            }
        }

        private void RestoreBenchTransitions()
        {
            if (_originalTransitions.Count == 0)
            {
                return;
            }

            foreach (KeyValuePair<FsmState, FsmTransition[]> pair in _originalTransitions)
            {
                if (pair.Key != null)
                {
                    pair.Key.Transitions = pair.Value ?? Array.Empty<FsmTransition>();
                }
            }

            _originalTransitions.Clear();
        }

        private void OnDestroy()
        {
            RestoreBenchTransitions();
            if (_hookInstalled)
            {
                On.PlayMakerFSM.OnEnable -= OnPlayMakerFsmEnable;
                _hookInstalled = false;
            }
        }
    }
}
