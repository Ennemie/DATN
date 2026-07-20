// File: QuestTask.cs
// Trách nhiệm (SRP): Quản lý runtime state và tiến độ của MỘT task duy nhất.
// Không biết về Quest cha, QuestManager, hay Firebase — hoàn toàn độc lập.

using UnityEngine;

public class QuestTask
{
    // ── Read-only References ──────────────────────────────────────────────────
    /// <summary>Định nghĩa tĩnh của task này (lấy từ QuestInfoSO.TaskInfo).</summary>
    public QuestInfoSO.TaskInfo Info { get; private set; }

    /// <summary>Dữ liệu runtime được lưu/load từ Firebase.</summary>
    public TaskSaveData SaveData { get; private set; }

    // ── Computed Properties ───────────────────────────────────────────────────
    /// <summary>Task đã hoàn thành khi currentProgress đạt đủ requiredAmount.</summary>
    public bool IsCompleted => SaveData.currentProgress >= Info.requiredAmount;

    /// <summary>Tiến độ chuẩn hóa về [0, 1] — dùng trực tiếp cho UI Slider/Progress Bar.</summary>
    public float NormalizedProgress =>
        Info.requiredAmount > 0
        ? Mathf.Clamp01((float)SaveData.currentProgress / Info.requiredAmount)
        : 1f;

    /// <summary>Chuỗi tiến độ dạng "2 / 5" — dùng cho UI Text.</summary>
    public string ProgressText =>
        $"{Mathf.Min(SaveData.currentProgress, Info.requiredAmount)} / {Info.requiredAmount}";

    // ── Constructor ───────────────────────────────────────────────────────────
    public QuestTask(QuestInfoSO.TaskInfo info, TaskSaveData saveData)
    {
        Info     = info;
        SaveData = saveData;
    }

    // ── Mutation Methods ──────────────────────────────────────────────────────
    /// <summary>
    /// Tăng tiến độ của task. Tự động clamp để không vượt quá requiredAmount.
    /// </summary>
    /// <param name="amount">Số lượng tăng (mặc định 1).</param>
    /// <returns>True nếu task hoàn thành SAU khi cộng progress này.</returns>
    public bool AddProgress(int amount = 1)
    {
        if (IsCompleted) return true; // đã xong, bỏ qua

        SaveData.currentProgress = Mathf.Min(
            SaveData.currentProgress + amount,
            Info.requiredAmount
        );

        return IsCompleted;
    }

    /// <summary>
    /// Hoàn thành task ngay lập tức — dùng trong cutscene event để skip task.
    /// </summary>
    public void ForceComplete()
    {
        SaveData.currentProgress = Info.requiredAmount;
    }

    /// <summary>Reset task về 0 (dùng khi cần cho quest restart).</summary>
    public void Reset()
    {
        SaveData.currentProgress = 0;
    }
}
