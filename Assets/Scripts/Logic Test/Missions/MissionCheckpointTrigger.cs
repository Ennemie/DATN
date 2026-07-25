// MissionCheckpointTrigger
// Thin checkpoint trigger that only talks to MissionFailManager.
// You can keep this script if you want a dedicated checkpoint object,
// or delete it and use MissionElementTrigger -> SaveCheckpointOnly instead.

using UnityEngine;

[RequireComponent(typeof(Collider))]
public class MissionCheckpointTrigger : MonoBehaviour
{
    [Header("Manager")]
    [SerializeField] private MissionFailManager missionFailManager;

    [Header("Checkpoint")]
    [SerializeField] private Transform checkpointPoint;
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

        if (missionFailManager == null)
            missionFailManager = FindFirstObjectByType<MissionFailManager>();

        if (missionFailManager == null)
        {
            Debug.LogWarning("[MissionCheckpointTrigger] Missing MissionFailManager.", this);
            return;
        }
        Transform cp = checkpointPoint != null ? checkpointPoint : transform;

        Debug.Log(
            "[MissionCheckpointTrigger] Save Checkpoint\n" +
            "Trigger Object = " + gameObject.name +
            "\nCheckpoint Point = " + cp.name,
            cp
        );

        missionFailManager.SetCheckpoint(cp, checkpointMessage);
        missionFailManager.SetCheckpoint(checkpointPoint != null ? checkpointPoint : transform, checkpointMessage);

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
