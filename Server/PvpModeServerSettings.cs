using System;
using Hkmp.Api.Server;
using Hkmp.Game.Settings;
using HKMP.Rounds.Settings;

namespace HKMP.Rounds.Server
{
    internal sealed class PvpModeServerSettings
    {
        private readonly IServerApi _serverApi;
        private DamageSnapshot _baseSnapshot;
        private bool _overrideActive;
        private bool _roundActive;
        private bool _disablePvpBeforeRound;
        private PvpMode _mode = PvpMode.Quick;
        private static PvpModeServerSettings _activeInstance;

        private sealed class DamageSnapshot
        {
            public bool IsPvpEnabled;
            public bool IsBodyDamageEnabled;
            public byte NailDamage;
            public byte GreatSlashDamage;
            public byte DashSlashDamage;
            public byte CycloneSlashDamage;
            public byte VengefulSpiritDamage;
            public byte ShadeSoulDamage;
            public byte DesolateDiveDamage;
            public byte DescendingDarkDamage;
            public byte HowlingWraithDamage;
            public byte AbyssShriekDamage;
            public byte GrubberflyElegyDamage;
            public byte SporeShroomDamage;
            public byte SporeDungShroomDamage;
            public byte ThornOfAgonyDamage;
            public byte SharpShadowDamage;
        }

        public static bool IsActive
        {
            get { return _activeInstance != null && _activeInstance._overrideActive; }
        }

        public PvpModeServerSettings(IServerApi serverApi)
        {
            _serverApi = serverApi ?? throw new ArgumentNullException(nameof(serverApi));
            ServerSettings settings = GetMutableSettings(out string message);
            if (settings != null)
            {
                _baseSnapshot = Capture(settings);
            }
            _activeInstance = this;
        }

        public bool Configure(
            PvpMode mode,
            bool roundActive,
            bool disablePvpBeforeRound,
            out string message)
        {
            if (!IsValidMode(mode))
            {
                message = "Invalid PvP mode.";
                return false;
            }

            return ChangeState(mode, roundActive, disablePvpBeforeRound, out message);
        }

        public bool SetMode(PvpMode mode, out string message)
        {
            if (!IsValidMode(mode))
            {
                message = "Invalid PvP mode.";
                return false;
            }

            return ChangeState(mode, _roundActive, _disablePvpBeforeRound, out message);
        }

        public bool SetRoundActive(bool active, out string message)
        {
            return ChangeState(_mode, active, _disablePvpBeforeRound, out message);
        }

        public bool SetDisablePvpBeforeRound(bool enabled, out string message)
        {
            return ChangeState(_mode, _roundActive, enabled, out message);
        }

        public static bool PreparePersistentServerSettings(object serverSettingsObject)
        {
            if (!IsActive || serverSettingsObject == null || _activeInstance._baseSnapshot == null)
            {
                return false;
            }

            ServerSettings settings = serverSettingsObject as ServerSettings;
            if (settings == null)
            {
                return false;
            }

            try
            {
                ApplySnapshot(settings, _activeInstance._baseSnapshot);
                return true;
            }
            catch (Exception exception)
            {
                Modding.Logger.LogError("[HKMP.Rounds] Could not prepare the original HKMP damage settings for persistence: " + exception);
                return false;
            }
        }

        private bool ChangeState(
            PvpMode mode,
            bool roundActive,
            bool disablePvpBeforeRound,
            out string message)
        {
            PvpMode previousMode = _mode;
            bool previousRoundActive = _roundActive;
            bool previousDisablePvp = _disablePvpBeforeRound;
            bool previousOverride = _overrideActive;

            _mode = mode;
            _roundActive = roundActive;
            _disablePvpBeforeRound = disablePvpBeforeRound;

            if (ApplyCurrentState(out message))
            {
                return true;
            }

            _mode = previousMode;
            _roundActive = previousRoundActive;
            _disablePvpBeforeRound = previousDisablePvp;
            _overrideActive = previousOverride;

            string rollbackMessage;
            ApplyCurrentState(out rollbackMessage);
            return false;
        }

        private bool ApplyCurrentState(out string message)
        {
            message = string.Empty;
            ServerSettings liveSettings = GetMutableSettings(out message);
            if (liveSettings == null)
            {
                return false;
            }

            DamageSnapshot currentSnapshot = Capture(liveSettings);
            if (!_overrideActive || _baseSnapshot == null)
            {
                _baseSnapshot = currentSnapshot;
            }
            ServerSettings desiredSettings = new ServerSettings();
            ApplySnapshot(desiredSettings, currentSnapshot);

            ApplySnapshot(desiredSettings, _baseSnapshot);
            bool blockPvp = _disablePvpBeforeRound && !_roundActive;
            desiredSettings.IsPvpEnabled = !blockPvp;
            desiredSettings.IsBodyDamageEnabled = false;

            if (blockPvp)
            {
                SetAllDamage(desiredSettings, 0);
            }
            else if (_mode == PvpMode.OneShot)
            {
                ApplyOneShot(desiredSettings);
            }
            ServerSettings rollbackSettings = new ServerSettings();
            ApplySnapshot(rollbackSettings, currentSnapshot);

            try
            {
                _serverApi.ServerManager.ApplyServerSettings(desiredSettings);
                _overrideActive = true;
                return true;
            }
            catch (Exception exception)
            {
                message = "Could not apply PvP server settings: " + exception.Message;
                try
                {
                    _serverApi.ServerManager.ApplyServerSettings(rollbackSettings);
                }
                catch (Exception rollbackException)
                {
                    message += " Rollback failed: " + rollbackException.Message;
                }
                return false;
            }
        }


        private ServerSettings GetMutableSettings(out string message)
        {
            message = string.Empty;
            if (_serverApi == null || _serverApi.ServerManager == null)
            {
                message = "HKMP server manager is unavailable.";
                return null;
            }

            IServerSettings interfaceSettings = _serverApi.ServerManager.ServerSettings;
            ServerSettings settings = interfaceSettings as ServerSettings;
            if (settings == null)
            {
                message = "HKMP ServerSettings is not the concrete ServerSettings type.";
            }

            return settings;
        }

        private static bool IsValidMode(PvpMode mode)
        {
            return mode == PvpMode.Quick || mode == PvpMode.Long || mode == PvpMode.OneShot;
        }

        private static DamageSnapshot Capture(ServerSettings settings)
        {
            return new DamageSnapshot
            {
                IsPvpEnabled = settings.IsPvpEnabled,
                IsBodyDamageEnabled = settings.IsBodyDamageEnabled,
                NailDamage = settings.NailDamage,
                GreatSlashDamage = settings.GreatSlashDamage,
                DashSlashDamage = settings.DashSlashDamage,
                CycloneSlashDamage = settings.CycloneSlashDamage,
                VengefulSpiritDamage = settings.VengefulSpiritDamage,
                ShadeSoulDamage = settings.ShadeSoulDamage,
                DesolateDiveDamage = settings.DesolateDiveDamage,
                DescendingDarkDamage = settings.DescendingDarkDamage,
                HowlingWraithDamage = settings.HowlingWraithDamage,
                AbyssShriekDamage = settings.AbyssShriekDamage,
                GrubberflyElegyDamage = settings.GrubberflyElegyDamage,
                SporeShroomDamage = settings.SporeShroomDamage,
                SporeDungShroomDamage = settings.SporeDungShroomDamage,
                ThornOfAgonyDamage = settings.ThornOfAgonyDamage,
                SharpShadowDamage = settings.SharpShadowDamage
            };
        }

        private static void ApplySnapshot(ServerSettings settings, DamageSnapshot snapshot)
        {
            if (settings == null || snapshot == null)
            {
                return;
            }

            settings.IsPvpEnabled = snapshot.IsPvpEnabled;
            settings.IsBodyDamageEnabled = snapshot.IsBodyDamageEnabled;
            settings.NailDamage = snapshot.NailDamage;
            settings.GreatSlashDamage = snapshot.GreatSlashDamage;
            settings.DashSlashDamage = snapshot.DashSlashDamage;
            settings.CycloneSlashDamage = snapshot.CycloneSlashDamage;
            settings.VengefulSpiritDamage = snapshot.VengefulSpiritDamage;
            settings.ShadeSoulDamage = snapshot.ShadeSoulDamage;
            settings.DesolateDiveDamage = snapshot.DesolateDiveDamage;
            settings.DescendingDarkDamage = snapshot.DescendingDarkDamage;
            settings.HowlingWraithDamage = snapshot.HowlingWraithDamage;
            settings.AbyssShriekDamage = snapshot.AbyssShriekDamage;
            settings.GrubberflyElegyDamage = snapshot.GrubberflyElegyDamage;
            settings.SporeShroomDamage = snapshot.SporeShroomDamage;
            settings.SporeDungShroomDamage = snapshot.SporeDungShroomDamage;
            settings.ThornOfAgonyDamage = snapshot.ThornOfAgonyDamage;
            settings.SharpShadowDamage = snapshot.SharpShadowDamage;
        }

        private static void ApplyOneShot(ServerSettings settings)
        {
            settings.NailDamage = 0;
            settings.GreatSlashDamage = 2;
            settings.DashSlashDamage = 2;
            settings.CycloneSlashDamage = 0;
            settings.VengefulSpiritDamage = 1;
            settings.ShadeSoulDamage = 2;
            settings.DesolateDiveDamage = 1;
            settings.DescendingDarkDamage = 2;
            settings.HowlingWraithDamage = 1;
            settings.AbyssShriekDamage = 2;
            settings.GrubberflyElegyDamage = 1;
            settings.SporeShroomDamage = 0;
            settings.SporeDungShroomDamage = 0;
            settings.ThornOfAgonyDamage = 0;
            settings.SharpShadowDamage = 0;
        }

        private static void SetAllDamage(ServerSettings settings, byte value)
        {
            settings.NailDamage = value;
            settings.GreatSlashDamage = value;
            settings.DashSlashDamage = value;
            settings.CycloneSlashDamage = value;
            settings.VengefulSpiritDamage = value;
            settings.ShadeSoulDamage = value;
            settings.DesolateDiveDamage = value;
            settings.DescendingDarkDamage = value;
            settings.HowlingWraithDamage = value;
            settings.AbyssShriekDamage = value;
            settings.GrubberflyElegyDamage = value;
            settings.SporeShroomDamage = value;
            settings.SporeDungShroomDamage = value;
            settings.ThornOfAgonyDamage = value;
            settings.SharpShadowDamage = value;
        }
    }
}