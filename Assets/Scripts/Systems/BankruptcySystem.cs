using UnityEngine;

namespace CIGAgamejam
{
    public sealed class BankruptcySystem : MonoBehaviour
    {
        [SerializeField] private EconomySystem _economySystem;
        [SerializeField] private GamePhaseSystem _gamePhaseSystem;

        private bool _hasConfigError;
        private bool _bankruptcyReported;

        private void Awake()
        {
            if (_economySystem == null)
            {
                Debug.LogError("[BankruptcySystem] EconomySystem is not assigned.");
                _hasConfigError = true;
            }

            if (_gamePhaseSystem == null)
            {
                Debug.LogError("[BankruptcySystem] GamePhaseSystem is not assigned.");
                _hasConfigError = true;
            }
        }

        private void OnEnable()
        {
            EventBus<OnRevenueChanged>.Subscribe(HandleRevenueChanged);
        }

        private void OnDestroy()
        {
            EventBus<OnRevenueChanged>.Unsubscribe(HandleRevenueChanged);
        }

        private void HandleRevenueChanged(OnRevenueChanged e)
        {
            if (_hasConfigError || _bankruptcyReported) return;
            if (e.CurrentRevenueIndex > _economySystem.BankruptcyThreshold) return;

            _bankruptcyReported = true;
            EventBus<OnShopBankrupted>.Publish(
                new OnShopBankrupted(e.CurrentRevenueIndex, _economySystem.BankruptcyThreshold));
            _gamePhaseSystem.EndGame(GameOutcome.ShopBankrupted);
        }
    }
}
