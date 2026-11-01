using UnityEngine;

namespace Procon
{
    /// <summary>
    /// Camera de estudio quase parada: desliza poucos centimetros, devagar,
    /// sempre olhando para o mesmo ponto do telao.
    /// </summary>
    public class CameraDrift : MonoBehaviour
    {
        public Vector3 target;
        public Vector3 amplitude = new Vector3(0.12f, 0.04f, 0f);
        [Min(1f)] public float period = 12f;

        Vector3 home;

        void Start() => home = transform.position;

        void LateUpdate()
        {
            var phase = Time.unscaledTime * Mathf.PI * 2f / period;
            transform.position = home + new Vector3(Mathf.Sin(phase) * amplitude.x,
                                                    Mathf.Sin(phase * 0.7f) * amplitude.y,
                                                    Mathf.Sin(phase * 0.5f) * amplitude.z);
            transform.LookAt(target);
        }
    }
}
