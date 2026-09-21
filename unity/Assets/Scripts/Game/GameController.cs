using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Procon
{
    /// <summary>Uma das tres pistas do dossie, com o botao e a ficha que ele abre.</summary>
    [System.Serializable]
    public class ClueSlot
    {
        public Button button;
        public Image icon;
        public TMP_Text label;
        public TMP_Text hotkey;
        public GameObject card;
        public TMP_Text cardTitle;
        public TMP_Text cardBody;
    }

    /// <summary>
    /// A rodada inteira: rota pelos locais, dossie de investigacao, veredito de
    /// infracao, pontuacao e tela final.
    /// </summary>
    public class GameController : MonoBehaviour
    {
        [Header("Dados")]
        public CaseDatabase database;

        [Header("Topo")]
        public TMP_Text levelBadge;
        public TMP_Text locationBadge;
        public TMP_Text caseCounter;
        public TMP_Text scoreValue;
        public TMP_Text correctValue;
        public TMP_Text wrongValue;
        public TMP_Text comboBadge;
        public RectTransform progressFill;
        public RectTransform routeBar;
        public TMP_Text routeStopTemplate;

        [Header("Cena (blocagem)")]
        public Image sceneWall;
        public Image sceneFloor;
        public Image[] sceneProps;
        public Image ownerBody;
        public Image inspectorBody;
        public TMP_Text speechPing;
        public TMP_Text decorLabel;
        public TMP_Text locationName;
        public TMP_Text locationSubtitle;
        public TMP_Text visitLine;

        [Header("Dialogo")]
        public TMP_Text caseTag;
        public TMP_Text speakerLabel;
        public TMP_Text questionText;
        public TMP_Text hintText;
        public TMP_Text instructionText;

        [Header("Investigacao")]
        public GameObject investigationPanel;
        public TMP_Text investigationStatus;
        public ClueSlot[] clues = new ClueSlot[3];

        [Header("Resposta")]
        public GameObject answerRow;
        public Button violationButton;
        public Image violationBackground;
        public Button lawfulButton;
        public Image lawfulBackground;

        [Header("Feedback")]
        public GameObject feedbackPanel;
        public Image feedbackStripe;
        public TMP_Text feedbackTitle;
        public TMP_Text feedbackPoints;
        public TMP_Text feedbackText;
        public TMP_Text feedbackReference;
        public Button nextButton;
        public TMP_Text nextButtonLabel;

        [Header("Resultado")]
        public GameObject finalPanel;
        public TMP_Text finalScore;
        public TMP_Text finalCorrect;
        public TMP_Text finalWrong;
        public TMP_Text finalPercent;
        public TMP_Text rankLabel;
        public TMP_Text finalSummary;
        public Button reviewButton;
        public TMP_Text reviewButtonLabel;
        public GameObject reviewPanel;
        public RectTransform reviewList;
        public TMP_Text reviewItemTemplate;
        public Button restartButton;
        public Button menuButton;

        [Header("Cenas")]
        public string menuSceneName = "00_Menu";

        [Header("Rolagem")]
        public ScrollRect dialogScroll;

        Round round;
        int index;
        int score;
        int correct;
        int wrong;
        int combo;
        bool answered;
        bool roundOver;
        readonly List<ProconCase> missed = new List<ProconCase>();
        readonly HashSet<int> collectedClues = new HashSet<int>();
        readonly List<GameObject> spawned = new List<GameObject>();

        ProconCase Current => round != null && index >= 0 && index < round.Total ? round.cases[index] : null;

        void Start()
        {
            if (database == null)
            {
                Debug.LogError("GameController sem banco de casos. Arraste o CaseDatabase para o campo Database.");
                enabled = false;
                return;
            }

            GameSession.LoadPreferences();
            ChiptuneAudio.Instance.PlayMusic();

            if (routeStopTemplate != null) routeStopTemplate.gameObject.SetActive(false);
            if (reviewItemTemplate != null) reviewItemTemplate.gameObject.SetActive(false);

            WireButtons();
            StartRound();
        }

        void WireButtons()
        {
            if (violationButton != null)
            {
                violationButton.onClick.RemoveAllListeners();
                violationButton.onClick.AddListener(() => Answer(true));
            }
            if (lawfulButton != null)
            {
                lawfulButton.onClick.RemoveAllListeners();
                lawfulButton.onClick.AddListener(() => Answer(false));
            }
            if (nextButton != null)
            {
                nextButton.onClick.RemoveAllListeners();
                nextButton.onClick.AddListener(NextCase);
            }
            if (reviewButton != null)
            {
                reviewButton.onClick.RemoveAllListeners();
                reviewButton.onClick.AddListener(ToggleReview);
            }
            if (restartButton != null)
            {
                restartButton.onClick.RemoveAllListeners();
                restartButton.onClick.AddListener(() =>
                {
                    ChiptuneAudio.Instance.Play("select");
                    StartRound();
                });
            }
            if (menuButton != null)
            {
                menuButton.onClick.RemoveAllListeners();
                menuButton.onClick.AddListener(BackToMenu);
            }

            for (var i = 0; i < clues.Length; i++)
            {
                var slot = clues[i];
                if (slot == null || slot.button == null) continue;
                var captured = i;
                slot.button.onClick.RemoveAllListeners();
                slot.button.onClick.AddListener(() => RevealClue(captured));
            }
        }

        // ------------------------------------------------------------------ rodada

        void StartRound()
        {
            round = RoundBuilder.Build(database, GameSession.Level);

            if (round.Total == 0)
            {
                Debug.LogError("O banco nao tem nenhum caso para o nivel " + GameSession.Level + ".");
                enabled = false;
                return;
            }

            index = 0;
            score = 0;
            correct = 0;
            wrong = 0;
            combo = 0;
            answered = false;
            roundOver = false;
            missed.Clear();

            if (finalPanel != null) finalPanel.SetActive(false);
            if (reviewPanel != null) reviewPanel.SetActive(false);

            BuildRouteBar();
            RenderCase();
        }

        void BuildRouteBar()
        {
            foreach (var item in spawned)
                if (item != null) Destroy(item);
            spawned.Clear();

            if (routeBar == null || routeStopTemplate == null) return;

            for (var i = 0; i < round.StopCount; i++)
            {
                var stop = Instantiate(routeStopTemplate, routeBar);
                stop.gameObject.SetActive(true);
                stop.name = "RouteStop" + i;
                spawned.Add(stop.gameObject);
            }
        }

        void PaintRouteBar(int activeStop)
        {
            for (var i = 0; i < spawned.Count; i++)
            {
                var label = spawned[i] != null ? spawned[i].GetComponent<TMP_Text>() : null;
                if (label == null) continue;

                var scenario = database.Scenario(round.stops[i].scenario);
                label.text = (i + 1).ToString("00") + " " + scenario.label;

                if (i < activeStop) label.color = Palette.Green;
                else if (i == activeStop) label.color = Palette.Yellow;
                else label.color = Palette.Alpha(Palette.White, 0.45f);
            }
        }

        // ------------------------------------------------------------------ um caso

        void RenderCase()
        {
            var current = Current;
            if (current == null) return;

            var scenario = database.Scenario(current.scenario);
            var level = database.Level(GameSession.Level);
            var stopIndex = round.stopOfCase[index];
            var positionInStop = round.positionInStop[index];
            var stopSize = round.stops[stopIndex].cases.Count;
            var isNewVisit = positionInStop == 0;

            answered = false;
            collectedClues.Clear();

            SetText(levelBadge, level.label);
            SetText(locationBadge, scenario.label);
            SetText(caseCounter, (index + 1) + "/" + round.Total);
            SetText(scoreValue, score.ToString("0000"));
            SetText(correctValue, correct.ToString());
            SetText(wrongValue, wrong.ToString());
            SetText(comboBadge, "COMBO x" + combo);
            if (comboBadge != null) comboBadge.color = combo >= 2 ? Palette.Yellow : Palette.Alpha(Palette.White, 0.35f);
            if (progressFill != null)
                progressFill.anchorMax = new Vector2((float)(index + 1) / round.Total, 1f);

            PaintRouteBar(stopIndex);

            SetText(locationName, scenario.label);
            SetText(locationSubtitle, scenario.subtitle);
            SetText(decorLabel, scenario.decor);
            SetText(visitLine, "VISITA " + (stopIndex + 1) + " DE " + round.StopCount);
            PaintScene(scenario);

            SetText(caseTag, scenario.label + " • CASO " + (positionInStop + 1) + "/" + stopSize);
            SetText(speakerLabel, "RESPOSTA DO RESPONSÁVEL • " + current.owner);
            SetText(questionText, current.text);
            SetText(speechPing, "?");

            var showHint = level.showHints && !string.IsNullOrWhiteSpace(current.hint);
            if (hintText != null)
            {
                hintText.gameObject.SetActive(showHint);
                hintText.text = showHint ? "PISTA: " + current.hint : "";
            }

            if (feedbackPanel != null) feedbackPanel.SetActive(false);
            if (answerRow != null) answerRow.SetActive(true);
            PaintAnswerButtons(null, null);

            RenderInvestigation(current);

            if (isNewVisit) ChiptuneAudio.Instance.Play("arrival");
            ScrollDialogToTop();
        }

        void PaintScene(ScenarioDef scenario)
        {
            if (sceneWall != null) sceneWall.color = Palette.Mix(Palette.Ink2, scenario.tint, 0.22f);
            if (sceneFloor != null) sceneFloor.color = Palette.Mix(Palette.Ink, scenario.tint, 0.12f);
            if (ownerBody != null) ownerBody.color = scenario.tint;
            if (inspectorBody != null) inspectorBody.color = Palette.Blue;

            if (sceneProps == null) return;
            for (var i = 0; i < sceneProps.Length; i++)
            {
                if (sceneProps[i] == null) continue;
                var t = sceneProps.Length <= 1 ? 0f : (float)i / (sceneProps.Length - 1);
                sceneProps[i].color = Palette.Mix(scenario.tint, Palette.Shadow, 0.20f + t * 0.35f);
            }
        }

        void RenderInvestigation(ProconCase current)
        {
            var hasDossier = current.HasInvestigation;

            if (investigationPanel != null) investigationPanel.SetActive(hasDossier);

            for (var i = 0; i < clues.Length; i++)
            {
                var slot = clues[i];
                if (slot == null) continue;
                if (slot.card != null) slot.card.SetActive(false);
                if (slot.button != null) slot.button.interactable = hasDossier;
                if (slot.hotkey != null) slot.hotkey.text = "TECLA " + (i + 1) + " • EXAMINAR";
                if (slot.icon != null) slot.icon.color = Palette.Alpha(Palette.Cyan, 0.55f);
            }

            if (!hasDossier)
            {
                SetInstruction("HÁ INFRAÇÃO NESTA SITUAÇÃO?");
                SetAnswerEnabled(true);
                return;
            }

            var dossier = current.investigation;
            var titles = new[] { "Ouvir o consumidor", dossier.documentTitle, dossier.inspectionTitle };
            var bodies = new[] { dossier.consumerStatement, dossier.documentBody, dossier.inspectionBody };

            for (var i = 0; i < clues.Length && i < titles.Length; i++)
            {
                var slot = clues[i];
                if (slot == null) continue;
                SetText(slot.label, titles[i]);
                SetText(slot.cardTitle, titles[i]);
                SetText(slot.cardBody, bodies[i]);
            }

            SetText(speakerLabel, "DOSSIÊ • INVESTIGAÇÃO EM CAMPO");
            SetText(questionText, dossier.briefing);
            if (hintText != null) hintText.gameObject.SetActive(false);

            UpdateInvestigationStatus();
            SetInstruction("REÚNA AS TRÊS PISTAS PARA DECIDIR.");
            SetAnswerEnabled(false);
        }

        void RevealClue(int clueIndex)
        {
            if (answered || roundOver) return;
            var current = Current;
            if (current == null || !current.HasInvestigation) return;
            if (clueIndex < 0 || clueIndex >= clues.Length) return;

            var slot = clues[clueIndex];
            if (slot == null) return;

            var isNew = collectedClues.Add(clueIndex);
            if (slot.card != null) slot.card.SetActive(true);
            if (slot.hotkey != null) slot.hotkey.text = "TECLA " + (clueIndex + 1) + " • CONFERIDO";
            if (slot.icon != null) slot.icon.color = Palette.Green;

            var ready = InvestigationReady();
            UpdateInvestigationStatus();
            SetAnswerEnabled(ready);

            if (ready)
            {
                SetInstruction("COM BASE NAS EVIDÊNCIAS, HÁ INFRAÇÃO?");
                var level = database.Level(GameSession.Level);
                if (hintText != null && level.showHints && !string.IsNullOrWhiteSpace(current.hint))
                {
                    hintText.gameObject.SetActive(true);
                    hintText.text = "PISTA: " + current.hint;
                }
            }

            if (isNew) ChiptuneAudio.Instance.Play(ready ? "dossier" : clueIndex == 0 ? "voice" : "paper");
        }

        bool InvestigationReady()
        {
            var current = Current;
            if (current == null) return false;
            return !current.HasInvestigation || collectedClues.Count >= 3;
        }

        void UpdateInvestigationStatus()
        {
            if (investigationStatus == null) return;
            var ready = InvestigationReady();
            investigationStatus.text =
                (ready ? "DOSSIÊ COMPLETO" : "ARQUIVO ABERTO") +
                " • " + collectedClues.Count + "/3 EVIDÊNCIAS" +
                (ready ? " • EMITIR PARECER" : " • USE 1, 2 E 3");
            investigationStatus.color = ready ? Palette.Green : Palette.Cyan;
        }

        // ------------------------------------------------------------------ veredito

        void Answer(bool playerSaysViolation)
        {
            if (answered || roundOver) return;
            var current = Current;
            if (current == null || !InvestigationReady()) return;

            answered = true;
            SetAnswerEnabled(false);
            foreach (var slot in clues)
                if (slot != null && slot.button != null) slot.button.interactable = false;

            var isCorrect = playerSaysViolation == current.isViolation;
            PaintAnswerButtons(current.isViolation, playerSaysViolation);

            var earned = 0;
            if (isCorrect)
            {
                combo++;
                correct++;
                earned = database.Level(GameSession.Level).basePoints + Scoring.ComboBonus(combo);
                score += earned;
            }
            else
            {
                combo = 0;
                wrong++;
                missed.Add(current);
            }

            SetText(feedbackTitle, isCorrect ? "✓ CORRETO!" : "✕ NÃO FOI DESSA VEZ");
            SetText(feedbackPoints, "+" + earned + " PONTOS");
            SetText(feedbackText, current.explanation);
            SetText(feedbackReference, "BASE EDUCATIVA: " + current.legalReference);
            if (feedbackTitle != null) feedbackTitle.color = isCorrect ? Palette.Green : Palette.Red;
            if (feedbackStripe != null) feedbackStripe.color = isCorrect ? Palette.GreenDark : Palette.RedDark;
            if (feedbackPanel != null) feedbackPanel.SetActive(true);

            SetText(speechPing, isCorrect ? "!" : "...");
            if (ownerBody != null) ownerBody.color = isCorrect ? Palette.Green : Palette.Red;

            SetText(scoreValue, score.ToString("0000"));
            SetText(correctValue, correct.ToString());
            SetText(wrongValue, wrong.ToString());
            SetText(comboBadge, "COMBO x" + combo);
            if (comboBadge != null) comboBadge.color = combo >= 2 ? Palette.Yellow : Palette.Alpha(Palette.White, 0.35f);

            SetInstruction("RESPOSTA REGISTRADA • CONFIRA A EXPLICAÇÃO");

            var isLast = index >= round.Total - 1;
            var endsVisit = !isLast && round.positionInStop[index + 1] == 0;
            SetText(nextButtonLabel, isLast ? "VER RESULTADO FINAL" : endsVisit ? "IR PARA O PRÓXIMO LOCAL" : "PRÓXIMA PERGUNTA");

            ChiptuneAudio.Instance.Play(isCorrect ? "correct" : "wrong");
        }

        void PaintAnswerButtons(bool? correctAnswer, bool? playerAnswer)
        {
            if (violationBackground != null)
                violationBackground.color = Palette.Mix(Palette.Red, Palette.Shadow, 0.25f);
            if (lawfulBackground != null)
                lawfulBackground.color = Palette.Mix(Palette.Green, Palette.Shadow, 0.25f);

            if (!correctAnswer.HasValue) return;

            var rightButton = correctAnswer.Value ? violationBackground : lawfulBackground;
            if (rightButton != null) rightButton.color = Palette.Green;

            if (playerAnswer.HasValue && playerAnswer.Value != correctAnswer.Value)
            {
                var wrongButton = playerAnswer.Value ? violationBackground : lawfulBackground;
                if (wrongButton != null) wrongButton.color = Palette.Red;
            }
        }

        void NextCase()
        {
            if (!answered || roundOver) return;
            ChiptuneAudio.Instance.Play("select");

            if (index >= round.Total - 1)
            {
                ShowFinal();
                return;
            }

            index++;
            RenderCase();
        }

        // ------------------------------------------------------------------ final

        void ShowFinal()
        {
            roundOver = true;
            var percent = round.Total == 0 ? 0 : Mathf.RoundToInt(100f * correct / round.Total);

            SetText(finalScore, score.ToString("0000"));
            SetText(finalCorrect, correct.ToString());
            SetText(finalWrong, wrong.ToString());
            SetText(finalPercent, percent + "%");
            SetText(rankLabel, Scoring.Rank(correct, round.Total));
            SetText(finalSummary,
                "Nível: " + database.Level(GameSession.Level).label +
                " • " + round.StopCount + " locais visitados" +
                " • " + round.Total + " respostas analisadas");

            PaintRouteBar(round.StopCount);
            RenderReview();

            if (finalPanel != null) finalPanel.SetActive(true);
            ChiptuneAudio.Instance.Play("finish");
        }

        void RenderReview()
        {
            if (reviewList == null || reviewItemTemplate == null) return;

            for (var i = reviewList.childCount - 1; i >= 0; i--)
            {
                var child = reviewList.GetChild(i);
                if (child == reviewItemTemplate.transform) continue;
                Destroy(child.gameObject);
            }

            if (reviewPanel != null) reviewPanel.SetActive(false);
            SetText(reviewButtonLabel, "REVISAR ERROS");
            if (reviewButton != null) reviewButton.gameObject.SetActive(missed.Count > 0);

            for (var i = 0; i < missed.Count; i++)
            {
                var item = Instantiate(reviewItemTemplate, reviewList);
                item.gameObject.SetActive(true);
                item.name = "ReviewItem" + i;
                var scenario = database.Scenario(missed[i].scenario);
                item.text =
                    "<b>" + (i + 1) + ". " + scenario.label + "</b>\n" +
                    missed[i].text + "\n" +
                    "<color=#31B86B>" + missed[i].explanation + "</color>\n" +
                    "<size=80%><color=#FFD84A>" + missed[i].legalReference + "</color></size>";
            }
        }

        void ToggleReview()
        {
            if (reviewPanel == null) return;
            var willOpen = !reviewPanel.activeSelf;
            reviewPanel.SetActive(willOpen);
            SetText(reviewButtonLabel, willOpen ? "FECHAR REVISÃO" : "REVISAR ERROS");
            ChiptuneAudio.Instance.Play("select");
        }

        void BackToMenu()
        {
            ChiptuneAudio.Instance.Play("select");
            GameSession.HasLevel = false;
            SceneManager.LoadScene(menuSceneName);
        }

        // ------------------------------------------------------------------ teclado

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                BackToMenu();
                return;
            }

            if (roundOver)
            {
                if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                {
                    ChiptuneAudio.Instance.Play("select");
                    StartRound();
                }
                if (keyboard.rKey.wasPressedThisFrame) ToggleReview();
                return;
            }

            if (answered)
            {
                if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame) NextCase();
                return;
            }

            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame) RevealClue(0);
            if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame) RevealClue(1);
            if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame) RevealClue(2);
            if (keyboard.vKey.wasPressedThisFrame) Answer(true);
            if (keyboard.fKey.wasPressedThisFrame) Answer(false);
        }

        // ------------------------------------------------------------------ utilidades

        void SetAnswerEnabled(bool enabledNow)
        {
            if (violationButton != null) violationButton.interactable = enabledNow;
            if (lawfulButton != null) lawfulButton.interactable = enabledNow;
        }

        void SetInstruction(string value)
        {
            SetText(instructionText, value);
        }

        void ScrollDialogToTop()
        {
            if (dialogScroll == null) return;
            Canvas.ForceUpdateCanvases();
            dialogScroll.verticalNormalizedPosition = 1f;
        }

        static void SetText(TMP_Text target, string value)
        {
            if (target != null) target.text = value;
        }
    }
}
