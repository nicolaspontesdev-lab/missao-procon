using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Procon.EditorTools
{
    /// <summary>
    /// Visual de programa de auditorio para a interface do telao: placas
    /// arredondadas com borda dourada e brilho de vidro, botoes em pilula com selo
    /// da tecla, placar em pilulas, lampadas em volta do cenario e do resultado.
    /// So muda a aparencia: os objetos e as referencias do GameController sao os
    /// mesmos que o ProconBuilder criou.
    /// </summary>
    public static class ShowSkin
    {
        const string Folder = "Assets/Art/Show/UI";

        static readonly Color Gold = new Color(1f, 0.8f, 0.28f);
        static readonly Color GoldSoft = new Color(1f, 0.8f, 0.28f, 0.55f);
        static readonly Color Glass = new Color(0.07f, 0.05f, 0.2f, 0.9f);
        static readonly Color GlassLight = new Color(0.13f, 0.12f, 0.36f, 0.95f);
        static readonly Color Neon = new Color(0.33f, 0.85f, 0.95f, 0.75f);

        static Sprite plate, plateRim, pill, pillRim, disc, ring, gloss, backdrop, rays, bulb;

        // ================================================================== sprites

        /// <summary>Desenha os sprites da interface (uma vez) e prepara o 9-slice.</summary>
        public static void EnsureSprites()
        {
            Directory.CreateDirectory(Folder);
            Write("placa", 96, 96, 28, (x, y) => Rounded(x, y, 96, 96, 22f, false));
            Write("placa_borda", 96, 96, 28, (x, y) => Rounded(x, y, 96, 96, 22f, true));
            Write("pilula", 128, 64, 31, (x, y) => Rounded(x, y, 128, 64, 31.5f, false));
            Write("pilula_borda", 128, 64, 31, (x, y) => Rounded(x, y, 128, 64, 31.5f, true));
            Write("circulo", 64, 64, 0, (x, y) => Rounded(x, y, 64, 64, 31.5f, false));
            Write("anel", 64, 64, 0, (x, y) => Rounded(x, y, 64, 64, 31.5f, true));
            Write("vidro", 64, 32, 0, (x, y) => new Color(1f, 1f, 1f, 0.18f * Mathf.Pow(y / 31f, 1.6f)));
            Write("fundo_telao", 320, 180, 0, (x, y) =>
            {
                var d = Vector2.Distance(new Vector2(x, y), new Vector2(160f, 110f)) / 190f;
                var c = Color.Lerp(new Color(0.24f, 0.08f, 0.48f), new Color(0.03f, 0.04f, 0.14f), Mathf.SmoothStep(0f, 1f, d));
                c.a = 1f;
                return c;
            });
            Write("raios", 512, 512, 0, (x, y) =>
            {
                var p = new Vector2(x - 255.5f, y - 255.5f);
                var angle = Mathf.Atan2(p.y, p.x);
                var stripe = Mathf.Pow(0.5f + 0.5f * Mathf.Cos(angle * 14f), 6f);
                var fall = Mathf.Clamp01(1f - p.magnitude / 256f);
                return new Color(1f, 1f, 1f, stripe * fall);
            });
            AssetDatabase.Refresh();

            plate = Load("placa"); plateRim = Load("placa_borda");
            pill = Load("pilula"); pillRim = Load("pilula_borda");
            disc = Load("circulo"); ring = Load("anel"); gloss = Load("vidro");
            backdrop = Load("fundo_telao"); rays = Load("raios");
            bulb = ShowBuilder.MenuSprite("lampada");
        }

        /// <summary>
        /// Retangulo arredondado com degrade de cima para baixo (claro em cima),
        /// ou so o contorno de 4 px, com borda suavizada.
        /// </summary>
        static Color Rounded(int x, int y, int w, int h, float radius, bool rimOnly)
        {
            var p = new Vector2(x + 0.5f - w / 2f, y + 0.5f - h / 2f);
            var half = new Vector2(w / 2f, h / 2f);
            var q = new Vector2(Mathf.Abs(p.x) - half.x + radius, Mathf.Abs(p.y) - half.y + radius);
            var d = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
            if (rimOnly)
                return new Color(1f, 1f, 1f, Mathf.Clamp01(2.5f - Mathf.Abs(d + 2.5f)));
            var shade = Mathf.Lerp(0.74f, 1f, (float)y / (h - 1));
            return new Color(shade, shade, shade, Mathf.Clamp01(0.5f - d));
        }

        static void Write(string name, int width, int height, int border, System.Func<int, int, Color> pixel)
        {
            var path = Folder + "/" + name + ".png";
            if (!File.Exists(path))
            {
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                for (var y = 0; y < height; y++)
                    for (var x = 0; x < width; x++)
                        texture.SetPixel(x, y, pixel(x, y));
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path);
            }
            if (border > 0 && AssetImporter.GetAtPath(path) is TextureImporter importer &&
                importer.spriteBorder != new Vector4(border, border, border, border))
            {
                importer.spriteBorder = new Vector4(border, border, border, border);
                importer.SaveAndReimport();
            }
        }

        static Sprite Load(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(Folder + "/" + name + ".png");

        // ================================================================== blocos

        /// <summary>
        /// Transforma um Image chapado numa placa: fundo arredondado em degrade,
        /// contorno colorido e reflexo de vidro na metade de cima.
        /// cornerScale maior deixa os cantos mais fechados (para pecas pequenas).
        /// </summary>
        public static void Plate(Image image, Color fill, Color rim, float cornerScale = 1f, bool shine = true, bool round = false)
        {
            if (image == null) return;
            image.sprite = round ? pill : plate;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = cornerScale;
            image.color = fill;

            var t = image.transform;
            if (shine)
            {
                var glass = Decor(t, "Vidro", gloss, Color.white, Image.Type.Simple, cornerScale);
                var rect = glass.rectTransform;
                rect.anchorMin = new Vector2(0f, 0.5f);
                rect.anchorMax = Vector2.one;
                var inset = 10f / cornerScale;
                rect.offsetMin = new Vector2(inset, 0f);
                rect.offsetMax = new Vector2(-inset, -3f);
                glass.transform.SetSiblingIndex(0);
            }
            var border = Decor(t, "Contorno", round ? pillRim : plateRim, rim, Image.Type.Sliced, cornerScale);
            UiKit.Full(border.rectTransform);
            border.transform.SetSiblingIndex(shine ? 1 : 0);
        }

        static Image Decor(Transform parent, string name, Sprite sprite, Color color, Image.Type type, float cornerScale)
        {
            var image = UiKit.Panel(name, parent, color);
            image.sprite = sprite;
            image.type = type;
            image.pixelsPerUnitMultiplier = cornerScale;
            image.raycastTarget = false;
            image.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            return image;
        }

        /// <summary>Sombra do texto: copia escura atras, deslocada para baixo.</summary>
        static void TextShadow(TMP_Text text, float offset = 3f)
        {
            if (text == null) return;
            var copy = Object.Instantiate(text.gameObject, text.transform.parent);
            copy.name = text.name + "Sombra";
            var shadow = copy.GetComponent<TMP_Text>();
            shadow.color = new Color(0f, 0f, 0f, 0.55f);
            var rect = (RectTransform)copy.transform;
            rect.anchoredPosition += new Vector2(offset, -offset);
            copy.transform.SetSiblingIndex(text.transform.GetSiblingIndex());
            var layout = copy.GetComponent<LayoutElement>() ?? copy.AddComponent<LayoutElement>();
            layout.ignoreLayout = true;
        }

        /// <summary>
        /// Lampadas presas nas bordas de um retangulo de tamanho qualquer: ficam
        /// ancoradas por fracao, entao acompanham o tamanho final do layout.
        /// </summary>
        static Marquee Bulbs(Transform parent, int across, int down, float size, float outset)
        {
            var list = new List<Graphic>();
            void Add(Vector2 anchor, Vector2 push)
            {
                var image = UiKit.Panel("Lampada", parent, Color.white);
                image.sprite = bulb;
                image.raycastTarget = false;
                image.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                var rect = image.rectTransform;
                rect.anchorMin = rect.anchorMax = anchor;
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(size, size);
                rect.anchoredPosition = push * outset;
                list.Add(image);
            }
            // sentido horario a partir do canto de baixo a esquerda
            for (var i = 0; i < down; i++) Add(new Vector2(0f, (float)i / down), Vector2.left);
            for (var i = 0; i < across; i++) Add(new Vector2((float)i / across, 1f), Vector2.up);
            for (var i = 0; i < down; i++) Add(new Vector2(1f, 1f - (float)i / down), Vector2.right);
            for (var i = 0; i < across; i++) Add(new Vector2(1f - (float)i / across, 0f), Vector2.down);

            var marquee = parent.gameObject.AddComponent<Marquee>();
            marquee.uiBulbs = list.ToArray();
            marquee.on = new Color(1f, 0.9f, 0.55f);
            marquee.off = new Color(0.4f, 0.26f, 0.12f);
            return marquee;
        }

        static Image ImageOf(Component c) => c != null ? c.GetComponent<Image>() : null;

        static Image EnsureImage(GameObject node)
        {
            var image = node.GetComponent<Image>();
            if (image == null) image = node.AddComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        // ================================================================== tela do jogo

        public static void SkinGame(Transform frame, GameController c)
        {
            EnsureSprites();

            // ---- fundo do telao: luz roxa no centro e raios girando devagar
            var canvas = frame.parent;
            var background = canvas.Find("Background")?.GetComponent<Image>();
            if (background != null)
            {
                background.sprite = backdrop;
                background.color = Color.white;
                var sun = Decor(background.transform, "Raios", rays, new Color(1f, 0.85f, 0.45f, 0.07f), Image.Type.Simple, 1f);
                var sunRect = sun.rectTransform;
                sunRect.anchorMin = sunRect.anchorMax = new Vector2(0.5f, 0.35f);
                sunRect.sizeDelta = new Vector2(2200f, 2200f);
                var sweep = sun.gameObject.AddComponent<Sweep>();
                sweep.angle = 8f;
                sweep.period = 24f;
                canvas.gameObject.AddComponent<RectMask2D>();
            }
            var cabinet = frame.GetComponent<Image>();
            if (cabinet != null) cabinet.color = Color.clear;

            // ---- faixa do titulo
            var topBar = frame.Find("TopBar")?.GetComponent<Image>();
            Plate(topBar, new Color(0.05f, 0.03f, 0.16f, 0.88f), Gold, 2f);
            var title = topBar != null ? topBar.transform.Find("Title")?.GetComponent<TMP_Text>() : null;
            if (title != null) { title.fontStyle = FontStyles.Bold; title.fontSize = 40; title.color = Gold; TextShadow(title); }
            Badge(c.levelBadge, new Color(0.35f, 0.15f, 0.7f, 0.95f), Gold);
            if (c.locationBadge != null) c.locationBadge.fontStyle = FontStyles.Bold;

            // ---- placar em pilulas
            var hud = frame.Find("Hud");
            if (hud != null)
            {
                hud.GetComponent<Image>().color = Color.clear;
                var list = hud.GetComponent<HorizontalLayoutGroup>();
                if (list != null) { list.spacing = 14f; list.padding = new RectOffset(4, 4, 6, 6); }
                foreach (var value in new[] { c.caseCounter, c.scoreValue, c.correctValue, c.wrongValue, c.comboBadge })
                    StatPill(value, value == c.scoreValue);
            }

            // ---- progresso: trilho arredondado e enchimento dourado
            var track = frame.Find("ProgressTrack")?.GetComponent<Image>();
            if (track != null)
            {
                Plate(track, new Color(0.03f, 0.02f, 0.1f, 0.9f), GoldSoft, 5f, false, true);
                var rect = track.rectTransform;
                rect.offsetMax = new Vector2(rect.offsetMax.x, rect.offsetMax.y + 2f);
            }
            if (c.progressFill != null)
            {
                var fill = c.progressFill.GetComponent<Image>();
                fill.sprite = pill; fill.type = Image.Type.Sliced; fill.pixelsPerUnitMultiplier = 5f;
                fill.color = Gold;
            }

            // ---- rota: cada parada vira uma pilula
            if (c.routeBar != null && c.routeStopTemplate != null)
            {
                var stop = UiKit.Node("ParadaModelo", c.routeBar);
                var pillImage = stop.AddComponent<Image>();
                pillImage.sprite = pill; pillImage.type = Image.Type.Sliced; pillImage.pixelsPerUnitMultiplier = 2.2f;
                pillImage.color = Palette.Alpha(Palette.Shadow, 0.6f);
                pillImage.raycastTarget = false;
                c.routeStopTemplate.transform.SetParent(stop.transform, false);
                UiKit.Full(c.routeStopTemplate.rectTransform);
                c.routeStopTemplate.fontStyle = FontStyles.Bold;
                c.routeStopTemplate.fontSize = 17;
                c.routeStopRoot = stop;
            }

            // ---- coluna da pergunta
            var dialog = ImageOf(c.dialogScroll);
            Plate(dialog, Glass, GoldSoft, 1.4f, false);
            if (c.caseTag != null) c.caseTag.fontStyle = FontStyles.Bold;
            if (c.questionText != null) { c.questionText.fontSize = 30; c.questionText.fontStyle = FontStyles.Bold; }
            if (c.instructionText != null) { c.instructionText.fontStyle = FontStyles.Bold; c.instructionText.color = Gold; }

            AnswerButton(c.violationButton, c.violationBackground, "É INFRAÇÃO", "V");
            AnswerButton(c.lawfulButton, c.lawfulBackground, "NÃO É INFRAÇÃO", "F");

            foreach (var clue in c.clues)
            {
                if (clue == null) continue;
                Plate(ImageOf(clue.button), GlassLight, Neon, 2.4f);
                if (clue.icon != null) { clue.icon.sprite = disc; clue.icon.preserveAspect = true; }
                if (clue.card != null) Plate(clue.card.GetComponent<Image>(), new Color(0.12f, 0.15f, 0.42f, 0.95f), Neon, 2.4f, false);
            }

            if (c.feedbackPanel != null)
            {
                Plate(c.feedbackPanel.GetComponent<Image>(), new Color(0.08f, 0.05f, 0.24f, 0.97f), Gold, 1.6f, false);
                if (c.feedbackStripe != null)
                {
                    c.feedbackStripe.sprite = pill; c.feedbackStripe.type = Image.Type.Sliced;
                    c.feedbackStripe.pixelsPerUnitMultiplier = 4f;
                    var stripeLayout = c.feedbackStripe.GetComponent<LayoutElement>();
                    if (stripeLayout != null) stripeLayout.preferredHeight = 10f;
                }
                if (c.feedbackTitle != null) { c.feedbackTitle.fontSize = 34; c.feedbackTitle.fontStyle = FontStyles.Bold; }
                if (c.feedbackPoints != null) c.feedbackPoints.fontStyle = FontStyles.Bold;
            }
            if (c.nextButton != null)
            {
                Plate(c.nextButton.GetComponent<Image>(), Gold, new Color(1f, 0.95f, 0.75f), 1.3f, true, true);
                if (c.nextButtonLabel != null) { c.nextButtonLabel.color = Palette.Ink; c.nextButtonLabel.fontStyle = FontStyles.Bold; }
            }

            // ---- coluna do cenario: placa do local e o cenario com lampadas, como um monitor do palco
            if (c.locationName != null)
            {
                Plate(c.locationName.transform.parent.GetComponent<Image>(), new Color(0.05f, 0.03f, 0.16f, 0.9f), GoldSoft, 1.8f);
                c.locationName.fontStyle = FontStyles.Bold;
            }
            if (c.stages != null && c.stages.Length > 0)
            {
                var box = c.stages[0].transform.parent;
                Plate(box.GetComponent<Image>(), new Color(0.02f, 0.01f, 0.06f, 1f), Gold, 1.6f, false);
                Bulbs(box, 22, 13, 12f, 3f);
            }
            if (c.visitLine != null) { c.visitLine.color = Gold; c.visitLine.fontStyle = FontStyles.Bold; }

            SkinFinal(c);
        }

        static void Badge(TMP_Text label, Color fill, Color rim)
        {
            if (label == null) return;
            var holder = UiKit.Node(label.name + "Selo", label.transform.parent);
            var rect = UiKit.Rect(holder);
            var source = label.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2((source.anchorMin.x + source.anchorMax.x) / 2f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(260f, 50f);
            holder.transform.SetSiblingIndex(label.transform.GetSiblingIndex());
            Plate(EnsureImage(holder), fill, rim, 1.6f, true, true);
            label.fontStyle = FontStyles.Bold;
            label.color = Color.white;
        }

        static void StatPill(TMP_Text value, bool highlight)
        {
            if (value == null) return;
            var node = value.transform.parent.gameObject;
            Plate(EnsureImage(node), highlight ? new Color(0.28f, 0.16f, 0.05f, 0.95f) : new Color(0.06f, 0.05f, 0.2f, 0.9f),
                  highlight ? Gold : Neon, 2.2f);
            value.fontStyle = FontStyles.Bold;
            if (highlight) value.fontSize = 30;
        }

        static void AnswerButton(Button button, Image background, string text, string key)
        {
            if (button == null || background == null) return;
            Plate(background, background.color, Gold, 1.2f, true, true);
            var label = button.GetComponentInChildren<TMP_Text>();
            if (label != null)
            {
                label.text = text;
                label.enableWordWrapping = false;
                label.enableAutoSizing = true;
                label.fontSizeMin = 18f;
                label.fontSizeMax = 30f;
                label.fontStyle = FontStyles.Bold;
                UiKit.Stretch(label.rectTransform, 86, 6, 16, 6);
            }
            // selo redondo com a tecla, como as letras das alternativas dos programas de perguntas
            var badge = UiKit.Panel("Tecla", button.transform, new Color(0.06f, 0.04f, 0.16f, 0.95f));
            badge.sprite = disc;
            badge.raycastTarget = false;
            var rect = badge.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(62f, 62f);
            rect.anchoredPosition = new Vector2(16f, 0f);
            var badgeRing = UiKit.Panel("Anel", badge.transform, Gold);
            badgeRing.sprite = ring;
            badgeRing.raycastTarget = false;
            UiKit.Full(badgeRing.rectTransform);
            var letter = UiKit.Label("Letra", badge.transform, key, 32, Gold, TextAlignmentOptions.Center);
            letter.fontStyle = FontStyles.Bold;
            UiKit.Full(letter.rectTransform);
        }

        // ================================================================== menu

        public static void SkinMenu(Transform frame, MenuController m)
        {
            EnsureSprites();

            var ticker = frame.Find("Ticker")?.GetComponent<Image>();
            if (ticker != null) ticker.color = new Color(0.03f, 0.02f, 0.08f, 0.8f);
            var subtitle = frame.Find("Subtitle")?.GetComponent<TMP_Text>();
            if (subtitle != null) { subtitle.color = Gold; TextShadow(subtitle); }

            MenuButton(m.playButton, Gold, Palette.Ink);
            MenuButton(m.helpButton, new Color(0.14f, 0.1f, 0.38f, 0.95f), Color.white);
            MenuButton(m.creditsButton, new Color(0.14f, 0.1f, 0.38f, 0.95f), Color.white);
            MenuButton(m.quitButton, new Color(0.55f, 0.1f, 0.2f, 0.95f), Color.white);
            MenuButton(m.startButton, Gold, Palette.Ink);
            MenuButton(m.backFromLevelButton, new Color(0.14f, 0.1f, 0.38f, 0.95f), Color.white);
            MenuButton(m.sfxButton, new Color(0.05f, 0.04f, 0.14f, 0.9f), Palette.Cyan);

            foreach (var option in m.levelOptions)
            {
                if (option == null) continue;
                Plate(option.background, option.background.color, Neon, 1.6f);
                if (option.title != null) { option.title.fontStyle = FontStyles.Bold; option.title.fontSize = 34; }
            }
            if (m.levelStatus != null) { m.levelStatus.fontStyle = FontStyles.Bold; TextShadow(m.levelStatus, 2f); }
            var levelTitle = m.levelPanel != null ? m.levelPanel.transform.Find("LevelTitle")?.GetComponent<TMP_Text>() : null;
            if (levelTitle != null) { levelTitle.fontStyle = FontStyles.Bold; TextShadow(levelTitle); }

            foreach (var panel in new[] { m.helpPanel, m.creditsPanel })
            {
                if (panel == null) continue;
                Plate(panel.GetComponent<Image>(), new Color(0.06f, 0.04f, 0.18f, 0.94f), Gold, 1.2f, false);
                Bulbs(panel.transform, 26, 10, 14f, 3f);
            }
            foreach (var back in new[] { m.backFromHelpButton, m.backFromCreditsButton })
                MenuButton(back, Gold, Palette.Ink);
        }

        static void MenuButton(Button button, Color fill, Color text)
        {
            if (button == null) return;
            Plate(button.GetComponent<Image>(), fill, new Color(1f, 0.9f, 0.6f), 1.2f, true, true);
            var label = button.GetComponentInChildren<TMP_Text>();
            if (label != null) { label.color = text; label.fontStyle = FontStyles.Bold; }
        }

        static void SkinFinal(GameController c)
        {
            if (c.finalPanel == null) return;
            var overlay = c.finalPanel.GetComponent<Image>();
            overlay.color = new Color(0.04f, 0.02f, 0.1f, 0.9f);
            var card = c.finalPanel.transform.Find("Card")?.GetComponent<Image>();
            if (card == null) return;
            Plate(card, new Color(0.12f, 0.06f, 0.3f, 0.98f), Gold, 1f);
            Bulbs(card.transform, 28, 12, 16f, 4f);

            var title = card.transform.Find("Title")?.GetComponent<TMP_Text>();
            if (title != null) { title.fontSize = 54; title.fontStyle = FontStyles.Bold; title.color = Gold; }
            if (c.rankLabel != null) c.rankLabel.fontStyle = FontStyles.Bold;
            foreach (var value in new[] { c.finalAnswered, c.finalScore, c.finalCorrect, c.finalWrong, c.finalPercent })
                StatPill(value, value == c.finalScore);

            Plate(ImageOf(c.reviewButton), Palette.Orange, Gold, 1.3f, true, true);
            Plate(ImageOf(c.restartButton), Gold, new Color(1f, 0.95f, 0.75f), 1.3f, true, true);
            var restartLabel = c.restartButton != null ? c.restartButton.GetComponentInChildren<TMP_Text>() : null;
            if (restartLabel != null) restartLabel.color = Palette.Ink;
            Plate(ImageOf(c.menuButton), new Color(0.1f, 0.08f, 0.26f), Gold, 1.3f, true, true);
            foreach (var button in new[] { c.reviewButton, c.restartButton, c.menuButton })
            {
                var label = button != null ? button.GetComponentInChildren<TMP_Text>() : null;
                if (label != null) label.fontStyle = FontStyles.Bold;
            }
            if (c.reviewPanel != null) Plate(c.reviewPanel.GetComponent<Image>(), Glass, GoldSoft, 1.6f, false);
        }
    }
}
