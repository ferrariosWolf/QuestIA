using System.Collections;
using Meta.XR;
using QuestAI.Voice;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace QuestAI.Vision
{
    public sealed class QuestVisionCapture : MonoBehaviour
    {
        private const string CameraPermission = "horizonos.permission.HEADSET_CAMERA";
        private PassthroughCameraAccess cameraAccess;
        private AssistantVoiceController voiceController;
        private float nextCaptureAt;
        private bool permissionRequested;
        private bool captureInProgress;

        private void Awake()
        {
            cameraAccess = GetComponent<PassthroughCameraAccess>();
            voiceController = GetComponent<AssistantVoiceController>();
        }

        private void Update()
        {
            if (voiceController == null || !voiceController.IsSessionActive) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Permission.HasUserAuthorizedPermission(CameraPermission))
            {
                if (!permissionRequested)
                {
                    permissionRequested = true;
                    Permission.RequestUserPermission(CameraPermission);
                }
                return;
            }
#endif
            if (cameraAccess == null || !cameraAccess.IsPlaying || captureInProgress || Time.realtimeSinceStartup < nextCaptureAt) return;
            nextCaptureAt = Time.realtimeSinceStartup + 0.5f;
            StartCoroutine(CaptureFrame());
        }

        private IEnumerator CaptureFrame()
        {
            captureInProgress = true;
            yield return new WaitForEndOfFrame();
            RenderTexture target = null;
            Texture2D snapshot = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                Texture source = cameraAccess.GetTexture();
                if (source == null) yield break;
                int width = Mathf.Min(1280, source.width);
                int height = Mathf.Max(1, Mathf.RoundToInt(width * source.height / (float)source.width));
                target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(source, target);
                RenderTexture.active = target;
                snapshot = new Texture2D(width, height, TextureFormat.RGB24, false);
                snapshot.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                snapshot.Apply(false, false);
                byte[] jpeg = snapshot.EncodeToJPG(82);
                Debug.Log($"[QuestAI][Vision] quadro capturado {width}x{height} bytes={jpeg.Length}");
                voiceController.SendVisionFrame(jpeg, width, height);
            }
            finally
            {
                RenderTexture.active = previous;
                if (snapshot != null) Destroy(snapshot);
                if (target != null) RenderTexture.ReleaseTemporary(target);
                captureInProgress = false;
            }
        }
    }
}
