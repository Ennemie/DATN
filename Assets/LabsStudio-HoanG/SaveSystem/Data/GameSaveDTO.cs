using System;
using System.Collections.Generic;

[Serializable]
public class GameSaveDTO
{
    // ----------------------------------------------------
    // TRƯỜNG BẮT BUỘC ĐỂ QUẢN LÝ PHIÊN BẢN (ANTI-CRASH)
    // ----------------------------------------------------
    public int DataVersion;
    public long LastUpdatedTimestamp;

    // ----------------------------------------------------
    // CÁC KHỐI DỮ LIỆU CỐT LÕI CỦA CYPHER 007
    // ----------------------------------------------------
    public ResourcesData Resources;
    public ProgressionData Progression;
    public InventoryData Inventory;

    /// <summary>
    /// Hàm khởi tạo mặc định cho người chơi mới tinh (New Game)
    /// </summary>
    public GameSaveDTO()
    {
        DataVersion = 1;
        LastUpdatedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        Resources = new ResourcesData();
        Progression = new ProgressionData();
        Inventory = new InventoryData();
    }
}

// ========================================================
// CHI TIẾT CÁC KHỐI DỮ LIỆU CON
// ========================================================

[Serializable]
public class ResourcesData
{
    public int IntelPoints = 0;    // Điểm tình báo cày cuốc
    public int WeaponTokens = 0;   // Mảnh thiết kế súng hiếm
}

[Serializable]
public class ProgressionData
{
    public string CurrentChapter = "chapter_01"; // Chương hiện tại
    public List<MissionState> CompletedMissions = new List<MissionState>(); // Danh sách các ải đã qua
}

[Serializable]
public class InventoryData
{
    public List<GadgetState> Gadgets = new List<GadgetState>(); // Kho đồ chơi công nghệ
}

// ========================================================
// CÁC ĐỊNH DẠNG DỮ LIỆU ĐỘC LẬP (STRUCTS/CLASSES)
// ========================================================

[Serializable]
public class MissionState
{
    public string MissionId;
    public bool MainClear;      // Đã qua màn chưa
    public bool StealthBonus;   // Đạt thành tích lén lút chưa
    public bool AllIntelFound;  // Nhặt hết ổ cứng mật chưa
}

[Serializable]
public class GadgetState
{
    public string GadgetId;
    public int Level;           // Cấp độ nâng cấp (0 là chưa mở khóa)
    public bool IsUnlocked;
}
