using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Procon.EditorTools
{
    /// <summary>
    /// Atalhos para montar a interface por codigo. So roda no editor: o
    /// resultado e uma cena normal, com GameObjects de verdade, que da para
    /// arrastar e editar no Inspector depois.
    /// </summary>
    public static class UiKit
    {
        public class ButtonParts
        {
            public GameObject root;
            public Button button;
            public Image background;
            public TextMeshProUGUI label;
        }

        public static GameObject Node(string name, Transform parent)
        {
            var node = new GameObject(name, typeof(RectTransform));
            node.transform.SetParent(parent, false);
            return node;
        }

        public static RectTransform Rect(GameObject node) => (RectTransform)node.transform;

        public static Image Panel(string name, Transform parent, Color color)
        {
            var node = Node(name, parent);
            var image = node.AddComponent<Image>();
            image.color = color;
            return image;
        }

        public static TextMeshProUGUI Label(
            string name, Transform parent, string text, float size, Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft)
        {
            var node = Node(name, parent);
            var label = node.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.raycastTarget = false;
            return label;
        }

        public static ButtonParts Button(
            string name, Transform parent, string text, Color background, Color foreground, float size)
        {
            var node = Node(name, parent);
            var image = node.AddComponent<Image>();
            image.color = background;

            var button = node.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            button.colors = new ColorBlock
            {
                normalColor = Color.white,
                highlightedColor = new Color(0.82f, 0.86f, 1f, 1f),
                pressedColor = new Color(0.64f, 0.68f, 0.82f, 1f),
                selectedColor = Color.white,
                disabledColor = new Color(1f, 1f, 1f, 0.45f),
                colorMultiplier = 1f,
                fadeDuration = 0.08f
            };

            var label = Label(name + "Label", node.transform, text, size, foreground, TextAlignmentOptions.Center);
            Stretch(Rect(label.gameObject), 12, 6, 12, 6);

            return new ButtonParts { root = node, button = button, background = image, label = label };
        }

        // ---------------------------------------------------------------- ancoras

        public static void Stretch(RectTransform rect, float left, float top, float right, float bottom)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        public static void Full(RectTransform rect) => Stretch(rect, 0, 0, 0, 0);

        /// <summary>Faixa colada no topo do pai.</summary>
        public static void TopBand(RectTransform rect, float fromTop, float height, float left = 0, float right = 0)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(left, -(fromTop + height));
            rect.offsetMax = new Vector2(-right, -fromTop);
        }

        /// <summary>Faixa colada na base do pai.</summary>
        public static void BottomBand(RectTransform rect, float fromBottom, float height, float left = 0, float right = 0)
        {
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = new Vector2(left, fromBottom);
            rect.offsetMax = new Vector2(-right, fromBottom + height);
        }

        /// <summary>Ancoras em fracao do pai: otimo para blocagem, escala junto.</summary>
        public static void Frac(RectTransform rect, float xMin, float yMin, float xMax, float yMax)
        {
            rect.anchorMin = new Vector2(xMin, yMin);
            rect.anchorMax = new Vector2(xMax, yMax);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        public static void Centered(RectTransform rect, float width, float height, float offsetY = 0f)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(0f, offsetY);
        }

        // ---------------------------------------------------------------- layout

        public static VerticalLayoutGroup VList(GameObject node, float spacing, int padding)
        {
            var layout = node.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childAlignment = TextAnchor.UpperCenter;
            return layout;
        }

        public static HorizontalLayoutGroup HList(GameObject node, float spacing, int padding)
        {
            var layout = node.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childAlignment = TextAnchor.MiddleCenter;
            return layout;
        }

        public static ContentSizeFitter FitHeight(GameObject node)
        {
            var fitter = node.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return fitter;
        }

        public static LayoutElement Height(GameObject node, float preferred)
        {
            var element = node.GetComponent<LayoutElement>();
            if (element == null) element = node.AddComponent<LayoutElement>();
            element.preferredHeight = preferred;
            element.minHeight = preferred;
            element.flexibleHeight = 0f;
            return element;
        }

        /// <summary>ScrollRect vertical pronto: devolve o container onde entra o conteudo.</summary>
        public static ScrollRect VScroll(string name, Transform parent, Color background, out RectTransform content)
        {
            var root = Node(name, parent);
            var image = root.AddComponent<Image>();
            image.color = background;

            var scroll = root.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            var viewport = Node("Viewport", root.transform);
            Stretch(Rect(viewport), 4, 4, 4, 4);
            viewport.AddComponent<RectMask2D>();

            var contentNode = Node("Content", viewport.transform);
            content = Rect(contentNode);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;

            scroll.viewport = Rect(viewport);
            scroll.content = content;

            return scroll;
        }
    }
}
