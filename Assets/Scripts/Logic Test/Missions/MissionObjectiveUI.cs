// Chức năng: Hiển thị popup New Objective bằng object Objectives chung; sau khi popup ẩn sẽ đăng ký nhiệm vụ vào MissionObjectiveListUI.
// Gán cho: GameManager hoặc object Managers luôn active.
// Tham chiếu với: MissionFlowManager gọi QueueObjective(); MissionObjectiveListUI nhận AddObjective() sau khi popup biến mất.
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class MissionObjectiveUI : MonoBehaviour
{
    [System.Serializable]
    private class ObjectiveToastRequest
    {
        public string title;
        public string objectiveId;
        public string content;
        public bool addToActiveList;
    }

    [Header("Popup References")]
    [SerializeField] private GameObject objectiveRoot;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text contentText;

    [Header("Active Objective List")]
    [SerializeField] private MissionObjectiveListUI activeObjectiveListUI;

    [Header("Settings")]
    [SerializeField] private string defaultTitle = "New Objective";
    [SerializeField] private float showDuration = 3f;
    [SerializeField] private bool hideRootOnAwake = true;
    [SerializeField] private bool logDebug;

    private readonly Queue<ObjectiveToastRequest> queue = new Queue<ObjectiveToastRequest>();
    private Coroutine queueRoutine;
    private bool isShowing;

    public bool IsBusy => isShowing || queue.Count > 0;

    private void Awake()
    {
        if (activeObjectiveListUI == null)
            activeObjectiveListUI = MissionObjectiveListUI.Instance;

        if (hideRootOnAwake && objectiveRoot != null)
            objectiveRoot.SetActive(false);
    }

    public void QueueObjective(string objectiveText)
    {
        QueueObjective(defaultTitle, objectiveText, objectiveText, true);
    }

    public void QueueObjectiveWithId(string objectiveId, string objectiveText)
    {
        QueueObjective(defaultTitle, objectiveId, objectiveText, true);
    }

    public void QueueObjective(string title, string objectiveId, string objectiveText)
    {
        QueueObjective(title, objectiveId, objectiveText, true);
    }

    public void QueueObjective(string title, string objectiveId, string objectiveText, bool addToActiveList)
    {
        MissionFlowManager flowManager =
            FindAnyObjectByType<MissionFlowManager>();

        if (flowManager != null &&
            flowManager.IsPersistentRestoreInProgress)
        {
            if (logDebug)
            {
                Debug.Log(
                    "[MissionObjectiveUI] Ignored objective popup while persistent restore is in progress.",
                    this
                );
            }

            return;
        }

        if (flowManager != null &&
            !string.IsNullOrWhiteSpace(objectiveId))
        {
            GameDataManager dataManager =
                GameDataManager.Instance;

            if (dataManager != null &&
                dataManager.HasValidSaveData &&
                dataManager.Data.GetObjectiveState(
                    objectiveId.Trim()
                ) == MissionProgressState.Completed)
            {
                if (logDebug)
                {
                    Debug.Log(
                        "[MissionObjectiveUI] Ignored popup for an already committed Completed objective: " +
                        objectiveId,
                        this
                    );
                }

                return;
            }
        }

        if (string.IsNullOrWhiteSpace(objectiveText))
        {
            Debug.LogWarning("[MissionObjectiveUI] QueueObjective ignored because objectiveText is empty.");
            return;
        }

        ObjectiveToastRequest request = new ObjectiveToastRequest
        {
            title = string.IsNullOrWhiteSpace(title) ? defaultTitle : title,
            objectiveId = string.IsNullOrWhiteSpace(objectiveId) ? objectiveText : objectiveId,
            content = objectiveText,
            addToActiveList = addToActiveList
        };

        queue.Enqueue(request);

        if (queueRoutine == null)
            queueRoutine = StartCoroutine(ProcessQueueRoutine());
    }

    /// <summary>
    /// Clear popup requests that may have been queued by normal runtime flow
    /// before Continue/checkpoint restore finished. Restore must rebuild the
    /// Canvas directly from persistent data instead of replaying old popups.
    /// </summary>
    public void ClearPendingObjectivesForRestore()
    {
        if (queueRoutine != null)
        {
            StopCoroutine(queueRoutine);
            queueRoutine = null;
        }

        queue.Clear();
        isShowing = false;

        if (objectiveRoot != null)
            objectiveRoot.SetActive(false);
    }

    public IEnumerator WaitUntilIdle()
    {
        while (IsBusy)
            yield return null;
    }

    private IEnumerator ProcessQueueRoutine()
    {
        while (queue.Count > 0)
        {
            ObjectiveToastRequest request = queue.Dequeue();
            isShowing = true;

            if (objectiveRoot != null)
                objectiveRoot.SetActive(true);

            if (titleText != null)
                titleText.text = request.title;

            if (contentText != null)
                contentText.text = request.content;

            if (logDebug)
                Debug.Log("[MissionObjectiveUI] Showing objective popup: " + request.content);

            yield return new WaitForSeconds(showDuration);

            if (objectiveRoot != null)
                objectiveRoot.SetActive(false);

            isShowing = false;

            if (request.addToActiveList)
            {
                MissionObjectiveListUI listUI = activeObjectiveListUI != null ? activeObjectiveListUI : MissionObjectiveListUI.Instance;
                if (listUI != null)
                {
                    listUI.AddObjective(request.objectiveId, request.content);
                }
                else
                {
                    Debug.LogWarning("[MissionObjectiveUI] Cannot add active objective because MissionObjectiveListUI is missing.");
                }
            }

            yield return null;
        }

        queueRoutine = null;
    }
}
