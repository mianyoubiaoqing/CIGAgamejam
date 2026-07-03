using UnityEngine;

namespace CIGAgamejam
{
    public sealed class BossInterferenceSystem : MonoBehaviour
    {
        [SerializeField] private GridSystem _gridSystem;

        private bool _hasConfigError;

        private void Awake()
        {
            if (_gridSystem == null)
            {
                Debug.LogError("[BossInterferenceSystem] GridSystem is not assigned.");
                _hasConfigError = true;
            }
        }

        private void OnEnable()
        {
            EventBus<OnGamePhaseChanged>.Subscribe(HandleGamePhaseChanged);
        }

        private void OnDestroy()
        {
            EventBus<OnGamePhaseChanged>.Unsubscribe(HandleGamePhaseChanged);
        }

        private void HandleGamePhaseChanged(OnGamePhaseChanged e)
        {
            if (_hasConfigError || e.NewPhase != GamePhase.DaySimulation) return;
            _gridSystem.DisableRandomBossTarget();
        }
    }
}
