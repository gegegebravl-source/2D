using System.Collections.Generic;
using UnityEngine;
using EXFIL.Core;

namespace EXFIL.Audio
{
    /// <summary>Central audio: one shots with distance attenuation, ambience loops and volume groups.</summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Sources")]
        public AudioSource SfxSource;
        public AudioSource AmbienceSource;
        public AudioClip[] AmbienceClips;

        private readonly Dictionary<string, float> _lastPlayed = new Dictionary<string, float>();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Services.Register(this);
        }

        private void Start()
        {
            ApplyVolumes();
            if (AmbienceSource != null && AmbienceClips != null && AmbienceClips.Length > 0)
            {
                AmbienceSource.clip = AmbienceClips[0];
                AmbienceSource.loop = true;
                AmbienceSource.Play();
            }
        }

        public void ApplyVolumes()
        {
            GameSettings settings = GameSettings.Load();
            if (SfxSource != null) SfxSource.volume = settings.MasterVolume * settings.SfxVolume;
            if (AmbienceSource != null) AmbienceSource.volume = settings.MasterVolume * settings.MusicVolume;
        }

        public void Play(AudioClip clip, float volume = 1f, float minRepeatInterval = 0f)
        {
            if (clip == null || SfxSource == null) return;
            if (minRepeatInterval > 0f)
            {
                float last;
                if (_lastPlayed.TryGetValue(clip.name, out last) && Time.time - last < minRepeatInterval) return;
                _lastPlayed[clip.name] = Time.time;
            }
            SfxSource.PlayOneShot(clip, volume);
        }

        public void PlayAt(AudioClip clip, Vector3 position, float volume = 1f)
        {
            if (clip == null) return;
            AudioSource.PlayClipAtPoint(clip, position, volume * GameSettings.Load().SfxVolume);
        }

        public void SetAmbience(int index)
        {
            if (AmbienceSource == null || AmbienceClips == null) return;
            if (index < 0 || index >= AmbienceClips.Length) return;
            AmbienceSource.clip = AmbienceClips[index];
            AmbienceSource.Play();
        }
    }
}
