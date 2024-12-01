using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Singleton;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class NetworkManager : Singleton<NetworkManager>
{
    private static readonly string SERVER_URI = "https://chzzk-chat-cluster.kro.kr:4400";
    private static readonly string MSG_ERROR_EMPTY_ID = "[ERROR] Empty channel ID";
    private static readonly string MSG_ERROR_SERVER_SOCKET_FAILED = "[ERROR] Failed to connect with WebSocket";
    private static readonly string MSG_INFO_SERVER_STARTED = "[INFO] Server is preparing. Please wait.";
    private static readonly string MSG_INFO_SERVER_READY = "[INFO] Server is prepared. Received socket address";
    private static readonly string MSG_INFO_SERVER_SOCKET_TRY = "[INFO] Trying to connect with WebSocket";
    private static readonly string MSG_INFO_SERVER_SOCKET_SUCCESS = "[INFO] Successfully connected with WebSocket";

    [NotNull] public GameObject panelReady;
    public TMP_InputField inputFieldForChannelID;
    public TMP_Text labelConnectionStatus;
    public Button connectButton;
    public TMP_Text labelChannelName;
    public TMP_Text topChat;
    public GameObject chatBox;
    public GameObject chatList;
    
    [SerializeField] private string channelID;
    [SerializeField] private AudioManager audioManager;
    [SerializeField] private Image clusterImage;

    private ClientWebSocket websocket;
    private Uri websocketURI;
    private string audioSavePath;
    private List<GameObject> chats;

    [System.Serializable]
    public class RequestData
    {
        public string top_chat;
        public string[] texts;
    }
    
    private void Awake()
    {
        chats = new List<GameObject>();
        audioSavePath = Application.persistentDataPath + "/received_audio.wav";
    }

    public void OnClickStartConnection()
    {
        connectButton.enabled = false;
            
        channelID = inputFieldForChannelID.text.Trim();

        if (string.IsNullOrEmpty(channelID))
        {
            labelConnectionStatus.text = MSG_ERROR_EMPTY_ID;
            connectButton.enabled = true;
            return;
        }

        StartCoroutine(SendConnectionRequest());
    }

    IEnumerator SendConnectionRequest()
    {
        using (UnityWebRequest request = UnityWebRequest.Get(SERVER_URI + "/connect/" + channelID))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string websocketURL = request.downloadHandler.text.Trim().Trim('"');

                if (string.IsNullOrEmpty(websocketURL))
                {
                    labelConnectionStatus.text = MSG_INFO_SERVER_STARTED;
                    connectButton.enabled = true;
                    yield break;
                }

                Debug.Log("반환된 웹소켓 URL : " + websocketURL);
                websocketURI = new Uri(websocketURL);
                labelConnectionStatus.text = MSG_INFO_SERVER_SOCKET_TRY;
                ConnectWebSocket();
            }
            else
            {
                labelConnectionStatus.text = MSG_ERROR_SERVER_SOCKET_FAILED;
                connectButton.enabled = true;
            }
        }
    }
    
    IEnumerator GetChannelName()
    {
        using (UnityWebRequest request = UnityWebRequest.Get(SERVER_URI + "/name/" + channelID))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string channelName = request.downloadHandler.text.Trim().Trim('"');

                Debug.Log("반환된 채널명 : " + channelName);
                labelChannelName.text = channelName + " (" + channelID + ")";
            }
            else
            {
                labelChannelName.text = "ERROR";
            }
        }
    }

    private async void ConnectWebSocket()
    {
        websocket = new ClientWebSocket();

        try
        {
            await websocket.ConnectAsync(websocketURI, CancellationToken.None);
            labelConnectionStatus.text = MSG_INFO_SERVER_SOCKET_SUCCESS;
            panelReady.SetActive(false);

            StartCoroutine(GetChannelName());

            await ReceiveData();
        }
        catch (Exception e)
        {
            labelConnectionStatus.text = MSG_ERROR_SERVER_SOCKET_FAILED;
            Debug.Log("웹소켓 연결 실패 : " + e.Message);
            connectButton.enabled = true;
        }
    }

    private async Task ReceiveData()
    {
        var buffer = new byte[1024 * 4];

        try
        {
            while (websocket.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result;
                using (var memoryStream = new MemoryStream())
                {
                    do
                    {
                        result = await websocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                        memoryStream.Write(buffer, 0, result.Count);
                    } while (!result.EndOfMessage);

                    memoryStream.Seek(0, SeekOrigin.Begin);

                    if (result.MessageType == WebSocketMessageType.Binary)
                    {
                        var data = memoryStream.ToArray();
                        
                        // 데이터 유형 확인 (첫 바이트를 통해 확인한다고 가정)
                        byte dataType = data[0];
                        byte[] contentData = new byte[data.Length - 1];
                        Array.Copy(data, 1, contentData, 0, contentData.Length);

                        if (dataType == 0x01)  // 예: 0x01이면 음성 데이터
                        {
                            audioManager.ConvertAndPlay(contentData);
                        }
                        else if (dataType == 0x02)  // 예: 0x02이면 이미지 데이터
                        {
                            ProcessImage(contentData);
                        }
                        else
                        {
                            Debug.LogWarning("알 수 없는 데이터 타입 수신: " + dataType);
                        }
                    }
                    else if (result.MessageType == WebSocketMessageType.Text)
                    {
                        memoryStream.Seek(0, SeekOrigin.Begin);
                        using (var reader = new StreamReader(memoryStream, Encoding.UTF8))
                        {
                            string jsonText = await reader.ReadToEndAsync();

                            try
                            {
                                foreach (var chat in chats)
                                {
                                    Destroy(chat);
                                }
                                
                                string fixedText = jsonText.Replace("\\\"", "\"").Replace("\\\\", "\\").Replace("\\\"", "\"").Replace("\"\"", "\"").Replace("\"{", "{").Replace("}\"", "}");

                                Debug.Log(fixedText);
                                
                                RequestData requestData = JsonUtility.FromJson<RequestData>(fixedText);

                                Debug.Log("Top Chat: " + requestData.top_chat);
                                Debug.Log("Texts:");
                                
                                Debug.Log(requestData.texts[0]);
                                foreach (string text in requestData.texts)
                                {
                                    var newChat = Instantiate(chatBox, chatList.transform);
                                    chats.Add(newChat);
                                    
                                    newChat.GetComponentInChildren<TMP_Text>().text = text;
                                }

                                topChat.text = requestData.top_chat;
                            }
                            catch (Exception ex)
                            {
                                Debug.LogError("JSON 파싱 에러: " + ex.Message);
                            }
                        }
                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await websocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "서버에서 종료됨",
                            CancellationToken.None);
                        panelReady.SetActive(true);
                    }
                    else
                    {
                        panelReady.SetActive(true);
                        Debug.LogError("예상치 못한 데이터 수신 발생 : " + result.MessageType);
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError("웹소켓 데이터 수신 중 에러 발생 : " + e.Message);
            panelReady.SetActive(true);
        }
    }

    private async Task SaveAudioFile(byte[] audioData)
    {
        try
        {
            await File.WriteAllBytesAsync(audioSavePath, audioData);
        }
        catch (Exception e)
        {
            Debug.LogError("음성 파일 저장 실패 : " + e.Message);
        }
    }

    private async void OnDestroy()
    {
        if (websocket != null && websocket.State == WebSocketState.Open)
        {
            await websocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "종료", CancellationToken.None);
            websocket.Dispose();
        }
    }

    private void ProcessImage(Byte[] imageData)
    {
        // 바이트 배열을 Texture2D로 변환
        Texture2D texture = new Texture2D(2, 2);
        if (texture.LoadImage(imageData))
        {
            // Texture2D를 Sprite로 변환
            Debug.Log(Time.time + " : 이미지 데이터 로드 성공");
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            clusterImage.sprite = sprite;  // UI에 적용
        }
        else
        {
            Debug.LogError("이미지 데이터 로드 실패");
        }
    }
}
