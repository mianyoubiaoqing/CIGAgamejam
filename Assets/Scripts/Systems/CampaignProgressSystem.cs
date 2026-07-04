using UnityEngine;

namespace CIGAgamejam
{
    public sealed class CampaignProgressSystem : MonoBehaviour
    {
        [SerializeField] private CampaignConfig _config;

        private int _currentDay;
        private bool _hasConfigError;

        public int CurrentDay => _currentDay;
        public int MaxDays => _hasConfigError ? 0 : _config.MaxDays;
        public bool IsFinalDay => !_hasConfigError && _currentDay >= _config.MaxDays;

        private void Awake()
        {
            TryInitialize(false);
        }

        private void Start()
        {
            TryInitialize(true);
        }

        private void TryInitialize(bool logMissing)
        {
            _hasConfigError = _config == null;
            if (_hasConfigError)
            {
                if (logMissing)
                    Debug.LogError("[CampaignProgressSystem] CampaignConfig is not assigned.");
                return;
            }

            _config.Validate();
        }

        public void StartCampaign()
        {
            if (_hasConfigError) return;

            _currentDay = _config.StartingDay;
            EventBus<OnDayStarted>.Publish(new OnDayStarted(_currentDay, _config.MaxDays));
        }

        public bool TryAdvanceToNextDay()
        {
            if (_hasConfigError) return false;

            EventBus<OnDayEnded>.Publish(new OnDayEnded(_currentDay));

            if (_currentDay >= _config.MaxDays)
            {
                EventBus<OnDayLimitReached>.Publish(new OnDayLimitReached(_currentDay, _config.MaxDays));
                return false;
            }

            _currentDay++;
            EventBus<OnDayStarted>.Publish(new OnDayStarted(_currentDay, _config.MaxDays));
            return true;
        }
    }
}
