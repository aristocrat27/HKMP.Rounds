using System;
using System.IO;
using UnityEngine;

namespace HKMP.Rounds.Client
{
    internal sealed class RoundStatsOverlay : MonoBehaviour
    {
        private const float EditHoldDuration = 0.25f;
        private const float MinimumWidth = 220f;
        private const float MinimumHeight = 110f;
        private const float ResizeBorder = 10f;
        private const float ResizeHandleSize = 10f;
        private const float BorderThickness = 2f;
        private const float MinimumVisiblePixels = 40f;

        private static RoundStatsOverlay _instance;

        private ClientNetManager _network;
        private bool _visible;
        private bool _editing;
        private bool _keyHeld;
        private bool _longPressTriggered;
        private float _keyDownTime;
        private bool _windowRectInitialized;
        private Rect _windowRect;
        private bool _dragging;
        private Vector2 _dragOffset;
        private bool _resizing;
        private bool _editCursorWasVisible;
        private bool _editCursorStateCaptured;
        private bool _editCursorHookInstalled;
        private CursorLockMode _editCursorWasLocked;
        private ResizeMode _resizeMode;
        private Vector2 _resizeStartMouse;
        private Rect _resizeStartRect;
        private Vector2 _scrollPosition;
        private string _statisticsText = "Statistics have not been loaded.";
        private GUIStyle _titleStyle;
        private GUIStyle _statisticsStyle;
        private GUIStyle _footerStyle;
        private Font _gameFont;

        private enum ResizeMode
        {
            None,
            Left,
            Right,
            Top,
            Bottom,
            TopLeft,
            TopRight,
            BottomLeft,
            BottomRight
        }

        [Serializable]
        private sealed class WindowSettings
        {
            public float X;
            public float Y;
            public float Width;
            public float Height;
        }

        private static string SettingsPath
        {
            get
            {
                return Path.Combine(
                    Application.persistentDataPath,
                    "HKMP.Rounds.StatsOverlay.json");
            }
        }

        public static bool IsVisible
        {
            get { return _instance != null && _instance._visible; }
        }

        public static void Initialize(ClientNetManager network)
        {
            if (_instance != null)
            {
                _instance._network = network;
                return;
            }

            GameObject overlayObject =
                new GameObject("[HKMP.Rounds] Statistics Overlay");

            DontDestroyOnLoad(overlayObject);

            RoundStatsOverlay overlay =
                overlayObject.AddComponent<RoundStatsOverlay>();

            overlay._network = network;
        }

        public static bool HandleHotkey()
        {
            return _instance != null && _instance.ProcessHotkey();
        }

        public static void ToggleFromSettings()
        {
            if (_instance != null)
            {
                _instance.ToggleVisibility();
            }
        }

        public static void SetStatistics(string text)
        {
            if (_instance == null)
            {
                return;
            }

            _instance._statisticsText =
                string.IsNullOrWhiteSpace(text)
                    ? "Statistics are unavailable."
                    : text.TrimEnd();

            _instance._scrollPosition = Vector2.zero;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this);
                return;
            }

            _instance = this;
            InitializeWindowRect();
        }

        private bool ProcessHotkey()
        {
            KeyCode key = ClientPreferencesStore.StatsKey;

            if (Input.GetKeyDown(key))
            {
                _keyHeld = true;
                _longPressTriggered = false;
                _keyDownTime = Time.unscaledTime;
                return true;
            }

            if (_keyHeld && Input.GetKey(key))
            {
                if (!_longPressTriggered &&
                    Time.unscaledTime - _keyDownTime >= EditHoldDuration)
                {
                    _longPressTriggered = true;
                    EnterEditMode();
                }

                return true;
            }

            if (_keyHeld && !Input.GetKey(key))
            {
                bool wasLongPress = _longPressTriggered;
                _keyHeld = false;
                _longPressTriggered = false;

                if (wasLongPress)
                {
                    ExitEditMode();
                }
                else
                {
                    ToggleVisibility();
                }

                return true;
            }

            return false;
        }

        private void ToggleVisibility()
        {
            _visible = !_visible;

            if (_visible)
            {
                InitializeWindowRect();
                RequestStatistics();
            }
            else
            {
                if (_editing)
                {
                    ExitEditMode();
                }
                _dragging = false;
                _resizing = false;
                _resizeMode = ResizeMode.None;
            }
        }

        private void EnterEditMode()
        {
            InitializeWindowRect();
            _editing = true;
            _visible = true;
            _dragging = false;
            _resizing = false;
            _resizeMode = ResizeMode.None;
            _editCursorWasVisible = Cursor.visible;
            _editCursorWasLocked = Cursor.lockState;
            _editCursorStateCaptured = true;
            if (!_editCursorHookInstalled)
            {
                Modding.ModHooks.CursorHook -= ApplyEditCursorPolicy;
                Modding.ModHooks.CursorHook += ApplyEditCursorPolicy;
                _editCursorHookInstalled = true;
            }
            ForceEditCursorVisible();
            RequestStatistics();
        }

        private void ExitEditMode()
        {
            _editing = false;
            _dragging = false;
            _resizing = false;
            _resizeMode = ResizeMode.None;
            EndCursorOverride();
            SaveWindowSettings();
        }

        private void ApplyEditCursorPolicy()
        {
            if (_editing)
            {
                ForceEditCursorVisible();
            }
        }

        private void EndCursorOverride()
        {
            if (_editCursorHookInstalled)
            {
                Modding.ModHooks.CursorHook -= ApplyEditCursorPolicy;
                _editCursorHookInstalled = false;
            }

            if (_editCursorStateCaptured)
            {
                Cursor.visible = _editCursorWasVisible;
                Cursor.lockState = _editCursorWasLocked;
                _editCursorStateCaptured = false;
            }
        }

        private static void ForceEditCursorVisible()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void RequestStatistics()
        {
            if (_network != null && BenchPolicyController.IsConnected)
            {
                _network.RequestRoundStats();
            }
        }

        private void OnGUI()
        {
            if (!_visible)
            {
                return;
            }

            InitializeWindowRect();
            EnsureStyles();

            if (_editing)
            {
                ForceEditCursorVisible();
                HandleWindowEditor();
            }

            DrawOverlay();
        }

        private void DrawOverlay()
        {
            Color previousColor = GUI.color;
            GUI.color = new Color(0.025f, 0.030f, 0.043f, 0.80f);
            GUI.DrawTexture(_windowRect, Texture2D.whiteTexture);

            Rect headerRect = new Rect(
                _windowRect.x,
                _windowRect.y,
                _windowRect.width,
                32f);

            GUI.color = new Color(0.075f, 0.085f, 0.105f, 0.96f);
            GUI.DrawTexture(headerRect, Texture2D.whiteTexture);
            GUI.color = previousColor;

            GUI.Label(
                new Rect(
                    _windowRect.x + 12f,
                    _windowRect.y + 3f,
                    _windowRect.width - 24f,
                    26f),
                RoundsLocalization.Translate("ROUND STATISTICS"),
                _titleStyle);

            float footerHeight = _editing ? 22f : 8f;
            Rect contentRect = new Rect(
                _windowRect.x + 12f,
                _windowRect.y + 38f,
                Mathf.Max(80f, _windowRect.width - 24f),
                Mathf.Max(30f, _windowRect.height - 44f - footerHeight));

            string localizedStatisticsText =
                RoundsLocalization.Translate(_statisticsText);

            float textHeight = Mathf.Max(
                contentRect.height,
                _statisticsStyle.CalcHeight(
                    new GUIContent(localizedStatisticsText),
                    Mathf.Max(80f, contentRect.width - 16f)) + 8f);

            Rect viewRect = new Rect(
                0f,
                0f,
                Mathf.Max(80f, contentRect.width - 4f),
                textHeight);

            _scrollPosition = GUI.BeginScrollView(
                contentRect,
                _scrollPosition,
                viewRect,
                false,
                textHeight > contentRect.height);

            GUI.Label(
                new Rect(
                    2f,
                    2f,
                    viewRect.width - 4f,
                    textHeight - 4f),
                localizedStatisticsText,
                _statisticsStyle);

            GUI.EndScrollView();

            if (_editing)
            {
                string editHint = RoundsLocalization.Translate(
                    "EDIT MODE • HOLD {KEY} TO MOVE / RESIZE")
                    .Replace("{KEY}", ClientPreferencesStore.FormatKeyCode(ClientPreferencesStore.StatsKey));
                GUI.Label(
                    new Rect(
                        _windowRect.x + 10f,
                        _windowRect.y + _windowRect.height - 23f,
                        _windowRect.width - 20f,
                        18f),
                    editHint,
                    _footerStyle);

                DrawEditorBorder();
                DrawResizeHandle(_windowRect.x, _windowRect.y);
                DrawResizeHandle(_windowRect.x + _windowRect.width, _windowRect.y);
                DrawResizeHandle(_windowRect.x, _windowRect.y + _windowRect.height);
                DrawResizeHandle(
                    _windowRect.x + _windowRect.width,
                    _windowRect.y + _windowRect.height);
            }
        }

        private void HandleWindowEditor()
        {
            Event currentEvent = Event.current;
            if (currentEvent == null)
            {
                return;
            }

            Vector2 mousePosition = currentEvent.mousePosition;

            if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0)
            {
                ResizeMode mode = GetResizeMode(mousePosition);
                if (mode != ResizeMode.None)
                {
                    BeginResize(mode, mousePosition);
                    currentEvent.Use();
                    return;
                }

                if (_windowRect.Contains(mousePosition))
                {
                    BeginDrag(mousePosition);
                    currentEvent.Use();
                    return;
                }
            }

            if (currentEvent.type == EventType.MouseDrag && currentEvent.button == 0)
            {
                if (_dragging)
                {
                    UpdateDrag(mousePosition);
                    currentEvent.Use();
                    return;
                }

                if (_resizing)
                {
                    UpdateResize(mousePosition);
                    currentEvent.Use();
                    return;
                }
            }

            if (currentEvent.type == EventType.MouseUp && currentEvent.button == 0)
            {
                if (_dragging || _resizing)
                {
                    _dragging = false;
                    _resizing = false;
                    _resizeMode = ResizeMode.None;
                    SaveWindowSettings();
                    currentEvent.Use();
                }
            }
        }

        private ResizeMode GetResizeMode(Vector2 mousePosition)
        {
            bool nearLeft = Mathf.Abs(mousePosition.x - _windowRect.x) <= ResizeBorder;
            bool nearRight = Mathf.Abs(mousePosition.x - (_windowRect.x + _windowRect.width)) <= ResizeBorder;
            bool nearTop = Mathf.Abs(mousePosition.y - _windowRect.y) <= ResizeBorder;
            bool nearBottom = Mathf.Abs(mousePosition.y - (_windowRect.y + _windowRect.height)) <= ResizeBorder;

            if (nearLeft && nearTop) return ResizeMode.TopLeft;
            if (nearRight && nearTop) return ResizeMode.TopRight;
            if (nearLeft && nearBottom) return ResizeMode.BottomLeft;
            if (nearRight && nearBottom) return ResizeMode.BottomRight;
            if (nearLeft) return ResizeMode.Left;
            if (nearRight) return ResizeMode.Right;
            if (nearTop) return ResizeMode.Top;
            if (nearBottom) return ResizeMode.Bottom;
            return ResizeMode.None;
        }

        private void BeginDrag(Vector2 mousePosition)
        {
            _dragging = true;
            _dragOffset = mousePosition - new Vector2(_windowRect.x, _windowRect.y);
        }

        private void UpdateDrag(Vector2 mousePosition)
        {
            _windowRect.x = mousePosition.x - _dragOffset.x;
            _windowRect.y = mousePosition.y - _dragOffset.y;
            ClampWindowToScreen();
        }

        private void BeginResize(ResizeMode mode, Vector2 mousePosition)
        {
            _resizing = true;
            _resizeMode = mode;
            _resizeStartMouse = mousePosition;
            _resizeStartRect = _windowRect;
        }

        private void UpdateResize(Vector2 mousePosition)
        {
            Vector2 delta = mousePosition - _resizeStartMouse;
            float left = _resizeStartRect.x;
            float right = _resizeStartRect.x + _resizeStartRect.width;
            float top = _resizeStartRect.y;
            float bottom = _resizeStartRect.y + _resizeStartRect.height;

            switch (_resizeMode)
            {
                case ResizeMode.Left:
                    left = _resizeStartRect.x + delta.x;
                    break;
                case ResizeMode.Right:
                    right = _resizeStartRect.x + _resizeStartRect.width + delta.x;
                    break;
                case ResizeMode.Top:
                    top = _resizeStartRect.y + delta.y;
                    break;
                case ResizeMode.Bottom:
                    bottom = _resizeStartRect.y + _resizeStartRect.height + delta.y;
                    break;
                case ResizeMode.TopLeft:
                    left = _resizeStartRect.x + delta.x;
                    top = _resizeStartRect.y + delta.y;
                    break;
                case ResizeMode.TopRight:
                    right = _resizeStartRect.x + _resizeStartRect.width + delta.x;
                    top = _resizeStartRect.y + delta.y;
                    break;
                case ResizeMode.BottomLeft:
                    left = _resizeStartRect.x + delta.x;
                    bottom = _resizeStartRect.y + _resizeStartRect.height + delta.y;
                    break;
                case ResizeMode.BottomRight:
                    right = _resizeStartRect.x + _resizeStartRect.width + delta.x;
                    bottom = _resizeStartRect.y + _resizeStartRect.height + delta.y;
                    break;
            }

            if (right - left < MinimumWidth)
            {
                if (_resizeMode == ResizeMode.Left ||
                    _resizeMode == ResizeMode.TopLeft ||
                    _resizeMode == ResizeMode.BottomLeft)
                {
                    left = right - MinimumWidth;
                }
                else
                {
                    right = left + MinimumWidth;
                }
            }

            if (bottom - top < MinimumHeight)
            {
                if (_resizeMode == ResizeMode.Top ||
                    _resizeMode == ResizeMode.TopLeft ||
                    _resizeMode == ResizeMode.TopRight)
                {
                    top = bottom - MinimumHeight;
                }
                else
                {
                    bottom = top + MinimumHeight;
                }
            }

            _windowRect = Rect.MinMaxRect(left, top, right, bottom);

            float maximumWidth = Mathf.Max(MinimumWidth, Screen.width);
            float maximumHeight = Mathf.Max(MinimumHeight, Screen.height);
            if (_windowRect.width > maximumWidth) _windowRect.width = maximumWidth;
            if (_windowRect.height > maximumHeight) _windowRect.height = maximumHeight;
            ClampWindowToScreen();
        }

        private void InitializeWindowRect()
        {
            if (_windowRectInitialized)
            {
                return;
            }

            bool loaded = false;
            try
            {
                if (File.Exists(SettingsPath))
                {
                    WindowSettings settings =
                        JsonUtility.FromJson<WindowSettings>(File.ReadAllText(SettingsPath));

                    if (settings != null && settings.Width >= MinimumWidth && settings.Height >= MinimumHeight)
                    {
                        _windowRect = new Rect(
                            settings.X,
                            settings.Y,
                            settings.Width,
                            settings.Height);
                        loaded = true;
                    }
                }
            }
            catch (Exception exception)
            {
                Modding.Logger.LogError(
                    "[HKMP.Rounds] Could not load statistics overlay geometry: " +
                    exception.Message);
            }

            if (!loaded)
            {
                float width = Mathf.Min(340f, Mathf.Max(MinimumWidth, Screen.width - 48f));
                float height = Mathf.Min(190f, Mathf.Max(MinimumHeight, Screen.height - 48f));
                _windowRect = new Rect(
                    Mathf.Max(24f, Screen.width - width - 28f),
                    28f,
                    width,
                    height);
            }

            ClampWindowToScreen();
            _windowRectInitialized = true;
        }

        private void ClampWindowToScreen()
        {
            _windowRect.width = Mathf.Clamp(
                _windowRect.width,
                MinimumWidth,
                Mathf.Max(MinimumWidth, Screen.width));
            _windowRect.height = Mathf.Clamp(
                _windowRect.height,
                MinimumHeight,
                Mathf.Max(MinimumHeight, Screen.height));
            _windowRect.x = Mathf.Clamp(
                _windowRect.x,
                -_windowRect.width + MinimumVisiblePixels,
                Screen.width - MinimumVisiblePixels);
            _windowRect.y = Mathf.Clamp(
                _windowRect.y,
                -_windowRect.height + MinimumVisiblePixels,
                Screen.height - MinimumVisiblePixels);
        }

        private void SaveWindowSettings()
        {
            try
            {
                string directory = Path.GetDirectoryName(SettingsPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                WindowSettings settings = new WindowSettings
                {
                    X = _windowRect.x,
                    Y = _windowRect.y,
                    Width = _windowRect.width,
                    Height = _windowRect.height
                };

                File.WriteAllText(SettingsPath, JsonUtility.ToJson(settings, true));
            }
            catch (Exception exception)
            {
                Modding.Logger.LogError(
                    "[HKMP.Rounds] Could not save statistics overlay geometry: " +
                    exception.Message);
            }
        }

        private void EnsureStyles()
        {
            if (_titleStyle != null)
            {
                return;
            }

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.92f, 0.94f, 0.98f) }
            };

            _statisticsStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                alignment = TextAnchor.UpperLeft,
                wordWrap = true,
                richText = false,
                normal = { textColor = Color.white }
            };

            _footerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.88f, 0.90f, 0.94f) }
            };

            _gameFont = FindGameFont();
            if (_gameFont != null)
            {
                _titleStyle.font = _gameFont;
                _statisticsStyle.font = _gameFont;
                _footerStyle.font = _gameFont;
            }
        }

        private static Font FindGameFont()
        {
            Font regular = null;
            Font bold = null;
            Font[] fonts = Resources.FindObjectsOfTypeAll<Font>();

            for (int i = 0; i < fonts.Length; i++)
            {
                Font font = fonts[i];
                if (font == null)
                {
                    continue;
                }

                if (string.Equals(font.name, "TrajanPro-Regular", StringComparison.Ordinal))
                {
                    regular = font;
                }
                else if (string.Equals(font.name, "TrajanPro-Bold", StringComparison.Ordinal))
                {
                    bold = font;
                }
            }

            return regular ?? bold ?? GUI.skin.font;
        }

        private void DrawEditorBorder()
        {
            Color previousColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.95f);

            GUI.DrawTexture(
                new Rect(_windowRect.x, _windowRect.y, _windowRect.width, BorderThickness),
                Texture2D.whiteTexture);
            GUI.DrawTexture(
                new Rect(_windowRect.x, _windowRect.y + _windowRect.height - BorderThickness, _windowRect.width, BorderThickness),
                Texture2D.whiteTexture);
            GUI.DrawTexture(
                new Rect(_windowRect.x, _windowRect.y, BorderThickness, _windowRect.height),
                Texture2D.whiteTexture);
            GUI.DrawTexture(
                new Rect(_windowRect.x + _windowRect.width - BorderThickness, _windowRect.y, BorderThickness, _windowRect.height),
                Texture2D.whiteTexture);

            GUI.color = previousColor;
        }

        private void DrawResizeHandle(float x, float y)
        {
            Color previousColor = GUI.color;
            GUI.color = Color.white;
            float halfSize = ResizeHandleSize * 0.5f;
            GUI.DrawTexture(
                new Rect(x - halfSize, y - halfSize, ResizeHandleSize, ResizeHandleSize),
                Texture2D.whiteTexture);
            GUI.color = previousColor;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                if (_editing)
                {
                    ExitEditMode();
                }
                else
                {
                    EndCursorOverride();
                }

                _instance = null;
            }
        }
    }
}
