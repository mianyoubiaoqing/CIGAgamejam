using UnityEngine;

namespace CIGAgamejam
{
    public sealed class EconomySystem : MonoBehaviour
    {
        [SerializeField] private EconomyConfig _config;

        private float _currentRevenueIndex;
        private bool _hasConfigError;

        public float CurrentRevenueIndex => _currentRevenueIndex;
        public float BankruptcyThreshold => _hasConfigError ? 0f : _config.BankruptcyThreshold;

        private void Awake()
        {
            if (_config == null)
            {
                Debug.LogError("[EconomySystem] EconomyConfig is not assigned.");
                _hasConfigError = true;
                return;
            }

            _config.Validate();
            ResetEconomy();
        }

        private void OnEnable()
        {
            EventBus<OnToolEffectResolved>.Subscribe(HandleToolEffectResolved);
            EventBus<OnCustomerLeftStore>.Subscribe(HandleCustomerLeftStore);
            EventBus<OnFavorabilityDeltaRequested>.Subscribe(HandleFavorabilityDeltaRequested);
        }

        private void OnDestroy()
        {
            EventBus<OnToolEffectResolved>.Unsubscribe(HandleToolEffectResolved);
            EventBus<OnCustomerLeftStore>.Unsubscribe(HandleCustomerLeftStore);
            EventBus<OnFavorabilityDeltaRequested>.Unsubscribe(HandleFavorabilityDeltaRequested);
        }

        public void ResetEconomy()
        {
            if (_config == null) return;

            _currentRevenueIndex = _config.StartingRevenueIndex;
            EventBus<OnRevenueChanged>.Publish(new OnRevenueChanged(_currentRevenueIndex, 0f));
        }

        public void ApplyRevenueDelta(float delta)
        {
            if (_hasConfigError) return;

            _currentRevenueIndex = Mathf.Max(0f, _currentRevenueIndex + delta);
            EventBus<OnRevenueChanged>.Publish(new OnRevenueChanged(_currentRevenueIndex, delta));
        }

        public void RecordCustomerPurchase(CustomerContext customer)
        {
            if (_hasConfigError || customer == null || customer.HasLeftStore) return;

            ApplyRevenueDelta(3f);
        }

        private void HandleToolEffectResolved(OnToolEffectResolved e)
        {
            if (_hasConfigError) return;

            float penalty = ResolveRevenuePenalty(e.Effect);
            if (penalty > 0f)
                ApplyRevenueDelta(-penalty);
        }

        private void HandleCustomerLeftStore(OnCustomerLeftStore e)
        {
            if (_hasConfigError) return;

            if (e.Reason == ToolEffectType.ScareCustomerAway
                || e.Reason == ToolEffectType.ScareCustomerGroup
                || e.Reason == ToolEffectType.BribeSecurity)
            {
                ApplyRevenueDelta(-5f);
            }
        }

        private void HandleFavorabilityDeltaRequested(OnFavorabilityDeltaRequested e)
        {
            if (_hasConfigError) return;
            ApplyRevenueDelta(e.Delta);
        }

        private static float ResolveRevenuePenalty(ToolEffectDefinition effect)
        {
            float configured = Mathf.Abs(effect.Amount);
            if (configured > 0f)
                return configured;

            return effect.EffectType switch
            {
                _ => 0f
            };
        }
    }
}
