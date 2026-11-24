using Hkmp.Api.Client;
using HKMP.Rounds.Client.Combat;
using UnityEngine;

namespace HKMP.Rounds.Client
{
    internal sealed class RoundsClientAddon : ClientAddon
    {
        private IClientApi _clientApi;
        private ClientNetManager _network;
        private RoundsSettingsUI _settingsUI;
        private RoundHudStatus _hudStatus;

        protected override string Name
        {
            get { return RoundsConstants.Name; }
        }

        protected override string Version
        {
            get { return RoundsConstants.Version; }
        }

        public override bool NeedsNetwork
        {
            get { return true; }
        }

        public override void Initialize(IClientApi clientApi)
        {
            _clientApi = clientApi;
            if (_clientApi != null && _clientApi.ClientManager != null)
            {
                _clientApi.ClientManager.ConnectEvent -= OnClientConnected;
                _clientApi.ClientManager.ConnectEvent += OnClientConnected;
                _clientApi.ClientManager.DisconnectEvent -= OnClientDisconnected;
                _clientApi.ClientManager.DisconnectEvent += OnClientDisconnected;
            }

            _network = new ClientNetManager(this, clientApi.NetClient);
            PhantomMapDiagnostics.Initialize();
            GameObject freezeObject = new GameObject("[HKMP.Rounds] Game Freeze Controller");
            Object.DontDestroyOnLoad(freezeObject);
            freezeObject.AddComponent<GameFreezeController>().Initialize();
            GameObject benchBlockerObject = new GameObject("[HKMP.Rounds] Round Bench Interaction Blocker");
            Object.DontDestroyOnLoad(benchBlockerObject);
            benchBlockerObject.AddComponent<RoundBenchInteractionBlocker>().Initialize();

            GameObject phantomMapObject = new GameObject("[HKMP.Rounds] Phantom Map Controller");
            Object.DontDestroyOnLoad(phantomMapObject);
            phantomMapObject.AddComponent<PhantomMapClientController>().Initialize(clientApi, _network);

            GameObject presetMapObject = new GameObject("[HKMP.Rounds] Preset Map Travel Controller");
            Object.DontDestroyOnLoad(presetMapObject);
            presetMapObject.AddComponent<MapPresetTravelController>().Initialize(_network);

            GameObject benchStateObject = new GameObject("[HKMP.Rounds] Ready Bench State");
            Object.DontDestroyOnLoad(benchStateObject);
            benchStateObject.AddComponent<ReadyBenchStateReporter>().Initialize(
                clientApi.NetClient,
                _network);

            PvpModeClientController.Initialize(_network.SendManaSpent);
            VignettePolicyController.Initialize(clientApi, _network);

            RoundPreparation.Initialize();
            RoundRoomExitBlocker.Initialize();
            RoundEnemyController.Initialize();
            DebugShadeController.Initialize();

            if (HKMPRounds.GlobalSettings != null)
            {
                RoundEnemyController.SetPersistentDisabled(
                    HKMPRounds.GlobalSettings.DisableEnemies);

                DebugShadeController.SetPersistentDisabled(
                    HKMPRounds.GlobalSettings.DebugDisableShade);
            }

            LastAttackerTracker.Initialize();
            ClientDeathTracker.Initialize(_network);
            ClientHealthTracker.Initialize(_network);
            RoundClientManager.Clear();

            GameObject hudStatusObject = new GameObject("[HKMP.Rounds] HUD Status");
            Object.DontDestroyOnLoad(hudStatusObject);
            _hudStatus = hudStatusObject.AddComponent<RoundHudStatus>();
            _hudStatus.Initialize(_network);

            BenchPolicyController.Initialize(clientApi, _network);
            if (clientApi != null && clientApi.NetClient != null && clientApi.NetClient.IsConnected)
            {
                PvpModeClientController.ResumeCharmPolicy();
            }

            GameObject settingsObject = new GameObject("[HKMP.Rounds] Settings UI");
            Object.DontDestroyOnLoad(settingsObject);
            _settingsUI = settingsObject.AddComponent<RoundsSettingsUI>();
            _settingsUI.Initialize(
                clientApi,
                HKMPRounds.GlobalSettings,
                _network,
                HKMPRounds.PreloadedObjects);

            _network.RequestPvpMode();
        }

        private void OnClientConnected()
        {
            PvpModeClientController.ResumeCharmPolicy();
            RoundClientManager.Clear();
            RoundRoomExitBlocker.Release();
            _network?.RequestPvpMode();
        }

        private void OnClientDisconnected()
        {
            PvpModeClientController.SuspendCharmPolicy();
            GameFreezeController.SetFrozen(false);
            RoundClientManager.Clear();
            RoundRoomExitBlocker.Release();
            RoundRoomExitBlocker.ClearServerLockedScenes();
        }
    }
}
