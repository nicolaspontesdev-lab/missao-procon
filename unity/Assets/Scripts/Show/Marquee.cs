using UnityEngine;
using UnityEngine.UI;

namespace Procon
{
    /// <summary>
    /// Lampadas de letreiro de programa de TV acendendo em sequencia (uma apagada
    /// a cada tres, andando). Serve para as lampadas 3D do estudio e para as
    /// lampadas desenhadas no menu.
    /// </summary>
    public class Marquee : MonoBehaviour
    {
        public Renderer[] bulbs = new Renderer[0];
        public Graphic[] uiBulbs = new Graphic[0];
        public Color on = new Color(1f, 0.85f, 0.45f);
        public Color off = new Color(0.35f, 0.22f, 0.1f);
        [Min(0.01f)] public float stepSeconds = 0.16f;
        [Min(0f)] public float glow = 4f;

        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        MaterialPropertyBlock block;
        int lastStep = -1;

        void Update()
        {
            var step = Mathf.FloorToInt(Time.unscaledTime / stepSeconds);
            if (step == lastStep) return;
            lastStep = step;
            if (block == null) block = new MaterialPropertyBlock();

            for (var i = 0; i < bulbs.Length; i++)
            {
                if (bulbs[i] == null) continue;
                var lit = (i + step) % 3 != 0;
                bulbs[i].GetPropertyBlock(block);
                block.SetColor(BaseColor, lit ? on : off);
                block.SetColor(EmissionColor, lit ? on * glow : Color.black);
                bulbs[i].SetPropertyBlock(block);
            }
            for (var i = 0; i < uiBulbs.Length; i++)
                if (uiBulbs[i] != null) uiBulbs[i].color = (i + step) % 3 != 0 ? on : off;
        }
    }
}
