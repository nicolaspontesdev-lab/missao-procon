using System.Collections.Generic;
using UnityEngine;

namespace Procon
{
    /// <summary>
    /// Todo o audio do jogo e gerado por codigo: nenhum arquivo de som entra no
    /// projeto. Os clipes sao sintetizados uma vez e ficam em cache.
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
        AudioSource musicSource;
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

            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.loop = true;
            musicSource.volume = 0.32f;

            GameSession.LoadPreferences();
        }

        // ------------------------------------------------------------------ musica

        public void PlayMusic()
        {
            if (musicSource == null) return;
            if (musicSource.clip == null) musicSource.clip = BuildMusicLoop();
            musicSource.mute = !GameSession.MusicOn;
            if (!musicSource.isPlaying) musicSource.Play();
        }

        public void RefreshMusicState()
        {
            if (musicSource == null) return;
            musicSource.mute = !GameSession.MusicOn;
            if (GameSession.MusicOn && !musicSource.isPlaying) PlayMusic();
        }

        /// <summary>Loop de 64 passos, o mesmo padrao do jogo original.</summary>
        AudioClip BuildMusicLoop()
        {
            const float stepSeconds = 0.205f;
            var roots = new[] { 45, 41, 48, 43 };
            var melody = new[] { 0, 7, 12, 7, 3, 7, 10, 7, 0, 7, 12, 15, 12, 7, 3, 7 };
            var notes = new List<Note>();

            for (var step = 0; step < 64; step++)
            {
                var time = step * stepSeconds;
                var root = roots[step / 16];

                if (step % 4 == 0) notes.Add(MakeNote(root, time, 0.44f, 0.30f, Wave.Triangle));
                if (step % 2 == 0) notes.Add(MakeNote(root + 12 + melody[step % 16], time, 0.21f, 0.13f, Wave.Square));
                if (step % 8 == 4) notes.Add(MakeNote(root + 19, time, 0.30f, 0.11f, Wave.Triangle));
            }

            return Render("procon-music", notes, 64 * stepSeconds);
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

        static Note MakeNote(int midi, float start, float duration, float volume, Wave wave)
        {
            var frequency = 440f * Mathf.Pow(2f, (midi - 69) / 12f);
            return Tone(frequency, start, duration, volume, wave);
        }

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
