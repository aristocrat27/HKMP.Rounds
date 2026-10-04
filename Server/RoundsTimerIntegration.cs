using System;
using System.Reflection;
using HKMP.Rounds.Settings;

namespace HKMP.Rounds.Server
{
    internal sealed class RoundsTimerIntegration
    {
        private const string ApiTypeName =
            "HKMP.Timer.TimerIntegrationApi, HKMP.Timer";

        private readonly RoundManager _roundManager;
        private readonly GlobalSettingsStore _settings;

        private Type _apiType;
        private MethodInfo _setExpirationHandler;
        private MethodInfo _startRound;
        private MethodInfo _endRound;

        private bool _handlerBound;
        private uint _activeRoundId;

        public RoundsTimerIntegration(
            RoundManager roundManager,
            GlobalSettingsStore settings)
        {
            _roundManager =
                roundManager;

            _settings =
                settings;
        }

        public bool StartRound(
            uint roundId)
        {
            if (!_settings.TimerIntegrationEnabled)
            {
                return true;
            }

            if (roundId == 0)
            {
                return false;
            }

            if (!ResolveApi() ||
                _startRound == null)
            {
                Modding.Logger.Log(
                    "[HKMP.Rounds] HKMP.Timer integration is enabled, but HKMP.Timer is unavailable.");
                return false;
            }

            BindExpirationHandler();

            try
            {
                object result =
                    _startRound.Invoke(
                        null,
                        new object[]
                        {
                            roundId
                        });

                bool started =
                    result is bool &&
                    (bool)result;

                if (started)
                {
                    _activeRoundId =
                        roundId;

                    return true;
                }

                Modding.Logger.Log(
                    "[HKMP.Rounds] HKMP.Timer did not start for round " +
                    roundId +
                    ".");

                return false;
            }
            catch (Exception exception)
            {
                Modding.Logger.LogError(
                    "[HKMP.Rounds] Failed to start HKMP.Timer for round " +
                    roundId +
                    ". Exception=" +
                    exception);

                return false;
            }
        }

        public bool EndRound(
            uint roundId)
        {
            if (roundId == 0 ||
                _activeRoundId != roundId)
            {
                return true;
            }

            _activeRoundId = 0;

            if (!ResolveApi() ||
                _endRound == null)
            {
                Modding.Logger.Log(
                    "[HKMP.Rounds] HKMP.Timer was not available while ending round " +
                    roundId +
                    ".");

                return false;
            }

            try
            {
                object result =
                    _endRound.Invoke(
                        null,
                        new object[]
                        {
                            roundId
                        });

                return
                    !(result is bool) ||
                    (bool)result;
            }
            catch (Exception exception)
            {
                Modding.Logger.LogError(
                    "[HKMP.Rounds] Failed to stop HKMP.Timer for round " +
                    roundId +
                    ". Exception=" +
                    exception);

                return false;
            }
        }

        private void BindExpirationHandler()
        {
            if (_handlerBound ||
                _setExpirationHandler == null)
            {
                return;
            }

            try
            {
                _setExpirationHandler.Invoke(
                    null,
                    new object[]
                    {
                        new Action<uint>(
                            OnTimerExpired)
                    });

                _handlerBound = true;
            }
            catch (Exception exception)
            {
                Modding.Logger.LogError(
                    "[HKMP.Rounds] Failed to bind HKMP.Timer expiration handler. Exception=" +
                    exception);
            }
        }

        private void OnTimerExpired(
            uint roundId)
        {
            if (roundId == 0 ||
                _activeRoundId != roundId ||
                _roundManager == null)
            {
                return;
            }

            _activeRoundId = 0;

            _roundManager.EndRoundByTimer(
                roundId);
        }

        private bool ResolveApi()
        {
            if (_apiType != null)
            {
                return
                    _startRound != null &&
                    _endRound != null &&
                    _setExpirationHandler != null;
            }

            Type apiType =
                Type.GetType(
                    ApiTypeName,
                    false);

            if (apiType == null)
            {
                try
                {
                    Assembly timerAssembly =
                        Assembly.Load(
                            "HKMP.Timer");

                    if (timerAssembly != null)
                    {
                        apiType =
                            timerAssembly.GetType(
                                "HKMP.Timer.TimerIntegrationApi",
                                false);
                    }
                }
                catch
                {
                }
            }

            if (apiType == null)
            {
                return false;
            }

            _apiType =
                apiType;

            _setExpirationHandler =
                apiType.GetMethod(
                    "SetRoundExpirationHandler",
                    BindingFlags.Public |
                    BindingFlags.Static,
                    null,
                    new Type[]
                    {
                        typeof(Action<uint>)
                    },
                    null);

            _startRound =
                apiType.GetMethod(
                    "StartRound",
                    BindingFlags.Public |
                    BindingFlags.Static,
                    null,
                    new Type[]
                    {
                        typeof(uint)
                    },
                    null);

            _endRound =
                apiType.GetMethod(
                    "EndRound",
                    BindingFlags.Public |
                    BindingFlags.Static,
                    null,
                    new Type[]
                    {
                        typeof(uint)
                    },
                    null);

            return
                _setExpirationHandler != null &&
                _startRound != null &&
                _endRound != null;
        }
    }
}
