using UnityEngine;

namespace Procon
{
    /// <summary>Letreiro 3D flutuando: sobe e desce e gira um pouco para os lados.</summary>
    public class Hover : MonoBehaviour
    {
        public float height = 0.08f;
        public float turn = 7f;
        [Min(0.5f)] public float period = 5f;

        Vector3 home;
        Quaternion rest;

        void Start()
        {
            home = transform.localPosition;
            rest = transform.localRotation;
        }

        void Update()
        {
            var t = Time.unscaledTime * Mathf.PI * 2f / period;
            transform.localPosition = home + Vector3.up * Mathf.Sin(t) * height;
            transform.localRotation = rest * Quaternion.Euler(0f, Mathf.Sin(t * 0.5f) * turn, 0f);
        }
    }
}
