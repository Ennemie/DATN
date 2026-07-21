// File: QuestInfo.cs  (QuestInfoSO)
// Trách nhiệm (SRP): Nguồn dữ liệu tĩnh (bất biến) DUY NHẤT cho một Quest.
// Chứa đủ thông tin để MỌI hệ thống (Minimap, NPC, UI, Cutscene, Reward) đọc trực tiếp.
// Không chứa runtime state hay logic — chỉ là "bản thiết kế" được định nghĩa bởi designer.

using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Quest", menuName = "DATN/Quest System/Quest Info", order = 0)]
public class QuestInfoSO : ScriptableObject
{
    // ══════════════════════════════════════════════════════════════════════════
    // IDENTITY
    // ══════════════════════════════════════════════════════════════════════════
    [field: SerializeField, Tooltip("ID duy nhất, tự sinh từ tên file. KHÔNG chỉnh tay.")]
    public string Id { get; private set; }

    // ══════════════════════════════════════════════════════════════════════════
    // GENERAL INFO  →  UI Journal / Quest Log đọc vào đây
    // ══════════════════════════════════════════════════════════════════════════
    [Header("━━━  GENERAL INFO  ━━━")]
    [Tooltip("Tên quest hiển thị trên UI (vd: 'Cuộc Điều Tra Bí Ẩn').")]
    public string displayName;

    [TextArea(2, 5), Tooltip("Mô tả tổng quan. Hiện trong Quest Journal khi player mở xem.")]
    public string description;

    [Tooltip("Icon đại diện cho quest. Hiện trên Journal, HUD góc màn hình.")]
    public Sprite questIcon;

    [Tooltip("Loại nhiệm vụ: Main (cốt truyện) hay Side (phụ).")]
    public QuestCategory category = QuestCategory.Main;

    // ══════════════════════════════════════════════════════════════════════════
    // NPC  →  Hệ thống NPC/Dialogue đọc vào đây
    // ══════════════════════════════════════════════════════════════════════════
    [Header("━━━  NPC  ━━━")]
    [Tooltip(
        "NPC giao nhiệm vụ cho player.\n" +
        "Hệ thống NPC dùng questGiverId để biết khi nào mở dialogue 'nhận quest'.")]
    public NpcQuestRef questGiver;

    [Tooltip(
        "NPC để player đến nộp nhiệm vụ (có thể khác hoặc giống questGiver).\n" +
        "Để trống nếu quest tự hoàn thành (vd: cutscene tự kết thúc).")]
    public NpcQuestRef questTurnIn;

    // ══════════════════════════════════════════════════════════════════════════
    // TASKS  →  QuestManager & UI Task List đọc vào đây
    // ══════════════════════════════════════════════════════════════════════════
    [Header("━━━  TASKS (Nhiệm vụ con)  ━━━")]
    [Tooltip(
        "Danh sách task theo thứ tự tuyến tính.\n" +
        "Player phải xong Task N mới sang Task N+1.\n" +
        "Mỗi task chứa đủ data cho: UI, Minimap, NPC, Cutscene.")]
    public List<TaskInfo> tasks = new List<TaskInfo>();

    // ══════════════════════════════════════════════════════════════════════════
    // REWARDS & FLOW  →  Hệ thống Reward đọc vào đây
    // ══════════════════════════════════════════════════════════════════════════
    [Header("━━━  REWARDS & FLOW  ━━━")]
    [Tooltip("Điểm tình báo (Intel Points) thưởng khi hoàn thành quest.")]
    public int intelPointsReward;

    [Tooltip("Token vũ khí (Weapon Token) thưởng khi hoàn thành quest.")]
    public int weaponTokenReward;

    [Tooltip("Kinh nghiệm (EXP) thưởng khi hoàn thành quest.")]
    public int experienceReward;

    [Tooltip("Các vật phẩm (prefab hoặc SO) được trao khi quest hoàn thành.")]
    public GameObject[] itemRewards;

    [Tooltip("Quest tiếp theo trong chuỗi tuyến tính. Để trống nếu đây là quest cuối.")]
    public QuestInfoSO nextLinearQuest;

    // ══════════════════════════════════════════════════════════════════════════
    // OnValidate — kiểm tra dữ liệu ngay trong Editor
    // ══════════════════════════════════════════════════════════════════════════
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(Id))
            Id = this.name;

        if (tasks == null || tasks.Count == 0)
            Debug.LogWarning($"[QuestInfoSO] '{name}' chưa có task nào!", this);

        // Tự điền taskId nếu designer bỏ trống
        if (tasks != null)
        {
            for (int i = 0; i < tasks.Count; i++)
            {
                if (string.IsNullOrEmpty(tasks[i].taskId))
                    tasks[i].taskId = $"{name}_task_{i}";
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // NESTED TYPES
    // ══════════════════════════════════════════════════════════════════════════

    // ── TaskInfo ─────────────────────────────────────────────────────────────
    /// <summary>
    /// Định nghĩa tĩnh của một task con bên trong Quest.<br/>
    /// Chứa đủ data cho: UI Task List, Minimap marker, NPC trigger, Cutscene hook.
    /// </summary>
    [System.Serializable]
    public class TaskInfo
    {
        // ── Core ──────────────────────────────────────────────────────────────
        [Tooltip("ID nội bộ (auto-generated nếu để trống). Dùng để match TaskSaveData.")]
        public string taskId;

        [Tooltip("Tên task hiện trên UI (vd: 'Nói chuyện với Thám tử Minh').")]
        public string displayName;

        [TextArea(1, 3), Tooltip("Mô tả chi tiết. Hiện trong Quest Journal khi player bấm vào task.")]
        public string description;

        [Tooltip("Loại hành động cần thực hiện. Ảnh hưởng đến icon và logic trigger.")]
        public TaskType taskType = TaskType.Interact;

        [Min(1), Tooltip("Số lần phải thực hiện để hoàn thành task (vd: 3 = thu thập 3 vật phẩm).")]
        public int requiredAmount = 1;

        // ── UI  ───────────────────────────────────────────────────────────────
        [Header("UI")]
        [Tooltip("Icon riêng cho task này. Nếu để trống, dùng icon mặc định theo TaskType.")]
        public Sprite taskIcon;

        [TextArea(1, 2), Tooltip("Gợi ý ngắn hiện bên dưới task (vd: 'Kiểm tra khu vực bếp').")]
        public string hintText;

        // ── Minimap ───────────────────────────────────────────────────────────
        [Header("Minimap")]
        [Tooltip("Hiện marker vị trí mục tiêu trên minimap khi task này đang active.")]
        public bool showOnMinimap = true;

        [Tooltip(
            "Vị trí thế giới (World Position) của mục tiêu.\n" +
            "Hệ thống Minimap đọc để đặt marker đúng chỗ.")]
        public Vector3 targetWorldPosition;

        [Tooltip("Tên địa điểm hiện trên minimap (vd: 'Phòng ngủ tầng 2').")]
        public string targetLocationName;

        [Tooltip("Icon marker trên minimap. Để trống = dùng icon mặc định của hệ thống Minimap.")]
        public Sprite minimapMarkerIcon;

        // ── NPC ───────────────────────────────────────────────────────────────
        [Header("NPC")]
        [Tooltip(
            "NPC liên quan đến task này (nếu có).\n" +
            "Hệ thống NPC dùng npcId để biết khi nào bật dialogue đúng.\n" +
            "Để trống nếu task không liên quan đến NPC.")]
        public NpcQuestRef linkedNpc;

        // ── Cutscene ──────────────────────────────────────────────────────────
        [Header("Cutscene")]
        [Tooltip(
            "Tên cutscene phát trước khi task này bắt đầu (pre-task).\n" +
            "Dùng để EcCutsceneManager.InitCutscenes() tự kích hoạt đúng cảnh phim.\n" +
            "Để trống nếu không có cutscene.")]
        public string preCutsceneName;

        [Tooltip(
            "Tên cutscene phát SAU KHI task này hoàn thành (post-task).\n" +
            "Để trống nếu không có cutscene.")]
        public string postCutsceneName;
    }

    // ── NpcQuestRef ───────────────────────────────────────────────────────────
    /// <summary>
    /// Tham chiếu đến một NPC liên quan đến quest/task.<br/>
    /// Dùng string ID thay vì direct reference để tránh coupling với prefab cụ thể.
    /// </summary>
    [System.Serializable]
    public class NpcQuestRef
    {
        [Tooltip(
            "ID của NPC trong scene (khớp với field 'npcId' trên script NPC).\n" +
            "Hệ thống NPC sẽ tìm NPC có ID này để bật/tắt dialogue đúng lúc.")]
        public string npcId;

        [Tooltip("Tên NPC hiện trên UI (vd: 'Thám tử Minh'). Dùng cho Quest Journal.")]
        public string displayName;

        [Tooltip("Icon avatar NPC trên UI Journal.")]
        public Sprite npcAvatar;
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// SUPPORTING ENUMS (nằm ngoài class để dùng ở nhiều nơi)
// ══════════════════════════════════════════════════════════════════════════════

/// <summary>Phân loại quest — dùng để lọc trong UI Journal.</summary>
public enum QuestCategory
{
    [Tooltip("Nhiệm vụ chính cốt truyện — phải làm để tiến game.")]
    Main,

    [Tooltip("Nhiệm vụ phụ — không bắt buộc nhưng có thưởng.")]
    Side,
}

/// <summary>Loại hành động của task — ảnh hưởng icon UI và logic trigger.</summary>
public enum TaskType
{
    [Tooltip("Tương tác với NPC hoặc object (nhấn F, click, v.v.).")]
    Interact,

    [Tooltip("Đi đến một vị trí cụ thể.")]
    GoToLocation,

    [Tooltip("Thu thập vật phẩm.")]
    Collect,

    [Tooltip("Điều tra / khám phá (examine) đồ vật, hiện trường.")]
    Investigate,

    [Tooltip("Xem cutscene (task tự hoàn thành khi cutscene kết thúc).")]
    WatchCutscene,

    [Tooltip("Tiêu diệt kẻ địch.")]
    Eliminate,
}