using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace QuestAI.UI
{
    public sealed class QuestChatPanel : MonoBehaviour
    {
        [Serializable] private class Config { public string backendUrl = "http://127.0.0.1:8787"; public string appToken = ""; }
        [Serializable] private class Request { public string message; public string sessionId; }
        [Serializable] private class Reply { public string answer; public string error; public string provider; }
        private Config config;
        private InputField input;
        private Text output;
        private Button send;
        private UnityWebRequest pending;
        private readonly string session = Guid.NewGuid().ToString("N");

        private void Awake()
        {
            var asset = Resources.Load<TextAsset>("QuestChatConfig");
            config = asset == null ? new Config() : JsonUtility.FromJson<Config>(asset.text);
            input = MakeInput("Pergunta", 65, "Digite uma pergunta...");
            output = MakeText("Resposta", -60, 170);
            output.text = "Envie uma pergunta para começar.";
            var go = new GameObject("Enviar", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(transform, false);
            var rect = go.GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(380, 60); rect.anchoredPosition = new Vector2(0, -180);
            go.GetComponent<Image>().color = new Color(0.05f, 0.48f, 0.68f);
            send = go.GetComponent<Button>();
            var label = MakeText("EnviarLabel", 0, 60); label.transform.SetParent(go.transform, false); label.rectTransform.sizeDelta = new Vector2(380, 60); label.text = "ENVIAR"; label.alignment = TextAnchor.MiddleCenter;
            send.onClick.AddListener(() => { if (pending == null && !string.IsNullOrWhiteSpace(input.text)) StartCoroutine(Send()); });
        }

        private Text MakeText(string name, float y, float height)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text)); go.transform.SetParent(transform, false);
            var text = go.GetComponent<Text>(); text.rectTransform.sizeDelta = new Vector2(780, height); text.rectTransform.anchoredPosition = new Vector2(0, y);
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = 24; text.color = Color.white; text.supportRichText = false; text.raycastTarget = false;
            text.resizeTextForBestFit = true; text.resizeTextMinSize = 16; text.resizeTextMaxSize = 24;
            return text;
        }

        private InputField MakeInput(string name, float y, string placeholder)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(InputField)); go.transform.SetParent(transform, false);
            var rect = go.GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(800, 70); rect.anchoredPosition = new Vector2(0, y);
            go.GetComponent<Image>().color = new Color(0.1f, 0.18f, 0.24f);
            var field = go.GetComponent<InputField>(); var text = MakeText(name + "Text", 0, 60); text.transform.SetParent(go.transform, false); field.textComponent = text;
            var hint = MakeText(name + "Hint", 0, 60); hint.transform.SetParent(go.transform, false); hint.text = placeholder; hint.color = Color.gray; field.placeholder = hint; field.characterLimit = 2000;
            return field;
        }

        private IEnumerator Send()
        {
            if (config == null || !Uri.TryCreate(config.backendUrl, UriKind.Absolute, out var uri) ||
                (uri.Scheme != "https" && !(uri.Scheme == "http" && uri.IsLoopback)))
            { output.text = "Configure uma URL HTTPS ou localhost em QuestChatConfig."; yield break; }
            send.interactable = false; input.interactable = false; output.text = "Pensando...";
            using (var request = new UnityWebRequest(config.backendUrl.TrimEnd('/') + "/api/chat", "POST"))
            {
                pending = request;
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(new Request { message = input.text, sessionId = session })));
                request.downloadHandler = new DownloadHandlerBuffer(); request.timeout = 30;
                request.SetRequestHeader("Content-Type", "application/json");
                if (!string.IsNullOrEmpty(config.appToken)) request.SetRequestHeader("Authorization", "Bearer " + config.appToken);
                yield return request.SendWebRequest();
                Reply reply = null;
                try { reply = JsonUtility.FromJson<Reply>(request.downloadHandler.text); } catch (ArgumentException) { }
                output.text = request.result == UnityWebRequest.Result.Success && !string.IsNullOrEmpty(reply?.answer)
                    ? (reply.provider == "mock" ? "[DEMONSTRAÇÃO]\n" : "") + reply.answer
                    : reply?.error ?? "Não foi possível conectar ao servidor. Tente novamente.";
                pending = null;
            }
            send.interactable = true; input.interactable = true;
        }

        private void OnDisable()
        {
            if (pending != null) { pending.Abort(); pending.Dispose(); pending = null; }
            StopAllCoroutines();
            if (send != null) send.interactable = true;
            if (input != null) input.interactable = true;
        }
    }
}
