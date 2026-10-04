using System;
using System.Collections;
using UnityEngine;

namespace Procon
{
    /// <summary>
    /// Animacao em degraus, como o steps() do CSS do jogo em HTML: o valor pula
    /// de um quadro para o outro em vez de deslizar. E isso que da a cara de
    /// pixel art; uma animacao suave destoaria do resto.
    /// </summary>
    public static class PixelMotion
    {
        /// <summary>Chama apply com o progresso de 0 a 1, em "steps" saltos.</summary>
        public static IEnumerator Steps(float duration, int steps, Action<float> apply, float delay = 0f)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            steps = Mathf.Max(1, steps);
            apply(0f);
            var wait = new WaitForSeconds(duration / steps);
            for (var i = 1; i <= steps; i++)
            {
                yield return wait;
                apply(i / (float)steps);
            }
        }

        /// <summary>Curva de ida e volta: 0 no inicio e no fim, 1 no meio.</summary>
        public static float Bounce(float t) => 1f - Mathf.Abs(t * 2f - 1f);
    }
}
