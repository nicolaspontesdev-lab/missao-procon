using UnityEngine;

namespace Procon
{
    /// <summary>
    /// O pouco que precisa atravessar a troca de cena: dificuldade escolhida
    /// e preferencias de audio.
    /// </summary>
    public static class GameSession
    {
        const string SfxPref = "procon.sfx";

        public static LevelKey Level { get; set; } = LevelKey.Fundamental;
        public static bool HasLevel { get; set; }
        public static bool SfxOn { get; set; } = true;

        static bool loaded;

        public static void LoadPreferences()
        {
            if (loaded) return;
            loaded = true;
            SfxOn = PlayerPrefs.GetInt(SfxPref, 1) == 1;
        }

        public static void SavePreferences()
        {
            PlayerPrefs.SetInt(SfxPref, SfxOn ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
