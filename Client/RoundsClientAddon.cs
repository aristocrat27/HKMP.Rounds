using Hkmp.Api.Client;

using HKMP.Rounds.Client.Combat;

namespace HKMP.Rounds.Client
{
    internal sealed class RoundsClientAddon : ClientAddon
    {
        private ClientNetManager _network;

        protected override string Name
        {
            get
            {
                return RoundsConstants.Name;
            }
        }

        protected override string Version
        {
            get
            {
                return RoundsConstants.Version;
            }
        }

        public override bool NeedsNetwork
        {
            get
            {
                return true;
            }
        }

        public override void Initialize(
            IClientApi clientApi)
        {
            _network = new ClientNetManager(
                this,
                clientApi.NetClient
            );

            LastAttackerTracker.Initialize();

            ClientDeathTracker.Initialize(
                _network
            );

            RoundClientManager.Clear();

            Modding.Logger.Log(
                "[HKMP.Rounds] Client addon initialization completed."
            );
        }

        public ClientNetManager Network
        {
            get
            {
                return _network;
            }
        }
    }
}