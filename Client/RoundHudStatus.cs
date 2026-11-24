using System;
using System.Collections;
using Modding;
using GlobalEnums;
using HKMP.Rounds.Networking;
using HKMP.Rounds.Networking.Packets;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HKMP.Rounds.Client
{
    internal sealed class RoundHudStatus : MonoBehaviour
    {
        private enum IconKind
        {
            Ready,
            NotReady
        }

        private enum ReadyHudEditTarget
        {
            None,
            PersonalReady,
            ReadyCount
        }

        private const float ReadyHotkeyCooldown = 0.35f;
        private const float ReadyHudEditHoldSeconds = 0.25f;
        private const float ReadyHudMinScale = 0.5f;
        private const float ReadyHudMaxScale = 3f;
        private const float ResizeHandlePixels = 16f;
        private const int IconSize = 256;

        private static readonly Color32 ReadyColor = new Color32(215, 235, 205, 255);
        private static readonly Color32 NotReadyColor = new Color32(220, 145, 145, 255);
        private ClientNetManager _network;
        private RoundSettingsPacket _state;
        private GameObject _geoPrefab;
        private GameObject _hudCanvasObject;
        private Vector3 _geoOrigin;
        private float _geoIconWorldWidth = 1f;
        private GameObject _personalReadyObject;
        private GameObject _readyCountObject;
        private DisplayItemAmount _readyCountDisplay;
        private Renderer _personalReadyRenderer;
        private Renderer _readyCountRenderer;
        private Sprite _readySprite;
        private Sprite _notReadySprite;
        private float _iconPixelsPerUnit = IconSize;
        private float _nextReadyRequestAt;
        private float _nextFreezeRequestAt;
        private bool _hookInstalled;
        private Vector2 _readyCountOffset;
        private float _readyCountScale = 1f;
        private Vector2 _personalReadyOffset;
        private float _personalReadyScale = 1f;
        private Vector3 _personalReadyBaseScale = Vector3.one;
        private Vector3 _personalReadyBasePosition;
        private Vector3 _readyCountBaseScale = Vector3.one;
        private Vector3 _readyCountBasePosition;
        private Camera _hudCamera;
        private bool _readyHudKeyHeld;
        private KeyCode _readyHudHeldKey;
        private bool _readyHudEditTriggered;
        private float _readyHudKeyDownTime;
        private bool _readyCountEditing;
        private bool _readyCountDragging;
        private bool _readyCountResizing;
        private ReadyHudEditTarget _editTarget;
        private Vector3 _readyCountStartMouseWorld;
        private Vector2 _readyCountStartOffset;
        private Vector2 _readyCountStartScreen;
        private float _readyCountStartScale;
        private bool _editCursorStateCaptured;
        private bool _editCursorWasVisible;
        private CursorLockMode _editCursorWasLocked;
        private bool _editCursorHookInstalled;
        private bool _roundHudManualOverride;
        private bool _lastRoundActive;
        private bool _hasObservedRoundActivity;

        public void Initialize(ClientNetManager network)
        {
            _network = network;
            _readyCountOffset = ClientPreferencesStore.ReadyHudOffset;
            _readyCountScale = ClientPreferencesStore.ReadyHudScale;
            _personalReadyOffset = ClientPreferencesStore.PersonalReadyOffset;
            _personalReadyScale = ClientPreferencesStore.PersonalReadyScale;
            if (_network != null)
            {
                _network.RoundSettingsReceived -= OnRoundSettingsReceived;
                _network.RoundSettingsReceived += OnRoundSettingsReceived;
            }

            if (!_hookInstalled)
            {
                On.HeroController.Awake += OnHeroAwake;
                ModHooks.SceneChanged += OnSceneChanged;
                _hookInstalled = true;
            }

            if (HeroController.instance != null)
            {
                StartCoroutine(SetupHudAfterSceneChange());
            }
        }

        private void OnHeroAwake(On.HeroController.orig_Awake orig, HeroController self)
        {
            orig(self);
            if (self != null && self == HeroController.instance)
            {
                StartCoroutine(SetupHudAfterSceneChange());
            }
        }

        private void OnSceneChanged(string sceneName)
        {
            _readyHudKeyHeld = false;
            _readyHudEditTriggered = false;
            _readyCountDragging = false;
            _readyCountResizing = false;
            if (_readyCountEditing)
            {
                ExitReadyCounterEdit();
            }

            StartCoroutine(SetupHudAfterSceneChange());
        }

        private IEnumerator SetupHudAfterSceneChange()
        {
            yield return null;
            yield return null;
            CreateHudObjects();
            UpdateHud();
        }

        private void CreateHudObjects()
        {
            if (_readyCountEditing)
            {
                ExitReadyCounterEdit();
            }

            RemoveHudObjects();

            GameManager gameManager = GameManager.instance;
            if (gameManager == null || gameManager.inventoryFSM == null ||
                gameManager.inventoryFSM.gameObject == null)
            {
                return;
            }

            Transform geoTransform = FindChildRecursive(
                gameManager.inventoryFSM.transform,
                "Geo");

            if (geoTransform == null)
            {
                return;
            }

            GameObject gameCamerasObject = GameObject.Find("_GameCameras");
            Transform gameCameras = gameCamerasObject != null
                ? gameCamerasObject.transform
                : null;
            if (gameCameras == null)
            {
                return;
            }

            Transform hudCamera = FindChildRecursive(gameCameras, "HudCamera");
            Transform hudCanvas = hudCamera != null
                ? FindChildRecursive(hudCamera, "Hud Canvas")
                : null;
            if (hudCanvas == null)
            {
                return;
            }

            _geoPrefab = geoTransform.gameObject;
            _hudCanvasObject = hudCanvas.gameObject;
            _hudCamera = hudCamera != null ? hudCamera.GetComponent<Camera>() : null;
            if (_hudCamera == null && hudCanvas != null)
            {
                _hudCamera = hudCanvas.GetComponentInParent<Camera>();
            }
            if (_hudCamera == null)
            {
                _hudCamera = Camera.main;
            }
            _geoOrigin = _geoPrefab.transform.position;
            SpriteRenderer geoSpriteRenderer = _geoPrefab.GetComponent<SpriteRenderer>();
            if (geoSpriteRenderer != null && geoSpriteRenderer.sprite != null &&
                geoSpriteRenderer.sprite.bounds.size.x > 0.001f)
            {
                float sourceWidth = geoSpriteRenderer.sprite.bounds.size.x;
                float sourceScale = Mathf.Abs(_geoPrefab.transform.lossyScale.x);
                _geoIconWorldWidth = Mathf.Max(0.1f, sourceWidth * sourceScale);
                _iconPixelsPerUnit = IconSize / sourceWidth;
            }

            EnsureSprites();
            _personalReadyObject = CreateIconObject(
                "HKMP.Rounds.ReadyStatus",
                _readySprite);
            _personalReadyRenderer = _personalReadyObject != null
                ? _personalReadyObject.GetComponent<SpriteRenderer>()
                : null;
            _personalReadyBaseScale = _personalReadyObject != null
                ? _personalReadyObject.transform.localScale
                : Vector3.one;
            _readyCountObject = CreateCountObject(
                "HKMP.Rounds.ReadyCount");
            _readyCountBaseScale = _readyCountObject != null
                ? _readyCountObject.transform.localScale
                : Vector3.one;
            LayoutHudObjects();
        }

        private GameObject CreateIconObject(string objectName, Sprite icon)
        {
            GameObject clone = Object.Instantiate(
                _geoPrefab,
                _hudCanvasObject.transform,
                true);
            clone.name = objectName;
            clone.transform.position = _geoOrigin;

            SpriteRenderer renderer = clone.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.sprite = icon;
                renderer.color = Color.white;
                renderer.enabled = true;
                renderer.drawMode = SpriteDrawMode.Simple;
            }

            Renderer[] renderers = clone.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && renderers[i] != renderer)
                {
                    renderers[i].enabled = false;
                }
            }

            DisplayItemAmount amount = clone.GetComponent<DisplayItemAmount>();
            if (amount != null)
            {
                amount.enabled = false;
            }

            Transform amountRoot = FindChildRecursive(clone.transform, "Geo Amount");
            if (amountRoot != null)
            {
                amountRoot.gameObject.SetActive(false);
            }

            Collider2D collider = clone.GetComponent<Collider2D>();
            if (collider != null)
            {
                collider.enabled = false;
            }

            return clone;
        }

        private float GetGeoAmountFontSize()
        {
            DisplayItemAmount geoAmount = _geoPrefab != null
                ? _geoPrefab.GetComponent<DisplayItemAmount>()
                : null;

            return geoAmount != null && geoAmount.textObject != null
                ? geoAmount.textObject.fontSize
                : 4f;
        }

        private GameObject CreateCountObject(string objectName)
        {
            GameObject clone = Object.Instantiate(
                _geoPrefab,
                _hudCanvasObject.transform,
                true);
            clone.name = objectName;
            clone.transform.position = _geoOrigin;

            SpriteRenderer renderer = clone.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.enabled = false;
            }

            _readyCountDisplay = clone.GetComponent<DisplayItemAmount>();
            if (_readyCountDisplay != null)
            {
                _readyCountDisplay.playerDataInt = "hkmpRoundsReadyCount";
                _readyCountDisplay.enabled = false;
                if (_readyCountDisplay.textObject != null)
                {
                    _readyCountDisplay.textObject.text = "0/0";
                    _readyCountDisplay.textObject.fontSize = GetGeoAmountFontSize();
                    _readyCountRenderer = _readyCountDisplay.textObject.GetComponent<Renderer>();
                }
            }

            Transform amountRoot = FindChildRecursive(clone.transform, "Geo Amount");
            if (amountRoot != null)
            {
                amountRoot.gameObject.SetActive(true);
            }

            Collider2D collider = clone.GetComponent<Collider2D>();
            if (collider != null)
            {
                collider.enabled = false;
            }

            return clone;
        }

        private void EnsureSprites()
        {
            if (_readySprite != null)
            {
                return;
            }

            _readySprite = CreateIconSprite(IconKind.Ready, ReadyColor, _iconPixelsPerUnit);
            _notReadySprite = CreateIconSprite(IconKind.NotReady, NotReadyColor, _iconPixelsPerUnit);
        }

        private static Sprite CreateIconSprite(IconKind kind, Color32 color, float pixelsPerUnit)
        {
            Color32[] pixels = new Color32[IconSize * IconSize];
            float center = (IconSize - 1f) * 0.5f;
            float glowRadius = 117f;
            float ringRadius = 82f;
            float ringThickness = 13f;
            float centerRadius = 35f;

            for (int y = 0; y < IconSize; y++)
            {
                for (int x = 0; x < IconSize; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    Color32 pixel = new Color32(0, 0, 0, 0);

                    float glowCoverage = Mathf.Clamp01((glowRadius + 0.5f - distance) / 2.5f);
                    if (glowCoverage > 0f)
                    {
                        pixel = WithAlpha(color, Mathf.RoundToInt(38f * glowCoverage));
                    }

                    float ringCoverage = Mathf.Clamp01((ringThickness * 0.5f + 0.75f - Mathf.Abs(distance - ringRadius)) / 1.5f);
                    if (distance <= ringRadius + ringThickness * 0.5f + 0.75f && ringCoverage > 0f)
                    {
                        pixel = WithAlpha(color, Mathf.RoundToInt(color.a * ringCoverage));
                    }

                    float centerCoverage = Mathf.Clamp01((centerRadius + 0.75f - distance) / 1.5f);
                    if (distance <= centerRadius + 0.75f && centerCoverage > 0f)
                    {
                        pixel = WithAlpha(color, Mathf.RoundToInt(238f * centerCoverage));
                    }

                    pixels[y * IconSize + x] = pixel;
                }
            }

            Texture2D texture = new Texture2D(
                IconSize,
                IconSize,
                TextureFormat.RGBA32,
                false);
            texture.name = "HKMP.Rounds." + kind + ".VectorTexture";
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            texture.hideFlags = HideFlags.DontUnloadUnusedAsset;

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, IconSize, IconSize),
                new Vector2(0.5f, 0.5f),
                Mathf.Max(1f, pixelsPerUnit));
            sprite.name = "HKMP.Rounds." + kind + ".VectorIcon";
            sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return sprite;
        }


        private static Color32 WithAlpha(Color32 color, int alpha)
        {
            return new Color32(
                color.r,
                color.g,
                color.b,
                (byte)Mathf.Clamp(alpha, 0, 255));
        }

        private void OnRoundSettingsReceived(RoundSettingsPacket packet)
        {
            if (packet == null)
            {
                return;
            }

            _state = packet;
            UpdateHud();
        }

        private void Update()
        {
            bool roundActive = IsRoundActive();
            if (!_hasObservedRoundActivity || roundActive != _lastRoundActive)
            {
                _hasObservedRoundActivity = true;
                _lastRoundActive = roundActive;
                _roundHudManualOverride = false;
            }

            if (!GameFreezeController.IsFrozen)
            {
                HandleReadyCounterHotkey();
            }

            if (_state != null && _state.CanManage &&
                Time.unscaledTime >= _nextFreezeRequestAt &&
                !RoundsSettingsUI.IsVisible &&
                GameManager.instance != null &&
                GameManager.instance.gameState == GameState.PLAYING &&
                Input.GetKeyDown(ClientPreferencesStore.FreezeKey))
            {
                _nextFreezeRequestAt = Time.unscaledTime + 0.35f;
                _network?.SendRoundAdminAction(RoundAdminAction.ToggleGameFreeze, 0);
            }

            if (!GameFreezeController.IsFrozen && !_readyHudKeyHeld && !_readyCountEditing && CanToggleReady() &&
                Input.GetKeyDown(ClientPreferencesStore.ReadyKey))
            {
                if (_network != null && Time.unscaledTime >= _nextReadyRequestAt)
                {
                    _nextReadyRequestAt = Time.unscaledTime + ReadyHotkeyCooldown;
                    _network.SendRoundAdminAction(RoundAdminAction.ToggleReady, 0);
                }
            }

            if (!GameFreezeController.IsFrozen && _readyCountEditing)
            {
                ForceEditCursorVisible();
                HandleReadyCounterEditorInput();
            }

            UpdateHudVisibility();
            UpdateIndicatorGlow();
        }

        private void LateUpdate()
        {
            LayoutHudObjects();
        }

        private void UpdateIndicatorGlow()
        {
            float pulse = 0.88f + 0.12f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3.2f));
            bool ready = _state != null && _state.LocalPlayerReady;

            SpriteRenderer personalRenderer = _personalReadyRenderer as SpriteRenderer;
            if (personalRenderer != null)
            {
                personalRenderer.color = new Color(1f, 1f, 1f, ready ? pulse : 1f);
            }
        }

        private void HandleReadyCounterHotkey()
        {
            if (RoundsSettingsUI.IsVisible)
            {
                _readyHudKeyHeld = false;
                _readyHudEditTriggered = false;
                _readyCountDragging = false;
                _readyCountResizing = false;
                if (_readyCountEditing)
                {
                    ExitReadyCounterEdit();
                }
                return;
            }

            if (!_readyHudKeyHeld && Input.GetKeyDown(ClientPreferencesStore.ReadyHudKey))
            {
                _readyHudHeldKey = ClientPreferencesStore.ReadyHudKey;
                _readyHudKeyHeld = true;
                _readyHudEditTriggered = false;
                _readyHudKeyDownTime = Time.unscaledTime;
            }

            if (!_readyHudKeyHeld)
            {
                return;
            }

            if (Input.GetKey(_readyHudHeldKey))
            {
                if (!_readyHudEditTriggered &&
                    Time.unscaledTime - _readyHudKeyDownTime >= ReadyHudEditHoldSeconds)
                {
                    _readyHudEditTriggered = true;
                    EnterReadyCounterEdit();
                }
                return;
            }

            if (_readyCountEditing)
            {
                ExitReadyCounterEdit();
            }
            else if (!_readyHudEditTriggered)
            {
                if (IsRoundActive())
                {
                    _roundHudManualOverride = !_roundHudManualOverride;
                }
                else
                {
                    ClientPreferencesStore.SetShowReadyCount(!ClientPreferencesStore.ShowReadyCount);
                }

                UpdateHudVisibility();
                LayoutHudObjects();
            }

            _readyHudKeyHeld = false;
            _readyHudEditTriggered = false;
        }

        private void EnterReadyCounterEdit()
        {
            if (_state == null ||
                ((_personalReadyObject == null || _personalReadyRenderer == null) &&
                 (_readyCountObject == null || _readyCountRenderer == null)))
            {
                return;
            }

            _readyCountOffset = ClientPreferencesStore.ReadyHudOffset;
            _readyCountScale = ClientPreferencesStore.ReadyHudScale;
            _personalReadyOffset = ClientPreferencesStore.PersonalReadyOffset;
            _personalReadyScale = ClientPreferencesStore.PersonalReadyScale;
            ClientPreferencesStore.SetShowPersonalReadyStatus(true);
            ClientPreferencesStore.SetShowReadyCount(true);
            if (IsRoundActive())
            {
                _roundHudManualOverride = true;
            }
            UpdateHudVisibility();
            LayoutHudObjects();

            _readyCountEditing = true;
            _readyCountDragging = false;
            _readyCountResizing = false;
            _editTarget = ReadyHudEditTarget.None;
            _editCursorWasVisible = Cursor.visible;
            _editCursorWasLocked = Cursor.lockState;
            _editCursorStateCaptured = true;
            if (!_editCursorHookInstalled)
            {
                ModHooks.CursorHook -= ApplyEditCursorPolicy;
                ModHooks.CursorHook += ApplyEditCursorPolicy;
                _editCursorHookInstalled = true;
            }
            ForceEditCursorVisible();
        }

        private void ExitReadyCounterEdit()
        {
            _readyCountEditing = false;
            _readyCountDragging = false;
            _readyCountResizing = false;
            _editTarget = ReadyHudEditTarget.None;
            ClientPreferencesStore.SetReadyHudLayout(_readyCountOffset, _readyCountScale);
            ClientPreferencesStore.SetPersonalReadyLayout(_personalReadyOffset, _personalReadyScale);

            if (_editCursorHookInstalled)
            {
                ModHooks.CursorHook -= ApplyEditCursorPolicy;
                _editCursorHookInstalled = false;
            }

            if (_editCursorStateCaptured)
            {
                Cursor.visible = _editCursorWasVisible;
                Cursor.lockState = _editCursorWasLocked;
                _editCursorStateCaptured = false;
            }
        }

        private void ApplyEditCursorPolicy()
        {
            if (_readyCountEditing)
            {
                ForceEditCursorVisible();
            }
        }

        private static void ForceEditCursorVisible()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void HandleReadyCounterEditorInput()
        {
            if (_hudCamera == null)
            {
                return;
            }

            Vector2 mouse = Input.mousePosition;
            if (Input.GetMouseButtonDown(0))
            {
                Rect personalRect = GetRendererScreenRect(_personalReadyRenderer);
                Rect countRect = GetRendererScreenRect(_readyCountRenderer);
                Rect personalHandle = GetResizeHandle(personalRect, true);
                Rect countHandle = GetResizeHandle(countRect, false);

                if (personalHandle.Contains(mouse))
                {
                    _editTarget = ReadyHudEditTarget.PersonalReady;
                    _readyCountResizing = true;
                    _readyCountDragging = false;
                    _readyCountStartScreen = mouse;
                    _readyCountStartScale = _personalReadyScale;
                }
                else if (countHandle.Contains(mouse))
                {
                    _editTarget = ReadyHudEditTarget.ReadyCount;
                    _readyCountResizing = true;
                    _readyCountDragging = false;
                    _readyCountStartScreen = mouse;
                    _readyCountStartScale = _readyCountScale;
                }
                else if (countRect.Contains(mouse))
                {
                    _editTarget = ReadyHudEditTarget.ReadyCount;
                    _readyCountDragging = true;
                    _readyCountResizing = false;
                    _readyCountStartScreen = mouse;
                    _readyCountStartOffset = _readyCountOffset;
                    _readyCountStartMouseWorld = ScreenToHudWorld(mouse, _readyCountObject);
                }
                else if (personalRect.Contains(mouse))
                {
                    _editTarget = ReadyHudEditTarget.PersonalReady;
                    _readyCountDragging = true;
                    _readyCountResizing = false;
                    _readyCountStartScreen = mouse;
                    _readyCountStartOffset = _personalReadyOffset;
                    _readyCountStartMouseWorld = ScreenToHudWorld(mouse, _personalReadyObject);
                }
                else
                {
                    _editTarget = ReadyHudEditTarget.None;
                    _readyCountDragging = false;
                    _readyCountResizing = false;
                }
            }

            GameObject editObject = GetEditTargetObject();
            if (_readyCountDragging && editObject != null && Input.GetMouseButton(0))
            {
                Vector3 mouseWorld = ScreenToHudWorld(Input.mousePosition, editObject);
                Vector3 delta = mouseWorld - _readyCountStartMouseWorld;
                Vector2 offset = new Vector2(
                    _readyCountStartOffset.x + delta.x,
                    _readyCountStartOffset.y + delta.y);

                if (_editTarget == ReadyHudEditTarget.PersonalReady)
                {
                    _personalReadyOffset = offset;
                }
                else if (_editTarget == ReadyHudEditTarget.ReadyCount)
                {
                    _readyCountOffset = offset;
                }

                LayoutHudObjects();
            }
            else if (_readyCountResizing && Input.GetMouseButton(0))
            {
                Vector2 delta = (Vector2)Input.mousePosition - _readyCountStartScreen;
                float directionalDelta = _editTarget == ReadyHudEditTarget.PersonalReady
                    ? -delta.x - delta.y
                    : delta.x - delta.y;
                float scaleDelta = directionalDelta / 180f;
                float scale = Mathf.Clamp(
                    _readyCountStartScale + scaleDelta,
                    ReadyHudMinScale,
                    ReadyHudMaxScale);

                if (_editTarget == ReadyHudEditTarget.PersonalReady)
                {
                    _personalReadyScale = scale;
                }
                else if (_editTarget == ReadyHudEditTarget.ReadyCount)
                {
                    _readyCountScale = scale;
                }

                LayoutHudObjects();
            }

            if (Input.GetMouseButtonUp(0))
            {
                _readyCountDragging = false;
                _readyCountResizing = false;
                _editTarget = ReadyHudEditTarget.None;
            }
        }

        private GameObject GetEditTargetObject()
        {
            return _editTarget == ReadyHudEditTarget.PersonalReady
                ? _personalReadyObject
                : _editTarget == ReadyHudEditTarget.ReadyCount
                    ? _readyCountObject
                    : null;
        }

        private Vector3 ScreenToHudWorld(Vector2 screenPosition, GameObject target)
        {
            if (target == null || _hudCamera == null)
            {
                return Vector3.zero;
            }

            float depth = target.transform.position.z - _hudCamera.transform.position.z;
            return _hudCamera.ScreenToWorldPoint(
                new Vector3(screenPosition.x, screenPosition.y, depth));
        }

        private Rect GetRendererScreenRect(Renderer renderer)
        {
            if (renderer == null || _hudCamera == null)
            {
                return new Rect();
            }

            Bounds bounds = renderer.bounds;
            Vector3 minimum = _hudCamera.WorldToScreenPoint(bounds.min);
            Vector3 maximum = _hudCamera.WorldToScreenPoint(bounds.max);
            if (minimum.z <= 0f || maximum.z <= 0f)
            {
                return new Rect();
            }

            return Rect.MinMaxRect(
                Mathf.Min(minimum.x, maximum.x),
                Mathf.Min(minimum.y, maximum.y),
                Mathf.Max(minimum.x, maximum.x),
                Mathf.Max(minimum.y, maximum.y));
        }

        private static Rect GetResizeHandle(Rect rect, bool leftAligned)
        {
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return new Rect();
            }

            float x = leftAligned
                ? rect.xMin - ResizeHandlePixels
                : rect.xMax - ResizeHandlePixels;

            return new Rect(
                x,
                rect.yMin - ResizeHandlePixels,
                ResizeHandlePixels * 2f,
                ResizeHandlePixels * 2f);
        }

        private void OnGUI()
        {
            if (!_readyCountEditing)
            {
                return;
            }

            ForceEditCursorVisible();
            Color previousColor = GUI.color;
            GUI.color = new Color(0.3f, 0.85f, 1f, 0.95f);
            DrawEditableTarget(_personalReadyRenderer, "READY STATUS", true);
            DrawEditableTarget(_readyCountRenderer, "READY COUNT", false);
            GUI.color = Color.white;
            GUI.Label(
                new Rect(16f, 16f, 500f, 24f),
                RoundsLocalization.Translate(
                    "READY HUD  •  DRAG AN ITEM TO MOVE  •  DRAG ITS CORNER TO RESIZE"));
            GUI.color = previousColor;
        }

        private void DrawEditableTarget(Renderer renderer, string label, bool leftAlignedHandle)
        {
            Rect screenRect = GetRendererScreenRect(renderer);
            if (screenRect.width <= 0f || screenRect.height <= 0f)
            {
                return;
            }

            Rect guiRect = new Rect(
                screenRect.x,
                Screen.height - screenRect.yMax,
                screenRect.width,
                screenRect.height);

            DrawOutline(guiRect, 2f);
            float handleX = leftAlignedHandle
                ? guiRect.xMin - ResizeHandlePixels * 0.5f
                : guiRect.xMax - ResizeHandlePixels * 0.5f;

            Rect handle = new Rect(
                handleX,
                guiRect.yMax - ResizeHandlePixels * 0.5f,
                ResizeHandlePixels,
                ResizeHandlePixels);
            GUI.DrawTexture(handle, Texture2D.whiteTexture);
            GUI.Label(
                new Rect(guiRect.x, guiRect.y - 22f, Mathf.Max(100f, guiRect.width), 20f),
                RoundsLocalization.Translate(label));
        }

        private static void DrawOutline(Rect rect, float thickness)
        {
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), Texture2D.whiteTexture);
        }

        private bool CanToggleReady()
        {
            GameManager gameManager = GameManager.instance;
            return _network != null &&
                   gameManager != null &&
                   gameManager.gameState == GameState.PLAYING &&
                   !gameManager.isPaused &&
                   BenchPolicyController.IsConnected &&
                   HeroController.instance != null &&
                   ReadyBenchStateReporter.CanToggleReady &&
                   !RoundsSettingsUI.IsVisible;
        }

        private void UpdateHud()
        {
            if (_state != null)
            {
                if (_personalReadyRenderer != null)
                {
                    SpriteRenderer renderer = _personalReadyRenderer as SpriteRenderer;
                    if (renderer != null)
                    {
                        renderer.sprite = _state.LocalPlayerReady ? _readySprite : _notReadySprite;
                    }
                }

                if (_readyCountDisplay != null && _readyCountDisplay.textObject != null)
                {
                    int required = Mathf.Max(0, _state.EffectiveReadyRequiredCount);
                    int ready = Mathf.Max(0, _state.ReadyPlayerCount);
                    _readyCountDisplay.textObject.text = ready + "/" + required;
                    _readyCountDisplay.textObject.fontSize = GetGeoAmountFontSize();
                }

            }

            UpdateHudVisibility();
            LayoutHudObjects();
        }

        private bool IsRoundActive()
        {
            return RoundClientManager.IsRoundActive || (_state != null && _state.RoundActive);
        }

        private void UpdateHudVisibility()
        {
            bool hasState = _state != null;
            bool roundActive = IsRoundActive();
            bool forceVisible = _readyCountEditing || (roundActive && _roundHudManualOverride);
            bool hiddenByRound = roundActive && !forceVisible;

            if (_personalReadyObject != null)
            {
                _personalReadyObject.SetActive(
                    hasState && (forceVisible || (!hiddenByRound && ClientPreferencesStore.ShowPersonalReadyStatus)));
            }

            if (_readyCountObject != null)
            {
                _readyCountObject.SetActive(
                    hasState && (forceVisible || (!hiddenByRound && ClientPreferencesStore.ShowReadyCount)));
            }

        }

        private void LayoutHudObjects()
        {
            if (_geoPrefab == null || _hudCanvasObject == null)
            {
                return;
            }

            const float besideGeoOffsetX = 2.2f;
            const float besideGeoOffsetY = 11.3f;
            const float itemSpacing = 0.08f;
            float centerY = _geoOrigin.y + besideGeoOffsetY;
            float firstCenterX = _geoOrigin.x + besideGeoOffsetX;
            float nextLeftX = firstCenterX - _geoIconWorldWidth * 0.5f;

            if (_personalReadyObject != null && _personalReadyObject.activeSelf)
            {
                _personalReadyObject.transform.localScale =
                    _personalReadyBaseScale * _personalReadyScale;
                PlaceRendererCenter(
                    _personalReadyObject,
                    _personalReadyRenderer,
                    firstCenterX,
                    centerY);
                _personalReadyBasePosition = _personalReadyObject.transform.position;
                _personalReadyObject.transform.position = _personalReadyBasePosition +
                    new Vector3(_personalReadyOffset.x, _personalReadyOffset.y, 0f);
                nextLeftX = _personalReadyRenderer != null
                    ? _personalReadyRenderer.bounds.max.x + itemSpacing
                    : firstCenterX + _geoIconWorldWidth * 0.5f + itemSpacing;
            }

            if (_readyCountObject != null)
            {
                _readyCountObject.transform.localScale = _readyCountBaseScale * _readyCountScale;
                PlaceRendererLeftEdge(
                    _readyCountObject,
                    _readyCountRenderer,
                    nextLeftX,
                    centerY);

                _readyCountBasePosition = _readyCountObject.transform.position;
                _readyCountObject.transform.position = _readyCountBasePosition +
                    new Vector3(_readyCountOffset.x, _readyCountOffset.y, 0f);
            }

        }

        private static void PlaceRendererCenter(
            GameObject target,
            Renderer renderer,
            float centerX,
            float centerY)
        {
            if (target == null || renderer == null)
            {
                return;
            }

            Bounds bounds = renderer.bounds;
            Vector3 position = target.transform.position;
            position.x += centerX - bounds.center.x;
            position.y += centerY - bounds.center.y;
            target.transform.position = position;
        }

        private static void PlaceRendererLeftEdge(
            GameObject target,
            Renderer renderer,
            float leftEdge,
            float centerY)
        {
            if (target == null || renderer == null)
            {
                return;
            }

            Bounds bounds = renderer.bounds;
            Vector3 position = target.transform.position;
            position.x += leftEdge - bounds.min.x;
            position.y += centerY - bounds.center.y;
            target.transform.position = position;
        }

        private void RemoveHudObjects()
        {
            if (_readyCountEditing)
            {
                ExitReadyCounterEdit();
            }

            DestroyHudObject(ref _personalReadyObject);
            DestroyHudObject(ref _readyCountObject);
            _readyCountDisplay = null;
            _personalReadyRenderer = null;
            _readyCountRenderer = null;
            _geoPrefab = null;
            _hudCanvasObject = null;
            _hudCamera = null;
            _personalReadyBaseScale = Vector3.one;
            _personalReadyBasePosition = Vector3.zero;
            _readyCountBaseScale = Vector3.one;
            _readyCountBasePosition = Vector3.zero;
        }

        private static void DestroyHudObject(ref GameObject gameObject)
        {
            if (gameObject != null)
            {
                Object.Destroy(gameObject);
                gameObject = null;
            }
        }

        private static Transform FindChildRecursive(Transform parent, string name)
        {
            if (parent == null)
            {
                return null;
            }

            if (string.Equals(parent.name, name, StringComparison.Ordinal))
            {
                return parent;
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform result = FindChildRecursive(parent.GetChild(i), name);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private void OnDestroy()
        {
            if (_network != null)
            {
                _network.RoundSettingsReceived -= OnRoundSettingsReceived;
            }

            if (_hookInstalled)
            {
                On.HeroController.Awake -= OnHeroAwake;
                ModHooks.SceneChanged -= OnSceneChanged;
                _hookInstalled = false;
            }

            if (_readyCountEditing)
            {
                ExitReadyCounterEdit();
            }
            RemoveHudObjects();
            DestroySprite(ref _readySprite);
            DestroySprite(ref _notReadySprite);
        }

        private static void DestroySprite(ref Sprite sprite)
        {
            if (sprite == null)
            {
                return;
            }

            Texture2D texture = sprite.texture;
            Object.Destroy(sprite);
            if (texture != null)
            {
                Object.Destroy(texture);
            }
            sprite = null;
        }
    }
}
