// File: QuestInfo.cs  (QuestInfoSO)
// Trách nhiệm (SRP): Định nghĩa tĩnh (bất biến) của một Quest trong ScriptableObject.
// Không chứa bất kỳ runtime state hay logic nào — chỉ là "bản thiết kế".

using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Quest", menuName = "DATN/Quest System/Quest Info", order = 0)]
public class QuestInfoSO : ScriptableObject
{
    // ── Identity ─────────────────────────────────────────────────────────────
    [field: SerializeField, Tooltip("ID duy nhất, tự sinh từ tên file. Không cần chỉnh tay.")]
    public string Id { get; private set; }

    // ── General Info ─────────────────────────────────────────────────────────
    [Header("General Info")]
    [Tooltip("Tên hiển thị trong UI của Quest (vd: 'Cuộc Điều Tra Bí Ẩn').")]
    public string displayName;

    [TextArea(2, 5), Tooltip("Mô tả ngắn về quest này.")]
    public string description;

    // ── Tasks ─────────────────────────────────────────────────────────────────
    [Header("Tasks (Nhiệm vụ con)")]
    [Tooltip(
        "Danh sách các task theo thứ tự. Player phải hoàn thành từng task trước khi sang task tiếp theo.\n" +
        "Ví dụ: Task 1 = 'Nói chuyện với NPC A', Task 2 = 'Thu thập 3 vật phẩm', ...")]
    public List<TaskInfo> tasks = new List<TaskInfo>();

    // ── Rewards & Flow ────────────────────────────────────────────────────────
    [Header("Rewards & Flow")]
    public int intelPointsReward;
    public int weaponTokenReward;
    public GameObject[] itemRewards;
    public int experienceReward;

    [Tooltip("Trỏ tới quest tiếp theo trong chuỗi tuyến tính. Để trống nếu đây là quest cuối.")]
    public QuestInfoSO nextLinearQuest;

    // ── Validation ────────────────────────────────────────────────────────────
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(Id))
            Id = this.name;

        // Cảnh báo nếu quest không có task nào
        if (tasks == null || tasks.Count == 0)
            Debug.LogWarning($"[QuestInfoSO] Quest '{name}' không có task nào. Hãy thêm ít nhất 1 task.", this);
    }

    // ── Nested: TaskInfo (Inline Serializable) ────────────────────────────────
    /// <summary>
    /// Định nghĩa tĩnh của một Task nhỏ bên trong Quest.<br/>
    /// Được thiết kế inline (không tách file riêng) vì task không cần tái sử dụng
    /// giữa các quest trong game tuyến tính.
    /// </summary>
    [System.Serializable]
    public class TaskInfo
    {
        [Tooltip("ID nội bộ của task, dùng để match với TaskSaveData (vd: 'talk_npc_a').")]
        public string taskId;

        [Tooltip("Tên task hiển thị trong UI (vd: 'Nói chuyện với Thám tử Minh').")]
        public string displayName;

        [TextArea(1, 3), Tooltip("Mô tả chi tiết yêu cầu của task này.")]
        public string description;

        [Tooltip("Số lần hành động cần thực hiện để hoàn thành task (vd: 3 = thu thập 3 vật phẩm, 1 = nói chuyện 1 lần).")]
        [Min(1)]
        public int requiredAmount = 1;
    }
}