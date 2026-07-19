// Chức năng: Checkpoint trigger độc lập; khi Player chạm vào thì lưu checkpoint gần nhất và hiện TMP CheckPoint một lần.
// Dùng khi checkpoint không nằm trực tiếp trong MissionElement hoặc muốn checkpoint đặt tự do trong map.
// Tham chiếu với: MissionFlowManager.SaveCheckpoint(); MissionFailManager sẽ dùng checkpoint này để respawn khi camera phát hiện hoặc player chết.
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class MissionCheckpointTrigger : MonoBehaviour
{
    [Header("Manager")]
    [SerializeField] private MissionFlowManager missionFlowManager;

    [Header("Checkpoint")]
    [SerializeField] private Transform checkpointPoint;
    [SerializeField] private string checkpointId;
    [SerializeField] private string checkpointMessage = "Checkpoint";

    [Header("Trigger")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool oneTime = true;
    [SerializeField] private bool disableAfterTriggered = false;

    private bool hasTriggered;
    private Collider triggerCollider;

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

        if (!IsPlayer(other))
            return;

        hasTriggered = true;

        if (missionFlowManager == null)
            missionFlowManager = FindFirstObjectByType<MissionFlowManager>();

        if (missionFlowManager == null)
        {
            Debug.LogWarning("[MissionCheckpointTrigger] Missing MissionFlowManager.", this);
            return;
        }

        missionFlowManager.SaveCheckpoint(checkpointPoint != null ? checkpointPoint : transform, checkpointId, checkpointMessage);

        if (disableAfterTriggered)
            gameObject.SetActive(false);
        else if (triggerCollider != null && oneTime)
            triggerCollider.enabled = false;
    }

    private bool IsPlayer(Collider other)
    {
        if (other.CompareTag(playerTag))
            return true;

        Transform root = other.transform.root;
        return root != null && root.CompareTag(playerTag);
    }
}
