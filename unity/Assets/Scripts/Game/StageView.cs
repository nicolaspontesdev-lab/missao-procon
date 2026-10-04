using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Procon
{
    /// <summary>Um ponto de encaixe do palco: um objeto que o caso liga ou desliga.</summary>
    [Serializable]
    public class StageSlot
    {
        [Tooltip("Nome usado pelo caderno visual, por exemplo produto-principal.")]
        public string id = "";

        [Tooltip("Ligado e desligado conforme o caso.")]
        public GameObject root;

        [Tooltip("O desenho do objeto. O caso pode trocar o sprite e a cor.")]
        public Image art;

        [Tooltip("Texto do caso sobre o objeto. Fica vazio quando o caso nao usa.")]
        public TMP_Text caption;

        [Tooltip("A etiqueta que envolve o texto. Some quando o texto esta vazio.")]
        public GameObject captionRoot;

        public void SetCaption(string value)
        {
            var text = value ?? "";
            if (caption != null) caption.text = text;
            if (captionRoot != null) captionRoot.SetActive(text.Length > 0);
        }

        Sprite originalSprite;
        Color originalTint;
        bool saved;

        /// <summary>Animacao de queda em curso, para nao rodar duas ao mesmo tempo.</summary>
        [NonSerialized] public Coroutine dropping;

        public void Remember()
        {
            if (art == null || saved) return;
            originalSprite = art.sprite;
            originalTint = art.color;
            saved = true;
        }

        public void Restore()
        {
            if (art == null || !saved) return;
            art.sprite = originalSprite;
            art.color = originalTint;
        }
    }

    /// <summary>
    /// Um dos cinco palcos. A arte vem do jogo em HTML (Assets/Art/Cenario) e e
    /// montada pelo editor; aqui so entra o que muda de caso para caso.
    /// </summary>
    public class StageView : MonoBehaviour
    {
        [Header("Identidade")]
        public ScenarioKey scenario;

        [Header("Personagens")]
        [Tooltip("Desenho do responsavel pelo estabelecimento.")]
        public Image owner;
        [Tooltip("As quatro aparencias do responsavel, iguais as do jogo em HTML.")]
        public Sprite[] ownerVariants = new Sprite[0];
        [Tooltip("Desenho do fiscal. Nao varia: e sempre o jogador.")]
        public Image inspector;
        public TMP_Text speechPing;

        [Header("Reacao ao veredito")]
        [Tooltip("Camada que pisca verde no acerto e vermelho no erro, como no HTML.")]
        public Image flash;
        [Tooltip("Onde nascem os pixels que explodem na resposta.")]
        public RectTransform burstOrigin;
        [Tooltip("Pontos ganhos, que sobem e somem.")]
        public TMP_Text scoreFloat;

        [Header("Chegada a um novo local")]
        public RectTransform visitBanner;
        public TMP_Text visitStep;
        public TMP_Text visitName;

        [Header("Encaixes")]
        public List<StageSlot> slots = new List<StageSlot>();

        static readonly Color FlashCorrect = new Color(0.192f, 0.722f, 0.420f, 0.30f);
        static readonly Color FlashWrong = new Color(0.847f, 0.227f, 0.306f, 0.28f);

        Coroutine flashing;
        Coroutine reacting;
        Coroutine inspectorReacting;
        Vector2 ownerBase, inspectorBase, bubbleBase, scoreBase;
        bool basesSaved;

        // enquanto um personagem entra ou reage, a respiracao espera
        bool ownerBusy, inspectorBusy;

        void Awake()
        {
            foreach (var slot in slots) slot?.Remember();
            if (flash != null) flash.color = Color.clear;
            SaveBases();
            HideTransient();
        }

        void OnEnable()
        {
            if (speechPing != null) StartCoroutine(BubblePing());
            StartCoroutine(Breathe());
        }

        void OnDisable()
        {
            StopAllCoroutines();
            flashing = null;
            reacting = null;
            inspectorReacting = null;
            ownerBusy = inspectorBusy = false;
            foreach (var slot in slots) if (slot != null) slot.dropping = null;
            ResetPoses();
            HideTransient();
        }

        void SaveBases()
        {
            if (basesSaved) return;
            if (owner != null) ownerBase = owner.rectTransform.anchoredPosition;
            if (inspector != null) inspectorBase = inspector.rectTransform.anchoredPosition;
            if (speechPing != null) bubbleBase = BubbleRect().anchoredPosition;
            if (scoreFloat != null) scoreBase = scoreFloat.rectTransform.anchoredPosition;
            basesSaved = true;
        }

        void ResetPoses()
        {
            if (!basesSaved) return;
            if (owner != null)
            {
                owner.rectTransform.anchoredPosition = ownerBase;
                owner.rectTransform.localEulerAngles = Vector3.zero;
            }
            if (inspector != null) inspector.rectTransform.anchoredPosition = inspectorBase;
            if (speechPing != null) BubbleRect().anchoredPosition = bubbleBase;
        }

        void HideTransient()
        {
            if (visitBanner != null) visitBanner.gameObject.SetActive(false);
            if (scoreFloat != null) scoreFloat.gameObject.SetActive(false);
        }

        RectTransform BubbleRect() => (RectTransform)speechPing.transform.parent;

        /// <summary>Largura do palco em unidades da tela, para converter os px do CSS.</summary>
        float CssToUnits(float cssPixels) => cssPixels / 900f * ((RectTransform)transform).rect.width;

        // ------------------------------------------------------------------ animacoes

        /// <summary>
        /// Chegada a um local novo: o fiscal entra pela esquerda (inspector-arrives,
        /// 1s em 8 degraus), o responsavel tambem (consumer-enter, 0,55s em 5) e a
        /// faixa VISITA X DE Y aparece e some (visit-banner, 2,1s em 7).
        /// </summary>
        public void PlayArrival(int visit, int total, string place)
        {
            if (!isActiveAndEnabled) return;
            SaveBases();

            if (inspector != null)
            {
                inspectorBusy = true;
                StartCoroutine(PixelMotion.Steps(1f, 8, t =>
                {
                    // passos: um pequeno pulo a cada degrau, como quem anda
                    var step = Mathf.RoundToInt(t * 8f) % 2 == 1 ? CssToUnits(4f) : 0f;
                    inspector.rectTransform.anchoredPosition = inspectorBase + new Vector2(CssToUnits(-260f) * (1f - t), t >= 1f ? 0f : step);
                    if (t >= 1f) inspectorBusy = false;
                }));
            }

            if (owner != null)
            {
                ownerBusy = true;
                StartCoroutine(PixelMotion.Steps(0.55f, 5, t =>
                {
                    owner.rectTransform.anchoredPosition = ownerBase + new Vector2(CssToUnits(-220f) * (1f - t), 0f);
                    if (t >= 1f) ownerBusy = false;
                }));
            }

            if (visitBanner == null) return;
            if (visitStep != null) visitStep.text = "VISITA " + visit + " DE " + total;
            if (visitName != null) visitName.text = "CHEGANDO: " + place;
            var group = visitBanner.GetComponent<CanvasGroup>();
            visitBanner.gameObject.SetActive(true);
            StartCoroutine(PixelMotion.Steps(2.1f, 7, t =>
            {
                // 0 a 12%: cresce e aparece; ate 70%: parado; depois sobe e some
                float alpha, scale, rise;
                if (t <= 0.12f) { var k = t / 0.12f; alpha = k; scale = Mathf.Lerp(0.7f, 1f, k); rise = 0f; }
                else if (t <= 0.70f) { alpha = 1f; scale = 1f; rise = 0f; }
                else { var k = (t - 0.70f) / 0.30f; alpha = 1f - k; scale = 1f; rise = k; }
                visitBanner.localScale = Vector3.one * scale;
                visitBanner.anchoredPosition = new Vector2(0f, CssToUnits(40f) * rise);
                if (group != null) group.alpha = alpha;
                if (t >= 1f) visitBanner.gameObject.SetActive(false);
            }));
        }

        /// <summary>
        /// Reacao do responsavel: pula no acerto (happy-jump) e balanca no erro
        /// (sad-wobble). Junto vem o flash da cena e a explosao de pixels.
        /// </summary>
        public void React(bool correct)
        {
            Flash(correct);
            if (!isActiveAndEnabled || owner == null) return;
            SaveBases();
            if (reacting != null) StopCoroutine(reacting);
            var rect = owner.rectTransform;
            ownerBusy = true;
            reacting = correct
                ? StartCoroutine(PixelMotion.Steps(0.6f, 6, t =>
                {
                    rect.anchoredPosition = ownerBase + new Vector2(0f, CssToUnits(15f) * PixelMotion.Bounce(t));
                    if (t >= 1f) ownerBusy = false;
                }))
                : StartCoroutine(PixelMotion.Steps(0.6f, 6, t =>
                {
                    rect.localEulerAngles = new Vector3(0f, 0f, Mathf.Sin(t * Mathf.PI * 2f) * 4f);
                    rect.anchoredPosition = ownerBase;
                    if (t >= 1f) ownerBusy = false;
                }));

            // o fiscal tambem reage: aceno de cabeca no acerto, "nao" no erro
            if (inspector != null)
            {
                if (inspectorReacting != null) StopCoroutine(inspectorReacting);
                var fiscal = inspector.rectTransform;
                inspectorBusy = true;
                inspectorReacting = correct
                    ? StartCoroutine(PixelMotion.Steps(0.5f, 4, t =>
                    {
                        fiscal.anchoredPosition = inspectorBase + new Vector2(0f, CssToUnits(7f) * PixelMotion.Bounce(t));
                        if (t >= 1f) inspectorBusy = false;
                    }, 0.1f))
                    : StartCoroutine(PixelMotion.Steps(0.5f, 4, t =>
                    {
                        var shake = new[] { 0f, -4f, 4f, -3f, 0f }[Mathf.RoundToInt(t * 4f)];
                        fiscal.anchoredPosition = inspectorBase + new Vector2(CssToUnits(shake), 0f);
                        if (t >= 1f) inspectorBusy = false;
                    }, 0.1f));
            }

            Burst(correct);
        }

        /// <summary>Doze pixels saindo do responsavel, como o pixel-burst do HTML.</summary>
        void Burst(bool correct)
        {
            if (burstOrigin == null) return;
            var colors = correct
                ? new[] { Palette.Green, Palette.Yellow, Palette.Cyan }
                : new[] { Palette.Red, Palette.Orange, Palette.RedDark };
            for (var i = 0; i < 12; i++)
            {
                var piece = new GameObject("Pixel", typeof(RectTransform), typeof(Image));
                var rect = (RectTransform)piece.transform;
                rect.SetParent(burstOrigin, false);
                rect.sizeDelta = Vector2.one * CssToUnits(10f);
                var image = piece.GetComponent<Image>();
                image.color = colors[i % colors.Length];
                image.raycastTarget = false;

                var angle = i / 12f * Mathf.PI * 2f;
                var distance = CssToUnits(70f + (i % 3) * 18f);
                var target = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
                StartCoroutine(PixelMotion.Steps(0.78f, 6, t =>
                {
                    rect.anchoredPosition = target * t;
                    rect.localScale = Vector3.one * Mathf.Lerp(1.35f, 0.2f, t);
                    rect.localEulerAngles = new Vector3(0f, 0f, 90f * t);
                    image.color = new Color(image.color.r, image.color.g, image.color.b, 1f - t);
                    if (t >= 1f) Destroy(piece);
                }));
            }
        }

        /// <summary>Pontos ganhos subindo e sumindo (score-float, 0,85s).</summary>
        public void FloatScore(string text)
        {
            if (!isActiveAndEnabled || scoreFloat == null) return;
            SaveBases();
            scoreFloat.text = text;
            scoreFloat.gameObject.SetActive(true);
            var rect = scoreFloat.rectTransform;
            StartCoroutine(PixelMotion.Steps(0.85f, 8, t =>
            {
                rect.anchoredPosition = scoreBase + new Vector2(0f, CssToUnits(85f) * t);
                rect.localScale = Vector3.one * Mathf.Lerp(0.7f, 1.2f, t);
                scoreFloat.alpha = t < 0.2f ? t / 0.2f : 1f - (t - 0.2f) / 0.8f;
                if (t >= 1f) scoreFloat.gameObject.SetActive(false);
            }));
        }

        /// <summary>O balao sobe e desce sem parar (ping), em dois degraus.</summary>
        IEnumerator BubblePing()
        {
            yield return null;
            SaveBases();
            var rect = BubbleRect();
            var wait = new WaitForSeconds(0.5f);
            var up = false;
            while (true)
            {
                up = !up;
                rect.anchoredPosition = bubbleBase + new Vector2(0f, up ? CssToUnits(5f) : 0f);
                yield return wait;
            }
        }

        /// <summary>
        /// Apaga tudo que pertencia ao caso anterior. Roda sempre antes de montar
        /// o caso novo, para nenhum preco, peso ou prazo sobrar de outro caso.
        /// </summary>
        public void Clear()
        {
            foreach (var slot in slots)
            {
                if (slot == null) continue;
                slot.Remember();
                if (slot.root != null) slot.root.SetActive(false);
                slot.SetCaption("");
                slot.Restore();
            }

            if (flash != null) flash.color = Color.clear;
        }

        public void Apply(CaseVisual visual)
        {
            SetOwnerVariant(visual != null ? visual.ownerVariant : 0);
            if (visual == null) return;

            foreach (var fill in visual.fills)
            {
                if (fill == null || string.IsNullOrEmpty(fill.slotId)) continue;
                var slot = Slot(fill.slotId);
                if (slot == null)
                {
                    Debug.LogWarning("Palco " + scenario + ": o caderno visual pede o encaixe \"" +
                                     fill.slotId + "\", que nao existe neste palco.");
                    continue;
                }

                if (slot.root != null) slot.root.SetActive(true);
                slot.SetCaption(fill.caption);
                DropIn(slot);
                if (slot.art == null) continue;
                if (fill.sprite != null) slot.art.sprite = fill.sprite;
                if (fill.overrideTint) slot.art.color = fill.tint;
            }
        }

        public StageSlot Slot(string id)
        {
            foreach (var slot in slots)
                if (slot != null && slot.id == id) return slot;
            return null;
        }

        public void SetSpeech(string value)
        {
            if (speechPing == null) return;
            var changed = speechPing.text != value;
            speechPing.text = value;
            if (changed && isActiveAndEnabled) Pop(BubbleRect(), 1.25f, 0.3f);
        }

        // ------------------------------------------------------------------ movimentos pequenos

        /// <summary>Objeto do caso caindo no balcao com um quique curto.</summary>
        void DropIn(StageSlot slot)
        {
            if (!isActiveAndEnabled || slot.root == null) return;
            var rect = (RectTransform)slot.root.transform;
            if (slot.dropping != null) StopCoroutine(slot.dropping);
            var height = CssToUnits(34f);
            // desce ate o balcao, passa um pouco e assenta: 0 e a posicao final
            var path = new[] { 1f, 0.55f, 0.15f, -0.12f, 0.04f, 0f };
            slot.dropping = StartCoroutine(PixelMotion.Steps(0.3f, path.Length - 1, t =>
                rect.anchoredPosition = new Vector2(0f, height * path[Mathf.RoundToInt(t * (path.Length - 1))])));
        }

        /// <summary>Salto de escala: cresce ate "peak" e volta (como o combo-pop do HTML).</summary>
        void Pop(RectTransform rect, float peak, float duration)
        {
            StartCoroutine(PixelMotion.Steps(duration, 4, t =>
                rect.localScale = Vector3.one * Mathf.Lerp(1f, peak, PixelMotion.Bounce(t))));
        }

        /// <summary>
        /// Respiracao: os dois personagens sobem e descem 2px, em tempos opostos.
        /// Para enquanto alguem entra em cena ou reage ao veredito.
        /// </summary>
        IEnumerator Breathe()
        {
            yield return null;
            SaveBases();
            var wait = new WaitForSeconds(0.7f);
            var phase = false;
            while (true)
            {
                phase = !phase;
                var lift = new Vector2(0f, CssToUnits(2f));
                if (owner != null && !ownerBusy)
                    owner.rectTransform.anchoredPosition = ownerBase + (phase ? lift : Vector2.zero);
                if (inspector != null && !inspectorBusy)
                    inspector.rectTransform.anchoredPosition = inspectorBase + (phase ? Vector2.zero : lift);
                yield return wait;
            }
        }

        /// <summary>Pisca a cena inteira, como o fx-correct e o fx-wrong do HTML.</summary>
        public void Flash(bool correct)
        {
            if (flash == null || !isActiveAndEnabled) return;
            if (flashing != null) StopCoroutine(flashing);
            flashing = StartCoroutine(FlashRoutine(correct ? FlashCorrect : FlashWrong));
        }

        IEnumerator FlashRoutine(Color color)
        {
            // quatro degraus, como o steps(4) do CSS: pisca sem suavizar
            for (var step = 4; step > 0; step--)
            {
                flash.color = new Color(color.r, color.g, color.b, color.a * step / 4f);
                yield return new WaitForSeconds(0.12f);
            }
            flash.color = Color.clear;
            flashing = null;
        }

        void SetOwnerVariant(int variant)
        {
            if (owner == null || ownerVariants == null || ownerVariants.Length == 0) return;
            var index = Mathf.Clamp(variant, 0, ownerVariants.Length - 1);
            if (ownerVariants[index] != null) owner.sprite = ownerVariants[index];
        }
    }
}
