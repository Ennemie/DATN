// Chức năng: Base class cho tất cả objective trong Mission System.
// - MissionEnterTriggerObjective / MissionInteractObjective kế thừa class này.
// - Khi CompleteObjective() được gọi, nó sẽ:
//   1) Set IsCompleted = true
//   2) Báo UI list objective hoàn thành
//   3) Bắn event AnyObjectiveCompleted để MissionFlowManager kiểm tra và chuyển element
using UnityEngine;

public class MissionObjective : MonoBehaviour
{
    public static event System.Action<MissionObjective> AnyObjectiveCompleted;

    [Header("Objective")]
    [SerializeField] private string objectiveId = "OBJECTIVE_ID";
    [SerializeField] private bool isCompleted;

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    public string ObjectiveId => objectiveId;
    public bool IsCompleted => isCompleted;

    public virtual void CompleteObjective()
    {
        if (isCompleted)
            return;

        isCompleted = true;

        if (logDebug)
            Debug.Log("[MissionObjective] Completed: " + objectiveId);

        // Nối sang UI objective list ở góc trái.
        if (MissionObjectiveListUI.Instance != null)
        {
            MissionObjectiveListUI.Instance.CompleteObjective(objectiveId);
        }
        else if (logDebug)
        {
            Debug.LogWarning("[MissionObjective] MissionObjectiveListUI.Instance is null. Objective UI will not be marked completed.");
        }

        // Nối sang MissionFlowManager.
        AnyObjectiveCompleted?.Invoke(this);
    }

    public virtual void ResetObjective()
    {
        isCompleted = false;

        if (logDebug)
            Debug.Log("[MissionObjective] Reset: " + objectiveId);
    }
}
