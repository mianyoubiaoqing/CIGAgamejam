using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

namespace Kaki
{
    public class BgmLowPassEffect : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour contextProvider;
        [SerializeField] private float enabledCutoff = 800f;
        [SerializeField] private float disabledCutoff = 22000f;

        private IBgmEffectContext effectContext;
        private Coroutine lowPassCoroutine;

        [TriggerAction]
        public void EnableBGMLowPass(float duration = 1f)
        {
            StartLowPassTransition(enabledCutoff, duration);
        }

        [TriggerAction]
        public void DisableBGMLowPass(float duration = 1f)
        {
            StartLowPassTransition(disabledCutoff, duration);
        }

        private void OnDisable()
        {
            if (lowPassCoroutine != null)
            {
                StopCoroutine(lowPassCoroutine);
                lowPassCoroutine = null;
            }
        }

        private void StartLowPassTransition(float targetCutoff, float duration)
        {
            var context = ResolveContext();
            if (context == null || context.Mixer == null)
            {
                return;
            }

            if (lowPassCoroutine != null)
            {
                StopCoroutine(lowPassCoroutine);
            }

            context.Mixer.GetFloat(context.BgmLowPassParameter, out float currentCutoff);
            lowPassCoroutine = StartCoroutine(SmoothSetFloat(context.Mixer, context.BgmLowPassParameter, currentCutoff, targetCutoff, duration));
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
            lowPassCoroutine = null;
        }
    }
}
