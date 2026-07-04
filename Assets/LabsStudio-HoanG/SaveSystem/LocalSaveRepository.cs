using System;
using System.IO;
using UnityEngine;
using Newtonsoft.Json; 

public class LocalSaveRepository : ISaveRepository
{
    // Đường dẫn lưu file vật lý dưới máy Client (save.dat)
    private readonly string saveFilePath = Path.Combine(Application.persistentDataPath, "save.dat");

    public void Save(GameSaveDTO data)
    {
        try
        {
            // Cập nhật lại mốc thời gian lưu game mới nhất
            long oldTimestamp = data.lastUpdated;
            data.lastUpdated = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            Debug.Log($"<color=yellow>[LocalSave]</color> ▶ Save BẮT ĐẦU | lastUpdated: {oldTimestamp} → {data.lastUpdated}");

            // Dùng Newtonsoft dịch siêu tốc, tự động format thụt lề đẹp mắt
            string jsonText = JsonConvert.SerializeObject(data, Formatting.Indented);

            // Ghi đè file vật lý
            File.WriteAllText(saveFilePath, jsonText);
            Debug.Log($"<color=lime>[LocalSave]</color> ■ Save THÀNH CÔNG | Kích thước JSON: {jsonText.Length} ký tự | Đường dẫn: {saveFilePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"<color=red>[LocalSave]</color> ✖ Save THẤT BẠI: {e.Message}");
        }
    }

    public void Load(Action<GameSaveDTO> onLoaded)
    {
        Debug.Log($"<color=cyan>[LocalSave]</color> ▶ Load BẮT ĐẦU | Đường dẫn: {saveFilePath}");

        // Nếu chưa từng có file save (Người chơi mới)
        if (!File.Exists(saveFilePath))
        {
            Debug.Log("<color=cyan>[LocalSave]</color>   └─ Không tìm thấy file save.dat → Khởi tạo GameSaveDTO mới (game mới).");
            onLoaded?.Invoke(new GameSaveDTO());
            return;
        }

        try
        {
            // Đọc văn bản JSON từ ổ cứng
            string jsonText = File.ReadAllText(saveFilePath);
            Debug.Log($"<color=cyan>[LocalSave]</color>   ├─ Đọc file thành công | Kích thước: {jsonText.Length} ký tự");

            // Dịch ngược từ JSON về lại Object C# trong RAM
            GameSaveDTO loadedData = JsonConvert.DeserializeObject<GameSaveDTO>(jsonText);

            Debug.Log($"<color=lime>[LocalSave]</color> ■ Load THÀNH CÔNG | " +
                      $"username=\"{loadedData.username}\" | " +
                      $"missions={loadedData.missions.Count} | " +
                      $"gadgets={loadedData.gadgets.Count} | " +
                      $"lastUpdated={loadedData.lastUpdated}");
            onLoaded?.Invoke(loadedData);
        }
        catch (Exception e)
        {
            Debug.LogError($"<color=red>[LocalSave]</color> ✖ Load THẤT BẠI: {e.Message} → Trả về GameSaveDTO mới để tránh crash.");
            onLoaded?.Invoke(new GameSaveDTO());
        }
    }
}