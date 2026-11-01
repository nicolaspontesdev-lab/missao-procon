using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Procon
{
    /// <summary>Botao cresce um pouco com o mouse em cima e afunda ao clicar.</summary>
    public class HoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public float hover = 1.04f;
        public float press = 0.97f;

        float target = 1f;
        Selectable selectable;

        void Awake() => selectable = GetComponent<Selectable>();

        bool Usable => selectable == null || selectable.IsInteractable();

        public void OnPointerEnter(PointerEventData eventData) { if (Usable) target = hover; }
        public void OnPointerExit(PointerEventData eventData) => target = 1f;
        public void OnPointerDown(PointerEventData eventData) { if (Usable) target = press; }
        public void OnPointerUp(PointerEventData eventData) => target = Usable ? hover : 1f;

        void OnDisable()
        {
            target = 1f;
            transform.localScale = Vector3.one;
        }

        void Update()
        {
            if (!Usable) target = 1f;
            var current = transform.localScale.x;
            if (Mathf.Abs(current - target) < 0.001f) return;
            transform.localScale = Vector3.one * Mathf.Lerp(current, target, 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime));
        }
    }
}
