using UnityEngine;
using UnityEngine.Events;

namespace CIGAgamejam
{
    public sealed class PrototypeAudioEventRelay : MonoBehaviour
    {
        public static PrototypeAudioEventRelay Instance { get; private set; }

        [Header("Phase")]
        public UnityEvent GamePhaseChanged = new();
        public UnityEvent PhaseNight = new();
        public UnityEvent PhaseDay = new();
        public UnityEvent PhaseResult = new();
        public UnityEvent PhaseGameOver = new();
        public UnityEvent GameEnded = new();
        public UnityEvent GameWon = new();
        public UnityEvent GameLost = new();

        [Header("Day / Turn")]
        public UnityEvent DayStarted = new();
        public UnityEvent DayEnded = new();
        public UnityEvent DayLimitReached = new();
        public UnityEvent NightTurnStarted = new();
        public UnityEvent NightTurnAdvanced = new();

        [Header("Tools")]
        public UnityEvent ToolSelected = new();
        public UnityEvent ToolPlacementRejected = new();
        public UnityEvent ToolPlaced = new();
        public UnityEvent ToolRemoved = new();
        public UnityEvent ToolDisabled = new();
        public UnityEvent ToolDisabledByBoss = new();
        public UnityEvent ToolDisabledBySecurity = new();
        public UnityEvent ToolDisabledByEffect = new();
        public UnityEvent ToolExhausted = new();
        public UnityEvent ToolTriggered = new();
        public UnityEvent ToolEffectResolved = new();

        [Header("Tool Placed By Type")]
        public UnityEvent SmithAgentPlaced = new();
        public UnityEvent QRCodePlaced = new();
        public UnityEvent FakeGoodsPlaced = new();
        public UnityEvent ClownBoxPlaced = new();
        public UnityEvent BribeEnvelopePlaced = new();
        public UnityEvent BoilingWaterPlaced = new();

        [Header("Tool Triggered By Type")]
        public UnityEvent SmithAgentTriggered = new();
        public UnityEvent QRCodeTriggered = new();
        public UnityEvent FakeGoodsTriggered = new();
        public UnityEvent ClownBoxTriggered = new();
        public UnityEvent BribeEnvelopeTriggered = new();
        public UnityEvent BoilingWaterTriggered = new();

        [Header("Tool Effect Resolved By Type")]
        public UnityEvent SmithAgentEffectResolved = new();
        public UnityEvent QRCodeEffectResolved = new();
        public UnityEvent FakeGoodsEffectResolved = new();
        public UnityEvent ClownBoxEffectResolved = new();
        public UnityEvent BribeEnvelopeEffectResolved = new();
        public UnityEvent BoilingWaterEffectResolved = new();

        [Header("Tool Effects")]
        public UnityEvent EffectScareCustomer = new();
        public UnityEvent EffectScareGroup = new();
        public UnityEvent EffectReduceFavorability = new();
        public UnityEvent EffectDestroyObject = new();
        public UnityEvent EffectFakeGoods = new();
        public UnityEvent EffectBribeSecurity = new();

        [Header("Customers")]
        public UnityEvent CustomerAngered = new();
        public UnityEvent CustomerScared = new();
        public UnityEvent CustomerLeftStore = new();
        public UnityEvent CustomerPurchased = new();
        public UnityEvent CustomerFinalized = new();

        [Header("World / Security")]
        public UnityEvent WorldObjectDestroyed = new();
        public UnityEvent SecurityRemovedTool = new();
        public UnityEvent ShopBankrupted = new();

        public GamePhase LastPhase { get; private set; } = GamePhase.None;
        public GameOutcome LastOutcome { get; private set; } = GameOutcome.None;
        public ToolConfig LastToolConfig { get; private set; }
        public PlacedTool LastPlacedTool { get; private set; }
        public ToolEffectType LastToolEffectType { get; private set; }
        public ToolDisableReason LastToolDisableReason { get; private set; } = ToolDisableReason.None;
        public CustomerState LastCustomerState { get; private set; } = CustomerState.Normal;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            EventBus<OnGamePhaseChanged>.Subscribe(HandleGamePhaseChanged);
            EventBus<OnGameEnded>.Subscribe(HandleGameEnded);
            EventBus<OnDayStarted>.Subscribe(HandleDayStarted);
            EventBus<OnDayEnded>.Subscribe(HandleDayEnded);
            EventBus<OnDayLimitReached>.Subscribe(HandleDayLimitReached);
            EventBus<OnNightTurnStarted>.Subscribe(HandleNightTurnStarted);
            EventBus<OnNightTurnAdvanced>.Subscribe(HandleNightTurnAdvanced);
            EventBus<OnToolSelected>.Subscribe(HandleToolSelected);
            EventBus<OnToolPlacementRejected>.Subscribe(HandleToolPlacementRejected);
            EventBus<OnToolPlaced>.Subscribe(HandleToolPlaced);
            EventBus<OnToolRemoved>.Subscribe(HandleToolRemoved);
            EventBus<OnToolDisabled>.Subscribe(HandleToolDisabled);
            EventBus<OnToolTriggered>.Subscribe(HandleToolTriggered);
            EventBus<OnToolEffectResolved>.Subscribe(HandleToolEffectResolved);
            EventBus<OnCustomerAngered>.Subscribe(HandleCustomerAngered);
            EventBus<OnCustomerLeftStore>.Subscribe(HandleCustomerLeftStore);
            EventBus<OnCustomerFinalized>.Subscribe(HandleCustomerFinalized);
            EventBus<OnWorldObjectDestroyed>.Subscribe(HandleWorldObjectDestroyed);
            EventBus<OnSecurityRemovedTool>.Subscribe(HandleSecurityRemovedTool);
            EventBus<OnShopBankrupted>.Subscribe(HandleShopBankrupted);
        }

        private void OnDisable()
        {
            EventBus<OnGamePhaseChanged>.Unsubscribe(HandleGamePhaseChanged);
            EventBus<OnGameEnded>.Unsubscribe(HandleGameEnded);
            EventBus<OnDayStarted>.Unsubscribe(HandleDayStarted);
            EventBus<OnDayEnded>.Unsubscribe(HandleDayEnded);
            EventBus<OnDayLimitReached>.Unsubscribe(HandleDayLimitReached);
            EventBus<OnNightTurnStarted>.Unsubscribe(HandleNightTurnStarted);
            EventBus<OnNightTurnAdvanced>.Unsubscribe(HandleNightTurnAdvanced);
            EventBus<OnToolSelected>.Unsubscribe(HandleToolSelected);
            EventBus<OnToolPlacementRejected>.Unsubscribe(HandleToolPlacementRejected);
            EventBus<OnToolPlaced>.Unsubscribe(HandleToolPlaced);
            EventBus<OnToolRemoved>.Unsubscribe(HandleToolRemoved);
            EventBus<OnToolDisabled>.Unsubscribe(HandleToolDisabled);
            EventBus<OnToolTriggered>.Unsubscribe(HandleToolTriggered);
            EventBus<OnToolEffectResolved>.Unsubscribe(HandleToolEffectResolved);
            EventBus<OnCustomerAngered>.Unsubscribe(HandleCustomerAngered);
            EventBus<OnCustomerLeftStore>.Unsubscribe(HandleCustomerLeftStore);
            EventBus<OnCustomerFinalized>.Unsubscribe(HandleCustomerFinalized);
            EventBus<OnWorldObjectDestroyed>.Unsubscribe(HandleWorldObjectDestroyed);
            EventBus<OnSecurityRemovedTool>.Unsubscribe(HandleSecurityRemovedTool);
            EventBus<OnShopBankrupted>.Unsubscribe(HandleShopBankrupted);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void HandleGamePhaseChanged(OnGamePhaseChanged e)
        {
            LastPhase = e.NewPhase;
            GamePhaseChanged.Invoke();

            switch (e.NewPhase)
            {
                case GamePhase.NightPlanning:
                    PhaseNight.Invoke();
                    break;
                case GamePhase.DaySimulation:
                    PhaseDay.Invoke();
                    break;
                case GamePhase.DayResult:
                    PhaseResult.Invoke();
                    break;
                case GamePhase.GameOver:
                    PhaseGameOver.Invoke();
                    break;
            }
        }

        private void HandleGameEnded(OnGameEnded e)
        {
            LastOutcome = e.Outcome;
            GameEnded.Invoke();

            if (e.Outcome == GameOutcome.ShopBankrupted)
                GameWon.Invoke();
            else if (e.Outcome == GameOutcome.TimeLimitFailed)
                GameLost.Invoke();
        }

        private void HandleDayStarted(OnDayStarted e) => DayStarted.Invoke();

        private void HandleDayEnded(OnDayEnded e) => DayEnded.Invoke();

        private void HandleDayLimitReached(OnDayLimitReached e) => DayLimitReached.Invoke();

        private void HandleNightTurnStarted(OnNightTurnStarted e) => NightTurnStarted.Invoke();

        private void HandleNightTurnAdvanced(OnNightTurnAdvanced e) => NightTurnAdvanced.Invoke();

        private void HandleToolSelected(OnToolSelected e)
        {
            LastToolConfig = e.Tool;
            ToolSelected.Invoke();
        }

        private void HandleToolPlacementRejected(OnToolPlacementRejected e)
        {
            LastToolConfig = e.Tool;
            ToolPlacementRejected.Invoke();
        }

        private void HandleToolPlaced(OnToolPlaced e)
        {
            LastPlacedTool = e.Tool;
            LastToolConfig = e.Tool != null ? e.Tool.Config : null;
            ToolPlaced.Invoke();
            InvokeToolPlacedEvent(LastToolConfig);
        }

        private void HandleToolRemoved(OnToolRemoved e)
        {
            LastPlacedTool = e.Tool;
            LastToolConfig = e.Tool != null ? e.Tool.Config : null;
            ToolRemoved.Invoke();
        }

        private void HandleToolDisabled(OnToolDisabled e)
        {
            LastPlacedTool = e.Tool;
            LastToolConfig = e.Tool != null ? e.Tool.Config : null;
            LastToolDisableReason = e.Reason;
            ToolDisabled.Invoke();

            switch (e.Reason)
            {
                case ToolDisableReason.BossInterference:
                    ToolDisabledByBoss.Invoke();
                    break;
                case ToolDisableReason.SecurityPatrol:
                    ToolDisabledBySecurity.Invoke();
                    break;
                case ToolDisableReason.Effect:
                case ToolDisableReason.AfterRemovingCustomer:
                    ToolDisabledByEffect.Invoke();
                    break;
                case ToolDisableReason.Exhausted:
                    ToolExhausted.Invoke();
                    break;
            }
        }

        private void HandleToolTriggered(OnToolTriggered e)
        {
            LastPlacedTool = e.Tool;
            LastToolConfig = e.Tool != null ? e.Tool.Config : null;
            ToolTriggered.Invoke();
            InvokeToolTriggeredEvent(LastToolConfig);
        }

        private void HandleToolEffectResolved(OnToolEffectResolved e)
        {
            LastPlacedTool = e.Tool;
            LastToolConfig = e.Tool != null ? e.Tool.Config : null;
            LastToolEffectType = e.Effect.EffectType;
            ToolEffectResolved.Invoke();
            InvokeToolEffectResolvedEvent(LastToolConfig);
            InvokeEffectEvent(e.Effect.EffectType);
        }

        private void HandleCustomerAngered(OnCustomerAngered e)
        {
            LastPlacedTool = e.SourceTool;
            LastToolConfig = e.SourceTool != null ? e.SourceTool.Config : null;
            LastToolEffectType = e.Reason;
            LastCustomerState = CustomerState.Angry;
            CustomerAngered.Invoke();
        }

        private void HandleCustomerLeftStore(OnCustomerLeftStore e)
        {
            LastToolEffectType = e.Reason;
            LastCustomerState = e.State;
            CustomerLeftStore.Invoke();

            if (e.State == CustomerState.Scared)
                CustomerScared.Invoke();
        }

        private void HandleCustomerFinalized(OnCustomerFinalized e)
        {
            LastCustomerState = e.State;
            CustomerFinalized.Invoke();

            if (e.Purchased)
                CustomerPurchased.Invoke();
            else if (e.State == CustomerState.Angry)
                CustomerAngered.Invoke();
            else if (e.State == CustomerState.Scared)
                CustomerScared.Invoke();
        }

        private void HandleWorldObjectDestroyed(OnWorldObjectDestroyed e) => WorldObjectDestroyed.Invoke();

        private void HandleSecurityRemovedTool(OnSecurityRemovedTool e)
        {
            LastPlacedTool = e.Tool;
            LastToolConfig = e.Tool != null ? e.Tool.Config : null;
            SecurityRemovedTool.Invoke();
        }

        private void HandleShopBankrupted(OnShopBankrupted e) => ShopBankrupted.Invoke();

        private void InvokeToolPlacedEvent(ToolConfig tool)
        {
            switch (tool != null ? tool.Id : string.Empty)
            {
                case "smith_agent":
                    SmithAgentPlaced.Invoke();
                    break;
                case "qrcode":
                    QRCodePlaced.Invoke();
                    break;
                case "fake_goods":
                    FakeGoodsPlaced.Invoke();
                    break;
                case "clown_box":
                    ClownBoxPlaced.Invoke();
                    break;
                case "bribe_envelope":
                    BribeEnvelopePlaced.Invoke();
                    break;
                case "boiling_water":
                    BoilingWaterPlaced.Invoke();
                    break;
            }
        }

        private void InvokeToolTriggeredEvent(ToolConfig tool)
        {
            switch (tool != null ? tool.Id : string.Empty)
            {
                case "smith_agent":
                    SmithAgentTriggered.Invoke();
                    break;
                case "qrcode":
                    QRCodeTriggered.Invoke();
                    break;
                case "fake_goods":
                    FakeGoodsTriggered.Invoke();
                    break;
                case "clown_box":
                    ClownBoxTriggered.Invoke();
                    break;
                case "bribe_envelope":
                    BribeEnvelopeTriggered.Invoke();
                    break;
                case "boiling_water":
                    BoilingWaterTriggered.Invoke();
                    break;
            }
        }

        private void InvokeToolEffectResolvedEvent(ToolConfig tool)
        {
            switch (tool != null ? tool.Id : string.Empty)
            {
                case "smith_agent":
                    SmithAgentEffectResolved.Invoke();
                    break;
                case "qrcode":
                    QRCodeEffectResolved.Invoke();
                    break;
                case "fake_goods":
                    FakeGoodsEffectResolved.Invoke();
                    break;
                case "clown_box":
                    ClownBoxEffectResolved.Invoke();
                    break;
                case "bribe_envelope":
                    BribeEnvelopeEffectResolved.Invoke();
                    break;
                case "boiling_water":
                    BoilingWaterEffectResolved.Invoke();
                    break;
            }
        }

        private void InvokeEffectEvent(ToolEffectType effectType)
        {
            switch (effectType)
            {
                case ToolEffectType.ScareCustomerAway:
                    EffectScareCustomer.Invoke();
                    break;
                case ToolEffectType.ScareCustomerGroup:
                    EffectScareGroup.Invoke();
                    break;
                case ToolEffectType.ReduceFavorability:
                    EffectReduceFavorability.Invoke();
                    break;
                case ToolEffectType.DestroyObject:
                    EffectDestroyObject.Invoke();
                    break;
                case ToolEffectType.ReplaceGoodsWithFake:
                    EffectFakeGoods.Invoke();
                    break;
                case ToolEffectType.BribeSecurity:
                    EffectBribeSecurity.Invoke();
                    break;
            }
        }
    }
}
