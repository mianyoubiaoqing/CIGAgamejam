using UnityEngine;

namespace CIGAgamejam
{
    [CreateAssetMenu(fileName = "CampaignConfig", menuName = "CIGAgamejam/Configs/CampaignConfig")]
    public sealed class CampaignConfig : ScriptableObject
    {
        [SerializeField, Min(1)] private int _startingDay = 1;
        [SerializeField, Min(1)] private int _maxDays = 7;

        public int StartingDay => _startingDay;
        public int MaxDays => _maxDays;

        private void OnValidate() => Validate();

        public void Validate()
        {
            if (_startingDay < 1)
            {
                Debug.LogError("[CampaignConfig] StartingDay cannot be lower than 1. Reset to 1.");
                _startingDay = 1;
            }

            if (_maxDays < _startingDay)
            {
                Debug.LogError("[CampaignConfig] MaxDays cannot be lower than StartingDay. Reset to StartingDay.");
                _maxDays = _startingDay;
            }
        }
    }
}
