// File: Quest.cs
// Trách nhiệm (SRP): Orchestrate danh sách QuestTask — biết task nào đang active,
//                    điều phối chuyển task, và xác định khi nào quest hoàn thành.
// KHÔNG tự xử lý số học tiến độ — việc đó là của QuestTask.
// KHÔNG giao tiếp với Firebase hay UI — việc đó là của QuestManager.

using System.Collections.Generic;
using UnityEngine;

public class Quest
{
    // ── Read-only References ──────────────────────────────────────────────────
    /// <summary>Định nghĩa tĩnh (ScriptableObject) của quest này.</summary>
    public QuestInfoSO Info { get; private set; }

    /// <summary>Dữ liệu runtime được lưu/load từ Firebase.</summary>
    public QuestSaveData SaveData { get; private set; }

    /// <summary>Danh sách các QuestTask theo thứ tự — mỗi phần tử wrap 1 TaskInfo + TaskSaveData.</summary>
    public IReadOnlyList<QuestTask> Tasks => _tasks;
    private readonly List<QuestTask> _tasks;

    // ── Computed Properties ───────────────────────────────────────────────────
    /// <summary>Task đang được thực hiện. Null nếu quest chưa active hoặc đã hoàn thành.</summary>
    public QuestTask CurrentTask =>
        SaveData.currentTaskIndex < _tasks.Count
        ? _tasks[SaveData.currentTaskIndex]
        : null;

    /// <summary>Chỉ số task hiện tại (0-based).</summary>
    public int CurrentTaskIndex => SaveData.currentTaskIndex;

    /// <summary>Quest đã hoàn thành toàn bộ chưa.</summary>
    public bool IsFullyCompleted => SaveData.state == QuestState.Completed;

    /// <summary>Trạng thái quest hiện tại.</summary>
    public QuestState State => SaveData.state;

    // ── Constructor ───────────────────────────────────────────────────────────
    /// <summary>
    /// Khởi tạo Quest với Info tĩnh và SaveData động.<br/>
    /// Đảm bảo taskSaves luôn đủ phần tử khớp với số task trong Info.
    /// </summary>
    public Quest(QuestInfoSO questInfo, QuestSaveData saveData)
    {
        Info     = questInfo;
        SaveData = saveData;

        // Đảm bảo taskSaves đủ phần tử (xử lý cả migration từ data cũ)
        if (saveData.taskSaves == null || saveData.taskSaves.Count == 0)
        {
            saveData.taskSaves = new List<TaskSaveData>(questInfo.tasks.Count);
            for (int i = 0; i < questInfo.tasks.Count; i++)
                saveData.taskSaves.Add(new TaskSaveData(questInfo.tasks[i].taskId));
        }

        // Build danh sách QuestTask runtime
        _tasks = new List<QuestTask>(questInfo.tasks.Count);
        for (int i = 0; i < questInfo.tasks.Count; i++)
        {
            // Nếu taskSaves thiếu phần tử (migration cũ) → tạo mới
            TaskSaveData tsd = i < saveData.taskSaves.Count
                ? saveData.taskSaves[i]
                : new TaskSaveData(questInfo.tasks[i].taskId);

            _tasks.Add(new QuestTask(questInfo.tasks[i], tsd));
        }
    }

    // ── Mutation Methods (called by QuestManager only) ────────────────────────

    /// <summary>
    /// Tăng tiến độ của task hiện tại.<br/>
    /// Nếu task hoàn thành → tự động chuyển sang task tiếp theo.<br/>
    /// Nếu hết task → đánh dấu quest Completed.
    /// </summary>
    /// <param name="amount">Lượng progress tăng thêm.</param>
    /// <returns>TaskCompletionResult để QuestManager biết chuyện gì vừa xảy ra.</returns>
    public TaskCompletionResult ProgressCurrentTask(int amount = 1)
    {
        if (SaveData.state != QuestState.Active)
            return TaskCompletionResult.QuestNotActive;

        QuestTask task = CurrentTask;
        if (task == null)
            return TaskCompletionResult.AlreadyCompleted;

        bool taskDone = task.AddProgress(amount);

        if (!taskDone)
            return TaskCompletionResult.Progressed;

        // Task vừa hoàn thành → chuyển sang task kế
        return AdvanceToNextTask();
    }

    /// <summary>
    /// Hoàn thành ngay lập tức task hiện tại (dùng trong cutscene event).
    /// </summary>
    public TaskCompletionResult ForceCompleteCurrentTask()
    {
        if (SaveData.state != QuestState.Active)
            return TaskCompletionResult.QuestNotActive;

        QuestTask task = CurrentTask;
        if (task == null)
            return TaskCompletionResult.AlreadyCompleted;

        task.ForceComplete();
        return AdvanceToNextTask();
    }

    /// <summary>Chuyển quest sang trạng thái Active.</summary>
    public void Activate()
    {
        if (SaveData.state == QuestState.CanStart)
            SaveData.state = QuestState.Active;
        else
            Debug.LogWarning($"[Quest] Cố gắng kích hoạt quest '{Info.displayName}' " +
                             $"nhưng state hiện tại là {SaveData.state} (cần CanStart).");
    }

    // ── Private Helpers ───────────────────────────────────────────────────────
    private TaskCompletionResult AdvanceToNextTask()
    {
        SaveData.currentTaskIndex++;

        if (SaveData.currentTaskIndex >= _tasks.Count)
        {
            // Tất cả task hoàn thành → quest done
            SaveData.state = QuestState.Completed;
            return TaskCompletionResult.QuestCompleted;
        }

        return TaskCompletionResult.TaskCompleted;
    }
}

// ── Result Enum (tránh magic boolean) ─────────────────────────────────────────
/// <summary>Kết quả trả về khi gọi ProgressCurrentTask / ForceCompleteCurrentTask.</summary>
public enum TaskCompletionResult
{
    /// <summary>Task tăng tiến độ nhưng chưa xong.</summary>
    Progressed,

    /// <summary>Task vừa hoàn thành, chuyển sang task tiếp theo.</summary>
    TaskCompleted,

    /// <summary>Task cuối vừa xong → toàn bộ Quest hoàn thành.</summary>
    QuestCompleted,

    /// <summary>Quest không ở trạng thái Active nên không xử lý được.</summary>
    QuestNotActive,

    /// <summary>Quest đã hoàn thành từ trước rồi.</summary>
    AlreadyCompleted,
}