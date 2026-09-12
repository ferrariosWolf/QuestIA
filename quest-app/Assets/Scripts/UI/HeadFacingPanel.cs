using System.Collections;
using UnityEngine;

namespace QuestAI.UI
{
    public sealed class HeadFacingPanel : MonoBehaviour
    {
        [SerializeField] private float distance = 1.35f;
        [SerializeField] private float verticalOffset = -0.05f;
        [SerializeField] private float minimumEyeHeight = 1.45f;
        [SerializeField] private float rightOffset = 0.78f;
        private float recenterUntil;

        private IEnumerator Start()
        {
            // O tracking do headset pode chegar alguns frames depois do Unity.
            yield return new WaitForSecondsRealtime(0.25f);
            recenterUntil = Time.realtimeSinceStartup + 2f;
            Recenter();
            yield return new WaitForSecondsRealtime(0.5f);
            Recenter();
            yield return new WaitForSecondsRealtime(0.75f);
            Recenter();
        }

        public void Recenter()
        {
            var viewer = Camera.main;
            if (viewer == null) return;

            var forward = viewer.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            forward.Normalize();

            var right = viewer.transform.right;
            right.y = 0f;
            if (right.sqrMagnitude < 0.01f) right = Vector3.right;
            right.Normalize();
            var target = viewer.transform.position + forward * distance + right * rightOffset + Vector3.up * verticalOffset;
            // Evita que a UI nasça no chão enquanto o tracking vertical ainda inicia.
            target.y = Mathf.Max(target.y, minimumEyeHeight);
            transform.position = target;
            // World Space Canvas usa a face frontal no sentido do olhar do usuário.
            // Apontar o eixo +Z para o mesmo sentido da câmera evita texto espelhado.
            transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }

        private void LateUpdate()
        {
            if (Time.realtimeSinceStartup < recenterUntil)
                Recenter();
        }
    }
}
