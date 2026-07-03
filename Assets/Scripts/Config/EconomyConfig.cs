using UnityEngine;

namespace CIGAgamejam
{
    [CreateAssetMenu(fileName = "EconomyConfig", menuName = "CIGAgamejam/Configs/EconomyConfig")]
    public sealed class EconomyConfig : ScriptableObject
    {
        [SerializeField, Min(0f)] private float _startingRevenueIndex = 100f;
        [SerializeField, Min(0f)] private float _bankruptcyThreshold = 20f;
        [SerializeField, Min(0f)] private float _baseCustomerRevenue = 10f;

        public float StartingRevenueIndex => _startingRevenueIndex;
        public float BankruptcyThreshold => _bankruptcyThreshold;
        public float BaseCustomerRevenue => _baseCustomerRevenue;

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
