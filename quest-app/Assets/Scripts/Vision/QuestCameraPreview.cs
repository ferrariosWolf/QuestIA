using Meta.XR;
using UnityEngine;

namespace QuestAI.Vision
{
    public sealed class QuestCameraPreview : MonoBehaviour
    {
        [SerializeField] private bool visibleOnStart = true;
        [SerializeField] private float distance = 2.2f;
        [SerializeField] private float width = 4.4f;
        private PassthroughCameraAccess cameraAccess;
        private GameObject previewObject;
        private MeshRenderer previewRenderer;
        private bool previewVisible;
        private Material previewMaterial;

        public bool IsVisible => previewVisible;

        private void Awake()
        {
            cameraAccess = GetComponent<PassthroughCameraAccess>();
        }

        private void Start()
        {
            CreatePreview();
            SetVisible(visibleOnStart);
        }

        private void LateUpdate()
        {
            if (previewObject == null) return;
            var viewer = Camera.main;
            if (viewer == null) { previewRenderer.enabled = false; return; }
            // A child of the tracked camera follows pitch, yaw and roll, including
            // the headset pose update immediately before rendering.
            if (previewObject.transform.parent != viewer.transform)
            {
                previewObject.transform.SetParent(viewer.transform, false);
                Debug.Log("[QuestAI][CameraPreview] Centralizada e vinculada ao headset: " + viewer.name);
            }
            previewObject.transform.localPosition = new Vector3(0f, 0f, Mathf.Max(distance, viewer.nearClipPlane + 0.1f));
            previewObject.transform.localRotation = Quaternion.identity;
            bool hasFrame = previewVisible && cameraAccess != null && cameraAccess.IsPlaying;
            previewRenderer.enabled = hasFrame;
            if (!hasFrame) return;
            var resolution = cameraAccess.CurrentResolution;
            var aspect = resolution.y > 0 ? resolution.x / (float)resolution.y : 4f / 3f;
            previewObject.transform.localScale = new Vector3(width, width / aspect, 1f);
            if (previewRenderer != null && previewRenderer.sharedMaterial != null)
                previewRenderer.sharedMaterial.mainTexture = cameraAccess.GetTexture();
        }

        public void Toggle()
        {
            SetVisible(!previewVisible);
        }

        public void SetVisible(bool visible)
        {
            previewVisible = visible;
            if (previewObject != null) previewObject.SetActive(visible);
            Debug.Log($"[QuestAI][CameraPreview] {(visible ? "visível" : "oculta")}");
        }

        private void CreatePreview()
        {
            previewObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            previewObject.name = "Quest Camera Environment View";
            previewObject.layer = gameObject.layer;
            previewRenderer = previewObject.GetComponent<MeshRenderer>();
            // UI/Default é incluído pelo pacote UGUI e não é removido pelo shader stripping.
            var shader = Shader.Find("UI/Default") ?? Shader.Find("Sprites/Default");
            if (shader == null)
            {
                Debug.LogError("[QuestAI][CameraPreview] Shader de prévia não disponível.");
                previewObject.SetActive(false);
                return;
            }
            previewMaterial = new Material(shader) { name = "Quest Camera Preview Material" };
            previewRenderer.sharedMaterial = previewMaterial;
            previewRenderer.enabled = false;
            previewRenderer.sharedMaterial.color = Color.white;
            var collider = previewObject.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
        }

        private void OnDestroy()
        {
            if (previewObject != null) Destroy(previewObject);
            if (previewMaterial != null) Destroy(previewMaterial);
        }
    }
}
