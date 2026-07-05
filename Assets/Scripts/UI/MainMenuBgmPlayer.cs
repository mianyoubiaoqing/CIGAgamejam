using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CIGAgamejam
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class MainMenuBgmPlayer : MonoBehaviour
    {
        [SerializeField] private AudioClip _bgmClip;
        [SerializeField, Range(0f, 1f)] private float _volume = 0.8f;
        [SerializeField] private bool _playOnStart = true;
        [SerializeField] private bool _stopWhenSceneChanges = true;
        [SerializeField] private Button[] _stopButtons;
        [SerializeField, Min(0f)] private float _fadeOutSeconds = 0.35f;

        private AudioSource _audioSource;
        private Coroutine _fadeRoutine;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.loop = true;
            _audioSource.volume = _volume;
            _audioSource.clip = _bgmClip;
        }

        private void OnEnable()
        {
            if (_stopWhenSceneChanges)
                SceneManager.activeSceneChanged += HandleActiveSceneChanged;

            if (_stopButtons == null)
                return;

            for (int i = 0; i < _stopButtons.Length; i++)
                if (_stopButtons[i] != null)
                    _stopButtons[i].onClick.AddListener(StopBgmWithFade);
        }

        private void Start()
        {
            if (_playOnStart)
                PlayBgm();
        }

        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;

            if (_stopButtons == null)
                return;

            for (int i = 0; i < _stopButtons.Length; i++)
                if (_stopButtons[i] != null)
                    _stopButtons[i].onClick.RemoveListener(StopBgmWithFade);
        }

        public void PlayBgm()
        {
            if (_audioSource == null)
                _audioSource = GetComponent<AudioSource>();

            if (_bgmClip == null)
                return;

            StopFadeRoutine();
            _audioSource.clip = _bgmClip;
            _audioSource.loop = true;
            _audioSource.volume = _volume;

            if (!_audioSource.isPlaying)
                _audioSource.Play();
        }

        public void StopBgm()
        {
            StopFadeRoutine();

            if (_audioSource != null)
                _audioSource.Stop();
        }

        public void StopBgmWithFade()
        {
            if (_audioSource == null || !_audioSource.isPlaying)
                return;

            StopFadeRoutine();
            if (_fadeOutSeconds <= 0f)
            {
                StopBgm();
                return;
            }

            _fadeRoutine = StartCoroutine(FadeOutAndStop());
        }

        private void HandleActiveSceneChanged(Scene previousScene, Scene newScene)
        {
            if (previousScene == gameObject.scene)
                StopBgm();
        }

        private IEnumerator FadeOutAndStop()
        {
            float startVolume = _audioSource.volume;
            float elapsed = 0f;

            while (elapsed < _fadeOutSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / _fadeOutSeconds);
                _audioSource.volume = Mathf.Lerp(startVolume, 0f, t);
                yield return null;
            }

            _audioSource.Stop();
            _audioSource.volume = _volume;
            _fadeRoutine = null;
        }

        private void StopFadeRoutine()
        {
            if (_fadeRoutine == null)
                return;

            StopCoroutine(_fadeRoutine);
            _fadeRoutine = null;
        }
    }
}
