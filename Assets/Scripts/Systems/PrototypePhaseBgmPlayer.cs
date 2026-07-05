using System.Collections;
using UnityEngine;

namespace CIGAgamejam
{
    [DisallowMultipleComponent]
    public sealed class PrototypePhaseBgmPlayer : MonoBehaviour
    {
        [Header("Clips")]
        [SerializeField] private AudioClip _nightBgmClip;
        [SerializeField] private AudioClip _dayBgmClip;
        [SerializeField] private AudioClip _phaseTransitionSfxClip;

        [Header("Playback")]
        [SerializeField, Range(0f, 1f)] private float _bgmVolume = 0.8f;
        [SerializeField, Range(0f, 1f)] private float _sfxVolume = 1f;
        [SerializeField, Min(0f)] private float _restartDelayAfterTransition = 0f;
        [SerializeField] private bool _stopOnResultAndGameOver = true;

        private AudioSource _bgmSource;
        private AudioSource _sfxSource;
        private Coroutine _pendingBgmRoutine;
        private AudioClip _currentBgmClip;

        private void Awake()
        {
            _bgmSource = gameObject.AddComponent<AudioSource>();
            _bgmSource.playOnAwake = false;
            _bgmSource.loop = true;
            _bgmSource.volume = _bgmVolume;

            _sfxSource = gameObject.AddComponent<AudioSource>();
            _sfxSource.playOnAwake = false;
            _sfxSource.loop = false;
            _sfxSource.volume = _sfxVolume;
        }

        private void OnEnable()
        {
            EventBus<OnGamePhaseChanged>.Subscribe(HandleGamePhaseChanged);
        }

        private void OnDisable()
        {
            EventBus<OnGamePhaseChanged>.Unsubscribe(HandleGamePhaseChanged);
            StopPendingBgmRoutine();
        }

        private void HandleGamePhaseChanged(OnGamePhaseChanged e)
        {
            switch (e.NewPhase)
            {
                case GamePhase.NightPlanning:
                    SwitchToBgm(_nightBgmClip, e.PreviousPhase != GamePhase.None);
                    break;
                case GamePhase.DaySimulation:
                    SwitchToBgm(_dayBgmClip, true);
                    break;
                case GamePhase.DayResult:
                case GamePhase.GameOver:
                    if (_stopOnResultAndGameOver)
                        StopBgm(playTransitionSfx: e.PreviousPhase != GamePhase.None);
                    break;
            }
        }

        private void SwitchToBgm(AudioClip clip, bool playTransitionSfx)
        {
            StopPendingBgmRoutine();

            if (clip == null)
            {
                StopBgm(playTransitionSfx);
                return;
            }

            if (_bgmSource.isPlaying && _currentBgmClip == clip)
                return;

            _bgmSource.Stop();
            _currentBgmClip = null;

            float transitionDuration = 0f;
            if (playTransitionSfx)
                transitionDuration = PlayTransitionSfx();

            float restartDelay = transitionDuration + _restartDelayAfterTransition;
            if (restartDelay > 0f && playTransitionSfx)
                _pendingBgmRoutine = StartCoroutine(PlayBgmAfterDelay(clip, restartDelay));
            else
                PlayBgm(clip);
        }

        private void StopBgm(bool playTransitionSfx)
        {
            StopPendingBgmRoutine();
            _bgmSource.Stop();
            _currentBgmClip = null;

            if (playTransitionSfx)
                PlayTransitionSfx();
        }

        private void PlayBgm(AudioClip clip)
        {
            _bgmSource.clip = clip;
            _bgmSource.loop = true;
            _bgmSource.volume = _bgmVolume;
            _bgmSource.Play();
            _currentBgmClip = clip;
        }

        private float PlayTransitionSfx()
        {
            if (_phaseTransitionSfxClip == null)
                return 0f;

            _sfxSource.Stop();
            _sfxSource.PlayOneShot(_phaseTransitionSfxClip, _sfxVolume);
            return _phaseTransitionSfxClip.length;
        }

        private IEnumerator PlayBgmAfterDelay(AudioClip clip, float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            PlayBgm(clip);
            _pendingBgmRoutine = null;
        }

        private void StopPendingBgmRoutine()
        {
            if (_pendingBgmRoutine == null)
                return;

            StopCoroutine(_pendingBgmRoutine);
            _pendingBgmRoutine = null;
        }
    }
}
