using System.Collections.Generic;
using UnityEngine;

namespace Procon
{
    /// <summary>
    /// Efeitos sonoros do jogo, sintetizados por codigo e guardados em cache.
    /// O jogo nao tem musica de fundo: a trilha foi retirada a pedido da equipe.
    /// </summary>
    public class ChiptuneAudio : MonoBehaviour
    {
        public enum Wave { Square, Triangle, Saw, Noise }

        struct Note
        {
            public float frequency;
            public float start;
            public float duration;
            public float volume;
            public Wave wave;
        }

        const int SampleRate = 44100;

        static ChiptuneAudio instance;

        AudioSource sfxSource;
        readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();

        public static ChiptuneAudio Instance
        {
            get
            {
                if (instance != null) return instance;
                var host = new GameObject("ChiptuneAudio");
                instance = host.AddComponent<ChiptuneAudio>();
                return instance;
            }
        }

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);

            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.volume = 0.7f;

            GameSession.LoadPreferences();
        }

        // ------------------------------------------------------------------ efeitos

        public void Play(string effect)
        {
            if (!GameSession.SfxOn || sfxSource == null) return;
            if (!cache.TryGetValue(effect, out var clip))
            {
                clip = BuildEffect(effect);
                cache[effect] = clip;
            }
            if (clip != null) sfxSource.PlayOneShot(clip);
        }

        AudioClip BuildEffect(string effect)
        {
            var notes = new List<Note>();
            var length = 0.4f;

            switch (effect)
            {
                case "select":
                    notes.Add(Tone(330f, 0f, 0.05f, 0.35f, Wave.Square));
                    notes.Add(Tone(440f, 0.05f, 0.05f, 0.35f, Wave.Square));
                    length = 0.16f;
                    break;
                case "correct":
                    notes.Add(Tone(523f, 0f, 0.10f, 0.40f, Wave.Square));
                    notes.Add(Tone(659f, 0.10f, 0.10f, 0.40f, Wave.Square));
                    notes.Add(Tone(784f, 0.20f, 0.18f, 0.45f, Wave.Square));
                    length = 0.45f;
                    break;
                case "wrong":
                    notes.Add(Tone(180f, 0f, 0.14f, 0.40f, Wave.Saw));
                    notes.Add(Tone(125f, 0.12f, 0.24f, 0.36f, Wave.Saw));
                    length = 0.42f;
                    break;
                case "finish":
                    notes.Add(Tone(392f, 0.00f, 0.18f, 0.38f, Wave.Square));
                    notes.Add(Tone(523f, 0.11f, 0.18f, 0.38f, Wave.Square));
                    notes.Add(Tone(659f, 0.22f, 0.18f, 0.38f, Wave.Square));
                    notes.Add(Tone(784f, 0.33f, 0.26f, 0.42f, Wave.Square));
                    length = 0.65f;
                    break;
                case "voice":
                    notes.Add(Tone(310f, 0f, 0.065f, 0.45f, Wave.Triangle));
                    notes.Add(Tone(390f, 0.075f, 0.065f, 0.45f, Wave.Triangle));
                    notes.Add(Tone(350f, 0.150f, 0.065f, 0.45f, Wave.Triangle));
                    length = 0.26f;
                    break;
                case "dossier":
                    notes.Add(Tone(523f, 0.00f, 0.10f, 0.42f, Wave.Triangle));
                    notes.Add(Tone(659f, 0.09f, 0.10f, 0.42f, Wave.Triangle));
                    notes.Add(Tone(784f, 0.18f, 0.10f, 0.42f, Wave.Triangle));
                    notes.Add(Tone(1047f, 0.27f, 0.16f, 0.45f, Wave.Triangle));
                    length = 0.50f;
                    break;
                case "arrival":
                    notes.Add(Tone(262f, 0f, 0.12f, 0.42f, Wave.Triangle));
                    notes.Add(Tone(392f, 0.13f, 0.20f, 0.38f, Wave.Triangle));
                    length = 0.38f;
                    break;
                case "paper":
                    notes.Add(Tone(2200f, 0f, 0.13f, 0.30f, Wave.Noise));
                    notes.Add(Tone(640f, 0.10f, 0.06f, 0.22f, Wave.Triangle));
                    length = 0.22f;
                    break;
                default:
                    return null;
            }

            return Render("procon-" + effect, notes, length);
        }

        // ------------------------------------------------------------------ sintese

        static Note Tone(float frequency, float start, float duration, float volume, Wave wave)
        {
            return new Note
            {
                frequency = frequency,
                start = start,
                duration = duration,
                volume = volume,
                wave = wave
            };
        }

        static AudioClip Render(string name, List<Note> notes, float seconds)
        {
            var total = Mathf.Max(1, Mathf.CeilToInt(seconds * SampleRate));
            var data = new float[total];
            var noise = new System.Random(7);

            foreach (var note in notes)
            {
                var from = Mathf.Clamp(Mathf.RoundToInt(note.start * SampleRate), 0, total);
                var length = Mathf.RoundToInt(note.duration * SampleRate);

                for (var i = 0; i < length; i++)
                {
                    var at = from + i;
                    if (at >= total) break;

                    var t = (float)i / SampleRate;
                    var attack = Mathf.Clamp01(t * 400f);
                    var decay = Mathf.Exp(-t * (note.wave == Wave.Noise ? 22f : 5.5f));
                    var sample = Sample(note.wave, note.frequency, t, noise);
                    data[at] += sample * note.volume * attack * decay;
                }
            }

            for (var i = 0; i < total; i++) data[i] = Mathf.Clamp(data[i], -1f, 1f);

            var clip = AudioClip.Create(name, total, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float Sample(Wave wave, float frequency, float time, System.Random noise)
        {
            var phase = time * frequency;
            switch (wave)
            {
                case Wave.Square:
                    return Mathf.Repeat(phase, 1f) < 0.5f ? 1f : -1f;
                case Wave.Triangle:
                    return 4f * Mathf.Abs(Mathf.Repeat(phase, 1f) - 0.5f) - 1f;
                case Wave.Saw:
                    return 2f * Mathf.Repeat(phase, 1f) - 1f;
                case Wave.Noise:
                    return (float)(noise.NextDouble() * 2.0 - 1.0);
                default:
                    return 0f;
            }
        }
    }
}
