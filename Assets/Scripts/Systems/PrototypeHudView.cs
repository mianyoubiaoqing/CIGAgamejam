using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CIGAgamejam
{
    public sealed class PrototypeHudView : MonoBehaviour
    {
        [SerializeField] private GamePhaseSystem _gamePhaseSystem;
        [SerializeField] private CampaignProgressSystem _campaignProgressSystem;
        [SerializeField] private EconomySystem _economySystem;
        [SerializeField] private ToolInventorySystem _inventorySystem;
        [SerializeField] private NightTurnSystem _nightTurnSystem;
        [SerializeField] private PrototypeInputController _inputController;
        [SerializeField] private bool _allowRuntimeHudGeneration;
        [SerializeField] private bool _preserveSceneHudLayout = true;
        [SerializeField] private bool _preserveSceneHudVisuals = true;
        [SerializeField] private Font _labelFont;
        [Header("HUD Art")]
        [SerializeField] private Sprite _buttonDaylightSprite;
        [SerializeField] private Sprite _buttonNightSprite;
        [SerializeField] private Sprite _buttonPropSprite;
        [SerializeField] private Sprite _phaseBarSprite;
        [SerializeField] private Sprite _phasePointerSprite;
        [SerializeField] private Sprite _logPanelSprite;
        [SerializeField] private Sprite _guideSprite;
        [SerializeField, Min(0f)] private float _initialGuideLockSeconds = 3f;
        [Header("Day Night Arc")]
        [SerializeField] private RectTransform _nightPointerEndpoint;
        [SerializeField] private RectTransform _midPointerEndpoint;
        [SerializeField] private RectTransform _dayPointerEndpoint;
        [SerializeField, Min(0.01f)] private float _pointerMoveDuration = 1f;
        [SerializeField] private Vector2 _dayNightTrackSize = new(150f, 150f);
        [SerializeField] private Vector2 _dayNightPointerSize = new(34f, 56f);

        private readonly Dictionary<ToolConfig, Text> _toolCountTexts = new();
        private readonly Dictionary<ToolConfig, Button> _toolButtons = new();
        private readonly Dictionary<ToolConfig, Image> _toolIcons = new();
        private readonly HashSet<Transform> _tooltipBoundButtons = new();
        private Font _font;
        private Text _confidenceText;
        private Text _flowText;
        private Text _phaseText;
        private Text _turnText;
        private Text _logText;
        private RectTransform _topBarRoot;
        private RectTransform _bottomBarRoot;
        private RectTransform _logPanelRoot;
        private RectTransform _dayNightTrack;
        private RectTransform _dayNightNeedle;
        private RectTransform _toolMenuRoot;
        private RectTransform _actionButtonRoot;
        private RectTransform _guideEntryRoot;
        private GameObject _guideOverlay;
        private Button _guideOverlayButton;
        private RectTransform _tooltipRoot;
        private Text _tooltipText;
        private Canvas _canvas;
        private Coroutine _dayNightPointerAnimation;
        private Coroutine _guideDismissRoutine;
        private float _currentDayNightProgress;
        private bool _loggedMissingDayNightEndpoints;
        private Button _startDayButton;
        private Button _nextNightButton;
        private GameObject _startScreen;
        private GameObject _resultPanel;
        private Text _resultText;
        private bool _hasGameStarted;
        private bool _initialGuideShown;
        private bool _guideCanDismiss;
        private int _currentDay = 1;
        private int _maxDays = 1;
        private int _inStoreCount;
        private int _todayTotal;
        private float _flowTrend;
        private float _confidence = 100f;
        private int _normalCustomerCount;
        private int _angryCustomerCount;
        private int _scaredCustomerCount;

        private void Awake()
        {
            _font = _labelFont != null
                ? _labelFont
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Canvas canvas = CreateCanvas();
            if (canvas == null)
                return;

            _canvas = canvas;
            BuildHud(canvas);
            ConfigureDayNightTrack();
            BuildTooltip(canvas);
            BuildResultPanel(canvas);
            BuildStartScreen(canvas);
            BuildGuide(canvas);
            _hasGameStarted = _gamePhaseSystem != null
                && (_gamePhaseSystem.CurrentPhase != GamePhase.None || _gamePhaseSystem.BeginOnStart);
            SetHudVisible(_hasGameStarted);
            RefreshAll();
        }

        private void Start()
        {
            EnsureToolButtonsForInventory();
            RefreshAll();
            TryShowInitialGuide();
        }

        private void OnEnable()
        {
            EventBus<OnDayStarted>.Subscribe(HandleDayStarted);
            EventBus<OnGamePhaseChanged>.Subscribe(HandleGamePhaseChanged);
            EventBus<OnNightTurnStarted>.Subscribe(HandleNightTurnStarted);
            EventBus<OnNightTurnAdvanced>.Subscribe(HandleNightTurnAdvanced);
            EventBus<OnRevenueChanged>.Subscribe(HandleRevenueChanged);
            EventBus<OnCustomerFlowChanged>.Subscribe(HandleCustomerFlowChanged);
            EventBus<OnToolInventoryChanged>.Subscribe(HandleToolInventoryChanged);
            EventBus<OnToolSelected>.Subscribe(HandleToolSelected);
            EventBus<OnPrototypeLogMessage>.Subscribe(HandlePrototypeLogMessage);
            EventBus<OnCustomerFinalized>.Subscribe(HandleCustomerFinalized);
            EventBus<OnGameEnded>.Subscribe(HandleGameEnded);
        }

        private void OnDestroy()
        {
            StopGuideDismissRoutine();
            EventBus<OnDayStarted>.Unsubscribe(HandleDayStarted);
            EventBus<OnGamePhaseChanged>.Unsubscribe(HandleGamePhaseChanged);
            EventBus<OnNightTurnStarted>.Unsubscribe(HandleNightTurnStarted);
            EventBus<OnNightTurnAdvanced>.Unsubscribe(HandleNightTurnAdvanced);
            EventBus<OnRevenueChanged>.Unsubscribe(HandleRevenueChanged);
            EventBus<OnCustomerFlowChanged>.Unsubscribe(HandleCustomerFlowChanged);
            EventBus<OnToolInventoryChanged>.Unsubscribe(HandleToolInventoryChanged);
            EventBus<OnToolSelected>.Unsubscribe(HandleToolSelected);
            EventBus<OnPrototypeLogMessage>.Unsubscribe(HandlePrototypeLogMessage);
            EventBus<OnCustomerFinalized>.Unsubscribe(HandleCustomerFinalized);
            EventBus<OnGameEnded>.Unsubscribe(HandleGameEnded);
        }

        private void BuildHud(Canvas canvas)
        {
            if (TryBindExistingHud(canvas))
                return;

            if (!_allowRuntimeHudGeneration)
            {
                Debug.LogError("PrototypeHudView missing/incomplete scene HUD. Please build the Prototype HUD hierarchy in Game.unity or enable runtime generation for debugging.");
                return;
            }

            RectTransform topBar = CreatePanel(canvas.transform, "Top Bar", AnchorTopStretch(82f), new Color(0.07f, 0.08f, 0.09f, 0.94f));
            RectTransform bottomBar = CreatePanel(canvas.transform, "Bottom Bar", AnchorBottomStretch(112f), new Color(0.08f, 0.08f, 0.08f, 0.94f));
            RectTransform logPanel = CreatePanel(canvas.transform, "Log Panel", AnchorRightMiddle(300f, 138f), new Color(0.05f, 0.05f, 0.05f, 0.78f));
            _topBarRoot = topBar;
            _bottomBarRoot = bottomBar;
            _logPanelRoot = logPanel;
            ApplySprite(logPanel.GetComponent<Image>(), _logPanelSprite, true);

            HorizontalLayoutGroup topLayout = topBar.gameObject.AddComponent<HorizontalLayoutGroup>();
            topLayout.padding = new RectOffset(12, 12, 10, 10);
            topLayout.spacing = 10f;
            topLayout.childAlignment = TextAnchor.MiddleLeft;
            topLayout.childControlHeight = true;
            topLayout.childControlWidth = true;
            topLayout.childForceExpandHeight = true;
            topLayout.childForceExpandWidth = false;

            _confidenceText = CreateLayoutText(topBar, "Favorability", "Favorability 100%", 16, TextAnchor.MiddleLeft, 140f);
            _flowText = CreateLayoutText(topBar, "Flow", "Traffic In Shop 0 Today 0", 16, TextAnchor.MiddleLeft, 300f);
            _phaseText = CreateLayoutText(topBar, "Phase", "Day 1/1 Night", 16, TextAnchor.MiddleLeft, 140f);
            _turnText = CreateLayoutText(topBar, "Turn", "Turn 1", 16, TextAnchor.MiddleLeft, 70f);
            ApplyStatusTextStyle(_confidenceText);
            ApplyStatusTextStyle(_flowText);
            ApplyStatusTextStyle(_phaseText);
            ApplyStatusTextStyle(_turnText);
            AddFlexibleSpace(topBar);
            BuildDayNightTrack(topBar);

            _logText = CreateText(logPanel, "Log Text", "Event Log", 15, TextAnchor.UpperLeft, Vector2.zero, new Vector2(272f, 116f));

            _toolMenuRoot = CreateContainer(bottomBar, "Tool Button Row", AnchorBottomToolRow());
            HorizontalLayoutGroup toolLayout = _toolMenuRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
            toolLayout.padding = new RectOffset(0, 0, 0, 0);
            toolLayout.spacing = 10f;
            toolLayout.childAlignment = TextAnchor.MiddleLeft;
            toolLayout.childControlHeight = false;
            toolLayout.childControlWidth = false;
            toolLayout.childForceExpandHeight = false;
            toolLayout.childForceExpandWidth = false;

            _actionButtonRoot = CreateContainer(bottomBar, "Action Button Row", AnchorBottomActionRow());
            HorizontalLayoutGroup actionLayout = _actionButtonRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
            actionLayout.padding = new RectOffset(0, 0, 0, 0);
            actionLayout.spacing = 10f;
            actionLayout.childAlignment = TextAnchor.MiddleRight;
            actionLayout.childControlHeight = false;
            actionLayout.childControlWidth = false;
            actionLayout.childForceExpandHeight = false;
            actionLayout.childForceExpandWidth = false;

            BuildToolButtons(_toolMenuRoot);
            BuildActionButtons(_actionButtonRoot);
            BuildGuideEntryButton(canvas);
            ConfigureHudRaycasts(canvas.transform);
        }

        private Canvas CreateCanvas()
        {
            Transform existing = transform.Find("Prototype HUD");
            if (existing != null && existing.TryGetComponent(out Canvas existingCanvas))
                return existingCanvas;

            foreach (Canvas sceneCanvas in FindObjectsOfType<Canvas>(true))
                if (sceneCanvas.gameObject.scene == gameObject.scene && sceneCanvas.name == "Prototype HUD")
                    return sceneCanvas;

            if (!_allowRuntimeHudGeneration)
            {
                Debug.LogError("PrototypeHudView missing Prototype HUD canvas. Please create and bind it in the scene hierarchy, or enable runtime generation for debugging.");
                return null;
            }

            var canvasObject = new GameObject("Prototype HUD");
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            canvas.sortingOrder = 5000;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            canvasObject.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private bool TryBindExistingHud(Canvas canvas)
        {
            if (canvas == null || canvas.transform.Find("Top Bar") == null)
                return false;

            _topBarRoot = FindRect(canvas.transform, "Top Bar");
            _bottomBarRoot = FindRect(canvas.transform, "Bottom Bar");
            _logPanelRoot = FindRect(canvas.transform, "Log Panel");
            _confidenceText = FindText(canvas.transform, "Top Bar/Favorability");
            _flowText = FindText(canvas.transform, "Top Bar/Flow");
            _phaseText = FindText(canvas.transform, "Top Bar/Phase");
            _turnText = FindText(canvas.transform, "Top Bar/Turn");
            _logText = FindText(canvas.transform, "Log Panel/Log Text");
            _dayNightTrack = FindRect(canvas.transform, "Top Bar/Day Night Root/Day Night Track");
            _dayNightNeedle = FindRect(canvas.transform, "Top Bar/Day Night Root/Day Night Track/Needle");
            _nightPointerEndpoint = FindRect(canvas.transform, "Top Bar/Day Night Root/Day Night Track/Night Endpoint");
            _midPointerEndpoint = FindRect(canvas.transform, "Top Bar/Day Night Root/Day Night Track/Mid Endpoint");
            _dayPointerEndpoint = FindRect(canvas.transform, "Top Bar/Day Night Root/Day Night Track/Day Endpoint");
            _toolMenuRoot = FindRect(canvas.transform, "Bottom Bar/Tool Button Row");
            _actionButtonRoot = FindRect(canvas.transform, "Bottom Bar/Action Button Row");
            _startDayButton = FindButton(_actionButtonRoot, "Next Day Button");
            _nextNightButton = FindButton(_actionButtonRoot, "Next Night Button");
            BindActionButton(_startDayButton, () => _gamePhaseSystem?.EndNightAndStartDay());
            BindActionButton(_nextNightButton, () => _gamePhaseSystem?.StartNextNightOrFail());
            BuildGuideEntryButton(canvas);

            return _confidenceText != null
                && _flowText != null
                && _phaseText != null
                && _turnText != null
                && _logText != null
                && _topBarRoot != null
                && _bottomBarRoot != null
                && _logPanelRoot != null
                && _dayNightTrack != null
                && _dayNightNeedle != null
                && _nightPointerEndpoint != null
                && _midPointerEndpoint != null
                && _dayPointerEndpoint != null
                && _toolMenuRoot != null
                && _actionButtonRoot != null;
        }

        private void BuildDayNightTrack(RectTransform topBar)
        {
            RectTransform trackRoot = CreateContainer(topBar, "Day Night Root", new RectSpec(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(300f, 56f), new Vector2(0f, 0.5f)));
            LayoutElement rootLayout = trackRoot.gameObject.AddComponent<LayoutElement>();
            rootLayout.preferredWidth = 300f;
            rootLayout.minWidth = 230f;
            rootLayout.preferredHeight = 56f;

            _dayNightTrack = CreatePanel(trackRoot, "Day Night Track", new RectSpec(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -5f), _dayNightTrackSize), new Color(0.2f, 0.2f, 0.22f, 1f));
            ApplySprite(_dayNightTrack.GetComponent<Image>(), _phaseBarSprite, true);
            CreateText(trackRoot, "Night Label", "Night", 14, TextAnchor.MiddleCenter, new Vector2(-118f, 16f), new Vector2(48f, 20f));
            CreateText(trackRoot, "Day Label", "Day", 14, TextAnchor.MiddleCenter, new Vector2(118f, 16f), new Vector2(48f, 20f));
            _dayNightNeedle = CreatePanel(_dayNightTrack, "Needle", new RectSpec(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, _dayNightPointerSize), new Color(0.95f, 0.78f, 0.2f, 1f));
            ApplySprite(_dayNightNeedle.GetComponent<Image>(), _phasePointerSprite, true);
            _nightPointerEndpoint = CreatePointerEndpoint(_dayNightTrack, "Night Endpoint", new Vector2(-132.32f, -6.97f), 295f);
            _midPointerEndpoint = CreatePointerEndpoint(_dayNightTrack, "Mid Endpoint", new Vector2(0f, -44f), 180f);
            _dayPointerEndpoint = CreatePointerEndpoint(_dayNightTrack, "Day Endpoint", new Vector2(132.32f, -6.97f), 65f);
        }

        private void ConfigureDayNightTrack()
        {
            if (_dayNightTrack == null || _dayNightNeedle == null)
                return;

            if (!_preserveSceneHudLayout)
            {
                _dayNightTrack.anchorMin = new Vector2(0.5f, 0.5f);
                _dayNightTrack.anchorMax = new Vector2(0.5f, 0.5f);
                _dayNightTrack.pivot = new Vector2(0.5f, 0.5f);
                _dayNightTrack.sizeDelta = _dayNightTrackSize;

                _dayNightNeedle.anchorMin = new Vector2(0.5f, 0.5f);
                _dayNightNeedle.anchorMax = new Vector2(0.5f, 0.5f);
                _dayNightNeedle.pivot = new Vector2(0.5f, 0.5f);
                _dayNightNeedle.sizeDelta = _dayNightPointerSize;
                _dayNightNeedle.SetAsLastSibling();
            }

            Image trackImage = _dayNightTrack.GetComponent<Image>();
            if (trackImage != null)
            {
                trackImage.raycastTarget = false;
                if (!_preserveSceneHudVisuals)
                    trackImage.preserveAspect = true;
            }

            Image pointerImage = _dayNightNeedle.GetComponent<Image>();
            if (pointerImage != null)
            {
                pointerImage.raycastTarget = false;
                if (!_preserveSceneHudVisuals)
                    pointerImage.preserveAspect = true;
            }
        }

        private void BuildToolButtons(RectTransform bottomBar)
        {
            _toolMenuRoot = bottomBar;
            EnsureToolButtonsForInventory();
        }

        private void CreateToolButton(RectTransform parent, ToolConfig tool, int index)
        {
            if (parent == null || tool == null) return;

            RectTransform buttonTransform = CreateButton(
                parent,
                $"Tool Button {tool.Id}",
                $"{tool.DisplayName}\nx{_inventorySystem.GetCount(tool)}",
                Vector2.zero,
                new Vector2(104f, 70f),
                () => _inputController?.SelectTool(tool),
                _buttonPropSprite);

            AddFixedLayout(buttonTransform, 104f, 70f);
            Text label = buttonTransform.GetComponentInChildren<Text>();
            if (label != null)
            {
                RectTransform labelRect = label.rectTransform;
                labelRect.anchorMin = new Vector2(0f, 0f);
                labelRect.anchorMax = new Vector2(1f, 0.42f);
                labelRect.offsetMin = new Vector2(5f, 4f);
                labelRect.offsetMax = new Vector2(-5f, -2f);
                label.fontSize = 13;
                ApplyButtonTextStyle(label);
            }

            if (tool.Icon != null)
                CreateIconImage(buttonTransform, "Icon", tool.Icon, new Vector2(0f, 11f), new Vector2(42f, 42f));

            _toolCountTexts[tool] = label;
            _toolButtons[tool] = FindButton(buttonTransform, string.Empty);
            BindTooltip(buttonTransform, tool);
        }

        private static RectTransform CreatePointerEndpoint(RectTransform parent, string name, Vector2 position, float rotationZ)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = rect.anchorMin;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(12f, 12f);
            rect.localRotation = Quaternion.Euler(0f, 0f, rotationZ);
            return rect;
        }

        private void BuildGuideEntryButton(Canvas canvas)
        {
            if (canvas == null || _logPanelRoot == null || _guideEntryRoot != null)
                return;

            Transform existing = _logPanelRoot.Find("Guide Entry Button");
            if (existing != null)
            {
                _guideEntryRoot = existing as RectTransform;
                Button existingButton = FindButton(existing, string.Empty);
                BindActionButton(existingButton, () => ShowGuide(0f));
                return;
            }

            _guideEntryRoot = CreatePanel(
                _logPanelRoot,
                "Guide Entry Button",
                new RectSpec(
                    new Vector2(0.5f, 0f),
                    new Vector2(0.5f, 0f),
                    new Vector2(0f, -10f),
                    new Vector2(58f, 58f),
                    new Vector2(0.5f, 1f)),
                new Color(1f, 1f, 1f, 1f));

            Image image = _guideEntryRoot.GetComponent<Image>();
            image.sprite = _guideSprite;
            image.preserveAspect = true;
            image.raycastTarget = true;

            Button button = _guideEntryRoot.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => ShowGuide(0f));
            _guideEntryRoot.SetAsLastSibling();
        }

        private void BuildGuide(Canvas canvas)
        {
            if (canvas == null || _guideOverlay != null)
                return;

            Transform existing = canvas.transform.Find("Guide Overlay");
            if (existing != null)
            {
                _guideOverlay = existing.gameObject;
                _guideOverlayButton = existing.GetComponent<Button>();
                if (_guideOverlayButton != null)
                    BindActionButton(_guideOverlayButton, TryDismissGuide);
                _guideOverlay.SetActive(false);
                return;
            }

            RectTransform overlay = CreatePanel(
                canvas.transform,
                "Guide Overlay",
                new RectSpec(Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero),
                new Color(0f, 0f, 0f, 0.72f));
            _guideOverlay = overlay.gameObject;

            Image blocker = overlay.GetComponent<Image>();
            blocker.raycastTarget = true;
            _guideOverlayButton = overlay.gameObject.AddComponent<Button>();
            _guideOverlayButton.targetGraphic = blocker;
            _guideOverlayButton.onClick.AddListener(TryDismissGuide);

            RectTransform guideImage = CreatePanel(
                overlay,
                "Guide Image",
                new RectSpec(
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    Vector2.zero,
                    new Vector2(960f, 600f),
                    new Vector2(0.5f, 0.5f)),
                Color.white);
            Image image = guideImage.GetComponent<Image>();
            image.sprite = _guideSprite;
            image.preserveAspect = true;
            image.raycastTarget = false;

            _guideOverlay.SetActive(false);
        }

        private void BuildTooltip(Canvas canvas)
        {
            if (canvas == null || _tooltipRoot != null) return;

            Transform existing = canvas.transform.Find("Tool Tooltip");
            if (existing != null)
            {
                _tooltipRoot = existing as RectTransform;
                _tooltipText = existing.GetComponentInChildren<Text>(true);
            }

            if (_tooltipRoot == null)
            {
                _tooltipRoot = CreatePanel(
                    canvas.transform,
                    "Tool Tooltip",
                    new RectSpec(
                        new Vector2(0.5f, 0.5f),
                        new Vector2(0.5f, 0.5f),
                        Vector2.zero,
                        new Vector2(240f, 100f),
                        new Vector2(0.5f, 0f)),
                    new Color(0.08f, 0.08f, 0.08f, 0.95f));

                Image background = _tooltipRoot.GetComponent<Image>();
                background.raycastTarget = false;

                Outline outline = _tooltipRoot.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0.98f, 0.92f, 0.78f, 0.8f);
                outline.effectDistance = new Vector2(1f, -1f);

                _tooltipText = CreateText(
                    _tooltipRoot,
                    "Tooltip Text",
                    string.Empty,
                    13,
                    TextAnchor.UpperLeft,
                    Vector2.zero,
                    Vector2.zero);
                RectTransform textRect = _tooltipText.rectTransform;
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.pivot = new Vector2(0.5f, 0.5f);
                textRect.offsetMin = new Vector2(10f, 8f);
                textRect.offsetMax = new Vector2(-10f, -8f);
                _tooltipText.supportRichText = true;
                _tooltipText.horizontalOverflow = HorizontalWrapMode.Wrap;
                _tooltipText.verticalOverflow = VerticalWrapMode.Overflow;
                _tooltipText.raycastTarget = false;
            }

            _tooltipRoot.gameObject.SetActive(false);
        }

        private void BindTooltip(RectTransform buttonTransform, ToolConfig tool)
        {
            if (buttonTransform == null || tool == null || !_tooltipBoundButtons.Add(buttonTransform))
                return;

            EventTrigger trigger = buttonTransform.GetComponent<EventTrigger>();
            if (trigger == null)
                trigger = buttonTransform.gameObject.AddComponent<EventTrigger>();

            trigger.triggers ??= new List<EventTrigger.Entry>();
            AddTooltipTrigger(trigger, EventTriggerType.PointerEnter, _ => ShowTooltip(tool, buttonTransform));
            AddTooltipTrigger(trigger, EventTriggerType.PointerExit, _ => HideTooltip());
        }

        private static void AddTooltipTrigger(
            EventTrigger trigger,
            EventTriggerType eventType,
            UnityEngine.Events.UnityAction<BaseEventData> callback)
        {
            var entry = new EventTrigger.Entry { eventID = eventType };
            entry.callback.AddListener(callback);
            trigger.triggers.Add(entry);
        }

        private void ShowTooltip(ToolConfig tool, RectTransform buttonTransform)
        {
            if (_tooltipRoot == null || _tooltipText == null || tool == null || buttonTransform == null)
                return;

            string description = string.IsNullOrWhiteSpace(tool.Description)
                ? "No description available."
                : tool.Description;
            _tooltipText.text =
                $"<size=15><b><color=#FAEBC7>{tool.DisplayName}</color></b></size>\n{description}";

            _tooltipRoot.gameObject.SetActive(true);
            _tooltipRoot.SetAsLastSibling();
            PositionTooltipAbove(buttonTransform);
        }

        private void PositionTooltipAbove(RectTransform buttonTransform)
        {
            RectTransform canvasRect = _canvas != null ? _canvas.transform as RectTransform : null;
            if (canvasRect == null) return;

            var corners = new Vector3[4];
            buttonTransform.GetWorldCorners(corners);
            Vector3 worldTopCenter = (corners[1] + corners[2]) * 0.5f;
            Camera eventCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : _canvas.worldCamera;
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(eventCamera, worldTopCenter);

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect,
                    screenPoint,
                    eventCamera,
                    out Vector2 localPoint))
                return;

            localPoint.y += 10f;
            Rect canvasBounds = canvasRect.rect;
            Vector2 tooltipSize = _tooltipRoot.rect.size;
            localPoint.x = Mathf.Clamp(
                localPoint.x,
                canvasBounds.xMin + tooltipSize.x * 0.5f,
                canvasBounds.xMax - tooltipSize.x * 0.5f);
            localPoint.y = Mathf.Clamp(
                localPoint.y,
                canvasBounds.yMin,
                canvasBounds.yMax - tooltipSize.y);
            _tooltipRoot.anchoredPosition = localPoint;
        }

        private void HideTooltip()
        {
            if (_tooltipRoot != null)
                _tooltipRoot.gameObject.SetActive(false);
        }

        private void TryShowInitialGuide()
        {
            if (_initialGuideShown || _guideOverlay == null || _gamePhaseSystem == null)
                return;

            if (_gamePhaseSystem.CurrentPhase == GamePhase.None)
                return;

            _initialGuideShown = true;
            ShowGuide(_initialGuideLockSeconds);
        }

        private void ShowGuide(float lockSeconds)
        {
            if (_guideOverlay == null)
                return;

            StopGuideDismissRoutine();
            _guideCanDismiss = lockSeconds <= 0f;
            _guideOverlay.SetActive(true);
            _guideOverlay.transform.SetAsLastSibling();

            if (!_guideCanDismiss)
                _guideDismissRoutine = StartCoroutine(UnlockGuideDismissAfter(lockSeconds));
        }

        private IEnumerator UnlockGuideDismissAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, seconds));
            _guideCanDismiss = true;
            _guideDismissRoutine = null;
        }

        private void TryDismissGuide()
        {
            if (!_guideCanDismiss || _guideOverlay == null)
                return;

            StopGuideDismissRoutine();
            _guideOverlay.SetActive(false);
        }

        private void StopGuideDismissRoutine()
        {
            if (_guideDismissRoutine == null)
                return;

            StopCoroutine(_guideDismissRoutine);
            _guideDismissRoutine = null;
        }

        private void BuildActionButtons(RectTransform bottomBar)
        {
            RectTransform nextDay = CreateButton(bottomBar, "Next Day Button", "Start Day", Vector2.zero, new Vector2(118f, 58f), () => _gamePhaseSystem?.EndNightAndStartDay(), _buttonDaylightSprite);
            RectTransform nextNight = CreateButton(bottomBar, "Next Night Button", "Next Night", Vector2.zero, new Vector2(100f, 58f), () => _gamePhaseSystem?.StartNextNightOrFail(), _buttonNightSprite);
            _startDayButton = FindButton(nextDay, string.Empty);
            _nextNightButton = FindButton(nextNight, string.Empty);
            AddFixedLayout(nextDay, 118f, 58f);
            AddFixedLayout(nextNight, 100f, 58f);
        }

        private void BuildResultPanel(Canvas canvas)
        {
            if (canvas == null || _resultPanel != null) return;

            if (TryBindResultPanel(canvas))
                return;

            if (!_allowRuntimeHudGeneration)
            {
                Debug.LogError("PrototypeHudView missing Game Result Panel in scene HUD.");
                return;
            }

            RectTransform panel = CreatePanel(
                canvas.transform,
                "Game Result Panel",
                new RectSpec(Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero),
                new Color(0.03f, 0.03f, 0.04f, 0.94f));
            _resultPanel = panel.gameObject;

            _resultText = CreateText(
                panel,
                "Result Text",
                string.Empty,
                24,
                TextAnchor.MiddleCenter,
                new Vector2(0f, 45f),
                new Vector2(620f, 330f));
            RectTransform textRect = _resultText.rectTransform;
            textRect.anchorMin = new Vector2(0.5f, 0.5f);
            textRect.anchorMax = textRect.anchorMin;
            textRect.pivot = new Vector2(0.5f, 0.5f);

            RectTransform restart = CreateButton(
                panel,
                "Restart Button",
                "Restart",
                new Vector2(0f, -175f),
                new Vector2(180f, 58f),
                ReloadCurrentScene);
            restart.anchorMin = new Vector2(0.5f, 0.5f);
            restart.anchorMax = restart.anchorMin;
            restart.pivot = new Vector2(0.5f, 0.5f);
            _resultPanel.SetActive(false);
            ConfigureHudRaycasts(canvas.transform);
        }

        private bool TryBindResultPanel(Canvas canvas)
        {
            Transform panel = canvas.transform.Find("Game Result Panel");
            if (panel == null) return false;

            _resultPanel = panel.gameObject;
            _resultText = FindText(panel, "Result Text");
            Button restartButton = FindButton(panel, "Restart Button");
            BindActionButton(restartButton, ReloadCurrentScene);
            _resultPanel.SetActive(false);
            return _resultText != null && restartButton != null;
        }

        private void BuildStartScreen(Canvas canvas)
        {
            if (canvas == null) return;

            Transform existing = canvas.transform.Find("Start Screen");
            if (existing != null)
            {
                _startScreen = existing.gameObject;
                Button existingButton = FindButton(existing, "Start Button");
                StartGameUI existingStartUI = _startScreen.GetComponent<StartGameUI>();
                if (existingStartUI != null)
                    existingStartUI.Configure(_gamePhaseSystem, existingButton);
                else
                    BindActionButton(existingButton, OnStartClicked);
                _startScreen.SetActive(_gamePhaseSystem == null
                    || (!_gamePhaseSystem.BeginOnStart && _gamePhaseSystem.CurrentPhase == GamePhase.None));
                return;
            }

            if (!_allowRuntimeHudGeneration)
            {
                if (_gamePhaseSystem != null && _gamePhaseSystem.BeginOnStart)
                    return;

                Debug.LogError("PrototypeHudView missing Start Screen in scene HUD.");
                return;
            }

            RectTransform screen = CreatePanel(
                canvas.transform,
                "Start Screen",
                new RectSpec(Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero),
                new Color(0.03f, 0.03f, 0.04f, 0.95f));
            _startScreen = screen.gameObject;

            Text title = CreateText(
                screen,
                "Title Text",
                "Swiper Yes Swiping",
                48,
                TextAnchor.MiddleCenter,
                new Vector2(0f, 120f),
                new Vector2(620f, 86f));
            title.fontStyle = FontStyle.Bold;
            ApplyStatusTextStyle(title);
            RectTransform titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0.5f, 0.5f);
            titleRect.anchorMax = titleRect.anchorMin;
            titleRect.pivot = new Vector2(0.5f, 0.5f);

            RectTransform startButton = CreateButton(
                screen,
                "Start Button",
                "Start Game",
                new Vector2(0f, 8f),
                new Vector2(220f, 64f),
                null,
                _buttonDaylightSprite);
            startButton.anchorMin = new Vector2(0.5f, 0.5f);
            startButton.anchorMax = startButton.anchorMin;
            startButton.pivot = new Vector2(0.5f, 0.5f);
            Button button = FindButton(startButton, string.Empty);
            button.onClick.RemoveAllListeners();

            Text subtitle = CreateText(
                screen,
                "Subtitle",
                "Set traps at night. Prank customers by day.",
                18,
                TextAnchor.MiddleCenter,
                new Vector2(0f, -96f),
                new Vector2(520f, 42f));
            ApplyStatusTextStyle(subtitle);
            RectTransform subtitleRect = subtitle.rectTransform;
            subtitleRect.anchorMin = new Vector2(0.5f, 0.5f);
            subtitleRect.anchorMax = subtitleRect.anchorMin;
            subtitleRect.pivot = new Vector2(0.5f, 0.5f);

            StartGameUI startUI = _startScreen.AddComponent<StartGameUI>();
            startUI.Configure(_gamePhaseSystem, button);
            _startScreen.transform.SetAsLastSibling();
        }

        private void OnStartClicked()
        {
            _gamePhaseSystem?.BeginGame();
            if (_startScreen != null)
                _startScreen.SetActive(false);
        }

        private RectTransform CreateButton(RectTransform parent, string name, string label, Vector2 anchoredPosition, Vector2 size, UnityEngine.Events.UnityAction action, Sprite backgroundSprite = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = rect.anchorMin;
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            RectTransform background = CreatePanel(
                rect,
                "Background",
                new RectSpec(Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero),
                new Color(0.18f, 0.2f, 0.23f, 1f));
            Image image = background.GetComponent<Image>();
            image.color = new Color(0.18f, 0.2f, 0.23f, 1f);
            ApplySprite(image, backgroundSprite, true);
            Button button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            if (action != null)
                button.onClick.AddListener(action);
            Text text = CreateStretchText(rect, "Label", label, 15, TextAnchor.MiddleCenter, new Vector2(6f, 4f));
            ApplyButtonTextStyle(text);
            return rect;
        }

        private static Image CreateIconImage(RectTransform parent, string name, Sprite sprite, Vector2 anchoredPosition, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = rect.anchorMin;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            Image image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        private RectTransform CreateContainer(Transform parent, string name, RectSpec spec)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = spec.AnchorMin;
            rect.anchorMax = spec.AnchorMax;
            rect.pivot = spec.Pivot;
            rect.anchoredPosition = spec.AnchoredPosition;
            rect.sizeDelta = spec.SizeDelta;
            return rect;
        }

        private RectTransform CreatePanel(Transform parent, string name, RectSpec spec, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = spec.AnchorMin;
            rect.anchorMax = spec.AnchorMax;
            rect.pivot = spec.Pivot;
            rect.anchoredPosition = spec.AnchoredPosition;
            rect.sizeDelta = spec.SizeDelta;
            Image image = go.AddComponent<Image>();
            image.color = color;
            return rect;
        }

private Text CreateLayoutText(RectTransform parent, string name, string value, int fontSize, TextAnchor anchor, float preferredWidth)
        {
            Text text = CreateStretchText(parent, name, value, fontSize, anchor, Vector2.zero);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;

            LayoutElement layoutElement = text.gameObject.AddComponent<LayoutElement>();
            layoutElement.minWidth = preferredWidth;
            layoutElement.preferredWidth = preferredWidth;
            layoutElement.flexibleWidth = 0f;
            layoutElement.preferredHeight = 48f;
            return text;
        }

        private Text CreateStretchText(RectTransform parent, string name, string value, int fontSize, TextAnchor anchor, Vector2 padding)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = padding;
            rect.offsetMax = -padding;
            Text text = go.AddComponent<Text>();
            text.text = value;
            text.font = _font;
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private Text CreateText(RectTransform parent, string name, string value, int fontSize, TextAnchor anchor, Vector2 anchoredPosition, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            Text text = go.AddComponent<Text>();
            text.text = value;
            text.font = _font;
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static void ApplyButtonTextStyle(Text text)
        {
            if (text == null) return;

            text.color = new Color(0.98f, 0.92f, 0.78f, 1f);
            Outline outline = text.GetComponent<Outline>() ?? text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.20f, 0.11f, 0.07f, 0.95f);
            outline.effectDistance = new Vector2(1.4f, -1.4f);
            Shadow shadow = text.GetComponent<Shadow>() ?? text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
            shadow.effectDistance = new Vector2(1.5f, -2f);
        }

        private static void ApplyStatusTextStyle(Text text)
        {
            if (text == null) return;

            text.color = new Color(0.96f, 0.90f, 0.78f, 1f);
            Outline outline = text.GetComponent<Outline>() ?? text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.10f, 0.06f, 0.04f, 0.95f);
            outline.effectDistance = new Vector2(1.2f, -1.2f);
            Shadow shadow = text.GetComponent<Shadow>() ?? text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
            shadow.effectDistance = new Vector2(1.2f, -1.6f);
        }

        private static void ApplySprite(Image image, Sprite sprite, bool preserveAspect)
        {
            if (image == null || sprite == null)
                return;

            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = preserveAspect;
            image.color = Color.white;
        }

        private static void AddFlexibleSpace(RectTransform parent)
        {
            var go = new GameObject("Flexible Space");
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            LayoutElement layoutElement = go.AddComponent<LayoutElement>();
            layoutElement.flexibleWidth = 1f;
        }

        private static void AddFixedLayout(RectTransform rect, float width, float height)
        {
            LayoutElement layoutElement = rect.gameObject.AddComponent<LayoutElement>();
            layoutElement.minWidth = width;
            layoutElement.preferredWidth = width;
            layoutElement.minHeight = height;
            layoutElement.preferredHeight = height;
        }

        private void RefreshAll(bool refreshDayNightPointer = true)
        {
            if (_confidenceText == null || _flowText == null || _phaseText == null || _turnText == null)
                return;

            _confidenceText.text = $"Favorability {_confidence:0}%";
            _flowText.text = $"Traffic In Shop {_inStoreCount} Today {_todayTotal}";
            GamePhase currentPhase = _gamePhaseSystem != null
                ? _gamePhaseSystem.CurrentPhase
                : GamePhase.NightPlanning;
            string phase = ResolvePhaseLabel(currentPhase);
            _phaseText.text = $"Day {_currentDay}/{_maxDays} {phase}";
            _turnText.text = $"Turn {(_nightTurnSystem != null ? _nightTurnSystem.CurrentTurn : 1)}";
            if (refreshDayNightPointer && _dayNightPointerAnimation == null)
                SetDayNightPointerImmediate(currentPhase);
            RefreshButtonStates();
        }

        private void SetDayNightPointerImmediate(GamePhase phase)
        {
            if (_dayNightNeedle == null)
                return;

            StopDayNightPointerAnimation();
            _currentDayNightProgress = GetDayNightProgress(phase);
            ApplyDayNightPointerPose(_currentDayNightProgress);
        }

        private void AnimateDayNightPointer(GamePhase previousPhase, GamePhase newPhase)
        {
            if (_dayNightNeedle == null)
                return;

            float previousProgress = GetDayNightProgress(previousPhase);
            float targetProgress = GetDayNightProgress(newPhase);
            if (Mathf.Approximately(previousProgress, targetProgress))
            {
                SetDayNightPointerImmediate(newPhase);
                return;
            }

            StopDayNightPointerAnimation();
            _currentDayNightProgress = previousProgress;
            ApplyDayNightPointerPose(_currentDayNightProgress);
            _dayNightPointerAnimation = StartCoroutine(
                AnimateDayNightPointerRoutine(previousProgress, targetProgress));
        }

        private IEnumerator AnimateDayNightPointerRoutine(float startProgress, float targetProgress)
        {
            float elapsed = 0f;
            float duration = Mathf.Max(0.01f, _pointerMoveDuration);

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float easedProgress = Mathf.SmoothStep(0f, 1f, progress);
                _currentDayNightProgress = Mathf.Lerp(startProgress, targetProgress, easedProgress);
                ApplyDayNightPointerPose(_currentDayNightProgress);
                yield return null;
            }

            _currentDayNightProgress = targetProgress;
            ApplyDayNightPointerPose(_currentDayNightProgress);
            _dayNightPointerAnimation = null;
        }

        private void StopDayNightPointerAnimation()
        {
            if (_dayNightPointerAnimation == null)
                return;

            StopCoroutine(_dayNightPointerAnimation);
            _dayNightPointerAnimation = null;
        }

        private static float GetDayNightProgress(GamePhase phase)
        {
            return phase == GamePhase.NightPlanning || phase == GamePhase.None
                ? 0f
                : 1f;
        }

        private void ApplyDayNightPointerPose(float progress)
        {
            if (_dayNightNeedle == null || !HasDayNightEndpoints())
                return;

            float clampedProgress = Mathf.Clamp01(progress);
            Vector2 start = _nightPointerEndpoint.anchoredPosition;
            Vector2 middle = _midPointerEndpoint.anchoredPosition;
            Vector2 end = _dayPointerEndpoint.anchoredPosition;
            Vector2 control = 2f * middle - 0.5f * (start + end);
            _dayNightNeedle.anchoredPosition = QuadraticBezier(
                start,
                control,
                end,
                clampedProgress);
            _dayNightNeedle.localRotation = Quaternion.Lerp(
                _nightPointerEndpoint.localRotation,
                _dayPointerEndpoint.localRotation,
                clampedProgress);
        }

        private bool HasDayNightEndpoints()
        {
            if (_nightPointerEndpoint != null && _midPointerEndpoint != null && _dayPointerEndpoint != null)
                return true;

            if (!_loggedMissingDayNightEndpoints)
            {
                Debug.LogError("[PrototypeHudView] Day Night Track requires Night Endpoint, Mid Endpoint, and Day Endpoint RectTransforms.");
                _loggedMissingDayNightEndpoints = true;
            }

            return false;
        }

        private static Vector2 QuadraticBezier(Vector2 start, Vector2 control, Vector2 end, float progress)
        {
            float inverse = 1f - progress;
            return inverse * inverse * start
                + 2f * inverse * progress * control
                + progress * progress * end;
        }

        private void SetHudVisible(bool visible)
        {
            if (_topBarRoot != null) _topBarRoot.gameObject.SetActive(visible);
            if (_bottomBarRoot != null) _bottomBarRoot.gameObject.SetActive(visible);
            if (_logPanelRoot != null) _logPanelRoot.gameObject.SetActive(visible);
            if (_guideEntryRoot != null) _guideEntryRoot.gameObject.SetActive(visible);
        }

        private void RefreshButtonStates()
        {
            GamePhase currentPhase = _gamePhaseSystem != null
                ? _gamePhaseSystem.CurrentPhase
                : GamePhase.NightPlanning;

            if (_startDayButton != null) _startDayButton.interactable = currentPhase == GamePhase.NightPlanning;
            if (_nextNightButton != null) _nextNightButton.interactable = currentPhase == GamePhase.DayResult;

            foreach (KeyValuePair<ToolConfig, Text> pair in _toolCountTexts)
            {
                Button button = _toolButtons.TryGetValue(pair.Key, out Button boundButton)
                    ? boundButton
                    : pair.Value != null ? pair.Value.GetComponentInParent<Button>() : null;
                if (button != null)
                    button.interactable = currentPhase == GamePhase.NightPlanning
                        && _inventorySystem != null
                        && _inventorySystem.GetCount(pair.Key) > 0;
            }
        }

        private void HandleDayStarted(OnDayStarted e)
        {
            _currentDay = e.CurrentDay;
            _maxDays = e.MaxDays;
            EnsureToolButtonsForInventory();
            RebuildToolButtonCounts();
            RefreshAll();
        }

        private void HandleGamePhaseChanged(OnGamePhaseChanged e)
        {
            if (e.NewPhase == GamePhase.NightPlanning && !_hasGameStarted)
            {
                _hasGameStarted = true;
                SetHudVisible(true);
            }

            RefreshAll(false);
            AnimateDayNightPointer(e.PreviousPhase, e.NewPhase);
            TryShowInitialGuide();
        }

        private void HandleNightTurnStarted(OnNightTurnStarted e) => RefreshAll();

        private void HandleNightTurnAdvanced(OnNightTurnAdvanced e) => RefreshAll();

        private void HandleRevenueChanged(OnRevenueChanged e)
        {
            _confidence = e.CurrentRevenueIndex;
            RefreshAll();
        }

        private void HandleCustomerFlowChanged(OnCustomerFlowChanged e)
        {
            _inStoreCount = e.InStoreCount;
            _todayTotal = e.TodayTotal;
            _flowTrend = e.Trend;
            RefreshAll();
        }

        private void HandleToolInventoryChanged(OnToolInventoryChanged e)
        {
            if (e.Tool == null) return;

            if (!_toolCountTexts.TryGetValue(e.Tool, out Text countText) || countText == null)
            {
                BindExistingToolButton(e.Tool, e.Count);
                if (!_toolCountTexts.ContainsKey(e.Tool) && _allowRuntimeHudGeneration)
                    CreateToolButton(_toolMenuRoot, e.Tool, _toolCountTexts.Count);
                RebuildToolButtonCounts();
                return;
            }

            countText.text = FormatToolCountLabel(e.Tool, countText, e.Count);
            RefreshButtonStates();
        }

        private void HandleToolSelected(OnToolSelected e)
        {
            RebuildToolButtonCounts();
        }

        private void HandlePrototypeLogMessage(OnPrototypeLogMessage e)
        {
            _logText.text = e.Message;
        }

        private void HandleCustomerFinalized(OnCustomerFinalized e)
        {
            switch (e.State)
            {
                case CustomerState.Angry:
                    _angryCustomerCount++;
                    break;
                case CustomerState.Scared:
                    _scaredCustomerCount++;
                    break;
                default:
                    _normalCustomerCount++;
                    break;
            }
        }

        private void HandleGameEnded(OnGameEnded e)
        {
            GameResultState.LastOutcome = e.Outcome;
            SceneManager.LoadScene("game over");
        }

        private static void ReloadCurrentScene()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(activeScene.buildIndex);
        }

        private void RebuildToolButtonCounts()
        {
            EnsureToolButtonsForInventory();
            if (_inventorySystem == null) return;

            foreach (KeyValuePair<ToolConfig, Text> pair in _toolCountTexts)
                if (pair.Key != null && pair.Value != null)
                    pair.Value.text = FormatToolCountLabel(
                        pair.Key,
                        pair.Value,
                        _inventorySystem.GetCount(pair.Key));
        }

        private void EnsureToolButtonsForInventory()
        {
            if (_inventorySystem == null || _toolMenuRoot == null) return;

            BindExistingToolButtons();
            foreach (KeyValuePair<ToolConfig, ToolStockState> pair in _inventorySystem.Stocks)
                if (pair.Key != null
                    && (!_toolCountTexts.TryGetValue(pair.Key, out Text countText) || countText == null)
                    && _allowRuntimeHudGeneration)
                    CreateToolButton(_toolMenuRoot, pair.Key, _toolCountTexts.Count);
        }

        private void BindExistingToolButtons()
        {
            if (_inventorySystem == null) return;

            foreach (KeyValuePair<ToolConfig, ToolStockState> pair in _inventorySystem.Stocks)
            {
                if (pair.Key == null)
                    continue;

                BindExistingToolButton(pair.Key, pair.Value.Count);
            }
        }

        private void BindExistingToolButton(ToolConfig tool, int count)
        {
            if (tool == null || _toolMenuRoot == null) return;

            Transform root = _toolMenuRoot.Find($"Tool Button {tool.Id}");
            if (root == null) return;

            Text label = FindText(root, "Label") ?? root.GetComponentInChildren<Text>(true);
            Button button = FindButton(root, string.Empty);
            Image icon = FindImage(root, "Icon");
            Image background = FindButtonImage(root, string.Empty);

            if (button != null)
            {
                ToolConfig selectedTool = tool;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => _inputController?.SelectTool(selectedTool));
                _toolButtons[tool] = button;
            }

            BindTooltip(root as RectTransform, tool);

            if (label != null)
            {
                label.text = FormatToolCountLabel(tool, label, count);
                if (!_preserveSceneHudVisuals)
                    ApplyButtonTextStyle(label);
                _toolCountTexts[tool] = label;
            }

            if (!_preserveSceneHudVisuals)
                ApplySprite(background, _buttonPropSprite, true);

            if (icon != null)
            {
                if (!_preserveSceneHudVisuals || icon.sprite == null)
                    icon.sprite = tool.Icon;
                icon.enabled = tool.Icon != null;
                if (!_preserveSceneHudVisuals)
                    icon.preserveAspect = true;
                icon.raycastTarget = false;
                _toolIcons[tool] = icon;
            }
        }

        private string FormatToolCountLabel(ToolConfig tool, Text label, int count)
        {
            if (_preserveSceneHudVisuals && label != null)
            {
                if (TryReplaceSceneToolCount(label.text, count, out string preservedLabel))
                    return preservedLabel;
            }

            string displayName = tool != null ? tool.DisplayName : string.Empty;
            return string.IsNullOrEmpty(displayName)
                ? $"x{count}"
                : displayName.EndsWith("\n")
                    ? $"{displayName}x{count}"
                    : $"{displayName}\nx{count}";
        }

        private static bool TryReplaceSceneToolCount(string text, int count, out string result)
        {
            result = string.Empty;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            string normalized = text.Replace("\r\n", "\n");
            string[] lines = normalized.Split('\n');
            int countLineIndex = -1;
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                    continue;

                if (IsCountLine(lines[i]))
                    countLineIndex = i;
                break;
            }

            if (countLineIndex < 0)
            {
                result = normalized.EndsWith("\n")
                    ? $"{normalized}x{count}"
                    : $"{normalized}\nx{count}";
                return true;
            }

            string countLine = lines[countLineIndex];
            int countStart = 0;
            while (countStart < countLine.Length && char.IsWhiteSpace(countLine[countStart]))
                countStart++;

            lines[countLineIndex] = $"{countLine.Substring(0, countStart)}x{count}";
            result = string.Join("\n", lines);
            return true;
        }

        private static bool IsCountLine(string line)
        {
            string trimmed = line.Trim();
            if (trimmed.Length < 2 || trimmed[0] != 'x')
                return false;

            for (int i = 1; i < trimmed.Length; i++)
                if (!char.IsDigit(trimmed[i]))
                    return false;

            return true;
        }

        private static string ResolvePhaseLabel(GamePhase phase)
        {
            return phase switch
            {
                GamePhase.NightPlanning => "Night",
                GamePhase.DaySimulation => "Day",
                GamePhase.DayResult => "Results",
                GamePhase.GameOver => "Game Over",
                _ => "Idle"
            };
        }

        private static RectSpec AnchorTopStretch(float height)
        {
            return new RectSpec(new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(0f, height), new Vector2(0.5f, 1f));
        }

        private static RectSpec AnchorBottomStretch(float height)
        {
            return new RectSpec(new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, height), new Vector2(0.5f, 0f));
        }

        private static RectSpec AnchorBottomToolRow()
        {
            return new RectSpec(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(720f, 78f), new Vector2(0f, 0.5f));
        }

        private static RectSpec AnchorBottomActionRow()
        {
            return new RectSpec(new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-18f, 0f), new Vector2(452f, 78f), new Vector2(1f, 0.5f));
        }

        private static RectSpec AnchorRightMiddle(float width, float height)
        {
            return new RectSpec(new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-18f, 18f), new Vector2(width, height), new Vector2(1f, 0.5f));
        }

        private static RectTransform FindRect(Transform root, string path)
        {
            Transform child = root.Find(path);
            return child != null ? child as RectTransform : null;
        }

        private static Text FindText(Transform root, string path)
        {
            Transform child = root.Find(path);
            return child != null ? child.GetComponent<Text>() : null;
        }

        private static void BindActionButton(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null || action == null)
                return;

            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }

        private static Image FindImage(Transform root, string path)
        {
            Transform child = root.Find(path);
            return child != null ? child.GetComponent<Image>() : null;
        }

        private static Image FindButtonImage(Transform root, string path)
        {
            if (root == null) return null;

            Transform target = string.IsNullOrEmpty(path) ? root : root.Find(path);
            if (target == null) return null;

            Transform background = target.Find("Background");
            if (background != null && background.TryGetComponent(out Image backgroundImage))
                return backgroundImage;

            return target.GetComponent<Image>();
        }

        private static Button FindButton(Transform root, string name)
        {
            if (root == null) return null;

            Transform target = string.IsNullOrEmpty(name) ? root : root.Find(name);
            if (target == null) return null;

            Transform background = target.Find("Background");
            if (background != null && background.TryGetComponent(out Button backgroundButton))
                return backgroundButton;

            if (target.TryGetComponent(out Button button))
                return button;

            return target.GetComponentInChildren<Button>(true);
        }

        private static void ConfigureHudRaycasts(Transform root)
        {
            if (root == null) return;

            foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;

            foreach (Button button in root.GetComponentsInChildren<Button>(true))
            {
                if (button.targetGraphic != null)
                {
                    button.targetGraphic.raycastTarget = true;
                    continue;
                }

                if (button.TryGetComponent(out Graphic graphic))
                    graphic.raycastTarget = true;
            }
        }

        private readonly struct RectSpec
        {
            public readonly Vector2 AnchorMin;
            public readonly Vector2 AnchorMax;
            public readonly Vector2 AnchoredPosition;
            public readonly Vector2 SizeDelta;
            public readonly Vector2 Pivot;

            public RectSpec(Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta, Vector2? pivot = null)
            {
                AnchorMin = anchorMin;
                AnchorMax = anchorMax;
                AnchoredPosition = anchoredPosition;
                SizeDelta = sizeDelta;
                Pivot = pivot ?? new Vector2(0.5f, 0.5f);
            }
        }
    }
}
