using UnityEngine;

namespace Procon
{
    /// <summary>Holofote do menu indo e voltando, como os canhoes de luz de auditorio.</summary>
    public class Sweep : MonoBehaviour
    {
        [Range(0f, 90f)] public float angle = 18f;
        [Min(0.5f)] public float period = 6f;
        public float offset;

        float baseZ;

        void Start() => baseZ = transform.localEulerAngles.z;

        void Update()
        {
            var z = baseZ + Mathf.Sin((Time.unscaledTime + offset) * Mathf.PI * 2f / period) * angle;
            transform.localRotation = Quaternion.Euler(0f, 0f, z);
        }
    }
}
