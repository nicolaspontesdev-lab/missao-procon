using UnityEngine;

namespace Procon
{
    /// <summary>Holofote de palco balancando para os lados e para frente, cada um no seu ritmo.</summary>
    public class LightSweep : MonoBehaviour
    {
        public float yaw = 20f;
        public float pitch = 8f;
        [Min(0.5f)] public float period = 6f;
        public float phase;

        Quaternion home;

        void Start() => home = transform.localRotation;

        void Update()
        {
            var t = Time.unscaledTime * Mathf.PI * 2f / period + phase;
            transform.localRotation = Quaternion.Euler(Mathf.Cos(t * 0.7f) * pitch, Mathf.Sin(t) * yaw, 0f) * home;
        }
    }
}
