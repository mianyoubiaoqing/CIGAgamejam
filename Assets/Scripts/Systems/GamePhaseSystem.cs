using UnityEngine;

namespace CIGAgamejam
{
    public sealed class GamePhaseSystem : MonoBehaviour
    {
        [SerializeField] private CampaignProgressSystem _campaignProgressSystem;
        [SerializeField] private bool _beginOnStart = false;

        private GamePhase _currentPhase = GamePhase.None;
        private bool _gameEnded;

        public GamePhase CurrentPhase => _currentPhase;

        private void Start()
        {
            if (_beginOnStart)
                BeginGame();
        }

        public void BeginGame()
        {
            _gameEnded = false;
            _campaignProgressSystem?.StartCampaign();
            TransitionTo(GamePhase.NightPlanning);
        }

        public void EndNightAndStartDay()
        {
            if (_gameEnded || _currentPhase != GamePhase.NightPlanning) return;
            TransitionTo(GamePhase.DaySimulation);
        }

        public void CompleteDaySimulation()
        {
            if (_gameEnded || _currentPhase != GamePhase.DaySimulation) return;
            TransitionTo(GamePhase.DayResult);
        }

        public void StartNextNightOrFail()
        {
            if (_gameEnded || _currentPhase != GamePhase.DayResult) return;

            if (_campaignProgressSystem != null && !_campaignProgressSystem.TryAdvanceToNextDay())
            {
                EndGame(GameOutcome.TimeLimitFailed);
                return;
            }

            TransitionTo(GamePhase.NightPlanning);
        }

        public void EndGame(GameOutcome outcome)
        {
            if (_gameEnded) return;

            _gameEnded = true;
            TransitionTo(GamePhase.GameOver);
            EventBus<OnGameEnded>.Publish(new OnGameEnded(outcome));
        }

        private void TransitionTo(GamePhase newPhase)
        {
            if (_currentPhase == newPhase) return;

            GamePhase previous = _currentPhase;
            _currentPhase = newPhase;
            EventBus<OnGamePhaseChanged>.Publish(new OnGamePhaseChanged(newPhase, previous));
        }
    }
}
