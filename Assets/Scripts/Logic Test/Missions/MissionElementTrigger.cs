// ============================================================================
// MissionElementTrigger.cs
// ============================================================================
// Chức năng:
// - Trigger cho CompleteCurrentElement
// - ActivateElementByIndex
// - SaveCheckpointOnly
// - StartMission
// - PlayConversationOnly
//
// PERSISTENCE INTEGRATION:
// - Checkpoint vẫn thuộc quyền CheckpointManager.
// - Trigger one-time có thể được commit theo persistent trigger ID.
// - Continue / Player death restore sẽ áp dụng lại trạng thái đã commit,
//   tránh trigger cũ tự chạy lại.
// - Các action mission vẫn giữ nguyên ý nghĩa cũ.
// ============================================================================

using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class MissionElementTrigger : MonoBehaviour
{
    public enum TriggerAction
    {
        CompleteCurrentElement,
        ActivateElementByIndex,
        SaveCheckpointOnly,
        StartMission,
        PlayConversationOnly
    }

    [Header("Manager")]
    [SerializeField] private MissionFlowManager missionFlowManager;
    [SerializeField] private CheckpointManager checkpointManager;
    [SerializeField] private MissionCheckpointToast checkpointToast;
    [SerializeField] private DialogueController dialogueController;

    [Header("Trigger")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool oneTime = true;
    [SerializeField] private bool disableAfterTriggered = true;
    [SerializeField] private TriggerAction action =
        TriggerAction.CompleteCurrentElement;

    [Header("Persistent Trigger Identity")]
    [Tooltip(
        "ID ổn định để lưu trạng thái trigger. " +
        "Để trống sẽ dùng hierarchy path runtime."
    )]
    [SerializeField] private string persistentTriggerId;

    [Tooltip(
        "Chỉ lưu trạng thái trigger nếu One Time + Disable After Triggered."
    )]
    [SerializeField] private bool persistTriggeredState = true;

    [Header("Optional Conversation Before Action")]
    [Tooltip(
        "Nếu có, trigger sẽ phát hội thoại trước. " +
        "Hội thoại kết thúc mới thực hiện action bên dưới."
    )]
    [SerializeField] private DialogueConversationData
        conversationBeforeAction;

    [Header("Activate Element By Index")]
    [SerializeField] private int targetElementIndex = 0;

    [Header("Checkpoint")]
    [Tooltip("Map mà checkpoint này thuộc về.")]
    [Min(1)]
    [SerializeField] private int mapNumber = 1;

    [Tooltip(
        "Số thứ tự checkpoint. CheckpointManager dùng số này để " +
        "xác định checkpoint đã commit."
    )]
    [Min(1)]
    [SerializeField] private int checkpointNumber = 1;

    [Tooltip(
        "Transform thực tế mà Player sẽ dùng làm điểm restore. " +
        "Nếu bỏ trống, dùng chính Transform của trigger."
    )]
    [SerializeField] private Transform checkpointPoint;

    [SerializeField] private string checkpointMessage =
        "Checkpoint";

    // Chức năng mới:
    // Danh sách Object sẽ được SetActive(true) khi CheckpointManager
    // thực sự restore về checkpoint này.
    // Các Object này chỉ là runtime activation data, KHÔNG lưu vào JSON.
    [Header("Checkpoint Restore Object State")]
    [Tooltip(
        "Khi Player restore về checkpoint này: các Object trong list sẽ được bật."
    )]
    [SerializeField] private GameObject[] checkpointRestoreActiveObjects;

    // Chức năng mới:
    // Danh sách Object sẽ được SetActive(false) khi CheckpointManager
    // thực sự restore về checkpoint này.
    [Tooltip(
        "Khi Player restore về checkpoint này: các Object trong list sẽ được tắt."
    )]
    [SerializeField] private GameObject[] checkpointRestoreInactiveObjects;

    public GameObject[] CheckpointRestoreActiveObjects =>
        checkpointRestoreActiveObjects;

    public GameObject[] CheckpointRestoreInactiveObjects =>
        checkpointRestoreInactiveObjects;

    private bool hasTriggered;
    private bool isRunning;
    private Collider triggerCollider;

    public TriggerAction Action => action;
    public int MapNumber => mapNumber;
    public int CheckpointNumber => checkpointNumber;
    public Transform CheckpointPoint => checkpointPoint;
    public int TargetElementIndex => targetElementIndex;

    public bool HasTriggered => hasTriggered;

    public bool ShouldPersistTriggeredState
    {
        get
        {
            return persistTriggeredState &&
                   oneTime &&
                   disableAfterTriggered;
        }
    }

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
    }

    private void OnTriggerEnter(Collider other)
    {
        if (oneTime && hasTriggered)
            return;

        if (isRunning)
            return;

        if (!IsPlayer(other))
            return;

        hasTriggered = true;
        StartCoroutine(TriggerRoutine());

        Debug.Log(
            "[MissionElementTrigger] Trigger Enter: " +
            gameObject.name,
            this
        );
    }

    private IEnumerator TriggerRoutine()
    {
        isRunning = true;

        if (missionFlowManager == null)
            missionFlowManager =
                FindAnyObjectByType<MissionFlowManager>();

        if (checkpointManager == null)
            checkpointManager =
                FindAnyObjectByType<CheckpointManager>();

        if (checkpointToast == null)
        {
            // Chức năng mới:
            // Checkpoint Toast có thể có visual root inactive, nên phải tìm component kể cả object inactive.
            MissionCheckpointToast[] toasts = FindObjectsByType<MissionCheckpointToast>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

            if (toasts != null && toasts.Length > 0)
                checkpointToast = toasts[0];
        }

        if (dialogueController == null)
        {
            dialogueController =
                DialogueController.Instance != null
                    ? DialogueController.Instance
                    : FindAnyObjectByType<DialogueController>();
        }

        if (conversationBeforeAction != null)
        {
            if (dialogueController != null)
            {
                yield return dialogueController
                    .PlayConversationRoutine(
                        conversationBeforeAction
                    );
            }
            else
            {
                Debug.LogWarning(
                    "[MissionElementTrigger] Missing DialogueController, " +
                    "cannot play conversation: " +
                    conversationBeforeAction.ConversationId,
                    this
                );
            }
        }

        if (action == TriggerAction.PlayConversationOnly)
        {
            FinishTrigger();
            yield break;
        }

        if (action == TriggerAction.SaveCheckpointOnly &&
            checkpointManager == null)
        {
            Debug.LogWarning(
                "[MissionElementTrigger] Missing CheckpointManager. " +
                "Checkpoint was not registered.",
                this
            );

            isRunning = false;
            yield break;
        }

        if (action != TriggerAction.SaveCheckpointOnly &&
            missionFlowManager == null)
        {
            Debug.LogWarning(
                "[MissionElementTrigger] Missing MissionFlowManager.",
                this
            );

            isRunning = false;
            yield break;
        }

        switch (action)
        {
            case TriggerAction.CompleteCurrentElement:
                missionFlowManager.CompleteCurrentElement();
                break;

            case TriggerAction.ActivateElementByIndex:
                Debug.Log(
                    "[MissionElementTrigger] Activate Element = " +
                    targetElementIndex,
                    this
                );

                missionFlowManager
                    .ActivateElementByIndex(
                        targetElementIndex
                    );
                break;

            case TriggerAction.SaveCheckpointOnly:
                if (!HandleCheckpoint())
                    yield break;
                break;

            case TriggerAction.StartMission:
                missionFlowManager.StartMission();
                break;

            case TriggerAction.PlayConversationOnly:
                break;
        }

        FinishTrigger();
    }

    // Chức năng mới:
    // Trả bool để checkpoint bị reject không bị FinishTrigger()/disable nhầm.
    private bool HandleCheckpoint()
    {
        Transform cp = checkpointPoint != null
            ? checkpointPoint
            : transform;

        string message =
            string.IsNullOrWhiteSpace(checkpointMessage)
                ? "Checkpoint"
                : checkpointMessage;

        // Điều chỉnh:
        // Checkpoint Trigger truyền trực tiếp hai list activation vào
        // CheckpointManager. Vì vậy Manager biết chính xác setup của checkpoint
        // này mà không cần đoán object nào thuộc checkpoint.
        bool accepted =
            checkpointManager.ReachCheckpoint(
                mapNumber,
                checkpointNumber,
                cp,
                message,
                checkpointRestoreActiveObjects,
                checkpointRestoreInactiveObjects
            );

        if (!accepted)
        {
            Debug.Log(
                "[MissionElementTrigger] Checkpoint was rejected by " +
                "CheckpointManager. " +
                "Map = " + mapNumber +
                ", Checkpoint = " + checkpointNumber,
                this
            );

            hasTriggered = false;
            isRunning = false;
            return false;
        }

        Debug.Log(
            "[MissionElementTrigger] Checkpoint Reached\n" +
            "Trigger Object = " + gameObject.name +
            "\nMap = " + mapNumber +
            "\nCheckpoint = " + checkpointNumber +
            "\nCheckpoint Point = " + cp.name,
            cp
        );

        // CŨ:
        // if (checkpointToast != null)
        // {
        //     checkpointToast.ShowCheckpoint(message);
        // }
        // else
        // {
        //     Debug.LogWarning(
        //         "[MissionElementTrigger] Missing MissionCheckpointToast.",
        //         this
        //     );
        // }
        //
        // MỚI:
        // CheckpointManager phát Toast sau khi commit thành công.

        return true;
    }

    /// <summary>
    /// Compatibility cho checkpoint gate hiện tại.
    /// Không sửa save.json.
    /// </summary>
    public void ApplyPersistentCheckpointGate(
        int savedMap,
        int savedCheckpoint,
        bool disableAtOrBeforeSavedCheckpoint)
    {
        if (action != TriggerAction.SaveCheckpointOnly)
            return;

        if (savedMap < 1 || savedCheckpoint < 1)
            return;

        bool shouldBeConsumed =
            IsAtOrBefore(
                mapNumber,
                checkpointNumber,
                savedMap,
                savedCheckpoint
            );

        if (!disableAtOrBeforeSavedCheckpoint)
        {
            shouldBeConsumed =
                mapNumber == savedMap &&
                checkpointNumber == savedCheckpoint;
        }

        if (!shouldBeConsumed)
            return;

        hasTriggered = true;
        isRunning = false;

        if (triggerCollider != null)
            triggerCollider.enabled = false;

        if (disableAfterTriggered &&
            gameObject.activeSelf)
        {
            gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Apply persistent trigger state từ Mission snapshot.
    /// </summary>
    public void ApplyPersistentTriggerState(
        bool triggered)
    {
        hasTriggered = triggered;
        isRunning = false;

        if (!triggered)
        {
            if (triggerCollider == null)
                triggerCollider = GetComponent<Collider>();

            if (triggerCollider != null)
                triggerCollider.enabled = true;

            return;
        }

        if (triggerCollider == null)
            triggerCollider = GetComponent<Collider>();

        if (triggerCollider != null)
            triggerCollider.enabled = false;

        if (disableAfterTriggered &&
            gameObject.activeSelf)
        {
            gameObject.SetActive(false);
        }
    }

    public void ResetCheckpointRuntimeGate()
    {
        hasTriggered = false;
        isRunning = false;

        if (triggerCollider == null)
            triggerCollider = GetComponent<Collider>();

        if (triggerCollider != null)
            triggerCollider.enabled = true;

        if (!gameObject.activeSelf)
            gameObject.SetActive(true);
    }

    /// <summary>
    /// Returns stable trigger identity used in save.json.
    /// </summary>
    public string GetPersistentTriggerId()
    {
        if (!string.IsNullOrWhiteSpace(
                persistentTriggerId))
        {
            return persistentTriggerId.Trim();
        }

        return GetHierarchyPath(transform);
    }

    private static bool IsAtOrBefore(
        int mapA,
        int checkpointA,
        int mapB,
        int checkpointB)
    {
        if (mapA < mapB)
            return true;

        if (mapA > mapB)
            return false;

        return checkpointA <= checkpointB;
    }

    private void FinishTrigger()
    {
        if (disableAfterTriggered)
        {
            gameObject.SetActive(false);
        }
        else if (triggerCollider != null &&
                 oneTime)
        {
            triggerCollider.enabled = false;
        }

        isRunning = false;
    }

    private bool IsPlayer(Collider other)
    {
        if (other.CompareTag(playerTag))
            return true;

        Transform root = other.transform.root;

        return root != null &&
               root.CompareTag(playerTag);
    }

    private static string GetHierarchyPath(
        Transform target)
    {
        if (target == null)
            return string.Empty;

        string path = target.name;

        while (target.parent != null)
        {
            target = target.parent;
            path = target.name + "/" + path;
        }

        return path;
    }
}
