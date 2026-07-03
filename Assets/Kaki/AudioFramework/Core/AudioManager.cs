using UnityEngine;
using System.Collections;
using UnityEngine.Audio;

namespace Kaki
{
    public class AudioManager : MonoBehaviour, IBgmEffectContext
    {
        public static AudioManager Instance { get; private set; }

        private Coroutine bgmFadeCoroutine;
        private Coroutine bgmSequenceCoroutine;
        private Coroutine bgmCrossfadeCoroutine;
        private Coroutine sfxSequenceCoroutine;

        // 当前BGM的“条目音量”，用于控制当前BGM的单曲音量
        private float currentBgmEntryVolume = 1f;

        [Header("音频源")]
        [Tooltip("配置项：bgmSourceAlt")]
        [SerializeField] private AudioSource bgmSource;
        [Tooltip("配置项：sfxSource")]
        [SerializeField] private AudioSource bgmSourceAlt;
        [Tooltip("配置项：oneShotSource")]
        [SerializeField] private AudioSource sfxSource;
        [Tooltip("配置项：oneShotBgmSource")]
        [SerializeField] private AudioSource oneShotSource;
        [Tooltip("配置项：triggerSource")]
        [SerializeField] private AudioSource oneShotBgmSource;
        [Tooltip("配置项：triggerBgmSource")]
        [SerializeField] private AudioSource triggerSource;
        [Tooltip("配置项：defaultBgmSource")]
        [SerializeField] private AudioSource triggerBgmSource;
        [Tooltip("配置项：defaultSfxSource")]
        [SerializeField] private AudioSource defaultBgmSource;
        [Tooltip("配置项")]
        [SerializeField] private AudioSource defaultSfxSource;

        // AudioManager 启动事件（用于外部系统订阅）
        public event System.Action OnAudioManagerStarted;

        [Header("混音器设置")]
        [Tooltip("配置项")]
        [SerializeField] private AudioMixer audioMixer;
        [Header("BGM 交叉淡入淡出")]
        [Tooltip("配置项：f")]
        [SerializeField] private bool enableBgmCrossfade = true;
        [Tooltip("配置项")]
        [SerializeField] private float bgmCrossfadeDuration = 1f;
        [Header("混音器组输出")]
        [Tooltip("配置项：sfxMixerGroup")]
        [SerializeField] private AudioMixerGroup bgmMixerGroup;
        [Tooltip("配置项")]
        [SerializeField] private AudioMixerGroup sfxMixerGroup;
        [Tooltip("当未指定MixerGroup时，按名称自动查找BGM组")]
        [SerializeField] private string bgmGroupName = "BGM";
        [Tooltip("当未指定MixerGroup时，按名称自动查找SFX组")]
        [SerializeField] private string sfxGroupName = "SFX";
        [Tooltip("AudioMixer中暴露的BGM音量参数名")]
        [SerializeField] private string bgmVolumeParam = "BGM_Volume";
        [Tooltip("AudioMixer中暴露的SFX音量参数名")]
        [SerializeField] private string sfxVolumeParam = "SFX_Volume";
        [Tooltip("AudioMixer中暴露的低通滤波器参数名,作用于BGM")]
        [SerializeField] private string bgmLowPassParam = "BGM_LOWPASS_CUTOFF";
        [Tooltip("AudioMixer中暴露的高通滤波器参数名,作用于BGM")]
        [SerializeField] private string bgmHighPassParam = "BGM_HIGHPASS_CUTOFF";

        [Header("默认音量0~1")]
        [Range(0f, 1f)] public float bgmVolume = 1f;
        [Range(0f, 1f)] public float sfxVolume = 1f;

        [Header("BGM 淡出时间")]
        [Tooltip("配置项：activeBgmSource")]
        [SerializeField] private float fadeOutDuration = 1f;

        private AudioSource activeBgmSource;
        private AudioSource inactiveBgmSource;

        public AudioMixer Mixer => audioMixer;
        public string BgmLowPassParameter => bgmLowPassParam;
        public string BgmHighPassParameter => bgmHighPassParam;

        // 单例初始化，避免场景重复创建
        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureBgmSources();
            EnsureSfxSources();
            ApplyOutputMixerGroups();
            if (audioMixer != null)
            {
                SetMixerVolume(bgmVolumeParam, Mathf.Clamp01(bgmVolume));
                SetMixerVolume(sfxVolumeParam, Mathf.Clamp01(sfxVolume));
            }
        }

        private void OnValidate()
        {
            ApplyOutputMixerGroups();
        }

        void Start()
        {
            OnAudioManagerStarted?.Invoke();
        }

        // 确保 AudioSource 输出到指定的 Mixer Group
        private void ApplyOutputMixerGroups()
        {
            if (audioMixer != null)
            {
                if (bgmMixerGroup == null && !string.IsNullOrWhiteSpace(bgmGroupName))
                {
                    var groups = audioMixer.FindMatchingGroups(bgmGroupName);
                    if (groups != null && groups.Length > 0)
                    {
                        bgmMixerGroup = groups[0];
                    }
                }

                if (sfxMixerGroup == null && !string.IsNullOrWhiteSpace(sfxGroupName))
                {
                    var groups = audioMixer.FindMatchingGroups(sfxGroupName);
                    if (groups != null && groups.Length > 0)
                    {
                        sfxMixerGroup = groups[0];
                    }
                }
            }

            if (bgmSource != null && bgmMixerGroup != null)
            {
                bgmSource.outputAudioMixerGroup = bgmMixerGroup;
            }

            if (bgmSourceAlt != null && bgmMixerGroup != null)
            {
                bgmSourceAlt.outputAudioMixerGroup = bgmMixerGroup;
            }

            if (defaultBgmSource != null && bgmMixerGroup != null)
            {
                defaultBgmSource.outputAudioMixerGroup = bgmMixerGroup;
            }

            if (sfxSource != null && sfxMixerGroup != null)
            {
                sfxSource.outputAudioMixerGroup = sfxMixerGroup;
            }

            if (oneShotSource != null && sfxMixerGroup != null)
            {
                oneShotSource.outputAudioMixerGroup = sfxMixerGroup;
            }

            if (oneShotBgmSource != null && bgmMixerGroup != null)
            {
                oneShotBgmSource.outputAudioMixerGroup = bgmMixerGroup;
            }

            if (triggerSource != null && sfxMixerGroup != null)
            {
                triggerSource.outputAudioMixerGroup = sfxMixerGroup;
            }

            if (triggerBgmSource != null && bgmMixerGroup != null)
            {
                triggerBgmSource.outputAudioMixerGroup = bgmMixerGroup;
            }

            if (defaultSfxSource != null && sfxMixerGroup != null)
            {
                defaultSfxSource.outputAudioMixerGroup = sfxMixerGroup;
            }
        }

        private void EnsureBgmSources()
        {
            if (bgmSource == null)
            {
                return;
            }

            if (bgmSourceAlt == null)
            {
                bgmSourceAlt = gameObject.AddComponent<AudioSource>();
                CopyBgmSourceSettings(bgmSource, bgmSourceAlt);
            }

            if (activeBgmSource == null)
            {
                activeBgmSource = bgmSource;
                inactiveBgmSource = bgmSourceAlt;
            }

            if (defaultBgmSource == null)
            {
                defaultBgmSource = gameObject.AddComponent<AudioSource>();
                CopyBgmSourceSettings(bgmSource, defaultBgmSource);
            }

            if (oneShotBgmSource == null)
            {
                oneShotBgmSource = gameObject.AddComponent<AudioSource>();
                CopyBgmSourceSettings(bgmSource, oneShotBgmSource);
            }

            if (triggerBgmSource == null)
            {
                triggerBgmSource = gameObject.AddComponent<AudioSource>();
                CopyBgmSourceSettings(bgmSource, triggerBgmSource);
            }
        }

        private void CopyBgmSourceSettings(AudioSource source, AudioSource target)
        {
            if (source == null || target == null)
            {
                return;
            }

            target.playOnAwake = false;
            target.loop = false;
            target.spatialBlend = source.spatialBlend;
            target.rolloffMode = source.rolloffMode;
            target.minDistance = source.minDistance;
            target.maxDistance = source.maxDistance;
            target.dopplerLevel = source.dopplerLevel;
            target.spread = source.spread;
            target.panStereo = source.panStereo;
            target.spatialize = source.spatialize;
            target.spatializePostEffects = source.spatializePostEffects;
            target.bypassEffects = source.bypassEffects;
            target.bypassListenerEffects = source.bypassListenerEffects;
            target.bypassReverbZones = source.bypassReverbZones;
            target.priority = source.priority;
            target.pitch = source.pitch;
            target.volume = source.volume;
        }

        private void EnsureSfxSources()
        {
            if (sfxSource == null)
            {
                return;
            }

            if (oneShotSource == null)
            {
                oneShotSource = gameObject.AddComponent<AudioSource>();
                CopySfxSourceSettings(sfxSource, oneShotSource);
            }

            if (triggerSource == null)
            {
                triggerSource = gameObject.AddComponent<AudioSource>();
                CopySfxSourceSettings(sfxSource, triggerSource);
            }

            if (defaultSfxSource == null)
            {
                defaultSfxSource = gameObject.AddComponent<AudioSource>();
                CopySfxSourceSettings(sfxSource, defaultSfxSource);
            }
        }

        private void CopySfxSourceSettings(AudioSource source, AudioSource target)
        {
            if (source == null || target == null)
            {
                return;
            }

            target.playOnAwake = false;
            target.loop = false;
            target.spatialBlend = source.spatialBlend;
            target.rolloffMode = source.rolloffMode;
            target.minDistance = source.minDistance;
            target.maxDistance = source.maxDistance;
            target.dopplerLevel = source.dopplerLevel;
            target.spread = source.spread;
            target.panStereo = source.panStereo;
            target.spatialize = source.spatialize;
            target.spatializePostEffects = source.spatializePostEffects;
            target.bypassEffects = source.bypassEffects;
            target.bypassListenerEffects = source.bypassListenerEffects;
            target.bypassReverbZones = source.bypassReverbZones;
            target.priority = source.priority;
            target.pitch = source.pitch;
            target.volume = source.volume;
        }

        // 播放一段BGM，结束后自动切换下一段（不循环）
        public void PlayBgmOnceThen(AudioClip firstClip, AudioClip nextClip, float firstVolume = 1f, float nextVolume = 1f)
        {
            if (bgmSource == null) return;
            if (firstClip == null || nextClip == null) return;

            AudioSource source = StartBgmPlayback(firstClip, false, firstVolume, allowCrossfade: true);
            if (source == null)
            {
                return;
            }

            bgmSequenceCoroutine = StartCoroutine(PlayNextBgmWhenFinished(source, nextClip, nextVolume));
        }




        // 停止 BGM（自动淡出）
        [TriggerAction]
        public void StopBGM()
        {
            if ((activeBgmSource == null || !activeBgmSource.isPlaying) &&
                (inactiveBgmSource == null || !inactiveBgmSource.isPlaying) &&
                (defaultBgmSource == null || !defaultBgmSource.isPlaying) &&
                (oneShotBgmSource == null || !oneShotBgmSource.isPlaying) &&
                (triggerBgmSource == null || !triggerBgmSource.isPlaying))
            {
                return;
            }

            CancelBgmSequence();
            StopBgmCrossfade();

            if (bgmFadeCoroutine != null)
                StopCoroutine(bgmFadeCoroutine);

            if (activeBgmSource != null && activeBgmSource.isPlaying)
            {
                bgmFadeCoroutine = StartCoroutine(FadeOutAndStopBGM(activeBgmSource, fadeOutDuration));
            }

            if (inactiveBgmSource != null && inactiveBgmSource.isPlaying)
            {
                StartCoroutine(FadeOutAndStopBGM(inactiveBgmSource, fadeOutDuration));
            }

            if (defaultBgmSource != null && defaultBgmSource.isPlaying)
            {
                StartCoroutine(FadeOutAndStopBGM(defaultBgmSource, fadeOutDuration));
            }

            if (oneShotBgmSource != null && oneShotBgmSource.isPlaying)
            {
                oneShotBgmSource.Stop();
            }

            if (triggerBgmSource != null && triggerBgmSource.isPlaying)
            {
                triggerBgmSource.Stop();
            }
        }




        // 设置全局BGM音量（0~1）
        [TriggerAction]
        public void SetGlobalBGMVolume(float volume)
        {
            bgmVolume = Mathf.Clamp01(volume);
            SetMixerVolume(bgmVolumeParam, bgmVolume);
        }

        // 更新当前BGM条目的独立音量（0~1），用于运行时实时调整
        public void SetCurrentBGMEntryVolume(float entryVolume)
        {
            currentBgmEntryVolume = Mathf.Clamp01(entryVolume);
            if (activeBgmSource != null)
            {
                activeBgmSource.volume = currentBgmEntryVolume;
            }
        }




        // 设置全局SFX音量（0~1）
        [TriggerAction]
        public void SetGlobalSFXVolume(float volume)
        {
            sfxVolume = Mathf.Clamp01(volume);
            SetMixerVolume(sfxVolumeParam, sfxVolume);
        }

        // 更新SFX循环播放的音量（0~1），不会影响Mixer中的全局SFX音量
        public void SetSfxLoopVolume(float volume)
        {
            if (sfxSource == null) return;
            sfxSource.volume = Mathf.Clamp01(volume);
        }

        // 更新 Default BGM 的音量（0~1）
        public void SetDefaultBgmVolume(float volume)
        {
            if (defaultBgmSource == null) return;
            defaultBgmSource.volume = Mathf.Clamp01(volume);
        }

        // 更新 Default SFX 的音量（0~1）
        public void SetDefaultSfxVolume(float volume)
        {
            if (defaultSfxSource == null) return;
            defaultSfxSource.volume = Mathf.Clamp01(volume);
        }

        // 播放一次性 BGM（不循环）
        public void PlayBGMOnce(AudioClip clip, float entryVolume = 1f)
        {
            StartBgmPlayback(clip, false, entryVolume, allowCrossfade: true);
        }



        // 播放 Default（单次播放，可打断同类型的 Loop/Default，不影响 Overlay/Trigger）
        public void PlayDefault(AudioClip clip, AudioType playbackType, float entryVolume = 1f)
        {
            if (clip == null) return;

            if (playbackType == AudioType.BGM)
            {
                EnsureBgmSources();
                ApplyOutputMixerGroups();

                if (defaultBgmSource == null) return;

                CancelBgmSequence();
                StopBgmCrossfade();

                if (bgmFadeCoroutine != null)
                {
                    StopCoroutine(bgmFadeCoroutine);
                    bgmFadeCoroutine = null;
                }

                float targetVolume = Mathf.Clamp01(entryVolume);
                var fromSource = GetCurrentMainBgmSource();
                bool canCrossfade = enableBgmCrossfade &&
                    fromSource != null &&
                    fromSource.isPlaying &&
                    fromSource != defaultBgmSource;

                if (canCrossfade)
                {
                    StopOtherMainBgmSources(fromSource, defaultBgmSource);
                    SetupBgmSource(defaultBgmSource, clip, false, 0f);
                    defaultBgmSource.Play();
                    bgmCrossfadeCoroutine = StartCoroutine(CrossfadeBgm(fromSource, defaultBgmSource, fromSource.volume, targetVolume, bgmCrossfadeDuration));
                }
                else
                {
                    StopBgmLoopPlaybackImmediate();
                    StopOtherMainBgmSources(null, defaultBgmSource);
                    defaultBgmSource.Stop();
                    SetupBgmSource(defaultBgmSource, clip, false, targetVolume);
                    defaultBgmSource.Play();
                }

                currentBgmEntryVolume = targetVolume;
                SetMixerVolume(bgmVolumeParam, bgmVolume);
            }
            else
            {
                StopSfxLoopPlaybackImmediate();
                EnsureSfxSources();
                ApplyOutputMixerGroups();

                if (defaultSfxSource == null) return;
                defaultSfxSource.Stop();
                defaultSfxSource.clip = clip;
                defaultSfxSource.loop = false;
                defaultSfxSource.volume = Mathf.Clamp01(entryVolume);
                defaultSfxSource.Play();
            }
        }

        // 播放 Overlay（叠加播放，不打断其他模式）
        public void PlayOverlay(AudioClip clip, AudioType playbackType, float entryVolume = 1f)
        {
            if (clip == null) return;

            if (playbackType == AudioType.BGM)
            {
                EnsureBgmSources();
                ApplyOutputMixerGroups();
                if (oneShotBgmSource == null) return;
                PlayOneShotOnSource(oneShotBgmSource, clip, entryVolume);
                return;
            }

            EnsureSfxSources();
            ApplyOutputMixerGroups();
            if (oneShotSource == null) return;
            PlayOneShotOnSource(oneShotSource, clip, entryVolume);
        }

        // 播放一次性音效（兼容旧调用，等价于 Overlay + SFX）
        public void PlaySFX(AudioClip clip, float entryVolume = 1f)
        {
            PlayOverlay(clip, AudioType.SFX, entryVolume);
        }

        // 播放 Trigger 音效（同一时刻只允许一个 Trigger 播放，不打断其他模式）
        public bool PlayTrigger(AudioClip clip, AudioType playbackType, float entryVolume = 1f)
        {
            if (clip == null) return false;

            if (playbackType == AudioType.BGM)
            {
                EnsureBgmSources();
                ApplyOutputMixerGroups();
                if (triggerBgmSource == null || triggerBgmSource.isPlaying) return false;
                triggerBgmSource.clip = clip;
                triggerBgmSource.loop = false;
                triggerBgmSource.volume = Mathf.Clamp01(entryVolume);
                triggerBgmSource.Play();
                return true;
            }

            EnsureSfxSources();
            ApplyOutputMixerGroups();
            if (triggerSource == null || triggerSource.isPlaying) return false;
            triggerSource.clip = clip;
            triggerSource.loop = false;
            triggerSource.volume = Mathf.Clamp01(entryVolume);
            triggerSource.Play();
            return true;
        }

        // 直接播放一个BGM剪辑（绕过任何“库”，用于事件驱动）
        public void PlayBGMClip(AudioClip clip, float entryVolume = 1f)
        {
            StartBgmPlayback(clip, true, entryVolume, allowCrossfade: true);
        }

        // 循环播放一个SFX剪辑
        public void PlaySFXLoop(AudioClip clip, float entryVolume = 1f)
        {
            if (sfxSource == null || clip == null) return;

            StopDefaultOfType(AudioType.SFX);
            StopSfxSequence();

            sfxSource.clip = clip;
            sfxSource.loop = true;
            sfxSource.volume = Mathf.Clamp01(entryVolume);
            sfxSource.Play();
        }

        [TriggerAction]
        public void StopSFX()
        {
            StopSfxLoopPlaybackImmediate();

            if (defaultSfxSource != null && defaultSfxSource.isPlaying)
            {
                defaultSfxSource.Stop();
            }

            if (oneShotSource != null && oneShotSource.isPlaying)
            {
                oneShotSource.Stop();
            }

            if (triggerSource != null && triggerSource.isPlaying)
            {
                triggerSource.Stop();
            }
        }

        // 先播放一次SFX引子，再循环播放主体
        public void PlaySFXOnceThenLoop(AudioClip introClip, AudioClip loopClip, float entryVolume = 1f)
        {
            if (sfxSource == null || introClip == null || loopClip == null) return;

            StopDefaultOfType(AudioType.SFX);
            StopSfxSequence();

            sfxSource.loop = false;
            sfxSource.clip = introClip;
            sfxSource.volume = Mathf.Clamp01(entryVolume);
            sfxSource.Play();

            sfxSequenceCoroutine = StartCoroutine(PlaySfxLoopAfterIntro(introClip.length, loopClip, entryVolume));
        }

        private AudioSource StartBgmPlayback(AudioClip clip, bool loop, float entryVolume, bool allowCrossfade)
        {
            if (bgmSource == null || clip == null)
            {
                return null;
            }

            EnsureBgmSources();
            CancelBgmSequence();
            StopBgmCrossfade();

            if (bgmFadeCoroutine != null)
            {
                StopCoroutine(bgmFadeCoroutine);
                bgmFadeCoroutine = null;
            }

            float targetVolume = Mathf.Clamp01(entryVolume);
            var fromSource = GetCurrentMainBgmSource();
            bool activePlaying = activeBgmSource != null && activeBgmSource.isPlaying;
            var targetSource = activePlaying ? inactiveBgmSource : activeBgmSource;
            if (targetSource == null)
            {
                targetSource = activeBgmSource ?? inactiveBgmSource;
            }

            bool canCrossfade = enableBgmCrossfade &&
                allowCrossfade &&
                fromSource != null &&
                fromSource.isPlaying &&
                targetSource != null &&
                fromSource != targetSource;

            if (canCrossfade)
            {
                StopOtherMainBgmSources(fromSource, targetSource);
                if (targetSource.isPlaying)
                {
                    targetSource.Stop();
                }

                SetupBgmSource(targetSource, clip, loop, 0f);
                targetSource.Play();

                bgmCrossfadeCoroutine = StartCoroutine(CrossfadeBgm(fromSource, targetSource, fromSource.volume, targetVolume, bgmCrossfadeDuration));

                if (activePlaying && targetSource == inactiveBgmSource)
                {
                    SwapActiveBgmSources();
                }
            }
            else
            {
                StopBgmLoopPlaybackImmediate();
                StopOtherMainBgmSources(null, targetSource);

                if (targetSource == null)
                {
                    targetSource = activeBgmSource;
                }

                SetupBgmSource(targetSource, clip, loop, targetVolume);
                targetSource.Play();

                if (targetSource == inactiveBgmSource)
                {
                    SwapActiveBgmSources();
                }
            }

            currentBgmEntryVolume = targetVolume;
            SetMixerVolume(bgmVolumeParam, bgmVolume);
            return activeBgmSource;
        }

        private void SetupBgmSource(AudioSource source, AudioClip clip, bool loop, float volume)
        {
            if (source == null)
            {
                return;
            }

            source.clip = clip;
            source.loop = loop;
            source.volume = Mathf.Clamp01(volume);
        }

        private void SwapActiveBgmSources()
        {
            var temp = activeBgmSource;
            activeBgmSource = inactiveBgmSource;
            inactiveBgmSource = temp;
        }

        private IEnumerator CrossfadeBgm(AudioSource from, AudioSource to, float fromVolume, float toVolume, float duration)
        {
            float timer = 0f;
            float time = Mathf.Max(0.01f, duration);

            while (timer < time)
            {
                timer += Time.deltaTime;
                float t = timer / time;

                if (from != null)
                {
                    from.volume = Mathf.Lerp(fromVolume, 0f, t);
                }

                if (to != null)
                {
                    to.volume = Mathf.Lerp(0f, toVolume, t);
                }

                yield return null;
            }

            if (from != null)
            {
                from.Stop();
                from.volume = fromVolume;
            }

            if (to != null)
            {
                to.volume = toVolume;
            }

            bgmCrossfadeCoroutine = null;
        }

        public AudioSource GetPlayingBgmSource()
        {
            if (activeBgmSource != null && activeBgmSource.isPlaying)
            {
                return activeBgmSource;
            }

            if (inactiveBgmSource != null && inactiveBgmSource.isPlaying)
            {
                return inactiveBgmSource;
            }

            if (defaultBgmSource != null && defaultBgmSource.isPlaying)
            {
                return defaultBgmSource;
            }

            return null;
        }

        private AudioSource GetCurrentMainBgmSource()
        {
            if (activeBgmSource != null && activeBgmSource.isPlaying)
            {
                return activeBgmSource;
            }

            if (inactiveBgmSource != null && inactiveBgmSource.isPlaying)
            {
                return inactiveBgmSource;
            }

            if (defaultBgmSource != null && defaultBgmSource.isPlaying)
            {
                return defaultBgmSource;
            }

            return null;
        }

        private void StopOtherMainBgmSources(AudioSource keepA, AudioSource keepB)
        {
            if (activeBgmSource != null && activeBgmSource.isPlaying && activeBgmSource != keepA && activeBgmSource != keepB)
            {
                activeBgmSource.Stop();
            }

            if (inactiveBgmSource != null && inactiveBgmSource.isPlaying && inactiveBgmSource != keepA && inactiveBgmSource != keepB)
            {
                inactiveBgmSource.Stop();
            }

            if (defaultBgmSource != null && defaultBgmSource.isPlaying && defaultBgmSource != keepA && defaultBgmSource != keepB)
            {
                defaultBgmSource.Stop();
            }
        }

        // 将线性音量(0~1)转换为分贝并写入AudioMixer
        private void SetMixerVolume(string parameter, float linearVolume)
        {
            float dB = Mathf.Log10(Mathf.Clamp(linearVolume, 0.0001f, 1f)) * 20f;
            audioMixer.SetFloat(parameter, dB);
        }

        // 从AudioMixer读取分贝并转换为线性音量(0~1)
        private float GetMixerVolume(string parameter)
        {
            if (audioMixer.GetFloat(parameter, out float dB))
                return Mathf.Pow(10f, dB/20f);
            return 1f;
        }

        // 平滑过渡某个Mixer参数（线性音量）
        private IEnumerator FadeMixerVolume(string parameter, float from, float to, float duration)
        {
            float timer = 0f;
            while (timer < duration)
            {
                timer += Time.deltaTime;
                float t = timer / duration;
                float v = Mathf.Lerp(from, to, t);
                SetMixerVolume(parameter, v);
                yield return null;
            }
            SetMixerVolume(parameter, to);
        }

        // 淡出并停止BGM，停止后恢复到淡出前音量（方便下次播放）
        private IEnumerator FadeOutAndStopBGM(AudioSource source, float duration)
        {
            if (source == null)
            {
                yield break;
            }

            float startVol = source.volume;
            float timer = 0f;
            while (timer < duration)
            {
                timer += Time.deltaTime;
                float t = timer / duration;
                source.volume = Mathf.Lerp(startVol, 0f, t);
                yield return null;
            }

            source.Stop();
            source.volume = startVol; // 恢复音量（为下次播放准备）
        }

        // 等当前BGM结束后播放下一首（不进行交叉淡入淡出）
        private IEnumerator PlayNextBgmWhenFinished(AudioSource source, AudioClip nextClip, float nextVolume)
        {
            while (source != null && source.isPlaying)
            {
                yield return null;
            }

            bgmSequenceCoroutine = null;
            if (source == null || nextClip == null)
            {
                yield break;
            }

            SetupBgmSource(source, nextClip, true, Mathf.Clamp01(nextVolume));
            source.Play();
            currentBgmEntryVolume = Mathf.Clamp01(nextVolume);
            SetMixerVolume(bgmVolumeParam, bgmVolume);
        }

        // 等SFX引子播放完成后开始循环
        private IEnumerator PlaySfxLoopAfterIntro(float introDuration, AudioClip loopClip, float entryVolume)
        {
            yield return new WaitForSeconds(Mathf.Max(0.01f, introDuration));

            if (sfxSource == null || loopClip == null)
            {
                sfxSequenceCoroutine = null;
                yield break;
            }

            sfxSource.clip = loopClip;
            sfxSource.loop = true;
            sfxSource.volume = Mathf.Clamp01(entryVolume);
            sfxSource.Play();

            sfxSequenceCoroutine = null;
        }

        private void PlayOneShotOnSource(AudioSource source, AudioClip clip, float entryVolume)
        {
            if (source == null || clip == null)
            {
                return;
            }

            float baseVolume = Mathf.Max(0.0001f, source.volume);
            float scale = Mathf.Clamp01(entryVolume) / baseVolume;
            source.PlayOneShot(clip, scale);
        }

        // 取消序列播放（BGM串联播放）
        private void CancelBgmSequence()
        {
            if (bgmSequenceCoroutine == null)
            {
                return;
            }

            StopCoroutine(bgmSequenceCoroutine);
            bgmSequenceCoroutine = null;
        }

        private void StopBgmCrossfade()
        {
            if (bgmCrossfadeCoroutine == null)
            {
                return;
            }

            StopCoroutine(bgmCrossfadeCoroutine);
            bgmCrossfadeCoroutine = null;
        }

        private void StopDefaultOfType(AudioType type)
        {
            if (type == AudioType.BGM)
            {
                if (defaultBgmSource != null && defaultBgmSource.isPlaying)
                {
                    defaultBgmSource.Stop();
                }
            }
            else
            {
                if (defaultSfxSource != null && defaultSfxSource.isPlaying)
                {
                    defaultSfxSource.Stop();
                }
            }
        }

        // 取消SFX序列播放（引子->循环）
        private void StopSfxSequence()
        {
            if (sfxSequenceCoroutine == null)
            {
                return;
            }

            StopCoroutine(sfxSequenceCoroutine);
            sfxSequenceCoroutine = null;
        }

        private void StopBgmLoopPlaybackImmediate()
        {
            StopBgmCrossfade();
            CancelBgmSequence();

            if (bgmFadeCoroutine != null)
            {
                StopCoroutine(bgmFadeCoroutine);
                bgmFadeCoroutine = null;
            }

            if (activeBgmSource != null)
            {
                activeBgmSource.Stop();
            }

            if (inactiveBgmSource != null)
            {
                inactiveBgmSource.Stop();
            }

        }

        private void StopSfxLoopPlaybackImmediate()
        {
            StopSfxSequence();

            if (sfxSource != null)
            {
                sfxSource.Stop();
            }
        }

    }
}
