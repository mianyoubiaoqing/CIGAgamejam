using UnityEngine;

namespace CIGAgamejam
{
    [CreateAssetMenu(fileName = "EconomyConfig", menuName = "CIGAgamejam/Configs/EconomyConfig")]
    public sealed class EconomyConfig : ScriptableObject
    {
        [SerializeField, Min(0f)] private float _startingRevenueIndex = 100f;
        [SerializeField, Range(0f, 100f)] private float _maximumRevenueIndex = 100f;
        [SerializeField, Min(0f)] private float _bankruptcyThreshold = 20f;
        [Header("Favorability Formula")]
        [SerializeField] private float _successfulPurchaseFavorabilityDelta = 3f;
        [SerializeField, Min(0f)] private float _scaredCustomerFavorabilityPenalty = 10f;
        [SerializeField, Min(0f)] private float _angryCustomerFavorabilityPenalty = 5f;

        public float StartingRevenueIndex => _startingRevenueIndex;
        public float MaximumRevenueIndex => _maximumRevenueIndex;
        public float BankruptcyThreshold => _bankruptcyThreshold;
        public float SuccessfulPurchaseFavorabilityDelta => _successfulPurchaseFavorabilityDelta;
        public float ScaredCustomerFavorabilityPenalty => _scaredCustomerFavorabilityPenalty;
        public float AngryCustomerFavorabilityPenalty => _angryCustomerFavorabilityPenalty;

        private void OnValidate() => Validate();

        public void Validate()
        {
            _maximumRevenueIndex = Mathf.Clamp(_maximumRevenueIndex, 0f, 100f);
            _startingRevenueIndex = Mathf.Clamp(_startingRevenueIndex, 0f, _maximumRevenueIndex);
            if (_bankruptcyThreshold > _startingRevenueIndex)
            {
                Debug.LogError("[EconomyConfig] BankruptcyThreshold cannot be higher than StartingRevenueIndex. Clamped.");
                _bankruptcyThreshold = _startingRevenueIndex;
            }
        }
    }
}
