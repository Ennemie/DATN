using System;
using System.Collections.Generic;
using UnityEngine;
using Firebase.Firestore;
using Firebase.Auth;
using Firebase.Extensions;

public class FirebaseSaveRepository : ISaveRepository
{
    private FirebaseFirestore db;
    private string currentUserId;

    public FirebaseSaveRepository()
    {
        db = FirebaseFirestore.DefaultInstance;
        if (FirebaseAuth.DefaultInstance.CurrentUser != null)
        {
            currentUserId = FirebaseAuth.DefaultInstance.CurrentUser.UserId;
        }
    }

    public void Save(GameSaveDTO data)
    {
        if (string.IsNullOrEmpty(currentUserId)) return;

        data.LastUpdatedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // 1. Bóc tách Khối Tài nguyên (Tương đương bảng con Resources)
        Dictionary<string, object> resourcesMap = new Dictionary<string, object>
        {
            { "IntelPoints", data.Resources.IntelPoints },
            { "WeaponTokens", data.Resources.WeaponTokens }
        };

        // 2. Bóc tách Khối Tiến trình (Chuyển List thành Array của các Maps)
        List<object> missionsList = new List<object>();
        foreach (var m in data.Progression.CompletedMissions)
        {
            missionsList.Add(new Dictionary<string, object>
            {
                { "MissionId", m.MissionId },
                { "MainClear", m.MainClear },
                { "StealthBonus", m.StealthBonus },
                { "AllIntelFound", m.AllIntelFound }
            });
        }
        Dictionary<string, object> progressionMap = new Dictionary<string, object>
        {
            { "CurrentChapter", data.Progression.CurrentChapter },
            { "CompletedMissions", missionsList }
        };

        // 3. Bóc tách Khối Túi đồ
        List<object> gadgetsList = new List<object>();
        foreach (var g in data.Inventory.Gadgets)
        {
            gadgetsList.Add(new Dictionary<string, object>
            {
                { "GadgetId", g.GadgetId },
                { "Level", g.Level },
                { "IsUnlocked", g.IsUnlocked }
            });
        }
        Dictionary<string, object> inventoryMap = new Dictionary<string, object>
        {
            { "Gadgets", gadgetsList }
        };

        // 4. GOM TẤT CẢ THÀNH CẤU TRÚC ĐA TẦNG CHUẨN ĐỒ ÁN
        Dictionary<string, object> structuredData = new Dictionary<string, object>
        {
            { "DataVersion", data.DataVersion },
            { "LastUpdatedTimestamp", data.LastUpdatedTimestamp },
            { "Resources", resourcesMap },
            { "Progression", progressionMap },
            { "Inventory", inventoryMap },
            
            // MẸO PHÒNG THÂN: Vẫn giữ trường json_data ẩn để hàm Load phía dưới đọc 1 dòng là xong, không cần parse tay phức tạp!
            { "json_data", JsonUtility.ToJson(data) } 
        };

        // Đẩy lên Firestore
        db.Collection("player_saves").Document(currentUserId)
            .SetAsync(structuredData)
            .ContinueWithOnMainThread(task => {
                if (task.IsCompleted) {
                    Debug.Log("<color=cyan>FirebaseSave:</color> Đã lưu cấu trúc ĐATN lên mây!");
                }
            });
    }

    public void Load(Action<GameSaveDTO> onLoaded)
    {
        if (string.IsNullOrEmpty(currentUserId)) {
            onLoaded?.Invoke(new GameSaveDTO());
            return;
        }

        db.Collection("player_saves").Document(currentUserId).GetSnapshotAsync().ContinueWithOnMainThread(task => {
            if (task.IsCompleted && task.Result.Exists) {
                // Nhờ có mẹo lưu kèm json_data ở trên, hàm Load vẫn ngắn gọn, chạy cực nhanh
                string jsonText = task.Result.GetValue<string>("json_data");
                GameSaveDTO loadedData = JsonUtility.FromJson<GameSaveDTO>(jsonText);
                onLoaded?.Invoke(loadedData);
            } else {
                onLoaded?.Invoke(new GameSaveDTO());
            }
        });
    }
}