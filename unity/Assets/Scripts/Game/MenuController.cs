using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Procon
{
    /// <summary>Um dos botoes de dificuldade do menu.</summary>
    [System.Serializable]
    public class LevelOption
    {
        public LevelKey key;
        public Button button;
        public Image background;
        public TMP_Text title;
        public TMP_Text description;
    }

    /// <summary>
    /// Menu do jogo: titulo, escolha de dificuldade, instrucoes, creditos e saida.
    /// </summary>
    public class MenuController : MonoBehaviour
    {
        [Header("Dados")]
        public CaseDatabase database;

        [Header("Paineis")]
        public GameObject titlePanel;
        public GameObject levelPanel;
        public GameObject helpPanel;
        public GameObject creditsPanel;

        [Header("Botoes do titulo")]
        public Button playButton;
        public Button helpButton;
        public Button creditsButton;
        public Button quitButton;

        [Header("Dificuldade")]
        public LevelOption[] levelOptions = new LevelOption[3];
        public TMP_Text levelStatus;
        public Button startButton;
        public Button backFromLevelButton;

        [Header("Voltar")]
        public Button backFromHelpButton;
        public Button backFromCreditsButton;

        [Header("Audio")]
        public Button musicButton;
        public TMP_Text musicLabel;
        public Button sfxButton;
        public TMP_Text sfxLabel;

        [Header("Cenas")]
        public string gameSceneName = "01_Jogo";

        readonly List<GameObject> panels = new List<GameObject>();
        GameObject activePanel;

        void Start()
        {
            GameSession.LoadPreferences();
            GameSession.HasLevel = false;
            ChiptuneAudio.Instance.PlayMusic();

            panels.Clear();
            if (titlePanel != null) panels.Add(titlePanel);
            if (levelPanel != null) panels.Add(levelPanel);
            if (helpPanel != null) panels.Add(helpPanel);
            if (creditsPanel != null) panels.Add(creditsPanel);

            WireButtons();
            RefreshAudioLabels();
            ResetLevelSelection();
            ShowPanel(titlePanel);
        }

        void WireButtons()
        {
            Bind(playButton, () =>
            {
                ResetLevelSelection();
                ShowPanel(levelPanel);
            });
            Bind(helpButton, () => ShowPanel(helpPanel));
            Bind(creditsButton, () => ShowPanel(creditsPanel));
            Bind(quitButton, Quit);
            Bind(backFromLevelButton, () => ShowPanel(titlePanel));
            Bind(backFromHelpButton, () => ShowPanel(titlePanel));
            Bind(backFromCreditsButton, () => ShowPanel(titlePanel));
            Bind(startButton, StartMission);

            Bind(musicButton, () =>
            {
                GameSession.MusicOn = !GameSession.MusicOn;
                GameSession.SavePreferences();
                ChiptuneAudio.Instance.RefreshMusicState();
                RefreshAudioLabels();
            });

            Bind(sfxButton, () =>
            {
                GameSession.SfxOn = !GameSession.SfxOn;
                GameSession.SavePreferences();
                RefreshAudioLabels();
            });

            foreach (var option in levelOptions)
            {
                if (option == null || option.button == null) continue;
                var captured = option.key;
                option.button.onClick.RemoveAllListeners();
                option.button.onClick.AddListener(() => ChooseLevel(captured));
            }
        }

        static void Bind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                ChiptuneAudio.Instance.Play("select");
                action();
            });
        }

        void ShowPanel(GameObject panel)
        {
            activePanel = panel;
            foreach (var item in panels)
                if (item != null) item.SetActive(item == panel);
        }

        void RefreshAudioLabels()
        {
            if (musicLabel != null) musicLabel.text = GameSession.MusicOn ? "MÚSICA: LIGADA" : "MÚSICA: DESLIGADA";
            if (sfxLabel != null) sfxLabel.text = GameSession.SfxOn ? "EFEITOS: LIGADOS" : "EFEITOS: DESLIGADOS";
        }

        void ResetLevelSelection()
        {
            GameSession.HasLevel = false;
            if (startButton != null) startButton.interactable = false;
            if (levelStatus != null) levelStatus.text = "Escolha uma dificuldade para liberar a missão.";
            PaintLevelButtons();
        }

        void ChooseLevel(LevelKey key)
        {
            GameSession.Level = key;
            GameSession.HasLevel = true;

            if (startButton != null) startButton.interactable = true;

            if (levelStatus != null && database != null)
            {
                var level = database.Level(key);
                levelStatus.text =
                    level.label + " • " + level.basePoints + " pontos por acerto + bônus de sequência • " +
                    database.RoundSize(key) + " casos na rodada";
            }

            PaintLevelButtons();
            ChiptuneAudio.Instance.Play("select");
        }

        void PaintLevelButtons()
        {
            foreach (var option in levelOptions)
            {
                if (option == null || option.background == null) continue;
                var selected = GameSession.HasLevel && option.key == GameSession.Level;
                option.background.color = selected
                    ? Palette.Blue
                    : Palette.Mix(Palette.Ink2, Palette.Shadow, 0.35f);
                if (option.title != null) option.title.color = selected ? Palette.Yellow : Palette.White;
            }
        }

        void StartMission()
        {
            if (!GameSession.HasLevel) return;
            SceneManager.LoadScene(gameSceneName);
        }

        void Quit()
        {
            GameSession.SavePreferences();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (activePanel == titlePanel)
            {
                if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                    playButton?.onClick.Invoke();
                if (keyboard.escapeKey.wasPressedThisFrame) Quit();
                return;
            }

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                ChiptuneAudio.Instance.Play("select");
                ShowPanel(titlePanel);
                return;
            }

            if (activePanel != levelPanel) return;

            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame) ChooseLevel(LevelKey.Fundamental);
            if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame) ChooseLevel(LevelKey.Medio);
            if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame) ChooseLevel(LevelKey.Tecnico);
            if ((keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame) && GameSession.HasLevel)
            {
                ChiptuneAudio.Instance.Play("select");
                StartMission();
            }
        }
    }
}
