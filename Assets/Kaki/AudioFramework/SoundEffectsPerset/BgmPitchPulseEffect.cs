using System.Collections;
using UnityEngine;

namespace Kaki
{
    public class BgmPitchPulseEffect : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour contextProvider;

        private IBgmEffectContext effectContext;
        private Coroutine pitchCoroutine;
        private AudioSource trackedSource;
        private AudioClip trackedClip;
        private float basePitch = 1f;

        [TriggerAction]
        public void PulseBGMPitch(float targetPitch = 0.8f, float toDuration = 0.2f, float backDuration = 0.2f)
        {
            var context = ResolveContext();
            if (context == null)
            {
                return;
            }

            var source = context.GetPlayingBgmSource();
            if (source == null)
            {
                return;
            }

            if (pitchCoroutine != null)
            {
                StopCoroutine(pitchCoroutine);
            }

            float originalPitch = GetBasePitch(source);
            float clampedTargetPitch = Mathf.Clamp(targetPitch, 0.1f, 3f);
            pitchCoroutine = StartCoroutine(PulsePitch(source, originalPitch, clampedTargetPitch, Mathf.Max(0f, toDuration), Mathf.Max(0f, backDuration)));
        }

        private void OnDisable()
        {
            if (pitchCoroutine != null)
            {
                StopCoroutine(pitchCoroutine);
                pitchCoroutine = null;
            }

            trackedSource = null;
            trackedClip = null;
            basePitch = 1f;
        }

        private IBgmEffectContext ResolveContext()
        {
            if (effectContext != null)
            {
                return effectContext;
            }

            effectContext = contextProvider as IBgmEffectContext;
            if (effectContext != null)
            {
                return effectContext;
            }

            effectContext = AudioManager.Instance;
            return effectContext;
        }

        private float GetBasePitch(AudioSource source)
        {
            if (source == null)
            {
                return 1f;
            }

            if (trackedSource != source || trackedClip != source.clip)
            {
                trackedSource = source;
                trackedClip = source.clip;
                basePitch = source.pitch;
            }

            return basePitch;
        }

        private IEnumerator PulsePitch(AudioSource source, float originalPitch, float targetPitch, float toDuration, float backDuration)
        {
            if (toDuration > 0f)
            {
                yield return LerpPitch(source, originalPitch, targetPitch, toDuration);
            }
            else if (source != null)
            {
                source.pitch = targetPitch;
            }

            if (backDuration > 0f)
            {
                yield return LerpPitch(source, targetPitch, originalPitch, backDuration);
            }
            else if (source != null)
            {
                source.pitch = originalPitch;
            }

            pitchCoroutine = null;
        }

        private IEnumerator LerpPitch(AudioSource source, float from, float to, float duration)
        {
            if (source == null)
            {
                yield break;
            }

            float timer = 0f;
            float totalDuration = Mathf.Max(0.0001f, duration);
            while (timer < totalDuration)
            {
                if (source == null)
                {
                    yield break;
                }

                timer += Time.deltaTime;
                float t = timer / totalDuration;
                source.pitch = Mathf.Lerp(from, to, t);
                yield return null;
            }

            if (source != null)
            {
                source.pitch = to;
            }
        }
    }
}
