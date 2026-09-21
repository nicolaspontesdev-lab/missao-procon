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
        const string MenuScenePath = "Assets/Scenes/00_Menu.unity";
        const string GameScenePath = "Assets/Scenes/01_Jogo.unity";

        [MenuItem("Missao PROCON/Gerar tudo (dados + cenas)", false, 0)]
        public static void BuildEverything()
        {
            var database = ImportCases();
            if (database == null) return;
            BuildGameScene(database);
            BuildMenuScene(database);
            RegisterScenes();
            ConfigurePlayer();
            EditorSceneManager.OpenScene(MenuScenePath);
            Debug.Log("Missao PROCON: dados e cenas gerados.");
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
            BuildGameScene(database);
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
                    key = LevelKey.Fundamental, label = "FUNDAMENTAL II", basePoints = 100, showHints = true,
                    description = "Situações diretas. O dossiê traz uma pista extra antes da decisão."
                },
                new LevelDef
                {
                    key = LevelKey.Medio, label = "ENSINO MÉDIO", basePoints = 125, showHints = false,
                    description = "Casos com mais nuance e sem pista. Vale mais ponto por acerto."
                },
                new LevelDef
                {
                    key = LevelKey.Tecnico, label = "TÉCNICO / SUPERIOR", basePoints = 150, showHints = false,
                    description = "Linguagem técnica do CDC, sem pista e com a maior pontuação."
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
            PlayerSettings.productName = "Missão PROCON";
            PlayerSettings.companyName = "Equipe Missão PROCON";
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        }

        // ================================================================== cena do jogo

        static void BuildGameScene(CaseDatabase database)
        {
            var frame = NewScene("Cabinet");

            var controllerNode = new GameObject("GameController");
            var controller = controllerNode.AddComponent<GameController>();
            controller.database = database;
            controller.menuSceneName = "00_Menu";

            // ---- topo
            var topBar = UiKit.Panel("TopBar", frame, Palette.Shadow);
            UiKit.TopBand(UiKit.Rect(topBar.gameObject), 0, 84);

            var title = UiKit.Label("Title", topBar.transform, "MISSÃO PROCON", 36, Palette.Yellow, TextAlignmentOptions.Left);
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
            columnRect.anchorMin = Vector2.zero;
            columnRect.anchorMax = new Vector2(0.40f, 1f);
            columnRect.offsetMin = Vector2.zero;
            columnRect.offsetMax = Vector2.zero;

            var header = UiKit.Panel("LocationHeader", column.transform, Palette.Shadow);
            UiKit.TopBand(UiKit.Rect(header.gameObject), 0, 88);
            controller.locationName = UiKit.Label("LocationName", header.transform, "LOCAL", 30, Palette.Yellow, TextAlignmentOptions.Left);
            UiKit.Frac(UiKit.Rect(controller.locationName.gameObject), 0.04f, 0.45f, 0.98f, 0.95f);
            controller.locationSubtitle = UiKit.Label("LocationSubtitle", header.transform, "SUBTÍTULO", 19, Palette.Cyan, TextAlignmentOptions.Left);
            UiKit.Frac(UiKit.Rect(controller.locationSubtitle.gameObject), 0.04f, 0.06f, 0.98f, 0.46f);

            var box = UiKit.Panel("SceneBox", column.transform, Palette.Ink);
            var boxRect = UiKit.Rect(box.gameObject);
            boxRect.anchorMin = Vector2.zero;
            boxRect.anchorMax = Vector2.one;
            boxRect.offsetMin = new Vector2(0f, 56f);
            boxRect.offsetMax = new Vector2(0f, -96f);

            controller.sceneWall = UiKit.Panel("Wall", box.transform, Palette.Ink2);
            UiKit.Frac(UiKit.Rect(controller.sceneWall.gameObject), 0f, 0.34f, 1f, 1f);

            controller.sceneFloor = UiKit.Panel("Floor", box.transform, Palette.Shadow);
            UiKit.Frac(UiKit.Rect(controller.sceneFloor.gameObject), 0f, 0f, 1f, 0.34f);

            var props = new Image[5];
            for (var i = 0; i < props.Length; i++)
            {
                props[i] = UiKit.Panel("Prop" + (i + 1), box.transform, Palette.Ink2);
                var x = 0.05f + i * 0.19f;
                UiKit.Frac(UiKit.Rect(props[i].gameObject), x, 0.58f, x + 0.14f, 0.84f);
            }
            controller.sceneProps = props;

            controller.inspectorBody = BuildFigure(box.transform, "Inspector", 0.09f, 0.35f, Palette.Blue, "FISCAL");
            controller.ownerBody = BuildFigure(box.transform, "Owner", 0.62f, 0.88f, Palette.Green, "RESPONSÁVEL");

            controller.speechPing = UiKit.Label("SpeechPing", box.transform, "?", 46, Palette.Yellow, TextAlignmentOptions.Center);
            UiKit.Frac(UiKit.Rect(controller.speechPing.gameObject), 0.64f, 0.66f, 0.96f, 0.90f);

            controller.decorLabel = UiKit.Label("DecorLabel", box.transform, "CENÁRIO", 16, Palette.Alpha(Palette.White, 0.55f), TextAlignmentOptions.Center);
            UiKit.Frac(UiKit.Rect(controller.decorLabel.gameObject), 0f, 0.01f, 1f, 0.09f);

            controller.visitLine = UiKit.Label("VisitLine", column.transform, "VISITA 1 DE 5", 20, Palette.Cyan, TextAlignmentOptions.Center);
            UiKit.BottomBand(UiKit.Rect(controller.visitLine.gameObject), 8, 44);
        }

        /// <summary>Boneco de blocagem: cabeca, tronco e dois bracos.</summary>
        static Image BuildFigure(Transform parent, string name, float xMin, float xMax, Color color, string caption)
        {
            var group = UiKit.Node(name, parent);
            UiKit.Frac(UiKit.Rect(group), xMin, 0.05f, xMax, 0.66f);

            var head = UiKit.Panel("Head", group.transform, Palette.Paper);
            UiKit.Frac(UiKit.Rect(head.gameObject), 0.28f, 0.74f, 0.72f, 1f);

            var torso = UiKit.Panel("Torso", group.transform, color);
            UiKit.Frac(UiKit.Rect(torso.gameObject), 0.16f, 0.22f, 0.84f, 0.72f);

            var armLeft = UiKit.Panel("ArmLeft", group.transform, Palette.Mix(Palette.Ink2, Palette.Shadow, 0.45f));
            UiKit.Frac(UiKit.Rect(armLeft.gameObject), 0.02f, 0.30f, 0.16f, 0.66f);

            var armRight = UiKit.Panel("ArmRight", group.transform, Palette.Mix(Palette.Ink2, Palette.Shadow, 0.45f));
            UiKit.Frac(UiKit.Rect(armRight.gameObject), 0.84f, 0.30f, 0.98f, 0.66f);

            var legs = UiKit.Panel("Legs", group.transform, Palette.Mix(Palette.Ink2, Palette.Shadow, 0.7f));
            UiKit.Frac(UiKit.Rect(legs.gameObject), 0.24f, 0.02f, 0.76f, 0.22f);

            var label = UiKit.Label("Caption", group.transform, caption, 14, Palette.Alpha(Palette.White, 0.5f), TextAlignmentOptions.Center);
            UiKit.Frac(UiKit.Rect(label.gameObject), -0.15f, -0.14f, 1.15f, 0.02f);

            return torso;
        }

        static void BuildDialogColumn(Transform parent, GameController controller)
        {
            var column = UiKit.Node("DialogColumn", parent);
            var columnRect = UiKit.Rect(column);
            columnRect.anchorMin = new Vector2(0.41f, 0f);
            columnRect.anchorMax = Vector2.one;
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
            UiKit.Centered(UiKit.Rect(card.gameObject), 1180, 880);
            UiKit.VList(card.gameObject, 12, 26);

            var title = UiKit.Label("Title", card.transform, "MISSÃO CONCLUÍDA", 44, Palette.Yellow, TextAlignmentOptions.Center);
            UiKit.Height(title.gameObject, 58);

            controller.rankLabel = UiKit.Label("RankLabel", card.transform, "PATENTE", 30, Palette.Cyan, TextAlignmentOptions.Center);
            UiKit.Height(controller.rankLabel.gameObject, 44);

            var stats = UiKit.Node("Stats", card.transform);
            UiKit.HList(stats, 12, 0);
            UiKit.Height(stats, 110);
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
            UiKit.Height(reviewScroll.gameObject, 430);
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
            var frame = NewScene("Cabinet");

            var controllerNode = new GameObject("MenuController");
            var controller = controllerNode.AddComponent<MenuController>();
            controller.database = database;
            controller.gameSceneName = "01_Jogo";

            var title = UiKit.Label("Title", frame, "MISSÃO PROCON", 86, Palette.Yellow, TextAlignmentOptions.Center);
            UiKit.TopBand(UiKit.Rect(title.gameObject), 56, 110);

            var subtitle = UiKit.Label("Subtitle", frame, "FISCALIZAÇÃO DO CONSUMIDOR • CÓDIGO DE DEFESA DO CONSUMIDOR", 26, Palette.Cyan, TextAlignmentOptions.Center);
            UiKit.TopBand(UiKit.Rect(subtitle.gameObject), 168, 44);

            // ---- titulo
            var titlePanel = UiKit.Node("TitlePanel", frame);
            UiKit.Stretch(UiKit.Rect(titlePanel), 0, 250, 0, 130);
            controller.titlePanel = titlePanel;

            var menuList = UiKit.Node("Options", titlePanel.transform);
            UiKit.Centered(UiKit.Rect(menuList), 560, 420);
            UiKit.VList(menuList, 16, 0);

            controller.playButton = MenuButton(menuList.transform, "JOGAR", Palette.Blue, Palette.White);
            controller.helpButton = MenuButton(menuList.transform, "COMO JOGAR", Palette.Mix(Palette.Ink, Palette.Shadow, 0.25f), Palette.White);
            controller.creditsButton = MenuButton(menuList.transform, "CRÉDITOS", Palette.Mix(Palette.Ink, Palette.Shadow, 0.25f), Palette.White);
            controller.quitButton = MenuButton(menuList.transform, "SAIR", Palette.Mix(Palette.Red, Palette.Shadow, 0.35f), Palette.White);

            // ---- dificuldade
            var levelPanel = UiKit.Node("LevelPanel", frame);
            UiKit.Stretch(UiKit.Rect(levelPanel), 40, 240, 40, 130);
            controller.levelPanel = levelPanel;

            var levelTitle = UiKit.Label("LevelTitle", levelPanel.transform, "ESCOLHA A DIFICULDADE", 32, Palette.White, TextAlignmentOptions.Center);
            UiKit.TopBand(UiKit.Rect(levelTitle.gameObject), 0, 52);

            var cards = UiKit.Node("LevelCards", levelPanel.transform);
            UiKit.TopBand(UiKit.Rect(cards), 64, 300);
            UiKit.HList(cards, 18, 0);

            controller.levelOptions = new LevelOption[3];
            var keys = new[] { LevelKey.Fundamental, LevelKey.Medio, LevelKey.Tecnico };
            for (var i = 0; i < keys.Length; i++)
            {
                var definition = database.Level(keys[i]);
                controller.levelOptions[i] = BuildLevelCard(cards.transform, keys[i], i + 1, definition);
            }

            controller.levelStatus = UiKit.Label("LevelStatus", levelPanel.transform, "Escolha uma dificuldade para liberar a missão.", 22, Palette.Yellow, TextAlignmentOptions.Center);
            UiKit.TopBand(UiKit.Rect(controller.levelStatus.gameObject), 380, 46);

            var levelButtons = UiKit.Node("LevelButtons", levelPanel.transform);
            UiKit.Centered(UiKit.Rect(levelButtons), 720, 84);
            UiKit.Rect(levelButtons).anchorMin = new Vector2(0.5f, 0f);
            UiKit.Rect(levelButtons).anchorMax = new Vector2(0.5f, 0f);
            UiKit.Rect(levelButtons).pivot = new Vector2(0.5f, 0f);
            UiKit.Rect(levelButtons).anchoredPosition = new Vector2(0f, 12f);
            UiKit.HList(levelButtons, 14, 0);

            var start = UiKit.Button("StartButton", levelButtons.transform, "INICIAR MISSÃO", Palette.Green, Palette.Ink, 26);
            controller.startButton = start.button;
            var backLevel = UiKit.Button("BackButton", levelButtons.transform, "VOLTAR", Palette.Mix(Palette.Ink, Palette.Shadow, 0.3f), Palette.White, 24);
            controller.backFromLevelButton = backLevel.button;

            levelPanel.SetActive(false);

            // ---- como jogar
            controller.helpPanel = BuildTextPanel(frame, "HelpPanel", "COMO JOGAR",
                "Você é fiscal do PROCON e visita cinco locais na mesma rodada.\n\n" +
                "1. Em alguns casos abre um DOSSIÊ. Examine as três pistas (teclas 1, 2 e 3) antes de decidir.\n" +
                "2. Leia a resposta do responsável pelo estabelecimento.\n" +
                "3. Decida: a situação É INFRAÇÃO (tecla V) ou NÃO É INFRAÇÃO (tecla F).\n" +
                "4. Leia a explicação e o artigo do CDC, e siga com ENTER.\n\n" +
                "Cada acerto vale os pontos do nível escolhido. Acertos seguidos dão bônus de sequência,\n" +
                "de 25 até 100 pontos extras. Um erro zera a sequência.\n\n" +
                "ESC volta ao menu a qualquer momento.",
                out var backHelp);
            controller.backFromHelpButton = backHelp;

            controller.creditsPanel = BuildTextPanel(frame, "CreditsPanel", "CRÉDITOS",
                "Missão PROCON — jogo educativo sobre o Código de Defesa do Consumidor.\n\n" +
                "Conteúdo e casos: equipe do projeto.\n" +
                "Versão Unity feita a partir do protótipo original do grupo.\n\n" +
                "Os casos e as explicações citam artigos do CDC (Lei 8.078/1990) com fim educativo.\n" +
                "O jogo não substitui orientação jurídica.",
                out var backCredits);
            controller.backFromCreditsButton = backCredits;

            // ---- audio (sempre visivel)
            var audioRow = UiKit.Node("AudioRow", frame);
            UiKit.BottomBand(UiKit.Rect(audioRow), 20, 62, 40, 40);
            UiKit.HList(audioRow, 14, 0);

            var music = UiKit.Button("MusicButton", audioRow.transform, "MÚSICA: LIGADA", Palette.Mix(Palette.Ink, Palette.Shadow, 0.25f), Palette.Cyan, 20);
            controller.musicButton = music.button;
            controller.musicLabel = music.label;

            var sfx = UiKit.Button("SfxButton", audioRow.transform, "EFEITOS: LIGADOS", Palette.Mix(Palette.Ink, Palette.Shadow, 0.25f), Palette.Cyan, 20);
            controller.sfxButton = sfx.button;
            controller.sfxLabel = sfx.label;

            var hintLine = UiKit.Label("Hint", audioRow.transform, "ENTER joga • ESC sai", 18, Palette.Alpha(Palette.White, 0.45f), TextAlignmentOptions.Center);
            hintLine.raycastTarget = false;

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), MenuScenePath);
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
            UiKit.Stretch(UiKit.Rect(panel), 90, 240, 90, 120);

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
