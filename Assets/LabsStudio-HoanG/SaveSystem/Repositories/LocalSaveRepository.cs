using System;
using System.IO;
using UnityEngine;

public class LocalSaveRepository : ISaveRepository
{
    // Đường dẫn lưu file vật lý dưới máy Client (save.dat)
    private readonly string saveFilePath = Path.Combine(Application.persistentDataPath, "save.dat");

    public void Save(GameSaveDTO data)
    {
        try
        {
            // 1. Cập nhật lại mốc thời gian lưu game mới nhất
            data.LastUpdatedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // 2. Chuyển đổi Object dữ liệu thành chuỗi văn bản JSON (bật format đẹp để dễ đọc)
            string jsonText = JsonUtility.ToJson(data, true);

            // 3. Ghi đè chuỗi văn bản JSON này vào file vật lý trên ổ cứng
            File.WriteAllText(saveFilePath, jsonText);

            Debug.Log($"<color=yellow>LocalSave:</color> Đã lưu JSON thuần thành công tại: {saveFilePath}");
        }
        catch (Exception e)
        {
            Debug.LogError("Lỗi ghi file Save cục bộ: " + e.Message);
        }
    }

    public void Load(Action<GameSaveDTO> onLoaded)
    {
        // Nếu file save chưa từng tồn tại (Người chơi mới tinh)
        if (!File.Exists(saveFilePath))
        {
            Debug.Log("<color=yellow>LocalSave:</color> Không tìm thấy file cũ. Khởi tạo dữ liệu mới.");
            onLoaded?.Invoke(new GameSaveDTO()); 
            return;
        }

        try
        {
            // 1. Đọc toàn bộ chuỗi văn bản JSON từ file lên
            string jsonText = File.ReadAllText(saveFilePath);

            // 2. Dịch ngược chuỗi văn bản đó thành Object DTO trong C#
            GameSaveDTO loadedData = JsonUtility.FromJson<GameSaveDTO>(jsonText);

            Debug.Log("<color=yellow>LocalSave:</color> Tải file save JSON từ ổ cứng thành công!");
            
            // Bắn dữ liệu đọc được ra ngoài cho game sử dụng
            onLoaded?.Invoke(loadedData);
        }
        catch (Exception e)
        {
            Debug.LogError("Lỗi đọc file Save cục bộ: " + e.Message);
            
            // Nếu file bị lỗi cấu trúc, trả về cục mặc định để game không bị treo
            onLoaded?.Invoke(new GameSaveDTO());
        }
    }
}