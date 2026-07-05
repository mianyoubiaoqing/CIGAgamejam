using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CIGAgamejam
{
    public sealed class MainMenuUI : MonoBehaviour
    {
        [SerializeField] private Button _startButton;
        [SerializeField] private Button _quitButton;
        [SerializeField] private string _gameSceneName = "Game";

        private void Awake()
        {
            if (_startButton == null)
                _startButton = GetComponentInChildren<Button>(true);

            if (_startButton == null)
                _startButton = FindObjectOfType<Button>(true);

            if (_startButton != null)
                _startButton.onClick.AddListener(StartGame);

            if (_quitButton != null)
                _quitButton.onClick.AddListener(QuitGame);
        }

        private void OnDestroy()
        {
            if (_startButton != null)
                _startButton.onClick.RemoveListener(StartGame);

            if (_quitButton != null)
                _quitButton.onClick.RemoveListener(QuitGame);
        }

        public void StartGame()
        {
            if (!string.IsNullOrWhiteSpace(_gameSceneName))
                SceneManager.LoadScene(_gameSceneName);
        }

        public void QuitGame()
        {
            Application.Quit();
        }
    }
}
