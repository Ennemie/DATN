using System.Collections.Generic;

// ══════════════════════════════════════════════════════════════════════════════
// TỔNG KHO CHỨA DỮ LIỆU RAM — khớp 100% với cấu trúc Database / Firebase
// Mỗi field tương ứng 1 bảng (hoặc node) trong cơ sở dữ liệu.
// ══════════════════════════════════════════════════════════════════════════════

[System.Serializable]
public class GameSaveDTO
{
    // ── Identity ──────────────────────────────────────────────────────────────
    public string username;
    public int dataVersion = 2;      // tăng lên 2 vì đổi missions → questSaves
    public long lastUpdated;         // Unix Timestamp

    // ── Resources (bảng Resources) ────────────────────────────────────────────
    public ResourcesData resources = new ResourcesData();

    // ── Progress (bảng Progress) ──────────────────────────────────────────────
    public ProgressData progress = new ProgressData();

    // ── Quests (bảng Quests) — thay thế missions cũ ───────────────────────────
    /// <summary>
    /// Danh sách save state của từng quest. QuestManager đọc/ghi vào đây.
    /// Dùng List thay vì Dictionary để JsonUtility serialize được.
    /// </summary>
    public List<QuestSaveData> questSaves = new List<QuestSaveData>();

    // ── Gadgets (bảng Gadget) ─────────────────────────────────────────────────
    public List<GadgetData> gadgets = new List<GadgetData>();
}

// ── ResourcesData ─────────────────────────────────────────────────────────────
[System.Serializable]
public class ResourcesData
{
    public int intelPoints;
    public int weaponTokens;
    public int experience;
}

// ── ProgressData ──────────────────────────────────────────────────────────────
[System.Serializable]
public class ProgressData
{
    public string currentChapter;
}

// ── GadgetData ────────────────────────────────────────────────────────────────
[System.Serializable]
public class GadgetData
{
    public string gadgetId;
    public int currentLevel;
    public bool isUnlocked;
}

// ── MissionData (DEPRECATED) ──────────────────────────────────────────────────
// Giữ lại để tránh lỗi compile nếu còn script cũ (GameCanvas) tham chiếu.
// TODO: Xóa sau khi GameCanvas đã được cập nhật dùng QuestManager.
[System.Serializable]
public class MissionData
{
    public string missionId;
    public bool mainClear;
    public bool stealthBonus;
    public bool allIntelFound;
}