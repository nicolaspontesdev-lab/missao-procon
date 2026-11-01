using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Procon.EditorTools
{
    /// <summary>
    /// Monta o projeto: importa os casos para um asset e gera as duas cenas do
    /// jogo. As cenas saem como cenas normais, com objetos editaveis.
    /// </summary>
    public static class ProconBuilder
    {
        const string JsonPath = "Assets/Editor/procon-cases.json";
        const string DatabasePath = "Assets/Data/CaseDatabase.asset";
        const string VisualBookPath = "Assets/Data/CaseVisualBook.asset";
        const string MenuScenePath = "Assets/Scenes/00_Menu.unity";
        const string GameScenePath = "Assets/Scenes/01_Jogo.unity";

        /// <summary>
        /// Avisa antes de reconstruir as cenas. Os palcos sao montados por codigo,
        /// entao regerar os recria iguais; o que se perde e edicao feita a mao
        /// dentro da cena.
        /// </summary>
        static bool ConfirmSceneRebuild()
        {
            return EditorUtility.DisplayDialog(
                "Regerar as cenas do Missao PROCON",
                "As cenas 00_Menu e 01_Jogo serao construidas do zero.\n\n" +
                "RECRIADO pelo codigo, identico: palcos, personagens, objetos, interface.\n\n" +
                "NAO E TOCADO: os 45 casos, o procon-cases.json e o CaseVisualBook.\n\n" +
                "PERDIDO: qualquer edicao feita a mao dentro das cenas.",
                "Regerar", "Cancelar");
        }

        [MenuItem("Missao PROCON/Gerar tudo (dados + cenas)", false, 0)]
        public static void BuildEverything()
        {
            if (!ConfirmSceneRebuild()) return;
            Regenerate();
        }

        /// <summary>
        /// Regera tudo sem perguntar. Fica fora do menu de proposito: e o caminho
        /// da automacao, nao o que se clica por engano.
        /// </summary>
        public static void Regenerate()
        {
            var database = ImportCases();
            if (database == null) return;
            var visualBook = EnsureVisualBook(database);

            BuildGameScene(database, visualBook);
            BuildMenuScene(database);
            RegisterScenes();
            ConfigurePlayer();
            EditorSceneManager.OpenScene(MenuScenePath);
            Debug.Log("Missao PROCON: dados e cenas gerados.");
        }

        /// <summary>
        /// Garante o caderno visual. Cria se nao existir e acrescenta apenas os
        /// casos que ainda nao tem entrada. Nunca apaga nem sobrescreve o que ja
        /// foi configurado.
        /// </summary>
        static CaseVisualBook EnsureVisualBook(CaseDatabase database)
        {
            Directory.CreateDirectory("Assets/Data");
            var book = AssetDatabase.LoadAssetAtPath<CaseVisualBook>(VisualBookPath);
            if (book == null)
            {
                book = ScriptableObject.CreateInstance<CaseVisualBook>();
                AssetDatabase.CreateAsset(book, VisualBookPath);
            }

            var added = 0;
            foreach (var item in database.cases)
            {
                if (item == null || string.IsNullOrEmpty(item.id)) continue;
                if (book.Has(item.id)) continue;
                book.entries.Add(new CaseVisual { caseId = item.id });
                added++;
            }

            if (added > 0)
            {
                EditorUtility.SetDirty(book);
                AssetDatabase.SaveAssets();
                Debug.Log("Missao PROCON: " + added + " caso(s) entraram no caderno visual; " +
                          (book.entries.Count - added) + " ja configurado(s) foram preservados.");
            }

            return book;
        }

        [MenuItem("Missao PROCON/1 - Importar casos do JSON", false, 20)]
        public static CaseDatabase ImportCases()
        {
            var text = AssetDatabase.LoadAssetAtPath<TextAsset>(JsonPath);
            if (text == null)
            {
                Debug.LogError("Nao achei " + JsonPath + ". O arquivo com os casos precisa estar ali.");
                return null;
            }

            var payload = JsonUtility.FromJson<CaseListDto>(text.text);
            if (payload == null || payload.items == null || payload.items.Length == 0)
            {
                Debug.LogError("O JSON de casos veio vazio ou em formato inesperado.");
                return null;
            }

            Directory.CreateDirectory("Assets/Data");
            var database = AssetDatabase.LoadAssetAtPath<CaseDatabase>(DatabasePath);
            if (database == null)
            {
                database = ScriptableObject.CreateInstance<CaseDatabase>();
                AssetDatabase.CreateAsset(database, DatabasePath);
            }

            database.casesPerScenario = 3;
            database.levels = DefaultLevels();
            database.scenarios = DefaultScenarios();
            database.cases = new List<ProconCase>();

            var withDossier = 0;
            foreach (var dto in payload.items)
            {
                var item = new ProconCase
                {
                    id = dto.id,
                    level = ParseLevel(dto.level),
                    scenario = ParseScenario(dto.scenario),
                    owner = dto.owner,
                    text = dto.text,
                    isViolation = dto.answer,
                    hint = dto.hint,
                    explanation = dto.explanation,
                    legalReference = dto.reference,
                    investigation = new Investigation()
                };

                if (dto.investigation != null && dto.investigation.Length >= 6)
                {
                    item.investigation.briefing = dto.investigation[0];
                    item.investigation.consumerStatement = dto.investigation[1];
                    item.investigation.documentTitle = dto.investigation[2];
                    item.investigation.documentBody = dto.investigation[3];
                    item.investigation.inspectionTitle = dto.investigation[4];
                    item.investigation.inspectionBody = dto.investigation[5];
                    withDossier++;
                }

                database.cases.Add(item);
            }

            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("Missao PROCON: " + database.cases.Count + " casos importados (" + withDossier + " com dossie).");
            return database;
        }

        [MenuItem("Missao PROCON/2 - Gerar cenas", false, 21)]
        public static void BuildScenesOnly()
        {
            var database = AssetDatabase.LoadAssetAtPath<CaseDatabase>(DatabasePath);
            if (database == null)
            {
                Debug.LogError("Importe os casos antes de gerar as cenas.");
                return;
            }
            if (!ConfirmSceneRebuild()) return;

            BuildGameScene(database, EnsureVisualBook(database));
            BuildMenuScene(database);
            RegisterScenes();
            EditorSceneManager.OpenScene(MenuScenePath);
        }

        // ================================================================== dados

        [Serializable]
        class CaseDto
        {
            public string id;
            public string level;
            public string scenario;
            public string owner;
            public string text;
            public bool answer;
            public string hint;
            public string explanation;
            public string reference;
            public string[] investigation;
        }

        [Serializable]
        class CaseListDto
        {
            public CaseDto[] items;
        }

        static LevelKey ParseLevel(string value)
        {
            switch (value)
            {
                case "medio": return LevelKey.Medio;
                case "tecnico": return LevelKey.Tecnico;
                default: return LevelKey.Fundamental;
            }
        }

        static ScenarioKey ParseScenario(string value)
        {
            switch (value)
            {
                case "eletronicos": return ScenarioKey.Eletronicos;
                case "banco": return ScenarioKey.Banco;
                case "online": return ScenarioKey.Online;
                case "telefonia": return ScenarioKey.Telefonia;
                default: return ScenarioKey.Supermercado;
            }
        }

        static List<LevelDef> DefaultLevels()
        {
            return new List<LevelDef>
            {
                new LevelDef
                {
                    key = LevelKey.Fundamental, label = "FÁCIL", basePoints = 100, showHints = true,
                    description = "Nível Fundamental II. Situações diretas, com uma pista extra antes da decisão."
                },
                new LevelDef
                {
                    key = LevelKey.Medio, label = "MÉDIO", basePoints = 125, showHints = false,
                    description = "Nível Ensino Médio. Casos com mais nuance e sem pista."
                },
                new LevelDef
                {
                    key = LevelKey.Tecnico, label = "DIFÍCIL", basePoints = 150, showHints = false,
                    description = "Nível Técnico / Superior. Linguagem técnica do CDC, sem pista."
                }
            };
        }

        static List<ScenarioDef> DefaultScenarios()
        {
            return new List<ScenarioDef>
            {
                new ScenarioDef { key = ScenarioKey.Supermercado, label = "SUPERMERCADO", subtitle = "FISCALIZAÇÃO DE PREÇOS", decor = "CORREDOR DE PRODUTOS", tint = Palette.Green },
                new ScenarioDef { key = ScenarioKey.Eletronicos, label = "ELETRÔNICOS", subtitle = "GARANTIA E ASSISTÊNCIA", decor = "MOSTRUÁRIO DA LOJA", tint = Palette.Cyan },
                new ScenarioDef { key = ScenarioKey.Banco, label = "BANCO", subtitle = "CRÉDITO RESPONSÁVEL", decor = "TERMINAIS BANCÁRIOS", tint = Palette.Yellow },
                new ScenarioDef { key = ScenarioKey.Online, label = "COMPRAS ONLINE", subtitle = "OFERTAS E ENTREGAS", decor = "CENTRO DE ENTREGAS", tint = Palette.Blue },
                new ScenarioDef { key = ScenarioKey.Telefonia, label = "TELEFONIA", subtitle = "PLANOS E SERVIÇOS", decor = "MOSTRUÁRIO DE PLANOS", tint = Palette.Orange }
            };
        }

        // ================================================================== base da cena

        static Transform NewScene(string title)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraNode = new GameObject("Main Camera");
            cameraNode.tag = "MainCamera";
            var camera = cameraNode.AddComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Palette.Ink;
            cameraNode.AddComponent<AudioListener>();

            var canvasNode = new GameObject("Canvas", typeof(RectTransform));
            var canvas = canvasNode.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasNode.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasNode.AddComponent<GraphicRaycaster>();

            var eventNode = new GameObject("EventSystem");
            eventNode.AddComponent<EventSystem>();
            eventNode.AddComponent<InputSystemUIInputModule>();

            var background = UiKit.Panel("Background", canvasNode.transform, Palette.Ink);
            UiKit.Full(UiKit.Rect(background.gameObject));

            var cabinet = UiKit.Panel("Cabinet", canvasNode.transform, Palette.Ink2);
            UiKit.Stretch(UiKit.Rect(cabinet.gameObject), 24, 24, 24, 24);
            cabinet.gameObject.name = title;

            return cabinet.transform;
        }

        static void RegisterScenes()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MenuScenePath, true),
                new EditorBuildSettingsScene(GameScenePath, true)
            };
        }

        static void ConfigurePlayer()
        {
            PlayerSettings.productName = "Show do PROCON";
            PlayerSettings.companyName = "Equipe PROCON";
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        }

        // ================================================================== cena do jogo

        static void BuildGameScene(CaseDatabase database, CaseVisualBook visualBook)
        {
            // estudio de TV em 3D; a interface 2D do jogo vai dentro do telao
            var rig = ShowBuilder.BuildStudioScene();
            var frame = rig.screen != null ? rig.screen : NewScene("Cabinet");

            var controllerNode = new GameObject("GameController");
            var controller = controllerNode.AddComponent<GameController>();
            controller.database = database;
            controller.visualBook = visualBook;
            controller.menuSceneName = "00_Menu";
            controller.presenter = rig.presenter;
            controller.presenterLine = rig.presenterLine;

            // ---- topo
            var topBar = UiKit.Panel("TopBar", frame, Palette.Shadow);
            UiKit.TopBand(UiKit.Rect(topBar.gameObject), 0, 84);

            var title = UiKit.Label("Title", topBar.transform, "SHOW DO PROCON", 36, Palette.Yellow, TextAlignmentOptions.Left);
            UiKit.Frac(UiKit.Rect(title.gameObject), 0.012f, 0f, 0.35f, 1f);

            controller.levelBadge = UiKit.Label("LevelBadge", topBar.transform, "NÍVEL", 24, Palette.Cyan, TextAlignmentOptions.Center);
            UiKit.Frac(UiKit.Rect(controller.levelBadge.gameObject), 0.36f, 0f, 0.64f, 1f);

            controller.locationBadge = UiKit.Label("LocationBadge", topBar.transform, "LOCAL", 24, Palette.White, TextAlignmentOptions.Right);
            UiKit.Frac(UiKit.Rect(controller.locationBadge.gameObject), 0.65f, 0f, 0.988f, 1f);

            // ---- placar
            var hud = UiKit.Panel("Hud", frame, Palette.Mix(Palette.Ink, Palette.Shadow, 0.45f));
            UiKit.TopBand(UiKit.Rect(hud.gameObject), 84, 76);
            UiKit.HList(hud.gameObject, 10, 10);

            controller.caseCounter = Stat(hud.transform, "CASO", "1/15", Palette.White);
            controller.scoreValue = Stat(hud.transform, "PONTOS", "0000", Palette.Yellow);
            controller.correctValue = Stat(hud.transform, "ACERTOS", "0", Palette.Green);
            controller.wrongValue = Stat(hud.transform, "ERROS", "0", Palette.Red);
            controller.comboBadge = Stat(hud.transform, "SEQUÊNCIA", "COMBO x0", Palette.Cyan);

            // ---- progresso
            var track = UiKit.Panel("ProgressTrack", frame, Palette.Shadow);
            UiKit.TopBand(UiKit.Rect(track.gameObject), 160, 12);
            var fill = UiKit.Panel("ProgressFill", track.transform, Palette.Cyan);
            var fillRect = UiKit.Rect(fill.gameObject);
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0.05f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            controller.progressFill = fillRect;

            // ---- rota
            var routeBar = UiKit.Node("RouteBar", frame);
            UiKit.TopBand(UiKit.Rect(routeBar), 172, 48, 8, 8);
            UiKit.HList(routeBar, 8, 6);
            controller.routeBar = UiKit.Rect(routeBar);
            controller.routeStopTemplate = UiKit.Label("RouteStopTemplate", routeBar.transform, "00 LOCAL", 19, Palette.White, TextAlignmentOptions.Center);

            // ---- corpo
            var body = UiKit.Node("Body", frame);
            UiKit.Stretch(UiKit.Rect(body), 16, 228, 16, 16);

            BuildSceneColumn(body.transform, controller);
            BuildDialogColumn(body.transform, controller);
            BuildFinalPanel(frame, controller);

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), GameScenePath);
        }

        static TextMeshProUGUI Stat(Transform parent, string caption, string value, Color color)
        {
            var node = UiKit.Node(caption, parent);
            var captionLabel = UiKit.Label("Caption", node.transform, caption, 16, Palette.Alpha(Palette.White, 0.5f), TextAlignmentOptions.Center);
            UiKit.Frac(UiKit.Rect(captionLabel.gameObject), 0f, 0.52f, 1f, 1f);
            var valueLabel = UiKit.Label("Value", node.transform, value, 26, color, TextAlignmentOptions.Center);
            UiKit.Frac(UiKit.Rect(valueLabel.gameObject), 0f, 0f, 1f, 0.55f);
            return valueLabel;
        }

        static void BuildSceneColumn(Transform parent, GameController controller)
        {
            var column = UiKit.Node("SceneColumn", parent);
            var columnRect = UiKit.Rect(column);
            columnRect.anchorMin = new Vector2(0.51f, 0f);
            columnRect.anchorMax = Vector2.one;
            columnRect.offsetMin = Vector2.zero;
            columnRect.offsetMax = Vector2.zero;

            var header = UiKit.Panel("LocationHeader", column.transform, Palette.Shadow);
            UiKit.TopBand(UiKit.Rect(header.gameObject), 0, 88);
            controller.locationName = UiKit.Label("LocationName", header.transform, "LOCAL", 30, Palette.Yellow, TextAlignmentOptions.Left);
            UiKit.Frac(UiKit.Rect(controller.locationName.gameObject), 0.04f, 0.45f, 0.98f, 0.95f);
            controller.locationSubtitle = UiKit.Label("LocationSubtitle", header.transform, "SUBTÍTULO", 19, Palette.Cyan, TextAlignmentOptions.Left);
            UiKit.Frac(UiKit.Rect(controller.locationSubtitle.gameObject), 0.04f, 0.06f, 0.98f, 0.46f);

            var box = UiKit.Panel("SceneBox", column.transform, Palette.Shadow);
            var boxRect = UiKit.Rect(box.gameObject);
            boxRect.anchorMin = Vector2.zero;
            boxRect.anchorMax = Vector2.one;
            boxRect.offsetMin = new Vector2(0f, 56f);
            boxRect.offsetMax = new Vector2(0f, -96f);

            // cinco palcos com a arte extraida do HTML; so o do caso atual fica ligado
            controller.stages = StageArt.BuildAll(box.transform, controller.database).ToArray();

            controller.visitLine = UiKit.Label("VisitLine", column.transform, "VISITA 1 DE 5", 20, Palette.Cyan, TextAlignmentOptions.Center);
            UiKit.BottomBand(UiKit.Rect(controller.visitLine.gameObject), 8, 44);
        }

        static void BuildDialogColumn(Transform parent, GameController controller)
        {
            var column = UiKit.Node("DialogColumn", parent);
            var columnRect = UiKit.Rect(column);
            columnRect.anchorMin = Vector2.zero;
            columnRect.anchorMax = new Vector2(0.49f, 1f);
            columnRect.offsetMin = Vector2.zero;
            columnRect.offsetMax = Vector2.zero;

            var scroll = UiKit.VScroll("DialogScroll", column.transform, Palette.Mix(Palette.Ink, Palette.Shadow, 0.35f), out var content);
            UiKit.Full(UiKit.Rect(scroll.gameObject));
            controller.dialogScroll = scroll;

            UiKit.VList(content.gameObject, 14, 20).childAlignment = TextAnchor.UpperLeft;
            UiKit.FitHeight(content.gameObject);

            controller.caseTag = UiKit.Label("CaseTag", content, "LOCAL • CASO 1/3", 19, Palette.Yellow);
            controller.speakerLabel = UiKit.Label("SpeakerLabel", content, "RESPOSTA DO RESPONSÁVEL", 21, Palette.Cyan);
            controller.questionText = UiKit.Label("QuestionText", content, "Texto do caso.", 28, Palette.White);
            controller.hintText = UiKit.Label("HintText", content, "PISTA:", 19, Palette.Orange);

            // ---- investigacao
            var investigation = UiKit.Node("InvestigationPanel", content);
            UiKit.VList(investigation, 10, 0).childAlignment = TextAnchor.UpperLeft;
            UiKit.FitHeight(investigation);
            controller.investigationPanel = investigation;

            controller.investigationStatus = UiKit.Label("InvestigationStatus", investigation.transform, "ARQUIVO ABERTO", 19, Palette.Cyan);

            var clueRow = UiKit.Node("ClueRow", investigation.transform);
            UiKit.HList(clueRow, 10, 0);
            UiKit.Height(clueRow, 104);

            var cards = UiKit.Node("EvidenceCards", investigation.transform);
            UiKit.VList(cards, 8, 0).childAlignment = TextAnchor.UpperLeft;
            UiKit.FitHeight(cards);

            controller.clues = new ClueSlot[3];
            for (var i = 0; i < 3; i++) controller.clues[i] = BuildClue(clueRow.transform, cards.transform, i);

            // ---- resposta
            controller.instructionText = UiKit.Label("InstructionText", content, "HÁ INFRAÇÃO NESTA SITUAÇÃO?", 23, Palette.Yellow);

            var answerRow = UiKit.Node("AnswerRow", content);
            UiKit.HList(answerRow, 14, 0);
            UiKit.Height(answerRow, 104);
            controller.answerRow = answerRow;

            var violation = UiKit.Button("ViolationButton", answerRow.transform, "É INFRAÇÃO\n<size=60%>tecla V</size>", Palette.Mix(Palette.Red, Palette.Shadow, 0.25f), Palette.White, 26);
            controller.violationButton = violation.button;
            controller.violationBackground = violation.background;

            var lawful = UiKit.Button("LawfulButton", answerRow.transform, "NÃO É INFRAÇÃO\n<size=60%>tecla F</size>", Palette.Mix(Palette.Green, Palette.Shadow, 0.25f), Palette.White, 26);
            controller.lawfulButton = lawful.button;
            controller.lawfulBackground = lawful.background;

            // ---- feedback
            var feedback = UiKit.Node("FeedbackPanel", content);
            var feedbackImage = feedback.AddComponent<Image>();
            feedbackImage.color = Palette.Mix(Palette.Ink2, Palette.Shadow, 0.35f);
            UiKit.VList(feedback, 8, 16).childAlignment = TextAnchor.UpperLeft;
            UiKit.FitHeight(feedback);
            controller.feedbackPanel = feedback;

            var stripe = UiKit.Panel("Stripe", feedback.transform, Palette.GreenDark);
            UiKit.Height(stripe.gameObject, 8);
            controller.feedbackStripe = stripe;

            controller.feedbackTitle = UiKit.Label("FeedbackTitle", feedback.transform, "CORRETO", 27, Palette.Green);
            controller.feedbackPoints = UiKit.Label("FeedbackPoints", feedback.transform, "+0 PONTOS", 22, Palette.Yellow);
            controller.feedbackText = UiKit.Label("FeedbackText", feedback.transform, "Explicação.", 21, Palette.White);
            controller.feedbackReference = UiKit.Label("FeedbackReference", feedback.transform, "BASE EDUCATIVA:", 17, Palette.Cyan);

            var next = UiKit.Button("NextButton", feedback.transform, "PRÓXIMA PERGUNTA", Palette.Blue, Palette.White, 24);
            UiKit.Height(next.root, 74);
            controller.nextButton = next.button;
            controller.nextButtonLabel = next.label;
        }

        static ClueSlot BuildClue(Transform buttonParent, Transform cardParent, int index)
        {
            var parts = UiKit.Button("Clue" + (index + 1), buttonParent, "", Palette.Mix(Palette.Ink2, Palette.Shadow, 0.2f), Palette.White, 16);
            UnityEngine.Object.DestroyImmediate(parts.label.gameObject);

            var icon = UiKit.Panel("Icon", parts.root.transform, Palette.Alpha(Palette.Cyan, 0.55f));
            UiKit.Frac(UiKit.Rect(icon.gameObject), 0.07f, 0.66f, 0.24f, 0.90f);

            var label = UiKit.Label("Label", parts.root.transform, "Pista", 17, Palette.White, TextAlignmentOptions.TopLeft);
            UiKit.Frac(UiKit.Rect(label.gameObject), 0.28f, 0.34f, 0.95f, 0.94f);

            var hotkey = UiKit.Label("Hotkey", parts.root.transform, "TECLA " + (index + 1) + " • EXAMINAR", 14, Palette.Yellow, TextAlignmentOptions.BottomLeft);
            UiKit.Frac(UiKit.Rect(hotkey.gameObject), 0.07f, 0.06f, 0.95f, 0.30f);

            var card = UiKit.Node("EvidenceCard" + (index + 1), cardParent);
            var cardImage = card.AddComponent<Image>();
            cardImage.color = Palette.Mix(Palette.Ink2, Palette.Blue, 0.18f);
            UiKit.VList(card, 4, 12).childAlignment = TextAnchor.UpperLeft;
            UiKit.FitHeight(card);

            var cardTitle = UiKit.Label("CardTitle", card.transform, "Título", 19, Palette.Yellow);
            var cardBody = UiKit.Label("CardBody", card.transform, "Conteúdo", 19, Palette.White);
            card.SetActive(false);

            return new ClueSlot
            {
                button = parts.button,
                icon = icon,
                label = label,
                hotkey = hotkey,
                card = card,
                cardTitle = cardTitle,
                cardBody = cardBody
            };
        }

        static void BuildFinalPanel(Transform frame, GameController controller)
        {
            var overlay = UiKit.Panel("FinalPanel", frame, Palette.Alpha(Palette.Shadow, 0.95f));
            UiKit.Full(UiKit.Rect(overlay.gameObject));
            controller.finalPanel = overlay.gameObject;

            var card = UiKit.Panel("Card", overlay.transform, Palette.Ink2);
            UiKit.Centered(UiKit.Rect(card.gameObject), 1240, 800);
            UiKit.VList(card.gameObject, 12, 26);
            UiKit.FitHeight(card.gameObject);   // cresce so quando abre a revisao

            var title = UiKit.Label("Title", card.transform, "FIM DO PROGRAMA", 44, Palette.Yellow, TextAlignmentOptions.Center);
            UiKit.Height(title.gameObject, 58);

            controller.rankLabel = UiKit.Label("RankLabel", card.transform, "PATENTE", 30, Palette.Cyan, TextAlignmentOptions.Center);
            UiKit.Height(controller.rankLabel.gameObject, 44);

            var stats = UiKit.Node("Stats", card.transform);
            UiKit.HList(stats, 12, 0);
            UiKit.Height(stats, 110);
            controller.finalAnswered = Stat(stats.transform, "PERGUNTAS", "0", Palette.White);
            controller.finalScore = Stat(stats.transform, "PONTOS", "0000", Palette.Yellow);
            controller.finalCorrect = Stat(stats.transform, "ACERTOS", "0", Palette.Green);
            controller.finalWrong = Stat(stats.transform, "ERROS", "0", Palette.Red);
            controller.finalPercent = Stat(stats.transform, "APROVEITAMENTO", "0%", Palette.Cyan);

            controller.finalSummary = UiKit.Label("Summary", card.transform, "Resumo", 19, Palette.Alpha(Palette.White, 0.75f), TextAlignmentOptions.Center);
            UiKit.Height(controller.finalSummary.gameObject, 34);

            var buttons = UiKit.Node("Buttons", card.transform);
            UiKit.HList(buttons, 12, 0);
            UiKit.Height(buttons, 78);

            var review = UiKit.Button("ReviewButton", buttons.transform, "REVISAR ERROS", Palette.Orange, Palette.Ink, 22);
            controller.reviewButton = review.button;
            controller.reviewButtonLabel = review.label;

            var restart = UiKit.Button("RestartButton", buttons.transform, "JOGAR DE NOVO", Palette.Blue, Palette.White, 22);
            controller.restartButton = restart.button;

            var menu = UiKit.Button("MenuButton", buttons.transform, "VOLTAR AO MENU", Palette.Mix(Palette.Ink, Palette.Shadow, 0.3f), Palette.White, 22);
            controller.menuButton = menu.button;

            var reviewScroll = UiKit.VScroll("ReviewPanel", card.transform, Palette.Mix(Palette.Ink, Palette.Shadow, 0.4f), out var reviewContent);
            UiKit.Height(reviewScroll.gameObject, 340);
            controller.reviewPanel = reviewScroll.gameObject;

            UiKit.VList(reviewContent.gameObject, 10, 12).childAlignment = TextAnchor.UpperLeft;
            UiKit.FitHeight(reviewContent.gameObject);
            controller.reviewList = reviewContent;
            controller.reviewItemTemplate = UiKit.Label("ReviewItemTemplate", reviewContent, "Caso", 19, Palette.White);

            overlay.gameObject.SetActive(false);
        }

        // ================================================================== cena do menu

        static void BuildMenuScene(CaseDatabase database)
        {
            ShowBuilder.EnsureMenuArt();
            var frame = NewScene("Cabinet");
            frame.GetComponent<Image>().color = new Color(0.09f, 0.05f, 0.2f);
            frame.gameObject.AddComponent<RectMask2D>();     // holofotes e brilhos nao vazam

            var controllerNode = new GameObject("MenuController");
            var controller = controllerNode.AddComponent<MenuController>();
            controller.database = database;
            controller.gameSceneName = "01_Jogo";

            // ---- palco: brilhos coloridos, chao iluminado e dois holofotes indo e voltando
            Glow(frame, "Brilho roxo", new Color(0.55f, 0.25f, 1f, 0.55f), 0.5f, 0.78f, 1500f);
            Glow(frame, "Brilho magenta", new Color(1f, 0.25f, 0.6f, 0.35f), 0.08f, 0.05f, 900f);
            Glow(frame, "Brilho ciano", new Color(0.25f, 0.8f, 1f, 0.3f), 0.92f, 0.05f, 900f);
            Glow(frame, "Chao do palco", new Color(1f, 0.8f, 0.4f, 0.25f), 0.5f, -0.02f, 1300f);
            Beam(frame, "Holofote esquerdo", 0.12f, new Color(1f, 0.85f, 0.5f, 0.22f), -25f, 0f);
            Beam(frame, "Holofote direito", 0.88f, new Color(0.5f, 0.85f, 1f, 0.22f), 25f, 2.5f);

            // ---- faixa de texto correndo no topo
            var tickerBand = UiKit.Panel("Ticker", frame, Palette.Alpha(Palette.Shadow, 0.85f));
            UiKit.TopBand(UiKit.Rect(tickerBand.gameObject), 0, 38);
            tickerBand.gameObject.AddComponent<RectMask2D>();
            var phrase = "AO VIVO • SHOW DO PROCON • É INFRAÇÃO OU NÃO É? • CÓDIGO DE DEFESA DO CONSUMIDOR • ";
            var tickerText = UiKit.Label("TickerTexto", tickerBand.transform, phrase + phrase + phrase + phrase,
                                         17, Palette.Yellow, TextAlignmentOptions.Left);
            tickerText.fontStyle = FontStyles.Bold;
            var tickerRect = tickerText.rectTransform;
            tickerRect.anchorMin = new Vector2(0f, 0f);
            tickerRect.anchorMax = new Vector2(0f, 1f);
            tickerRect.pivot = new Vector2(0f, 0.5f);
            tickerRect.anchoredPosition = Vector2.zero;
            var tickerFit = tickerText.gameObject.AddComponent<ContentSizeFitter>();
            tickerFit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            controller.ticker = tickerRect;

            // ---- letreiro do programa com moldura de lampadas
            var sign = UiKit.Node("Letreiro", frame);
            var signRect = UiKit.Rect(sign);
            signRect.anchorMin = signRect.anchorMax = new Vector2(0.5f, 1f);
            signRect.pivot = new Vector2(0.5f, 1f);
            signRect.sizeDelta = new Vector2(1120f, 320f);
            signRect.anchoredPosition = new Vector2(0f, -66f);
            controller.title = signRect;

            var rim = UiKit.Panel("Moldura", sign.transform, Palette.Yellow);
            UiKit.Full(UiKit.Rect(rim.gameObject));
            var plate = UiKit.Panel("Placa", sign.transform, new Color(0.13f, 0.06f, 0.28f));
            UiKit.Stretch(UiKit.Rect(plate.gameObject), 10, 10, 10, 10);
            var bulbs = BulbFrame(sign.transform, 1120f, 320f, 34f);
            var marquee = sign.AddComponent<Marquee>();
            marquee.uiBulbs = bulbs;
            marquee.on = new Color(1f, 0.9f, 0.55f);
            marquee.off = new Color(0.45f, 0.28f, 0.12f);

            var showDo = UiKit.Label("ShowDo", sign.transform, "SHOW DO", 54, Palette.White, TextAlignmentOptions.Center);
            showDo.fontStyle = FontStyles.Bold;
            showDo.characterSpacing = 18f;
            UiKit.Frac(showDo.rectTransform, 0.05f, 0.66f, 0.95f, 0.9f);
            var title = UiKit.Label("Title", sign.transform, "PROCON", 150, Palette.Yellow, TextAlignmentOptions.Center);
            title.fontStyle = FontStyles.Bold;
            title.outlineWidth = 0.18f;
            title.outlineColor = new Color32(120, 40, 10, 255);
            UiKit.Frac(title.rectTransform, 0.05f, 0.14f, 0.95f, 0.72f);

            var subtitle = UiKit.Label("Subtitle", frame, "O PROGRAMA DO CÓDIGO DE DEFESA DO CONSUMIDOR", 26, Palette.Cyan, TextAlignmentOptions.Center);
            subtitle.fontStyle = FontStyles.Bold;
            UiKit.TopBand(UiKit.Rect(subtitle.gameObject), 402, 40);

            // ---- titulo
            var titlePanel = UiKit.Node("TitlePanel", frame);
            UiKit.Stretch(UiKit.Rect(titlePanel), 0, 460, 0, 100);
            controller.titlePanel = titlePanel;

            var menuList = UiKit.Node("Options", titlePanel.transform);
            UiKit.Centered(UiKit.Rect(menuList), 520, 384);
            UiKit.VList(menuList, 16, 0);

            controller.playButton = MenuButton(menuList.transform, "JOGAR", Palette.Yellow, Palette.Ink);
            controller.helpButton = MenuButton(menuList.transform, "COMO JOGAR", Palette.Mix(Palette.Ink2, Palette.Shadow, 0.2f), Palette.White);
            controller.creditsButton = MenuButton(menuList.transform, "CRÉDITOS", Palette.Mix(Palette.Ink2, Palette.Shadow, 0.2f), Palette.White);
            controller.quitButton = MenuButton(menuList.transform, "SAIR", Palette.Mix(Palette.Red, Palette.Shadow, 0.3f), Palette.White);

            // ---- dificuldade
            var levelPanel = UiKit.Node("LevelPanel", frame);
            UiKit.Stretch(UiKit.Rect(levelPanel), 40, 452, 40, 96);
            controller.levelPanel = levelPanel;

            var levelTitle = UiKit.Label("LevelTitle", levelPanel.transform, "ESCOLHA A DIFICULDADE", 32, Palette.White, TextAlignmentOptions.Center);
            UiKit.TopBand(UiKit.Rect(levelTitle.gameObject), 0, 48);

            var cards = UiKit.Node("LevelCards", levelPanel.transform);
            UiKit.TopBand(UiKit.Rect(cards), 56, 270, 60, 60);
            UiKit.HList(cards, 18, 0);

            controller.levelOptions = new LevelOption[3];
            var keys = new[] { LevelKey.Fundamental, LevelKey.Medio, LevelKey.Tecnico };
            for (var i = 0; i < keys.Length; i++)
            {
                var definition = database.Level(keys[i]);
                controller.levelOptions[i] = BuildLevelCard(cards.transform, keys[i], i + 1, definition);
            }

            controller.levelStatus = UiKit.Label("LevelStatus", levelPanel.transform, "Escolha uma dificuldade para começar o programa.", 22, Palette.Yellow, TextAlignmentOptions.Center);
            UiKit.TopBand(UiKit.Rect(controller.levelStatus.gameObject), 334, 40);

            var levelButtons = UiKit.Node("LevelButtons", levelPanel.transform);
            UiKit.Centered(UiKit.Rect(levelButtons), 720, 80);
            UiKit.Rect(levelButtons).anchorMin = new Vector2(0.5f, 0f);
            UiKit.Rect(levelButtons).anchorMax = new Vector2(0.5f, 0f);
            UiKit.Rect(levelButtons).pivot = new Vector2(0.5f, 0f);
            UiKit.Rect(levelButtons).anchoredPosition = new Vector2(0f, 4f);
            UiKit.HList(levelButtons, 14, 0);

            var start = UiKit.Button("StartButton", levelButtons.transform, "COMEÇAR O SHOW", Palette.Yellow, Palette.Ink, 26);
            controller.startButton = start.button;
            var backLevel = UiKit.Button("BackButton", levelButtons.transform, "VOLTAR", Palette.Mix(Palette.Ink, Palette.Shadow, 0.3f), Palette.White, 24);
            controller.backFromLevelButton = backLevel.button;

            levelPanel.SetActive(false);

            // ---- como jogar
            controller.helpPanel = BuildTextPanel(frame, "HelpPanel", "COMO JOGAR",
                "Você é o participante do Show do PROCON. O apresentador mostra uma situação no telão.\n\n" +
                "1. Leia a resposta do responsável pelo estabelecimento. Em alguns casos abre um DOSSIÊ:\n" +
                "    examine as três pistas (teclas 1, 2 e 3) antes de decidir.\n" +
                "2. Decida: a situação É INFRAÇÃO (tecla V) ou NÃO É INFRAÇÃO (tecla F).\n" +
                "3. Leia a explicação e a base legal, e siga para a próxima pergunta com ENTER.\n\n" +
                "Acertos seguidos dão bônus de sequência, de 25 até 100 pontos extras. ESC volta ao menu.",
                out var backHelp);
            controller.backFromHelpButton = backHelp;

            controller.creditsPanel = BuildTextPanel(frame, "CreditsPanel", "CRÉDITOS",
                "Show do PROCON — jogo educativo sobre o Código de Defesa do Consumidor.\n\n" +
                "Conteúdo e casos: equipe do projeto.\n" +
                "Versão Unity feita a partir do protótipo original do grupo (Missão PROCON).\n\n" +
                "Os casos e as explicações citam artigos do CDC (Lei 8.078/1990) com fim educativo.\n" +
                "O jogo não substitui orientação jurídica.",
                out var backCredits);
            controller.backFromCreditsButton = backCredits;

            // ---- audio (sempre visivel)
            var audioRow = UiKit.Node("AudioRow", frame);
            UiKit.BottomBand(UiKit.Rect(audioRow), 20, 56, 40, 40);
            UiKit.HList(audioRow, 14, 0);

            var sfx = UiKit.Button("SfxButton", audioRow.transform, "EFEITOS: LIGADOS", Palette.Mix(Palette.Ink, Palette.Shadow, 0.25f), Palette.Cyan, 20);
            controller.sfxButton = sfx.button;
            controller.sfxLabel = sfx.label;

            var hintLine = UiKit.Label("Hint", audioRow.transform, "ENTER joga • ESC sai", 18, Palette.Alpha(Palette.White, 0.45f), TextAlignmentOptions.Center);
            hintLine.raycastTarget = false;

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), MenuScenePath);
        }

        /// <summary>Brilho redondo e suave, centrado na fracao (x, y) da tela.</summary>
        static void Glow(Transform parent, string name, Color color, float x, float y, float size)
        {
            var glow = UiKit.Panel(name, parent, color);
            glow.sprite = ShowBuilder.MenuSprite("brilho");
            glow.raycastTarget = false;
            var rect = UiKit.Rect(glow.gameObject);
            rect.anchorMin = rect.anchorMax = new Vector2(x, y);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = Vector2.zero;
        }

        /// <summary>Feixe de holofote preso no alto da tela, balancando.</summary>
        static void Beam(Transform parent, string name, float x, Color color, float tilt, float offset)
        {
            var beam = UiKit.Panel(name, parent, color);
            beam.sprite = ShowBuilder.MenuSprite("feixe");
            beam.raycastTarget = false;
            var rect = UiKit.Rect(beam.gameObject);
            rect.anchorMin = rect.anchorMax = new Vector2(x, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(420f, 1250f);
            rect.anchoredPosition = new Vector2(0f, 40f);
            rect.localRotation = Quaternion.Euler(0f, 0f, tilt);
            var sweep = beam.gameObject.AddComponent<Sweep>();
            sweep.angle = 16f;
            sweep.period = 7f;
            sweep.offset = offset;
        }

        /// <summary>Lampadas em volta de um retangulo, no sentido horario.</summary>
        static Graphic[] BulbFrame(Transform parent, float width, float height, float spacing)
        {
            var bulbs = new List<Graphic>();
            var sprite = ShowBuilder.MenuSprite("lampada");
            void Line(Vector2 from, Vector2 to)
            {
                var count = Mathf.Max(1, Mathf.RoundToInt(Vector2.Distance(from, to) / spacing));
                for (var i = 0; i < count; i++)
                {
                    var bulb = UiKit.Panel("Lampada", parent, Color.white);
                    bulb.sprite = sprite;
                    bulb.raycastTarget = false;
                    var rect = UiKit.Rect(bulb.gameObject);
                    rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                    rect.sizeDelta = new Vector2(20f, 20f);
                    rect.anchoredPosition = Vector2.Lerp(from, to, (float)i / count);
                    bulbs.Add(bulb);
                }
            }
            float x = width / 2f - 5f, y = height / 2f - 5f;
            Line(new Vector2(-x, -y), new Vector2(-x, y));
            Line(new Vector2(-x, y), new Vector2(x, y));
            Line(new Vector2(x, y), new Vector2(x, -y));
            Line(new Vector2(x, -y), new Vector2(-x, -y));
            return bulbs.ToArray();
        }

        static Button MenuButton(Transform parent, string text, Color background, Color foreground)
        {
            var parts = UiKit.Button(text.Replace(" ", "") + "Button", parent, text, background, foreground, 28);
            UiKit.Height(parts.root, 84);
            return parts.button;
        }

        static LevelOption BuildLevelCard(Transform parent, LevelKey key, int number, LevelDef definition)
        {
            var parts = UiKit.Button("Level" + number, parent, "", Palette.Mix(Palette.Ink2, Palette.Shadow, 0.35f), Palette.White, 20);
            UnityEngine.Object.DestroyImmediate(parts.label.gameObject);

            var number_ = UiKit.Label("Number", parts.root.transform, "TECLA " + number, 18, Palette.Yellow, TextAlignmentOptions.Center);
            UiKit.Frac(UiKit.Rect(number_.gameObject), 0.05f, 0.82f, 0.95f, 0.96f);

            var title = UiKit.Label("Title", parts.root.transform, definition.label, 30, Palette.White, TextAlignmentOptions.Center);
            UiKit.Frac(UiKit.Rect(title.gameObject), 0.05f, 0.60f, 0.95f, 0.82f);

            var description = UiKit.Label("Description", parts.root.transform, definition.description, 19, Palette.Alpha(Palette.White, 0.75f), TextAlignmentOptions.Top);
            UiKit.Frac(UiKit.Rect(description.gameObject), 0.07f, 0.22f, 0.93f, 0.58f);

            var points = UiKit.Label("Points", parts.root.transform, definition.basePoints + " pontos por acerto", 20, Palette.Cyan, TextAlignmentOptions.Center);
            UiKit.Frac(UiKit.Rect(points.gameObject), 0.05f, 0.06f, 0.95f, 0.20f);

            return new LevelOption
            {
                key = key,
                button = parts.button,
                background = parts.background,
                title = title,
                description = description
            };
        }

        static GameObject BuildTextPanel(Transform frame, string name, string heading, string body, out Button backButton)
        {
            var panel = UiKit.Node(name, frame);
            UiKit.Stretch(UiKit.Rect(panel), 90, 452, 90, 96);

            var background = panel.AddComponent<Image>();
            background.color = Palette.Mix(Palette.Ink, Palette.Shadow, 0.35f);

            var title = UiKit.Label("Heading", panel.transform, heading, 36, Palette.Yellow, TextAlignmentOptions.Center);
            UiKit.TopBand(UiKit.Rect(title.gameObject), 18, 54);

            var text = UiKit.Label("Body", panel.transform, body, 22, Palette.White, TextAlignmentOptions.TopLeft);
            UiKit.Stretch(UiKit.Rect(text.gameObject), 40, 84, 40, 110);

            var parts = UiKit.Button("BackButton", panel.transform, "VOLTAR", Palette.Blue, Palette.White, 24);
            var rect = UiKit.Rect(parts.root);
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(320f, 74f);
            rect.anchoredPosition = new Vector2(0f, 20f);

            backButton = parts.button;
            panel.SetActive(false);
            return panel;
        }
    }
}
