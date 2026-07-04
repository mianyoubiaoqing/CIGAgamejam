using UnityEngine;

namespace CIGAgamejam
{
    [CreateAssetMenu(fileName = "EconomyConfig", menuName = "CIGAgamejam/Configs/EconomyConfig")]
    public sealed class EconomyConfig : ScriptableObject
    {
        [SerializeField, Min(0f)] private float _startingRevenueIndex = 100f;
        [SerializeField, Min(0f)] private float _bankruptcyThreshold = 20f;
        [Header("Favorability Formula")]
        [SerializeField] private float _successfulPurchaseFavorabilityDelta = 3f;
        [SerializeField, Min(0f)] private float _scaredCustomerFavorabilityPenalty = 10f;
        [SerializeField, Min(0f)] private float _angryCustomerFavorabilityPenalty = 5f;

        public float StartingRevenueIndex => _startingRevenueIndex;
        public float BankruptcyThreshold => _bankruptcyThreshold;
        public float SuccessfulPurchaseFavorabilityDelta => _successfulPurchaseFavorabilityDelta;
        public float ScaredCustomerFavorabilityPenalty => _scaredCustomerFavorabilityPenalty;
        public float AngryCustomerFavorabilityPenalty => _angryCustomerFavorabilityPenalty;

        private void OnValidate() => Validate();

        public void Validate()
        {
            if (_bankruptcyThreshold > _startingRevenueIndex)
            {
                Debug.LogError("[EconomyConfig] BankruptcyThreshold cannot be higher than StartingRevenueIndex. Clamped.");
                _bankruptcyThreshold = _startingRevenueIndex;
            }
        }
    }
}
