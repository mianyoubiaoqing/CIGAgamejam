using UnityEngine;
using UnityEngine.Audio;

namespace Kaki
{
    public interface IBgmEffectContext
    {
        AudioMixer Mixer { get; }
        string BgmLowPassParameter { get; }
        string BgmHighPassParameter { get; }
        AudioSource GetPlayingBgmSource();
    }
}
