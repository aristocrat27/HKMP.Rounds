using GlobalEnums;
using Hkmp.Api.Client.Networking;
using HKMP.Rounds.Client;
using HKMP.Rounds.Networking.Packets;
using HKMP.Rounds.Settings;
using HutongGames.PlayMaker;
using Modding;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;

namespace HKMP.Rounds.Client
{
    internal static class PvpModeClientController
    {
        private static readonly int[] RestrictedCharmIds = Enumerable.Range(1, 40).ToArray();
        private static readonly HashSet<int> QuickForbiddenCharms = new HashSet<int> { 7, 8, 9, 17, 27, 29, 34, 36, 39 };
        private static readonly HashSet<int> LongForbiddenCharms = new HashSet<int> { 17 };
        private static readonly HashSet<int> OneShotForbiddenCharms = new HashSet<int> { 7, 8, 9, 17, 27, 29, 33, 34, 36, 39 };

        private sealed class CharmOriginalState
        {
            public bool WasOwned;
            public bool WasEquipped;
        }

        private sealed class CharmSnapshot
        {
            public readonly Dictionary<int, CharmOriginalState> Charms = new Dictionary<int, CharmOriginalState>();
            public int RoyalCharmState;
            public int GrimmChildLevel;
            public int CharmsOwned;
            public bool HasCharm;
        }

        private static CharmSnapshot _charmSnapshot;
        private static bool _fullCharmGrantApplied;
        private static List<int> _lastAllowedEquippedCharms;
        private static bool _applyingCharmPolicy;
        private static bool _charmPolicySuspended;
        private static bool _hooksInstalled;
        private static Action<uint, int> _sendManaSpent;
        private static PvpMode _mode = PvpMode.Quick;
        private static PvpMode _roundMode = PvpMode.Quick;
        private static bool _roundActive;
        private static uint _roundId;
        private static int _originalMpCharge;
        private static int _originalMpReserve;
        private static int _serverMana;

        public static PvpMode Mode { get { return _mode; } }
        public static int ServerMana { get { return _serverMana; } }
        internal static bool IsAttackerAllowed(ushort attackerId)
        {
            return PhantomMapClientController.IsPlayerInSamePhantomMap(attackerId);
        }
        public static bool IsOneShotRound { get { return _roundActive && _roundMode == PvpMode.OneShot; } }
        public static bool CanRestoreAllLocalCharms
        {
            get
            {
                return !_roundActive && !BenchPolicyController.IsConnected &&
                       IsGameplayReady(PlayerData.instance);
            }
        }

        public static void Initialize(Action<uint, int> sendManaSpent)
        {
            _sendManaSpent = sendManaSpent;
            if (_hooksInstalled) return;

            On.PlayerData.AddMPCharge += OnAddMPCharge;
            On.PlayerData.TakeReserveMP += OnTakeReserveMP;
            On.PlayerData.TakeMP += OnTakeMP;
            On.PlayerData.SetBool += OnSetBool;
            On.HeroController.Update += OnHeroUpdate;
            On.HeroController.AddHealth += OnAddHealth;
            ModHooks.SceneChanged += OnSceneChanged;
            OneShotElegyFix.Initialize();
            _hooksInstalled = true;
            _charmPolicySuspended = true;
        }

        public static void ResumeCharmPolicy()
        {
            _charmPolicySuspended = false;
            ApplyCharmPolicy();
        }

        public static void SuspendCharmPolicy()
        {
            if (_roundActive)
            {
                EndRound(_roundId);
            }

            RestoreModeRemovedCharms();
            _charmPolicySuspended = true;
            _mode = PvpMode.Quick;
            _roundMode = PvpMode.Quick;
        }

        public static void SetMode(PvpMode mode)
        {
            _mode = NormalizeMode(mode);
            _charmPolicySuspended = false;
            ApplyCharmPolicy();
        }

        public static void StartRound(PvpMode mode, uint roundId)
        {
            _charmPolicySuspended = false;
            SetMode(mode);
            _roundMode = _mode;
            _roundId = roundId;
            _roundActive = true;

            PlayerData data = PlayerData.instance;
            if (_roundMode == PvpMode.OneShot && data != null)
            {
                _originalMpCharge = data.MPCharge;
                _originalMpReserve = data.MPReserve;
                _serverMana = PvpModeRules.OneShotStartingMana;
                ApplyServerMana();
                ApplyOneShotHealth();
            }
        }

        public static void ApplyManaUpdate(ManaUpdatePacket packet)
        {
            if (packet == null || !_roundActive || _roundMode != PvpMode.OneShot || packet.RoundId != _roundId)
            {
                return;
            }

            _serverMana = Math.Max(0, packet.Mana);
            ApplyServerMana();
            HKMP.Rounds.Client.Combat.ClientHealthTracker.ReportCurrentHealth();
        }

        public static void EndRound(uint roundId)
        {
            if (!_roundActive || roundId != _roundId) return;

            _roundActive = false;
            PlayerData data = PlayerData.instance;
            if (data != null)
            {
                if (_roundMode == PvpMode.OneShot)
                {
                    SetMPChargeAndRefreshHud(
                        Math.Max(0, _originalMpCharge),
                        Math.Max(0, _originalMpReserve));
                }
            }

            _roundId = 0;
            _serverMana = 0;
        }

        public static void Clear()
        {
            if (_roundActive)
            {
                EndRound(_roundId);
            }

            RestoreModeRemovedCharms();

            if (_hooksInstalled)
            {
                On.PlayerData.AddMPCharge -= OnAddMPCharge;
                On.PlayerData.TakeReserveMP -= OnTakeReserveMP;
                On.PlayerData.TakeMP -= OnTakeMP;
                On.PlayerData.SetBool -= OnSetBool;
                On.HeroController.Update -= OnHeroUpdate;
                On.HeroController.AddHealth -= OnAddHealth;
                ModHooks.SceneChanged -= OnSceneChanged;
                OneShotElegyFix.Clear();
                _hooksInstalled = false;
            }

            _roundActive = false;
            _roundId = 0;
            _serverMana = 0;
            _sendManaSpent = null;
            _mode = PvpMode.Quick;
            _roundMode = PvpMode.Quick;
            _charmSnapshot = null;
            _fullCharmGrantApplied = false;
            _lastAllowedEquippedCharms = null;
            _applyingCharmPolicy = false;
            _charmPolicySuspended = false;
        }

        private static void OnSceneChanged(string sceneName)
        {
            ApplyCharmPolicy();

            if (_roundActive && _roundMode == PvpMode.OneShot)
            {
                ApplyServerMana();
                ApplyOneShotHealth();
            }
        }

        private static bool IsGameplayReady(PlayerData data)
        {
            GameManager gameManager = GameManager.instance;
            return data != null &&
                   data.equippedCharms != null &&
                   HeroController.instance != null &&
                   gameManager != null &&
                   gameManager.gameState == GameState.PLAYING;
        }

        private static bool IsAtBench(PlayerData data)
        {
            GameManager gameManager = GameManager.instance;
            return data != null &&
                   gameManager != null &&
                   gameManager.GetPlayerDataBool("atBench");
        }

        private static HashSet<int> GetForbiddenCharms(PvpMode mode)
        {
            if (mode == PvpMode.Long)
            {
                return LongForbiddenCharms;
            }

            return mode == PvpMode.OneShot
                ? OneShotForbiddenCharms
                : QuickForbiddenCharms;
        }

        private static void ApplyCharmPolicy()
        {
            if (_charmPolicySuspended)
            {
                return;
            }

            PlayerData data = PlayerData.instance;
            if (!IsGameplayReady(data))
            {
                return;
            }

            EnsureCharmSnapshot(data);
            if (_charmSnapshot == null)
            {
                return;
            }

            bool grantAllAllowedCharms = _mode == PvpMode.Quick ||
                                         _mode == PvpMode.Long ||
                                         _mode == PvpMode.OneShot;
            HashSet<int> forbidden = GetForbiddenCharms(_mode);
            bool changed = false;
            _applyingCharmPolicy = true;

            try
            {
                if (!grantAllAllowedCharms)
                {
                    if (_fullCharmGrantApplied)
                    {
                        RestoreCharmSnapshotData(data, true, ref changed);
                        _fullCharmGrantApplied = false;
                    }
                }
                else
                {
                    _fullCharmGrantApplied = true;
                    if (_mode == PvpMode.Long)
                    {
                        if (data.royalCharmState != 4)
                        {
                            data.royalCharmState = 4;
                            changed = true;
                        }
                    }
                    else if (_charmSnapshot.RoyalCharmState == 3 && forbidden.Contains(36) && data.royalCharmState != 4)
                    {
                        if (data.royalCharmState != 2)
                        {
                            data.royalCharmState = 2;
                            changed = true;
                        }
                    }
                    else if (data.royalCharmState != _charmSnapshot.RoyalCharmState &&
                             _charmSnapshot.RoyalCharmState != 3)
                    {
                        data.royalCharmState = _charmSnapshot.RoyalCharmState;
                        changed = true;
                    }

                    int desiredGrimmChildLevel = _mode == PvpMode.OneShot
                        ? 5
                        : Math.Max(1, _charmSnapshot.GrimmChildLevel);
                    if (data.grimmChildLevel != desiredGrimmChildLevel)
                    {
                        data.grimmChildLevel = desiredGrimmChildLevel;
                        changed = true;
                    }

                    if (!data.GetBool("gotCharm_40"))
                    {
                        data.SetBool("gotCharm_40", true);
                        changed = true;
                    }
                }

                for (int i = 0; i < RestrictedCharmIds.Length; i++)
                {
                    int charmId = RestrictedCharmIds[i];
                    bool isForbidden = IsForbiddenInMode(data, charmId);
                    string ownedName = "gotCharm_" + charmId;
                    string equippedName = "equippedCharm_" + charmId;

                    if (isForbidden)
                    {
                        if (data.equippedCharms.Remove(charmId))
                        {
                            changed = true;
                        }

                        if (data.GetBool(ownedName))
                        {
                            data.SetBool(ownedName, false);
                            changed = true;
                        }
                    }
                    else if (grantAllAllowedCharms && charmId != 36 && !data.GetBool(ownedName))
                    {
                        data.SetBool(ownedName, true);
                        changed = true;
                    }

                    bool shouldBeEquipped = !isForbidden && data.equippedCharms.Contains(charmId);
                    if (data.GetBool(equippedName) != shouldBeEquipped)
                    {
                        data.SetBool(equippedName, shouldBeEquipped);
                        changed = true;
                    }
                }

                int ownedCount = 0;
                for (int charmId = 1; charmId <= 40; charmId++)
                {
                    if (charmId == 36)
                    {
                        if (data.royalCharmState >= 3)
                        {
                            ownedCount++;
                        }
                    }
                    else if (data.GetBool("gotCharm_" + charmId))
                    {
                        ownedCount++;
                    }
                }

                if (data.charmsOwned != ownedCount)
                {
                    data.charmsOwned = ownedCount;
                    changed = true;
                }
                bool shouldHaveCharm = grantAllAllowedCharms
                    ? ownedCount > 0
                    : _charmSnapshot.HasCharm || ownedCount > 0;
                if (data.hasCharm != shouldHaveCharm)
                {
                    data.hasCharm = shouldHaveCharm;
                    changed = true;
                }
            }
            finally
            {
                _applyingCharmPolicy = false;
            }

            if (changed)
            {
                _lastAllowedEquippedCharms = new List<int>(data.equippedCharms);
                RefreshCharmState(data);
            }

            if (IsAtBench(data) || _lastAllowedEquippedCharms == null)
            {
                _lastAllowedEquippedCharms = new List<int>(data.equippedCharms);
            }
        }

        private static void EnsureCharmSnapshot(PlayerData data)
        {
            if (_charmSnapshot != null || data == null || data.equippedCharms == null)
            {
                return;
            }

            CharmSnapshot snapshot = new CharmSnapshot
            {
                RoyalCharmState = data.royalCharmState,
                GrimmChildLevel = data.grimmChildLevel,
                CharmsOwned = data.charmsOwned,
                HasCharm = data.hasCharm
            };

            for (int charmId = 1; charmId <= 40; charmId++)
            {
                snapshot.Charms[charmId] = new CharmOriginalState
                {
                    WasOwned = charmId == 36
                        ? data.royalCharmState >= 3
                        : data.GetBool("gotCharm_" + charmId),
                    WasEquipped = data.equippedCharms.Contains(charmId) || data.GetBool("equippedCharm_" + charmId)
                };
            }

            _charmSnapshot = snapshot;
        }

        private static bool IsForbiddenInMode(PlayerData data, int charmId)
        {
            if (!GetForbiddenCharms(_mode).Contains(charmId))
            {
                return false;
            }

            if (charmId == 36)
            {
                if (data != null && data.royalCharmState == 4)
                {
                    return false;
                }

                return data != null && data.royalCharmState == 3 ||
                       _charmSnapshot != null && _charmSnapshot.RoyalCharmState == 3;
            }

            return true;
        }

        private static void EnforceBenchCharmLock(PlayerData data)
        {
            if (_charmPolicySuspended || !IsGameplayReady(data))
            {
                return;
            }

            if (_lastAllowedEquippedCharms == null)
            {
                _lastAllowedEquippedCharms = new List<int>(data.equippedCharms);
                ApplyCharmPolicy();
                return;
            }

            if (IsAtBench(data))
            {
                _lastAllowedEquippedCharms = new List<int>(data.equippedCharms);
                return;
            }

            if (data.equippedCharms.SequenceEqual(_lastAllowedEquippedCharms))
            {
                return;
            }

            _applyingCharmPolicy = true;

            try
            {
                HashSet<int> expected = new HashSet<int>(_lastAllowedEquippedCharms);
                HashSet<int> allCharmIds = new HashSet<int>(data.equippedCharms);
                allCharmIds.UnionWith(_lastAllowedEquippedCharms);
                allCharmIds.UnionWith(RestrictedCharmIds);

                data.equippedCharms.Clear();
                data.equippedCharms.AddRange(_lastAllowedEquippedCharms);

                foreach (int charmId in allCharmIds)
                {
                    data.SetBool("equippedCharm_" + charmId, expected.Contains(charmId));
                }
            }
            finally
            {
                _applyingCharmPolicy = false;
            }

            RefreshCharmState(data);
        }

        private static void RestoreCharmSnapshotData(PlayerData data, bool restoreEquipment, ref bool changed)
        {
            if (data == null || _charmSnapshot == null)
            {
                return;
            }

            if (data.royalCharmState != _charmSnapshot.RoyalCharmState)
            {
                data.royalCharmState = _charmSnapshot.RoyalCharmState;
                changed = true;
            }
            if (data.grimmChildLevel != _charmSnapshot.GrimmChildLevel)
            {
                data.grimmChildLevel = _charmSnapshot.GrimmChildLevel;
                changed = true;
            }

            foreach (KeyValuePair<int, CharmOriginalState> item in _charmSnapshot.Charms)
            {
                int charmId = item.Key;
                CharmOriginalState original = item.Value;
                string ownedName = "gotCharm_" + charmId;
                string equippedName = "equippedCharm_" + charmId;
                bool currentOwned = data.GetBool(ownedName);

                if (currentOwned != original.WasOwned)
                {
                    data.SetBool(ownedName, original.WasOwned);
                    changed = true;
                }

                if (restoreEquipment)
                {
                    bool shouldEquip = original.WasOwned && original.WasEquipped;
                    bool currentlyEquipped = data.equippedCharms.Contains(charmId);
                    if (shouldEquip && !currentlyEquipped)
                    {
                        data.equippedCharms.Add(charmId);
                        changed = true;
                    }
                    else if (!shouldEquip && currentlyEquipped)
                    {
                        data.equippedCharms.Remove(charmId);
                        changed = true;
                    }

                    if (data.GetBool(equippedName) != shouldEquip)
                    {
                        data.SetBool(equippedName, shouldEquip);
                        changed = true;
                    }
                }
            }

            if (restoreEquipment)
            {
                if (data.charmsOwned != _charmSnapshot.CharmsOwned)
                {
                    data.charmsOwned = _charmSnapshot.CharmsOwned;
                    changed = true;
                }
                if (data.hasCharm != _charmSnapshot.HasCharm)
                {
                    data.hasCharm = _charmSnapshot.HasCharm;
                    changed = true;
                }
            }
        }

        public static bool RestoreAllLocalCharms()
        {
            if (!CanRestoreAllLocalCharms)
            {
                return false;
            }

            PlayerData data = PlayerData.instance;
            if (data == null || data.equippedCharms == null)
            {
                return false;
            }

            EnsureCharmSnapshot(data);
            _charmPolicySuspended = true;
            bool changed = false;
            _applyingCharmPolicy = true;

            try
            {
                if (data.royalCharmState != 4)
                {
                    data.royalCharmState = 4;
                    changed = true;
                }

                if (data.grimmChildLevel != 5)
                {
                    data.grimmChildLevel = 5;
                    changed = true;
                }

                for (int charmId = 1; charmId <= 40; charmId++)
                {
                    if (charmId == 36)
                    {
                        continue;
                    }

                    string ownedName = "gotCharm_" + charmId;
                    string equippedName = "equippedCharm_" + charmId;
                    if (!data.GetBool(ownedName))
                    {
                        data.SetBool(ownedName, true);
                        changed = true;
                    }

                    bool equipped = data.equippedCharms.Contains(charmId);
                    if (data.GetBool(equippedName) != equipped)
                    {
                        data.SetBool(equippedName, equipped);
                        changed = true;
                    }
                }

                if (data.charmsOwned != 40)
                {
                    data.charmsOwned = 40;
                    changed = true;
                }

                if (!data.hasCharm)
                {
                    data.hasCharm = true;
                    changed = true;
                }
            }
            finally
            {
                _applyingCharmPolicy = false;
            }

            _fullCharmGrantApplied = true;
            _lastAllowedEquippedCharms = new List<int>(data.equippedCharms);
            if (changed)
            {
                RefreshCharmState(data);
            }

            return true;
        }

        private static void RestoreModeRemovedCharms()
        {
            PlayerData data = PlayerData.instance;
            if (data == null || data.equippedCharms == null || _charmSnapshot == null)
            {
                _charmSnapshot = null;
                _fullCharmGrantApplied = false;
                _lastAllowedEquippedCharms = null;
                return;
            }

            bool changed = false;
            _applyingCharmPolicy = true;

            try
            {
                if (_fullCharmGrantApplied)
                {
                    RestoreCharmSnapshotData(data, true, ref changed);
                }
                else
                {
                    RestoreCharmSnapshotEntry(data, 17, ref changed);
                    int ownedCount = 0;
                    for (int charmId = 1; charmId <= 40; charmId++)
                    {
                        if (charmId == 36)
                        {
                            if (data.royalCharmState >= 3)
                            {
                                ownedCount++;
                            }
                        }
                        else if (data.GetBool("gotCharm_" + charmId))
                        {
                            ownedCount++;
                        }
                    }

                    if (data.charmsOwned != ownedCount)
                    {
                        data.charmsOwned = ownedCount;
                        changed = true;
                    }
                    bool shouldHaveCharm = _charmSnapshot.HasCharm || ownedCount > 0;
                    if (data.hasCharm != shouldHaveCharm)
                    {
                        data.hasCharm = shouldHaveCharm;
                        changed = true;
                    }
                }
            }
            finally
            {
                _applyingCharmPolicy = false;
            }

            _charmSnapshot = null;
            _fullCharmGrantApplied = false;
            _lastAllowedEquippedCharms = new List<int>(data.equippedCharms);

            if (changed)
            {
                RefreshCharmState(data);
            }
        }

        private static void RestoreCharmSnapshotEntry(PlayerData data, int charmId, ref bool changed)
        {
            if (data == null || _charmSnapshot == null ||
                !_charmSnapshot.Charms.TryGetValue(charmId, out CharmOriginalState original))
            {
                return;
            }

            string ownedName = "gotCharm_" + charmId;
            string equippedName = "equippedCharm_" + charmId;
            if (data.GetBool(ownedName) != original.WasOwned)
            {
                data.SetBool(ownedName, original.WasOwned);
                changed = true;
            }

            bool shouldEquip = original.WasOwned && original.WasEquipped;
            bool currentlyEquipped = data.equippedCharms.Contains(charmId);
            if (shouldEquip && !currentlyEquipped)
            {
                data.equippedCharms.Add(charmId);
                changed = true;
            }
            else if (!shouldEquip && currentlyEquipped)
            {
                data.equippedCharms.Remove(charmId);
                changed = true;
            }

            if (data.GetBool(equippedName) != shouldEquip)
            {
                data.SetBool(equippedName, shouldEquip);
                changed = true;
            }
        }

        private static void RefreshCharmState(PlayerData data)
        {
            if (data == null)
            {
                return;
            }

            data.CalculateNotchesUsed();

            if (HeroController.instance != null)
            {
                HeroController.instance.CharmUpdate();
            }

            PlayMakerFSM.BroadcastEvent("CHARM EQUIP CHECK");
            PlayMakerFSM.BroadcastEvent("CHARM INDICATOR CHECK");
        }

        private static void OnSetBool(
            On.PlayerData.orig_SetBool orig,
            PlayerData self,
            string boolName,
            bool value)
        {
            if (!_applyingCharmPolicy &&
                !_charmPolicySuspended &&
                self == PlayerData.instance &&
                IsGameplayReady(self) &&
                !string.IsNullOrEmpty(boolName))
            {
                const string equippedPrefix = "equippedCharm_";
                const string ownedPrefix = "gotCharm_";
                bool isEquippedFlag = boolName.StartsWith(equippedPrefix, StringComparison.Ordinal);
                bool isOwnedFlag = boolName.StartsWith(ownedPrefix, StringComparison.Ordinal);

                if (isEquippedFlag || isOwnedFlag)
                {
                    string suffix = boolName.Substring(isEquippedFlag ? equippedPrefix.Length : ownedPrefix.Length);
                    int charmId;
                    if (int.TryParse(suffix, out charmId) && RestrictedCharmIds.Contains(charmId))
                    {
                        if (IsForbiddenInMode(self, charmId) && value)
                        {
                            value = false;
                        }
                        else if (isEquippedFlag && !IsAtBench(self) && _lastAllowedEquippedCharms != null)
                        {
                            value = !IsForbiddenInMode(self, charmId) && _lastAllowedEquippedCharms.Contains(charmId);
                        }
                    }
                }
            }

            orig(self, boolName, value);
        }

        private static bool OnAddMPCharge(On.PlayerData.orig_AddMPCharge orig, PlayerData self, int amount)
        {
            if (_roundActive && _roundMode == PvpMode.OneShot) return false;
            return orig(self, amount);
        }

        private static void OnTakeReserveMP(On.PlayerData.orig_TakeReserveMP orig, PlayerData self, int amount)
        {
            if (_roundActive && _roundMode == PvpMode.OneShot)
            {
                self.MPReserve = 0;
                return;
            }

            orig(self, amount);
        }

        private static void OnTakeMP(On.PlayerData.orig_TakeMP orig, PlayerData self, int amount)
        {
            bool track = _roundActive && _roundMode == PvpMode.OneShot && self == PlayerData.instance;
            int before = track ? Math.Max(0, self.MPCharge) : 0;
            orig(self, amount);
            if (!track) return;

            int after = Math.Max(0, self.MPCharge);
            int spent = Math.Max(0, before - after);
            if (spent <= 0) return;

            _serverMana = Math.Max(0, _serverMana - spent);
            self.MPReserve = 0;
            if (_sendManaSpent != null) _sendManaSpent(_roundId, spent);
        }

        private static void OnAddHealth(
            On.HeroController.orig_AddHealth orig,
            HeroController self,
            int amount)
        {
            if (amount > 0 &&
                self == HeroController.instance &&
                _roundActive &&
                (_roundMode == PvpMode.Quick || _roundMode == PvpMode.OneShot) &&
                !RoundPreparation.IsApplyingRoundStartHealth)
            {
                return;
            }

            orig(self, amount);
        }

        private static void OnHeroUpdate(On.HeroController.orig_Update orig, HeroController self)
        {
            bool isLocalHero = self != null && self == HeroController.instance;
            orig(self);

            if (!isLocalHero)
            {
                return;
            }

            PlayerData data = PlayerData.instance;
            if (data == null)
            {
                return;
            }

            EnforceBenchCharmLock(data);

            if (!_roundActive)
            {
                return;
            }

            if (_roundMode == PvpMode.OneShot)
            {
                ApplyOneShotHealth();

                if (data.MPCharge != _serverMana || data.MPReserve != 0)
                {
                    ApplyServerMana();
                }
            }
        }

        public static void ApplyOneShotHealth()
        {
            if (!IsOneShotRound)
            {
                return;
            }

            PlayerData data = PlayerData.instance;
            if (data == null || data.health <= 0)
            {
                return;
            }

            bool changed = data.health != 1 ||
                           data.healthBlue != 0 ||
                           data.joniHealthBlue != 0 ||
                           data.damagedBlue;

            data.health = 1;
            data.healthBlue = 0;
            data.joniHealthBlue = 0;
            data.damagedBlue = false;

            if (changed && HeroController.instance != null)
            {
                HeroController.instance.TakeHealth(0);
                try
                {
                    EventRegister.SendEvent("UPDATE HEALTH");
                    EventRegister.SendEvent("UPDATE BLUE HEALTH");
                    PlayMakerFSM.BroadcastEvent("UPDATE HEALTH");
                    PlayMakerFSM.BroadcastEvent("UPDATE BLUE HEALTH");
                }
                catch (Exception exception)
                {
                    Modding.Logger.LogError(
                        "[HKMP.Rounds] Could not refresh OneShot health HUD: " + exception.Message);
                }
            }
        }

        private static void ApplyServerMana()
        {
            PlayerData data = PlayerData.instance;
            if (!_roundActive || _roundMode != PvpMode.OneShot || data == null) return;

            SetMPChargeAndRefreshHud(_serverMana, 0);
        }

        private static void SetMPChargeAndRefreshHud(int charge, int reserve)
        {
            PlayerData data = PlayerData.instance;
            if (data == null)
            {
                return;
            }

            int previousCharge = Math.Max(0, data.MPCharge);
            int previousReserve = Math.Max(0, data.MPReserve);
            int nextCharge = Math.Max(0, charge);
            int nextReserve = Math.Max(0, reserve);

            if (HeroController.instance != null)
            {
                HeroController.instance.SetMPCharge(nextCharge);
            }
            else
            {
                data.MPCharge = nextCharge;
            }

            data.MPReserve = nextReserve;
            if (previousCharge == nextCharge && previousReserve == nextReserve)
            {
                return;
            }

            try
            {
                if (nextCharge > previousCharge)
                {
                    PlayMakerFSM.BroadcastEvent("MP GAIN");
                }
                else if (nextCharge < previousCharge)
                {
                    PlayMakerFSM.BroadcastEvent("MP LOSE");
                }

                if (nextCharge == 0 && previousCharge > 0)
                {
                    PlayMakerFSM.BroadcastEvent("MP DRAIN");
                }

                if (nextReserve < previousReserve)
                {
                    PlayMakerFSM.BroadcastEvent("MP RESERVE DOWN");
                }
            }
            catch (Exception exception)
            {
                Modding.Logger.LogError(
                    "[HKMP.Rounds] Could not refresh OneShot SOUL HUD: " + exception.Message);
            }
        }

        private static PvpMode NormalizeMode(PvpMode mode)
        {
            return mode == PvpMode.Long || mode == PvpMode.OneShot ? mode : PvpMode.Quick;
        }
    }
}
