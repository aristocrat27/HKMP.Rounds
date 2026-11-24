using System;
using Hkmp.Api.Client;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using HKMP.Rounds.Networking.Packets;
using HKMP.Rounds.Networking;
using HKMP.Rounds.Settings;
using HutongGames.PlayMaker;
using Modding;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using ImageConversion = UnityEngine.ImageConversion;
namespace HKMP.Rounds.Client
{
    internal sealed class RoundsSettingsUI : MonoBehaviour
    {
        private static readonly Color RowColor = new Color(0.065f, 0.078f, 0.105f, 0.98f);
        private static readonly Color ButtonColor = new Color(0.13f, 0.155f, 0.20f, 0.98f);
        private static readonly Color ButtonHoverColor = new Color(0.22f, 0.26f, 0.33f, 1f);
        private static readonly Color MutedTextColor = new Color(0.70f, 0.75f, 0.83f, 1f);
        private static readonly Color AccentTextColor = new Color(0.96f, 0.84f, 0.59f, 1f);

        private const string HelpRepositoryRawBaseUrl =
            "https://raw.githubusercontent.com/aristocrat27/HKMP.Rounds/main/";
        private const string HelpReadmeUrl = HelpRepositoryRawBaseUrl + "README.md";

        private enum FrameDecorationKind
        {
            Horizontal,
            Vertical,
            Corner
        }

        private enum ClientBindingTarget
        {
            None,
            Ready,
            Stats,
            ReadyHud,
            Freeze
        }

        private static readonly KeyCode[] BindableKeys = Enum.GetValues(typeof(KeyCode))
            .Cast<KeyCode>()
            .Where(ClientPreferencesStore.IsBindableKey)
            .ToArray();

        private sealed class FrameDecorationLayout
        {
            public RectTransform Transform;
            public FrameDecorationKind Kind;
            public float Aspect;
        }

        private sealed class BenchOptionRow
        {
            public GameObject Root;
            public string SearchText;
        }

        private readonly List<FrameDecorationLayout> _frameDecorationLayouts =
            new List<FrameDecorationLayout>();
        private readonly List<BenchLocation> _knownBenches = new List<BenchLocation>();
        private readonly List<BenchOptionRow> _benchOptionRows = new List<BenchOptionRow>();
        private readonly Dictionary<string, string> _numericInputDrafts =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, bool> _collapsedSections =
            new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private readonly List<PhantomMantisMap> _phantomMantisMaps = new List<PhantomMantisMap>();
        private static RoundsSettingsUI _instance;
        private static Sprite _oneShotIcon;
        private static Sprite _quickModeIcon;
        private static Sprite _longModeIcon;
        private static bool _quickModeIconLoadAttempted;
        private static bool _longModeIconLoadAttempted;
        private static Sprite _boundChainIcon;
        private static Sprite _unboundChainIcon;
        private static Sprite _benchSearchIcon;
        private static Font _questionMarkFont;
        private static Font _uiFont;
        private static readonly Dictionary<string, Sprite> HelpImageCache =
            new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        private IClientApi _clientApi;
        private GlobalSettingsStore _settings;
        private ClientNetManager _network;
        private Dictionary<string, Dictionary<string, GameObject>> _preloadedObjects;

        private GameObject _root;
        private Canvas _canvas;
        private RectTransform _canvasRect;
        private RectTransform _windowRect;
        private CanvasGroup _canvasGroup;
        private Animator _windowAnimator;
        private EventSystem _eventSystem;
        private StandaloneInputModule _standaloneInputModule;
        private readonly Dictionary<BaseInputModule, bool> _savedInputModuleStates =
            new Dictionary<BaseInputModule, bool>();
        private readonly Dictionary<EventSystem, bool> _savedEventSystemStates =
            new Dictionary<EventSystem, bool>();
        private bool _ownsEventSystem;
        private bool _standaloneModuleWasAdded;
        private bool _standaloneOriginalEnabled;
        private bool _standaloneOriginalForceModuleActive;
        private bool _standaloneOriginalStateSaved;
        private GameObject _previousSelectedObject;
        private EventSystem _previousEventSystem;
        private RectTransform _content;
        private ScrollRect _scrollRect;
        private Button _closeButton;
        private Text _superTitle;
        private Text _mainTitle;
        private Text _description;
        private Text _statusBar;
        private Scrollbar _verticalScrollbar;
        private Button _russianLanguageButton;
        private Button _englishLanguageButton;
        private Button _helpButton;
        private Button _restoreCharmsButton;
        private VectorStrengthGraphic _restoreCharmsIcon;
        private GameObject _helpRoot;
        private RectTransform _helpContentRoot;
        private Text _helpTitle;
        private ScrollRect _helpScrollRect;
        private bool _helpVisible;
        private string _helpReadmeContent;
        private string _helpReadmeError;
        private bool _helpReadmeLoading;
        private int _helpReadmeRequestId;

        private Vector3 _windowBaseScale = Vector3.one;
        private Vector2 _lastResponsiveWindowSize;
        private Coroutine _showBuildSequence;
        private Coroutine _hideSequence;
        private bool _windowGeometryInitialized;
        private bool _windowVisualsConfigured;
        private bool _usingFallbackUi;
        private bool _initialized;
        private bool _visible;
        private bool _closing;
        private bool _statisticsOnlyView;
        private bool _hudHiddenByRoundsUi;
        private bool _benchDropdownExpanded;
        private string _benchSearchQuery = string.Empty;
        private GameObject _benchNoResultsObject;
        private bool _canManageBench;
        private bool _canManageMapTravel;
        private bool _mapTravelEnabled;
        private int _phantomMapPage;
        private int _phantomMapPageCount = 1;
        private bool _canManageSettings;
        private bool _canManagePvpMode;
        private bool _hasSharedBench;
        private bool _sharedBenchLocked;
        private bool _playModeAnimation;
        private PvpMode _pvpMode = PvpMode.Quick;
        private bool _cursorSubscribed;
        private bool _cursorWasVisible;
        private CursorLockMode _cursorWasLocked;
        private string _sharedBenchScene = string.Empty;
        private string _sharedBenchMarker = string.Empty;
        private int _sharedBenchMapZone;
        private int _sharedBenchPhantomMapId;
        private string _statusMessage = string.Empty;
        private BenchLocation _selectedBench;
        private float _nextSceneDiscoveryAt;
        private bool _sceneDiscoveryPending;
        private string _lastSentUiCatalogJson = string.Empty;
        private string _lastRoundSettingsUiSignature = string.Empty;
        private string _statsText = "Statistics have not been loaded.";
        private int _winTarget;
        private bool _roundActive;
        private bool _waitingForPreparation;
        private bool _openedWhilePaused;
        private ClientBindingTarget _bindingCaptureTarget;

        public static bool IsVisible
        {
            get { return _instance != null && _instance._visible; }
        }

        public void Initialize(
            IClientApi clientApi,
            GlobalSettingsStore settings,
            ClientNetManager network,
            Dictionary<string, Dictionary<string, GameObject>> preloadedObjects)
        {
            _instance = this;
            _clientApi = clientApi;
            _settings = settings ?? new GlobalSettingsStore();
            RoundsLocalization.SetLanguage(ClientPreferencesStore.GetUiLanguage(_settings.UiLanguage));
            _knownBenches.Clear();
            MergeKnownBenches(_settings.GetKnownBenches());
            MergeKnownBenches(BenchCatalog.GetBuiltInCatalog());
            _network = network;
            _preloadedObjects = preloadedObjects;

            RoundStatsOverlay.Initialize(_network);
            RoundStatsOverlay.SetStatistics(_statsText);

            if (_network != null)
            {
                _network.BenchSettingsReceived -= OnBenchSettingsReceived;
                _network.BenchSettingsReceived += OnBenchSettingsReceived;
                _network.PvpModeReceived -= OnPvpModeReceived;
                _network.PvpModeReceived += OnPvpModeReceived;
                _network.RoundSettingsReceived -= OnRoundSettingsReceived;
                _network.RoundSettingsReceived += OnRoundSettingsReceived;
                _network.RoundStatsReceived -= OnRoundStatsReceived;
                _network.RoundStatsReceived += OnRoundStatsReceived;
                _network.MapTravelStateReceived -= OnMapTravelStateReceived;
                _network.MapTravelStateReceived += OnMapTravelStateReceived;
            }

            if (_clientApi != null && _clientApi.ClientManager != null)
            {
                _clientApi.ClientManager.ConnectEvent -= OnClientConnected;
                _clientApi.ClientManager.ConnectEvent += OnClientConnected;
                _clientApi.ClientManager.DisconnectEvent -= OnClientDisconnected;
                _clientApi.ClientManager.DisconnectEvent += OnClientDisconnected;
            }

            CreateUi();
            UnityEngine.SceneManagement.SceneManager.activeSceneChanged += OnActiveSceneChanged;
            _initialized = true;
            _sceneDiscoveryPending = false;
            if (_clientApi != null && _clientApi.NetClient != null && _clientApi.NetClient.IsConnected)
            {
                OnClientConnected();
            }
        }

        private void Update()
        {
            if (!_initialized)
            {
                return;
            }

            ClientPreferencesStore.SynchronizeMenuKeyConflicts();
            UpdateRestoreCharmsButton();

            if (_bindingCaptureTarget != ClientBindingTarget.None)
            {
                HandleBindingCapture();
                return;
            }

            bool statsVisibleBeforeHotkey = RoundStatsOverlay.IsVisible;
            if (RoundStatsOverlay.HandleHotkey())
            {
                if (_visible && !_closing &&
                    statsVisibleBeforeHotkey != RoundStatsOverlay.IsVisible)
                {
                    RebuildRows();
                }

                return;
            }

            if (HKMP.Rounds.HKMPRounds.MenuOpenAction.WasPressed)
            {
                if (_visible && _statisticsOnlyView)
                {
                    _statisticsOnlyView = false;
                    RebuildRows();
                    UpdateStatusBar();
                }
                else if (_visible)
                {
                    Hide();
                }
                else
                {
                    _statisticsOnlyView = false;
                    Show();
                }
                return;
            }

            if (_visible && !_closing && UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                Hide();
            }

            if (_visible && _sceneDiscoveryPending && Time.unscaledTime >= _nextSceneDiscoveryAt)
            {
                _sceneDiscoveryPending = false;
                DiscoverCurrentSceneBenches();
            }
        }


        private void BeginBindingCapture(ClientBindingTarget target)
        {
            if (target == ClientBindingTarget.None)
            {
                return;
            }

            _bindingCaptureTarget = target;
            switch (target)
            {
                case ClientBindingTarget.Ready:
                    _statusMessage = "Press a key for the ready command. Escape cancels.";
                    break;
                case ClientBindingTarget.Stats:
                    _statusMessage = "Press a key for the statistics overlay. Escape cancels.";
                    break;
                case ClientBindingTarget.Freeze:
                    _statusMessage = "Press a key for game freeze. Escape cancels.";
                    break;
                default:
                    _statusMessage = "Press a key for the ready HUD visibility/edit hotkey. Escape cancels.";
                    break;
            }
            RebuildRows();
            UpdateStatusBar();
        }

        private void HandleBindingCapture()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                _bindingCaptureTarget = ClientBindingTarget.None;
                _statusMessage = "Key binding cancelled.";
                RebuildRows();
                UpdateStatusBar();
                return;
            }

            for (int i = 0; i < BindableKeys.Length; i++)
            {
                KeyCode key = BindableKeys[i];
                if (!UnityEngine.Input.GetKeyDown(key))
                {
                    continue;
                }

                bool saved;
                switch (_bindingCaptureTarget)
                {
                    case ClientBindingTarget.Ready:
                        saved = ClientPreferencesStore.TrySetReadyKey(key);
                        break;

                    case ClientBindingTarget.Stats:
                        saved = ClientPreferencesStore.TrySetStatsKey(key);
                        break;

                    case ClientBindingTarget.ReadyHud:
                        saved = ClientPreferencesStore.TrySetReadyHudKey(key);
                        break;

                    case ClientBindingTarget.Freeze:
                        saved = ClientPreferencesStore.TrySetFreezeKey(key);
                        break;

                    default:
                        saved = false;
                        break;
                }

                _bindingCaptureTarget = ClientBindingTarget.None;
                _statusMessage = saved
                    ? "Hotkey saved: " + key + "."
                    : "That key is already used by the other hotkey or cannot be bound.";
                RebuildRows();
                UpdateStatusBar();
                return;
            }
        }

        private void LateUpdate()
        {
            if (_visible)
            {
                EnsureEventSystem();
                KeepInputOnWindow();
                ForceCursorVisible();

                if (_windowRect != null)
                {
                    Vector2 currentSize = _windowRect.rect.size;
                    if ((currentSize - _lastResponsiveWindowSize).sqrMagnitude > 0.5f)
                    {
                        OnWindowResized();
                    }
                }
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus && _visible)
            {
                ForceCursorVisible();
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (!paused && _visible)
            {
                ForceCursorVisible();
            }
        }

        private void CreateUi()
        {
            GameObject prefab = FindChallengeDoorCanvas(_preloadedObjects);
            if (prefab != null)
            {
                GameObject stagingRoot = new GameObject("[HKMP.Rounds] Settings UI Staging");
                stagingRoot.SetActive(false);
                try
                {
                    _root = Instantiate(prefab, stagingRoot.transform, false);
                    _root.name = "[HKMP.Rounds] Settings Canvas";
                    RemoveInheritedMenuBehaviours(_root);
                    _root.SetActive(false);
                    _root.transform.SetParent(null, true);
                    Object.DontDestroyOnLoad(_root);
                }
                finally
                {
                    Object.Destroy(stagingRoot);
                }

                ConfigureCanvasTree();
            }
            else
            {
                Debug.LogWarning(
                    "[HKMP.Rounds] Challenge Door UI prefab could not be resolved; using the built-in fallback panel.");
                CreateFallbackUi();
            }

            _root.SetActive(false);
            ConfigureOverlayCanvas();
            ConfigureWindowInteraction();
            ConfigureHeaderLayout();
            ConfigureWindowVisuals();
            _windowVisualsConfigured = true;
            PreloadMapPresetSprites();
            Canvas.ForceUpdateCanvases();
        }

        private static void RemoveInheritedMenuBehaviours(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            BossDoorChallengeUI[] challengeUiComponents = root.GetComponentsInChildren<BossDoorChallengeUI>(true);
            for (int i = 0; i < challengeUiComponents.Length; i++)
            {
                if (challengeUiComponents[i] != null)
                {
                    Object.DestroyImmediate(challengeUiComponents[i]);
                }
            }

            MenuButtonList[] menuButtonLists = root.GetComponentsInChildren<MenuButtonList>(true);
            for (int i = 0; i < menuButtonLists.Length; i++)
            {
                if (menuButtonLists[i] != null)
                {
                    Object.DestroyImmediate(menuButtonLists[i]);
                }
            }

            EventTrigger[] eventTriggers = root.GetComponentsInChildren<EventTrigger>(true);
            for (int i = 0; i < eventTriggers.Length; i++)
            {
                if (eventTriggers[i] != null)
                {
                    Object.DestroyImmediate(eventTriggers[i]);
                }
            }

            Animator[] animators = root.GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < animators.Length; i++)
            {
                if (animators[i] != null)
                {
                    Object.DestroyImmediate(animators[i]);
                }
            }

            Animation[] legacyAnimations = root.GetComponentsInChildren<Animation>(true);
            for (int i = 0; i < legacyAnimations.Length; i++)
            {
                if (legacyAnimations[i] != null)
                {
                    Object.DestroyImmediate(legacyAnimations[i]);
                }
            }

            PlayMakerFSM[] stateMachines = root.GetComponentsInChildren<PlayMakerFSM>(true);
            for (int i = 0; i < stateMachines.Length; i++)
            {
                if (stateMachines[i] != null)
                {
                    Object.DestroyImmediate(stateMachines[i]);
                }
            }
        }

        private void ConfigureCanvasTree()
        {
            _canvas = _root.GetComponent<Canvas>();
            if (_canvas == null)
            {
                _canvas = _root.GetComponentInChildren<Canvas>(true);
            }

            if (_canvas == null)
            {
                CreateFallbackUi();
                return;
            }

            _canvasGroup = _root.GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
            {
                _canvasGroup = _root.AddComponent<CanvasGroup>();
            }

            NormalizeChildCanvasGroups();
            RemoveInheritedTextLocalizersAndChainCaption(_root);
            _windowAnimator = _root.GetComponent<Animator>();
            if (_windowAnimator != null)
            {
                _windowAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
            }

            Transform panel = _root.transform.Find("Panel");
            if (panel == null)
            {
                CreateFallbackUi();
                return;
            }

            _windowRect = panel as RectTransform;
            if (_windowRect == null)
            {
                CreateFallbackUi();
                return;
            }

            _superTitle = FindText(panel, "Title_Super_Text");
            _mainTitle = FindText(panel, "Title_Main_Text");
            _description = FindText(panel, "Description_Text");

            SetText(_superTitle, "HKMP.Rounds");
            SetText(_mainTitle, "SETTINGS");
            SetText(_description, "PvP mode, round rules and shared bench");
            SetAnchored(
                _superTitle.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(146f, -12f),
                new Vector2(360f, 24f));
            SetAnchored(
                _mainTitle.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(146f, -39f),
                new Vector2(400f, 40f));
            SetAnchored(
                _description.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(146f, -80f),
                new Vector2(600f, 26f));

            Transform buttonsPanel = panel.Find("Buttons");
            if (buttonsPanel == null)
            {
                CreateFallbackUi();
                return;
            }

            Transform beginButtonTransform = panel.Find("BeginButton");
            if (beginButtonTransform != null)
            {
                beginButtonTransform.gameObject.SetActive(false);
            }

            SetupScrollableContent(buttonsPanel);
            _closeButton = CreateButton(panel, "RoundsCloseButton", "CLOSE", Hide, 15);
            SetAnchored(
                _closeButton.GetComponent<RectTransform>(),
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(-20f, 18f),
                new Vector2(140f, 42f));
            CreateStatusBar(panel);
            CreateHeaderControls(panel);

            ConfigureCanvasCamera();
        }

        private static void RemoveInheritedTextLocalizersAndChainCaption(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            Component[] components = root.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null || component is Transform || component is RectTransform)
                {
                    continue;
                }

                if (string.Equals(component.GetType().Name, "AutoLocalizeTextUI", StringComparison.Ordinal))
                {
                    Object.DestroyImmediate(component);
                    continue;
                }

                GameObject componentObject = component.gameObject;
                string objectName = componentObject != null ? componentObject.name : string.Empty;
                string textValue = ReadComponentText(component);
                bool chainCaption =
                    string.Equals(textValue, "Bindings", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(objectName, "Bindings", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(textValue, "Р’РµСЂРёРіРё", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(textValue, "Chains", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(textValue, "Chain", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(objectName, "Р’РµСЂРёРіРё", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(objectName, "Chains", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(objectName, "Chain", StringComparison.OrdinalIgnoreCase);

                if (chainCaption && componentObject != null)
                {
                    componentObject.SetActive(false);
                }
            }
        }

        private static string ReadComponentText(Component component)
        {
            if (component == null)
            {
                return string.Empty;
            }

            Text unityText = component as Text;
            if (unityText != null)
            {
                return unityText.text ?? string.Empty;
            }

            Type componentType = component.GetType();
            try
            {
                PropertyInfo textProperty = componentType.GetProperty(
                    "text",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (textProperty != null && textProperty.PropertyType == typeof(string) && textProperty.GetIndexParameters().Length == 0)
                {
                    object value = textProperty.GetValue(component, null);
                    return value as string ?? string.Empty;
                }

                FieldInfo textField = componentType.GetField(
                    "text",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (textField != null && textField.FieldType == typeof(string))
                {
                    object value = textField.GetValue(component);
                    return value as string ?? string.Empty;
                }
            }
            catch
            {
            }

            return string.Empty;
        }

        private void CreateFallbackUi()
        {
            _usingFallbackUi = true;
            _windowAnimator = null;
            if (_root != null)
            {
                Object.Destroy(_root);
            }

            _root = new GameObject(
                "[HKMP.Rounds] Settings Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(CanvasGroup));

            Object.DontDestroyOnLoad(_root);
            _canvas = _root.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 1000;
            _canvasGroup = _root.GetComponent<CanvasGroup>();

            GameObject panelObject = CreateUiObject(_root.transform, "Panel");
            RectTransform panelRect = panelObject.GetComponent<RectTransform>();
            _windowRect = panelRect;
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(850f, 570f);

            Image panelImage = panelObject.AddComponent<Image>();
            panelImage.sprite = WhiteSprite;
            panelImage.color = new Color(0.045f, 0.045f, 0.06f, 0.78f);
            panelImage.raycastTarget = true;

            _superTitle = CreateText(panelRect, "Title_Super_Text", "HKMP.Rounds", 16, MutedTextColor);
            _superTitle.fontStyle = FontStyle.Bold;
            SetAnchored(_superTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(146f, -12f), new Vector2(360f, 24f));

            _mainTitle = CreateText(panelRect, "Title_Main_Text", "SETTINGS", 24, AccentTextColor);
            _mainTitle.fontStyle = FontStyle.Bold;
            SetAnchored(_mainTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(146f, -39f), new Vector2(400f, 40f));

            _description = CreateText(panelRect, "Description_Text", "Round options and shared bench", 14, MutedTextColor);
            SetAnchored(_description.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(146f, -80f), new Vector2(600f, 26f));

            GameObject buttons = CreateUiObject(panelRect, "Buttons");
            RectTransform buttonsRect = buttons.GetComponent<RectTransform>();
            buttonsRect.anchorMin = Vector2.zero;
            buttonsRect.anchorMax = Vector2.one;
            buttonsRect.offsetMin = new Vector2(18f, 58f);
            buttonsRect.offsetMax = new Vector2(-18f, -115f);
            SetupScrollableContent(buttons.transform);

            _closeButton = CreateButton(panelRect, "BeginButton", "CLOSE", Hide, 15);
            SetAnchored(
                _closeButton.GetComponent<RectTransform>(),
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(-20f, 18f),
                new Vector2(140f, 42f));
            CreateStatusBar(panelRect);
            CreateHeaderControls(panelRect);

            ConfigureCanvasCamera();
        }

        private void CreateHeaderControls(Transform panel)
        {
            if (panel == null)
            {
                return;
            }

            _russianLanguageButton = CreateFlagButton(
                panel,
                "RoundsLanguageRU",
                VectorFlagGraphic.FlagKind.Russia,
                "RU",
                16f,
                delegate { ChangeUiLanguage("ru"); });

            _englishLanguageButton = CreateFlagButton(
                panel,
                "RoundsLanguageEN",
                VectorFlagGraphic.FlagKind.UnitedKingdom,
                "EN",
                68f,
                delegate { ChangeUiLanguage("en"); });

            _helpButton = CreateButton(panel, "RoundsHelpButton", "?", ToggleHelp, 18);
            RectTransform helpRect = _helpButton.GetComponent<RectTransform>();
            SetAnchored(
                helpRect,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(120f, -14f),
                new Vector2(30f, 28f));
            Text helpLabel = _helpButton.GetComponentInChildren<Text>();
            if (helpLabel != null)
            {
                helpLabel.text = "?";
                helpLabel.font = GetQuestionMarkFont();
                helpLabel.fontStyle = FontStyle.Normal;
                helpLabel.color = Color.white;
                helpLabel.supportRichText = false;
                helpLabel.alignment = TextAnchor.MiddleCenter;
                helpLabel.enabled = true;
                if (helpLabel.font != null)
                {
                    helpLabel.font.RequestCharactersInTexture("?", helpLabel.fontSize, FontStyle.Normal);
                }
            }

            CreateRestoreCharmsButton(panel);
            CreateHelpWindow(panel);
        }

        private void CreateRestoreCharmsButton(Transform panel)
        {
            _restoreCharmsButton = CreateButton(
                panel,
                "RoundsRestoreAllCharmsButton",
                string.Empty,
                RestoreAllLocalCharms,
                12);

            SetAnchored(
                _restoreCharmsButton.GetComponent<RectTransform>(),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-20f, -14f),
                new Vector2(38f, 38f));

            Image background = _restoreCharmsButton.GetComponent<Image>();
            if (background != null)
            {
                background.color = new Color(0.04f, 0.04f, 0.05f, 0.82f);
            }

            Text label = _restoreCharmsButton.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.enabled = false;
            }

            GameObject iconObject = CreateUiObject(
                _restoreCharmsButton.transform,
                "RoundsRestoreAllCharmsIcon");
            RectTransform iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = Vector2.zero;
            iconRect.sizeDelta = new Vector2(30f, 30f);

            _restoreCharmsIcon = iconObject.AddComponent<VectorStrengthGraphic>();
            _restoreCharmsIcon.raycastTarget = false;
            _restoreCharmsIcon.color = Color.white;
            UpdateRestoreCharmsButton();
        }

        private void UpdateRestoreCharmsButton()
        {
            if (_restoreCharmsButton == null)
            {
                return;
            }

            bool connected = (_clientApi != null && _clientApi.NetClient != null && _clientApi.NetClient.IsConnected) ||
                             BenchPolicyController.IsConnected;
            bool canShow = _visible && !_closing && !_statisticsOnlyView && !connected &&
                           PvpModeClientController.CanRestoreAllLocalCharms;
            if (_restoreCharmsButton.gameObject.activeSelf != canShow)
            {
                _restoreCharmsButton.gameObject.SetActive(canShow);
            }
        }



        private void RestoreAllLocalCharms()
        {
            if (!PvpModeClientController.RestoreAllLocalCharms())
            {
                UpdateRestoreCharmsButton();
                return;
            }

            _statusMessage = "All charms have been granted for local play.";
            UpdateStatusBar();
            UpdateRestoreCharmsButton();
        }

        private Button CreateFlagButton(
            Transform parent,
            string objectName,
            VectorFlagGraphic.FlagKind flagKind,
            string caption,
            float x,
            UnityEngine.Events.UnityAction action)
        {
            Button button = CreateButton(parent, objectName, caption, action, 10);
            SetAnchored(
                button.GetComponent<RectTransform>(),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(x, -14f),
                new Vector2(46f, 34f));

            Text captionText = button.GetComponentInChildren<Text>();
            if (captionText != null)
            {
                captionText.enabled = false;
            }

            Image background = button.GetComponent<Image>();
            if (background != null)
            {
                background.color = new Color(0.04f, 0.04f, 0.05f, 0.75f);
            }

            GameObject iconObject = CreateUiObject(button.transform, objectName + "Image");
            RectTransform iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = Vector2.zero;
            iconRect.sizeDelta = new Vector2(40f, 28f);
            VectorFlagGraphic flag = iconObject.AddComponent<VectorFlagGraphic>();
            flag.Kind = flagKind;
            flag.color = Color.white;
            flag.raycastTarget = false;
            return button;
        }

        private void ChangeUiLanguage(string language)
        {
            RoundsLocalization.SetLanguage(language);
            ClientPreferencesStore.SetUiLanguage(language);
            if (_settings != null)
            {
                _settings.SetUiLanguage(language);
            }

            SetText(_mainTitle, "SETTINGS");
            SetText(_description, "PvP mode, round rules and shared bench");
            if (_visible)
            {
                RebuildRows();
            }
            UpdateStatusBar();
            if (_helpVisible)
            {
                PopulateHelpContent();
            }
        }

        private void CreateHelpWindow(Transform panel)
        {
            if (panel == null || panel.Find("RoundsHelpWindow") != null)
            {
                return;
            }

            _helpRoot = CreateUiObject(panel, "RoundsHelpWindow");
            RectTransform helpRect = _helpRoot.GetComponent<RectTransform>();
            helpRect.anchorMin = Vector2.zero;
            helpRect.anchorMax = Vector2.one;
            helpRect.pivot = new Vector2(0f, 1f);
            helpRect.offsetMin = new Vector2(10f, 50f);
            helpRect.offsetMax = new Vector2(-10f, -10f);

            Image backdrop = _helpRoot.AddComponent<Image>();
            backdrop.sprite = WhiteSprite;
            backdrop.color = new Color(0.035f, 0.035f, 0.045f, 0.985f);
            backdrop.raycastTarget = true;

            _helpTitle = CreateText(_helpRoot.transform, "RoundsHelpTitle", "Round settings help", 18, AccentTextColor);
            _helpTitle.fontStyle = FontStyle.Bold;
            SetAnchored(
                _helpTitle.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(0.75f, 1f),
                new Vector2(146f, -10f),
                new Vector2(0f, 36f));
            _helpTitle.alignment = TextAnchor.MiddleLeft;

            Button close = CreateButton(_helpRoot.transform, "RoundsHelpClose", "CLOSE", ToggleHelp, 12);
            SetAnchored(
                close.GetComponent<RectTransform>(),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-12f, -10f),
                new Vector2(92f, 32f));

            GameObject viewportObject = CreateUiObject(_helpRoot.transform, "RoundsHelpViewport");
            RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.pivot = new Vector2(0.5f, 0.5f);
            viewportRect.offsetMin = new Vector2(14f, 12f);
            viewportRect.offsetMax = new Vector2(-14f, -54f);
            Image viewportImage = viewportObject.AddComponent<Image>();
            viewportImage.sprite = WhiteSprite;
            viewportImage.color = new Color(0f, 0f, 0f, 0.12f);
            viewportImage.raycastTarget = true;
            Mask mask = viewportObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            GameObject contentObject = CreateUiObject(viewportObject.transform, "RoundsHelpContent");
            _helpContentRoot = contentObject.GetComponent<RectTransform>();
            _helpContentRoot.anchorMin = new Vector2(0f, 1f);
            _helpContentRoot.anchorMax = new Vector2(1f, 1f);
            _helpContentRoot.pivot = new Vector2(0.5f, 1f);
            _helpContentRoot.anchoredPosition = Vector2.zero;
            _helpContentRoot.sizeDelta = Vector2.zero;
            VerticalLayoutGroup layout = contentObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = contentObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _helpScrollRect = viewportObject.AddComponent<ScrollRect>();
            _helpScrollRect.content = _helpContentRoot;
            _helpScrollRect.viewport = viewportRect;
            _helpScrollRect.horizontal = false;
            _helpScrollRect.vertical = true;
            _helpScrollRect.movementType = ScrollRect.MovementType.Clamped;
            _helpScrollRect.scrollSensitivity = 32f;
            _helpRoot.SetActive(false);
        }

        private void ToggleHelp()
        {
            _helpVisible = !_helpVisible;
            if (_helpRoot != null)
            {
                _helpRoot.SetActive(_helpVisible);
            }

            if (_helpVisible)
            {
                PopulateHelpContent(true);
                if (_russianLanguageButton != null) _russianLanguageButton.transform.SetAsLastSibling();
                if (_englishLanguageButton != null) _englishLanguageButton.transform.SetAsLastSibling();
                if (_helpButton != null) _helpButton.transform.SetAsLastSibling();
                ForceCursorVisible();
            }
        }

        private void PopulateHelpContent(bool refreshFromRepository = false)
        {
            if (_helpContentRoot == null)
            {
                return;
            }

            SetText(_helpTitle, "Round settings help");
            ClearHelpContent();

            if (refreshFromRepository)
            {
                _helpReadmeContent = null;
                _helpReadmeError = null;
                _helpReadmeLoading = true;
                int requestId = ++_helpReadmeRequestId;
                AddHelpText(GetHelpLoadingMessage(), 14, false);
                StartCoroutine(DownloadHelpReadme(requestId));
                return;
            }

            if (_helpReadmeLoading)
            {
                AddHelpText(GetHelpLoadingMessage(), 14, false);
                return;
            }

            if (!string.IsNullOrWhiteSpace(_helpReadmeContent))
            {
                RenderHelpReadme(_helpReadmeContent);
                FinishHelpContentLayout();
                return;
            }

            if (!string.IsNullOrWhiteSpace(_helpReadmeError))
            {
                AddHelpText(GetHelpUnavailableMessage(_helpReadmeError), 14, false);
                FinishHelpContentLayout();
                return;
            }

            _helpReadmeLoading = true;
            int fallbackRequestId = ++_helpReadmeRequestId;
            AddHelpText(GetHelpLoadingMessage(), 14, false);
            StartCoroutine(DownloadHelpReadme(fallbackRequestId));
        }

        private void ClearHelpContent()
        {
            if (_helpContentRoot == null)
            {
                return;
            }

            for (int i = _helpContentRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = _helpContentRoot.GetChild(i);
                if (child != null)
                {
                    Object.Destroy(child.gameObject);
                }
            }
        }

        private IEnumerator DownloadHelpReadme(int requestId)
        {
            WWW request = null;
            string startError = null;
            try
            {
                request = new WWW(HelpReadmeUrl);
            }
            catch (Exception exception)
            {
                startError = exception.Message;
            }

            if (request == null)
            {
                CompleteHelpReadmeRequest(requestId, null, startError);
                yield break;
            }

            yield return request;

            string requestError = request.error;
            string readme = request.text;
            request.Dispose();
            CompleteHelpReadmeRequest(requestId, readme, requestError);
        }

        private void CompleteHelpReadmeRequest(int requestId, string readme, string error)
        {
            if (requestId != _helpReadmeRequestId)
            {
                return;
            }

            _helpReadmeLoading = false;
            if (!string.IsNullOrEmpty(error))
            {
                _helpReadmeContent = null;
                _helpReadmeError = error;
                Modding.Logger.LogError(
                    "[HKMP.Rounds] Could not download README.md from GitHub: " + error);
            }
            else if (string.IsNullOrWhiteSpace(readme))
            {
                _helpReadmeContent = null;
                _helpReadmeError = "The repository returned an empty README.md.";
                Modding.Logger.LogError(
                    "[HKMP.Rounds] GitHub returned an empty README.md.");
            }
            else
            {
                _helpReadmeContent = readme;
                _helpReadmeError = null;
            }

            if (_helpVisible && _helpContentRoot != null)
            {
                PopulateHelpContent();
            }
        }

        private static string GetHelpLoadingMessage()
        {
            return string.Equals(RoundsLocalization.Language, "ru", StringComparison.OrdinalIgnoreCase)
                ? "Загрузка справки из GitHub..."
                : "Loading help from GitHub...";
        }

        private static string GetHelpUnavailableMessage(string reason)
        {
            string prefix = string.Equals(RoundsLocalization.Language, "ru", StringComparison.OrdinalIgnoreCase)
                ? "Не удалось загрузить README из GitHub: "
                : "Unable to load README from GitHub: ";
            return prefix + (reason ?? "Unknown network error.");
        }

        private void RenderHelpReadme(string markdown)
        {
            string normalized = (markdown ?? string.Empty)
                .Replace("\r\n", "\n")
                .Replace('\r', '\n');
            string[] lines = normalized.Split('\n');
            List<string> selectedLines = SelectHelpReadmeLines(lines);

            bool insideCodeBlock = false;
            List<string> codeLines = new List<string>();
            for (int i = 0; i < selectedLines.Count; i++)
            {
                string line = selectedLines[i] ?? string.Empty;
                string trimmed = line.TrimStart();
                if (trimmed.StartsWith("```", StringComparison.Ordinal))
                {
                    if (insideCodeBlock)
                    {
                        AddHelpCodeBlock(codeLines);
                        codeLines.Clear();
                        insideCodeBlock = false;
                    }
                    else
                    {
                        insideCodeBlock = true;
                    }
                    continue;
                }

                if (insideCodeBlock)
                {
                    codeLines.Add(line);
                }
                else
                {
                    AddHelpMarkdownLine(line
                        .Replace("F6", "{MENU_KEY}")
                        .Replace("Alpha2", "{STATS_KEY}"));
                }
            }

            if (insideCodeBlock && codeLines.Count > 0)
            {
                AddHelpCodeBlock(codeLines);
            }
        }

        private List<string> SelectHelpReadmeLines(string[] lines)
        {
            List<string> selected = new List<string>();
            int targetHeadingIndex = -1;
            int firstLanguageHeadingIndex = -1;
            bool russian = string.Equals(
                RoundsLocalization.Language, "ru", StringComparison.OrdinalIgnoreCase);
            string targetLanguage = russian ? "ru" : "en";

            for (int i = 0; i < lines.Length; i++)
            {
                string language = GetHelpReadmeHeadingLanguage(lines[i]);
                if (language == null)
                {
                    continue;
                }

                if (firstLanguageHeadingIndex < 0)
                {
                    firstLanguageHeadingIndex = i;
                }

                if (targetHeadingIndex < 0 &&
                    string.Equals(language, targetLanguage, StringComparison.Ordinal))
                {
                    targetHeadingIndex = i;
                }
            }

            // If this README has no section for the active language, keep the full document.
            if (targetHeadingIndex < 0)
            {
                selected.AddRange(lines);
                return selected;
            }

            // Keep the shared title and introduction before the first language heading.
            if (firstLanguageHeadingIndex > 0)
            {
                for (int i = 0; i < firstLanguageHeadingIndex; i++)
                {
                    selected.Add(lines[i]);
                }
            }

            int sectionEnd = lines.Length;
            for (int i = targetHeadingIndex + 1; i < lines.Length; i++)
            {
                if (GetHelpReadmeHeadingLanguage(lines[i]) != null)
                {
                    sectionEnd = i;
                    break;
                }
            }

            for (int i = targetHeadingIndex + 1; i < sectionEnd; i++)
            {
                selected.Add(lines[i]);
            }

            while (selected.Count > 0)
            {
                string trailingLine = (selected[selected.Count - 1] ?? string.Empty).Trim();
                if (trailingLine.Length == 0 ||
                    Regex.IsMatch(trailingLine, @"^(---+|\*\*\*+|___+)\s*$"))
                {
                    selected.RemoveAt(selected.Count - 1);
                    continue;
                }

                break;
            }

            return selected;
        }

        private static string GetHelpReadmeHeadingLanguage(string line)
        {
            string heading = (line ?? string.Empty).Trim();
            if (heading.Equals("# English", StringComparison.OrdinalIgnoreCase) ||
                heading.Equals("## English", StringComparison.OrdinalIgnoreCase))
            {
                return "en";
            }

            if (heading.Equals("# Russian", StringComparison.OrdinalIgnoreCase) ||
                heading.Equals("## Russian", StringComparison.OrdinalIgnoreCase) ||
                heading.Equals("# Русский", StringComparison.OrdinalIgnoreCase) ||
                heading.Equals("## Русский", StringComparison.OrdinalIgnoreCase))
            {
                return "ru";
            }

            return null;
        }

        private void AddHelpCodeBlock(IList<string> lines)
        {
            GameObject codeRoot = CreateUiObject(_helpContentRoot, "HelpCodeBlock");
            Image background = codeRoot.AddComponent<Image>();
            background.sprite = WhiteSprite;
            background.color = new Color(0.035f, 0.045f, 0.06f, 1f);
            background.raycastTarget = false;

            VerticalLayoutGroup layout = codeRoot.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 7, 7);
            layout.spacing = 2f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            LayoutElement rootLayout = codeRoot.AddComponent<LayoutElement>();
            rootLayout.flexibleWidth = 1f;

            string code = string.Join("\n", lines.ToArray());
            Text codeText = CreateText(
                codeRoot.transform,
                "HelpCodeText",
                EscapeRichText(code),
                13,
                new Color(0.78f, 0.83f, 0.91f, 1f));
            codeText.text = EscapeRichText(code);
            codeText.fontStyle = FontStyle.Normal;
            codeText.alignment = TextAnchor.UpperLeft;
            codeText.horizontalOverflow = HorizontalWrapMode.Wrap;
            codeText.verticalOverflow = VerticalWrapMode.Overflow;
            codeText.supportRichText = false;

            ContentSizeFitter textFitter = codeText.gameObject.AddComponent<ContentSizeFitter>();
            textFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            textFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ContentSizeFitter rootFitter = codeRoot.AddComponent<ContentSizeFitter>();
            rootFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            rootFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private void FinishHelpContentLayout()
        {
            Canvas.ForceUpdateCanvases();
            if (_helpContentRoot != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(_helpContentRoot);
            }
            if (_helpScrollRect != null)
            {
                _helpScrollRect.verticalNormalizedPosition = 1f;
            }
        }

        private void AddHelpMarkdownLine(string sourceLine)
        {
            string rawLine = sourceLine ?? string.Empty;
            string line = rawLine.TrimEnd();
            string trimmedLine = line.Trim();
            if (trimmedLine.Length == 0)
            {
                GameObject spacer = CreateUiObject(_helpContentRoot, "HelpSpacer");
                LayoutElement spacerLayout = spacer.AddComponent<LayoutElement>();
                spacerLayout.minHeight = 6f;
                spacerLayout.preferredHeight = 6f;
                return;
            }

            Match imageMatch = Regex.Match(
                trimmedLine,
                @"^!\[(?<alt>[^\]]*)\]\((?:<(?<source>[^>]+)>|(?<source>[^)\s]+))(?:\s+(?:""[^""]*""|'[^']*'))?\)$");
            if (imageMatch.Success)
            {
                AddHelpImage(
                    imageMatch.Groups["alt"].Value,
                    imageMatch.Groups["source"].Value.Trim());
                return;
            }

            if (Regex.IsMatch(trimmedLine, @"^(---+|\*\*\*+|___+)\s*$"))
            {
                AddHelpText("────────────────────────", 13, false);
                return;
            }

            Match headingMatch = Regex.Match(trimmedLine, @"^(?<level>#{1,6})\s+(?<text>.*)$");
            if (headingMatch.Success)
            {
                int level = headingMatch.Groups["level"].Value.Length;
                int fontSize = level == 1 ? 22 : level == 2 ? 20 : level == 3 ? 18 : 16;
                AddHelpText(headingMatch.Groups["text"].Value, fontSize, true);
                return;
            }

            Match quoteMatch = Regex.Match(trimmedLine, @"^>\s?(?<text>.*)$");
            if (quoteMatch.Success)
            {
                AddHelpText("│  " + quoteMatch.Groups["text"].Value, 14, false);
                return;
            }

            Match unorderedListMatch = Regex.Match(line, @"^(?<indent>\s*)[-*+]\s+(?<text>.*)$");
            if (unorderedListMatch.Success)
            {
                int indent = Mathf.Clamp(unorderedListMatch.Groups["indent"].Value.Length / 2, 0, 4);
                string prefix = new string(' ', indent * 3) + "•  ";
                AddHelpText(prefix + unorderedListMatch.Groups["text"].Value, 15, false);
                return;
            }

            Match orderedListMatch = Regex.Match(line, @"^(?<indent>\s*)(?<number>\d+)[.)]\s+(?<text>.*)$");
            if (orderedListMatch.Success)
            {
                int indent = Mathf.Clamp(orderedListMatch.Groups["indent"].Value.Length / 2, 0, 4);
                string prefix = new string(' ', indent * 3) + orderedListMatch.Groups["number"].Value + ".  ";
                AddHelpText(prefix + orderedListMatch.Groups["text"].Value, 15, false);
                return;
            }

            AddHelpText(line, 16, false);
        }

        private void AddHelpImage(string alt, string imageSource)
        {
            GameObject imageObject = CreateUiObject(_helpContentRoot, "HelpImage");
            Image image = imageObject.AddComponent<Image>();
            image.enabled = false;
            image.preserveAspect = true;
            image.raycastTarget = false;

            Text fallbackText = CreateText(
                imageObject.transform,
                "HelpImageFallback",
                string.Empty,
                13,
                MutedTextColor);
            RectTransform fallbackRect = fallbackText.rectTransform;
            fallbackRect.anchorMin = Vector2.zero;
            fallbackRect.anchorMax = Vector2.one;
            fallbackRect.offsetMin = Vector2.zero;
            fallbackRect.offsetMax = Vector2.zero;
            fallbackText.alignment = TextAnchor.MiddleCenter;
            fallbackText.horizontalOverflow = HorizontalWrapMode.Wrap;
            fallbackText.verticalOverflow = VerticalWrapMode.Overflow;
            fallbackText.raycastTarget = false;
            fallbackText.gameObject.SetActive(false);

            LayoutElement imageLayout = imageObject.AddComponent<LayoutElement>();
            imageLayout.preferredWidth = 360f;
            imageLayout.preferredHeight = 42f;
            imageLayout.minHeight = 36f;
            imageLayout.flexibleWidth = 1f;
            StartCoroutine(LoadHelpImageRoutine(
                imageSource, imageObject, image, imageLayout, fallbackText));

            if (!string.IsNullOrWhiteSpace(alt))
            {
                AddHelpText(alt, 15, true);
            }
        }

        private IEnumerator LoadHelpImageRoutine(
            string imageSource,
            GameObject imageObject,
            Image image,
            LayoutElement imageLayout,
            Text fallbackText)
        {
            string imageUrl = ResolveHelpImageUrl(imageSource);
            if (string.IsNullOrEmpty(imageUrl))
            {
                ShowHelpImageFallback(imageObject, imageLayout, fallbackText);
                yield break;
            }

            Sprite sprite;
            if (HelpImageCache.TryGetValue(imageUrl, out sprite) && sprite != null)
            {
                ApplyHelpImageSprite(imageObject, image, imageLayout, fallbackText, sprite);
                yield break;
            }

            WWW request = null;
            try
            {
                request = new WWW(imageUrl);
            }
            catch (Exception exception)
            {
                Modding.Logger.Log(
                    "[HKMP.Rounds] Could not start help image download '" + imageUrl + "': " +
                    exception.Message);
            }

            if (request == null)
            {
                ShowHelpImageFallback(imageObject, imageLayout, fallbackText);
                yield break;
            }

            yield return request;
            string requestError = request.error;
            byte[] imageBytes = request.bytes;
            request.Dispose();

            if (imageObject == null)
            {
                yield break;
            }
            if (!string.IsNullOrEmpty(requestError) || imageBytes == null || imageBytes.Length == 0)
            {
                Modding.Logger.Log(
                    "[HKMP.Rounds] Could not download help image '" + imageUrl + "': " +
                    (requestError ?? "The response did not contain image data."));
                ShowHelpImageFallback(imageObject, imageLayout, fallbackText);
                yield break;
            }

            Texture2D texture = null;
            Sprite downloadedSprite = null;
            Exception imageException = null;
            try
            {
                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!ImageConversion.LoadImage(texture, imageBytes))
                {
                    throw new InvalidDataException("The response is not a supported image.");
                }

                texture.name = "HKMP.Rounds.HelpImage." + imageUrl.GetHashCode() + ".Texture";
                texture.filterMode = FilterMode.Bilinear;
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.hideFlags = HideFlags.HideAndDontSave;
                downloadedSprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
                downloadedSprite.name = "HKMP.Rounds.HelpImage." + imageUrl.GetHashCode();
            }
            catch (Exception exception)
            {
                imageException = exception;
            }

            if (downloadedSprite == null)
            {
                if (texture != null)
                {
                    Object.Destroy(texture);
                }
                Modding.Logger.Log(
                    "[HKMP.Rounds] Could not create help image '" + imageUrl + "': " +
                    (imageException != null ? imageException.Message : "Unknown error."));
                ShowHelpImageFallback(imageObject, imageLayout, fallbackText);
                yield break;
            }

            HelpImageCache[imageUrl] = downloadedSprite;
            ApplyHelpImageSprite(
                imageObject, image, imageLayout, fallbackText, downloadedSprite);
        }

        private void ApplyHelpImageSprite(
            GameObject imageObject,
            Image image,
            LayoutElement imageLayout,
            Text fallbackText,
            Sprite sprite)
        {
            if (imageObject == null || image == null || imageLayout == null || sprite == null)
            {
                return;
            }

            image.sprite = sprite;
            image.enabled = true;
            if (fallbackText != null)
            {
                fallbackText.gameObject.SetActive(false);
            }

            imageLayout.preferredHeight = Mathf.Clamp(
                360f * sprite.rect.height / Mathf.Max(1f, sprite.rect.width),
                70f,
                230f);
            imageLayout.minHeight = 70f;
            if (_helpContentRoot != null)
            {
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(_helpContentRoot);
            }
        }

        private void ShowHelpImageFallback(
            GameObject imageObject,
            LayoutElement imageLayout,
            Text fallbackText)
        {
            if (imageObject == null)
            {
                return;
            }

            if (imageLayout != null)
            {
                imageLayout.preferredHeight = 42f;
                imageLayout.minHeight = 36f;
            }
            if (fallbackText != null)
            {
                fallbackText.text = MarkdownInlineToRichText(
                    RoundsLocalization.Translate("Image unavailable."));
                fallbackText.gameObject.SetActive(true);
            }
            if (_helpContentRoot != null)
            {
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(_helpContentRoot);
            }
        }

        private static string ResolveHelpImageUrl(string imageSource)
        {
            string source = (imageSource ?? string.Empty).Trim();
            if (source.Length == 0)
            {
                return null;
            }

            Uri uri;
            if (Uri.TryCreate(source, UriKind.Absolute, out uri))
            {
                if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }
                if (string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
                {
                    string[] segments = uri.GetComponents(
                        UriComponents.Path, UriFormat.UriEscaped).Trim('/').Split('/');
                    if (segments.Length >= 5 &&
                        string.Equals(segments[2], "blob", StringComparison.OrdinalIgnoreCase))
                    {
                        return "https://raw.githubusercontent.com/" + segments[0] + "/" +
                               segments[1] + "/" + string.Join("/", segments.Skip(3).ToArray()) +
                               uri.Query + uri.Fragment;
                    }
                }
                return uri.AbsoluteUri;
            }

            source = source.Replace('\\', '/').TrimStart('/');
            while (source.StartsWith("./", StringComparison.Ordinal))
            {
                source = source.Substring(2);
            }

            try
            {
                Uri baseUri = new Uri(HelpRepositoryRawBaseUrl);
                Uri resolvedUri;
                if (source.Length == 0 ||
                    !Uri.TryCreate(baseUri, Uri.EscapeUriString(source), out resolvedUri) ||
                    !resolvedUri.AbsoluteUri.StartsWith(
                        HelpRepositoryRawBaseUrl, StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }
                return resolvedUri.AbsoluteUri;
            }
            catch (UriFormatException)
            {
                return null;
            }
        }

        private void AddHelpText(string source, int fontSize, bool bold)
        {
            string value = (source ?? string.Empty)
                .Replace("F6", "{MENU_KEY}")
                .Replace("Alpha2", "{STATS_KEY}");
            string prefix = string.Empty;
            if (value.StartsWith("•  ", StringComparison.Ordinal))
            {
                prefix = "•  ";
                value = value.Substring(prefix.Length);
            }

            value = RoundsLocalization.Translate(value);
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            value = prefix + value
                .Replace("{MENU_KEY}", HKMP.Rounds.HKMPRounds.MenuHotkeyDisplayName)
                .Replace("{STATS_KEY}", ClientPreferencesStore.FormatKeyCode(ClientPreferencesStore.StatsKey));
            Text text = CreateText(
                _helpContentRoot,
                "HelpText",
                MarkdownInlineToRichText(value),
                fontSize,
                Color.white);
            text.supportRichText = true;
            text.fontStyle = bold ? FontStyle.Bold : GetBodyFontStyle();
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            LayoutElement element = text.gameObject.AddComponent<LayoutElement>();
            element.minHeight = fontSize + 9f;
            element.flexibleWidth = 1f;
            ContentSizeFitter fitter = text.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            if (text.font != null && !string.IsNullOrEmpty(text.text))
            {
                text.font.RequestCharactersInTexture(text.text, text.fontSize, text.fontStyle);
            }
        }

        private static string MarkdownInlineToRichText(string value)
        {
            string result = EscapeRichText(value ?? string.Empty);
            result = Regex.Replace(result, @"`([^`]+)`", "<color=#C8D2E6>$1</color>");
            result = Regex.Replace(result, @"\*\*(.+?)\*\*", "<b>$1</b>");
            result = Regex.Replace(result, @"(?<!\*)\*([^*]+)\*(?!\*)", "<i>$1</i>");
            result = Regex.Replace(result, @"~~(.+?)~~", "<color=#9AA4B2>$1</color>");
            result = Regex.Replace(
                result,
                @"\[([^\]]+)\]\(([^)\s]+)\)",
                "<color=#86B7F5>$1</color> ($2)");
            return result;
        }

        private static string EscapeRichText(string value)
        {
            return (value ?? string.Empty)
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;");
        }

        private void SetupScrollableContent(Transform buttonsPanel)
        {
            RectTransform viewport = buttonsPanel as RectTransform;
            if (viewport == null)
            {
                return;
            }

            Component[] components = buttonsPanel.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null || component is Transform || component is RectTransform ||
                    component is Image || component is CanvasRenderer || component is ScrollRect ||
                    component is RectMask2D || component is Scrollbar)
                {
                    continue;
                }

                Type type = component.GetType();
                if (type.Name == "MenuButtonList" ||
                    component is LayoutGroup ||
                    component is ContentSizeFitter)
                {
                    Behaviour behaviour = component as Behaviour;
                    if (behaviour != null)
                    {
                        behaviour.enabled = false;
                    }
                }
            }

            for (int i = buttonsPanel.childCount - 1; i >= 0; i--)
            {
                Transform child = buttonsPanel.GetChild(i);
                if (child != null)
                {
                    child.gameObject.SetActive(false);
                    Object.DestroyImmediate(child.gameObject);
                }
            }

            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.pivot = new Vector2(0.5f, 0.5f);
            viewport.offsetMin = new Vector2(34f, 68f);
            viewport.offsetMax = new Vector2(-34f, -292f);

            Image viewportImage = buttonsPanel.GetComponent<Image>();
            if (viewportImage == null)
            {
                viewportImage = buttonsPanel.gameObject.AddComponent<Image>();
            }

            viewportImage.sprite = WhiteSprite;
            viewportImage.color = new Color(0.015f, 0.015f, 0.02f, 0.24f);
            viewportImage.raycastTarget = true;

            RectMask2D mask = buttonsPanel.GetComponent<RectMask2D>();
            if (mask == null)
            {
                buttonsPanel.gameObject.AddComponent<RectMask2D>();
            }

            _scrollRect = buttonsPanel.GetComponent<ScrollRect>();
            if (_scrollRect == null)
            {
                _scrollRect = buttonsPanel.gameObject.AddComponent<ScrollRect>();
            }

            _scrollRect.horizontal = false;
            _scrollRect.vertical = true;
            _scrollRect.movementType = ScrollRect.MovementType.Clamped;
            _scrollRect.scrollSensitivity = 46f;
            _scrollRect.viewport = viewport;
            _scrollRect.inertia = true;
            _scrollRect.decelerationRate = 0.12f;

            GameObject contentObject = CreateUiObject(buttonsPanel, "RoundsSettingsContent");
            _content = contentObject.GetComponent<RectTransform>();
            _content.anchorMin = new Vector2(0f, 1f);
            _content.anchorMax = new Vector2(1f, 1f);
            _content.pivot = new Vector2(0.5f, 1f);
            _content.anchoredPosition = Vector2.zero;
            _content.offsetMin = new Vector2(0f, 0f);
            _content.offsetMax = new Vector2(-18f, 0f);
            _content.sizeDelta = Vector2.zero;

            VerticalLayoutGroup vertical = contentObject.AddComponent<VerticalLayoutGroup>();
            vertical.padding = new RectOffset(8, 8, 8, 8);
            vertical.spacing = 8f;
            vertical.childAlignment = TextAnchor.UpperCenter;
            vertical.childControlWidth = true;
            vertical.childControlHeight = true;
            vertical.childForceExpandWidth = true;
            vertical.childForceExpandHeight = false;

            ContentSizeFitter fitter = contentObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _scrollRect.content = _content;

            GameObject scrollbarObject = CreateUiObject(buttonsPanel, "RoundsVerticalScrollbar");
            RectTransform scrollbarRect = scrollbarObject.GetComponent<RectTransform>();
            scrollbarRect.anchorMin = new Vector2(1f, 0f);
            scrollbarRect.anchorMax = new Vector2(1f, 1f);
            scrollbarRect.pivot = new Vector2(1f, 0.5f);
            scrollbarRect.sizeDelta = new Vector2(12f, -8f);
            scrollbarRect.anchoredPosition = new Vector2(-4f, 0f);

            Image scrollbarBackground = scrollbarObject.AddComponent<Image>();
            scrollbarBackground.sprite = WhiteSprite;
            scrollbarBackground.color = new Color(0.08f, 0.08f, 0.10f, 0.78f);
            scrollbarBackground.raycastTarget = true;

            GameObject handleObject = CreateUiObject(scrollbarObject.transform, "Handle");
            RectTransform handleRect = handleObject.GetComponent<RectTransform>();
            handleRect.anchorMin = Vector2.zero;
            handleRect.anchorMax = Vector2.one;
            handleRect.offsetMin = new Vector2(1f, 1f);
            handleRect.offsetMax = new Vector2(-1f, -1f);

            Image handleImage = handleObject.AddComponent<Image>();
            handleImage.sprite = WhiteSprite;
            handleImage.color = new Color(0.70f, 0.70f, 0.76f, 0.9f);
            handleImage.raycastTarget = true;

            _verticalScrollbar = scrollbarObject.AddComponent<Scrollbar>();
            _verticalScrollbar.handleRect = handleRect;
            _verticalScrollbar.targetGraphic = handleImage;
            _verticalScrollbar.direction = Scrollbar.Direction.BottomToTop;
            ColorBlock scrollbarColors = _verticalScrollbar.colors;
            scrollbarColors.normalColor = new Color(0.70f, 0.70f, 0.76f, 0.8f);
            scrollbarColors.highlightedColor = Color.white;
            scrollbarColors.pressedColor = new Color(0.86f, 0.86f, 0.92f, 1f);
            scrollbarColors.fadeDuration = 0.08f;
            _verticalScrollbar.colors = scrollbarColors;
            _scrollRect.verticalScrollbar = _verticalScrollbar;
            _scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            _scrollRect.verticalScrollbarSpacing = 3f;
        }

        private void ConfigureCanvasCamera()
        {
            if (_canvas == null)
            {
                return;
            }

            if (GameCameras.instance != null && GameCameras.instance.hudCamera != null &&
                _canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                _canvas.worldCamera = GameCameras.instance.hudCamera;
            }

            _canvas.pixelPerfect = true;
        }

        private void Show()
        {
            if (_visible || _root == null || RoundPreparation.IsGameplayTransitionPending ||
                EmergencyBenchWarpController.IsWarpRunning || PhantomMapClientController.IsDeathReturnPending)
            {
                return;
            }

            GameManager gameManager = GameManager.instance;
            HeroController hero = HeroController.instance;
            if ((gameManager != null && gameManager.IsInSceneTransition) ||
                (hero != null && hero.cState != null && hero.cState.dead))
            {
                return;
            }

            _openedWhilePaused = gameManager != null && gameManager.isPaused;
            _helpVisible = false;
            if (_helpRoot != null)
            {
                _helpRoot.SetActive(false);
            }

            _visible = true;
            _closing = false;
            _sceneDiscoveryPending = true;
            _nextSceneDiscoveryAt = Time.unscaledTime + 1f;
            _instance = this;
            _cursorWasVisible = Cursor.visible;
            _cursorWasLocked = Cursor.lockState;
            BeginCursorOverride();
            TakeInputFromOtherUi();
            EnsureEventSystem();

            if (_canvasGroup != null)
            {
                _canvasGroup.interactable = true;
                _canvasGroup.blocksRaycasts = true;
                _canvasGroup.alpha = 1f;
            }

            _root.SetActive(true);
            NormalizeChildCanvasGroups();
            EnsureEventSystem();

            if (_windowRect != null)
            {
                _windowRect.localScale = _windowBaseScale;
            }

            Canvas.ForceUpdateCanvases();
            if (_network == null || !BenchPolicyController.IsConnected)
            {
                _canManageBench = false;
                _canManageMapTravel = false;
                _canManageSettings = false;
                _canManagePvpMode = false;
                _statusMessage = "Local mode: preset maps and selected-bench warp are available without a host.";
            }
            else if (string.IsNullOrWhiteSpace(_statusMessage))
            {
                _statusMessage = "Connected to HKMP. Loading server settings...";
            }

            if (!_openedWhilePaused &&
                GameCameras.instance != null &&
                GameCameras.instance.hudCanvas != null)
            {
                FSMUtility.SendEventToGameObject(GameCameras.instance.hudCanvas, "OUT");
                _hudHiddenByRoundsUi = true;
            }

            RebuildRows();
            Canvas.ForceUpdateCanvases();
            if (_content != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
            }
            EnsureEventSystem();
            KeepInputOnWindow();
            FocusFirstInteractableControl();
            UpdateStatusBar();
            ForceCursorVisible();
            if (_showBuildSequence != null)
            {
                StopCoroutine(_showBuildSequence);
            }
            _showBuildSequence = StartCoroutine(CompleteShowAfterFirstFrame());
        }

        private IEnumerator CompleteShowAfterFirstFrame()
        {
            yield return null;

            if (!_visible || _closing || _root == null)
            {
                _showBuildSequence = null;
                yield break;
            }

            if (_network != null && BenchPolicyController.IsConnected)
            {
                _network.RequestBenchSettings("[]");
                _network.RequestPvpMode();
                _network.RequestRoundSettings();
                RequestMapTravelState(_phantomMapPage);
                if (_statisticsOnlyView)
                {
                    _network.RequestRoundStats();
                }
            }
            UpdateStatusBar();
            _showBuildSequence = null;
        }


        private void Hide()
        {
            if (!_visible || _closing)
            {
                return;
            }

            _bindingCaptureTarget = ClientBindingTarget.None;
            _closing = true;
            if (_showBuildSequence != null)
            {
                StopCoroutine(_showBuildSequence);
                _showBuildSequence = null;
            }
            if (_canvasGroup != null)
            {
                _canvasGroup.interactable = false;
            }

            if (_hideSequence != null)
            {
                StopCoroutine(_hideSequence);
            }
            _hideSequence = StartCoroutine(HideSequence());
        }

        private IEnumerator HideSequence()
        {
            if (HasWindowAnimations())
            {
                _windowAnimator.enabled = true;
                _windowAnimator.Play("Close", 0, 0f);
                yield return null;

                float animationLength =
                    Mathf.Max(0f, _windowAnimator.GetCurrentAnimatorStateInfo(0).length);
                float elapsed = 0f;

                while (elapsed < animationLength && _root != null)
                {
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }

                _windowAnimator.enabled = false;
            }
            else
            {
                if (_windowAnimator != null)
                {
                    _windowAnimator.enabled = false;
                }

                const float duration = 0.14f;
                float elapsed = 0f;
                Vector3 startScale = _windowBaseScale;
                Vector3 endScale = _windowBaseScale * 0.97f;
                float startAlpha = _canvasGroup != null ? _canvasGroup.alpha : 1f;
                if (_windowRect != null)
                {
                    startScale = _windowRect.localScale;
                    endScale = _windowBaseScale * 0.97f;
                }

                while (elapsed < duration && _root != null)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                    if (_canvasGroup != null)
                    {
                        _canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);
                    }
                    if (_windowRect != null)
                    {
                        _windowRect.localScale = Vector3.LerpUnclamped(startScale, endScale, t);
                    }
                    yield return null;
                }
            }

            if (_root != null)
            {
                _root.SetActive(false);
            }

            EndCursorOverride();
            _visible = false;
            _closing = false;
            _statisticsOnlyView = false;
            _openedWhilePaused = false;
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = false;
            }
            if (_windowRect != null)
            {
                _windowRect.localScale = _windowBaseScale;
            }
            RestoreEventSystemState();
            RestoreOtherUiSelection();

            if (_hudHiddenByRoundsUi &&
                GameCameras.instance != null &&
                GameCameras.instance.hudCanvas != null)
            {
                FSMUtility.SendEventToGameObject(GameCameras.instance.hudCanvas, "IN");
                _hudHiddenByRoundsUi = false;
            }

            _hideSequence = null;
        }

        private bool HasWindowAnimations()
        {
            if (_windowAnimator == null || _windowAnimator.runtimeAnimatorController == null)
            {
                return false;
            }

            bool hasOpenState =
                _windowAnimator.HasState(0, Animator.StringToHash("Base Layer.Open")) ||
                _windowAnimator.HasState(0, Animator.StringToHash("Open"));
            bool hasCloseState =
                _windowAnimator.HasState(0, Animator.StringToHash("Base Layer.Close")) ||
                _windowAnimator.HasState(0, Animator.StringToHash("Close"));

            return hasOpenState && hasCloseState;
        }

        public static void CloseForGameplayTransition()
        {
            if (_instance != null)
            {
                _instance.HideImmediatelyForGameplayTransition();
            }
        }

        private void HideImmediatelyForGameplayTransition()
        {
            if (!_visible && !_closing)
            {
                return;
            }

            if (_showBuildSequence != null)
            {
                StopCoroutine(_showBuildSequence);
                _showBuildSequence = null;
            }
            if (_hideSequence != null)
            {
                StopCoroutine(_hideSequence);
                _hideSequence = null;
            }
            _closing = false;
            _visible = false;
            _bindingCaptureTarget = ClientBindingTarget.None;
            if (_root != null)
            {
                _root.SetActive(false);
            }
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = false;
            }
            if (_windowRect != null)
            {
                _windowRect.localScale = _windowBaseScale;
            }

            _openedWhilePaused = false;
            _statisticsOnlyView = false;
            EndCursorOverride();
            RestoreEventSystemState();
            RestoreOtherUiSelection();

            if (_hudHiddenByRoundsUi &&
                GameCameras.instance != null &&
                GameCameras.instance.hudCanvas != null)
            {
                FSMUtility.SendEventToGameObject(GameCameras.instance.hudCanvas, "IN");
                _hudHiddenByRoundsUi = false;
            }
        }

        private void RebuildRows()
        {
            if (_content == null)
            {
                return;
            }

            RemoveUnavailableBenchSelections();

            RectTransform previousContent = _content;
            float previousScrollPosition = _scrollRect != null
                ? _scrollRect.verticalNormalizedPosition
                : 1f;
            EventSystem selectionSystem = IsUsableEventSystem(_eventSystem)
                ? _eventSystem
                : EventSystem.current;
            GameObject previousSelection = selectionSystem != null
                ? selectionSystem.currentSelectedGameObject
                : null;
            InputField previousInput = previousSelection != null
                ? previousSelection.GetComponentInParent<InputField>()
                : null;
            Transform selectionTarget = previousInput != null
                ? previousInput.transform
                : previousSelection != null ? previousSelection.transform : null;
            string selectionPath = GetContentChildPath(previousContent, selectionTarget);
            int previousCaret = previousInput != null ? previousInput.caretPosition : 0;
            bool restoreInputFocus = previousInput != null && previousInput.isFocused;
            List<BenchOptionRow> previousBenchRows = new List<BenchOptionRow>(_benchOptionRows);
            GameObject previousNoResultsObject = _benchNoResultsObject;
            RectTransform replacementContent = CreateContentBuffer(previousContent);
            replacementContent.gameObject.SetActive(false);
            _content = replacementContent;
            _benchOptionRows.Clear();
            _benchNoResultsObject = null;

            try
            {
                BuildRows(previousScrollPosition);
            }
            catch (Exception exception)
            {
                _content = previousContent;
                _benchOptionRows.Clear();
                _benchOptionRows.AddRange(previousBenchRows);
                _benchNoResultsObject = previousNoResultsObject;
                Object.Destroy(replacementContent.gameObject);
                Modding.Logger.LogError("[HKMP.Rounds] Could not rebuild the settings view: " + exception);
                return;
            }

            if (_scrollRect != null)
            {
                _scrollRect.content = replacementContent;
            }

            replacementContent.gameObject.SetActive(true);
            previousContent.gameObject.SetActive(false);
            _content = replacementContent;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(replacementContent);
            if (_scrollRect != null)
            {
                _scrollRect.verticalNormalizedPosition = Mathf.Clamp01(previousScrollPosition);
            }

            if (selectionSystem != null && !string.IsNullOrEmpty(selectionPath))
            {
                Transform restoredTransform = ResolveContentChildPath(replacementContent, selectionPath);
                GameObject restoredSelection = restoredTransform != null ? restoredTransform.gameObject : null;
                if (restoredSelection != null && restoredSelection.activeInHierarchy)
                {
                    selectionSystem.SetSelectedGameObject(restoredSelection);
                    InputField restoredInput = restoredSelection.GetComponent<InputField>();
                    if (restoreInputFocus && restoredInput != null && restoredInput.interactable)
                    {
                        restoredInput.Select();
                        restoredInput.ActivateInputField();
                        restoredInput.caretPosition = Mathf.Clamp(previousCaret, 0, restoredInput.text.Length);
                        restoredInput.ForceLabelUpdate();
                    }
                }
            }

            Object.Destroy(previousContent.gameObject);
        }

        private void BuildRows(float previousScrollPosition)
        {
            SetText(_mainTitle, _statisticsOnlyView ? "STATISTICS" : "SETTINGS");
            SetText(_description, _statisticsOnlyView
                ? "Live match statistics"
                : "PvP mode, round rules, maps and benches");

            if (_statisticsOnlyView)
            {
                RebuildStatisticsRows(previousScrollPosition);
                return;
            }

            CreatePvpModeSelectorRow();

            CreateSectionLabel("MAPS");
            CreateMapPresetGallery();
            CreateVerticalSpacer(18f, "PresetMapGalleryBottomSpacer");

            if (_canManageMapTravel)
            {
                CreateSettingRow("Allow players to travel between maps", _mapTravelEnabled, delegate
                {
                    if (_network == null || !BenchPolicyController.IsConnected)
                    {
                        _statusMessage = "Connect to the HKMP host to change map travel.";
                    }
                    else
                    {
                        _network.SetMapTravelEnabled(
                            !_mapTravelEnabled,
                            GameManager.instance != null ? GameManager.instance.sceneName : string.Empty,
                            SceneTravelCatalog.BuildCurrentSceneJson());
                        _statusMessage = "Updating map travel setting...";
                    }
                    UpdateStatusBar();
                });

                CreateActionRow("CREATE PHANTOM MANTIS VILLAGE", delegate
                {
                    if (_network == null || !BenchPolicyController.IsConnected)
                    {
                        _statusMessage = "Connect to the HKMP host to create a phantom map.";
                    }
                    else
                    {
                        _network.CreatePhantomMantis(
                            GameManager.instance != null ? GameManager.instance.sceneName : string.Empty,
                            SceneTravelCatalog.BuildCurrentSceneJson());
                        _statusMessage = "Creating a new phantom Mantis map...";
                    }
                    UpdateStatusBar();
                });
            }

            if (BenchPolicyController.IsConnected)
            {
                if (_mapTravelEnabled || _canManageMapTravel)
                {
                    if (_phantomMantisMaps.Count == 0)
                    {
                        CreateInfoRow("No phantom Mantis maps have been created.");
                    }
                    else
                    {
                        for (int i = 0; i < _phantomMantisMaps.Count; i++)
                        {
                            CreatePhantomMantisRow(_phantomMantisMaps[i]);
                        }

                        if (_phantomMapPageCount > 1)
                        {
                            CreateActionRow("PHANTOM MAP PAGE " + (_phantomMapPage + 1) + " / " + _phantomMapPageCount, delegate
                            {
                                _phantomMapPage = (_phantomMapPage + 1) % _phantomMapPageCount;
                                RequestMapTravelState(_phantomMapPage);
                            });
                            CreateActionRow("PREVIOUS PHANTOM MAP PAGE", delegate
                            {
                                _phantomMapPage = (_phantomMapPage - 1 + _phantomMapPageCount) % _phantomMapPageCount;
                                RequestMapTravelState(_phantomMapPage);
                            });
                        }
                    }

                }
            }

            CreateSectionLabel("HOST & CONTROLS");
            CreateBindingRow(
                "Ready hotkey (/rd)",
                ClientPreferencesStore.FormatKeyCode(ClientPreferencesStore.ReadyKey),
                _bindingCaptureTarget == ClientBindingTarget.Ready,
                delegate { BeginBindingCapture(ClientBindingTarget.Ready); });
            CreateBindingRow(
                "Statistics overlay hotkey",
                ClientPreferencesStore.FormatKeyCode(ClientPreferencesStore.StatsKey),
                _bindingCaptureTarget == ClientBindingTarget.Stats,
                delegate { BeginBindingCapture(ClientBindingTarget.Stats); });
            CreateBindingRow(
                "Ready HUD (tap / hold to edit and resize)",
                ClientPreferencesStore.FormatKeyCode(ClientPreferencesStore.ReadyHudKey),
                _bindingCaptureTarget == ClientBindingTarget.ReadyHud,
                delegate { BeginBindingCapture(ClientBindingTarget.ReadyHud); });
            if (_canManageSettings)
            {
                CreateBindingRow(
                    "Freeze / unfreeze game",
                    ClientPreferencesStore.FormatKeyCode(ClientPreferencesStore.FreezeKey),
                    _bindingCaptureTarget == ClientBindingTarget.Freeze,
                    delegate { BeginBindingCapture(ClientBindingTarget.Freeze); });
            }
            CreateSettingRow("Show personal ready status", ClientPreferencesStore.ShowPersonalReadyStatus, delegate
            {
                ClientPreferencesStore.SetShowPersonalReadyStatus(!ClientPreferencesStore.ShowPersonalReadyStatus);
                RebuildRows();
            });
            CreateSettingRow("Show ready count", ClientPreferencesStore.ShowReadyCount, delegate
            {
                ClientPreferencesStore.SetShowReadyCount(!ClientPreferencesStore.ShowReadyCount);
                RebuildRows();
            });

            if (_canManageSettings)
            {
                CreateSettingRow("Timer Integration", _settings.TimerIntegrationEnabled, delegate
                {
                    RequestSetting(RoundSettingKey.TimerIntegration, !_settings.TimerIntegrationEnabled, 0);
                });
            }

            if (_canManageSettings)
            {
                CreateSettingRow("Hide phantom Mantis backgrounds for all players", _settings.HidePhantomMantisBackground, delegate
                {
                    RequestSetting(RoundSettingKey.HidePhantomMantisBackground, !_settings.HidePhantomMantisBackground, 0);
                });
            }
            else
            {
                CreateInfoRow("Hide phantom Mantis backgrounds: " +
                    (_settings.HidePhantomMantisBackground ? "ON" : "OFF"));
            }

            if (_canManageSettings)
            {
                CreateSettingRow("Enforce vignette for all players", _settings.VignetteEnabled, delegate
                {
                    RequestSetting(RoundSettingKey.VignetteEnabled, !_settings.VignetteEnabled, 0);
                });
            }

            CreateSectionLabel("MATCH CONTROL");
            if (_canManageSettings)
            {
                CreateIntegerInputSettingRow("Win target (0 = off)", _winTarget, 0, 1000, delegate (int value)
                {
                    if (_network != null)
                    {
                        _network.SendRoundAdminAction(RoundAdminAction.SetWinTarget, value);
                        _statusMessage = "Updating win target...";
                        UpdateStatusBar();
                    }
                });

                CreateIntegerInputSettingRow("Round SOUL", _settings.RoundSoul, 0, 198, delegate (int value)
                {
                    RequestSetting(RoundSettingKey.RoundSoul, false, value);
                });

                CreateIntegerInputSettingRow("Ready player count (0 = all)", _settings.ReadyRequiredCount, 0, 1000, delegate (int value)
                {
                    RequestSetting(RoundSettingKey.ReadyRequiredCount, false, value);
                });

                CreateSettingRow("Auto Start", _settings.AutoStart, delegate
                {
                    RequestSetting(RoundSettingKey.AutoStart, !_settings.AutoStart, 0);
                });

                CreateSettingRow("Separate rounds for each room", _settings.IndependentRoundsPerRoom, delegate
                {
                    RequestSetting(RoundSettingKey.IndependentRoundsPerRoom, !_settings.IndependentRoundsPerRoom, 0);
                });

                CreateSettingRow("Disable Mobs", _settings.DisableEnemies, delegate
                {
                    RequestSetting(RoundSettingKey.DisableEnemies, !_settings.DisableEnemies, 0);
                });

                CreateSettingRow("Disable Shade", _settings.DebugDisableShade, delegate
                {
                    RequestSetting(RoundSettingKey.DebugDisableShade, !_settings.DebugDisableShade, 0);
                });

                CreateSettingRow("Individual wins for each team member", _settings.IndividualTeamWins, delegate
                {
                    RequestSetting(RoundSettingKey.IndividualTeamWins, !_settings.IndividualTeamWins, 0);
                });

                CreateSettingRow("Disable PvP before round", _settings.DisablePvpBeforeRound, delegate
                {
                    RequestSetting(RoundSettingKey.DisablePvpBeforeRound, !_settings.DisablePvpBeforeRound, 0);
                });

                CreateSettingRow("Give 2 Lifeblood masks", _settings.GiveLifebloodMasks, delegate
                {
                    RequestSetting(RoundSettingKey.GiveLifebloodMasks, !_settings.GiveLifebloodMasks, 0);
                });

                if (_roundActive)
                {
                    CreateActionRow("END ROUND", delegate
                    {
                        if (_network != null)
                        {
                            _network.SendRoundAdminAction(RoundAdminAction.EndRound, 0);
                            _statusMessage = "Ending round...";
                            UpdateStatusBar();
                        }
                    });
                }
                else if (_waitingForPreparation)
                {
                    CreateInfoRow("Round preparation is in progress.");
                }

            }
            else
            {
                CreateInfoRow(_roundActive ? "A round is currently active." : _waitingForPreparation ? "Round preparation is in progress." : "No active round.");
                if (_winTarget > 0)
                {
                    CreateInfoRow("Match target: " + _winTarget + " wins.");
                }
                else
                {
                    CreateInfoRow("Match win target: off.");
                }
            }

            CreateSectionLabel("BENCHES");
            string selectedName = _selectedBench == null
                ? "Select a discovered bench"
                : GetShortBenchName(_selectedBench);

            if (_hasSharedBench)
            {
                string phantomSuffix = _sharedBenchPhantomMapId > 0
                    ? " (Phantom Mantis Village #" + _sharedBenchPhantomMapId + ")"
                    : string.Empty;
                string lockSuffix = RoundsLocalization.Translate(_sharedBenchLocked ? " (LOCKED)" : " (UNLOCKED)");
                string rawSharedInfo = RoundsLocalization.Translate("Shared bench: ") +
                    _sharedBenchScene + " / " + _sharedBenchMarker + phantomSuffix + lockSuffix;
                CreateInfoRow(rawSharedInfo);
                Transform lastInfo = _content.GetChild(_content.childCount - 1);
                Text infoLabel = lastInfo != null ? lastInfo.GetComponentInChildren<Text>(true) : null;
                if (infoLabel != null)
                {
                    infoLabel.text = rawSharedInfo;
                }
            }
            else
            {
                CreateInfoRow("Shared bench: not set" + (_sharedBenchLocked ? " (LOCKED, but unavailable on this client)" : string.Empty));
            }

            CreateBenchSelectorRow(selectedName);
            if (_benchDropdownExpanded)
            {
                CreateBenchSearchRow();
                List<BenchLocation> benchOptions = BuildBenchOptions();
                if (benchOptions.Count == 0)
                {
                    CreateInfoRow("No benches have been discovered yet. Visit rooms to populate the list.");
                }
                else
                {
                    for (int i = 0; i < benchOptions.Count; i++)
                    {
                        CreateBenchOptionRow(benchOptions[i]);
                    }
                    CreateBenchNoResultsRow();
                    ApplyBenchSearchFilter(_benchSearchQuery);
                }
            }

            if (_canManageBench)
            {
                CreateActionRow("SET HERE", delegate
                {
                    bool atBench;
                    BenchLocation location = BenchCatalogScanner.FindBenchForSetHere(out atBench);
                    if (location == null)
                    {
                        _statusMessage = "Stand on or near a bench before using SET HERE.";
                        UpdateStatusBar();
                        return;
                    }

                    _selectedBench = location;
                    _network?.SetSharedBenchHere(location, atBench, BuildKnownBenchesJson(location));
                    _statusMessage = atBench ? "Setting the bench you are sitting on..." : "Setting the nearby bench...";
                    UpdateStatusBar();
                });

                if (_clientApi != null &&
                    _clientApi.ClientManager != null &&
                    _clientApi.ClientManager.Players != null)
                {
                    List<IClientPlayer> warpTargets = _clientApi.ClientManager.Players
                        .Where(player => player != null)
                        .OrderBy(player => player.Username, StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    for (int i = 0; i < warpTargets.Count; i++)
                    {
                        IClientPlayer target = warpTargets[i];
                        ushort targetPlayerId = target.Id;
                        string targetName = target.Username ?? ("Player " + targetPlayerId);
                        CreatePlayerWarpRow(targetName, targetPlayerId);
                    }
                }

            }

            CreateActionRow("WARP TO SELECTED BENCH", delegate
            {
                if (_selectedBench == null || !_selectedBench.IsValid)
                {
                    _statusMessage = "Select a bench before warping.";
                    UpdateStatusBar();
                    return;
                }

                string warpMessage;
                bool warped = EmergencyBenchWarpController.TryWarpToSelectedBench(
                    _selectedBench.Copy(),
                    out warpMessage);
                _statusMessage = warped
                    ? warpMessage
                    : "Emergency warp failed: " + warpMessage;
                UpdateStatusBar();
            });

            CreateSectionLabel("STATISTICS");
            CreateActionRow(
                RoundStatsOverlay.IsVisible
                    ? "HIDE STATISTICS WINDOW"
                    : "SHOW STATISTICS WINDOW",
                delegate
                {
                    RoundStatsOverlay.ToggleFromSettings();
                    RebuildRows();
                });
            if (_canManageSettings)
            {
                CreateActionRow("RESET STATISTICS", delegate
                {
                    if (_network != null && BenchPolicyController.IsConnected)
                    {
                        _network.SendRoundAdminAction(RoundAdminAction.ResetStatistics, 0);
                        _statusMessage = "Resetting statistics for everyone...";
                    }
                    else
                    {
                        _statusMessage = "Connect to the host to reset statistics.";
                    }

                    UpdateStatusBar();
                });
            }

            ApplyCollapsibleSections();
            Canvas.ForceUpdateCanvases();
            if (_content != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
            }

            if (_scrollRect != null)
            {
                _scrollRect.verticalNormalizedPosition = Mathf.Clamp01(previousScrollPosition);
            }
            UpdateStatusBar();
        }

        private static RectTransform CreateContentBuffer(RectTransform source)
        {
            GameObject contentObject = CreateUiObject(source.parent, "RoundsSettingsContent");
            RectTransform content = contentObject.GetComponent<RectTransform>();
            content.anchorMin = source.anchorMin;
            content.anchorMax = source.anchorMax;
            content.pivot = source.pivot;
            content.anchoredPosition3D = source.anchoredPosition3D;
            content.offsetMin = source.offsetMin;
            content.offsetMax = source.offsetMax;
            content.sizeDelta = source.sizeDelta;
            content.localRotation = source.localRotation;
            content.localScale = source.localScale;

            VerticalLayoutGroup sourceLayout = source.GetComponent<VerticalLayoutGroup>();
            if (sourceLayout != null)
            {
                VerticalLayoutGroup layout = contentObject.AddComponent<VerticalLayoutGroup>();
                layout.padding = new RectOffset(
                    sourceLayout.padding.left,
                    sourceLayout.padding.right,
                    sourceLayout.padding.top,
                    sourceLayout.padding.bottom);
                layout.spacing = sourceLayout.spacing;
                layout.childAlignment = sourceLayout.childAlignment;
                layout.childControlWidth = sourceLayout.childControlWidth;
                layout.childControlHeight = sourceLayout.childControlHeight;
                layout.childForceExpandWidth = sourceLayout.childForceExpandWidth;
                layout.childForceExpandHeight = sourceLayout.childForceExpandHeight;
            }

            ContentSizeFitter sourceFitter = source.GetComponent<ContentSizeFitter>();
            if (sourceFitter != null)
            {
                ContentSizeFitter fitter = contentObject.AddComponent<ContentSizeFitter>();
                fitter.horizontalFit = sourceFitter.horizontalFit;
                fitter.verticalFit = sourceFitter.verticalFit;
            }

            return content;
        }

        private static string GetContentChildPath(Transform root, Transform target)
        {
            if (root == null || target == null || target == root || !target.IsChildOf(root))
            {
                return string.Empty;
            }

            List<int> indices = new List<int>();
            Transform current = target;
            while (current != null && current != root)
            {
                indices.Add(current.GetSiblingIndex());
                current = current.parent;
            }

            if (current != root)
            {
                return string.Empty;
            }

            indices.Reverse();
            return string.Join("/", indices.Select(index => index.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray());
        }

        private static Transform ResolveContentChildPath(Transform root, string path)
        {
            if (root == null || string.IsNullOrEmpty(path))
            {
                return null;
            }

            string[] parts = path.Split('/');
            Transform current = root;
            for (int i = 0; i < parts.Length; i++)
            {
                int childIndex;
                if (!int.TryParse(parts[i], out childIndex) || childIndex < 0 || childIndex >= current.childCount)
                {
                    return null;
                }

                current = current.GetChild(childIndex);
            }

            return current;
        }

        private void CreatePvpModeSelectorRow()
        {
            GameObject row = CreateUiObject(_content, "PvpModeSelectorRow");
            LayoutElement rowLayout = row.AddComponent<LayoutElement>();
            rowLayout.minHeight = 76f;
            rowLayout.preferredHeight = 76f;

            HorizontalLayoutGroup horizontal = row.AddComponent<HorizontalLayoutGroup>();
            horizontal.spacing = 8f;
            horizontal.padding = new RectOffset(2, 2, 2, 2);
            horizontal.childAlignment = TextAnchor.MiddleCenter;
            horizontal.childControlWidth = true;
            horizontal.childControlHeight = true;
            horizontal.childForceExpandWidth = true;
            horizontal.childForceExpandHeight = true;

            Button selectedButton = null;
            Image selectedChain = null;
            CreatePvpModeButton(row.transform, PvpMode.Quick, "QUICK", GetPvpModeIcon(PvpMode.Quick), out Button quickButton, out Image quickChain);
            CreatePvpModeButton(row.transform, PvpMode.Long, "LONG", GetPvpModeIcon(PvpMode.Long), out Button longButton, out Image longChain);
            CreatePvpModeButton(row.transform, PvpMode.OneShot, "ONESHOT", GetPvpModeIcon(PvpMode.OneShot), out Button oneShotButton, out Image oneShotChain);

            if (_pvpMode == PvpMode.Quick) { selectedButton = quickButton; selectedChain = quickChain; }
            else if (_pvpMode == PvpMode.Long) { selectedButton = longButton; selectedChain = longChain; }
            else { selectedButton = oneShotButton; selectedChain = oneShotChain; }

            if (!_canManagePvpMode)
            {
                quickButton.interactable = false;
                longButton.interactable = false;
                oneShotButton.interactable = false;
            }

            if (_playModeAnimation && selectedButton != null && selectedChain != null)
            {
                _playModeAnimation = false;
                StartCoroutine(PlayChainSelection(selectedButton.transform as RectTransform, selectedChain));
            }

            CreateInfoRow(GetPvpModeDescription(_pvpMode));
        }

        private void CreatePvpModeButton(
            Transform parent,
            PvpMode mode,
            string caption,
            Sprite icon,
            out Button button,
            out Image chain)
        {
            bool selected = _pvpMode == mode;
            button = CreateButton(
                parent,
                "PvpMode_" + mode,
                caption,
                delegate
                {
                    if (!_canManagePvpMode || _network == null) return;
                    if (_pvpMode == mode) return;
                    _statusMessage = "Changing PvP mode...";
                    UpdateStatusBar();
                    _network.SetPvpMode(mode);
                },
                12);

            LayoutElement layout = button.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = 120f;
            layout.preferredWidth = 190f;
            layout.flexibleWidth = 1f;
            layout.minHeight = 70f;
            layout.preferredHeight = 70f;

            Image background = button.GetComponent<Image>();
            if (background != null)
            {
                background.color = selected
                    ? new Color(0.24f, 0.24f, 0.30f, 1f)
                    : ButtonColor;
            }

            Text label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.alignment = TextAnchor.MiddleCenter;
                label.rectTransform.offsetMin = new Vector2(62f, 3f);
                label.rectTransform.offsetMax = new Vector2(-24f, -3f);
                label.fontSize = 18;
                label.resizeTextForBestFit = false;
            }

            GameObject iconObject = CreateUiObject(button.transform, "ModeIcon");
            RectTransform iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = new Vector2(8f, 0f);
            iconRect.sizeDelta = new Vector2(52f, 52f);
            Image iconImage = iconObject.AddComponent<Image>();
            iconImage.sprite = icon;
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;

            GameObject chainObject = CreateUiObject(button.transform, "ModeChain");
            RectTransform chainRect = chainObject.GetComponent<RectTransform>();
            chainRect.anchorMin = new Vector2(1f, 1f);
            chainRect.anchorMax = new Vector2(1f, 1f);
            chainRect.pivot = new Vector2(1f, 1f);
            chainRect.anchoredPosition = new Vector2(-4f, -2f);
            chainRect.sizeDelta = new Vector2(24f, 24f);
            chain = chainObject.AddComponent<Image>();
            chain.sprite = GetChainIcon(selected);
            chain.preserveAspect = true;
            chain.raycastTarget = false;
        }

        private IEnumerator PlayChainSelection(RectTransform target, Image chain)
        {
            if (target == null || chain == null) yield break;

            Vector3 originalScale = target.localScale;
            Color originalColor = Color.white;
            Sprite activeChain = GetChainIcon(true);
            Sprite inactiveChain = GetChainIcon(false);
            target.localScale = originalScale * 0.96f;
            chain.sprite = inactiveChain;
            chain.rectTransform.localScale = Vector3.one * 0.25f;
            chain.color = new Color(1f, 1f, 1f, 0f);

            float elapsed = 0f;
            const float duration = 0.32f;
            while (elapsed < duration && target != null && chain != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                target.localScale = Vector3.Lerp(originalScale * 0.96f, originalScale, t);
                chain.rectTransform.localScale = Vector3.Lerp(Vector3.one * 0.25f, Vector3.one, t);
                chain.color = new Color(originalColor.r, originalColor.g, originalColor.b, t);
                if (t >= 0.46f)
                {
                    chain.sprite = activeChain;
                }
                yield return null;
            }

            if (target != null) target.localScale = originalScale;
            if (chain != null)
            {
                chain.sprite = activeChain;
                chain.rectTransform.localScale = Vector3.one;
                chain.color = originalColor;
            }
        }

        private static string GetPvpModeDescription(PvpMode mode)
        {
            switch (mode)
            {
                case PvpMode.Long:
                    return "Long: healing is allowed; only Spore Shroom is disabled.";
                case PvpMode.OneShot:
                    return "OneShot: 1 HP, server-managed SOUL, no healing, and no Quick Focus, Spell Twister, Kingsoul, Spore Shroom, Hiveblood, Lifeblood charms, Deep Focus or Weaversong. Carefree Melody is granted.";
                default:
                    return "Quick: no healing; Quick Focus, Kingsoul, Spore Shroom, Hiveblood, Lifeblood charms, Deep Focus and Weaversong are removed. All other charms are granted.";
            }
        }

        private static Sprite GetPvpModeIcon(PvpMode mode)
        {
            if (mode == PvpMode.Quick)
            {
                if (!_quickModeIconLoadAttempted)
                {
                    _quickModeIconLoadAttempted = true;
                    _quickModeIcon = LoadEmbeddedSprite(
                        "HKMP.Rounds.Resources.quick_mode.png",
                        "HKMP.Rounds.QuickModeIcon");
                }

                return _quickModeIcon != null ? _quickModeIcon : GetOneShotIcon();
            }

            if (mode == PvpMode.Long)
            {
                if (!_longModeIconLoadAttempted)
                {
                    _longModeIconLoadAttempted = true;
                    _longModeIcon = LoadEmbeddedSprite(
                        "HKMP.Rounds.Resources.long_mode.png",
                        "HKMP.Rounds.LongModeIcon");
                }

                return _longModeIcon != null ? _longModeIcon : GetOneShotIcon();
            }

            return GetOneShotIcon();
        }

        private static Sprite LoadEmbeddedSprite(
            string resourceName,
            string spriteName)
        {
            Texture2D texture = null;
            try
            {
                byte[] imageBytes = LoadResourceBytes(resourceName);
                if (imageBytes == null || imageBytes.Length == 0)
                {
                    Debug.LogWarning(
                        "[HKMP.Rounds] Image resource was not found: " + resourceName);
                    return null;
                }

                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!ImageConversion.LoadImage(texture, imageBytes))
                {
                    Object.Destroy(texture);
                    return null;
                }

                if (resourceName.EndsWith(
                        "oneshot_niko.png",
                        StringComparison.OrdinalIgnoreCase))
                {
                    RemoveExternalWhiteBackground(texture);
                }

                texture.name = spriteName + ".Texture";
                texture.filterMode = FilterMode.Bilinear;
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.anisoLevel = 4;
                texture.hideFlags = HideFlags.HideAndDontSave;

                return Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
            }
            catch (Exception exception)
            {
                if (texture != null)
                {
                    Object.Destroy(texture);
                }

                Debug.LogWarning(
                    "[HKMP.Rounds] Could not load image resource '" + resourceName + "': " + exception.Message);
                return null;
            }
        }

        private static byte[] LoadResourceBytes(string resourceName)
        {
            if (string.IsNullOrWhiteSpace(resourceName))
            {
                return null;
            }

            Assembly assembly = Assembly.GetExecutingAssembly();
            string fileName = Path.GetFileName(resourceName);
            string matchedResourceName = null;
            string[] manifestResourceNames = assembly.GetManifestResourceNames();

            for (int i = 0; i < manifestResourceNames.Length; i++)
            {
                string candidateName = manifestResourceNames[i];
                if (string.Equals(
                        candidateName,
                        resourceName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    matchedResourceName = candidateName;
                    break;
                }

                if (matchedResourceName == null &&
                    candidateName.EndsWith(
                        "." + fileName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    matchedResourceName = candidateName;
                }
            }

            if (matchedResourceName != null)
            {
                using (Stream stream = assembly.GetManifestResourceStream(matchedResourceName))
                {
                    if (stream != null)
                    {
                        using (MemoryStream memory = new MemoryStream())
                        {
                            stream.CopyTo(memory);
                            return memory.ToArray();
                        }
                    }
                }
            }

            int resourcesMarker = resourceName.IndexOf(
                "Resources.",
                StringComparison.OrdinalIgnoreCase);
            if (resourcesMarker >= 0)
            {
                fileName = resourceName.Substring(
                    resourcesMarker + "Resources.".Length);
            }

            string assemblyDirectory = Path.GetDirectoryName(assembly.Location);
            if (string.IsNullOrWhiteSpace(assemblyDirectory))
            {
                return null;
            }

            string mapsFileName = fileName.StartsWith("Maps.", StringComparison.OrdinalIgnoreCase)
                ? fileName.Substring("Maps.".Length)
                : string.Empty;
            string[] candidatePaths = string.IsNullOrWhiteSpace(mapsFileName)
                ? new[]
                {
                    Path.Combine(assemblyDirectory, "Resources", fileName),
                    Path.Combine(assemblyDirectory, fileName)
                }
                : new[]
                {
                    Path.Combine(assemblyDirectory, "Resources", "Maps", mapsFileName),
                    Path.Combine(assemblyDirectory, "Resources", mapsFileName),
                    Path.Combine(assemblyDirectory, "Resources", fileName),
                    Path.Combine(assemblyDirectory, fileName)
                };

            for (int i = 0; i < candidatePaths.Length; i++)
            {
                string candidate = candidatePaths[i];
                if (File.Exists(candidate))
                {
                    return File.ReadAllBytes(candidate);
                }
            }

            return null;
        }

        private static void RemoveExternalWhiteBackground(Texture2D texture)
        {
            if (texture == null)
            {
                return;
            }

            Color32[] pixels = texture.GetPixels32();
            bool changed = false;
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 pixel = pixels[i];
                if (pixel.a != 0 ||
                    (pixel.r == 0 && pixel.g == 0 && pixel.b == 0))
                {
                    continue;
                }

                pixel.r = 0;
                pixel.g = 0;
                pixel.b = 0;
                pixels[i] = pixel;
                changed = true;
            }

            if (changed)
            {
                texture.SetPixels32(pixels);
                texture.Apply(false, false);
            }
        }

        private static Sprite GetOneShotIcon()
        {
            if (_oneShotIcon == null)
            {
                _oneShotIcon = LoadEmbeddedSprite(
                    "HKMP.Rounds.Resources.oneshot_niko.png",
                    "HKMP.Rounds.OneShotNikoIcon");
            }

            if (_oneShotIcon == null)
            {
                _oneShotIcon = CreateHeartIcon();
            }

            return _oneShotIcon;
        }

        private static Sprite GetChainIcon(bool selected)
        {
            if (selected)
            {
                if (_boundChainIcon == null)
                {
                    _boundChainIcon = CreateChainIcon(true);
                }

                return _boundChainIcon;
            }

            if (_unboundChainIcon == null)
            {
                _unboundChainIcon = CreateChainIcon(false);
            }

            return _unboundChainIcon;
        }

        private static Sprite CreateChainIcon(bool selected)
        {
            const int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = selected ? "HKMP.Rounds.ChainBound" : "HKMP.Rounds.ChainUnbound";
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            Color32 color = selected
                ? new Color32(245, 235, 198, 255)
                : new Color32(140, 140, 151, 230);
            Color32[] pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x + 0.5f) / size * 2f - 1f;
                    float ny = (y + 0.5f) / size * 2f - 1f;
                    bool first = IsChainRing(nx, ny, -0.20f, 0f, 0.76f, 0.42f, 0.24f);
                    bool second = IsChainRing(nx, ny, 0.20f, 0f, -0.76f, 0.42f, 0.24f);
                    bool gap = !selected && Mathf.Abs(nx) < 0.12f && Mathf.Abs(ny) < 0.11f;
                    if ((first || second) && !gap)
                    {
                        pixels[y * size + x] = color;
                    }
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static bool IsChainRing(float x, float y, float centerX, float centerY, float angle, float radiusX, float radiusY)
        {
            float cosine = Mathf.Cos(angle);
            float sine = Mathf.Sin(angle);
            float translatedX = x - centerX;
            float translatedY = y - centerY;
            float rotatedX = translatedX * cosine + translatedY * sine;
            float rotatedY = -translatedX * sine + translatedY * cosine;
            float outer = (rotatedX * rotatedX) / (radiusX * radiusX) +
                          (rotatedY * rotatedY) / (radiusY * radiusY);
            float innerRadiusX = radiusX - 0.095f;
            float innerRadiusY = radiusY - 0.095f;
            float inner = (rotatedX * rotatedX) / (innerRadiusX * innerRadiusX) +
                          (rotatedY * rotatedY) / (innerRadiusY * innerRadiusY);
            return outer <= 1f && inner >= 1f;
        }

        private static Sprite CreateHeartIcon()
        {
            const int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "HKMP.Rounds.OneShotHeartIcon";
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            Color32 border = new Color32(248, 230, 218, 255);
            Color32 fill = new Color32(176, 48, 66, 255);
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float hx = (x + 0.5f) / size * 2f - 1f;
                    float hy = (y + 0.5f) / size * 2f - 1f;
                    hy = -hy + 0.12f;
                    float value = Mathf.Pow(hx * hx + hy * hy - 1f, 3f) - hx * hx * hy * hy * hy;
                    if (value <= 0f)
                    {
                        float sample = Mathf.Pow((hx + 0.025f) * (hx + 0.025f) + (hy - 0.025f) * (hy - 0.025f) - 1f, 3f) -
                                       (hx + 0.025f) * (hx + 0.025f) * (hy - 0.025f) * (hy - 0.025f) * (hy - 0.025f);
                        pixels[y * size + x] = sample > 0f ? border : fill;
                    }
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private void CreateIntegerInputSettingRow(
            string name,
            int value,
            int minimum,
            int maximum,
            Action<int> setter)
        {
            GameObject row = CreateUiObject(_content, "NumericInput_" + name.Replace(" ", string.Empty));
            LayoutElement rowLayout = row.AddComponent<LayoutElement>();
            rowLayout.minHeight = 50f;
            rowLayout.preferredHeight = 50f;

            HorizontalLayoutGroup horizontal = row.AddComponent<HorizontalLayoutGroup>();
            horizontal.padding = new RectOffset(12, 8, 2, 2);
            horizontal.spacing = 8f;
            horizontal.childAlignment = TextAnchor.MiddleLeft;
            horizontal.childControlWidth = true;
            horizontal.childControlHeight = true;
            horizontal.childForceExpandWidth = false;
            horizontal.childForceExpandHeight = true;

            Text label = CreateText(row.transform, "Label", name, 15, Color.white);
            LayoutElement labelLayout = label.gameObject.AddComponent<LayoutElement>();
            labelLayout.flexibleWidth = 1f;
            labelLayout.minWidth = 145f;

            GameObject inputObject = CreateUiObject(row.transform, "NumericValue");
            Image inputBackground = inputObject.AddComponent<Image>();
            inputBackground.sprite = WhiteSprite;
            inputBackground.color = new Color(0.04f, 0.04f, 0.06f, 0.95f);
            inputBackground.raycastTarget = true;
            CanvasGroup inputGroup = inputObject.AddComponent<CanvasGroup>();
            inputGroup.interactable = true;
            inputGroup.blocksRaycasts = true;
            InputField input = inputObject.AddComponent<InputField>();
            Text inputText = CreateText(inputObject.transform, "Text", value.ToString(), 16, AccentTextColor);
            inputText.alignment = TextAnchor.MiddleCenter;
            inputText.horizontalOverflow = HorizontalWrapMode.Overflow;
            inputText.verticalOverflow = VerticalWrapMode.Truncate;
            RectTransform inputTextRect = inputText.rectTransform;
            inputTextRect.anchorMin = Vector2.zero;
            inputTextRect.anchorMax = Vector2.one;
            inputTextRect.offsetMin = new Vector2(6f, 2f);
            inputTextRect.offsetMax = new Vector2(-6f, -2f);
            input.textComponent = inputText;
            input.targetGraphic = inputBackground;
            input.interactable = true;
            input.readOnly = false;
            input.shouldActivateOnSelect = true;
            input.transition = Selectable.Transition.ColorTint;
            input.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            input.contentType = InputField.ContentType.IntegerNumber;
            input.lineType = InputField.LineType.SingleLine;
            input.characterLimit = 4;
            string draft;
            input.text = _numericInputDrafts.TryGetValue(name, out draft)
                ? draft
                : value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            input.onValueChanged.AddListener(delegate (string text)
            {
                _numericInputDrafts[name] = text ?? string.Empty;
            });
            LayoutElement inputLayout = inputObject.AddComponent<LayoutElement>();
            inputLayout.minWidth = 72f;
            inputLayout.preferredWidth = 84f;
            inputLayout.preferredHeight = 40f;

            Button apply = CreateButton(row.transform, "Apply", "APPLY", delegate
            {
                int parsed;
                if (!int.TryParse(input.text, System.Globalization.NumberStyles.Integer,
                        System.Globalization.CultureInfo.InvariantCulture, out parsed))
                {
                    _statusMessage = name + " must be a whole number.";
                    UpdateStatusBar();
                    return;
                }

                if (parsed < minimum || parsed > maximum)
                {
                    _statusMessage = name + " must be between " + minimum + " and " + maximum + ".";
                    UpdateStatusBar();
                    return;
                }

                setter(parsed);
            }, 12);
            LayoutElement applyLayout = apply.gameObject.AddComponent<LayoutElement>();
            applyLayout.minWidth = 116f;
            applyLayout.preferredWidth = 120f;
            applyLayout.preferredHeight = 36f;
            Text applyLabel = apply.GetComponentInChildren<Text>(true);
            if (applyLabel != null)
            {
                applyLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
                applyLabel.verticalOverflow = VerticalWrapMode.Truncate;
                applyLabel.resizeTextForBestFit = true;
                applyLabel.resizeTextMinSize = 9;
                applyLabel.resizeTextMaxSize = 12;
                applyLabel.alignment = TextAnchor.MiddleCenter;
            }
        }

        private void ClearConfirmedNumericInputDraft(string name, int authoritativeValue)
        {
            string draft;
            int parsed;
            if (_numericInputDrafts.TryGetValue(name, out draft) &&
                int.TryParse(
                    draft,
                    System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out parsed) &&
                parsed == authoritativeValue)
            {
                _numericInputDrafts.Remove(name);
            }
        }

        private void RebuildStatisticsRows(float previousScrollPosition)
        {
            CreatePvpModeSelectorRow();
            CreateSectionLabel("STATISTICS");
            CreateActionRow(
                RoundStatsOverlay.IsVisible
                    ? "HIDE STATISTICS WINDOW"
                    : "SHOW STATISTICS WINDOW",
                delegate
                {
                    RoundStatsOverlay.ToggleFromSettings();
                    RebuildRows();
                });

            if (_canManageSettings)
            {
                CreateActionRow("RESET STATISTICS", delegate
                {
                    if (_network != null && BenchPolicyController.IsConnected)
                    {
                        _network.SendRoundAdminAction(RoundAdminAction.ResetStatistics, 0);
                        _statusMessage = "Resetting statistics for everyone...";
                    }
                    else
                    {
                        _statusMessage = "Connect to the host to reset statistics.";
                    }

                    UpdateStatusBar();
                });
            }

            CreateActionRow("BACK TO SETTINGS", delegate
            {
                _statisticsOnlyView = false;
                RebuildRows();
                UpdateStatusBar();
            });

            ApplyCollapsibleSections();
            Canvas.ForceUpdateCanvases();
            if (_content != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
            }

            if (_scrollRect != null)
            {
                _scrollRect.verticalNormalizedPosition = Mathf.Clamp01(previousScrollPosition);
            }

            UpdateStatusBar();
        }

        private static readonly Dictionary<int, Sprite> MapPresetSpriteCache = new Dictionary<int, Sprite>();
        private static readonly HashSet<int> MapPresetSpriteLoadAttempted = new HashSet<int>();

        private void PreloadMapPresetSprites()
        {
            for (int id = 1; id <= 4; id++)
            {
                MapPresetDefinition definition = MapPresetCatalog.GetById(id);
                if (definition != null)
                {
                    GetMapPresetSprite(definition);
                }
            }
        }

        private static Sprite GetMapPresetSprite(MapPresetDefinition definition)
        {
            if (definition == null)
            {
                return null;
            }

            Sprite cached;
            if (MapPresetSpriteCache.TryGetValue(definition.Id, out cached))
            {
                return cached;
            }

            if (MapPresetSpriteLoadAttempted.Contains(definition.Id))
            {
                return null;
            }

            MapPresetSpriteLoadAttempted.Add(definition.Id);
            string resourceName = "HKMP.Rounds.Resources.Maps." + definition.ImageFileName;
            string assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            Sprite sprite = LoadEmbeddedSprite(resourceName, "MapPreset_" + definition.Id);
            if (sprite == null)
            {
                sprite = MapPresetPreviewLoader.GetFallbackSprite(definition);
            }
            if (sprite != null)
            {
                MapPresetSpriteCache[definition.Id] = sprite;
            }

            return sprite;
        }

        private void CreateMapPresetGallery()
        {
            List<MapPresetDefinition> maps = MapPresetCatalog.GetAvailableMaps();
            if (maps.Count == 0)
            {
                CreateInfoRow("No built-in map presets are available.");
                return;
            }

            const float stripHeight = 86f;
            const float stripSpacing = 9f;
            GameObject gallery = CreateUiObject(_content, "PresetMapGallery");
            VerticalLayoutGroup layout = gallery.AddComponent<VerticalLayoutGroup>();
            layout.spacing = stripSpacing;
            layout.padding = new RectOffset(2, 2, 2, 2);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            LayoutElement galleryLayout = gallery.AddComponent<LayoutElement>();
            const float galleryBottomSpacing = 52f;
            galleryLayout.minHeight = maps.Count * stripHeight + (maps.Count - 1) * stripSpacing + 4f + galleryBottomSpacing;
            galleryLayout.preferredHeight = galleryLayout.minHeight;
            galleryLayout.flexibleWidth = 1f;

            for (int i = 0; i < maps.Count; i++)
            {
                MapPresetDefinition definition = maps[i];
                int mapId = definition.Id;
                string mapName = definition.DisplayName;

                GameObject strip = CreateUiObject(gallery.transform, "PresetMap_" + mapId);
                Sprite image = GetMapPresetSprite(definition);
                Color fallbackColor = (Color)MapPresetCatalog.GetFallbackColor(definition);
                Image background = strip.AddComponent<Image>();
                background.sprite = WhiteSprite;
                background.type = Image.Type.Simple;
                background.color = image != null
                    ? new Color(0.10f, 0.11f, 0.14f, 1f)
                    : new Color(fallbackColor.r * 0.32f, fallbackColor.g * 0.32f, fallbackColor.b * 0.32f, 1f);
                background.raycastTarget = true;

                GameObject preview = CreateUiObject(strip.transform, "MapPreview");
                RectTransform previewRect = preview.GetComponent<RectTransform>();
                previewRect.anchorMin = new Vector2(0f, 0f);
                previewRect.anchorMax = new Vector2(0f, 1f);
                previewRect.pivot = new Vector2(0f, 0.5f);
                previewRect.sizeDelta = new Vector2(153f, 0f);
                previewRect.anchoredPosition = Vector2.zero;
                Image previewImage = preview.AddComponent<Image>();
                previewImage.sprite = image != null ? image : WhiteSprite;
                previewImage.type = Image.Type.Simple;
                previewImage.preserveAspect = true;
                previewImage.color = image != null ? Color.white : fallbackColor;
                previewImage.raycastTarget = false;

                Outline border = strip.AddComponent<Outline>();
                border.effectColor = new Color(0.74f, 0.68f, 0.49f, 0.66f);
                border.effectDistance = new Vector2(1f, -1f);

                LayoutElement stripLayout = strip.AddComponent<LayoutElement>();
                stripLayout.minHeight = stripHeight;
                stripLayout.preferredHeight = stripHeight;
                stripLayout.flexibleWidth = 1f;

                Button button = strip.AddComponent<Button>();
                button.targetGraphic = background;
                ColorBlock colors = button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(1f, 1f, 1f, 0.9f);
                colors.pressedColor = new Color(0.72f, 0.82f, 1f, 1f);
                colors.disabledColor = Color.gray;
                colors.fadeDuration = 0.08f;
                button.colors = colors;
                button.onClick.AddListener(delegate { RequestPresetMapTravel(mapId, mapName); });
                button.interactable = !BenchPolicyController.IsConnected || _mapTravelEnabled || _canManageMapTravel;

                GameObject overlay = CreateUiObject(strip.transform, "DarkOverlay");
                RectTransform overlayRect = overlay.GetComponent<RectTransform>();
                overlayRect.anchorMin = Vector2.zero;
                overlayRect.anchorMax = Vector2.one;
                overlayRect.offsetMin = Vector2.zero;
                overlayRect.offsetMax = Vector2.zero;
                Image shade = overlay.AddComponent<Image>();
                shade.sprite = WhiteSprite;
                shade.color = new Color(0f, 0f, 0f, 0.48f);
                shade.raycastTarget = false;

                Text caption = CreateText(overlay.transform, "Caption", mapName, 20, Color.white);
                RectTransform captionRect = caption.rectTransform;
                captionRect.anchorMin = Vector2.zero;
                captionRect.anchorMax = Vector2.one;
                captionRect.offsetMin = new Vector2(164f, 2f);
                captionRect.offsetMax = new Vector2(-12f, -2f);
                caption.alignment = TextAnchor.MiddleCenter;
                caption.font = GetHollowKnightMapFont();
                caption.fontStyle = FontStyle.Bold;
                caption.horizontalOverflow = HorizontalWrapMode.Wrap;
                caption.verticalOverflow = VerticalWrapMode.Truncate;
                caption.resizeTextForBestFit = true;
                caption.resizeTextMinSize = 14;
                caption.resizeTextMaxSize = 20;
                caption.raycastTarget = false;
            }
        }

        private static Font GetHollowKnightUiFont()
        {
            if (_uiFont != null && _uiFont.HasCharacter('Я') && _uiFont.HasCharacter('ё'))
            {
                return _uiFont;
            }

            _uiFont = null;
            string[] fontNames = { "Georgia", "Arial" };
            for (int i = 0; i < fontNames.Length; i++)
            {
                try
                {
                    Font candidate = Font.CreateDynamicFontFromOSFont(fontNames[i], 36);
                    if (candidate != null && candidate.HasCharacter('Я') && candidate.HasCharacter('ё'))
                    {
                        _uiFont = candidate;
                        return _uiFont;
                    }
                }
                catch
                {
                }
            }

            try
            {
                Font builtInArial = Resources.GetBuiltinResource<Font>("Arial.ttf");
                if (builtInArial != null)
                {
                    _uiFont = builtInArial;
                    return _uiFont;
                }
            }
            catch
            {
            }

            _uiFont = Resources.GetBuiltinResource<Font>("Perpetua.ttf");
            return _uiFont;
        }

        private static Font GetQuestionMarkFont()
        {
            if (_questionMarkFont != null && _questionMarkFont.HasCharacter('?'))
            {
                return _questionMarkFont;
            }

            try
            {
                _questionMarkFont = Font.CreateDynamicFontFromOSFont("Arial", 24);
                if (_questionMarkFont != null && _questionMarkFont.HasCharacter('?'))
                {
                    return _questionMarkFont;
                }
            }
            catch
            {
            }

            try
            {
                Font builtin = Resources.GetBuiltinResource<Font>("Arial.ttf");
                if (builtin != null && builtin.HasCharacter('?'))
                {
                    _questionMarkFont = builtin;
                    return builtin;
                }
            }
            catch
            {
            }

            return GetHollowKnightUiFont();
        }

        private static FontStyle GetBodyFontStyle()
        {
            return FontStyle.Normal;
        }

        private static Font GetHollowKnightMapFont()
        {
            try
            {
                Font font = Resources.GetBuiltinResource<Font>("Trajan Pro.ttf");
                if (font != null)
                {
                    return font;
                }
            }
            catch
            {
            }

            Font[] loadedFonts = Resources.FindObjectsOfTypeAll<Font>();
            if (loadedFonts != null)
            {
                Font font = loadedFonts.FirstOrDefault(candidate =>
                    candidate != null &&
                    candidate.name.IndexOf("Trajan", StringComparison.OrdinalIgnoreCase) >= 0);
                if (font != null)
                {
                    return font;
                }
            }

            return Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        private void RequestPresetMapTravel(int mapId, string mapName)
        {
            if (_network != null && BenchPolicyController.IsConnected)
            {
                _network.TravelToPresetMap(
                    mapId,
                    GameManager.instance != null ? GameManager.instance.sceneName : string.Empty);
                _statusMessage = "Requesting travel to " + mapName + "...";
                UpdateStatusBar();
                return;
            }

            string message;
            bool started = MapPresetTravelController.TryTravelLocally(mapId, out message);
            _statusMessage = started
                ? "Travelling locally to " + mapName + "."
                : "Local travel failed: " + message;
            UpdateStatusBar();
        }

        private void CreateSectionLabel(string text)
        {
            string key = NormalizeSectionKey(text);
            bool collapsed;
            if (!_collapsedSections.TryGetValue(key, out collapsed))
            {
                collapsed = false;
            }

            GameObject section = CreateUiObject(_content, "Section_" + key);
            Image background = section.AddComponent<Image>();
            background.sprite = WhiteSprite;
            background.color = new Color(0.035f, 0.048f, 0.072f, 0.98f);
            background.raycastTarget = true;

            Button toggle = section.AddComponent<Button>();
            toggle.targetGraphic = background;
            ColorBlock buttonColors = toggle.colors;
            buttonColors.normalColor = new Color(0.035f, 0.048f, 0.072f, 0.98f);
            buttonColors.highlightedColor = new Color(0.105f, 0.135f, 0.19f, 1f);
            buttonColors.pressedColor = new Color(0.07f, 0.09f, 0.13f, 1f);
            buttonColors.fadeDuration = 0.08f;
            toggle.colors = buttonColors;
            toggle.onClick.AddListener(delegate
            {
                bool current;
                _collapsedSections.TryGetValue(key, out current);
                _collapsedSections[key] = !current;
                RebuildRows();
            });

            LayoutElement layout = section.AddComponent<LayoutElement>();
            layout.minHeight = 42f;
            layout.preferredHeight = 42f;

            HorizontalLayoutGroup horizontal = section.AddComponent<HorizontalLayoutGroup>();
            horizontal.padding = new RectOffset(12, 10, 3, 3);
            horizontal.spacing = 12f;
            horizontal.childAlignment = TextAnchor.MiddleLeft;
            horizontal.childControlWidth = true;
            horizontal.childControlHeight = true;
            horizontal.childForceExpandWidth = false;
            horizontal.childForceExpandHeight = true;

            GameObject accent = CreateUiObject(section.transform, "Accent");
            Image accentImage = accent.AddComponent<Image>();
            accentImage.sprite = WhiteSprite;
            accentImage.color = new Color(0.82f, 0.69f, 0.40f, 1f);
            accentImage.raycastTarget = false;
            LayoutElement accentLayout = accent.AddComponent<LayoutElement>();
            accentLayout.minWidth = 4f;
            accentLayout.preferredWidth = 4f;
            accentLayout.flexibleHeight = 1f;

            string localizedTitle = RoundsLocalization.Translate(text);
            Text label = CreateText(section.transform, "Title", localizedTitle + (collapsed ? "  [+]" : "  [-]"), 16, AccentTextColor);
            label.text = localizedTitle + (collapsed ? "  [+]" : "  [-]");
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleLeft;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false;
            LayoutElement labelLayout = label.gameObject.AddComponent<LayoutElement>();
            labelLayout.minWidth = 100f;
            labelLayout.flexibleWidth = 1f;
        }

        private static string NormalizeSectionKey(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return "section";
            }

            return new string(text.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
        }

        private void ApplyCollapsibleSections()
        {
            if (_content == null)
            {
                return;
            }

            bool hasSection = false;
            bool collapsed = false;
            for (int i = 0; i < _content.childCount; i++)
            {
                Transform child = _content.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                string childName = child.gameObject.name;
                if (string.Equals(childName, "PvpModeSelectorRow", StringComparison.Ordinal))
                {
                    child.gameObject.SetActive(true);
                    continue;
                }

                if (childName.StartsWith("Section_", StringComparison.Ordinal))
                {
                    hasSection = true;
                    string key = childName.Substring("Section_".Length);
                    _collapsedSections.TryGetValue(key, out collapsed);
                    child.gameObject.SetActive(true);
                }
                else if (hasSection)
                {
                    child.gameObject.SetActive(!collapsed);
                }
                else
                {
                    child.gameObject.SetActive(true);
                }
            }
        }

        private void CreateBindingRow(
            string name,
            string keyName,
            bool capturing,
            UnityEngine.Events.UnityAction onClick)
        {
            GameObject row = CreateUiObject(_content, "Binding_" + name.Replace(" ", string.Empty));
            Image rowImage = row.AddComponent<Image>();
            rowImage.sprite = WhiteSprite;
            rowImage.color = RowColor;
            rowImage.raycastTarget = false;

            LayoutElement rowLayout = row.AddComponent<LayoutElement>();
            rowLayout.minHeight = 68f;
            rowLayout.preferredHeight = 68f;

            HorizontalLayoutGroup horizontal = row.AddComponent<HorizontalLayoutGroup>();
            horizontal.padding = new RectOffset(14, 12, 4, 4);
            horizontal.spacing = 10f;
            horizontal.childAlignment = TextAnchor.MiddleLeft;
            horizontal.childControlWidth = true;
            horizontal.childControlHeight = true;
            horizontal.childForceExpandWidth = false;
            horizontal.childForceExpandHeight = true;

            GameObject accent = CreateUiObject(row.transform, "Accent");
            Image accentImage = accent.AddComponent<Image>();
            accentImage.sprite = WhiteSprite;
            accentImage.color = new Color(0.48f, 0.58f, 0.78f, 0.95f);
            accentImage.raycastTarget = false;
            LayoutElement accentLayout = accent.AddComponent<LayoutElement>();
            accentLayout.minWidth = 3f;
            accentLayout.preferredWidth = 3f;
            accentLayout.flexibleHeight = 1f;

            Text label = CreateText(row.transform, "Label", name, 17, Color.white);
            label.fontStyle = FontStyle.Normal;
            LayoutElement labelLayout = label.gameObject.AddComponent<LayoutElement>();
            labelLayout.flexibleWidth = 1f;
            labelLayout.minWidth = 165f;

            string caption = capturing ? "PRESS A KEY" : keyName;
            Button binding = CreateButton(row.transform, "Binding", caption, onClick, 17);
            LayoutElement bindingLayout = binding.gameObject.AddComponent<LayoutElement>();
            bindingLayout.preferredWidth = 164f;
            bindingLayout.minWidth = 164f;
            bindingLayout.preferredHeight = 48f;
            Text bindingLabel = binding.GetComponentInChildren<Text>();
            if (bindingLabel != null)
            {
                bindingLabel.font = GetHollowKnightUiFont();
                bindingLabel.fontStyle = GetBodyFontStyle();
                bindingLabel.color = Color.white;
                bindingLabel.supportRichText = false;
                bindingLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
                bindingLabel.SetAllDirty();
            }
        }

        private void CreateSettingRow(string name, bool value, UnityEngine.Events.UnityAction onClick)
        {
            GameObject row = CreateUiObject(_content, "Setting_" + name.Replace(" ", string.Empty));
            Image rowImage = row.AddComponent<Image>();
            rowImage.sprite = WhiteSprite;
            rowImage.color = RowColor;
            rowImage.raycastTarget = false;

            LayoutElement rowLayout = row.AddComponent<LayoutElement>();
            rowLayout.minHeight = 56f;
            rowLayout.preferredHeight = 56f;

            HorizontalLayoutGroup horizontal = row.AddComponent<HorizontalLayoutGroup>();
            horizontal.padding = new RectOffset(14, 12, 3, 3);
            horizontal.spacing = 10f;
            horizontal.childAlignment = TextAnchor.MiddleLeft;
            horizontal.childControlWidth = true;
            horizontal.childControlHeight = true;
            horizontal.childForceExpandWidth = false;
            horizontal.childForceExpandHeight = true;

            GameObject accent = CreateUiObject(row.transform, "Accent");
            Image accentImage = accent.AddComponent<Image>();
            accentImage.sprite = WhiteSprite;
            accentImage.color = value
                ? new Color(0.32f, 0.67f, 0.55f, 0.95f)
                : new Color(0.35f, 0.40f, 0.49f, 0.8f);
            accentImage.raycastTarget = false;
            LayoutElement accentLayout = accent.AddComponent<LayoutElement>();
            accentLayout.minWidth = 3f;
            accentLayout.preferredWidth = 3f;
            accentLayout.flexibleHeight = 1f;

            Text label = CreateText(row.transform, "Label", name, 17, Color.white);
            LayoutElement labelLayout = label.gameObject.AddComponent<LayoutElement>();
            labelLayout.flexibleWidth = 1f;
            labelLayout.minWidth = 150f;

            Button toggle = CreateButton(row.transform, "Toggle", value ? "ON" : "OFF", onClick, 14);
            Image toggleImage = toggle.targetGraphic as Image;
            Color toggleNormal = value
                ? new Color(0.18f, 0.40f, 0.34f, 1f)
                : new Color(0.17f, 0.20f, 0.26f, 1f);
            Color toggleHover = value
                ? new Color(0.20f, 0.40f, 0.34f, 1f)
                : new Color(0.20f, 0.25f, 0.32f, 1f);
            if (toggleImage != null)
            {
                toggleImage.color = Color.white;
            }
            ColorBlock toggleColors = toggle.colors;
            toggleColors.normalColor = toggleNormal;
            toggleColors.highlightedColor = toggleHover;
            toggleColors.selectedColor = toggleHover;
            toggleColors.pressedColor = new Color(0.08f, 0.11f, 0.15f, 1f);
            toggleColors.colorMultiplier = 1f;
            toggleColors.fadeDuration = 0.08f;
            toggle.colors = toggleColors;
            toggle.transition = Selectable.Transition.ColorTint;
            LayoutElement toggleLayout = toggle.gameObject.AddComponent<LayoutElement>();
            toggleLayout.preferredWidth = 92f;
            toggleLayout.minWidth = 92f;
            toggleLayout.preferredHeight = 42f;
        }

        private void CreateBenchSelectorRow(string selectedName)
        {
            GameObject row = CreateUiObject(_content, "BenchSelectorRow");
            LayoutElement rowLayout = row.AddComponent<LayoutElement>();
            rowLayout.preferredHeight = 60f;
            rowLayout.minHeight = 60f;

            HorizontalLayoutGroup horizontal = row.AddComponent<HorizontalLayoutGroup>();
            horizontal.spacing = 5f;
            horizontal.childAlignment = TextAnchor.MiddleCenter;
            horizontal.childControlWidth = true;
            horizontal.childControlHeight = true;
            horizontal.childForceExpandWidth = false;
            horizontal.childForceExpandHeight = true;

            string caption = selectedName ?? string.Empty;
            if (caption.Length > 48)
            {
                caption = caption.Substring(0, 45) + "...";
            }

            Button selector = CreateButton(
                row.transform,
                "BenchDropdown",
                caption,
                delegate
                {
                    _benchDropdownExpanded = !_benchDropdownExpanded;
                    if (_benchDropdownExpanded && _network != null && BenchPolicyController.IsConnected)
                    {
                        _network.RequestBenchSettings(BuildKnownBenchesJson(), true);
                    }
                    RebuildRows();
                },
                19);

            Text selectorLabel = selector.GetComponentInChildren<Text>();
            if (selectorLabel != null)
            {
                selectorLabel.text = caption;
            }

            LayoutElement selectorLayout = selector.gameObject.AddComponent<LayoutElement>();
            selectorLayout.flexibleWidth = 1f;
            selectorLayout.minWidth = 170f;
            selectorLayout.preferredHeight = 52f;

            if (_canManageBench)
            {
                Button lockToggle = CreateButton(
                    row.transform,
                    "ToggleSharedBenchLock",
                    _sharedBenchLocked ? "UNLOCK" : "LOCK",
                    delegate
                    {
                        if (_network == null)
                        {
                            return;
                        }

                        if (!_sharedBenchLocked && !_hasSharedBench)
                        {
                            _statusMessage = "Select a shared bench before locking it.";
                            UpdateStatusBar();
                            return;
                        }

                        _network.ToggleSharedBenchLock(BuildKnownBenchesJson());
                        _statusMessage = _sharedBenchLocked
                            ? "Requesting unlock of the shared bench..."
                            : "Requesting lock of the shared bench...";
                        UpdateStatusBar();
                    },
                    16);

                LayoutElement lockLayout = lockToggle.gameObject.AddComponent<LayoutElement>();
                lockLayout.preferredWidth = 176f;
                lockLayout.minWidth = 176f;
                lockLayout.preferredHeight = 52f;
            }

            Button useHost = CreateButton(
                row.transform,
                "UseHostBench",
                "USE HOST",
                delegate
                {
                    ClientPreferencesStore.SetPreferSharedBench(true);
                    _selectedBench = _hasSharedBench
                        ? new BenchLocation(
                            _sharedBenchScene,
                            _sharedBenchMarker,
                            _sharedBenchMapZone,
                            "Host-selected shared bench",
                            _sharedBenchPhantomMapId)
                        : null;
                    _statusMessage = "Using the host-selected bench by default.";
                    UpdateStatusBar();
                    RebuildRows();
                },
                16);
            LayoutElement useHostLayout = useHost.gameObject.AddComponent<LayoutElement>();
            useHostLayout.preferredWidth = 104f;
            useHostLayout.minWidth = 104f;
            useHostLayout.preferredHeight = 52f;
        }

        private static string GetShortBenchName(BenchLocation location)
        {
            if (location == null)
            {
                return string.Empty;
            }

            string baseName = string.IsNullOrWhiteSpace(location.BenchName)
                ? location.MarkerName + "  ·  " + location.SceneName
                : location.BenchName.Trim() + "  ·  " + location.SceneName;

            if (location.PhantomMapId > 0)
            {
                string phantomTitle = "Phantom Mantis Village #" + location.PhantomMapId;
                if (baseName.IndexOf(phantomTitle, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    baseName = phantomTitle + " (" + location.MarkerName + ")  ·  " + location.SceneName;
                }
            }

            return baseName;
        }

        private List<BenchLocation> BuildBenchOptions()
        {
            return _knownBenches
                .Where(location => location != null && location.IsValid)
                .Select(location => location.Copy())
                .OrderBy(location => location.SceneName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(location => location.MarkerName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static bool IsBenchOptionSelected(BenchLocation selected, BenchLocation option)
        {
            return selected != null && option != null && selected.IsSameLocation(option);
        }

        private void CreateBenchSearchRow()
        {
            GameObject row = CreateUiObject(_content, "BenchSearchRow");
            LayoutElement rowLayout = row.AddComponent<LayoutElement>();
            rowLayout.minHeight = 48f;
            rowLayout.preferredHeight = 48f;

            HorizontalLayoutGroup horizontal = row.AddComponent<HorizontalLayoutGroup>();
            horizontal.padding = new RectOffset(12, 12, 3, 3);
            horizontal.spacing = 10f;
            horizontal.childAlignment = TextAnchor.MiddleLeft;
            horizontal.childControlWidth = true;
            horizontal.childControlHeight = true;
            horizontal.childForceExpandWidth = false;
            horizontal.childForceExpandHeight = true;

            GameObject iconObject = CreateUiObject(row.transform, "SearchIcon");
            iconObject.transform.localRotation = Quaternion.Euler(0f, 0f, -45f);
            Image iconImage = iconObject.AddComponent<Image>();
            iconImage.sprite = BenchSearchIcon;
            iconImage.color = MutedTextColor;
            iconImage.raycastTarget = false;
            LayoutElement iconLayout = iconObject.AddComponent<LayoutElement>();
            iconLayout.minWidth = 24f;
            iconLayout.preferredWidth = 24f;
            iconLayout.minHeight = 24f;
            iconLayout.preferredHeight = 24f;

            GameObject inputObject = CreateUiObject(row.transform, "BenchSearchInput");
            Image inputBackground = inputObject.AddComponent<Image>();
            inputBackground.sprite = WhiteSprite;
            inputBackground.color = new Color(0.035f, 0.045f, 0.065f, 0.98f);
            inputBackground.raycastTarget = true;
            CanvasGroup inputGroup = inputObject.AddComponent<CanvasGroup>();
            inputGroup.interactable = true;
            inputGroup.blocksRaycasts = true;

            InputField input = inputObject.AddComponent<InputField>();
            input.targetGraphic = inputBackground;
            input.contentType = InputField.ContentType.Standard;
            input.lineType = InputField.LineType.SingleLine;
            input.characterLimit = 64;

            Text valueText = CreateText(inputObject.transform, "Text", _benchSearchQuery, 16, Color.white);
            valueText.alignment = TextAnchor.MiddleLeft;
            valueText.horizontalOverflow = HorizontalWrapMode.Overflow;
            valueText.verticalOverflow = VerticalWrapMode.Truncate;
            valueText.rectTransform.anchorMin = Vector2.zero;
            valueText.rectTransform.anchorMax = Vector2.one;
            valueText.rectTransform.offsetMin = new Vector2(10f, 1f);
            valueText.rectTransform.offsetMax = new Vector2(-10f, -1f);
            input.textComponent = valueText;
            input.interactable = true;
            input.readOnly = false;
            input.shouldActivateOnSelect = true;
            input.transition = Selectable.Transition.ColorTint;
            input.navigation = new Navigation { mode = Navigation.Mode.Automatic };

            Text placeholder = CreateText(inputObject.transform, "Placeholder", "Search benches...", 15, MutedTextColor);
            placeholder.fontStyle = FontStyle.Italic;
            placeholder.alignment = TextAnchor.MiddleLeft;
            placeholder.rectTransform.anchorMin = Vector2.zero;
            placeholder.rectTransform.anchorMax = Vector2.one;
            placeholder.rectTransform.offsetMin = new Vector2(10f, 1f);
            placeholder.rectTransform.offsetMax = new Vector2(-10f, -1f);
            input.placeholder = placeholder;
            input.text = _benchSearchQuery ?? string.Empty;
            input.onValueChanged.AddListener(ApplyBenchSearchFilter);

            LayoutElement inputLayout = inputObject.AddComponent<LayoutElement>();
            inputLayout.minWidth = 180f;
            inputLayout.preferredWidth = 180f;
            inputLayout.flexibleWidth = 1f;
            inputLayout.minHeight = 40f;
            inputLayout.preferredHeight = 40f;
        }

        private void CreateBenchNoResultsRow()
        {
            _benchNoResultsObject = CreateUiObject(_content, "BenchSearchNoResults");
            LayoutElement layout = _benchNoResultsObject.AddComponent<LayoutElement>();
            layout.minHeight = 38f;
            layout.preferredHeight = 38f;
            Text label = CreateText(_benchNoResultsObject.transform, "Text", "No matching benches.", 14, MutedTextColor);
            label.alignment = TextAnchor.MiddleCenter;
        }

        private void ApplyBenchSearchFilter(string query)
        {
            _benchSearchQuery = query ?? string.Empty;
            int visibleCount = 0;
            for (int i = 0; i < _benchOptionRows.Count; i++)
            {
                BenchOptionRow option = _benchOptionRows[i];
                if (option == null || option.Root == null)
                {
                    continue;
                }

                bool matches = string.IsNullOrWhiteSpace(_benchSearchQuery) ||
                    (option.SearchText ?? string.Empty).IndexOf(
                        _benchSearchQuery.Trim(),
                        StringComparison.OrdinalIgnoreCase) >= 0;
                option.Root.SetActive(matches);
                if (matches)
                {
                    visibleCount++;
                }
            }

            if (_benchNoResultsObject != null)
            {
                _benchNoResultsObject.SetActive(visibleCount == 0);
            }

            if (_content != null)
            {
                LayoutRebuilder.MarkLayoutForRebuild(_content);
            }
        }

        private void CreateBenchOptionRow(BenchLocation location)
        {
            if (location == null || !location.IsValid)
            {
                return;
            }

            bool selected = IsBenchOptionSelected(_selectedBench, location);
            string caption = GetShortBenchName(location);
            string searchText = string.Join(" ", new[]
            {
                caption,
                location.SceneName,
                location.MarkerName,
                location.BenchName ?? string.Empty
            });

            Button button = CreateButton(
                _content,
                "BenchOption_" + location.Key.GetHashCode(),
                caption,
                delegate
                {
                    _selectedBench = location.Copy();
                    ClientPreferencesStore.SetPreferSharedBench(false);
                    ClientPreferencesStore.SetPersonalBench(_selectedBench);
                    if (_canManageBench && !_sharedBenchLocked && _network != null)
                    {
                        _network.SetSelectedBench(_selectedBench, BuildKnownBenchesJson(_selectedBench));
                        _statusMessage = "Updating shared bench...";
                    }
                    else
                    {
                        _statusMessage = "Personal return bench selected.";
                    }

                    _benchDropdownExpanded = false;
                    _benchSearchQuery = string.Empty;
                    UpdateStatusBar();
                    RebuildRows();
                },
                18);

            Text label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.text = caption;
                label.alignment = TextAnchor.MiddleCenter;
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                label.verticalOverflow = VerticalWrapMode.Truncate;
                label.color = selected ? AccentTextColor : Color.white;
            }

            LayoutElement layout = button.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 50f;
            layout.minHeight = 50f;
            _benchOptionRows.Add(new BenchOptionRow
            {
                Root = button.gameObject,
                SearchText = searchText
            });
        }

        private static Sprite BenchSearchIcon
        {
            get
            {
                if (_benchSearchIcon == null)
                {
                    const int size = 32;
                    Color32[] pixels = new Color32[size * size];
                    Vector2 center = new Vector2(12f, 12f);
                    Vector2 handleStart = new Vector2(12f, 5.5f);
                    Vector2 handleEnd = new Vector2(12f, -0.5f);
                    for (int y = 0; y < size; y++)
                    {
                        for (int x = 0; x < size; x++)
                        {
                            Vector2 point = new Vector2(x + 0.5f, y + 0.5f);
                            float ringDistance = Mathf.Abs(Vector2.Distance(point, center) - 7f);
                            Vector2 segment = handleEnd - handleStart;
                            float lengthSquared = segment.sqrMagnitude;
                            float t = Mathf.Clamp01(Vector2.Dot(point - handleStart, segment) / lengthSquared);
                            float handleDistance = Vector2.Distance(point, handleStart + segment * t);
                            bool ring = ringDistance <= 1.7f;
                            bool handle = handleDistance <= 1.7f && t > 0.05f;
                            if (ring || handle)
                            {
                                byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(1.5f - Mathf.Min(ringDistance, handleDistance)) * 255f);
                                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                            }
                        }
                    }

                    Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                    texture.name = "HKMP.Rounds.BenchSearchIcon";
                    texture.SetPixels32(pixels);
                    texture.Apply(false, true);
                    _benchSearchIcon = Sprite.Create(
                        texture,
                        new Rect(0f, 0f, size, size),
                        new Vector2(0.5f, 0.5f),
                        100f);
                }

                return _benchSearchIcon;
            }
        }

        private void CreateVerticalSpacer(float height, string objectName)
        {
            GameObject spacer = CreateUiObject(_content, objectName);
            LayoutElement layout = spacer.AddComponent<LayoutElement>();
            layout.minHeight = Mathf.Max(0f, height);
            layout.preferredHeight = Mathf.Max(0f, height);
            layout.flexibleHeight = 0f;
            layout.flexibleWidth = 1f;
        }

        private void CreateActionRow(string caption, UnityEngine.Events.UnityAction onClick)
        {
            Button button = CreateButton(_content, "Action_" + caption.Replace(" ", string.Empty), caption, onClick, 15);
            LayoutElement layout = button.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 56f;
            layout.minHeight = 56f;

            Image background = button.targetGraphic as Image;
            if (background != null)
            {
                background.color = new Color(0.12f, 0.16f, 0.22f, 1f);
            }

            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.92f, 0.92f, 0.92f, 1f);
            colors.highlightedColor = new Color(1f, 0.94f, 0.78f, 1f);
            colors.pressedColor = new Color(0.72f, 0.78f, 0.86f, 1f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            Text label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.fontStyle = FontStyle.Bold;
                label.fontSize = 15;
                label.color = AccentTextColor;
                label.SetAllDirty();
            }
        }

        private void CreatePlayerWarpRow(string playerName, ushort playerId)
        {
            GameObject row = CreateUiObject(_content, "PlayerWarp_" + playerId);
            Image background = row.AddComponent<Image>();
            background.sprite = WhiteSprite;
            background.color = RowColor;
            background.raycastTarget = false;

            LayoutElement rowLayout = row.AddComponent<LayoutElement>();
            rowLayout.minHeight = 68f;
            rowLayout.preferredHeight = 68f;
            rowLayout.flexibleWidth = 1f;

            HorizontalLayoutGroup horizontal = row.AddComponent<HorizontalLayoutGroup>();
            horizontal.padding = new RectOffset(14, 12, 7, 7);
            horizontal.spacing = 12f;
            horizontal.childAlignment = TextAnchor.MiddleLeft;
            horizontal.childControlWidth = true;
            horizontal.childControlHeight = true;
            horizontal.childForceExpandWidth = false;
            horizontal.childForceExpandHeight = true;

            GameObject accent = CreateUiObject(row.transform, "Accent");
            Image accentImage = accent.AddComponent<Image>();
            accentImage.sprite = WhiteSprite;
            accentImage.color = new Color(0.36f, 0.66f, 0.82f, 1f);
            accentImage.raycastTarget = false;
            LayoutElement accentLayout = accent.AddComponent<LayoutElement>();
            accentLayout.minWidth = 4f;
            accentLayout.preferredWidth = 4f;
            accentLayout.flexibleHeight = 1f;

            GameObject details = CreateUiObject(row.transform, "PlayerDetails");
            VerticalLayoutGroup detailsLayout = details.AddComponent<VerticalLayoutGroup>();
            detailsLayout.spacing = 1f;
            detailsLayout.padding = new RectOffset(0, 0, 2, 2);
            detailsLayout.childAlignment = TextAnchor.MiddleLeft;
            detailsLayout.childControlWidth = true;
            detailsLayout.childControlHeight = true;
            detailsLayout.childForceExpandWidth = true;
            detailsLayout.childForceExpandHeight = false;
            LayoutElement detailsSize = details.AddComponent<LayoutElement>();
            detailsSize.minWidth = 180f;
            detailsSize.flexibleWidth = 1f;

            Text nameLabel = CreateText(details.transform, "PlayerName", playerName, 18, Color.white);
            nameLabel.fontStyle = FontStyle.Bold;
            nameLabel.verticalOverflow = VerticalWrapMode.Truncate;
            LayoutElement nameLayout = nameLabel.gameObject.AddComponent<LayoutElement>();
            nameLayout.minHeight = 26f;
            nameLayout.preferredHeight = 26f;

            Text idLabel = CreateText(details.transform, "PlayerId", "CONNECTED PLAYER  ·  ID " + playerId, 11, MutedTextColor);
            idLabel.fontStyle = FontStyle.Normal;
            LayoutElement idLayout = idLabel.gameObject.AddComponent<LayoutElement>();
            idLayout.minHeight = 18f;
            idLayout.preferredHeight = 18f;

            Button warp = CreateButton(
                row.transform,
                "WarpToPlayer_" + playerId,
                "WARP",
                delegate
                {
                    if (_network == null || !BenchPolicyController.IsConnected)
                    {
                        _statusMessage = "Connect to an HKMP server to request a player warp.";
                    }
                    else
                    {
                        _network.SendRoundAdminAction(
                            RoundAdminAction.WarpPlayerToBench,
                            playerId);
                        _statusMessage = "Bench warp requested for " + playerName + ".";
                    }

                    UpdateStatusBar();
                },
                14);

            LayoutElement warpLayout = warp.gameObject.AddComponent<LayoutElement>();
            warpLayout.minWidth = 112f;
            warpLayout.preferredWidth = 112f;
            warpLayout.minHeight = 48f;
            warpLayout.preferredHeight = 48f;

            Image warpBackground = warp.targetGraphic as Image;
            if (warpBackground != null)
            {
                warpBackground.color = new Color(0.19f, 0.39f, 0.43f, 1f);
            }

            ColorBlock warpColors = warp.colors;
            warpColors.normalColor = new Color(0.88f, 0.96f, 0.96f, 1f);
            warpColors.highlightedColor = Color.white;
            warpColors.pressedColor = new Color(0.62f, 0.82f, 0.83f, 1f);
            warp.colors = warpColors;

            Text warpLabel = warp.GetComponentInChildren<Text>();
            if (warpLabel != null)
            {
                warpLabel.fontStyle = FontStyle.Bold;
                warpLabel.color = Color.white;
                warpLabel.SetAllDirty();
            }
        }

        private void CreatePhantomMantisRow(PhantomMantisMap map)
        {
            if (map == null)
            {
                return;
            }

            int mapId = map.Id;
            string displayName = string.IsNullOrWhiteSpace(map.DisplayName)
                ? "Phantom Mantis Village #" + mapId
                : map.DisplayName;
            if (displayName.StartsWith("Phantom Mantis #", StringComparison.OrdinalIgnoreCase))
            {
                displayName = "Phantom Mantis Village #" + displayName.Substring("Phantom Mantis #".Length);
            }
            displayName = RoundsLocalization.Translate(displayName);

            GameObject row = CreateUiObject(_content, "PhantomMapRow_" + mapId);
            HorizontalLayoutGroup rowLayout = row.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 8f;
            rowLayout.padding = new RectOffset(0, 0, 0, 0);
            rowLayout.childAlignment = TextAnchor.MiddleCenter;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = true;

            LayoutElement rowSize = row.AddComponent<LayoutElement>();
            rowSize.minHeight = 48f;
            rowSize.preferredHeight = 48f;
            rowSize.flexibleWidth = 1f;

            Button travel = CreateButton(
                row.transform,
                "TravelToPhantom_" + mapId,
                displayName,
                delegate
                {
                    if (_network == null || !BenchPolicyController.IsConnected)
                    {
                        _statusMessage = "Connect to the HKMP host to travel to a phantom map.";
                    }
                    else
                    {
                        _network.TravelToPhantomMantis(
                            mapId,
                            GameManager.instance != null ? GameManager.instance.sceneName : string.Empty,
                            SceneTravelCatalog.BuildCurrentSceneJson(),
                            _phantomMapPage);
                        _statusMessage = "Requesting travel to " + displayName + "...";
                    }

                    UpdateStatusBar();
                },
                14);

            LayoutElement travelSize = travel.gameObject.AddComponent<LayoutElement>();
            travelSize.minHeight = 48f;
            travelSize.preferredHeight = 48f;
            travelSize.flexibleWidth = 1f;

            if (_canManageMapTravel)
            {
                Button delete = CreateButton(
                    row.transform,
                    "DeletePhantom_" + mapId,
                    string.Empty,
                    delegate
                    {
                        if (_network == null || !BenchPolicyController.IsConnected)
                        {
                            _statusMessage = "Connect to the HKMP host to delete a phantom map.";
                        }
                        else
                        {
                            _network.DeletePhantomMantis(
                                mapId,
                                GameManager.instance != null ? GameManager.instance.sceneName : string.Empty,
                                SceneTravelCatalog.BuildCurrentSceneJson(),
                                _phantomMapPage);
                            _statusMessage = "Deleting " + displayName + "...";
                        }

                        UpdateStatusBar();
                    },
                    14);

                Image deleteBackground = delete.targetGraphic as Image;
                if (deleteBackground != null)
                {
                    deleteBackground.color = new Color(0.34f, 0.09f, 0.13f, 1f);
                }

                ColorBlock deleteColors = delete.colors;
                deleteColors.normalColor = new Color(0.80f, 0.25f, 0.31f, 1f);
                deleteColors.highlightedColor = new Color(1f, 0.34f, 0.4f, 1f);
                deleteColors.pressedColor = new Color(0.25f, 0.04f, 0.06f, 1f);
                delete.colors = deleteColors;

                LayoutElement deleteSize = delete.gameObject.AddComponent<LayoutElement>();
                deleteSize.minWidth = 48f;
                deleteSize.preferredWidth = 48f;
                deleteSize.minHeight = 48f;
                deleteSize.preferredHeight = 48f;

                CreateTrashIcon(delete.transform);
            }
        }

        private void CreateTrashIcon(Transform parent)
        {
            CreateTrashPart(parent, "TrashHandle", new Vector2(9f, 3f), new Vector2(0f, 9f));
            CreateTrashPart(parent, "TrashLid", new Vector2(20f, 3f), new Vector2(0f, 6f));
            CreateTrashPart(parent, "TrashBody", new Vector2(15f, 16f), new Vector2(0f, -2f));
            CreateTrashPart(parent, "TrashRim", new Vector2(17f, 2f), new Vector2(0f, 5f));
            CreateTrashPart(parent, "TrashLineLeft", new Vector2(2f, 10f), new Vector2(-4f, -2f));
            CreateTrashPart(parent, "TrashLineRight", new Vector2(2f, 10f), new Vector2(4f, -2f));
        }

        private void CreateTrashPart(Transform parent, string name, Vector2 size, Vector2 position)
        {
            GameObject part = CreateUiObject(parent, name);
            RectTransform rect = part.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;

            Image image = part.AddComponent<Image>();
            image.sprite = WhiteSprite;
            image.color = Color.white;
            image.raycastTarget = false;
        }

        private void CreateInfoRow(string message)
        {
            GameObject row = CreateUiObject(_content, "InfoRow");
            Image background = row.AddComponent<Image>();
            background.sprite = WhiteSprite;
            background.color = new Color(0.045f, 0.058f, 0.082f, 0.88f);
            background.raycastTarget = false;
            LayoutElement layout = row.AddComponent<LayoutElement>();
            layout.minHeight = 42f;
            layout.preferredHeight = 46f;
            HorizontalLayoutGroup horizontal = row.AddComponent<HorizontalLayoutGroup>();
            horizontal.padding = new RectOffset(14, 12, 5, 5);
            horizontal.childAlignment = TextAnchor.MiddleLeft;
            horizontal.childControlWidth = true;
            horizontal.childControlHeight = true;
            horizontal.childForceExpandWidth = true;
            horizontal.childForceExpandHeight = true;
            Text label = CreateText(row.transform, "InfoText", message, 15, MutedTextColor);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            LayoutElement textLayout = label.gameObject.AddComponent<LayoutElement>();
            textLayout.flexibleWidth = 1f;
        }

        private void RequestSetting(RoundSettingKey key, bool boolValue, int intValue)
        {
            if (!_canManageSettings || _network == null || !BenchPolicyController.IsConnected)
            {
                _statusMessage = "Only the host or an authorized admin can change this setting.";
                UpdateStatusBar();
                return;
            }

            _statusMessage = "Updating round settings...";
            UpdateStatusBar();
            _network.SetRoundSetting(key, boolValue, intValue);
        }

        private void OnPvpModeReceived(PvpModePacket packet)
        {
            if (packet == null)
            {
                return;
            }

            bool modeChanged = _pvpMode != packet.Mode;
            bool permissionsChanged = _canManagePvpMode != packet.CanManage;
            _pvpMode = packet.Mode;
            _canManagePvpMode = packet.CanManage;
            _statusMessage = packet.Message ?? string.Empty;

            if (modeChanged && _visible)
            {
                _playModeAnimation = true;
            }

            UpdateStatusBar();

            if (_visible && (modeChanged || permissionsChanged))
            {
                RebuildRows();
            }
        }

        private void OnRoundSettingsReceived(RoundSettingsPacket packet)
        {
            if (packet == null)
            {
                return;
            }

            bool permissionsChanged = _canManageSettings != packet.CanManage;
            _canManageSettings = packet.CanManage;
            _winTarget = packet.WinTarget;
            ClearConfirmedNumericInputDraft("Round SOUL", packet.RoundSoul);
            ClearConfirmedNumericInputDraft("Ready player count (0 = all)", packet.ReadyRequiredCount);
            ClearConfirmedNumericInputDraft("Win target (0 = off)", packet.WinTarget);
            _roundActive = packet.RoundActive;
            _waitingForPreparation = packet.WaitingForPreparation;
            _statusMessage = packet.Message ?? string.Empty;
            UpdateStatusBar();

            string signature = BuildRoundSettingsUiSignature();
            bool settingsChanged = !string.Equals(
                signature,
                _lastRoundSettingsUiSignature,
                StringComparison.Ordinal);
            _lastRoundSettingsUiSignature = signature;

            if (_visible && (permissionsChanged || settingsChanged))
            {
                RebuildRows();
            }
        }

        private void OnRoundStatsReceived(RoundStatsPacket packet)
        {
            if (packet == null)
            {
                return;
            }

            _statsText = packet.Text ?? "Statistics are unavailable.";
            RoundStatsOverlay.SetStatistics(_statsText);
            if (_visible && _statisticsOnlyView)
            {
                RebuildRows();
            }
        }

        private void RequestMapTravelState(int page)
        {
            if (_network == null || !BenchPolicyController.IsConnected)
            {
                _statusMessage = "Connect to an HKMP server to load phantom map state.";
                UpdateStatusBar();
                return;
            }

            _network.RequestMapTravelState(
                GameManager.instance != null ? GameManager.instance.sceneName : string.Empty,
                "[]",
                page,
                false,
                PhantomMapClientController.CurrentPhantomMapId);
        }

        private void OnMapTravelStateReceived(MapTravelStatePacket packet)
        {
            if (packet == null)
            {
                return;
            }

            _mapTravelEnabled = packet.TravelEnabled;
            _canManageMapTravel = packet.CanManage;
            _phantomMapPage = Math.Max(0, packet.PhantomMapsPage);
            _phantomMapPageCount = Math.Max(1, packet.PhantomMapsPageCount);
            if (_phantomMapPage >= _phantomMapPageCount)
            {
                _phantomMapPage = _phantomMapPageCount - 1;
            }

            try
            {
                List<PhantomMantisMap> maps =
                    Newtonsoft.Json.JsonConvert.DeserializeObject<List<PhantomMantisMap>>(packet.PhantomMapsJson ?? "[]");
                _phantomMantisMaps.Clear();
                if (maps != null)
                {
                    _phantomMantisMaps.AddRange(maps.Where(map => map != null && map.IsValid));
                    _phantomMantisMaps.Sort((first, second) => first.Id.CompareTo(second.Id));
                }
            }
            catch
            {
                _phantomMantisMaps.Clear();
            }

            if (!string.IsNullOrWhiteSpace(packet.Message))
            {
                _statusMessage = packet.Message;
            }

            if (_visible && !_closing && !_statisticsOnlyView)
            {
                RebuildRows();
            }
            UpdateStatusBar();
        }

        private void OnBenchSettingsReceived(BenchSettingsPacket packet)
        {
            if (packet == null)
            {
                return;
            }

            string previousUiSignature = BuildBenchUiSignature();

            BenchPolicyController.ApplyServerState(packet);
            _canManageBench = packet.CanManageBench;
            _hasSharedBench = packet.HasSharedBench;
            _sharedBenchLocked = packet.SharedBenchLocked;
            _sharedBenchScene = _hasSharedBench ? packet.SceneName ?? string.Empty : string.Empty;
            _sharedBenchMarker = _hasSharedBench ? packet.MarkerName ?? string.Empty : string.Empty;
            _sharedBenchMapZone = _hasSharedBench ? packet.MapZone : 0;
            _sharedBenchPhantomMapId = _hasSharedBench ? Math.Max(0, packet.PhantomMapId) : 0;

            if (packet.HasBenchCatalog)
            {
                try
                {
                    List<BenchLocation> received =
                        Newtonsoft.Json.JsonConvert.DeserializeObject<List<BenchLocation>>(packet.BenchCatalogJson ?? "[]");
                    if (received != null)
                    {
                        if (received.Count > 0)
                        {
                            HashSet<int> authoritativePhantomMapIds = new HashSet<int>(
                                received
                                    .Where(location => location != null && location.IsValid && location.PhantomMapId > 0)
                                    .Select(location => location.PhantomMapId));

                            _knownBenches.RemoveAll(location =>
                                location != null &&
                                location.PhantomMapId > 0 &&
                                !authoritativePhantomMapIds.Contains(location.PhantomMapId));

                            BenchLocation savedSelection;
                            if (ClientPreferencesStore.TryGetPersonalBench(out savedSelection) &&
                                savedSelection != null &&
                                savedSelection.PhantomMapId > 0 &&
                                !authoritativePhantomMapIds.Contains(savedSelection.PhantomMapId))
                            {
                                ClientPreferencesStore.ClearPersonalBench();
                            }
                        }

                        MergeKnownBenches(received);
                    }
                }
                catch
                {
                }
            }

            if (_hasSharedBench)
            {
                BenchLocation shared = new BenchLocation(
                    _sharedBenchScene, _sharedBenchMarker, _sharedBenchMapZone, string.Empty, _sharedBenchPhantomMapId);
                if (!_knownBenches.Any(item => item.IsSameLocation(shared)))
                {
                    _knownBenches.Add(shared);
                }
            }

            BenchLocation personalSelection;
            bool hasPersonalSelection = ClientPreferencesStore.TryGetPersonalBench(out personalSelection);
            bool personalSelectionIsKnown = hasPersonalSelection &&
                _knownBenches.Any(item => item != null && item.IsSameLocation(personalSelection));
            if (hasPersonalSelection && !personalSelectionIsKnown &&
                BenchPolicyController.CanUseBenchLocation(personalSelection))
            {
                _knownBenches.Add(personalSelection.Copy());
                _knownBenches.Sort((first, second) =>
                {
                    int sceneCompare = StringComparer.OrdinalIgnoreCase.Compare(first.SceneName, second.SceneName);
                    return sceneCompare != 0
                        ? sceneCompare
                        : StringComparer.OrdinalIgnoreCase.Compare(first.MarkerName, second.MarkerName);
                });
                personalSelectionIsKnown = true;
            }
            else if (packet.HasBenchCatalog && hasPersonalSelection && !personalSelectionIsKnown)
            {
                ClientPreferencesStore.ClearPersonalBench();
                hasPersonalSelection = false;
            }

            if (_sharedBenchLocked && _hasSharedBench)
            {
                _selectedBench = new BenchLocation(
                    _sharedBenchScene,
                    _sharedBenchMarker,
                    _sharedBenchMapZone,
                    "Host-selected shared bench",
                    _sharedBenchPhantomMapId);
            }
            else if (ClientPreferencesStore.PreferSharedBench && _hasSharedBench)
            {
                _selectedBench = new BenchLocation(
                    _sharedBenchScene,
                    _sharedBenchMarker,
                    _sharedBenchMapZone,
                    "Host-selected shared bench",
                    _sharedBenchPhantomMapId);
            }
            else if (hasPersonalSelection && personalSelectionIsKnown)
            {
                _selectedBench = personalSelection.Copy();
            }
            else if (_hasSharedBench)
            {
                _selectedBench = new BenchLocation(
                    _sharedBenchScene,
                    _sharedBenchMarker,
                    _sharedBenchMapZone,
                    "Host-selected shared bench",
                    _sharedBenchPhantomMapId);
            }
            else
            {
                _selectedBench = _knownBenches.FirstOrDefault();
                if (_selectedBench != null)
                {
                    _selectedBench = _selectedBench.Copy();
                }
            }

            _statusMessage = packet.Message ?? string.Empty;
            UpdateStatusBar();

            if (_selectedBench == null && _knownBenches.Count > 0)
            {
                _selectedBench = _knownBenches[0];
            }

            string currentUiSignature = BuildBenchUiSignature();

            if (_visible && !string.Equals(
                    previousUiSignature,
                    currentUiSignature,
                    StringComparison.Ordinal))
            {
                RebuildRows();
            }
        }

        private string BuildRoundSettingsUiSignature()
        {
            if (_settings == null)
            {
                return _canManageSettings ? "manage" : "view";
            }

            return string.Join(
                "|",
                _canManageSettings,
                _settings.AutoStart,
                _settings.RoundSoul,
                _settings.ReadyRequiredCount,
                _settings.IndependentRoundsPerRoom,
                _settings.TimerIntegrationEnabled,
                _settings.DisableEnemies,
                _settings.DebugDisableShade,
                _settings.IndividualTeamWins,
                _settings.DisablePvpBeforeRound,
                _settings.GiveLifebloodMasks,
                _settings.VignetteEnabled,
                _settings.HidePhantomMantisBackground,
                _winTarget,
                _roundActive,
                _waitingForPreparation);
        }

        private string BuildBenchUiSignature()
        {
            List<string> parts = new List<string>
            {
                _canManageBench.ToString(),
                _hasSharedBench.ToString(),
                _sharedBenchLocked.ToString(),
                _sharedBenchScene ?? string.Empty,
                _sharedBenchMarker ?? string.Empty,
                _sharedBenchMapZone.ToString(),
                _sharedBenchPhantomMapId.ToString(),
                ClientPreferencesStore.PreferSharedBench.ToString(),
                _selectedBench == null ? string.Empty : _selectedBench.Key + "|" + (_selectedBench.BenchName ?? string.Empty)
            };

            foreach (BenchLocation location in _knownBenches
                         .Where(item => item != null && item.IsValid)
                         .OrderBy(item => item.SceneName, StringComparer.OrdinalIgnoreCase)
                         .ThenBy(item => item.MarkerName, StringComparer.OrdinalIgnoreCase)
                         .ThenBy(item => item.PhantomMapId))
            {
                parts.Add(location.SceneName);
                parts.Add(location.MarkerName);
                parts.Add(location.MapZone.ToString());
                parts.Add(location.PhantomMapId.ToString());
                parts.Add(location.BenchName ?? string.Empty);
            }

            return string.Join("|", parts);
        }

        private void RemoveUnavailableBenchSelections()
        {
            _knownBenches.RemoveAll(location => location == null || !location.IsValid ||
                !BenchPolicyController.CanUseBenchLocation(location));

            if (_selectedBench != null &&
                !_knownBenches.Any(location => location.IsSameLocation(_selectedBench)))
            {
                BenchLocation personalSelection;
                if (ClientPreferencesStore.TryGetPersonalBench(out personalSelection))
                {
                    _selectedBench = _knownBenches.FirstOrDefault(location => location.IsSameLocation(personalSelection));
                }
                else
                {
                    _selectedBench = _hasSharedBench
                        ? _knownBenches.FirstOrDefault(location =>
                            string.Equals(location.SceneName, _sharedBenchScene, StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(location.MarkerName, _sharedBenchMarker, StringComparison.OrdinalIgnoreCase) &&
                            location.PhantomMapId == _sharedBenchPhantomMapId)
                        : _knownBenches.FirstOrDefault();
                }
            }
        }

        private void MergeKnownBenches(IEnumerable<BenchLocation> locations)
        {
            foreach (BenchLocation location in locations)
            {
                if (location == null || !location.IsValid ||
                    !BenchPolicyController.CanUseBenchLocation(location))
                {
                    continue;
                }

                BenchLocation existing = _knownBenches.FirstOrDefault(item => item.IsSameLocation(location));
                if (existing == null)
                {
                    _knownBenches.Add(location.Copy());
                }
                else
                {
                    existing.MapZone = location.MapZone;
                    if (string.IsNullOrWhiteSpace(existing.BenchName) &&
                        !string.IsNullOrWhiteSpace(location.BenchName))
                    {
                        existing.BenchName = location.BenchName;
                    }
                }
            }

            _knownBenches.Sort((first, second) =>
            {
                int sceneCompare = StringComparer.OrdinalIgnoreCase.Compare(first.SceneName, second.SceneName);
                return sceneCompare != 0
                    ? sceneCompare
                    : StringComparer.OrdinalIgnoreCase.Compare(first.MarkerName, second.MarkerName);
            });
        }

        private string BuildKnownBenchesJson(BenchLocation include = null)
        {
            List<BenchLocation> locations = new List<BenchLocation>(_knownBenches);
            MergeIntoList(locations, BenchCatalog.GetBuiltInCatalog());
            MergeIntoList(locations, BenchCatalogScanner.ScanCurrentScene());
            if (include != null && BenchPolicyController.CanUseBenchLocation(include))
            {
                MergeIntoList(locations, new[] { include });
            }

            locations.RemoveAll(location => location == null ||
                !BenchPolicyController.CanUseBenchLocation(location));

            try
            {
                string json = Newtonsoft.Json.JsonConvert.SerializeObject(locations, Newtonsoft.Json.Formatting.None);
                return json.Length <= 45000 ? json : "[]";
            }
            catch
            {
                return "[]";
            }
        }

        private static void MergeIntoList(List<BenchLocation> target, IEnumerable<BenchLocation> source)
        {
            if (target == null || source == null)
            {
                return;
            }

            foreach (BenchLocation item in source)
            {
                if (item == null || !item.IsValid || target.Any(existing => existing.IsSameLocation(item)))
                {
                    continue;
                }

                target.Add(item.Copy());
            }
        }

        private void DiscoverCurrentSceneBenches()
        {
            if (!_visible || _network == null || !BenchPolicyController.IsConnected)
            {
                return;
            }

            string json = BenchCatalogScanner.ScanCurrentSceneJson();
            if (string.Equals(json, _lastSentUiCatalogJson, StringComparison.Ordinal))
            {
                return;
            }

            _lastSentUiCatalogJson = json;
            _network.DiscoverBenches(json);
        }

        private void OnActiveSceneChanged(Scene previous, Scene current)
        {
            if (_visible || _closing)
            {
                HideImmediatelyForGameplayTransition();
            }
            _lastSentUiCatalogJson = string.Empty;
            _sceneDiscoveryPending = false;
        }

        private void OnClientDisconnected()
        {
            _canManageBench = false;
            _canManageMapTravel = false;
            _mapTravelEnabled = false;
            _phantomMantisMaps.Clear();
            _canManageSettings = false;
            _canManagePvpMode = false;
            _hasSharedBench = false;
            _sharedBenchLocked = false;
            _sharedBenchScene = string.Empty;
            _sharedBenchMarker = string.Empty;
            _sharedBenchMapZone = 0;
            _sharedBenchPhantomMapId = 0;
            _statusMessage = "Disconnected from the HKMP server. Local map travel and selected-bench warp are still available.";
            BenchPolicyController.ClearServerState();
            UpdateRestoreCharmsButton();
            if (_visible)
            {
                RebuildRows();
                UpdateStatusBar();
            }
        }

        private void OnClientConnected()
        {
            if (_network == null || !BenchPolicyController.IsConnected)
            {
                return;
            }

            _statusMessage = "Connected to HKMP. Loading server settings...";
            _network.RequestPvpMode();
            _network.RequestRoundSettings();
            UpdateRestoreCharmsButton();
            if (_visible)
            {
                RebuildRows();
                UpdateStatusBar();
            }
        }

        private void CreateStatusBar(Transform panel)
        {
            if (panel == null)
            {
                return;
            }

            Transform existing = panel.Find("RoundsStatusBar");
            if (existing != null)
            {
                _statusBar = existing.GetComponent<Text>();
            }

            if (_statusBar == null)
            {
                _statusBar = CreateText(panel, "RoundsStatusBar", string.Empty, 14, MutedTextColor);
            }

            RectTransform rect = _statusBar.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.offsetMin = new Vector2(18f, 16f);
            rect.offsetMax = new Vector2(-178f, 40f);
            _statusBar.alignment = TextAnchor.MiddleLeft;
            _statusBar.horizontalOverflow = HorizontalWrapMode.Overflow;
            _statusBar.verticalOverflow = VerticalWrapMode.Truncate;
            _statusBar.raycastTarget = false;
            UpdateStatusBar();
        }

        private void UpdateStatusBar()
        {
            if (_statusBar == null)
            {
                return;
            }

            string defaultStatus = HKMP.Rounds.HKMPRounds.MenuHotkeyDisplayName + "  ·  " +
                RoundsLocalization.Translate("Close with Esc") + "  ·  " +
                RoundsLocalization.Translate("Scroll for more options");
            _statusBar.text = RoundsLocalization.Translate(string.IsNullOrWhiteSpace(_statusMessage)
                ? defaultStatus
                : _statusMessage);
        }

        private void ConfigureHeaderLayout()
        {
            ConfigureHeaderText(_superTitle, 118f, 30f, 24);
            ConfigureHeaderText(_mainTitle, 158f, 54f, 46);
            ConfigureHeaderText(_description, 226f, 30f, 17);
        }

        private static void ConfigureHeaderText(
            Text text,
            float topOffset,
            float height,
            int minimumFontSize)
        {
            if (text == null)
            {
                return;
            }

            RectTransform rect = text.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -topOffset);
            rect.sizeDelta = new Vector2(-84f, height);
            text.alignment = TextAnchor.MiddleCenter;
            text.fontSize = minimumFontSize;
            text.resizeTextForBestFit = false;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
        }

        private void ConfigureWindowVisuals()
        {
            if (_windowRect == null)
            {
                return;
            }

            RectMask2D windowClip = _windowRect.GetComponent<RectMask2D>();
            if (windowClip == null)
            {
                windowClip = _windowRect.gameObject.AddComponent<RectMask2D>();
            }

            windowClip.padding = Vector4.zero;
            _frameDecorationLayouts.Clear();
            RectTransform canvasParent = _windowRect.parent as RectTransform;
            if (canvasParent != null)
            {
                List<Transform> siblings = new List<Transform>();
                for (int i = 0; i < canvasParent.childCount; i++)
                {
                    Transform child = canvasParent.GetChild(i);
                    if (child != null && child != _windowRect)
                    {
                        siblings.Add(child);
                    }
                }

                Rect canvasBounds = canvasParent.rect;
                float canvasWidth = canvasBounds.width > 100f ? canvasBounds.width : 1920f;
                float canvasHeight = canvasBounds.height > 100f ? canvasBounds.height : 1080f;
                GameObject frameRootObject = CreateUiObject(_windowRect, "RoundsWindowFrameDecorations");
                RectTransform frameRootRect = frameRootObject.GetComponent<RectTransform>();
                frameRootRect.anchorMin = Vector2.zero;
                frameRootRect.anchorMax = Vector2.one;
                frameRootRect.offsetMin = Vector2.zero;
                frameRootRect.offsetMax = Vector2.zero;
                frameRootObject.transform.SetAsLastSibling();

                for (int i = 0; i < siblings.Count; i++)
                {
                    Transform item = siblings[i];
                    if (item == null)
                    {
                        continue;
                    }

                    RectTransform itemRect = item as RectTransform;
                    if (itemRect == null)
                    {
                        SetRaycastTargets(item, false);
                        item.gameObject.SetActive(false);
                        continue;
                    }

                    Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                        canvasParent,
                        itemRect);
                    float widthRatio = Mathf.Abs(bounds.size.x) / Mathf.Max(1f, canvasWidth);
                    float heightRatio = Mathf.Abs(bounds.size.y) / Mathf.Max(1f, canvasHeight);
                    float horizontalOffset = Mathf.Abs(bounds.center.x) / Mathf.Max(1f, canvasWidth);
                    float verticalOffset = Mathf.Abs(bounds.center.y) / Mathf.Max(1f, canvasHeight);
                    bool hasGraphic = item.GetComponentInChildren<Graphic>(true) != null;
                    bool fullScreenGraphic = widthRatio >= 0.96f && heightRatio >= 0.96f;
                    bool backdropDecoration = IsBackdropDecoration(item);
                    bool centeredFrame = hasGraphic &&
                                         widthRatio >= 0.48f &&
                                         heightRatio >= 0.48f &&
                                         horizontalOffset <= 0.10f &&
                                         verticalOffset <= 0.10f;

                    if (fullScreenGraphic && backdropDecoration)
                    {
                        item.gameObject.SetActive(false);
                        continue;
                    }

                    if (fullScreenGraphic && !backdropDecoration)
                    {
                        centeredFrame = true;
                    }
                    bool cornerDecoration = hasGraphic &&
                                            widthRatio <= 0.36f &&
                                            heightRatio <= 0.36f &&
                                            horizontalOffset >= 0.12f &&
                                            verticalOffset >= 0.12f;
                    bool horizontalDecoration = !centeredFrame && !cornerDecoration && hasGraphic &&
                                                widthRatio >= 0.18f &&
                                                heightRatio <= 0.36f &&
                                                verticalOffset >= 0.12f;
                    bool verticalDecoration = !centeredFrame && !cornerDecoration && !horizontalDecoration && hasGraphic &&
                                              heightRatio >= 0.18f &&
                                              widthRatio <= 0.36f &&
                                              horizontalOffset >= 0.12f;

                    if (!centeredFrame && !cornerDecoration && !horizontalDecoration && !verticalDecoration)
                    {
                        SetRaycastTargets(item, false);
                        item.gameObject.SetActive(false);
                        continue;
                    }

                    bool left = bounds.center.x < 0f;
                    bool top = bounds.center.y >= 0f;
                    float sourceWidth = Mathf.Max(1f, bounds.size.x);
                    float sourceHeight = Mathf.Max(1f, bounds.size.y);
                    float sourceAspect = sourceHeight / sourceWidth;

                    item.SetParent(frameRootRect, false);
                    item.gameObject.SetActive(true);
                    itemRect.localScale = Vector3.one;
                    itemRect.localRotation = Quaternion.identity;
                    SetRaycastTargets(item, false);

                    if (centeredFrame)
                    {
                        itemRect.anchorMin = Vector2.zero;
                        itemRect.anchorMax = Vector2.one;
                        itemRect.pivot = new Vector2(0.5f, 0.5f);
                        itemRect.offsetMin = Vector2.zero;
                        itemRect.offsetMax = Vector2.zero;
                    }
                    else if (horizontalDecoration)
                    {
                        float desiredHeight = Mathf.Clamp(
                            _windowRect.rect.width * sourceAspect,
                            52f,
                            160f);
                        float verticalAnchor = top ? 1f : 0f;
                        itemRect.anchorMin = new Vector2(0f, verticalAnchor);
                        itemRect.anchorMax = new Vector2(1f, verticalAnchor);
                        itemRect.pivot = new Vector2(0.5f, verticalAnchor);
                        itemRect.anchoredPosition = new Vector2(0f, top ? -4f : 4f);
                        itemRect.sizeDelta = new Vector2(-64f, desiredHeight);
                        _frameDecorationLayouts.Add(new FrameDecorationLayout
                        {
                            Transform = itemRect,
                            Kind = FrameDecorationKind.Horizontal,
                            Aspect = sourceAspect
                        });
                    }
                    else if (verticalDecoration)
                    {
                        float sourceAspectWidth = sourceWidth / sourceHeight;
                        float desiredWidth = Mathf.Clamp(
                            _windowRect.rect.height * sourceAspectWidth,
                            42f,
                            160f);
                        float horizontalAnchor = left ? 0f : 1f;
                        itemRect.anchorMin = new Vector2(horizontalAnchor, 0f);
                        itemRect.anchorMax = new Vector2(horizontalAnchor, 1f);
                        itemRect.pivot = new Vector2(horizontalAnchor, 0.5f);
                        itemRect.anchoredPosition = new Vector2(left ? 4f : -4f, 0f);
                        itemRect.sizeDelta = new Vector2(desiredWidth, -64f);
                        _frameDecorationLayouts.Add(new FrameDecorationLayout
                        {
                            Transform = itemRect,
                            Kind = FrameDecorationKind.Vertical,
                            Aspect = sourceAspect
                        });
                    }
                    else
                    {
                        float desiredWidth = Mathf.Clamp(
                            _windowRect.rect.width * 0.13f,
                            64f,
                            190f);
                        float desiredHeight = Mathf.Clamp(
                            desiredWidth * sourceAspect,
                            48f,
                            Mathf.Max(48f, _windowRect.rect.height * 0.24f));
                        float horizontalAnchor = left ? 0f : 1f;
                        float verticalAnchor = top ? 1f : 0f;
                        itemRect.anchorMin = new Vector2(horizontalAnchor, verticalAnchor);
                        itemRect.anchorMax = new Vector2(horizontalAnchor, verticalAnchor);
                        itemRect.pivot = new Vector2(horizontalAnchor, verticalAnchor);
                        itemRect.anchoredPosition = new Vector2(
                            left ? 4f : -4f,
                            top ? -4f : 4f);
                        itemRect.sizeDelta = new Vector2(desiredWidth, desiredHeight);
                        _frameDecorationLayouts.Add(new FrameDecorationLayout
                        {
                            Transform = itemRect,
                            Kind = FrameDecorationKind.Corner,
                            Aspect = sourceAspect
                        });
                    }

                    Graphic[] graphics = item.GetComponentsInChildren<Graphic>(true);
                    for (int graphicIndex = 0; graphicIndex < graphics.Length; graphicIndex++)
                    {
                        Graphic graphic = graphics[graphicIndex];
                        if (graphic != null)
                        {
                            Color color = graphic.color;
                            color.a *= 0.82f;
                            graphic.color = color;
                        }
                    }

                }

                frameRootObject.transform.SetSiblingIndex(2);
                frameRootObject.SetActive(true);
            }

            if (!_usingFallbackUi && _windowRect.Find("RoundsWindowBackdrop") == null)
            {
                GameObject backdrop = CreateUiObject(_windowRect, "RoundsWindowBackdrop");
                RectTransform backdropRect = backdrop.GetComponent<RectTransform>();
                backdropRect.anchorMin = Vector2.zero;
                backdropRect.anchorMax = Vector2.one;
                backdropRect.offsetMin = Vector2.zero;
                backdropRect.offsetMax = Vector2.zero;
                Image backdropImage = backdrop.AddComponent<Image>();
                backdropImage.sprite = WhiteSprite;
                backdropImage.color = new Color(0.018f, 0.020f, 0.030f, 0.46f);
                backdropImage.raycastTarget = false;
                backdrop.transform.SetAsFirstSibling();
            }

            if (_windowRect.Find("RoundsWindowOutline") == null)
            {
                CreateWindowOutline();
            }

            Transform backdropTransform = _windowRect.Find("RoundsWindowBackdrop");
            Transform outlineTransform = _windowRect.Find("RoundsWindowOutline");
            Transform frameDecorations = _windowRect.Find("RoundsWindowFrameDecorations");
            if (backdropTransform != null)
            {
                backdropTransform.SetAsFirstSibling();
            }

            if (frameDecorations != null)
            {
                frameDecorations.SetSiblingIndex(backdropTransform != null ? 1 : 0);
            }

            if (outlineTransform != null)
            {
                outlineTransform.SetAsLastSibling();
            }

            Transform resizeHandle = _windowRect.Find("RoundsWindowResizeHandle");
            if (resizeHandle != null)
            {
                resizeHandle.SetAsLastSibling();
            }

            RefreshFrameDecorationGeometry();
            _lastResponsiveWindowSize = _windowRect.rect.size;
        }

        private void RefreshFrameDecorationGeometry()
        {
            if (_windowRect == null)
            {
                return;
            }

            for (int i = 0; i < _frameDecorationLayouts.Count; i++)
            {
                FrameDecorationLayout item = _frameDecorationLayouts[i];
                if (item == null || item.Transform == null)
                {
                    continue;
                }

                RectTransform rect = item.Transform;
                switch (item.Kind)
                {
                    case FrameDecorationKind.Horizontal:
                        rect.sizeDelta = new Vector2(
                            -64f,
                            Mathf.Clamp(_windowRect.rect.width * item.Aspect, 52f, 160f));
                        break;
                    case FrameDecorationKind.Vertical:
                        rect.sizeDelta = new Vector2(
                            Mathf.Clamp(_windowRect.rect.height / Mathf.Max(0.01f, item.Aspect), 42f, 160f),
                            -64f);
                        break;
                    case FrameDecorationKind.Corner:
                        float width = Mathf.Clamp(_windowRect.rect.width * 0.13f, 64f, 190f);
                        float height = Mathf.Clamp(
                            width * item.Aspect,
                            48f,
                            Mathf.Max(48f, _windowRect.rect.height * 0.24f));
                        rect.sizeDelta = new Vector2(width, height);
                        break;
                }
            }
        }

        private void OnWindowResized()
        {
            if (_windowRect == null)
            {
                return;
            }

            RefreshFrameDecorationGeometry();
            Canvas.ForceUpdateCanvases();

            if (_content != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
            }

            if (_scrollRect != null && _scrollRect.viewport != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(_scrollRect.viewport);
            }

            _lastResponsiveWindowSize = _windowRect.rect.size;
        }

        private static bool IsBackdropDecoration(Transform item)
        {
            if (item == null)
            {
                return false;
            }

            string name = item.name.ToLowerInvariant();
            if (name.Contains("frame") ||
                name.Contains("border") ||
                name.Contains("ornament") ||
                name.Contains("trim"))
            {
                return false;
            }

            return name.Contains("background") ||
                   name.Contains("backdrop") ||
                   name.Contains("dimmer") ||
                   name.Contains("darken") ||
                   name.Contains("fade") ||
                   name.Contains("overlay") ||
                   name.Contains("shade") ||
                   name.Contains("blackout");
        }

        private void CreateWindowOutline()
        {
            GameObject outlineObject = CreateUiObject(_windowRect, "RoundsWindowOutline");
            RectTransform outlineRoot = outlineObject.GetComponent<RectTransform>();
            outlineRoot.anchorMin = Vector2.zero;
            outlineRoot.anchorMax = Vector2.one;
            outlineRoot.offsetMin = Vector2.zero;
            outlineRoot.offsetMax = Vector2.zero;

            Color borderColor = new Color(0.78f, 0.73f, 0.61f, 0.76f);
            CreateBorderPiece(outlineRoot, "Top", borderColor, new Vector2(0f, 2f), new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero);
            CreateBorderPiece(outlineRoot, "Bottom", borderColor, new Vector2(0f, 2f), new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero);
            CreateBorderPiece(outlineRoot, "Left", borderColor, new Vector2(2f, 0f), new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero);
            CreateBorderPiece(outlineRoot, "Right", borderColor, new Vector2(2f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), Vector2.zero);
            outlineObject.transform.SetSiblingIndex(_windowRect.Find("RoundsWindowBackdrop") != null ? 1 : 0);
        }

        private static void CreateBorderPiece(
            RectTransform parent,
            string name,
            Color color,
            Vector2 size,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition)
        {
            GameObject piece = CreateUiObject(parent, name);
            RectTransform rect = piece.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            bool horizontal = Mathf.Approximately(anchorMin.y, anchorMax.y);
            rect.pivot = horizontal
                ? new Vector2(0.5f, anchorMin.y)
                : new Vector2(anchorMin.x, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Image image = piece.AddComponent<Image>();
            image.sprite = WhiteSprite;
            image.color = color;
            image.raycastTarget = false;
        }

        private static void SetRaycastTargets(Transform root, bool enabled)
        {
            if (root == null)
            {
                return;
            }

            Graphic[] graphics = root.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                if (graphics[i] != null)
                {
                    graphics[i].raycastTarget = enabled;
                }
            }
        }

        private void BeginCursorOverride()
        {
            if (!_cursorSubscribed)
            {
                ModHooks.CursorHook -= ApplyCursorPolicy;
                ModHooks.CursorHook += ApplyCursorPolicy;
                _cursorSubscribed = true;
            }

            ForceCursorVisible();
        }

        private void EndCursorOverride()
        {
            if (_cursorSubscribed)
            {
                ModHooks.CursorHook -= ApplyCursorPolicy;
                _cursorSubscribed = false;
            }

            Cursor.visible = _cursorWasVisible;
            Cursor.lockState = _cursorWasLocked;
        }

        private static void ApplyCursorPolicy()
        {
            if (_instance != null && _instance._visible)
            {
                ForceCursorVisible();
            }
        }

        private static void ForceCursorVisible()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void NormalizeChildCanvasGroups()
        {
            if (_root == null)
            {
                return;
            }

            CanvasGroup[] groups = _root.GetComponentsInChildren<CanvasGroup>(true);
            for (int i = 0; i < groups.Length; i++)
            {
                CanvasGroup group = groups[i];
                if (group == null || group == _canvasGroup)
                {
                    continue;
                }

                group.alpha = 1f;
                group.interactable = true;
                group.blocksRaycasts = true;
            }
        }

        private void ConfigureOverlayCanvas()
        {
            if (_canvas == null && _root != null) _canvas = _root.GetComponentInChildren<Canvas>(true);
            if (_canvas == null) return;

            _canvas.enabled = true;
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.worldCamera = null;
            _canvas.overrideSorting = true;
            _canvas.sortingOrder = 32000;
            _canvas.pixelPerfect = true;
            _canvasRect = _canvas.GetComponent<RectTransform>();

            CanvasScaler scaler = _canvas.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = _canvas.gameObject.AddComponent<CanvasScaler>();
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;
            scaler.dynamicPixelsPerUnit = 1f;

            Canvas[] canvases = _root != null
                ? _root.GetComponentsInChildren<Canvas>(true)
                : new[] { _canvas };

            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];
                if (canvas == null)
                {
                    continue;
                }

                canvas.enabled = true;

                GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
                if (raycaster == null)
                {
                    raycaster = canvas.gameObject.AddComponent<GraphicRaycaster>();
                }

                raycaster.enabled = true;
                raycaster.ignoreReversedGraphics = true;
                raycaster.blockingObjects = GraphicRaycaster.BlockingObjects.None;
            }
        }

        private void EnsureEventSystem()
        {
            if (!_visible)
            {
                return;
            }

            if (_ownsEventSystem && _eventSystem != null)
            {
                if (!_eventSystem.gameObject.activeSelf)
                {
                    _eventSystem.gameObject.SetActive(true);
                }
                _eventSystem.enabled = true;
                EnsureStandaloneInputModule(_eventSystem);
                DisableCompetingEventSystems(_eventSystem);
                return;
            }

            EventSystem system = IsUsableEventSystem(_eventSystem)
                ? _eventSystem
                : IsUsableEventSystem(EventSystem.current)
                    ? EventSystem.current
                    : null;
            if (system == null)
            {
                EventSystem[] systems = Object.FindObjectsOfType<EventSystem>();
                for (int i = 0; i < systems.Length; i++)
                {
                    if (IsUsableEventSystem(systems[i]))
                    {
                        system = systems[i];
                        break;
                    }
                }
            }

            if (system == null)
            {
                GameObject eventSystemObject =
                    new GameObject("[HKMP.Rounds] Settings EventSystem");
                Object.DontDestroyOnLoad(eventSystemObject);
                _eventSystem = eventSystemObject.AddComponent<EventSystem>();
                _ownsEventSystem = true;
                EnsureStandaloneInputModule(_eventSystem);
                DisableCompetingEventSystems(_eventSystem);
                return;
            }

            _eventSystem = system;
            _ownsEventSystem = false;
            _eventSystem.enabled = true;
            EnsureStandaloneInputModule(_eventSystem);
            DisableCompetingEventSystems(_eventSystem);
        }

        private void DisableCompetingEventSystems(EventSystem activeSystem)
        {
            if (activeSystem == null)
            {
                return;
            }

            EventSystem[] systems = Object.FindObjectsOfType<EventSystem>();
            for (int i = 0; i < systems.Length; i++)
            {
                EventSystem system = systems[i];
                if (system == null || system == activeSystem)
                {
                    continue;
                }

                if (!_savedEventSystemStates.ContainsKey(system))
                {
                    _savedEventSystemStates.Add(system, system.enabled);
                }
                system.enabled = false;
            }
        }

        private void EnsureStandaloneInputModule(EventSystem system)
        {
            if (system == null)
            {
                return;
            }

            BaseInputModule[] modules = system.GetComponents<BaseInputModule>();
            for (int i = 0; i < modules.Length; i++)
            {
                BaseInputModule module = modules[i];
                if (module == null || module is StandaloneInputModule)
                {
                    continue;
                }

                if (!_savedInputModuleStates.ContainsKey(module))
                {
                    _savedInputModuleStates.Add(module, module.enabled);
                }
                module.enabled = false;
            }

            _standaloneInputModule = system.GetComponent<StandaloneInputModule>();
            if (_standaloneInputModule == null)
            {
                _standaloneInputModule = system.gameObject.AddComponent<StandaloneInputModule>();
                _standaloneModuleWasAdded = true;
                _standaloneOriginalEnabled = false;
                _standaloneOriginalForceModuleActive = false;
                _standaloneOriginalStateSaved = true;
            }
            else if (!_standaloneOriginalStateSaved)
            {
                _standaloneOriginalEnabled = _standaloneInputModule.enabled;
                _standaloneOriginalForceModuleActive = _standaloneInputModule.forceModuleActive;
                _standaloneOriginalStateSaved = true;
            }

            _standaloneInputModule.forceModuleActive = true;
            _standaloneInputModule.horizontalAxis = "Horizontal";
            _standaloneInputModule.verticalAxis = "Vertical";
            _standaloneInputModule.submitButton = "Submit";
            _standaloneInputModule.cancelButton = "Cancel";
            _standaloneInputModule.enabled = true;
            system.sendNavigationEvents = true;
        }

        private static bool IsUsableEventSystem(EventSystem system)
        {
            return system != null &&
                   system.isActiveAndEnabled &&
                   system.gameObject.activeInHierarchy;
        }

        private void RestoreEventSystemState()
        {
            if (_ownsEventSystem)
            {
                if (_eventSystem != null)
                {
                    _eventSystem.enabled = false;
                    _eventSystem.gameObject.SetActive(false);
                }
                RestoreCompetingEventSystems();
                return;
            }

            foreach (KeyValuePair<BaseInputModule, bool> pair in _savedInputModuleStates)
            {
                if (pair.Key != null)
                {
                    pair.Key.enabled = pair.Value;
                }
            }
            _savedInputModuleStates.Clear();

            if (_standaloneInputModule != null)
            {
                if (_standaloneModuleWasAdded)
                {
                    Object.Destroy(_standaloneInputModule);
                }
                else if (_standaloneOriginalStateSaved)
                {
                    _standaloneInputModule.forceModuleActive = _standaloneOriginalForceModuleActive;
                    _standaloneInputModule.enabled = _standaloneOriginalEnabled;
                }
            }

            _standaloneModuleWasAdded = false;
            _standaloneOriginalStateSaved = false;
            _standaloneInputModule = null;
            _eventSystem = null;
            _ownsEventSystem = false;
            RestoreCompetingEventSystems();
        }

        private void RestoreCompetingEventSystems()
        {
            foreach (KeyValuePair<EventSystem, bool> pair in _savedEventSystemStates)
            {
                if (pair.Key != null)
                {
                    pair.Key.enabled = pair.Value;
                }
            }
            _savedEventSystemStates.Clear();
        }

        private void FocusFirstInteractableControl()
        {
            if (!_visible || _root == null || _eventSystem == null || !_eventSystem.isActiveAndEnabled)
            {
                return;
            }

            Button[] buttons = _root.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                Button button = buttons[i];
                if (button == null || !button.isActiveAndEnabled || !button.interactable ||
                    !button.gameObject.activeInHierarchy)
                {
                    continue;
                }

                _eventSystem.SetSelectedGameObject(button.gameObject);
                return;
            }
        }

        private void TakeInputFromOtherUi()
        {
            EventSystem current = EventSystem.current;
            if (current == null)
            {
                return;
            }

            GameObject selected = current.currentSelectedGameObject;
            if (selected != null &&
                (_root == null || !selected.transform.IsChildOf(_root.transform)))
            {
                _previousSelectedObject = selected;
                _previousEventSystem = current;
            }

            current.SetSelectedGameObject(null);
        }

        private void KeepInputOnWindow()
        {
            if (!_visible || _root == null)
            {
                return;
            }

            EventSystem current = IsUsableEventSystem(_eventSystem)
                ? _eventSystem
                : EventSystem.current;
            if (current == null)
            {
                return;
            }

            GameObject selected = current.currentSelectedGameObject;
            if (selected == null || selected.transform.IsChildOf(_root.transform))
            {
                return;
            }

            if (_previousSelectedObject == null)
            {
                _previousSelectedObject = selected;
                _previousEventSystem = current;
            }

            current.SetSelectedGameObject(null);
        }

        private void RestoreOtherUiSelection()
        {
            EventSystem current = _previousEventSystem != null ? _previousEventSystem : EventSystem.current;
            if (current != null && _previousSelectedObject != null && _previousSelectedObject.activeInHierarchy)
            {
                current.SetSelectedGameObject(_previousSelectedObject);
            }

            _previousSelectedObject = null;
            _previousEventSystem = null;
        }

        private void ConfigureWindowInteraction()
        {
            if (_root == null) return;
            if (_windowRect == null)
            {
                Transform panel = _root.transform.Find("Panel");
                if (panel != null) _windowRect = panel as RectTransform;
            }

            if (_windowRect == null) return;

            LayoutGroup[] windowLayoutGroups = _windowRect.GetComponents<LayoutGroup>();
            for (int i = 0; i < windowLayoutGroups.Length; i++)
            {
                if (windowLayoutGroups[i] != null)
                {
                    windowLayoutGroups[i].enabled = false;
                }
            }

            ContentSizeFitter windowContentSizeFitter = _windowRect.GetComponent<ContentSizeFitter>();
            if (windowContentSizeFitter != null)
            {
                windowContentSizeFitter.enabled = false;
            }

            AspectRatioFitter windowAspectRatioFitter = _windowRect.GetComponent<AspectRatioFitter>();
            if (windowAspectRatioFitter != null)
            {
                windowAspectRatioFitter.enabled = false;
            }

            _windowRect.anchorMin = new Vector2(0.5f, 0.5f);
            _windowRect.anchorMax = new Vector2(0.5f, 0.5f);
            _windowRect.pivot = new Vector2(0.5f, 0.5f);
            _windowRect.anchoredPosition = Vector2.zero;
            _windowRect.localScale = Vector3.one;
            _windowRect.localRotation = Quaternion.identity;

            RectTransform layoutSpace = _windowRect.parent as RectTransform;
            float canvasWidth = layoutSpace != null
                ? layoutSpace.rect.width
                : _canvasRect != null ? _canvasRect.rect.width : 0f;
            float canvasHeight = layoutSpace != null
                ? layoutSpace.rect.height
                : _canvasRect != null ? _canvasRect.rect.height : 0f;
            if (canvasWidth < 100f)
            {
                canvasWidth = 1920f;
            }
            if (canvasHeight < 100f)
            {
                canvasHeight = 1080f;
            }
            float maxWidth = Mathf.Max(320f, canvasWidth - 48f);
            float maxHeight = Mathf.Max(240f, canvasHeight - 48f);
            float minWidth = Mathf.Min(320f, maxWidth);
            float minHeight = Mathf.Min(240f, maxHeight);
            Vector2 currentSize = _windowRect.sizeDelta;
            float initialWidth = _windowGeometryInitialized && currentSize.x > 0f
                ? currentSize.x
                : Mathf.Min(1260f, canvasWidth * 0.88f);
            float initialHeight = _windowGeometryInitialized && currentSize.y > 0f
                ? currentSize.y
                : Mathf.Min(990f, canvasHeight * 0.92f);
            if (!_windowGeometryInitialized)
            {
                _windowGeometryInitialized = true;
            }

            _windowRect.sizeDelta = new Vector2(
                Mathf.Clamp(initialWidth, minWidth, maxWidth),
                Mathf.Clamp(initialHeight, minHeight, maxHeight));
            _windowBaseScale = _windowRect.localScale;
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = false;
            }

            Transform existingDrag = _windowRect.Find("RoundsWindowDragHandle");
            if (existingDrag == null)
            {
                GameObject dragObject = CreateUiObject(_windowRect, "RoundsWindowDragHandle");
                RectTransform dragRect = dragObject.GetComponent<RectTransform>();
                dragRect.anchorMin = new Vector2(0f, 1f);
                dragRect.anchorMax = new Vector2(1f, 1f);
                dragRect.pivot = new Vector2(0.5f, 1f);
                dragRect.anchoredPosition = Vector2.zero;
                dragRect.sizeDelta = new Vector2(0f, 116f);
                Image dragImage = dragObject.AddComponent<Image>();
                dragImage.sprite = WhiteSprite;
                dragImage.color = new Color(1f, 1f, 1f, 0.01f);
                dragImage.raycastTarget = true;
                dragObject.AddComponent<RoundsWindowPointerHandle>().Initialize(
                    _windowRect,
                    layoutSpace != null ? layoutSpace : _canvasRect,
                    false);
                dragObject.transform.SetAsFirstSibling();
            }

            Transform existingResize = _windowRect.Find("RoundsWindowResizeHandle");
            if (existingResize == null)
            {
                GameObject resizeObject = CreateUiObject(_windowRect, "RoundsWindowResizeHandle");
                RectTransform resizeRect = resizeObject.GetComponent<RectTransform>();
                resizeRect.anchorMin = new Vector2(0f, 0f);
                resizeRect.anchorMax = new Vector2(0f, 0f);
                resizeRect.pivot = new Vector2(0f, 0f);
                resizeRect.anchoredPosition = new Vector2(4f, 4f);
                resizeRect.sizeDelta = new Vector2(26f, 26f);
                Image resizeImage = resizeObject.AddComponent<Image>();
                resizeImage.sprite = WhiteSprite;
                resizeImage.color = new Color(1f, 1f, 1f, 0.12f);
                resizeImage.raycastTarget = true;
                Text grip = CreateText(resizeObject.transform, "Grip", "↙", 17, MutedTextColor);
                grip.alignment = TextAnchor.MiddleCenter;
                grip.rectTransform.anchorMin = Vector2.zero;
                grip.rectTransform.anchorMax = Vector2.one;
                grip.rectTransform.offsetMin = Vector2.zero;
                grip.rectTransform.offsetMax = Vector2.zero;
                resizeObject.AddComponent<RoundsWindowPointerHandle>().Initialize(
                    _windowRect,
                    layoutSpace != null ? layoutSpace : _canvasRect,
                    true,
                    OnWindowResized);
                resizeObject.transform.SetAsLastSibling();
            }
        }

        private static GameObject FindChallengeDoorCanvas(
            Dictionary<string, Dictionary<string, GameObject>> preloadedObjects)
        {
            try
            {
                if (preloadedObjects == null)
                {
                    return null;
                }

                Dictionary<string, GameObject> atrium;
                GameObject challengeDoor;
                if (!preloadedObjects.TryGetValue("GG_Atrium", out atrium) ||
                    atrium == null ||
                    !atrium.TryGetValue("GG_Challenge_Door", out challengeDoor) ||
                    challengeDoor == null)
                {
                    return null;
                }

                PlayMakerFSM[] fsms = challengeDoor.GetComponentsInChildren<PlayMakerFSM>(true);
                for (int i = 0; i < fsms.Length; i++)
                {
                    PlayMakerFSM fsm = fsms[i];
                    if (fsm == null || !string.Equals(fsm.FsmName, "Challenge UI", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    FsmState state = fsm.Fsm == null ? null : fsm.Fsm.GetState("Open UI");
                    if (state == null || state.Actions == null)
                    {
                        continue;
                    }

                    for (int actionIndex = 0; actionIndex < state.Actions.Length; actionIndex++)
                    {
                        object action = state.Actions[actionIndex];
                        if (action == null || action.GetType().Name != "ShowBossDoorChallengeUI")
                        {
                            continue;
                        }

                        System.Reflection.FieldInfo field = action.GetType().GetField(
                            "prefab",
                            System.Reflection.BindingFlags.Instance |
                            System.Reflection.BindingFlags.Public |
                            System.Reflection.BindingFlags.NonPublic);

                        if (field == null)
                        {
                            continue;
                        }

                        object fsmGameObject = field.GetValue(action);
                        if (fsmGameObject == null)
                        {
                            continue;
                        }

                        System.Reflection.PropertyInfo valueProperty = fsmGameObject.GetType().GetProperty(
                            "Value",
                            System.Reflection.BindingFlags.Instance |
                            System.Reflection.BindingFlags.Public |
                            System.Reflection.BindingFlags.NonPublic);

                        GameObject prefab = valueProperty == null
                            ? null
                            : valueProperty.GetValue(fsmGameObject, null) as GameObject;

                        if (prefab != null)
                        {
                            return prefab;
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[HKMP.Rounds] Could not resolve the ToggleableBindings-style menu prefab: " + exception.Message);
            }

            return null;
        }

        private static Text FindText(Transform parent, string childName)
        {
            if (parent == null)
            {
                return null;
            }

            Transform child = parent.Find(childName);
            return child == null ? null : child.GetComponent<Text>();
        }

        private static void SetText(Text text, string value)
        {
            if (text != null)
            {
                text.text = RoundsLocalization.Translate(value ?? string.Empty);
                if (text.font != null && !string.IsNullOrEmpty(text.text))
                {
                    text.font.RequestCharactersInTexture(text.text, text.fontSize, text.fontStyle);
                }
            }
        }

        private static GameObject CreateUiObject(Transform parent, string name)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            return gameObject;
        }

        private static Text CreateText(
            Transform parent,
            string name,
            string value,
            int fontSize,
            Color color)
        {
            GameObject gameObject = CreateUiObject(parent, name);
            Text text = gameObject.AddComponent<Text>();
            text.font = GetHollowKnightUiFont();
            string textValue = value ?? string.Empty;
            text.text = textValue.IndexOf("<b>", StringComparison.Ordinal) >= 0 ||
                        textValue.IndexOf("<i>", StringComparison.Ordinal) >= 0
                ? textValue
                : RoundsLocalization.Translate(textValue);
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAnchor.MiddleLeft;
            text.fontStyle = GetBodyFontStyle();
            text.supportRichText = false;
            text.resizeTextForBestFit = false;
            text.alignByGeometry = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            if (text.font != null && !string.IsNullOrEmpty(text.text))
            {
                text.font.RequestCharactersInTexture(text.text, text.fontSize, text.fontStyle);
            }
            return text;
        }

        private static Button CreateButton(
            Transform parent,
            string name,
            string caption,
            UnityEngine.Events.UnityAction onClick,
            int fontSize)
        {
            GameObject gameObject = CreateUiObject(parent, name);
            Image background = gameObject.AddComponent<Image>();
            background.sprite = WhiteSprite;
            background.color = ButtonColor;
            background.raycastTarget = true;

            Button button = gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            ColorBlock colors = button.colors;
            colors.normalColor = ButtonColor;
            colors.highlightedColor = ButtonHoverColor;
            colors.pressedColor = new Color(0.08f, 0.08f, 0.1f, 1f);
            colors.disabledColor = new Color(0.08f, 0.08f, 0.08f, 0.55f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.onClick.AddListener(onClick);

            Text label = CreateText(gameObject.transform, "Text", caption, fontSize, Color.white);
            RectTransform rect = label.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(8f, 2f);
            rect.offsetMax = new Vector2(-8f, -2f);
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.resizeTextForBestFit = false;
            label.resizeTextMinSize = label.fontSize;
            label.resizeTextMaxSize = label.fontSize;
            return button;
        }

        private static Sprite _whiteSprite;
        private static Sprite WhiteSprite
        {
            get
            {
                if (_whiteSprite == null)
                {
                    _whiteSprite = Sprite.Create(
                        Texture2D.whiteTexture,
                        new Rect(0f, 0f, 1f, 1f),
                        new Vector2(0.5f, 0.5f),
                        1f);
                }

                return _whiteSprite;
            }
        }

        private static void SetAnchored(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(anchorMin.x, anchorMax.y);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        private void OnDestroy()
        {
            UnityEngine.SceneManagement.SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            if (_instance == this)
            {
                _instance = null;
            }
            if (_clientApi != null && _clientApi.ClientManager != null)
            {
                _clientApi.ClientManager.ConnectEvent -= OnClientConnected;
                _clientApi.ClientManager.DisconnectEvent -= OnClientDisconnected;
            }

            if (_network != null)
            {
                _network.BenchSettingsReceived -= OnBenchSettingsReceived;
                _network.PvpModeReceived -= OnPvpModeReceived;
                _network.RoundSettingsReceived -= OnRoundSettingsReceived;
                _network.RoundStatsReceived -= OnRoundStatsReceived;
                _network.MapTravelStateReceived -= OnMapTravelStateReceived;
            }

            _restoreCharmsButton = null;
            _restoreCharmsIcon = null;

            if (_showBuildSequence != null)
            {
                StopCoroutine(_showBuildSequence);
                _showBuildSequence = null;
            }
            if (_hideSequence != null)
            {
                StopCoroutine(_hideSequence);
                _hideSequence = null;
            }
            if (_visible || _closing)
            {
                _visible = false;
                _closing = false;
                EndCursorOverride();
            }
            _openedWhilePaused = false;

            RestoreEventSystemState();
            RestoreOtherUiSelection();
            if (_hudHiddenByRoundsUi &&
                GameCameras.instance != null &&
                GameCameras.instance.hudCanvas != null)
            {
                FSMUtility.SendEventToGameObject(GameCameras.instance.hudCanvas, "IN");
                _hudHiddenByRoundsUi = false;
            }

            if (_ownsEventSystem && _eventSystem != null)
            {
                GameObject eventSystemObject = _eventSystem.gameObject;
                _eventSystem = null;
                _standaloneInputModule = null;
                _ownsEventSystem = false;
                if (eventSystemObject != null)
                {
                    Object.Destroy(eventSystemObject);
                }
            }
        }
    }
}
