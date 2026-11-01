using System.Collections;
using UnityEngine;

namespace Procon
{
    /// <summary>
    /// Apresentador do Show do PROCON: boneco de blocos montado pelo ProconBuilder.
    /// Fica respirando parado, aponta para o telao quando chega uma pergunta e
    /// reage ao acerto (bracos para cima e pulo) ou ao erro (balanca a cabeca).
    /// O braco "do telao" e o que fica do lado +X do mundo.
    /// </summary>
    public class Presenter : MonoBehaviour
    {
        public Transform body;
        public Transform head;
        public Transform armScreen;
        public Transform armMic;
        [Tooltip("Grupo dos olhos: pisca achatando na vertical.")]
        public Transform eyes;
        [Tooltip("Boca: abre e fecha enquanto ele fala.")]
        public Transform mouth;

        Vector3 bodyBase;
        Quaternion headBase, screenBase, micBase;
        Coroutine acting;
        bool busy;
        Vector3 mouthBase;
        float nextBlink, talkUntil;

        void Awake()
        {
            if (body != null) bodyBase = body.localPosition;
            if (head != null) headBase = head.localRotation;
            if (armScreen != null) screenBase = armScreen.localRotation;
            if (armMic != null) micBase = armMic.localRotation;
            if (mouth != null) mouthBase = mouth.localScale;
            nextBlink = Random.Range(2f, 4f);
        }

        void LateUpdate()
        {
            // piscar: a cada 2 a 5 segundos os olhos fecham por um instante
            if (eyes != null)
            {
                var closed = Time.time > nextBlink && Time.time < nextBlink + 0.12f;
                eyes.localScale = new Vector3(1f, closed ? 0.12f : 1f, 1f);
                if (Time.time > nextBlink + 0.12f) nextBlink = Time.time + Random.Range(2f, 5f);
            }
            // falar: a boca abre e fecha enquanto ele apresenta
            if (mouth != null)
            {
                var talking = Time.time < talkUntil;
                var open = talking ? 1f + Mathf.Abs(Mathf.Sin(Time.time * 14f)) * 2.2f : 1f;
                mouth.localScale = new Vector3(mouthBase.x, mouthBase.y * open, mouthBase.z);
            }
        }

        void Update()
        {
            if (busy || body == null) return;
            // respiracao: sobe e desce 1 cm, e a cabeca olha de leve para o telao
            var t = Time.time;
            body.localPosition = bodyBase + new Vector3(0f, Mathf.Sin(t * 2.1f) * 0.01f, 0f);
            if (head != null) head.localRotation = headBase * Quaternion.Euler(0f, Mathf.Sin(t * 0.6f) * 6f - 4f, 0f);
        }

        /// <summary>Pergunta nova: vira para o telao e aponta.</summary>
        public void Introduce()
        {
            talkUntil = Time.time + 1.6f;
            Play(IntroduceRoutine());
        }

        /// <summary>Reacao a resposta do jogador.</summary>
        public void React(bool correct)
        {
            talkUntil = Time.time + 0.9f;
            Play(correct ? CheerRoutine(1) : LamentRoutine());
        }

        /// <summary>Fim do programa: comemora duas vezes.</summary>
        public void Celebrate() => Play(CheerRoutine(2));

        void Play(IEnumerator routine)
        {
            if (!isActiveAndEnabled) return;
            if (acting != null) StopCoroutine(acting);
            ResetPose();
            acting = StartCoroutine(routine);
        }

        void ResetPose()
        {
            if (body != null) body.localPosition = bodyBase;
            if (head != null) head.localRotation = headBase;
            if (armScreen != null) armScreen.localRotation = screenBase;
            if (armMic != null) armMic.localRotation = micBase;
        }

        static float Ease(float t) => t * t * (3f - 2f * t);

        IEnumerator Blend(float duration, System.Action<float> apply)
        {
            for (var time = 0f; time < duration; time += Time.deltaTime)
            {
                apply(Ease(time / duration));
                yield return null;
            }
            apply(1f);
        }

        IEnumerator IntroduceRoutine()
        {
            busy = true;
            var point = Quaternion.Euler(0f, 0f, 95f);
            var look = headBase * Quaternion.Euler(0f, -35f, 0f);
            yield return Blend(0.35f, k =>
            {
                armScreen.localRotation = Quaternion.Slerp(screenBase, point, k);
                head.localRotation = Quaternion.Slerp(headBase, look, k);
            });
            yield return new WaitForSeconds(1.1f);
            yield return Blend(0.4f, k =>
            {
                armScreen.localRotation = Quaternion.Slerp(point, screenBase, k);
                head.localRotation = Quaternion.Slerp(look, headBase, k);
            });
            busy = false;
        }

        IEnumerator CheerRoutine(int jumps)
        {
            busy = true;
            var upScreen = Quaternion.Euler(0f, 0f, 160f);
            var upMic = Quaternion.Euler(0f, 0f, -160f);
            yield return Blend(0.2f, k =>
            {
                armScreen.localRotation = Quaternion.Slerp(screenBase, upScreen, k);
                armMic.localRotation = Quaternion.Slerp(micBase, upMic, k);
            });
            for (var i = 0; i < jumps; i++)
                yield return Blend(0.45f, k => body.localPosition = bodyBase + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.18f);
            yield return Blend(0.3f, k =>
            {
                armScreen.localRotation = Quaternion.Slerp(upScreen, screenBase, k);
                armMic.localRotation = Quaternion.Slerp(upMic, micBase, k);
            });
            busy = false;
        }

        IEnumerator LamentRoutine()
        {
            busy = true;
            var down = headBase * Quaternion.Euler(18f, 0f, 0f);
            yield return Blend(0.25f, k => head.localRotation = Quaternion.Slerp(headBase, down, k));
            yield return Blend(0.9f, k =>
                head.localRotation = down * Quaternion.Euler(0f, Mathf.Sin(k * Mathf.PI * 4f) * 16f, 0f));
            yield return Blend(0.3f, k => head.localRotation = Quaternion.Slerp(down, headBase, k));
            busy = false;
        }

        void OnDisable()
        {
            busy = false;
            ResetPose();
        }
    }
}
