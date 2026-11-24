using UnityEngine;

namespace HKMP.Rounds.Client
{
    internal sealed class GameFreezeController : MonoBehaviour
    {
        private static GameFreezeController _instance;
        private static bool _isFrozen;
        private static float _restoreTimeScale = 1f;
        private static Behaviour _inputHandler;
        private static bool _inputHandlerWasEnabled;

        public static bool IsFrozen
        {
            get { return _isFrozen; }
        }

        public void Initialize()
        {
            _instance = this;
            On.HeroController.Update -= OnHeroUpdate;
            On.HeroController.Update += OnHeroUpdate;
            On.HeroController.CanInput -= OnHeroCanInput;
            On.HeroController.CanInput += OnHeroCanInput;
            ApplyFreezeState();
        }

        public static void SetFrozen(bool frozen)
        {
            if (_isFrozen == frozen)
            {
                ApplyFreezeState();
                return;
            }

            if (frozen)
            {
                _restoreTimeScale = Time.timeScale;
                _isFrozen = true;
                ApplyFreezeState();
                return;
            }

            _isFrozen = false;
            ApplyFreezeState();
            Time.timeScale = _restoreTimeScale > 0f ? _restoreTimeScale : 1f;
            _restoreTimeScale = 1f;
        }

        private static void ApplyFreezeState()
        {
            if (_isFrozen)
            {
                Time.timeScale = 0f;
                GameManager manager = GameManager.instance;
                Behaviour currentInputHandler = manager != null
                    ? manager.inputHandler as Behaviour
                    : null;

                if (currentInputHandler != null && currentInputHandler != _inputHandler)
                {
                    RestoreInputHandler();
                    _inputHandler = currentInputHandler;
                    _inputHandlerWasEnabled = currentInputHandler.enabled;
                }

                if (_inputHandler != null)
                {
                    _inputHandler.enabled = false;
                }
                return;
            }

            RestoreInputHandler();
        }

        private static void RestoreInputHandler()
        {
            if (_inputHandler != null)
            {
                _inputHandler.enabled = _inputHandlerWasEnabled;
            }

            _inputHandler = null;
            _inputHandlerWasEnabled = false;
        }

        private static void OnHeroUpdate(On.HeroController.orig_Update orig, HeroController self)
        {
            if (_isFrozen && self == HeroController.instance)
            {
                return;
            }

            orig(self);
        }

        private static bool OnHeroCanInput(On.HeroController.orig_CanInput orig, HeroController self)
        {
            return !_isFrozen && orig(self);
        }

        private void Update()
        {
            if (_isFrozen)
            {
                ApplyFreezeState();
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                On.HeroController.Update -= OnHeroUpdate;
                On.HeroController.CanInput -= OnHeroCanInput;
                SetFrozen(false);
                _instance = null;
            }
        }
    }
}
