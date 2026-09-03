// ============================================================================
// MissionCheckpointTrigger.cs
// ============================================================================
// Compatibility checkpoint trigger.
//
// IMPORTANT:
// - Checkpoint authority = CheckpointManager.
// - Không còn lưu checkpoint trực tiếp vào MissionFailManager.
// - Nếu project đã dùng MissionElementTrigger -> SaveCheckpointOnly thì
//   script này không bắt buộc phải dùng.
//
// PERSISTENCE:
// - CheckpointManager commit Map + Checkpoint.
// - Khi Continue, CheckpointManager resolve lại Transform từ scene.
// ============================================================================

using UnityEngine;

[RequireComponent(typeof(Collider))]
public class MissionCheckpointTrigger : MonoBehaviour
{
    [Header("Manager")]
    [SerializeField] private CheckpointManager checkpointManager;
    [SerializeField] private MissionCheckpointToast checkpointToast;

    [Header("Checkpoint")]
    [Min(1)]
    [SerializeField] private int mapNumber = 1;

    [Min(1)]
    [SerializeField] private int checkpointNumber = 1;

    [SerializeField] private Transform checkpointPoint;
    [SerializeField] private string checkpointMessage = "Checkpoint";

    [Header("Trigger")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool oneTime = true;
    [SerializeField] private bool disableAfterTriggered = false;

    private bool hasTriggered;
    private Collider triggerCollider;

    public int MapNumber => mapNumber;
    public int CheckpointNumber => checkpointNumber;
    public Transform CheckpointPoint => checkpointPoint;

    private void Reset()
    {
        Collider col = GetComponent<Collider>();

        if (col != null)
            col.isTrigger = true;
    }

    private void Awake()
    {
        triggerCollider = GetComponent<Collider>();

        if (triggerCollider != null)
            triggerCollider.isTrigger = true;

        // Chức năng mới:
        // Tìm CheckpointToast kể cả khi visual root của Toast đang inactive.
        if (checkpointToast == null)
        {
            MissionCheckpointToast[] toasts = FindObjectsByType<MissionCheckpointToast>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

            if (toasts != null && toasts.Length > 0)
                checkpointToast = toasts[0];
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (oneTime && hasTriggered)
            return;

        if (!IsPlayer(other))
            return;

        if (checkpointManager == null)
        {
            checkpointManager =
                FindAnyObjectByType<CheckpointManager>();
        }

        if (checkpointToast == null)
        {
            // Chức năng mới:
            // Thử resolve lại ngay lúc trigger thực sự được kích hoạt, tránh phụ thuộc thứ tự Awake.
            MissionCheckpointToast[] toasts = FindObjectsByType<MissionCheckpointToast>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

            if (toasts != null && toasts.Length > 0)
                checkpointToast = toasts[0];
        }

        if (checkpointManager == null)
        {
            Debug.LogWarning(
                "[MissionCheckpointTrigger] Missing CheckpointManager.",
                this
            );
            return;
        }

        Transform cp =
            checkpointPoint != null
                ? checkpointPoint
                : transform;

        string message =
            string.IsNullOrWhiteSpace(checkpointMessage)
                ? "Checkpoint"
                : checkpointMessage;

        bool accepted =
            checkpointManager.ReachCheckpoint(
                mapNumber,
                checkpointNumber,
                cp,
                message
            );

        if (!accepted)
            return;

        hasTriggered = true;

        // CŨ:
        // if (checkpointToast != null)
        //     checkpointToast.ShowCheckpoint(message);
        //
        // MỚI:
        // CheckpointManager phát Toast sau khi commit thành công.

        if (disableAfterTriggered)
        {
            gameObject.SetActive(false);
        }
        else if (triggerCollider != null && oneTime)
        {
            triggerCollider.enabled = false;
        }
    }

    private bool IsPlayer(Collider other)
    {
        if (other.CompareTag(playerTag))
            return true;

        Transform root = other.transform.root;

        return root != null &&
               root.CompareTag(playerTag);
    }
}
