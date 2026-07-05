using UnityEngine;
using UnityEngine.UI;

namespace CIGAgamejam
{
    public sealed class StartGameUI : MonoBehaviour
    {
        [SerializeField] private GamePhaseSystem _gamePhaseSystem;
        [SerializeField] private Button _startButton;

        private void Awake()
        {
            if (_startButton != null)
                _startButton.onClick.AddListener(OnStartClicked);
        }

        private void OnDestroy()
        {
            if (_startButton != null)
                _startButton.onClick.RemoveListener(OnStartClicked);
        }

        public void Configure(GamePhaseSystem gamePhaseSystem, Button startButton)
        {
            if (_startButton != null)
                _startButton.onClick.RemoveListener(OnStartClicked);

            _gamePhaseSystem = gamePhaseSystem;
            _startButton = startButton;

            if (_startButton != null)
                _startButton.onClick.AddListener(OnStartClicked);
        }

        private void OnStartClicked()
        {
            _gamePhaseSystem?.BeginGame();
            gameObject.SetActive(false);
        }
    }
}
