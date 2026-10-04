using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Procon.EditorTools
{
    /// <summary>
    /// Monta os palcos com a arte extraida do jogo em HTML.
    ///
    /// A ferramenta ferramentas/extrair-artes fotografa cada peca do HTML e grava
    /// em Assets/Art/Cenario, junto com manifesto.json: a posicao de cada peca no
    /// cenario original de 900x530. Aqui cada peca vira uma Image no mesmo lugar.
    ///
    /// Nenhum texto vem desenhado nas imagens: letra pequena reduzida vira borrao.
    /// Placa, balcao, gondola e os textos dos casos sao escritos por cima com a
    /// fonte do jogo, em tamanho de leitura.
    ///
    /// Trocar qualquer peca por arte nova e so substituir o PNG com o mesmo nome.
    /// </summary>
    public static class StageArt
    {
        const string ArtFolder = "Assets/Art/Cenario/";
        const string ManifestPath = ArtFolder + "manifesto.json";

        // Tamanhos de texto, em unidades do canvas de 1920x1080.
        const float SignTitleSize = 24f;
        const float SignSubtitleSize = 15f;
        const float CounterLabelSize = 18f;
        const float DecorLabelSize = 15f;
        const float CalloutSize = 17f;

        [Serializable]
        class Piece
        {
            public string nome;
            public float x, y, w, h;
        }

        [Serializable]
        class Manifest
        {
            public float largura = 900f, altura = 530f;
            public Piece[] pecas;
        }

        /// <summary>Letreiro do balcao de cada ambiente, igual ao CSS do jogo em HTML.</summary>
        static readonly Dictionary<ScenarioKey, string> CounterLabels = new Dictionary<ScenarioKey, string>
        {
            { ScenarioKey.Supermercado, "CAIXA / FISCALIZAÇÃO" },
            { ScenarioKey.Eletronicos, "ASSISTÊNCIA TÉCNICA" },
            { ScenarioKey.Banco, "ATENDIMENTO BANCÁRIO" },
            { ScenarioKey.Online, "CENTRO DE ENTREGAS" },
            { ScenarioKey.Telefonia, "LOJA DE TELEFONIA" },
        };

        enum CalloutMode { Above, Center }

        /// <summary>
        /// Encaixes dos objetos dos casos, no espaco de 900x530 do cenario.
        /// O HTML nao tinha esses objetos, entao a posicao foi escolhida aqui:
        /// produtos sobre o tampo do balcao (y 353), entre o fiscal e o responsavel.
        /// O texto do caso vira uma etiqueta por cima do objeto, que cresce com o
        /// texto em vez de encolher a letra.
        /// </summary>
        struct SlotSpec
        {
            public string id, sprite;
            public float x, y, w, h;
            public CalloutMode mode;
            public float anchorY;      // Center: altura do centro da etiqueta no objeto
            public float lift;         // Above: distancia acima do objeto
            public float size;
            public Color fill, text;
        }

        static SlotSpec[] Slots => new[]
        {
            Above("etiqueta", "etiqueta", 190, 162, 78, 38),
            new SlotSpec { id = "cartaz", sprite = "cartaz", x = 290, y = 45, w = 138, h = 128,
                           mode = CalloutMode.Center, anchorY = 0.42f, size = 14f,
                           fill = Palette.Paper, text = Palette.BlueDark },
            Above("documento", "documento", 250, 286, 60, 72),
            Above("documento-secundario", "documento", 420, 288, 60, 72, 34f),
            Above("caixa", "caixa", 228, 286, 100, 72),
            Above("produto-principal", "embalagem", 332, 280, 54, 80),
            Above("produto-secundario", "embalagem", 394, 284, 46, 76, 34f),   // mais alto: nao bate no vizinho
            Above("balanca", "balanca", 450, 290, 98, 70),
            // cada aparelho no proprio tamanho, apoiado no tampo, para o desenho nao esticar
            Above("celular", "celular", 342, 290, 40, 70),
            Above("fone", "fone", 322, 304, 82, 56),
            Above("notebook", "notebook", 316, 284, 116, 76),
            Above("cartao", "cartao", 430, 312, 76, 48),
            new SlotSpec { id = "terminal", sprite = "terminal", x = 556, y = 262, w = 85, h = 98,
                           mode = CalloutMode.Center, anchorY = 0.68f, size = CalloutSize,
                           fill = Palette.BlueDark, text = Palette.Cyan },          // como tela
        };

        static SlotSpec Above(string id, string sprite, float x, float y, float w, float h, float lift = 4f)
        {
            return new SlotSpec { id = id, sprite = sprite, x = x, y = y, w = w, h = h,
                                  mode = CalloutMode.Above, lift = lift, size = CalloutSize,
                                  fill = Palette.Paper, text = Palette.Ink };
        }

        public static List<StageView> BuildAll(Transform sceneBox, CaseDatabase database)
        {
            // Criar uma cena nova pode descarregar o banco recem-salvo; a referencia
            // vira objeto destruido e passa a valer null. Recarrega do disco.
            if (database == null)
                database = AssetDatabase.LoadAssetAtPath<CaseDatabase>("Assets/Data/CaseDatabase.asset");

            var manifest = LoadManifest();
            var stages = new List<StageView>();
            if (manifest == null) return stages;

            foreach (ScenarioKey key in Enum.GetValues(typeof(ScenarioKey)))
            {
                var texts = database != null ? database.Scenario(key) : new ScenarioDef { key = key };
                var stage = Build(sceneBox, key, texts, manifest);
                if (stage != null) stages.Add(stage);
            }
            return stages;
        }

        static StageView Build(Transform sceneBox, ScenarioKey key, ScenarioDef texts, Manifest manifest)
        {
            var scenario = key.ToString().ToLowerInvariant();
            var pieces = new Dictionary<string, Piece>();
            foreach (var piece in manifest.pecas) pieces[piece.nome] = piece;

            var node = UiKit.Node("Palco_" + key, sceneBox);
            UiKit.Full(UiKit.Rect(node));
            var fitter = node.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = manifest.largura / manifest.altura;
            // como o overflow: hidden do HTML: quem entra pela lateral surge da borda
            node.AddComponent<RectMask2D>();

            var stage = node.AddComponent<StageView>();
            stage.scenario = key;
            var root = node.transform;

            // mesma ordem de pintura do HTML: fundo, decoracao, personagens, balcao
            Place(root, "Parede", "parede_" + scenario, pieces, manifest);

            var decor = Place(root, "Decoracao", "decor_" + scenario, pieces, manifest);
            if (decor != null && !string.IsNullOrEmpty(texts.decor))
                Tag(decor.transform, "Rotulo", texts.decor, DecorLabelSize, Palette.Paper, Palette.BlueDark,
                    new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -8f));

            var sign = Place(root, "Placa", "placa_" + scenario, pieces, manifest);
            if (sign != null) SignText(sign.transform, texts.label, texts.subtitle);

            Place(root, "Relogio", "relogio", pieces, manifest);
            Place(root, "Piso", "piso", pieces, manifest);

            stage.owner = Place(root, "Responsavel", "responsavel_0", pieces, manifest);
            stage.ownerVariants = new[]
            {
                LoadSprite("responsavel_0"), LoadSprite("responsavel_1"),
                LoadSprite("responsavel_2"), LoadSprite("responsavel_3")
            };
            stage.inspector = Place(root, "Fiscal", "fiscal", pieces, manifest);

            var counter = Place(root, "Balcao", "balcao_" + scenario, pieces, manifest);
            if (counter != null && CounterLabels.TryGetValue(key, out var counterText))
                Tag(counter.transform, "Letreiro", counterText, CounterLabelSize, Palette.Paper, Palette.BlueDark,
                    new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.5f), Vector2.zero);

            Place(root, "Tampo", "tampo", pieces, manifest);

            var slots = new List<StageSlot>();
            foreach (var spec in Slots) slots.Add(MakeSlot(root, spec, manifest));
            stage.slots = slots;

            var balloon = Place(root, "Balao", "balao", pieces, manifest);
            if (balloon != null)
            {
                // a arte e uma grade de 16x14 quadradinhos (com a sombra); o corpo
                // branco vai da coluna 1 a 13 e da linha 1 a 8, contando de cima
                var ping = UiKit.Label("BalaoTexto", balloon.transform, "?", 30f, Palette.BlueDark, TextAlignmentOptions.Center);
                UiKit.Frac(UiKit.Rect(ping.gameObject), 1f / 16f, 5f / 14f, 14f / 16f, 13f / 14f);
                ping.fontStyle = FontStyles.Bold;
                ping.enableAutoSizing = true;
                ping.fontSizeMin = 16f;
                ping.fontSizeMax = 30f;
                ping.margin = Vector4.zero;
                stage.speechPing = ping;
            }

            // ponto de onde saem os pixels e onde nascem os pontos: o peito do responsavel
            var burst = UiKit.Node("Explosao", root);
            AnchorAtCss(UiKit.Rect(burst), 753f, 290f, manifest);
            stage.burstOrigin = UiKit.Rect(burst);

            var score = UiKit.Label("Pontos", root, "", 30f, Palette.Ink, TextAlignmentOptions.Center);
            AnchorAtCss(score.rectTransform, 753f, 196f, manifest);
            score.rectTransform.sizeDelta = new Vector2(160f, 44f);
            score.fontStyle = FontStyles.Bold;
            stage.scoreFloat = score;

            var flash = UiKit.Panel("Flash", root, Color.clear);
            UiKit.Full(UiKit.Rect(flash.gameObject));
            flash.raycastTarget = false;
            stage.flash = flash;

            BuildVisitBanner(root, stage);
            return stage;
        }

        /// <summary>Faixa "VISITA X DE Y / CHEGANDO: LOCAL" do HTML, por cima de tudo.</summary>
        static void BuildVisitBanner(Transform root, StageView stage)
        {
            var outline = UiKit.Panel("FaixaVisita", root, Palette.Shadow);
            outline.raycastTarget = false;
            var rect = UiKit.Rect(outline.gameObject);
            UiKit.Centered(rect, 470f, 118f);
            outline.gameObject.AddComponent<CanvasGroup>();

            var body = UiKit.Panel("Fundo", outline.transform, Palette.Ink2);
            body.raycastTarget = false;
            UiKit.Stretch(UiKit.Rect(body.gameObject), 4, 4, 4, 4);
            var band = UiKit.Panel("Faixa", body.transform, Palette.Yellow);
            UiKit.Frac(UiKit.Rect(band.gameObject), 0f, 0f, 1f, 0.07f);

            stage.visitStep = UiKit.Label("Etapa", body.transform, "VISITA 1 DE 5", 18f, Palette.Cyan, TextAlignmentOptions.Center);
            UiKit.Frac(stage.visitStep.rectTransform, 0.04f, 0.58f, 0.96f, 0.92f);
            stage.visitStep.fontStyle = FontStyles.Bold;

            stage.visitName = UiKit.Label("Local", body.transform, "CHEGANDO", 26f, Palette.Yellow, TextAlignmentOptions.Center);
            UiKit.Frac(stage.visitName.rectTransform, 0.04f, 0.12f, 0.96f, 0.60f);
            stage.visitName.fontStyle = FontStyles.Bold;

            stage.visitBanner = rect;
            outline.gameObject.SetActive(false);
        }

        /// <summary>Ancora num ponto do cenario de 900x530, medido de cima para baixo.</summary>
        static void AnchorAtCss(RectTransform rect, float x, float y, Manifest manifest)
        {
            var point = new Vector2(x / manifest.largura, 1f - y / manifest.altura);
            rect.anchorMin = point;
            rect.anchorMax = point;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
        }

        // ------------------------------------------------------------------ texto

        /// <summary>Nome e subtitulo do ambiente na placa, como no HTML.</summary>
        static void SignText(Transform sign, string title, string subtitle)
        {
            var titleLabel = UiKit.Label("Nome", sign, title ?? "", SignTitleSize, Palette.White, TextAlignmentOptions.Center);
            UiKit.Frac(UiKit.Rect(titleLabel.gameObject), 0.03f, 0.42f, 0.97f, 0.92f);
            titleLabel.fontStyle = FontStyles.Bold;

            var subtitleLabel = UiKit.Label("Subtitulo", sign, subtitle ?? "", SignSubtitleSize, Palette.Yellow,
                                            TextAlignmentOptions.Center);
            UiKit.Frac(UiKit.Rect(subtitleLabel.gameObject), 0.03f, 0.10f, 0.97f, 0.44f);
            subtitleLabel.fontStyle = FontStyles.Bold;
        }

        /// <summary>
        /// Etiqueta de papel com contorno escuro, no estilo das plaquinhas do HTML.
        /// O tamanho acompanha o texto: a letra nunca encolhe para caber.
        /// Devolve o texto; o pai do pai e a etiqueta inteira.
        /// </summary>
        static TextMeshProUGUI Tag(Transform parent, string name, string text, float size, Color fill, Color textColor,
                                   Vector2 anchor, Vector2 pivot, Vector2 offset)
        {
            var outline = UiKit.Panel(name, parent, Palette.Shadow);
            outline.raycastTarget = false;
            var rect = UiKit.Rect(outline.gameObject);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = offset;
            Fit(outline.gameObject, 3);

            var body = UiKit.Panel("Fundo", outline.transform, fill);
            body.raycastTarget = false;
            var bodyLayout = body.gameObject.AddComponent<HorizontalLayoutGroup>();
            bodyLayout.padding = new RectOffset(9, 9, 5, 5);
            bodyLayout.childControlWidth = true;
            bodyLayout.childControlHeight = true;
            bodyLayout.childForceExpandWidth = false;
            bodyLayout.childForceExpandHeight = false;

            var label = UiKit.Label("Texto", body.transform, text, size, textColor, TextAlignmentOptions.Center);
            label.fontStyle = FontStyles.Bold;
            label.lineSpacing = -8f;
            return label;
        }

        static void Fit(GameObject node, int padding)
        {
            var layout = node.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var fitter = node.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        // ------------------------------------------------------------------ pecas

        static Image Place(Transform parent, string objectName, string artName,
                           Dictionary<string, Piece> pieces, Manifest manifest)
        {
            if (!pieces.TryGetValue(artName, out var piece))
            {
                Debug.LogWarning("Missao PROCON: a peca \"" + artName + "\" nao esta no manifesto. " +
                                 "Rode ferramentas/extrair-artes/extrair.py.");
                return null;
            }
            return PlaceAt(parent, objectName, LoadSprite(artName), piece.x, piece.y, piece.w, piece.h, manifest);
        }

        static Image PlaceAt(Transform parent, string objectName, Sprite sprite,
                             float x, float y, float w, float h, Manifest manifest)
        {
            var image = UiKit.Panel(objectName, parent, Color.white);
            image.sprite = sprite;
            image.raycastTarget = false;
            if (sprite == null) image.color = Palette.Alpha(Palette.Red, 0.4f);   // falta a arte: aparece

            // o manifesto mede de cima para baixo; a Unity ancora de baixo para cima
            UiKit.Frac(UiKit.Rect(image.gameObject),
                       x / manifest.largura, 1f - (y + h) / manifest.altura,
                       (x + w) / manifest.largura, 1f - y / manifest.altura);
            return image;
        }

        static StageSlot MakeSlot(Transform parent, SlotSpec spec, Manifest manifest)
        {
            var art = PlaceAt(parent, "Encaixe_" + spec.id, LoadSprite(spec.sprite),
                              spec.x, spec.y, spec.w, spec.h, manifest);

            TextMeshProUGUI caption;
            if (spec.mode == CalloutMode.Above)
                caption = Tag(art.transform, "Etiqueta", "", spec.size, spec.fill, spec.text,
                              new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, spec.lift));
            else
                caption = Tag(art.transform, "Etiqueta", "", spec.size, spec.fill, spec.text,
                              new Vector2(0.5f, spec.anchorY), new Vector2(0.5f, 0.5f), Vector2.zero);

            var captionRoot = caption.transform.parent.parent.gameObject;
            captionRoot.SetActive(false);
            art.gameObject.SetActive(false);

            return new StageSlot { id = spec.id, root = art.gameObject, art = art,
                                   caption = caption, captionRoot = captionRoot };
        }

        // ------------------------------------------------------------------ arquivos

        public static Sprite LoadSprite(string artName)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(ArtFolder + artName + ".png");
        }

        static Manifest LoadManifest()
        {
            if (!File.Exists(ManifestPath))
            {
                Debug.LogError("Missao PROCON: nao achei " + ManifestPath + ". A arte dos palcos vem de " +
                               "ferramentas/extrair-artes/extrair.py; rode antes de gerar as cenas.");
                return null;
            }
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath));
            if (manifest == null || manifest.pecas == null || manifest.pecas.Length == 0)
            {
                Debug.LogError("Missao PROCON: manifesto.json vazio ou em formato inesperado.");
                return null;
            }
            return manifest;
        }
    }
}
