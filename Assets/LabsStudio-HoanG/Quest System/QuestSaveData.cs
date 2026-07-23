// File: QuestSaveData.cs
// Trách nhiệm (SRP): Lưu trữ & serialize toàn bộ state runtime của một Quest.
// Không chứa logic — chỉ là Data Transfer Object (DTO) cho Firebase / PlayerPrefs.

using System.Collections.Generic;
using Newtonsoft.Json;

[System.Serializable]
public class QuestSaveData
{
    /// <summary>Khớp với QuestInfoSO.Id để tìm lại đúng quest khi load.</summary>
    [JsonProperty("missionId")]
    public string questId;

    /// <summary>Trạng thái tổng của quest (Locked / CanStart / Active / Completed).</summary>
    public QuestState state;

    /// <summary>Index của task đang được thực hiện trong danh sách Quest.Tasks.</summary>
    public int currentTaskIndex;

    /// <summary>
    /// Tiến độ save của từng task theo thứ tự — luôn được khởi tạo đủ phần tử
    /// tương ứng với số task trong QuestInfoSO.tasks[].
    /// </summary>
    public List<TaskSaveData> taskSaves;

    /// <summary>Backward-compatible mission flag used by the Firebase schema.</summary>
    public bool stealthBonus;

    /// <summary>Backward-compatible mission flag used by the Firebase schema.</summary>
    public bool allIntelFound;

    /// <summary>
    /// Legacy mission completion flag. Kept for Firebase compatibility.
    /// Quest state remains the source of truth in the runtime quest system.
    /// </summary>
    public bool mainClear
    {
        get => state == QuestState.Completed;
        set
        {
            if (value)
                state = QuestState.Completed;
        }
    }

    /// <summary>Default ctor required for JSON deserialization.</summary>
    public QuestSaveData()
    {
        taskSaves = new List<TaskSaveData>();
    }

    /// <summary>Khởi tạo mới với tất cả task ở mức tiến độ 0.</summary>
    public QuestSaveData(string id, QuestState initialState, int taskCount)
    {
        questId          = id;
        state            = initialState;
        currentTaskIndex = 0;
        taskSaves        = new List<TaskSaveData>(taskCount);
    }

    // ── Migration helper ─────────────────────────────────────────────────────
    // Giữ backward-compat với dữ liệu Firebase cũ (nếu có).
    // QuestManager sẽ gọi hàm này khi phát hiện taskSaves == null sau khi load.
    public void MigrateFromLegacy(int legacyObjectiveIndex, int legacyProgress, int taskCount)
    {
        currentTaskIndex = legacyObjectiveIndex;
        taskSaves        = new List<TaskSaveData>(taskCount);
        for (int i = 0; i < taskCount; i++)
        {
            var t = new TaskSaveData($"task_{i}");
            // Các task đã qua → coi như đã hoàn thành (progress = -1 là sentinel)
            // QuestTask sẽ đọc và xử lý đúng khi init
            if (i < legacyObjectiveIndex)
                t.currentProgress = int.MaxValue; // đã hoàn thành
            else if (i == legacyObjectiveIndex)
                t.currentProgress = legacyProgress;
            taskSaves.Add(t);
        }
    }
}