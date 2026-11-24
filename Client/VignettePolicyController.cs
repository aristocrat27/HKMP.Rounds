using Hkmp.Api.Client;
using HKMP.Rounds.Networking.Packets;
using UnityEngine;

namespace HKMP.Rounds.Client
{
    internal sealed class VignettePolicyController : MonoBehaviour
    {
        private static VignettePolicyController _instance;

        private IClientApi _clientApi;
        private ClientNetManager _network;
        private HeroController _trackedHero;
        private bool _hasOriginalState;
        private bool _originalActive;
        private bool _hasServerPolicy;
        private bool _vignetteEnabled;

        public static void Initialize(IClientApi clientApi, ClientNetManager network)
        {
            if (_instance == null)
            {
                GameObject controllerObject = new GameObject("[HKMP.Rounds] Vignette Policy");
                Object.DontDestroyOnLoad(controllerObject);
                _instance = controllerObject.AddComponent<VignettePolicyController>();
            }

            _instance.Configure(clientApi, network);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
        }

        private void Configure(IClientApi clientApi, ClientNetManager network)
        {
            if (_network != null)
            {
                _network.RoundSettingsReceived -= OnRoundSettingsReceived;
            }

            if (_clientApi != null && _clientApi.ClientManager != null)
            {
                _clientApi.ClientManager.DisconnectEvent -= OnClientDisconnected;
            }

            _clientApi = clientApi;
            _network = network;

            if (_network != null)
            {
                _network.RoundSettingsReceived -= OnRoundSettingsReceived;
                _network.RoundSettingsReceived += OnRoundSettingsReceived;
            }

            if (_clientApi != null && _clientApi.ClientManager != null)
            {
                _clientApi.ClientManager.DisconnectEvent -= OnClientDisconnected;
                _clientApi.ClientManager.DisconnectEvent += OnClientDisconnected;
            }
        }

        private void OnRoundSettingsReceived(RoundSettingsPacket packet)
        {
            if (packet == null)
            {
                return;
            }

            _hasServerPolicy = true;
            _vignetteEnabled = packet.VignetteEnabled;
            ApplyPolicy();
        }

        private void Update()
        {
            ApplyPolicy();
        }

        private void ApplyPolicy()
        {
            HeroController hero = HeroController.instance;
            if (hero != _trackedHero)
            {
                RestoreTrackedHero();
                _trackedHero = hero;
                _hasOriginalState = false;
            }

            if (_trackedHero == null || _trackedHero.vignette == null)
            {
                return;
            }

            if (!_hasOriginalState)
            {
                _originalActive = _trackedHero.vignette.gameObject.activeSelf;
                _hasOriginalState = true;
            }

            if (_hasServerPolicy &&
                _trackedHero.vignette.gameObject.activeSelf != _vignetteEnabled)
            {
                _trackedHero.vignette.gameObject.SetActive(_vignetteEnabled);
            }
        }

        private void OnClientDisconnected()
        {
            _hasServerPolicy = false;
            RestoreTrackedHero();
        }

        private void RestoreTrackedHero()
        {
            if (_trackedHero != null &&
                _hasOriginalState &&
                _trackedHero.vignette != null)
            {
                _trackedHero.vignette.gameObject.SetActive(_originalActive);
            }

            _trackedHero = null;
            _hasOriginalState = false;
        }

        private void OnDestroy()
        {
            if (_network != null)
            {
                _network.RoundSettingsReceived -= OnRoundSettingsReceived;
            }

            if (_clientApi != null && _clientApi.ClientManager != null)
            {
                _clientApi.ClientManager.DisconnectEvent -= OnClientDisconnected;
            }

            RestoreTrackedHero();

            if (_instance == this)
            {
                _instance = null;
            }
        }
    }
}
