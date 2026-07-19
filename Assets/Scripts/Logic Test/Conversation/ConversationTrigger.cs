// Chức năng: Trigger chuyên phát hội thoại, có thể tùy chọn gọi MissionFlowManager sau khi hội thoại kết thúc.
// Dùng cho case: bước vào nhà -> hiện hộp thoại; hoặc tới điểm A -> hội thoại -> start/activate element.
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ConversationTrigger : MonoBehaviour
{
    public enum AfterConversationAction
    {
        None,
        StartMission,
        ActivateElementByIndex,
        CompleteCurrentElement,
        SaveCheckpointOnly
    }

    [Header("Conversation")]
    [SerializeField] private DialogueConversationData conversation;
    [SerializeField] private DialogueController dialogueController;

    [Header("Optional Mission Action After Conversation")]
    [SerializeField] private MissionFlowManager missionFlowManager;
    [SerializeField] private AfterConversationAction afterConversationAction = AfterConversationAction.None;
    [SerializeField] private int targetElementIndex;

    [Header("Optional Checkpoint")]
    [SerializeField] private Transform checkpointPoint;
    [SerializeField] private string checkpointId;
    [SerializeField] private string checkpointMessage = "Checkpoint";

    [Header("Trigger")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool oneTime = true;
    [SerializeField] private bool disableAfterTriggered = true;

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

        if (dialogueController == null)
            dialogueController = DialogueController.Instance != null ? DialogueController.Instance : FindFirstObjectByType<DialogueController>();

        if (conversation != null)
        {
            if (dialogueController != null)
                yield return dialogueController.PlayConversationRoutine(conversation);
            else
                Debug.LogWarning("[ConversationTrigger] Missing DialogueController, cannot play conversation: " + conversation.ConversationId, this);
        }

        RunAfterConversationAction();

        if (disableAfterTriggered)
            gameObject.SetActive(false);
        else if (triggerCollider != null && oneTime)
            triggerCollider.enabled = false;

        isRunning = false;
    }

    private void RunAfterConversationAction()
    {
        if (afterConversationAction == AfterConversationAction.None)
            return;

        if (missionFlowManager == null)
            missionFlowManager = FindFirstObjectByType<MissionFlowManager>();

        if (missionFlowManager == null)
        {
            Debug.LogWarning("[ConversationTrigger] Missing MissionFlowManager.", this);
            return;
        }

        switch (afterConversationAction)
        {
            case AfterConversationAction.StartMission:
                missionFlowManager.StartMission();
                break;

            case AfterConversationAction.ActivateElementByIndex:
                missionFlowManager.ActivateElementByIndex(targetElementIndex);
                break;

            case AfterConversationAction.CompleteCurrentElement:
                missionFlowManager.CompleteCurrentElement();
                break;

            case AfterConversationAction.SaveCheckpointOnly:
                missionFlowManager.SaveCheckpoint(checkpointPoint != null ? checkpointPoint : transform, checkpointId, checkpointMessage);
                break;
        }
    }

    private bool IsPlayer(Collider other)
    {
        if (other.CompareTag(playerTag))
            return true;

        Transform root = other.transform.root;
        return root != null && root.CompareTag(playerTag);
    }
}
