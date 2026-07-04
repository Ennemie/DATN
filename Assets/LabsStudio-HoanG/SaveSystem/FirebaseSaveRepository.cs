using System;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Firebase.Auth;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class FirebaseSaveRepository : ISaveRepository
{
    // =========================================================================
    // [CẤU HÌNH DATA CONNECT]
    // =========================================================================
    private const string PROJECT_ID = "datt-f9730"; 
    private const string LOCATION = "asia-southeast1"; 
    private const string SERVICE_ID = "datt-f9730-service"; 
    private const string CONNECTOR_ID = "default";
    
    // API Key
    private const string API_KEY = "AIzaSyApDh_1NW9lEWRtdOnwKGiq16oGP8cuXJk";

    private readonly string baseUrl = $"https://firebasedataconnect.googleapis.com/v1beta/projects/{PROJECT_ID}/locations/{LOCATION}/services/{SERVICE_ID}/connectors/{CONNECTOR_ID}";

    public async void Save(GameSaveDTO data)
    {
        FirebaseAuth auth = FirebaseAuth.DefaultInstance;
        if (auth == null || auth.CurrentUser == null)
        {
            Debug.LogWarning("<color=orange>[FirebaseDataConnect]</color> ✖ Save bị hủy: Người dùng chưa đăng nhập.");
            return;
        }

        string userId = auth.CurrentUser.UserId;
        Debug.Log($"<color=orange>[FirebaseDataConnect]</color> ▶ Save BẮT ĐẦU | Lưu đa bảng cho UID: {userId}");

        try
        {
            string idToken = await auth.CurrentUser.TokenAsync(false);
            string endpoint = $"{baseUrl}:executeMutation";

            // 1. Lưu Base (Player, Resource, Progress)
            var baseVariables = new {
                playerId = userId,
                username = data.username,
                dataVersion = data.dataVersion,
                lastUpdated = data.lastUpdated,
                intelPoints = data.resources.intelPoints,
                weaponTokens = data.resources.weaponTokens,
                currentChapter = data.progress.currentChapter
            };
            
            await SendPostRequest(endpoint, JsonConvert.SerializeObject(new {
                operationName = "UpsertPlayerBase",
                variables = baseVariables
            }), idToken);

            // 2. Lưu từng Mission
            if (data.missions != null)
            {
                foreach (var mission in data.missions)
                {
                    var missionVars = new {
                        id = $"{userId}_{mission.missionId}",
                        playerId = userId,
                        missionId = mission.missionId,
                        mainClear = mission.mainClear,
                        stealthBonus = mission.stealthBonus,
                        allIntelFound = mission.allIntelFound
                    };
                    await SendPostRequest(endpoint, JsonConvert.SerializeObject(new {
                        operationName = "UpsertPlayerMission",
                        variables = missionVars
                    }), idToken);
                }
            }

            // 3. Lưu từng Gadget
            if (data.gadgets != null)
            {
                foreach (var gadget in data.gadgets)
                {
                    var gadgetVars = new {
                        id = $"{userId}_{gadget.gadgetId}",
                        playerId = userId,
                        gadgetId = gadget.gadgetId,
                        currentLevel = gadget.currentLevel,
                        isUnlocked = gadget.isUnlocked
                    };
                    await SendPostRequest(endpoint, JsonConvert.SerializeObject(new {
                        operationName = "UpsertPlayerGadget",
                        variables = gadgetVars
                    }), idToken);
                }
            }

            Debug.Log($"<color=lime>[FirebaseDataConnect]</color> ■ Save THÀNH CÔNG | Đã lưu vào 5 bảng.");
        }
        catch (Exception e)
        {
            Debug.LogError($"<color=red>[FirebaseDataConnect]</color> ✖ Save LỖI: {e.Message}");
        }
    }

    public async void Load(Action<GameSaveDTO> onLoaded)
    {
        FirebaseAuth auth = FirebaseAuth.DefaultInstance;
        if (auth == null || auth.CurrentUser == null)
        {
            Debug.LogWarning("<color=orange>[FirebaseDataConnect]</color> ✖ Load bị hủy: Người dùng chưa đăng nhập. → Fallback Local.");
            onLoaded?.Invoke(null);
            return;
        }

        string userId = auth.CurrentUser.UserId;
        Debug.Log($"<color=cyan>[FirebaseDataConnect]</color> ▶ Load BẮT ĐẦU | Truy vấn GraphQL đa bảng cho UID: {userId}...");

        try
        {
            string idToken = await auth.CurrentUser.TokenAsync(false);
            string endpoint = $"{baseUrl}:executeQuery";

            var payload = new {
                operationName = "GetFullPlayerGame",
                variables = new { playerId = userId }
            };

            string responseJson = await SendPostRequest(endpoint, JsonConvert.SerializeObject(payload), idToken);
            Debug.Log($"<color=cyan>[FirebaseDataConnect]</color> Raw Load Response: {responseJson}");

            JObject responseObj = JObject.Parse(responseJson);
            JToken dataToken = responseObj["data"];

            if (dataToken != null && dataToken["player"] != null && dataToken["player"].HasValues)
            {
                GameSaveDTO loadedData = new GameSaveDTO();
                
                // Parse Player
                loadedData.username = dataToken["player"]["username"]?.ToString();
                loadedData.dataVersion = dataToken["player"]["dataVersion"]?.ToObject<int>() ?? 1;
                loadedData.lastUpdated = dataToken["player"]["lastUpdated"]?.ToObject<long>() ?? 0;

                // Parse Resource
                if (dataToken["playerResource"] != null && dataToken["playerResource"].HasValues)
                {
                    loadedData.resources.intelPoints = dataToken["playerResource"]["intelPoints"]?.ToObject<int>() ?? 0;
                    loadedData.resources.weaponTokens = dataToken["playerResource"]["weaponTokens"]?.ToObject<int>() ?? 0;
                }

                // Parse Progress
                if (dataToken["playerProgress"] != null && dataToken["playerProgress"].HasValues)
                {
                    loadedData.progress.currentChapter = dataToken["playerProgress"]["currentChapter"]?.ToString();
                }

                // Parse Missions
                if (dataToken["playerMissions"] != null && dataToken["playerMissions"].Type == JTokenType.Array)
                {
                    loadedData.missions = dataToken["playerMissions"].ToObject<List<MissionData>>();
                }

                // Parse Gadgets
                if (dataToken["playerGadgets"] != null && dataToken["playerGadgets"].Type == JTokenType.Array)
                {
                    loadedData.gadgets = dataToken["playerGadgets"].ToObject<List<GadgetData>>();
                }

                Debug.Log($"<color=lime>[FirebaseDataConnect]</color> ■ Load THÀNH CÔNG | Dữ liệu kéo về hoàn tất.");
                onLoaded?.Invoke(loadedData);
            }
            else
            {
                Debug.LogWarning($"<color=orange>[FirebaseDataConnect]</color> ✖ Không tìm thấy dữ liệu trên Cloud (hoặc game mới). → Fallback Local.");
                onLoaded?.Invoke(null);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"<color=red>[FirebaseDataConnect]</color> ✖ Load LỖI: {e.Message} → Fallback Local.");
            onLoaded?.Invoke(null);
        }
    }

    private Task<string> SendPostRequest(string url, string jsonBody, string authToken)
    {
        var tcs = new TaskCompletionSource<string>();
        UnityMainThreadDispatcher.Enqueue(StartWebRequest(url, jsonBody, authToken, tcs));
        return tcs.Task;
    }

    private System.Collections.IEnumerator StartWebRequest(string url, string jsonBody, string authToken, TaskCompletionSource<string> tcs)
    {
        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("x-goog-api-key", API_KEY);
            request.SetRequestHeader("X-Firebase-Auth-Token", authToken);

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
            {
                tcs.SetException(new Exception($"{request.responseCode} - {request.error}\nBody: {request.downloadHandler.text}"));
            }
            else
            {
                tcs.SetResult(request.downloadHandler.text);
            }
        }
    }
}