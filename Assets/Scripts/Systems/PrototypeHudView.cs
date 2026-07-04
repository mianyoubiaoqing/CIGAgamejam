using System.Collections.Generic;
using UnityEngine;
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

        private readonly Dictionary<ToolConfig, Text> _toolCountTexts = new();
        private Font _font;
        private Text _confidenceText;
        private Text _flowText;
        private Text _phaseText;
        private Text _turnText;
        private Text _logText;
        private RectTransform _dayNightNeedle;
        private RectTransform _toolMenuRoot;
        private RectTransform _actionButtonRoot;
        private int _currentDay = 1;
        private int _maxDays = 1;
        private int _inStoreCount;
        private int _todayTotal;
        private float _flowTrend;
        private float _confidence = 100f;

        private void Awake()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildHud();
            RefreshAll();
        }

        private void Start()
        {
            EnsureToolButtonsForInventory();
            RefreshAll();
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
        }

        private void OnDestroy()
        {
            EventBus<OnDayStarted>.Unsubscribe(HandleDayStarted);
            EventBus<OnGamePhaseChanged>.Unsubscribe(HandleGamePhaseChanged);
            EventBus<OnNightTurnStarted>.Unsubscribe(HandleNightTurnStarted);
            EventBus<OnNightTurnAdvanced>.Unsubscribe(HandleNightTurnAdvanced);
            EventBus<OnRevenueChanged>.Unsubscribe(HandleRevenueChanged);
            EventBus<OnCustomerFlowChanged>.Unsubscribe(HandleCustomerFlowChanged);
            EventBus<OnToolInventoryChanged>.Unsubscribe(HandleToolInventoryChanged);
            EventBus<OnToolSelected>.Unsubscribe(HandleToolSelected);
            EventBus<OnPrototypeLogMessage>.Unsubscribe(HandlePrototypeLogMessage);
        }

        private void BuildHud()
        {
            Canvas canvas = CreateCanvas();
            if (TryBindExistingHud(canvas))
                return;

            RectTransform topBar = CreatePanel(canvas.transform, "Top Bar", AnchorTopStretch(82f), new Color(0.07f, 0.08f, 0.09f, 0.94f));
            RectTransform bottomBar = CreatePanel(canvas.transform, "Bottom Bar", AnchorBottomStretch(112f), new Color(0.08f, 0.08f, 0.08f, 0.94f));
            RectTransform logPanel = CreatePanel(canvas.transform, "Log Panel", AnchorRightMiddle(300f, 138f), new Color(0.05f, 0.05f, 0.05f, 0.78f));

            HorizontalLayoutGroup topLayout = topBar.gameObject.AddComponent<HorizontalLayoutGroup>();
            topLayout.padding = new RectOffset(12, 12, 10, 10);
            topLayout.spacing = 10f;
            topLayout.childAlignment = TextAnchor.MiddleLeft;
            topLayout.childControlHeight = true;
            topLayout.childControlWidth = true;
            topLayout.childForceExpandHeight = true;
            topLayout.childForceExpandWidth = false;

            _confidenceText = CreateLayoutText(topBar, "Favorability", "\u597d\u611f\u5ea6 100%", 16, TextAnchor.MiddleLeft, 140f);
            _flowText = CreateLayoutText(topBar, "Flow", "\u5ba2\u6d41 \u5e97\u51850 \u4eca\u65e5", 16, TextAnchor.MiddleLeft, 300f);
            _phaseText = CreateLayoutText(topBar, "Phase", "\u7b2c1/1\u5929 \u591c\u665a", 16, TextAnchor.MiddleLeft, 140f);
            _turnText = CreateLayoutText(topBar, "Turn", "\u56de\u5408 1", 16, TextAnchor.MiddleLeft, 70f);
            AddFlexibleSpace(topBar);
            BuildDayNightTrack(topBar);

            _logText = CreateText(logPanel, "Log Text", "\u4e8b\u4ef6\u65e5\u5fd7", 15, TextAnchor.UpperLeft, Vector2.zero, new Vector2(272f, 116f));

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
        }

        private Canvas CreateCanvas()
        {
            Transform existing = transform.Find("Prototype HUD");
            if (existing != null && existing.TryGetComponent(out Canvas existingCanvas))
                return existingCanvas;

            var canvasObject = new GameObject("Prototype HUD");
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            canvasObject.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private bool TryBindExistingHud(Canvas canvas)
        {
            if (canvas == null || canvas.transform.Find("Top Bar") == null)
                return false;

            _confidenceText = FindText(canvas.transform, "Top Bar/Favorability");
            _flowText = FindText(canvas.transform, "Top Bar/Flow");
            _phaseText = FindText(canvas.transform, "Top Bar/Phase");
            _turnText = FindText(canvas.transform, "Top Bar/Turn");
            _logText = FindText(canvas.transform, "Log Panel/Log Text");
            _dayNightNeedle = FindRect(canvas.transform, "Top Bar/Day Night Root/Day Night Track/Needle");
            _toolMenuRoot = FindRect(canvas.transform, "Bottom Bar/Tool Button Row");
            _actionButtonRoot = FindRect(canvas.transform, "Bottom Bar/Action Button Row");

            return _confidenceText != null
                && _flowText != null
                && _phaseText != null
                && _turnText != null
                && _logText != null
                && _dayNightNeedle != null
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

            RectTransform track = CreatePanel(trackRoot, "Day Night Track", new RectSpec(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -5f), new Vector2(270f, 16f)), new Color(0.2f, 0.2f, 0.22f, 1f));
            CreateText(trackRoot, "Night Label", "\u591c", 14, TextAnchor.MiddleCenter, new Vector2(-118f, 16f), new Vector2(36f, 20f));
            CreateText(trackRoot, "Day Label", "\u663c", 14, TextAnchor.MiddleCenter, new Vector2(118f, 16f), new Vector2(36f, 20f));
            _dayNightNeedle = CreatePanel(track, "Needle", new RectSpec(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(10f, 28f)), new Color(0.95f, 0.78f, 0.2f, 1f));
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
                () => _inputController?.SelectTool(tool));

            AddFixedLayout(buttonTransform, 104f, 70f);
            _toolCountTexts[tool] = buttonTransform.GetComponentInChildren<Text>();
        }

        private void BuildActionButtons(RectTransform bottomBar)
        {
            AddFixedLayout(CreateButton(bottomBar, "Skip Button", "\u8df3\u8fc7", Vector2.zero, new Vector2(86f, 58f), () => _nightTurnSystem?.SkipTurn()), 86f, 58f);
            AddFixedLayout(CreateButton(bottomBar, "Day Button", "\u5f00\u59cb\u8425\u4e1a", Vector2.zero, new Vector2(118f, 58f), () => _gamePhaseSystem?.EndNightAndStartDay()), 118f, 58f);
            AddFixedLayout(CreateButton(bottomBar, "Result Button", "\u7ed3\u675f\u9ed1\u591c", Vector2.zero, new Vector2(118f, 58f), () => _gamePhaseSystem?.CompleteDaySimulation()), 118f, 58f);
            AddFixedLayout(CreateButton(bottomBar, "Next Button", "\u4e0b\u4e00\u591c", Vector2.zero, new Vector2(100f, 58f), () => _gamePhaseSystem?.StartNextNightOrFail()), 100f, 58f);
        }

        private RectTransform CreateButton(RectTransform parent, string name, string label, Vector2 anchoredPosition, Vector2 size, UnityEngine.Events.UnityAction action)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = rect.anchorMin;
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Image image = go.AddComponent<Image>();
            image.color = new Color(0.18f, 0.2f, 0.23f, 1f);
            Button button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);
            CreateStretchText(rect, "Label", label, 15, TextAnchor.MiddleCenter, new Vector2(6f, 4f));
            return rect;
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

        private void RefreshAll()
        {
            _confidenceText.text = $"\u597d\u611f\u5ea6 {_confidence:0}%";
            _flowText.text = $"\u5ba2\u6d41 \u5e97\u5185{_inStoreCount} \u4eca\u65e5{_todayTotal}";
            string phase = _gamePhaseSystem != null ? ResolvePhaseLabel(_gamePhaseSystem.CurrentPhase) : "\u591c\u665a";
            _phaseText.text = $"\u7b2c{_currentDay}/{_maxDays}\u5929 {phase}";
            _turnText.text = $"\u56de\u5408 {(_nightTurnSystem != null ? _nightTurnSystem.CurrentTurn : 1)}";
            if (_dayNightNeedle != null)
                _dayNightNeedle.anchoredPosition = new Vector2(phase == "\u591c\u665a" ? 0f : 270f, 0f);
        }

        private void HandleDayStarted(OnDayStarted e)
        {
            _currentDay = e.CurrentDay;
            _maxDays = e.MaxDays;
            EnsureToolButtonsForInventory();
            RebuildToolButtonCounts();
            RefreshAll();
        }

        private void HandleGamePhaseChanged(OnGamePhaseChanged e) => RefreshAll();

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
                CreateToolButton(_toolMenuRoot, e.Tool, _toolCountTexts.Count);
                RebuildToolButtonCounts();
                return;
            }

            countText.text = $"{e.Tool.DisplayName}\nx{e.Count}";
        }

        private void HandleToolSelected(OnToolSelected e)
        {
            RebuildToolButtonCounts();
        }

        private void HandlePrototypeLogMessage(OnPrototypeLogMessage e)
        {
            _logText.text = e.Message;
        }

        private void RebuildToolButtonCounts()
        {
            EnsureToolButtonsForInventory();
            if (_inventorySystem == null) return;

            foreach (KeyValuePair<ToolConfig, Text> pair in _toolCountTexts)
                if (pair.Key != null && pair.Value != null)
                    pair.Value.text = $"{pair.Key.DisplayName}\nx{_inventorySystem.GetCount(pair.Key)}";
        }

        private void EnsureToolButtonsForInventory()
        {
            if (_inventorySystem == null || _toolMenuRoot == null) return;

            BindExistingToolButtons();
            foreach (KeyValuePair<ToolConfig, ToolStockState> pair in _inventorySystem.Stocks)
                if (pair.Key != null && (!_toolCountTexts.TryGetValue(pair.Key, out Text countText) || countText == null))
                    CreateToolButton(_toolMenuRoot, pair.Key, _toolCountTexts.Count);
        }

        private void BindExistingToolButtons()
        {
            foreach (KeyValuePair<ToolConfig, ToolStockState> pair in _inventorySystem.Stocks)
            {
                if (pair.Key == null || _toolCountTexts.ContainsKey(pair.Key))
                    continue;

                Transform button = _toolMenuRoot.Find($"Tool Button {pair.Key.Id}");
                Text countText = button != null ? button.GetComponentInChildren<Text>() : null;
                if (countText != null)
                    _toolCountTexts[pair.Key] = countText;
            }
        }

        private static string ResolvePhaseLabel(GamePhase phase)
        {
            return phase switch
            {
                GamePhase.NightPlanning => "\u591c\u665a",
                GamePhase.DaySimulation => "\u767d\u5929",
                GamePhase.DayResult => "\u7ed3\u7b97",
                GamePhase.GameOver => "\u7ed3\u675f",
                _ => "\u5f85\u673a"
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
