using UnityEngine;

namespace StarFall
{
    public sealed class GameAudio : MonoBehaviour
    {
        private AudioSource source;
        private AudioClip shot, hurt, victory, ultimate;
        public float Volume { get; private set; }

        private void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0;
            shot = Tone("Shot", 660, 0.055f);
            hurt = Tone("Hurt", 130, 0.13f);
            victory = Tone("Victory", 880, 0.5f);
            ultimate = Tone("Ultimate", 220, 0.55f);
            SetVolume(PlayerPrefs.GetFloat("StarFall.Volume", 0.5f));
        }

        public void SetVolume(float value) { Volume = Mathf.Clamp01(value); source.volume = Volume; }
        public void Save() { PlayerPrefs.SetFloat("StarFall.Volume", Volume); PlayerPrefs.Save(); }
        public void PlayShot() => source.PlayOneShot(shot, 0.13f);
        public void PlayHurt() => source.PlayOneShot(hurt, 0.3f);
        public void PlayVictory() => source.PlayOneShot(victory, 0.25f);
        public void PlayUltimate() => source.PlayOneShot(ultimate, 0.42f);

        private static AudioClip Tone(string name, float frequency, float duration)
        {
            const int sampleRate = 22050;
            var samples = new float[Mathf.CeilToInt(sampleRate * duration)];
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / samples.Length;
                samples[i] = Mathf.Sin(2 * Mathf.PI * frequency * i / sampleRate) * Mathf.Sin(Mathf.PI * t) * (1 - t);
            }
            var clip = AudioClip.Create(name, samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void OnDestroy()
        {
            if (shot != null) Destroy(shot);
            if (hurt != null) Destroy(hurt);
            if (victory != null) Destroy(victory);
            if (ultimate != null) Destroy(ultimate);
        }
    }
}
