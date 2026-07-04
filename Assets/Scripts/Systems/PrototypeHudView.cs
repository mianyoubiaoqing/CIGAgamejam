using System.Collections.Generic;
using UnityEngine;
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
        [Header("HUD Art")]
        [SerializeField] private Sprite _buttonDaylightSprite;
        [SerializeField] private Sprite _buttonNightSprite;
        [SerializeField] private Sprite _buttonPropSprite;
        [SerializeField] private Sprite _phaseBarSprite;
        [SerializeField] private Sprite _phasePointerSprite;
        [SerializeField] private Sprite _logPanelSprite;

        private readonly Dictionary<ToolConfig, Text> _toolCountTexts = new();
        private Font _font;
        private Text _confidenceText;
        private Text _flowText;
        private Text _phaseText;
        private Text _turnText;
        private Text _logText;
        private RectTransform _topBarRoot;
        private RectTransform _bottomBarRoot;
        private RectTransform _logPanelRoot;
        private RectTransform _dayNightNeedle;
        private RectTransform _toolMenuRoot;
        private RectTransform _actionButtonRoot;
        private Button _skipButton;
        private Button _startDayButton;
        private Button _finishDayButton;
        private Button _nextNightButton;
        private GameObject _startScreen;
        private GameObject _resultPanel;
        private Text _resultText;
        private bool _hasGameStarted;
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
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Canvas canvas = CreateCanvas();
            BuildHud();
            ApplyBoundHudArt();
            BuildResultPanel(canvas);
            BuildStartScreen(canvas);
            _hasGameStarted = _gamePhaseSystem != null && _gamePhaseSystem.CurrentPhase != GamePhase.None;
            SetHudVisible(_hasGameStarted);
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
            EventBus<OnCustomerFinalized>.Subscribe(HandleCustomerFinalized);
            EventBus<OnGameEnded>.Subscribe(HandleGameEnded);
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
            EventBus<OnCustomerFinalized>.Unsubscribe(HandleCustomerFinalized);
            EventBus<OnGameEnded>.Unsubscribe(HandleGameEnded);
        }

        private void BuildHud()
        {
            Canvas canvas = CreateCanvas();
            if (TryBindExistingHud(canvas))
                return;

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

            _confidenceText = CreateLayoutText(topBar, "Favorability", "\u597d\u611f\u5ea6 100%", 16, TextAnchor.MiddleLeft, 140f);
            _flowText = CreateLayoutText(topBar, "Flow", "\u5ba2\u6d41 \u5e97\u51850 \u4eca\u65e5", 16, TextAnchor.MiddleLeft, 300f);
            _phaseText = CreateLayoutText(topBar, "Phase", "\u7b2c1/1\u5929 \u591c\u665a", 16, TextAnchor.MiddleLeft, 140f);
            _turnText = CreateLayoutText(topBar, "Turn", "\u56de\u5408 1", 16, TextAnchor.MiddleLeft, 70f);
            ApplyStatusTextStyle(_confidenceText);
            ApplyStatusTextStyle(_flowText);
            ApplyStatusTextStyle(_phaseText);
            ApplyStatusTextStyle(_turnText);
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

            _topBarRoot = FindRect(canvas.transform, "Top Bar");
            _bottomBarRoot = FindRect(canvas.transform, "Bottom Bar");
            _logPanelRoot = FindRect(canvas.transform, "Log Panel");
            _confidenceText = FindText(canvas.transform, "Top Bar/Favorability");
            _flowText = FindText(canvas.transform, "Top Bar/Flow");
            _phaseText = FindText(canvas.transform, "Top Bar/Phase");
            _turnText = FindText(canvas.transform, "Top Bar/Turn");
            _logText = FindText(canvas.transform, "Log Panel/Log Text");
            _dayNightNeedle = FindRect(canvas.transform, "Top Bar/Day Night Root/Day Night Track/Needle");
            _toolMenuRoot = FindRect(canvas.transform, "Bottom Bar/Tool Button Row");
            _actionButtonRoot = FindRect(canvas.transform, "Bottom Bar/Action Button Row");
            _skipButton = FindButton(_actionButtonRoot, "Skip Button");
            _startDayButton = FindButton(_actionButtonRoot, "Day Button");
            _finishDayButton = FindButton(_actionButtonRoot, "Result Button");
            _nextNightButton = FindButton(_actionButtonRoot, "Next Button");
            BindActionButton(_skipButton, () => _nightTurnSystem?.SkipTurn());
            BindActionButton(_startDayButton, () => _gamePhaseSystem?.EndNightAndStartDay());
            BindActionButton(_finishDayButton, () => _gamePhaseSystem?.CompleteDaySimulation());
            BindActionButton(_nextNightButton, () => _gamePhaseSystem?.StartNextNightOrFail());

            return _confidenceText != null
                && _flowText != null
                && _phaseText != null
                && _turnText != null
                && _logText != null
                && _topBarRoot != null
                && _bottomBarRoot != null
                && _logPanelRoot != null
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
            ApplySprite(track.GetComponent<Image>(), _phaseBarSprite, true);
            CreateText(trackRoot, "Night Label", "\u591c", 14, TextAnchor.MiddleCenter, new Vector2(-118f, 16f), new Vector2(36f, 20f));
            CreateText(trackRoot, "Day Label", "\u663c", 14, TextAnchor.MiddleCenter, new Vector2(118f, 16f), new Vector2(36f, 20f));
            _dayNightNeedle = CreatePanel(track, "Needle", new RectSpec(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(10f, 28f)), new Color(0.95f, 0.78f, 0.2f, 1f));
            ApplySprite(_dayNightNeedle.GetComponent<Image>(), _phasePointerSprite, true);
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
        }

        private void BuildActionButtons(RectTransform bottomBar)
        {
            RectTransform skip = CreateButton(bottomBar, "Skip Button", "\u8df3\u8fc7", Vector2.zero, new Vector2(86f, 58f), () => _nightTurnSystem?.SkipTurn(), _buttonNightSprite);
            RectTransform day = CreateButton(bottomBar, "Day Button", "\u5f00\u59cb\u8425\u4e1a", Vector2.zero, new Vector2(118f, 58f), () => _gamePhaseSystem?.EndNightAndStartDay(), _buttonDaylightSprite);
            RectTransform result = CreateButton(bottomBar, "Result Button", "\u7ed3\u675f\u8425\u4e1a", Vector2.zero, new Vector2(118f, 58f), () => _gamePhaseSystem?.CompleteDaySimulation(), _buttonDaylightSprite);
            RectTransform next = CreateButton(bottomBar, "Next Button", "\u4e0b\u4e00\u591c", Vector2.zero, new Vector2(100f, 58f), () => _gamePhaseSystem?.StartNextNightOrFail(), _buttonNightSprite);
            _skipButton = skip.GetComponent<Button>();
            _startDayButton = day.GetComponent<Button>();
            _finishDayButton = result.GetComponent<Button>();
            _nextNightButton = next.GetComponent<Button>();
            AddFixedLayout(skip, 86f, 58f);
            AddFixedLayout(day, 118f, 58f);
            AddFixedLayout(result, 118f, 58f);
            AddFixedLayout(next, 100f, 58f);
        }

        private void BuildResultPanel(Canvas canvas)
        {
            if (canvas == null || _resultPanel != null) return;

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
                "重新开始",
                new Vector2(0f, -175f),
                new Vector2(180f, 58f),
                ReloadCurrentScene);
            restart.anchorMin = new Vector2(0.5f, 0.5f);
            restart.anchorMax = restart.anchorMin;
            restart.pivot = new Vector2(0.5f, 0.5f);
            _resultPanel.SetActive(false);
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
                if (existingStartUI == null)
                    existingStartUI = _startScreen.AddComponent<StartGameUI>();
                existingStartUI.Configure(_gamePhaseSystem, existingButton);
                _startScreen.SetActive(_gamePhaseSystem == null || _gamePhaseSystem.CurrentPhase == GamePhase.None);
                _startScreen.transform.SetAsLastSibling();
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
                "捣蛋鬼要捣蛋",
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
                "开始游戏",
                new Vector2(0f, 8f),
                new Vector2(220f, 64f),
                null,
                _buttonDaylightSprite);
            startButton.anchorMin = new Vector2(0.5f, 0.5f);
            startButton.anchorMax = startButton.anchorMin;
            startButton.pivot = new Vector2(0.5f, 0.5f);
            Button button = startButton.GetComponent<Button>();
            button.onClick.RemoveAllListeners();

            Text subtitle = CreateText(
                screen,
                "Subtitle",
                "夜晚布设机关，白天整蛊顾客",
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

            Image image = go.AddComponent<Image>();
            image.color = new Color(0.18f, 0.2f, 0.23f, 1f);
            ApplySprite(image, backgroundSprite, true);
            Button button = go.AddComponent<Button>();
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

        private void RefreshAll()
        {
            if (_confidenceText == null || _flowText == null || _phaseText == null || _turnText == null)
                return;

            _confidenceText.text = $"\u597d\u611f\u5ea6 {_confidence:0}%";
            _flowText.text = $"\u5ba2\u6d41 \u5e97\u5185{_inStoreCount} \u4eca\u65e5{_todayTotal}";
            string phase = _gamePhaseSystem != null ? ResolvePhaseLabel(_gamePhaseSystem.CurrentPhase) : "\u591c\u665a";
            _phaseText.text = $"\u7b2c{_currentDay}/{_maxDays}\u5929 {phase}";
            _turnText.text = $"\u56de\u5408 {(_nightTurnSystem != null ? _nightTurnSystem.CurrentTurn : 1)}";
            if (_dayNightNeedle != null)
                _dayNightNeedle.anchoredPosition = new Vector2(phase == "\u591c\u665a" ? 0f : 270f, 0f);
            RefreshButtonStates();
        }

        private void SetHudVisible(bool visible)
        {
            if (_topBarRoot != null) _topBarRoot.gameObject.SetActive(visible);
            if (_bottomBarRoot != null) _bottomBarRoot.gameObject.SetActive(visible);
            if (_logPanelRoot != null) _logPanelRoot.gameObject.SetActive(visible);
        }

        private void ApplyBoundHudArt()
        {
            ApplySprite(FindImage(transform, "Prototype HUD/Log Panel"), _logPanelSprite, true);
            ApplySprite(FindImage(transform, "Prototype HUD/Top Bar/Day Night Root/Day Night Track"), _phaseBarSprite, true);
            ApplySprite(FindImage(transform, "Prototype HUD/Top Bar/Day Night Root/Day Night Track/Needle"), _phasePointerSprite, true);
            ApplySprite(FindImage(transform, "Prototype HUD/Bottom Bar/Action Button Row/Skip Button"), _buttonNightSprite, true);
            ApplySprite(FindImage(transform, "Prototype HUD/Bottom Bar/Action Button Row/Day Button"), _buttonDaylightSprite, true);
            ApplySprite(FindImage(transform, "Prototype HUD/Bottom Bar/Action Button Row/Result Button"), _buttonDaylightSprite, true);
            ApplySprite(FindImage(transform, "Prototype HUD/Bottom Bar/Action Button Row/Next Button"), _buttonNightSprite, true);
            ApplyButtonTextStyle(FindText(transform, "Prototype HUD/Bottom Bar/Action Button Row/Skip Button/Label"));
            ApplyButtonTextStyle(FindText(transform, "Prototype HUD/Bottom Bar/Action Button Row/Day Button/Label"));
            ApplyButtonTextStyle(FindText(transform, "Prototype HUD/Bottom Bar/Action Button Row/Result Button/Label"));
            ApplyButtonTextStyle(FindText(transform, "Prototype HUD/Bottom Bar/Action Button Row/Next Button/Label"));

            ApplyStatusTextStyle(_confidenceText);
            ApplyStatusTextStyle(_flowText);
            ApplyStatusTextStyle(_phaseText);
            ApplyStatusTextStyle(_turnText);
            ApplyButtonTextStyle(_logText);
        }

        private void RefreshButtonStates()
        {
            GamePhase currentPhase = _gamePhaseSystem != null
                ? _gamePhaseSystem.CurrentPhase
                : GamePhase.NightPlanning;
            bool isNight = currentPhase == GamePhase.None
                || currentPhase == GamePhase.NightPlanning;

            if (_skipButton != null) _skipButton.interactable = isNight;
            if (_startDayButton != null) _startDayButton.interactable = isNight;
            if (_finishDayButton != null) _finishDayButton.interactable = currentPhase == GamePhase.DaySimulation;
            if (_nextNightButton != null) _nextNightButton.interactable = currentPhase == GamePhase.DayResult;

            foreach (KeyValuePair<ToolConfig, Text> pair in _toolCountTexts)
            {
                Button button = pair.Value != null ? pair.Value.GetComponentInParent<Button>() : null;
                if (button != null)
                    button.interactable = isNight
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

            RefreshAll();
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
                CreateToolButton(_toolMenuRoot, e.Tool, _toolCountTexts.Count);
                RebuildToolButtonCounts();
                return;
            }

            countText.text = $"{e.Tool.DisplayName}\nx{e.Count}";
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
            if (_resultPanel == null || _resultText == null) return;

            string outcome = e.Outcome == GameOutcome.ShopBankrupted ? "店铺破产" : "经营结束";
            _resultText.text =
                $"{outcome}\n\n" +
                $"最终好感度：{_confidence:0}\n" +
                $"经营天数：{_currentDay}\n\n" +
                $"正常顾客：{_normalCustomerCount}\n" +
                $"愤怒顾客：{_angryCustomerCount}\n" +
                $"受惊顾客：{_scaredCustomerCount}";
            _resultPanel.SetActive(true);
            if (_toolMenuRoot != null) _toolMenuRoot.gameObject.SetActive(false);
            if (_actionButtonRoot != null) _actionButtonRoot.gameObject.SetActive(false);
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
                {
                    _toolCountTexts[pair.Key] = countText;
                    Button uiButton = button.GetComponent<Button>();
                    ToolConfig tool = pair.Key;
                    if (uiButton != null)
                    {
                        uiButton.onClick.RemoveAllListeners();
                        uiButton.onClick.AddListener(() => _inputController?.SelectTool(tool));
                    }

                    ApplySprite(button.GetComponent<Image>(), _buttonPropSprite, true);
                    ApplyButtonTextStyle(countText);
                    if (pair.Key.Icon != null && button.Find("Icon") == null)
                        CreateIconImage(button as RectTransform, "Icon", pair.Key.Icon, new Vector2(0f, 11f), new Vector2(42f, 42f));
                }
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

        private static Button FindButton(Transform root, string name)
        {
            if (root == null) return null;
            Transform child = root.Find(name);
            return child != null ? child.GetComponent<Button>() : null;
        }

        private static void BindActionButton(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null || action == null)
                return;

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        private static Image FindImage(Transform root, string path)
        {
            Transform child = root.Find(path);
            return child != null ? child.GetComponent<Image>() : null;
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
