// File: TaskSaveData.cs
// Trách nhiệm (SRP): Lưu trữ & serialize state runtime của một Task duy nhất.
// Không chứa logic — chỉ là Data Transfer Object (DTO) cho Firebase / PlayerPrefs.

[System.Serializable]
public class TaskSaveData
{
    /// <summary>Khớp với TaskInfo.taskId để tìm lại đúng task khi load.</summary>
    public string taskId;

    /// <summary>Tiến độ hiện tại của task (0 → TaskInfo.requiredAmount).</summary>
    public int currentProgress;

    public TaskSaveData(string id)
    {
        taskId         = id;
        currentProgress = 0;
    }
}
