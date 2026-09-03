// ============================================================================
// MissionObjective.cs
// ============================================================================
// Chức năng: Base class cho mọi Objective.
//
// PERSISTENCE INTEGRATION:
// - ObjectiveId là identity ổn định.
// - IsCompleted là runtime state.
// - CompleteObjective() KHÔNG tự ghi save.json.
//   CheckpointManager mới là commit point.
// - ApplyPersistentState() chỉ áp dụng trạng thái đã commit khi Continue hoặc
//   khi restore checkpoint sau Player Death.
// - Restore không bắn AnyObjectiveCompleted để tránh chạy mission flow lần nữa.
// ============================================================================

using UnityEngine;

public class MissionObjective : MonoBehaviour
{
    public static event System.Action<MissionObjective>
        AnyObjectiveCompleted;

    [Header("Objective")]
    [SerializeField] private string objectiveId = "OBJECTIVE_ID";
    [SerializeField] private bool isCompleted;

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    public string ObjectiveId => objectiveId;
    public bool IsCompleted => isCompleted;

    public virtual void CompleteObjective()
    {
        if (IsPersistentRestoreInProgress())
            return;

        if (isCompleted)
            return;

        isCompleted = true;

        if (logDebug)
        {
            Debug.Log(
                "[MissionObjective] Completed: " +
                objectiveId
            );
        }

        if (MissionObjectiveListUI.Instance != null)
        {
            MissionObjectiveListUI.Instance
                .CompleteObjective(objectiveId);
        }
        else if (logDebug)
        {
            Debug.LogWarning(
                "[MissionObjective] MissionObjectiveListUI.Instance " +
                "is null. Objective UI will not be marked completed."
            );
        }

        AnyObjectiveCompleted?.Invoke(this);
    }

    public virtual void ResetObjective()
    {
        isCompleted = false;

        if (logDebug)
        {
            Debug.Log(
                "[MissionObjective] Reset: " +
                objectiveId
            );
        }
    }

    /// <summary>
    /// Apply persistent state without firing completion events.
    /// Dùng cho Continue và checkpoint restore.
    /// </summary>
    public void ApplyPersistentState(
        MissionProgressState state)
    {
        bool completed =
            state == MissionProgressState.Completed;

        isCompleted = completed;

        OnPersistentStateApplied(completed);
    }

    /// <summary>
    /// Cho subclass tự tắt prompt / trigger nếu objective đã completed.
    /// Không chứa save logic.
    /// </summary>
    protected virtual void OnPersistentStateApplied(
        bool completed)
    {
    }

    private bool IsPersistentRestoreInProgress()
    {
        MissionFlowManager flowManager =
            FindAnyObjectByType<MissionFlowManager>();

        return flowManager != null &&
               flowManager.IsPersistentRestoreInProgress;
    }
}
