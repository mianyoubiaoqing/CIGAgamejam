using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

namespace Kaki
{
    public class BgmHighPassPulseEffect : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour contextProvider;
        [SerializeField] private float enabledCutoff = 800f;
        [SerializeField] private float disabledCutoff = 10f;

        private IBgmEffectContext effectContext;
        private Coroutine highPassCoroutine;

        [TriggerAction]
        public void EnableBGMHighPass(float duration = 1f)
        {
            StartHighPassTransition(enabledCutoff, duration);
        }

        [TriggerAction]
        public void DisableBGMHighPass(float duration = 1f)
        {
            StartHighPassTransition(disabledCutoff, duration);
        }

        private void OnDisable()
        {
            if (highPassCoroutine != null)
            {
                StopCoroutine(highPassCoroutine);
                highPassCoroutine = null;
            }
        }

        private void StartHighPassTransition(float targetCutoff, float duration)
        {
            var context = ResolveContext();
            if (context == null || context.Mixer == null)
            {
                return;
            }

            if (highPassCoroutine != null)
            {
                StopCoroutine(highPassCoroutine);
            }

            context.Mixer.GetFloat(context.BgmHighPassParameter, out float currentCutoff);
            float clampedTarget = Mathf.Clamp(targetCutoff, 10f, 22000f);
            highPassCoroutine = StartCoroutine(SmoothSetFloat(context.Mixer, context.BgmHighPassParameter, currentCutoff, clampedTarget, duration));
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

        private IEnumerator PulseHighPass(AudioMixer mixer, string parameter, float from, float to, float toDuration, float backDuration)
        {
            if (toDuration > 0f)
            {
                yield return SmoothSetFloat(mixer, parameter, from, to, toDuration);
            }
            else
            {
                mixer.SetFloat(parameter, to);
            }

            if (backDuration > 0f)
            {
                yield return SmoothSetFloat(mixer, parameter, to, from, backDuration);
            }
            else
            {
                mixer.SetFloat(parameter, from);
            }

            highPassCoroutine = null;
        }

        private IEnumerator SmoothSetFloat(AudioMixer mixer, string parameter, float from, float to, float duration)
        {
            float timer = 0f;
            float totalDuration = Mathf.Max(0.0001f, duration);

            while (timer < totalDuration)
            {
                timer += Time.deltaTime;
                float t = timer / totalDuration;
                float value = Mathf.Lerp(from, to, t);
                mixer.SetFloat(parameter, value);
                yield return null;
            }

            mixer.SetFloat(parameter, to);
            highPassCoroutine = null;
        }
    }
}
