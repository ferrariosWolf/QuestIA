using QuestAI.Core;
using QuestAI.Vision;
using UnityEngine;
using UnityEngine.UI;

namespace QuestAI.UI
{
    public sealed class QuestAssistantUI : MonoBehaviour
    {
        [SerializeField] private AssistantStateController stateController;
        [SerializeField] private Text headingText;
        [SerializeField] private Text modeText;
        [SerializeField] private Text versionText;
        [SerializeField] private Text stateText;
        [SerializeField] private Text detailText;
        [SerializeField] private Button activateButton;
        [SerializeField] private Button deactivateButton;
        private GameObject chat;
        private Button cameraButton;

        public void Configure(
            AssistantStateController controller,
            Text heading,
            Text mode,
            Text version,
            Text state,
            Text detail,
            Button activate,
            Button deactivate)
        {
            stateController = controller;
            headingText = heading;
            modeText = mode;
            versionText = version;
            stateText = state;
            detailText = detail;
            activateButton = activate;
            deactivateButton = deactivate;
        }

        private void Awake()
        {
            if (GetComponent<HeadFacingPanel>() == null)
                gameObject.AddComponent<HeadFacingPanel>();
            chat = new GameObject("Chat", typeof(RectTransform));
            chat.transform.SetParent(transform, false);
            chat.AddComponent<QuestChatPanel>();
            deactivateButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -255);
            deactivateButton.GetComponent<RectTransform>().sizeDelta = new Vector2(380, 55);
            activateButton.onClick.AddListener(stateController.Activate);
            deactivateButton.onClick.AddListener(stateController.Deactivate);
            stateController.StateChanged += Refresh;
            Refresh(stateController.CurrentState);
            activateButton.GetComponentInChildren<Text>().text = "ATIVAR VOZ";
            CreateCameraButton();
            if (cameraButton != null)
                cameraButton.gameObject.SetActive(stateController.CurrentState != AssistantState.Inactive);
        }

        private void OnDestroy()
        {
            if (stateController != null)
                stateController.StateChanged -= Refresh;
        }

        private void Refresh(AssistantState state)
        {
            bool active = state != AssistantState.Inactive && state != AssistantState.Error;
            bool voiceError = state == AssistantState.Error;
            headingText.text = "QUEST AI";
            modeText.text = voiceError ? "Voz desconectada • toque ATIVAR VOZ" : state == AssistantState.Listening ? "Ouvindo • fale agora" : state == AssistantState.Thinking ? "Pensando..." : state == AssistantState.Speaking ? "Respondendo..." : active ? "Conectando..." : "Seu assistente espacial";
            modeText.color = voiceError ? new Color(1f, 0.68f, 0.35f) : new Color(0.45f, 0.9f, 1f);
            versionText.text = active ? string.Empty : "VOZ EM PORTUGUÊS";
            stateText.text = active ? state.ToString().ToUpperInvariant() : "PRONTO";
            stateText.color = voiceError ? new Color(1f, 0.68f, 0.35f) : active ? new Color(0.35f, 1f, 0.62f) : new Color(0.72f, 0.78f, 0.84f);
            detailText.text = active ? "Fale naturalmente • câmera pronta para análise" : "Toque para iniciar uma conversa";
            detailText.gameObject.SetActive(!active);
            stateText.gameObject.SetActive(!active);
            versionText.gameObject.SetActive(!active);
            chat.SetActive(active);
            if (cameraButton != null) cameraButton.gameObject.SetActive(active);
            activateButton.gameObject.SetActive(!active);
            deactivateButton.gameObject.SetActive(active);
        }

        private void CreateCameraButton()
        {
            var camera = FindFirstObjectByType<QuestCameraPreview>();
            if (camera == null) return;
            var go = new GameObject("CameraButton", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(transform, false);
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(250, 55);
            rect.anchoredPosition = new Vector2(0, -315);
            go.GetComponent<Image>().color = new Color(0.16f, 0.35f, 0.48f, 0.82f);
            cameraButton = go.GetComponent<Button>();
            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(go.transform, false);
            var label = labelObject.GetComponent<Text>();
            label.rectTransform.sizeDelta = new Vector2(250, 55);
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 24;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.text = "PRÉVIA  ON";
            cameraButton.onClick.AddListener(() =>
            {
                camera.Toggle();
                label.text = camera.IsVisible ? "CÂMERA  ON" : "PRÉVIA  OFF";
            });
        }
    }
}
