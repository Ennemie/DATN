// Chức năng: Trigger đơn giản để báo MissionFlowManager chuyển flow.
// Có thể dùng để Complete Current Element, Activate Element By Index, hoặc Save Checkpoint Only khi Player bước vào trigger.
// BẢN NÂNG CẤP:
// - Có thể phát conversation trước khi thực hiện action.
// - Phù hợp case: Player tới vị trí chỉ định -> hội thoại -> activate objective/element.
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
    [SerializeField] private DialogueController dialogueController;

    [Header("Trigger")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool oneTime = true;
    [SerializeField] private bool disableAfterTriggered = true;
    [SerializeField] private TriggerAction action = TriggerAction.CompleteCurrentElement;

    [Header("Optional Conversation Before Action")]
    [Tooltip("Nếu có, trigger sẽ phát hội thoại trước. Hội thoại kết thúc mới thực hiện action bên dưới.")]
    [SerializeField] private DialogueConversationData conversationBeforeAction;

    [Header("Activate Element By Index")]
    [SerializeField] private int targetElementIndex = 0;

    [Header("Save Checkpoint Only")]
    [SerializeField] private Transform checkpointPoint;
    [SerializeField] private string checkpointId;
    [SerializeField] private string checkpointMessage = "Checkpoint";

    private bool hasTriggered;
    private bool isRunning;
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

        if (isRunning)
            return;

        if (!IsPlayer(other))
            return;

        hasTriggered = true;
        StartCoroutine(TriggerRoutine());
    }

    private IEnumerator TriggerRoutine()
    {
        isRunning = true;

        if (missionFlowManager == null)
            missionFlowManager = FindFirstObjectByType<MissionFlowManager>();

        if (dialogueController == null)
            dialogueController = DialogueController.Instance != null ? DialogueController.Instance : FindFirstObjectByType<DialogueController>();

        if (conversationBeforeAction != null)
        {
            if (dialogueController != null)
                yield return dialogueController.PlayConversationRoutine(conversationBeforeAction);
            else
                Debug.LogWarning("[MissionElementTrigger] Missing DialogueController, cannot play conversation: " + conversationBeforeAction.ConversationId, this);
        }

        if (missionFlowManager == null && action != TriggerAction.PlayConversationOnly)
        {
            Debug.LogWarning("[MissionElementTrigger] Missing MissionFlowManager.", this);
            isRunning = false;
            yield break;
        }

        switch (action)
        {
            case TriggerAction.CompleteCurrentElement:
                missionFlowManager.CompleteCurrentElement();
                break;

            case TriggerAction.ActivateElementByIndex:
                missionFlowManager.ActivateElementByIndex(targetElementIndex);
                break;

            case TriggerAction.SaveCheckpointOnly:
                missionFlowManager.SaveCheckpoint(checkpointPoint != null ? checkpointPoint : transform, checkpointId, checkpointMessage);
                break;

            case TriggerAction.StartMission:
                missionFlowManager.StartMission();
                break;

            case TriggerAction.PlayConversationOnly:
                break;
        }

        if (disableAfterTriggered)
            gameObject.SetActive(false);
        else if (triggerCollider != null && oneTime)
            triggerCollider.enabled = false;

        isRunning = false;
    }

    private bool IsPlayer(Collider other)
    {
        if (other.CompareTag(playerTag))
            return true;

        Transform root = other.transform.root;
        return root != null && root.CompareTag(playerTag);
    }
}
