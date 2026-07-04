using System.Collections.Generic;
using UnityEngine;

namespace CIGAgamejam
{
    public sealed class ToolResolutionSystem : MonoBehaviour
    {
        [SerializeField] private GridSystem _gridSystem;

        private readonly Dictionary<ToolEffectType, IToolEffectHandler> _handlers = new();
        private readonly HashSet<GridPosition> _destroyedObjects = new();
        private readonly Dictionary<int, HashSet<GridPosition>> _destroyedObjectTriggersByCustomer = new();
        private bool _hasConfigError;

        public IReadOnlyCollection<GridPosition> DestroyedObjects => _destroyedObjects;

        private void Awake()
        {
            if (_gridSystem == null)
            {
                Debug.LogError("[ToolResolutionSystem] GridSystem is not assigned.");
                _hasConfigError = true;
            }

            RegisterDefaultHandlers();
        }

        private void OnEnable()
        {
            EventBus<OnToolPlaced>.Subscribe(HandleToolPlaced);
        }

        private void OnDestroy()
        {
            EventBus<OnToolPlaced>.Unsubscribe(HandleToolPlaced);
        }

        public void ResolveDayStartTools()
        {
            ResolveTrigger(ToolTriggerTiming.OnDayStart, null, default);
        }

        public void ResolveCustomerEnterCell(CustomerContext customer)
        {
            if (customer == null) return;
            ResolveDestroyedObjectProximity(customer);
            ResolveTrigger(ToolTriggerTiming.OnCustomerEnterCell, customer, customer.Position);
        }

        public void ResolveCustomerPassFrontCell(CustomerContext customer)
        {
            if (customer == null) return;
            ResolveTrigger(ToolTriggerTiming.OnCustomerPassFrontCell, customer, customer.Position);
        }

        public void ResolveCustomerPurchase(CustomerContext customer)
        {
            if (customer == null) return;
            ResolveTrigger(ToolTriggerTiming.OnCustomerPurchase, customer, customer.Position);
        }

        public void ResolveManual(GridPosition position)
        {
            ResolveTrigger(ToolTriggerTiming.OnManualResolve, null, position);
        }

        public void RegisterHandler(IToolEffectHandler handler)
        {
            if (handler == null) return;
            _handlers[handler.EffectType] = handler;
        }

        private void HandleToolPlaced(OnToolPlaced e)
        {
            if (e.Tool?.Config == null || e.Tool.Config.TriggerTiming != ToolTriggerTiming.OnManualResolve)
                return;

            ResolveManual(e.Tool.Origin);
        }

        private void ResolveTrigger(ToolTriggerTiming timing, CustomerContext customer, GridPosition position)
        {
            if (_hasConfigError) return;

            IReadOnlyList<PlacedTool> tools = _gridSystem.GetTriggerableTools(timing, position);
            for (int i = 0; i < tools.Count; i++)
                ResolveTool(tools[i], timing, customer);
        }

        private void ResolveTool(PlacedTool tool, ToolTriggerTiming timing, CustomerContext customer)
        {
            if (tool == null || !tool.CanTrigger(timing)) return;

            ToolEffectDefinition[] effects = tool.Config.Effects;
            if (effects == null || effects.Length == 0) return;

            bool customerWasInStore = customer != null && !customer.HasLeftStore;

            EventBus<OnToolTriggered>.Publish(new OnToolTriggered(tool, timing));

            for (int i = 0; i < effects.Length; i++)
            {
                ToolEffectDefinition effect = effects[i];
                if (!effect.PassesChance()) continue;
                if (!_handlers.TryGetValue(effect.EffectType, out IToolEffectHandler handler))
                {
                    Debug.LogWarning($"[ToolResolutionSystem] Missing handler for {effect.EffectType}.");
                    continue;
                }

                var context = new ToolEffectContext(tool, effect, customer);
                handler.Resolve(context);
                if (effect.EffectType == ToolEffectType.DestroyObject)
                {
                    MarkDestroyedObject(tool.Origin);
                    _gridSystem.MarkTileDestroyed(tool.Origin);
                }

                EventBus<OnToolEffectResolved>.Publish(
                    new OnToolEffectResolved(tool, effect, customer != null ? customer.CustomerId : -1));
            }

            bool removedCustomer = customerWasInStore && customer.HasLeftStore;
            TryDisableAfterRemovingCustomer(tool, removedCustomer);
            tool.ConsumeUse();
            RetireInactiveTool(tool);
        }

        private void RetireInactiveTool(PlacedTool tool)
        {
            if (tool == null || (!tool.IsDisabled && !tool.IsExhausted)) return;

            if (tool.IsExhausted && !tool.IsDisabled)
            {
                tool.Disable(ToolDisableReason.Exhausted);
                EventBus<OnToolDisabled>.Publish(new OnToolDisabled(tool, ToolDisableReason.Exhausted));
            }

            _gridSystem.RemoveToolFromBoard(tool);
        }

        private static void TryDisableAfterRemovingCustomer(PlacedTool tool, bool removedCustomer)
        {
            if (!removedCustomer || tool.IsDisabled)
                return;

            if (!tool.Config.PassesDisableAfterRemovingCustomerChance())
                return;

            if (tool.Disable(ToolDisableReason.AfterRemovingCustomer))
                EventBus<OnToolDisabled>.Publish(
                    new OnToolDisabled(tool, ToolDisableReason.AfterRemovingCustomer));
        }

        private void RegisterDefaultHandlers()
        {
            RegisterHandler(new ModifyPurchaseCostEffectHandler());
            RegisterHandler(new ScareCustomerAwayEffectHandler());
            RegisterHandler(new ReplaceGoodsWithFakeEffectHandler());
            RegisterHandler(new BribeSecurityEffectHandler());
            RegisterHandler(new DestroyObjectEffectHandler());
            RegisterHandler(new ReduceFavorabilityEffectHandler());
            RegisterHandler(new ScareCustomerGroupEffectHandler());
            RegisterHandler(new DisableToolEffectHandler());
        }

        private void MarkDestroyedObject(GridPosition position)
        {
            if (!_gridSystem.TryGetCellType(position, out GridCellType cellType))
                cellType = GridCellType.Floor;

            if (_destroyedObjects.Add(position))
                EventBus<OnWorldObjectDestroyed>.Publish(new OnWorldObjectDestroyed(position, cellType));
        }

        private void ResolveDestroyedObjectProximity(CustomerContext customer)
        {
            foreach (GridPosition destroyedPosition in _destroyedObjects)
            {
                int distance = Mathf.Abs(customer.Position.X - destroyedPosition.X)
                    + Mathf.Abs(customer.Position.Y - destroyedPosition.Y);
                if (distance > 1) continue;

                if (!_destroyedObjectTriggersByCustomer.TryGetValue(customer.CustomerId, out HashSet<GridPosition> triggered))
                {
                    triggered = new HashSet<GridPosition>();
                    _destroyedObjectTriggersByCustomer[customer.CustomerId] = triggered;
                }

                if (!triggered.Add(destroyedPosition)) continue;

                customer.State = CustomerState.Angry;
                EventBus<OnFavorabilityDeltaRequested>.Publish(
                    new OnFavorabilityDeltaRequested(-2f, customer.CustomerId, "DestroyedObjectAnger"));
                EventBus<OnCustomerAngered>.Publish(
                    new OnCustomerAngered(customer.CustomerId, ToolEffectType.DestroyObject, null));
            }
        }

        public static void DisableAngerSource(PlacedTool tool)
        {
            if (tool != null && tool.Disable(ToolDisableReason.Effect))
                EventBus<OnToolDisabled>.Publish(new OnToolDisabled(tool, ToolDisableReason.Effect));
        }
    }

    public sealed class ToolEffectContext
    {
        public PlacedTool Tool { get; }
        public ToolEffectDefinition Effect { get; }
        public CustomerContext Customer { get; }

        public ToolEffectContext(PlacedTool tool, ToolEffectDefinition effect, CustomerContext customer)
        {
            Tool = tool;
            Effect = effect;
            Customer = customer;
        }
    }

    public interface IToolEffectHandler
    {
        ToolEffectType EffectType { get; }
        void Resolve(ToolEffectContext context);
    }

    public sealed class ModifyPurchaseCostEffectHandler : IToolEffectHandler
    {
        public ToolEffectType EffectType => ToolEffectType.ModifyPurchaseCost;

        public void Resolve(ToolEffectContext context)
        {
            if (context.Customer != null)
                context.Customer.PurchaseCostModifier += context.Effect.Amount;
        }
    }

    public sealed class ScareCustomerAwayEffectHandler : IToolEffectHandler
    {
        public ToolEffectType EffectType => ToolEffectType.ScareCustomerAway;

        public void Resolve(ToolEffectContext context)
        {
            if (context.Customer == null) return;

            context.Customer.HasLeftStore = true;
            context.Customer.State = CustomerState.Scared;
            context.Customer.WasScaredAway = true;
            EventBus<OnCustomerLeftStore>.Publish(
                new OnCustomerLeftStore(context.Customer.CustomerId, ToolEffectType.ScareCustomerAway, CustomerState.Scared));
        }
    }

    public sealed class ScareCustomerGroupEffectHandler : IToolEffectHandler
    {
        public ToolEffectType EffectType => ToolEffectType.ScareCustomerGroup;

        public void Resolve(ToolEffectContext context)
        {
            if (context.Customer == null || context.Tool == null) return;

            int count = ResolveScareCount();
            context.Customer.HasLeftStore = true;
            context.Customer.WasScaredAway = true;
            context.Customer.State = CustomerState.Scared;
            EventBus<OnCustomerLeftStore>.Publish(
                new OnCustomerLeftStore(context.Customer.CustomerId, ToolEffectType.ScareCustomerGroup, CustomerState.Scared));

            EventBus<OnGroupScareRequested>.Publish(
                new OnGroupScareRequested(context.Tool.Origin, Mathf.Max(0, count - 1), context.Customer.CustomerId));
        }

        private static int ResolveScareCount()
        {
            float roll = Random.value;
            if (roll < 0.5f) return 1;
            if (roll < 0.8f) return 2;
            return 3;
        }
    }

    public sealed class ReplaceGoodsWithFakeEffectHandler : IToolEffectHandler
    {
        public ToolEffectType EffectType => ToolEffectType.ReplaceGoodsWithFake;

        public void Resolve(ToolEffectContext context)
        {
            if (context.Customer == null) return;

            context.Customer.State = CustomerState.Angry;
            context.Customer.BoughtFakeGoods = true;
            ToolResolutionSystem.DisableAngerSource(context.Tool);
            EventBus<OnFavorabilityDeltaRequested>.Publish(
                new OnFavorabilityDeltaRequested(-5f, context.Customer.CustomerId, "FakeGoodsAnger"));
            EventBus<OnCustomerAngered>.Publish(
                new OnCustomerAngered(context.Customer.CustomerId, ToolEffectType.ReplaceGoodsWithFake, context.Tool));
        }
    }

    public sealed class BribeSecurityEffectHandler : IToolEffectHandler
    {
        public ToolEffectType EffectType => ToolEffectType.BribeSecurity;

        public void Resolve(ToolEffectContext context)
        {
            if (context.Customer == null) return;

            context.Customer.HasLeftStore = true;
            context.Customer.State = CustomerState.Scared;
            context.Customer.WasRemovedBySecurity = true;
            EventBus<OnCustomerLeftStore>.Publish(
                new OnCustomerLeftStore(context.Customer.CustomerId, ToolEffectType.BribeSecurity, CustomerState.Scared));
        }
    }

    public sealed class DestroyObjectEffectHandler : IToolEffectHandler
    {
        public ToolEffectType EffectType => ToolEffectType.DestroyObject;

        public void Resolve(ToolEffectContext context)
        {
        }
    }

    public sealed class ReduceFavorabilityEffectHandler : IToolEffectHandler
    {
        public ToolEffectType EffectType => ToolEffectType.ReduceFavorability;

        public void Resolve(ToolEffectContext context)
        {
            if (context.Customer == null) return;

            context.Customer.State = CustomerState.Angry;
            ToolResolutionSystem.DisableAngerSource(context.Tool);
            float penalty = context.Effect.Amount > 0f ? -context.Effect.Amount : -2f;
            EventBus<OnFavorabilityDeltaRequested>.Publish(
                new OnFavorabilityDeltaRequested(penalty, context.Customer.CustomerId, "ToolAnger"));
            EventBus<OnCustomerAngered>.Publish(
                new OnCustomerAngered(context.Customer.CustomerId, ToolEffectType.ReduceFavorability, context.Tool));
        }
    }

    public sealed class DisableToolEffectHandler : IToolEffectHandler
    {
        public ToolEffectType EffectType => ToolEffectType.DisableTool;

        public void Resolve(ToolEffectContext context)
        {
            if (context.Tool != null && context.Tool.Disable(ToolDisableReason.Effect))
                EventBus<OnToolDisabled>.Publish(
                    new OnToolDisabled(context.Tool, ToolDisableReason.Effect));
        }
    }
}
