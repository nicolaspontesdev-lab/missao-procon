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

        /// <summary>Canvas da interface por cima de uma cena 3D que ja tem camera.</summary>
        static Transform NewOverlay()
        {
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

            var cabinet = UiKit.Panel("Cabinet", canvasNode.transform, Color.clear);
            cabinet.raycastTarget = false;
            UiKit.Stretch(UiKit.Rect(cabinet.gameObject), 24, 24, 24, 24);
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

        // Paleta reduzida da rodada: azul-noite translucido, dourado de destaque,
        // branco para leitura; verde e vermelho so nas respostas.
        static readonly Color Night = new Color(0.04f, 0.07f, 0.17f, 0.92f);
        static readonly Color NightLight = new Color(0.09f, 0.15f, 0.33f, 0.95f);
        static readonly Color Gold = new Color(1f, 0.8f, 0.28f);
        static readonly Color GoldLine = new Color(1f, 0.8f, 0.28f, 0.6f);

        static void BuildGameScene(CaseDatabase database, CaseVisualBook visualBook)
        {
            ShowSkin.EnsureSprites();

            // estudio 3D; o telao mostra so a pergunta e a ilustracao, os controles
            // ficam numa camada da tela, embaixo, ao lado do apresentador
            var rig = ShowBuilder.BuildStudioScene();
            if (rig.screen == null) return;

            var controllerNode = new GameObject("GameController");
            var controller = controllerNode.AddComponent<GameController>();
            controller.database = database;
            controller.visualBook = visualBook;
            controller.menuSceneName = "00_Menu";
            controller.presenter = rig.presenter;

            var evidence = BuildScreen(rig.screen, controller);
            BuildScoreCard(rig.overlay, controller);
            BuildControls(rig.overlay, controller, evidence);
            BuildFeedback(rig.overlay, controller);
            BuildFinalPanel(rig.overlay, controller);

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), GameScenePath);
        }

        /// <summary>Retangulo fixo medido a partir do canto de cima a esquerda do pai.</summary>
        static void Box(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        static TextMeshProUGUI Text(string name, Transform parent, string text, float size, Color color,
                                    TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft, bool bold = false)
        {
            var label = UiKit.Label(name, parent, text, size, color, alignment);
            if (bold) label.fontStyle = FontStyles.Bold;
            label.overflowMode = TextOverflowModes.Ellipsis;
            return label;
        }

        static void Pill(Image image, Color color, float corner = 1.4f)
        {
            image.sprite = ShowSkin.PillSprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = corner;
            image.color = color;
        }

        // ------------------------------------------------------------------ telao

        /// <summary>
        /// Telao (1280 x 720): local no alto, pergunta grande a esquerda e o
        /// cenario 2D original pequeno a direita, como apoio visual.
        /// </summary>
        static Transform BuildScreen(Transform screen, GameController controller)
        {
            var background = UiKit.Panel("Fundo", screen, Color.white);
            background.sprite = ShowSkin.BackdropSprite;
            background.raycastTarget = false;
            UiKit.Full(background.rectTransform);

            controller.locationName = Text("Local", screen, "LOCAL", 40, Gold, TextAlignmentOptions.Left, true);
            Box(controller.locationName.rectTransform, 44, 18, 620, 52);
            controller.locationSubtitle = Text("Assunto", screen, "ASSUNTO", 24, Palette.Cyan, TextAlignmentOptions.Right, true);
            Box(controller.locationSubtitle.rectTransform, 660, 28, 576, 36);
            var line = UiKit.Panel("Linha", screen, GoldLine);
            line.raycastTarget = false;
            Box(line.rectTransform, 44, 80, 1192, 3);

            // ---- pergunta: uma coluna que se ajusta ao texto (quem fala, pergunta,
            // pista e evidencias do dossie), sem posicoes fixas que se sobreponham
            var column = UiKit.Node("ColunaDaPergunta", screen);
            Box(UiKit.Rect(column), 44, 100, 580, 540);
            UiKit.VList(column, 10, 0).childAlignment = TextAnchor.UpperLeft;
            column.AddComponent<RectMask2D>();

            controller.speakerLabel = Text("QuemFala", column.transform, "RESPOSTA DO RESPONSÁVEL", 24, Palette.Cyan, TextAlignmentOptions.Left, true);
            controller.questionText = Text("Pergunta", column.transform, "Texto do caso.", 38, Color.white, TextAlignmentOptions.TopLeft, true);
            controller.questionText.overflowMode = TextOverflowModes.Overflow;
            controller.questionText.lineSpacing = -2f;
            controller.hintText = Text("Pista", column.transform, "PISTA:", 24, Palette.Orange, TextAlignmentOptions.TopLeft, true);
            controller.hintText.overflowMode = TextOverflowModes.Overflow;

            // evidencias do dossie, aparecem uma a uma embaixo da pergunta
            var evidence = UiKit.Node("Evidencias", column.transform);
            UiKit.VList(evidence, 8, 0).childAlignment = TextAnchor.UpperLeft;
            UiKit.FitHeight(evidence);
            controller.clues = new ClueSlot[3];

            controller.instructionText = Text("Instrucao", screen, "HÁ INFRAÇÃO NESTA SITUAÇÃO?", 30, Gold, TextAlignmentOptions.Left, true);
            Box(controller.instructionText.rectTransform, 44, 652, 580, 46);
            controller.instructionText.enableWordWrapping = false;
            controller.instructionText.enableAutoSizing = true;
            controller.instructionText.fontSizeMin = 18f;
            controller.instructionText.fontSizeMax = 30f;

            // ---- ilustracao: o cenario 2D original, pequeno, com uma borda so
            var box = UiKit.Panel("SceneBox", screen, new Color(0.02f, 0.01f, 0.06f, 1f));
            Box(box.rectTransform, 652, 118, 584, 344);
            var rim = UiKit.Panel("Borda", box.transform, Gold);
            rim.raycastTarget = false;
            var rimRect = rim.rectTransform;
            UiKit.Full(rimRect);
            rimRect.offsetMin = new Vector2(-4f, -4f);
            rimRect.offsetMax = new Vector2(4f, 4f);
            rim.transform.SetAsFirstSibling();
            var hole = UiKit.Panel("Moldura", box.transform, new Color(0.02f, 0.01f, 0.06f, 1f));
            hole.raycastTarget = false;
            UiKit.Full(hole.rectTransform);
            hole.transform.SetSiblingIndex(1);
            controller.stages = StageArt.BuildAll(box.transform, controller.database).ToArray();

            controller.visitLine = Text("Visita", screen, "VISITA 1 DE 5", 22, Gold, TextAlignmentOptions.Center, true);
            Box(controller.visitLine.rectTransform, 652, 474, 584, 32);
            return evidence.transform;
        }

        // ------------------------------------------------------------------ placar discreto

        static void BuildScoreCard(Transform overlay, GameController controller)
        {
            var card = UiKit.Panel("Placar", overlay, Night);
            card.raycastTarget = false;
            ShowSkin.Plate(card, Night, GoldLine, 1.8f, false);
            Box(card.rectTransform, 28, 24, 460, 150);

            var live = UiKit.Panel("AoVivo", card.transform, Palette.Red);
            live.raycastTarget = false;
            Pill(live, Palette.Red, 3f);
            Box(live.rectTransform, 18, 16, 150, 36);
            var liveText = Text("Texto", live.transform, "● AO VIVO", 20, Color.white, TextAlignmentOptions.Center, true);
            UiKit.Full(liveText.rectTransform);

            controller.levelBadge = Text("Nivel", card.transform, "FÁCIL", 24, Gold, TextAlignmentOptions.Right, true);
            Box(controller.levelBadge.rectTransform, 180, 16, 262, 36);

            var questionCaption = Text("PerguntaLegenda", card.transform, "PERGUNTA", 16, Palette.Alpha(Color.white, 0.6f), TextAlignmentOptions.Left, true);
            Box(questionCaption.rectTransform, 20, 64, 150, 20);
            controller.caseCounter = Text("Pergunta", card.transform, "1/15", 34, Color.white, TextAlignmentOptions.Left, true);
            Box(controller.caseCounter.rectTransform, 20, 84, 150, 40);

            var scoreCaption = Text("PontosLegenda", card.transform, "PONTOS", 16, Palette.Alpha(Color.white, 0.6f), TextAlignmentOptions.Left, true);
            Box(scoreCaption.rectTransform, 170, 64, 150, 20);
            controller.scoreValue = Text("Pontos", card.transform, "0000", 34, Gold, TextAlignmentOptions.Left, true);
            Box(controller.scoreValue.rectTransform, 170, 84, 150, 40);

            controller.comboBadge = Text("Combo", card.transform, "COMBO x0", 20, Palette.Cyan, TextAlignmentOptions.Right, true);
            Box(controller.comboBadge.rectTransform, 300, 90, 142, 30);

            var track = UiKit.Panel("Progresso", card.transform, new Color(1f, 1f, 1f, 0.12f));
            track.raycastTarget = false;
            Pill(track, new Color(1f, 1f, 1f, 0.12f), 6f);
            Box(track.rectTransform, 20, 130, 420, 8);
            var fill = UiKit.Panel("Enchimento", track.transform, Gold);
            fill.raycastTarget = false;
            Pill(fill, Gold, 6f);
            var fillRect = fill.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0.05f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            controller.progressFill = fillRect;
        }

        // ------------------------------------------------------------------ controles da rodada

        /// <summary>
        /// Embaixo, a direita do apresentador: pistas do dossie (quando houver) e os
        /// dois botoes de resposta. Crescem para cima, sem cobrir o apresentador.
        /// </summary>
        static void BuildControls(Transform overlay, GameController controller, Transform evidence)
        {
            var controls = UiKit.Node("Controles", overlay);
            var rect = UiKit.Rect(controls);
            rect.anchorMin = new Vector2(0.35f, 0f);
            rect.anchorMax = new Vector2(0.97f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 30f);
            rect.sizeDelta = new Vector2(0f, rect.sizeDelta.y);   // largura so pelas ancoras
            var list = UiKit.VList(controls, 12, 0);
            list.childAlignment = TextAnchor.LowerCenter;
            UiKit.FitHeight(controls);

            // ---- pistas do dossie
            var investigation = UiKit.Node("Pistas", controls.transform);
            UiKit.VList(investigation, 6, 0);
            UiKit.FitHeight(investigation);
            controller.investigationPanel = investigation;

            controller.investigationStatus = Text("Situacao", investigation.transform, "DOSSIÊ ABERTO", 22, Palette.Cyan, TextAlignmentOptions.Center, true);
            UiKit.Height(controller.investigationStatus.gameObject, 30);

            var clueRow = UiKit.Node("BotoesDasPistas", investigation.transform);
            UiKit.HList(clueRow, 12, 0);
            UiKit.Height(clueRow, 76);
            for (var i = 0; i < 3; i++) controller.clues[i] = BuildClue(clueRow.transform, evidence, i);

            // ---- respostas
            var answerRow = UiKit.Node("Respostas", controls.transform);
            UiKit.HList(answerRow, 22, 0);
            UiKit.Height(answerRow, 116);
            controller.answerRow = answerRow;

            var violation = AnswerButton(answerRow.transform, "ViolationButton", "É INFRAÇÃO", "V", Palette.Mix(Palette.Red, Palette.Shadow, 0.25f));
            controller.violationButton = violation.button;
            controller.violationBackground = violation.background;
            var lawful = AnswerButton(answerRow.transform, "LawfulButton", "NÃO É INFRAÇÃO", "F", Palette.Mix(Palette.Green, Palette.Shadow, 0.25f));
            controller.lawfulButton = lawful.button;
            controller.lawfulBackground = lawful.background;
        }

        static UiKit.ButtonParts AnswerButton(Transform parent, string name, string text, string key, Color color)
        {
            var parts = UiKit.Button(name, parent, text, color, Color.white, 36);
            Pill(parts.background, color, 1.1f);
            parts.label.fontStyle = FontStyles.Bold;
            parts.label.enableWordWrapping = false;
            parts.label.enableAutoSizing = true;
            parts.label.fontSizeMin = 22f;
            parts.label.fontSizeMax = 36f;
            UiKit.Stretch(parts.label.rectTransform, 108, 8, 28, 8);

            // tecla num selo redondo a esquerda, como a letra das alternativas
            var badge = UiKit.Panel("Tecla", parts.root.transform, new Color(0f, 0f, 0f, 0.35f));
            badge.sprite = ShowSkin.DiscSprite;
            badge.raycastTarget = false;
            var badgeRect = badge.rectTransform;
            badgeRect.anchorMin = badgeRect.anchorMax = new Vector2(0f, 0.5f);
            badgeRect.pivot = new Vector2(0f, 0.5f);
            badgeRect.sizeDelta = new Vector2(72f, 72f);
            badgeRect.anchoredPosition = new Vector2(20f, 0f);
            var letter = Text("Letra", badge.transform, key, 36, Gold, TextAlignmentOptions.Center, true);
            UiKit.Full(letter.rectTransform);
            return parts;
        }

        static ClueSlot BuildClue(Transform buttonParent, Transform cardParent, int index)
        {
            var parts = UiKit.Button("Clue" + (index + 1), buttonParent, "", NightLight, Color.white, 18);
            Pill(parts.background, NightLight, 2f);
            UnityEngine.Object.DestroyImmediate(parts.label.gameObject);

            var icon = UiKit.Panel("Icon", parts.root.transform, Palette.Alpha(Palette.Cyan, 0.55f));
            icon.sprite = ShowSkin.DiscSprite;
            icon.raycastTarget = false;
            var iconRect = icon.rectTransform;
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.sizeDelta = new Vector2(22f, 22f);
            iconRect.anchoredPosition = new Vector2(22f, 0f);

            var label = Text("Label", parts.root.transform, "Pista", 20, Color.white, TextAlignmentOptions.Left, true);
            UiKit.Stretch(label.rectTransform, 56, 8, 16, 30);
            var hotkey = Text("Hotkey", parts.root.transform, "TECLA " + (index + 1), 17, Gold, TextAlignmentOptions.Left);
            UiKit.Stretch(hotkey.rectTransform, 56, 44, 16, 6);

            // a evidencia revelada aparece no telao, embaixo da pergunta
            var card = UiKit.Node("Evidencia" + (index + 1), cardParent);
            UiKit.VList(card, 2, 0).childAlignment = TextAnchor.UpperLeft;
            UiKit.FitHeight(card);
            var cardTitle = Text("CardTitle", card.transform, "Título", 24, Gold, TextAlignmentOptions.TopLeft, true);
            var cardBody = Text("CardBody", card.transform, "Conteúdo", 24, Color.white);
            cardBody.overflowMode = TextOverflowModes.Overflow;
            card.SetActive(false);

            return new ClueSlot
            {
                button = parts.button, icon = icon, label = label, hotkey = hotkey,
                card = card, cardTitle = cardTitle, cardBody = cardBody
            };
        }

        // ------------------------------------------------------------------ resultado da pergunta

        static void BuildFeedback(Transform overlay, GameController controller)
        {
            var card = UiKit.Panel("Resultado", overlay, Night);
            ShowSkin.Plate(card, new Color(0.04f, 0.07f, 0.17f, 0.96f), GoldLine, 1.4f, false);
            var rect = card.rectTransform;
            rect.anchorMin = new Vector2(0.35f, 0f);
            rect.anchorMax = new Vector2(0.97f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 30f);
            rect.sizeDelta = new Vector2(0f, rect.sizeDelta.y);
            var list = UiKit.VList(card.gameObject, 10, 26);
            list.padding = new RectOffset(34, 34, 22, 26);
            list.childAlignment = TextAnchor.UpperLeft;
            UiKit.FitHeight(card.gameObject);
            controller.feedbackPanel = card.gameObject;

            var stripe = UiKit.Panel("Faixa", card.transform, Palette.GreenDark);
            stripe.raycastTarget = false;
            Pill(stripe, Palette.GreenDark, 8f);
            UiKit.Height(stripe.gameObject, 6);
            controller.feedbackStripe = stripe;

            // titulo e pontos a esquerda, botao de proxima a direita
            var header = UiKit.Node("Cabecalho", card.transform);
            var headerList = UiKit.HList(header, 20, 0);
            headerList.childForceExpandWidth = false;
            UiKit.Height(header, 78);

            var titles = UiKit.Node("Titulos", header.transform);
            UiKit.VList(titles, 0, 0).childAlignment = TextAnchor.MiddleLeft;
            titles.AddComponent<LayoutElement>().flexibleWidth = 1f;
            controller.feedbackTitle = Text("FeedbackTitle", titles.transform, "CORRETO!", 40, Palette.Green, TextAlignmentOptions.Left, true);
            UiKit.Height(controller.feedbackTitle.gameObject, 46);
            controller.feedbackPoints = Text("FeedbackPoints", titles.transform, "+0 PONTOS", 24, Gold, TextAlignmentOptions.Left, true);
            UiKit.Height(controller.feedbackPoints.gameObject, 30);

            var next = UiKit.Button("NextButton", header.transform, "PRÓXIMA PERGUNTA", Gold, Palette.Ink, 26);
            Pill(next.background, Gold, 1.3f);
            next.label.fontStyle = FontStyles.Bold;
            var nextLayout = next.root.AddComponent<LayoutElement>();
            nextLayout.preferredWidth = 400f;
            nextLayout.minWidth = 400f;
            controller.nextButton = next.button;
            controller.nextButtonLabel = next.label;

            controller.feedbackText = Text("FeedbackText", card.transform, "Explicação.", 27, Color.white);
            controller.feedbackText.overflowMode = TextOverflowModes.Overflow;
            controller.feedbackReference = Text("FeedbackReference", card.transform, "BASE EDUCATIVA:", 21, Palette.Cyan);
            controller.feedbackReference.overflowMode = TextOverflowModes.Overflow;

            card.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ resultado final

        static void BuildFinalPanel(Transform overlay, GameController controller)
        {
            var dim = UiKit.Panel("FinalPanel", overlay, new Color(0.01f, 0.02f, 0.06f, 0.94f));
            UiKit.Full(dim.rectTransform);
            controller.finalPanel = dim.gameObject;

            var card = UiKit.Panel("Card", dim.transform, Night);
            ShowSkin.Plate(card, new Color(0.05f, 0.09f, 0.22f, 0.98f), Gold, 1.2f, false);
            UiKit.Centered(card.rectTransform, 1000, 560);
            var list = UiKit.VList(card.gameObject, 16, 40);
            list.childAlignment = TextAnchor.UpperCenter;
            UiKit.FitHeight(card.gameObject);

            var title = Text("Title", card.transform, "FIM DO PROGRAMA", 58, Gold, TextAlignmentOptions.Center, true);
            UiKit.Height(title.gameObject, 70);
            controller.rankLabel = Text("RankLabel", card.transform, "PATENTE", 30, Palette.Cyan, TextAlignmentOptions.Center, true);
            UiKit.Height(controller.rankLabel.gameObject, 40);

            var stats = UiKit.Node("Stats", card.transform);
            UiKit.HList(stats, 16, 0);
            UiKit.Height(stats, 120);
            controller.finalScore = FinalStat(stats.transform, "PONTOS", Gold);
            controller.finalCorrect = FinalStat(stats.transform, "ACERTOS", Palette.Green);
            controller.finalWrong = FinalStat(stats.transform, "ERROS", Palette.Red);

            controller.finalSummary = Text("Summary", card.transform, "Resumo", 22, Palette.Alpha(Color.white, 0.75f), TextAlignmentOptions.Center);
            UiKit.Height(controller.finalSummary.gameObject, 32);

            var buttons = UiKit.Node("Buttons", card.transform);
            UiKit.HList(buttons, 20, 0);
            UiKit.Height(buttons, 84);
            var restart = UiKit.Button("RestartButton", buttons.transform, "JOGAR DE NOVO", Gold, Palette.Ink, 28);
            Pill(restart.background, Gold, 1.3f);
            restart.label.fontStyle = FontStyles.Bold;
            controller.restartButton = restart.button;
            var menu = UiKit.Button("MenuButton", buttons.transform, "VOLTAR AO MENU", NightLight, Color.white, 28);
            Pill(menu.background, NightLight, 1.3f);
            menu.label.fontStyle = FontStyles.Bold;
            controller.menuButton = menu.button;

            dim.gameObject.SetActive(false);
        }

        static TextMeshProUGUI FinalStat(Transform parent, string caption, Color color)
        {
            var node = UiKit.Panel(caption, parent, NightLight);
            node.raycastTarget = false;
            ShowSkin.Plate(node, NightLight, Palette.Alpha(color, 0.5f), 2f, false);
            var captionLabel = Text("Legenda", node.transform, caption, 20, Palette.Alpha(Color.white, 0.65f), TextAlignmentOptions.Center, true);
            UiKit.Frac(captionLabel.rectTransform, 0f, 0.62f, 1f, 0.92f);
            var value = Text("Valor", node.transform, "0", 48, color, TextAlignmentOptions.Center, true);
            UiKit.Frac(value.rectTransform, 0f, 0.08f, 1f, 0.66f);
            return value;
        }

        // ================================================================== cena do menu

        static void BuildMenuScene(CaseDatabase database)
        {
            ShowBuilder.EnsureMenuArt();
            ShowBuilder.BuildMenuStage();       // palco 3D: titulo em letras 3D, holofotes e lampadas
            var frame = NewOverlay();

            var controllerNode = new GameObject("MenuController");
            var controller = controllerNode.AddComponent<MenuController>();
            controller.database = database;
            controller.gameSceneName = "01_Jogo";

            // ---- faixa de texto correndo no topo
            var tickerBand = UiKit.Panel("Ticker", frame, Palette.Alpha(Palette.Shadow, 0.85f));
            UiKit.TopBand(UiKit.Rect(tickerBand.gameObject), 0, 38);
            tickerBand.gameObject.AddComponent<RectMask2D>();
            // frase fixa e centralizada: correndo, ela aparecia cortada nas duas pontas da tela
            var tickerText = UiKit.Label("TickerTexto", tickerBand.transform,
                                         "AO VIVO  •  SHOW DO PROCON  •  É INFRAÇÃO OU NÃO É?  •  CÓDIGO DE DEFESA DO CONSUMIDOR",
                                         17, Palette.Yellow, TextAlignmentOptions.Center);
            tickerText.fontStyle = FontStyles.Bold;
            tickerText.enableWordWrapping = false;
            tickerText.enableAutoSizing = true;
            tickerText.fontSizeMin = 11f;
            tickerText.fontSizeMax = 17f;
            UiKit.Stretch(tickerText.rectTransform, 24, 0, 24, 0);

            var subtitle = UiKit.Label("Subtitle", frame, "O PROGRAMA DO CÓDIGO DE DEFESA DO CONSUMIDOR", 26, Palette.Cyan, TextAlignmentOptions.Center);
            subtitle.fontStyle = FontStyles.Bold;
            UiKit.TopBand(UiKit.Rect(subtitle.gameObject), 506, 40);

            // ---- titulo
            var titlePanel = UiKit.Node("TitlePanel", frame);
            UiKit.Stretch(UiKit.Rect(titlePanel), 0, 560, 0, 90);
            controller.titlePanel = titlePanel;

            var menuList = UiKit.Node("Options", titlePanel.transform);
            UiKit.Centered(UiKit.Rect(menuList), 520, 246);
            UiKit.VList(menuList, 12, 0);

            controller.playButton = MenuButton(menuList.transform, "JOGAR", Palette.Yellow, Palette.Ink);
            controller.helpButton = MenuButton(menuList.transform, "COMO JOGAR", Palette.Mix(Palette.Ink2, Palette.Shadow, 0.2f), Palette.White);
            controller.quitButton = MenuButton(menuList.transform, "SAIR", Palette.Mix(Palette.Red, Palette.Shadow, 0.3f), Palette.White);

            // ---- dificuldade
            var levelPanel = UiKit.Node("LevelPanel", frame);
            UiKit.Stretch(UiKit.Rect(levelPanel), 40, 556, 40, 70);
            controller.levelPanel = levelPanel;

            var levelTitle = UiKit.Label("LevelTitle", levelPanel.transform, "ESCOLHA A DIFICULDADE", 32, Palette.White, TextAlignmentOptions.Center);
            UiKit.TopBand(UiKit.Rect(levelTitle.gameObject), 0, 48);

            var cards = UiKit.Node("LevelCards", levelPanel.transform);
            UiKit.TopBand(UiKit.Rect(cards), 52, 222, 60, 60);
            UiKit.HList(cards, 18, 0);

            controller.levelOptions = new LevelOption[3];
            var keys = new[] { LevelKey.Fundamental, LevelKey.Medio, LevelKey.Tecnico };
            for (var i = 0; i < keys.Length; i++)
            {
                var definition = database.Level(keys[i]);
                controller.levelOptions[i] = BuildLevelCard(cards.transform, keys[i], i + 1, definition);
            }

            controller.levelStatus = UiKit.Label("LevelStatus", levelPanel.transform, "Escolha uma dificuldade para começar o programa.", 22, Palette.Yellow, TextAlignmentOptions.Center);
            UiKit.TopBand(UiKit.Rect(controller.levelStatus.gameObject), 280, 40);

            var levelButtons = UiKit.Node("LevelButtons", levelPanel.transform);
            UiKit.Centered(UiKit.Rect(levelButtons), 720, 70);
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


            // ---- audio (sempre visivel)
            var audioRow = UiKit.Node("AudioRow", frame);
            UiKit.BottomBand(UiKit.Rect(audioRow), 20, 50, 40, 1060);
            UiKit.HList(audioRow, 14, 0);

            var sfx = UiKit.Button("SfxButton", audioRow.transform, "EFEITOS: LIGADOS", Palette.Mix(Palette.Ink, Palette.Shadow, 0.25f), Palette.Cyan, 20);
            controller.sfxButton = sfx.button;
            controller.sfxLabel = sfx.label;

            var hintLine = UiKit.Label("Hint", audioRow.transform, "ENTER joga • ESC sai", 18, Palette.Alpha(Palette.White, 0.45f), TextAlignmentOptions.Center);
            hintLine.raycastTarget = false;

            ShowSkin.SkinMenu(frame, controller);
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), MenuScenePath);
        }

        static Button MenuButton(Transform parent, string text, Color background, Color foreground)
        {
            var parts = UiKit.Button(text.Replace(" ", "") + "Button", parent, text, background, foreground, 28);
            UiKit.Height(parts.root, 74);
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
            UiKit.Stretch(UiKit.Rect(panel), 120, 540, 120, 70);

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
