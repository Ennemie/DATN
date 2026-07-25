// Chức năng: Objective hoàn thành khi Player bước vào vùng trigger.
// Gán cho: Object trigger trong map, ví dụ Trigger_EnterHouse_B hoặc Trigger_AfterCamera.
// Tham chiếu với: MissionObjective base class để set IsCompleted; MissionFlowManager kiểm tra objective này trong Required Objectives.
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
        triggerCollider.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other))
            return;

        CompleteObjective();

        if (disableGameObjectAfterComplete)
            gameObject.SetActive(false);
    }

    private bool IsPlayer(Collider other)
    {
        if (other.CompareTag(playerTag))
            return true;

        if (other.transform.root != null && other.transform.root.CompareTag(playerTag))
            return true;

        return false;
    }
}
