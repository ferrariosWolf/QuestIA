using System;
using System.Collections;
using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using QuestAI.Core;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace QuestAI.Voice
{
    public sealed class AssistantVoiceController : MonoBehaviour
    {
        private const int PlaybackBufferSeconds = 10;
        private static readonly TimeSpan ConnectionTimeout = TimeSpan.FromSeconds(15);

        [Header("Quest AI Backend — conexão USB via ADB reverse")]
        [Tooltip("127.0.0.1 funciona no Quest quando adb reverse tcp:8787 tcp:8787 está ativo.")]
        [SerializeField] private string backendHost = "127.0.0.1";
        [SerializeField] private int backendPort = 8787;

        [Header("Áudio")]
        [SerializeField] private int microphoneSampleRate = 48000;
        [SerializeField] private int transmissionSampleRate = 24000;
        [SerializeField] private AssistantStateController stateController;
        // O Quest só libera o áudio de forma confiável após uma ação explícita
        // do usuário no app. O botão inicia a sessão e mantém o microfone ativo.
        [SerializeField] private bool activateOnStart = false;

        private readonly ConcurrentQueue<InboundMessage> inbound = new ConcurrentQueue<InboundMessage>();
        private readonly ConcurrentQueue<float> playbackSamples = new ConcurrentQueue<float>();
        private readonly SemaphoreSlim sendLock = new SemaphoreSlim(1, 1);
        private ClientWebSocket socket;
        private CancellationTokenSource cancellation;
        private AudioClip microphoneClip;
        private AudioClip playbackClip;
        private AudioSource playbackSource;
        private string microphoneDevice;
        private int lastMicrophonePosition;
        private bool sessionRequested;
        private bool microphoneSuppressed;
        private float resumeMicrophoneAt;

        public bool IsSessionActive => sessionRequested && socket != null && socket.State == WebSocketState.Open;

        [Serializable]
        private sealed class StatusMessage
        {
            public string type = string.Empty;
            public string state = string.Empty;
            public string message = string.Empty;
        }

        private struct InboundMessage
        {
            public string Status;
            public byte[] Audio;
            public Exception Error;

            public InboundMessage(string status, byte[] audio, Exception error)
            {
                Status = status;
                Audio = audio;
                Error = error;
            }
        }

        private void Awake()
        {
            if (stateController == null)
                stateController = GetComponent<AssistantStateController>();

            playbackSource = gameObject.AddComponent<AudioSource>();
            playbackSource.playOnAwake = false;
            playbackSource.loop = true;
            playbackSource.spatialBlend = 0f;
            playbackSource.volume = 1.8f;
        }

        private void OnEnable()
        {
            stateController.ActivationRequested += OnActivationRequested;
            stateController.DeactivationRequested += OnDeactivationRequested;
        }

        private IEnumerator Start()
        {
            if (!activateOnStart) yield break;
            yield return new WaitForSecondsRealtime(1f);
            if (stateController != null && stateController.CurrentState == AssistantState.Inactive)
                stateController.Activate();
        }

        private void OnDisable()
        {
            if (stateController != null)
            {
                stateController.ActivationRequested -= OnActivationRequested;
                stateController.DeactivationRequested -= OnDeactivationRequested;
            }
            sessionRequested = false;
            StopSession();
        }

        private void Update()
        {
            InboundMessage message;
            while (inbound.TryDequeue(out message))
            {
                if (message.Audio != null)
                {
                    Pcm16AudioCodec.DecodePcm16(message.Audio, playbackSamples.Enqueue);
                    // PCM16 mono: 2 bytes por amostra. O backend envia a resposta
                    // inteira e logo volta ao estado listening; não podemos liberar
                    // o microfone nem limpar a fila antes de o áudio realmente tocar.
                    float audioSeconds = message.Audio.Length / (2f * transmissionSampleRate);
                    microphoneSuppressed = true;
                    float audioEnd = Time.realtimeSinceStartup + audioSeconds + 0.25f;
                    resumeMicrophoneAt = float.IsPositiveInfinity(resumeMicrophoneAt)
                        ? audioEnd
                        : Mathf.Max(resumeMicrophoneAt, audioEnd);
                }
                else if (message.Error != null)
                    Fail(message.Error);
                else if (!string.IsNullOrWhiteSpace(message.Status))
                    ApplyBackendStatus(message.Status);
            }
            UpdateMicrophoneSuppression();
            StreamMicrophoneSamples();
        }

        private void OnActivationRequested()
        {
            if (sessionRequested)
                return;
            sessionRequested = true;
            StartCoroutine(RequestMicrophoneAndConnect());
        }

        private void OnDeactivationRequested()
        {
            sessionRequested = false;
            StopSession();
        }

        private IEnumerator RequestMicrophoneAndConnect()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
            {
                Permission.RequestUserPermission(Permission.Microphone);
                float deadline = Time.realtimeSinceStartup + 15f;
                while (sessionRequested && !Permission.HasUserAuthorizedPermission(Permission.Microphone) && Time.realtimeSinceStartup < deadline)
                    yield return null;

                if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
                {
                    Fail(new UnauthorizedAccessException("Permissão de microfone não concedida."));
                    yield break;
                }
            }
#else
            yield return null;
#endif
            if (sessionRequested)
                ConnectAsync();
        }

        private async void ConnectAsync()
        {
            if (!IsBackendConfigurationValid())
            {
                Fail(new InvalidOperationException("Configure Backend Host/IP no Inspector antes do build."));
                return;
            }

            Uri uri = null;
            try
            {
                cancellation = new CancellationTokenSource();
                socket = new ClientWebSocket();
                uri = new Uri("ws://" + backendHost.Trim() + ":" + backendPort + "/");
                Debug.Log($"[QuestAI][WS] Connecting to {uri}", this);
                using (var connectTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token))
                {
                    connectTimeout.CancelAfter(ConnectionTimeout);
                    await socket.ConnectAsync(uri, connectTimeout.Token);
                }
                Debug.Log($"[QuestAI][WS] Connected uri={uri} state={socket.State}", this);
                if (!sessionRequested)
                    return;

                await SendJsonAsync("{\"type\":\"hello\",\"client\":\"quest-ai-unity\",\"protocol\":1}", cancellation.Token);
                await SendJsonAsync("{\"type\":\"activate\"}", cancellation.Token);
                StartPlayback();
                StartMicrophone();
                _ = ReceiveLoopAsync(cancellation.Token);
            }
            catch (Exception exception)
            {
                LogConnectionFailure(exception, uri);
                Fail(exception);
            }
        }

        private async Task SendJsonAsync(string json, CancellationToken token)
        {
            byte[] data = Encoding.UTF8.GetBytes(json);
            await sendLock.WaitAsync(token);
            try
            {
                if (socket == null || socket.State != WebSocketState.Open)
                    throw new WebSocketException("WebSocket não está aberto para envio de JSON.");
                await socket.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, token);
            }
            finally
            {
                sendLock.Release();
            }
        }

        public void SendVisionFrame(byte[] jpeg, int width, int height)
        {
            if (!IsSessionActive || jpeg == null || jpeg.Length == 0 || cancellation == null) return;
            string json = "{\"type\":\"vision_frame\",\"mimeType\":\"image/jpeg\",\"width\":" + width +
                          ",\"height\":" + height + ",\"imageBase64\":\"" + Convert.ToBase64String(jpeg) + "\"}";
            _ = SendVisionSafelyAsync(json, cancellation.Token);
        }

        private async Task SendVisionSafelyAsync(string json, CancellationToken token)
        {
            try { await SendJsonAsync(json, token); }
            catch (Exception exception)
            {
                if (!(exception is OperationCanceledException))
                    Debug.LogWarning($"[QuestAI][Vision] Falha ao enviar quadro: {exception.Message}", this);
            }
        }

        private void LogConnectionFailure(Exception exception, Uri uri)
        {
            string inner = exception.InnerException == null
                ? "none"
                : exception.InnerException.GetType().FullName + ": " + exception.InnerException.Message;
            Debug.LogError(
                $"[QuestAI][WS] Connection failed: type={exception.GetType().FullName} message={exception.Message} " +
                $"inner={inner} state={(socket == null ? "null" : socket.State.ToString())} uri={uri}", this);
            Debug.LogException(exception, this);
        }

        private bool IsBackendConfigurationValid()
        {
            return backendPort > 0 && backendPort <= 65535 &&
                   !string.IsNullOrWhiteSpace(backendHost) &&
                   backendHost.IndexOf("CONFIGURE", StringComparison.OrdinalIgnoreCase) < 0 &&
                   Uri.CheckHostName(backendHost.Trim()) != UriHostNameType.Unknown;
        }

        private void StartMicrophone()
        {
            microphoneDevice = Microphone.devices.Length > 0 ? Microphone.devices[0] : null;
            microphoneClip = Microphone.Start(microphoneDevice, true, 1, microphoneSampleRate);
            lastMicrophonePosition = 0;
            if (microphoneClip == null)
                throw new InvalidOperationException("Nenhum microfone disponível.");
        }

        private void StreamMicrophoneSamples()
        {
            if (!sessionRequested || microphoneClip == null || socket == null || socket.State != WebSocketState.Open)
                return;

            int currentPosition = Microphone.GetPosition(microphoneDevice);
            if (currentPosition < 0 || currentPosition == lastMicrophonePosition)
                return;

            // Descarta (sem transmitir depois) tudo que o microfone captou enquanto
            // a resposta da IA estava tocando. Isso impede eco/barge-in falso.
            if (microphoneSuppressed)
            {
                lastMicrophonePosition = currentPosition;
                return;
            }

            if (currentPosition > lastMicrophonePosition)
                SendMicrophoneRange(lastMicrophonePosition, currentPosition - lastMicrophonePosition);
            else
            {
                SendMicrophoneRange(lastMicrophonePosition, microphoneClip.samples - lastMicrophonePosition);
                if (currentPosition > 0)
                    SendMicrophoneRange(0, currentPosition);
            }
            lastMicrophonePosition = currentPosition;
        }

        private void SendMicrophoneRange(int offset, int sampleCount)
        {
            if (sampleCount <= 0)
                return;
            var samples = new float[sampleCount * microphoneClip.channels];
            microphoneClip.GetData(samples, offset);

            if (microphoneClip.channels > 1)
            {
                var mono = new float[sampleCount];
                for (int frame = 0; frame < sampleCount; frame++)
                {
                    float sum = 0f;
                    for (int channel = 0; channel < microphoneClip.channels; channel++)
                        sum += samples[frame * microphoneClip.channels + channel];
                    mono[frame] = sum / microphoneClip.channels;
                }
                samples = mono;
            }

            byte[] pcm = Pcm16AudioCodec.ResampleToPcm16(samples, microphoneClip.frequency, transmissionSampleRate);
            _ = SendBinaryAsync(pcm, cancellation.Token);
        }

        private async Task SendBinaryAsync(byte[] data, CancellationToken token)
        {
            await sendLock.WaitAsync(token);
            try
            {
                if (socket != null && socket.State == WebSocketState.Open)
                    await socket.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Binary, true, token);
            }
            catch (Exception exception)
            {
                if (!(exception is OperationCanceledException))
                    inbound.Enqueue(new InboundMessage(null, null, exception));
            }
            finally
            {
                sendLock.Release();
            }
        }

        private async Task ReceiveLoopAsync(CancellationToken token)
        {
            var buffer = new byte[16384];
            try
            {
                while (socket != null && socket.State == WebSocketState.Open && !token.IsCancellationRequested)
                {
                    using (var message = new MemoryStream())
                    {
                        WebSocketReceiveResult result;
                        do
                        {
                            result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                            if (result.MessageType == WebSocketMessageType.Close)
                            {
                                inbound.Enqueue(new InboundMessage("disconnected", null, null));
                                return;
                            }
                            message.Write(buffer, 0, result.Count);
                        } while (!result.EndOfMessage);

                        byte[] payload = message.ToArray();
                        if (result.MessageType == WebSocketMessageType.Binary)
                            inbound.Enqueue(new InboundMessage(null, payload, null));
                        else
                        {
                            string json = Encoding.UTF8.GetString(payload);
                            StatusMessage status = JsonUtility.FromJson<StatusMessage>(json);
                            if (status != null && status.type == "status")
                                inbound.Enqueue(new InboundMessage(status.state, null, null));
                            else if (status != null && status.type == "error")
                                inbound.Enqueue(new InboundMessage(null, null, new InvalidOperationException(status.message)));
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                if (!(exception is OperationCanceledException))
                    inbound.Enqueue(new InboundMessage(null, null, exception));
            }
        }

        private void StartPlayback()
        {
            playbackClip = AudioClip.Create("Quest AI Realtime", transmissionSampleRate * PlaybackBufferSeconds, 1, transmissionSampleRate, true, FillPlaybackBuffer);
            playbackSource.clip = playbackClip;
            playbackSource.Play();
        }

        private void FillPlaybackBuffer(float[] data)
        {
            float sample;
            for (int i = 0; i < data.Length; i++)
                data[i] = playbackSamples.TryDequeue(out sample) ? sample : 0f;
        }

        private void FlushPlayback()
        {
            float ignored;
            while (playbackSamples.TryDequeue(out ignored)) { }
        }

        private void ApplyBackendStatus(string status)
        {
            if (!sessionRequested)
                return;
            switch (status)
            {
                case "connected":
                    stateController.SetConnectionState(AssistantState.Listening);
                    break;
                case "listening":
                    stateController.SetConnectionState(AssistantState.Listening);
                    break;
                case "thinking":
                    microphoneSuppressed = true;
                    resumeMicrophoneAt = float.PositiveInfinity;
                    stateController.SetConnectionState(AssistantState.Thinking);
                    break;
                case "speaking":
                    microphoneSuppressed = true;
                    resumeMicrophoneAt = float.PositiveInfinity;
                    stateController.SetConnectionState(AssistantState.Speaking);
                    break;
                case "interrupted":
                    FlushPlayback();
                    microphoneSuppressed = false;
                    resumeMicrophoneAt = 0f;
                    stateController.SetConnectionState(AssistantState.Listening);
                    break;
                case "error":
                case "disconnected":
                    sessionRequested = false;
                    stateController.SetConnectionState(AssistantState.Error);
                    StopSession();
                    break;
            }
        }

        private void UpdateMicrophoneSuppression()
        {
            if (!microphoneSuppressed || float.IsPositiveInfinity(resumeMicrophoneAt))
                return;
            if (Time.realtimeSinceStartup < resumeMicrophoneAt || !playbackSamples.IsEmpty)
                return;

            microphoneSuppressed = false;
            resumeMicrophoneAt = 0f;
            if (microphoneClip != null)
                lastMicrophonePosition = Microphone.GetPosition(microphoneDevice);
            stateController.SetConnectionState(AssistantState.Listening);
        }

        private void Fail(Exception exception)
        {
            Debug.LogError($"[QuestAI] Voice failure: type={exception.GetType().FullName} message={exception.Message}", this);
            sessionRequested = false;
            StopSession();
            stateController.SetConnectionState(AssistantState.Error);
        }

        private void StopSession()
        {
            if (microphoneClip != null)
            {
                Microphone.End(microphoneDevice);
                microphoneClip = null;
            }
            if (playbackSource != null)
            {
                playbackSource.Stop();
                playbackSource.clip = null;
            }
            if (playbackClip != null)
            {
                Destroy(playbackClip);
                playbackClip = null;
            }
            float ignored;
            while (playbackSamples.TryDequeue(out ignored)) { }
            microphoneSuppressed = false;
            resumeMicrophoneAt = 0f;

            ClientWebSocket closingSocket = socket;
            socket = null;
            if (closingSocket != null)
                CloseAndDisposeSocketAsync(closingSocket);

            if (cancellation != null)
            {
                cancellation.Cancel();
                cancellation.Dispose();
                cancellation = null;
            }
        }

        private async void CloseAndDisposeSocketAsync(ClientWebSocket closingSocket)
        {
            try
            {
                if (closingSocket.State == WebSocketState.Open)
                {
                    using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1)))
                        await closingSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Assistente desativado", timeout.Token);
                }
            }
            catch (Exception) { }
            finally
            {
                closingSocket.Dispose();
            }
        }
    }
}
