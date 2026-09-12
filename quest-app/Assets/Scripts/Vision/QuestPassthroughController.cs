using System.Collections;
using UnityEngine;

namespace QuestAI.Vision
{
    public sealed class QuestPassthroughController : MonoBehaviour
    {
        private IEnumerator Start()
        {
            // OVRManager.instance só fica disponível depois da inicialização do OpenXR.
            yield return null;
            yield return new WaitForSecondsRealtime(0.5f);
            var manager = OVRManager.instance;
            if (manager == null)
            {
                Debug.LogWarning("[QuestAI][Passthrough] OVRManager não inicializou.");
                yield break;
            }

            manager.isInsightPassthroughEnabled = true;
            var layer = GetComponent<OVRPassthroughLayer>();
            if (layer == null) layer = gameObject.AddComponent<OVRPassthroughLayer>();
            layer.overlayType = OVROverlay.OverlayType.Underlay;
            layer.hidden = false;
            layer.textureOpacity = 1f;
            layer.enabled = false;
            layer.enabled = true;
            Debug.Log("[QuestAI][Passthrough] underlay ativo; painel lateral translúcido.");
        }
    }
}
