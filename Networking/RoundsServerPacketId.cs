using Hkmp.Api.Command.Server;
using HKMP.Rounds.Server.Commands;
using HKMP.Rounds.Server.Stats;
using System;

namespace HKMP.Rounds.Networking
{
    internal enum RoundsServerPacketId : byte
    {
        Prepared = 0,
        DeathReport = 1,
        HealthReport = 2
    }
}