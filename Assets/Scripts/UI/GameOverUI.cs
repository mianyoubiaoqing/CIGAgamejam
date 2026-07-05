using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CIGAgamejam
{
    public sealed class GameOverUI : MonoBehaviour
    {
        [SerializeField] private Image _background;
        [SerializeField] private Sprite _victorySprite;
        [SerializeField] private Sprite _failureSprite;
        [SerializeField] private Button _restartButton;
        [SerializeField] private Button _mainMenuButton;
        [SerializeField] private string _gameSceneName = "Game";
        [SerializeField] private string _mainMenuSceneName = "main menu";

        private void Awake()
        {
            if (_background != null)
                _background.sprite = GameResultState.LastOutcome == GameOutcome.ShopBankrupted
                    ? _victorySprite
                    : _failureSprite;

            if (_restartButton != null)
                _restartButton.onClick.AddListener(RestartGame);

            if (_mainMenuButton != null)
                _mainMenuButton.onClick.AddListener(ReturnToMainMenu);
        }

        private void OnDestroy()
        {
            if (_restartButton != null)
                _restartButton.onClick.RemoveListener(RestartGame);

            if (_mainMenuButton != null)
                _mainMenuButton.onClick.RemoveListener(ReturnToMainMenu);
        }

        private void RestartGame()
        {
            SceneManager.LoadScene(_gameSceneName);
        }

        private void ReturnToMainMenu()
        {
            SceneManager.LoadScene(_mainMenuSceneName);
        }
    }
}
