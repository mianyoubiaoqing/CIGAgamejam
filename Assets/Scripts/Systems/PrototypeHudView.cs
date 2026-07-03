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
            RectTransform topBar = CreatePanel(canvas.transform, "Top Bar", AnchorTopStretch(74f), new Color(0.07f, 0.08f, 0.09f, 0.94f));
            RectTransform bottomBar = CreatePanel(canvas.transform, "Tool Menu", AnchorBottomStretch(100f), new Color(0.08f, 0.08f, 0.08f, 0.94f));
            RectTransform logPanel = CreatePanel(canvas.transform, "Log Panel", AnchorRightMiddle(270f, 132f), new Color(0.05f, 0.05f, 0.05f, 0.78f));
            _toolMenuRoot = bottomBar;

            _confidenceText = CreateText(topBar, "Confidence", "\u4fe1\u5fc3 100%", 18, TextAnchor.MiddleLeft, new Vector2(12f, 0f), new Vector2(170f, 52f));
            _flowText = CreateText(topBar, "Flow", "\u5ba2\u6d41 \u5e97\u51850 \u4eca\u65e50", 18, TextAnchor.MiddleLeft, new Vector2(188f, 0f), new Vector2(230f, 52f));
            _phaseText = CreateText(topBar, "Phase", "\u7b2c1/1\u5929 \u591c\u665a", 18, TextAnchor.MiddleLeft, new Vector2(430f, 0f), new Vector2(180f, 52f));
            _turnText = CreateText(topBar, "Turn", "\u56de\u5408 1", 18, TextAnchor.MiddleLeft, new Vector2(620f, 0f), new Vector2(110f, 52f));
            BuildDayNightTrack(topBar);

            _logText = CreateText(logPanel, "Log Text", "\u4e8b\u4ef6\u65e5\u5fd7", 15, TextAnchor.UpperLeft, Vector2.zero, new Vector2(250f, 110f));
            BuildToolButtons(bottomBar);
            BuildActionButtons(bottomBar);
        }

        private Canvas CreateCanvas()
        {
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

        private void BuildDayNightTrack(RectTransform topBar)
        {
            RectTransform track = CreatePanel(topBar, "Day Night Track", new RectSpec(new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-190f, 0f), new Vector2(280f, 16f)), new Color(0.2f, 0.2f, 0.22f, 1f));
            CreateText(track, "Night Label", "\u591c", 14, TextAnchor.MiddleCenter, new Vector2(-120f, 23f), new Vector2(36f, 20f));
            CreateText(track, "Day Label", "\u663c", 14, TextAnchor.MiddleCenter, new Vector2(120f, 23f), new Vector2(36f, 20f));
            _dayNightNeedle = CreatePanel(track, "Needle", new RectSpec(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(10f, 28f)), new Color(0.95f, 0.78f, 0.2f, 1f));
        }

        private void BuildToolButtons(RectTransform bottomBar)
        {
            if (_inventorySystem == null) return;

            int index = 0;
            foreach (KeyValuePair<ToolConfig, ToolStockState> pair in _inventorySystem.Stocks)
            {
                CreateToolButton(bottomBar, pair.Key, index);
                index++;
            }
        }

        private void CreateToolButton(RectTransform parent, ToolConfig tool, int index)
        {
            if (parent == null || tool == null) return;

            RectTransform buttonTransform = CreateButton(
                parent,
                $"Tool Button {tool.Id}",
                $"{tool.DisplayName}\nx{_inventorySystem.GetCount(tool)}",
                new Vector2(20f + index * 112f, 0f),
                new Vector2(104f, 70f),
                () => _inputController?.SelectTool(tool));

            _toolCountTexts[tool] = buttonTransform.GetComponentInChildren<Text>();
        }

        private void BuildActionButtons(RectTransform bottomBar)
        {
            CreateButton(bottomBar, "Skip Button", "\u8df3\u8fc7", new Vector2(-250f, 0f), new Vector2(86f, 58f), () => _nightTurnSystem?.SkipTurn(), true);
            CreateButton(bottomBar, "Day Button", "\u5f00\u59cb\u8425\u4e1a", new Vector2(-148f, 0f), new Vector2(118f, 58f), () => _gamePhaseSystem?.EndNightAndStartDay(), true);
            CreateButton(bottomBar, "Result Button", "\u7ed3\u675f\u767d\u5929", new Vector2(-18f, 0f), new Vector2(118f, 58f), () => _gamePhaseSystem?.CompleteDaySimulation(), true);
            CreateButton(bottomBar, "Next Button", "\u4e0b\u4e00\u591c", new Vector2(112f, 0f), new Vector2(100f, 58f), () => _gamePhaseSystem?.StartNextNightOrFail(), true);
        }

        private RectTransform CreateButton(RectTransform parent, string name, string label, Vector2 anchoredPosition, Vector2 size, UnityEngine.Events.UnityAction action, bool alignRight = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = alignRight ? new Vector2(1f, 0.5f) : new Vector2(0f, 0.5f);
            rect.anchorMax = rect.anchorMin;
            rect.pivot = alignRight ? new Vector2(1f, 0.5f) : new Vector2(0f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Image image = go.AddComponent<Image>();
            image.color = new Color(0.18f, 0.2f, 0.23f, 1f);
            Button button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);
            CreateText(rect, "Label", label, 15, TextAnchor.MiddleCenter, Vector2.zero, size);
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

        private void RefreshAll()
        {
            _confidenceText.text = $"\u4fe1\u5fc3 {_confidence:0}%";
            _flowText.text = $"\u5ba2\u6d41 \u5e97\u5185{_inStoreCount} \u4eca\u65e5{_todayTotal} \u8d8b\u52bf{_flowTrend:+0%;-0%;0%}";
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

            if (!_toolCountTexts.ContainsKey(e.Tool))
            {
                CreateToolButton(_toolMenuRoot, e.Tool, _toolCountTexts.Count);
                return;
            }

            _toolCountTexts[e.Tool].text = $"{e.Tool.DisplayName}\nx{e.Count}";
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
            foreach (KeyValuePair<ToolConfig, Text> pair in _toolCountTexts)
                if (pair.Key != null && pair.Value != null)
                    pair.Value.text = $"{pair.Key.DisplayName}\nx{_inventorySystem.GetCount(pair.Key)}";
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

        private static RectSpec AnchorRightMiddle(float width, float height)
        {
            return new RectSpec(new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-18f, 0f), new Vector2(width, height), new Vector2(1f, 0.5f));
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
