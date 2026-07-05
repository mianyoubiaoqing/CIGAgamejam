using UnityEngine;

namespace CIGAgamejam
{
    public sealed class NightTurnSystem : MonoBehaviour
    {
        [SerializeField] private SecurityPatrolSystem _securityPatrolSystem;

        private int _currentTurn;
        private bool _isNightActive;

        public int CurrentTurn => _currentTurn;
        public bool IsNightActive => _isNightActive;

        private void OnEnable()
        {
            EventBus<OnGamePhaseChanged>.Subscribe(HandleGamePhaseChanged);
        }

        private void OnDestroy()
        {
            EventBus<OnGamePhaseChanged>.Unsubscribe(HandleGamePhaseChanged);
        }

        public void RecordPlayerAction(string actionLabel)
        {
            if (!_isNightActive) return;

            _currentTurn++;
            EventBus<OnNightTurnAdvanced>.Publish(new OnNightTurnAdvanced(_currentTurn, actionLabel));
            EventBus<OnPrototypeLogMessage>.Publish(new OnPrototypeLogMessage($"Night turn {_currentTurn}: {actionLabel}"));
            _securityPatrolSystem?.AdvancePatrolTurn();
        }

        public void SkipTurn()
        {
            RecordPlayerAction("Skip");
        }

        private void HandleGamePhaseChanged(OnGamePhaseChanged e)
        {
            if (e.NewPhase == GamePhase.NightPlanning)
            {
                _currentTurn = 1;
                _isNightActive = true;
                EventBus<OnNightTurnStarted>.Publish(new OnNightTurnStarted(_currentTurn));
                EventBus<OnPrototypeLogMessage>.Publish(new OnPrototypeLogMessage("Night begins: choose tools, place traps, and avoid security patrol vision."));
                return;
            }

            if (e.NewPhase == GamePhase.DaySimulation)
                _isNightActive = false;
        }
    }
}
