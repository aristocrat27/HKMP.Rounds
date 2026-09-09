using System.Collections.Generic;
using UnityEngine;

using Modding;
using Hkmp.Api.Client;
using Hkmp.Api.Server;

using HKMP.Rounds.Client;
using HKMP.Rounds.Server;

namespace HKMP.Rounds
{
    public sealed class HKMPRounds : Mod
    {
        public override string GetVersion()
        {
            return RoundsConstants.Version;
        }

        public override void Initialize(
            Dictionary<string, Dictionary<string, GameObject>> preloadedObjects)
        {
            Modding.Logger.Log(
                "Initializing HKMP.Rounds " +
                RoundsConstants.Version
            );

            // Регистрируем серверную часть аддона.
            ServerAddon.RegisterAddon(new RoundsServerAddon());

            // Регистрируем клиентскую часть аддона.
            ClientAddon.RegisterAddon(new RoundsClientAddon());

            Modding.Logger.Log("HKMP.Rounds initialized.");
        }
    }
}