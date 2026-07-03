using System.Collections.Generic;
using UnityEngine;

namespace CIGAgamejam
{
    public sealed class ToolResolutionSystem : MonoBehaviour
    {
        [SerializeField] private GridSystem _gridSystem;

        private readonly Dictionary<ToolEffectType, IToolEffectHandler> _handlers = new();
        private bool _hasConfigError;

        private void Awake()
        {
            if (_gridSystem == null)
            {
                Debug.LogError("[ToolResolutionSystem] GridSystem is not assigned.");
                _hasConfigError = true;
            }

            RegisterDefaultHandlers();
        }

        public void ResolveDayStartTools()
        {
            ResolveTrigger(ToolTriggerTiming.OnDayStart, null, default);
        }

        public void ResolveCustomerEnterCell(CustomerContext customer)
        {
            if (customer == null) return;
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
                EventBus<OnToolEffectResolved>.Publish(
                    new OnToolEffectResolved(tool, effect, customer != null ? customer.CustomerId : -1));
            }

            tool.ConsumeUse();
        }

        private void RegisterDefaultHandlers()
        {
            RegisterHandler(new ModifyPurchaseCostEffectHandler());
            RegisterHandler(new ScareCustomerAwayEffectHandler());
            RegisterHandler(new ReplaceGoodsWithFakeEffectHandler());
            RegisterHandler(new BribeSecurityEffectHandler());
            RegisterHandler(new DestroyObjectEffectHandler());
            RegisterHandler(new DisableToolEffectHandler());
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
            context.Customer.WasScaredAway = true;
            EventBus<OnCustomerLeftStore>.Publish(
                new OnCustomerLeftStore(context.Customer.CustomerId, ToolEffectType.ScareCustomerAway));
        }
    }

    public sealed class ReplaceGoodsWithFakeEffectHandler : IToolEffectHandler
    {
        public ToolEffectType EffectType => ToolEffectType.ReplaceGoodsWithFake;

        public void Resolve(ToolEffectContext context)
        {
            if (context.Customer == null) return;

            context.Customer.HasLeftStore = true;
            context.Customer.BoughtFakeGoods = true;
            EventBus<OnCustomerLeftStore>.Publish(
                new OnCustomerLeftStore(context.Customer.CustomerId, ToolEffectType.ReplaceGoodsWithFake));
        }
    }

    public sealed class BribeSecurityEffectHandler : IToolEffectHandler
    {
        public ToolEffectType EffectType => ToolEffectType.BribeSecurity;

        public void Resolve(ToolEffectContext context)
        {
            if (context.Customer == null) return;

            context.Customer.HasLeftStore = true;
            context.Customer.WasRemovedBySecurity = true;
            EventBus<OnCustomerLeftStore>.Publish(
                new OnCustomerLeftStore(context.Customer.CustomerId, ToolEffectType.BribeSecurity));
        }
    }

    public sealed class DestroyObjectEffectHandler : IToolEffectHandler
    {
        public ToolEffectType EffectType => ToolEffectType.DestroyObject;

        public void Resolve(ToolEffectContext context)
        {
        }
    }

    public sealed class DisableToolEffectHandler : IToolEffectHandler
    {
        public ToolEffectType EffectType => ToolEffectType.DisableTool;

        public void Resolve(ToolEffectContext context)
        {
            context.Tool?.Disable();
        }
    }
}
