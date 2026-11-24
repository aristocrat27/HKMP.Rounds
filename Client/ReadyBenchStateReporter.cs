using Hkmp.Api.Client.Networking;
using Modding;
using UnityEngine;
using GlobalEnums;

namespace HKMP.Rounds.Client
{
    internal sealed class ReadyBenchStateReporter : MonoBehaviour
    {
        private const float HeartbeatInterval = 1f;
        private static bool _readyReleaseRequired;
        private static bool _waitingForRoundEnd;
        private static bool _observedAliveBench;
        private static bool _observedReturnToBenchPending;

        private INetClient _netClient;
        private ClientNetManager _network;
        private bool _hasReported;
        private bool _lastAtBench;
        private bool _lastReturnToBenchPending;
        private bool _lastHeroAlive;
        private float _nextHeartbeatAt;

        public static bool CanToggleReady
        {
            get { return !_readyReleaseRequired; }
        }

        public static void NotifyRoundStarted()
        {
            _readyReleaseRequired = true;
            _waitingForRoundEnd = true;
            _observedAliveBench = false;
            _observedReturnToBenchPending = false;
        }

        public static void NotifyRoundEnded()
        {
            _readyReleaseRequired = true;
            _waitingForRoundEnd = false;
            _observedAliveBench = false;
            _observedReturnToBenchPending = false;
        }

        public static void ResetReadyGate()
        {
            _readyReleaseRequired = false;
            _waitingForRoundEnd = false;
            _observedAliveBench = false;
            _observedReturnToBenchPending = false;
        }

        public void Initialize(INetClient netClient, ClientNetManager network)
        {
            _netClient = netClient;
            _network = network;
            _hasReported = false;
            _nextHeartbeatAt = 0f;
            ModHooks.BeforePlayerDeadHook -= OnBeforePlayerDead;
            ModHooks.BeforePlayerDeadHook += OnBeforePlayerDead;
        }

        private void OnBeforePlayerDead()
        {
            if (_netClient == null || _network == null || !_netClient.IsConnected)
            {
                return;
            }

            _readyReleaseRequired = true;
            _waitingForRoundEnd = RoundClientManager.IsRoundActive;
            _observedAliveBench = false;
            _observedReturnToBenchPending = false;
            bool atBench = PlayerData.instance != null && PlayerData.instance.atBench;
            _lastAtBench = atBench;
            _lastReturnToBenchPending = RoundPreparation.IsReturnToBenchPending;
            _lastHeroAlive = false;
            _hasReported = true;
            _nextHeartbeatAt = Time.unscaledTime + HeartbeatInterval;
            _network.SendBenchState(atBench, _lastReturnToBenchPending, false, true);
        }

        private void Update()
        {
            if (_netClient == null || _network == null || !_netClient.IsConnected)
            {
                _hasReported = false;
                _nextHeartbeatAt = 0f;
                ResetReadyGate();
                return;
            }

            PlayerData playerData = PlayerData.instance;
            if (playerData == null)
            {
                return;
            }

            HeroController hero = HeroController.instance;
            GameManager manager = GameManager.instance;
            bool heroAlive = hero != null &&
                             hero.cState != null &&
                             !hero.cState.dead &&
                             manager != null &&
                             manager.gameState == GameState.PLAYING &&
                             !manager.IsInSceneTransition;
            bool atBench = playerData.atBench;
            bool returnToBenchPending = RoundPreparation.IsReturnToBenchPending;

            if (!_waitingForRoundEnd && _readyReleaseRequired)
            {
                if (returnToBenchPending)
                {
                    _observedReturnToBenchPending = true;
                }
                else if (heroAlive && _observedReturnToBenchPending && !atBench)
                {
                    _readyReleaseRequired = false;
                    _observedAliveBench = false;
                    _observedReturnToBenchPending = false;
                }
                else if (heroAlive && atBench)
                {
                    _observedAliveBench = true;
                }
                else if (heroAlive && _observedAliveBench && !atBench)
                {
                    _readyReleaseRequired = false;
                    _observedAliveBench = false;
                }
            }

            bool stateChanged = !_hasReported ||
                                atBench != _lastAtBench ||
                                returnToBenchPending != _lastReturnToBenchPending ||
                                heroAlive != _lastHeroAlive;
            bool heartbeatDue = Time.unscaledTime >= _nextHeartbeatAt;
            if (!stateChanged && !heartbeatDue)
            {
                return;
            }

            _lastAtBench = atBench;
            _lastReturnToBenchPending = returnToBenchPending;
            _lastHeroAlive = heroAlive;
            _hasReported = true;
            _nextHeartbeatAt = Time.unscaledTime + HeartbeatInterval;
            _network.SendBenchState(atBench, returnToBenchPending, heroAlive, false);
        }

        private void OnDestroy()
        {
            ModHooks.BeforePlayerDeadHook -= OnBeforePlayerDead;
        }
    }
}
