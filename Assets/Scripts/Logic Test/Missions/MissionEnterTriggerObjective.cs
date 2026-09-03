// ============================================================================
// MissionEnterTriggerObjective.cs
// ============================================================================
// Objective hoàn thành khi Player bước vào trigger.
//
// PERSISTENCE INTEGRATION:
// - Nếu objective đã Completed trong save, trigger sẽ không kích hoạt lại.
// - Restore chỉ áp dụng state, không gọi CompleteObjective().
// ============================================================================

using UnityEngine;

[RequireComponent(typeof(Collider))]
public class MissionEnterTriggerObjective : MissionObjective
{
    [Header("Trigger")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool disableGameObjectAfterComplete = true;

    private Collider triggerCollider;

    private void Awake()
    {
        triggerCollider = GetComponent<Collider>();

        if (triggerCollider != null)
            triggerCollider.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other))
            return;

        MissionFlowManager flowManager =
            FindAnyObjectByType<MissionFlowManager>();

        if (flowManager != null &&
            flowManager.IsPersistentRestoreInProgress)
        {
            return;
        }

        CompleteObjective();

        if (disableGameObjectAfterComplete)
            gameObject.SetActive(false);
    }

    protected override void OnPersistentStateApplied(
        bool completed)
    {
        if (!completed)
            return;

        if (disableGameObjectAfterComplete)
        {
            if (triggerCollider != null)
                triggerCollider.enabled = false;

            if (gameObject.activeSelf)
                gameObject.SetActive(false);
        }
    }

    private bool IsPlayer(Collider other)
    {
        if (other.CompareTag(playerTag))
            return true;

        if (other.transform.root != null &&
            other.transform.root.CompareTag(playerTag))
        {
            return true;
        }

        return false;
    }
}
