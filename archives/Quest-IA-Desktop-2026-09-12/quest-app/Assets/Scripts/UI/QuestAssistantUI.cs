using QuestAI.Core;
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
            chat = new GameObject("Chat", typeof(RectTransform));
            chat.transform.SetParent(transform, false);
            chat.AddComponent<QuestChatPanel>();
            deactivateButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -255);
            deactivateButton.GetComponent<RectTransform>().sizeDelta = new Vector2(380, 55);
            activateButton.onClick.AddListener(stateController.Activate);
            deactivateButton.onClick.AddListener(stateController.Deactivate);
            stateController.StateChanged += Refresh;
            Refresh(stateController.CurrentState);
        }

        private void OnDestroy()
        {
            if (stateController != null)
                stateController.StateChanged -= Refresh;
        }

        private void Refresh(AssistantState state)
        {
            bool active = state == AssistantState.Active;
            headingText.text = "QUEST AI";
            modeText.text = active ? "Assistente ativado" : "Assistente XR";
            versionText.text = active ? string.Empty : "Versão 0.3";
            stateText.text = active ? "ATIVO" : "INATIVO";
            stateText.color = active ? new Color(0.35f, 1f, 0.62f) : new Color(0.72f, 0.78f, 0.84f);
            detailText.text = "Pronto para iniciar";
            detailText.gameObject.SetActive(!active);
            stateText.gameObject.SetActive(!active);
            versionText.gameObject.SetActive(!active);
            chat.SetActive(active);
            activateButton.gameObject.SetActive(!active);
            deactivateButton.gameObject.SetActive(active);
        }
    }
}
