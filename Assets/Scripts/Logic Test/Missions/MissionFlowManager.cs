    // Chức năng: Quản lý flow Mission theo từng Element: bật/tắt object, checkpoint, hội thoại, objective UI,
    // camera intro, chờ objective hoàn thành, post action, rồi mới sang element tiếp theo.
    //
    // BẢN NÂNG CẤP THEO CONVERSATION FLOW:
    // - Auto Start mặc định = false: first element không có nghĩa là nhận objective ngay khi scene start.
    // - Element có thể chạy Conversation trước Objective; Conversation xong mới queue New Objective.
    // - Element có thể chạy Camera Shot; nếu shot có ConversationAfterArrive thì camera đợi hội thoại kết thúc rồi mới return.
    // - Objective complete không chuyển ngay lập tức; có thể chạy Post Conversation / Post Camera Shots trước khi sang element tiếp theo.
    // - Trigger có thể ActivateElementByIndex để bắt đầu mission sau khi Player tới vị trí chỉ định.
    // - Không ép Conversation tự complete mission; MissionFlowManager là nơi quyết định flow.
    using System.Collections;
    using System.Reflection;
    using UnityEngine;
    using System.Collections.Generic;
    public class MissionFlowManager : MonoBehaviour
    {
        [System.Serializable]
        public class ObjectiveAnnouncement
        {
            public string objectiveId;
            public string title = "New Objective";
            [TextArea] public string text;
            public bool addToActiveList = true;
        }

        [System.Serializable]
        public class CameraShot
        {
            public string shotName;
            public Transform targetPoint;
            public float moveTime = 3f;
            public float holdTime = 0.5f;
            public bool returnToPlayerAfterShot = true;

            [Header("Optional Dialogue After Arrive")]
            [Tooltip("Nếu có conversation này, camera sẽ tới target, chờ Delay Before Conversation, chạy hội thoại, hội thoại xong mới return.")]
            public DialogueConversationData conversationAfterArrive;
            public float delayBeforeConversation = 1f;
        }

        [System.Serializable]
        public class MissionElement
        {
            [Header("Element")]
            public string elementName;

            [Tooltip("ID objective mà element này đang chờ. Ví dụ: GO_TO_HOUSE, FIND_KEY. Có thể để trống nếu dùng Required Objectives bên dưới.")]
            public string requiredObjectiveId;

            [Header("Objects On Enter")]
            public GameObject triggerObjectToEnable;
            public GameObject[] setActiveTrueOnEnter;
            public GameObject[] setActiveFalseOnEnter;

            [Header("Pre Objective Flow")]
            [Tooltip("Chạy trước khi hiện New Objective. Dùng cho intro Commander, tutorial, hoặc NPC nói xong mới giao nhiệm vụ.")]
            public DialogueConversationData conversationBeforeObjective;

            [Tooltip("Camera shots chạy trước khi hiện New Objective. Nếu shot có conversationAfterArrive thì camera sẽ đợi conversation xong.")]
            public CameraShot[] cameraShotsBeforeObjective;

            [Header("Objectives UI")]
            [HideInInspector]
            public string[] newObjectiveTexts; // Legacy fallback. Prefer ObjectiveAnnouncements.

            [Tooltip(
                "Danh sách nhiệm vụ/objective chính của Element. " +
                "Mỗi announcement là một row logic của Active Objective List."
            )]
            public ObjectiveAnnouncement[] objectiveAnnouncements;
            public bool waitForObjectiveToastQueue;

            [Header("Camera Intro After Objective")]
            [Tooltip("Giữ tên field cameraShots để không phá dữ liệu cũ. Shot này chạy sau Objective UI, trước khi chờ Required Objectives.")]
            public CameraShot[] cameraShots;

            [Header("Required Objectives")]
            [Tooltip("Kéo MissionObjective cần hoàn thành vào đây. Manager sẽ tự kiểm tra completed/isCompleted bằng reflection nếu cần.")]
            public MissionObjective[] requiredObjectives;

            public bool resetRequiredObjectivesOnEnter = true;
            public bool autoCompleteIfNoRequiredObjectives;

            [Header("Post Complete Flow")]
            [Tooltip("Chạy sau khi element objective hoàn thành, trước khi sang element tiếp theo.")]
            public DialogueConversationData conversationAfterComplete;

            [Tooltip("Camera shots chạy sau khi element objective hoàn thành, trước khi sang element tiếp theo.")]
            public CameraShot[] cameraShotsAfterComplete;

            [Tooltip("Nếu true, sau post flow mới sang element tiếp theo. Nên để true.")]
            public bool proceedToNextElementAfterPostFlow = true;
        }

        [Header("Managers")]
        [SerializeField] private MissionObjectiveUI objectiveUI;
        [SerializeField] private MissionCheckpointToast checkpointToast;
        [SerializeField] private MissionFailManager failManager;
        [SerializeField] private MissionCameraDirector cameraDirector;
        [SerializeField] private DialogueController dialogueController;

        [Header("Flow")]
        [Tooltip("Mặc định false để scene load không tự nhận objective. Dùng MissionElementTrigger/ConversationTrigger/Debug Button để kích hoạt element.")]
        [SerializeField] private bool autoStart = false;
        [SerializeField] private int startElementIndex;
        [SerializeField] private MissionElement[] elements;

        [Header("Debug")]
        [SerializeField] private int currentElementIndex = -1;
        [SerializeField] private bool logDebug = true;

        private bool isTransitioning;
        private bool isCompletingElement;
        private bool completeRequestedDuringTransition;
        private Coroutine activeFlowRoutine;
        private Transform currentCheckpoint;
        private string currentCheckpointId;

        // =========================================================================
        // MISSION PERSISTENCE
        // =========================================================================
        [Header("Mission Persistence")]
        [Tooltip("Stable ID của MissionFlow này. Không đổi sau khi đã có save.")]
        [SerializeField] private string missionId = "MISSION_01";

        [SerializeField] private GameDataManager dataManager;
        [SerializeField] private CheckpointManager checkpointManager;

        // Runtime state.
        private bool currentElementCompleted;
        private bool missionRuntimeCompleted;

        // True while Continue / checkpoint restore is applying the committed snapshot.
        // Mission triggers/objectives must not start a new flow during this window.
        private bool persistentRestoreInProgress;

        // Active/Started objectives được lưu theo thứ tự hiển thị trên Canvas.
        private readonly List<string> startedObjectiveOrder =
            new List<string>();

        // Completed objectives của Mission runtime.
        private readonly HashSet<string> completedObjectiveIds =
            new HashSet<string>();

        // Objective IDs mà MissionFlowManager thực sự biết tới.
        private readonly HashSet<string> knownObjectiveIds =
            new HashSet<string>();

        private readonly Dictionary<string, string> objectiveDisplayTexts =
            new Dictionary<string, string>();

        private readonly Dictionary<string, bool> objectiveShowInActiveList =
            new Dictionary<string, bool>();

        private const BindingFlags InstanceFlags =
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic;

        public int CurrentElementIndex => currentElementIndex;
        public Transform CurrentCheckpoint => currentCheckpoint;
        public string CurrentCheckpointId => currentCheckpointId;
        public bool IsBusy => isTransitioning || isCompletingElement;

        public string MissionId => missionId;
        public bool IsMissionStarted =>
            currentElementIndex >= 0 && !missionRuntimeCompleted;

        public bool IsMissionCompleted => missionRuntimeCompleted;

        public bool IsPersistentRestoreInProgress =>
            persistentRestoreInProgress;

        private void Awake()
        {
            if (objectiveUI == null)
                objectiveUI = FindAnyObjectByType<MissionObjectiveUI>();

            if (checkpointToast == null)
                checkpointToast = FindAnyObjectByType<MissionCheckpointToast>();

            if (failManager == null)
                failManager = FindAnyObjectByType<MissionFailManager>();

            if (cameraDirector == null)
                cameraDirector = FindAnyObjectByType<MissionCameraDirector>();

            if (dialogueController == null)
                dialogueController = FindAnyObjectByType<DialogueController>();

            if (dataManager == null)
                dataManager = GameDataManager.Instance;

            if (dataManager == null)
                dataManager = FindAnyObjectByType<GameDataManager>();

            if (checkpointManager == null)
                checkpointManager = FindAnyObjectByType<CheckpointManager>();

            BuildKnownObjectiveIndex();

            // IMPORTANT: OnEnable runs before Awake in Unity.
            // Re-subscribe here after checkpointManager has been resolved so
            // Continue/Death checkpoint events are never missed.
            SubscribeCheckpointEvents();
        }

        private void OnEnable()
        {
            MissionObjective.AnyObjectiveCompleted +=
                HandleObjectiveCompleted;

            SubscribeCheckpointEvents();
        }

        private void OnDisable()
        {
            MissionObjective.AnyObjectiveCompleted -=
                HandleObjectiveCompleted;

            UnsubscribeCheckpointEvents();
        }

        private void SubscribeCheckpointEvents()
        {
            if (checkpointManager == null)
                return;

            checkpointManager.PersistentCheckpointApplied -=
                HandlePersistentCheckpointApplied;

            checkpointManager.PersistentCheckpointApplied +=
                HandlePersistentCheckpointApplied;

            checkpointManager.CheckpointRestoreRequested -=
                HandleCheckpointRestoreRequested;

            checkpointManager.CheckpointRestoreRequested +=
                HandleCheckpointRestoreRequested;
        }

        private void UnsubscribeCheckpointEvents()
        {
            if (checkpointManager == null)
                return;

            checkpointManager.PersistentCheckpointApplied -=
                HandlePersistentCheckpointApplied;

            checkpointManager.CheckpointRestoreRequested -=
                HandleCheckpointRestoreRequested;
        }

        private void Start()
        {
            if (autoStart)
                StartMission();
        }

        private void Update()
        {
            if (IsBusy)
                return;

            CheckCurrentElementCompletion();
        }

        public void StartMission()
        {
            if (persistentRestoreInProgress)
            {
                if (logDebug)
                    Debug.Log(
                        "[MissionFlowManager] StartMission ignored while persistent restore is in progress.",
                        this
                    );
                return;
            }

            ActivateElementByIndex(startElementIndex);
        }

        public void StartMissionAtElement(int index)
        {
            if (persistentRestoreInProgress)
            {
                if (logDebug)
                    Debug.Log(
                        "[MissionFlowManager] StartMissionAtElement ignored while persistent restore is in progress.",
                        this
                    );
                return;
            }

            ActivateElementByIndex(index);
        }

        public void ActivateElementByIndex(int index)
        {
            ActivateElementByIndex(index, false);
        }

        /// <summary>
        /// Activate an Element by index.
        ///
        /// allowReactivationOfCompletedElement is intended for deliberate
        /// non-linear mission branches. By default, stale one-time triggers
        /// cannot jump back into an already completed/passed Element after
        /// Continue.
        /// </summary>
        public void ActivateElementByIndex(
            int index,
            bool allowReactivationOfCompletedElement)
        {
            if (persistentRestoreInProgress)
            {
                if (logDebug)
                    Debug.Log(
                        "[MissionFlowManager] ActivateElementByIndex ignored while persistent restore is in progress. Index = " +
                        index,
                        this
                    );
                return;
            }

            if (!allowReactivationOfCompletedElement &&
                IsPersistentlyCompletedPastElement(index))
            {
                if (logDebug)
                    Debug.Log(
                        "[MissionFlowManager] Ignored stale activation of a completed/past Element. Index = " +
                        index,
                        this
                    );

                return;
            }

            Debug.Log("[MissionFlowManager] ActivateElementByIndex " + index);
        if (elements == null || elements.Length == 0)
            {
                Debug.LogWarning("[MissionFlowManager] No mission elements configured.", this);
                return;
            }

            if (index < 0 || index >= elements.Length)
            {
                Debug.LogWarning("[MissionFlowManager] Invalid element index: " + index, this);
                return;
            }

            if (activeFlowRoutine != null)
            {
                StopCoroutine(activeFlowRoutine);
                activeFlowRoutine = null;
            }

            isTransitioning = false;
            isCompletingElement = false;
            completeRequestedDuringTransition = false;

            // Normal gameplay transition:
            // - Previous Started objectives are no longer active once we jump to
            //   another Mission Element.
            // - Completed objective history is kept.
            ClearStartedObjectiveRuntime(true);

            currentElementCompleted = false;
            missionRuntimeCompleted = false;

            activeFlowRoutine = StartCoroutine(EnterElementRoutine(index));
        }

        public void CompleteCurrentElement()
        {
            if (persistentRestoreInProgress)
            {
                if (logDebug)
                    Debug.Log(
                        "[MissionFlowManager] CompleteCurrentElement ignored while persistent restore is in progress.",
                        this
                    );
                return;
            }

            if (currentElementIndex < 0)
                return;

            if (isTransitioning)
            {
                completeRequestedDuringTransition = true;

                if (logDebug)
                    Debug.Log("[MissionFlowManager] Complete requested during transition. Will complete after current transition.", this);

                return;
            }

            if (isCompletingElement)
                return;

            if (elements == null || currentElementIndex >= elements.Length)
                return;

            activeFlowRoutine = StartCoroutine(CompleteElementRoutine(currentElementIndex));
        }

        // Gọi hàm này từ trigger/objective đặc biệt nếu muốn báo complete theo ID thay vì kéo Required Objectives.
        public void CompleteObjectiveById(string objectiveId)
        {
            if (IsBlank(objectiveId))
            {
                Debug.LogWarning("[MissionFlowManager] CompleteObjectiveById ignored because objectiveId is empty.", this);
                return;
            }

            if (currentElementIndex < 0 || elements == null || currentElementIndex >= elements.Length)
            {
                Debug.LogWarning("[MissionFlowManager] Cannot complete objective. Invalid current element index.", this);
                return;
            }

            MissionElement currentElement = elements[currentElementIndex];

            if (!IsBlank(currentElement.requiredObjectiveId) && currentElement.requiredObjectiveId != objectiveId)
            {
                Debug.LogWarning(
                    "[MissionFlowManager] Ignored objective. Current element requires: " +
                    currentElement.requiredObjectiveId +
                    " but received: " +
                    objectiveId,
                    this
                );

                return;
            }

            if (logDebug)
                Debug.Log("[MissionFlowManager] Objective matched current element: " + objectiveId, this);

            NotifyObjectiveUICompleted(objectiveId);
            CompleteCurrentElement();
        }

        [System.Obsolete(
            "Checkpoint ownership moved to CheckpointManager. " +
            "Use MissionElementTrigger -> SaveCheckpointOnly instead.",
            false
        )]
        public void SaveCheckpoint(Transform checkpointPoint, string checkpointId, string message)
        {
            if (checkpointPoint == null)
            {
                Debug.LogWarning("[MissionFlowManager] SaveCheckpoint ignored because checkpointPoint is null.", this);
                return;
            }

            currentCheckpoint = checkpointPoint;
            currentCheckpointId = IsBlank(checkpointId) ? checkpointPoint.name : checkpointId;

            if (failManager != null)
                failManager.SetCheckpoint(checkpointPoint);

            if (checkpointToast != null)
                checkpointToast.ShowCheckpoint(message);

            if (logDebug)
                Debug.Log("[MissionFlowManager] Saved checkpoint: " + currentCheckpointId, this);
        }

        private bool IsPersistentlyCompletedPastElement(
            int index)
        {
            if (index < 0 ||
                elements == null ||
                index >= elements.Length)
            {
                return false;
            }

            ResolveDataManagerAndCheckpointManager();

            if (dataManager == null ||
                !dataManager.HasValidSaveData)
            {
                return false;
            }

            MissionProgressSaveData missionState =
                dataManager.Data.GetMissionState(missionId);

            if (missionState == null ||
                missionState.State != MissionProgressState.Started ||
                missionState.CurrentElementIndex < 0)
            {
                return false;
            }

            // Do not let stale one-time triggers restart the current saved
            // Element immediately after Continue.
            if (index == missionState.CurrentElementIndex)
                return true;

            if (index > missionState.CurrentElementIndex)
                return false;

            MissionElement element = elements[index];

            if (element == null)
                return false;

            List<string> objectiveIds =
                new List<string>();

            if (element.objectiveAnnouncements != null)
            {
                for (int i = 0;
                     i < element.objectiveAnnouncements.Length;
                     i++)
                {
                    ObjectiveAnnouncement announcement =
                        element.objectiveAnnouncements[i];

                    if (announcement == null)
                        continue;

                    string id =
                        IsBlank(announcement.objectiveId)
                            ? announcement.text
                            : announcement.objectiveId;

                    if (!IsBlank(id))
                        objectiveIds.Add(id.Trim());
                }
            }

            if (objectiveIds.Count == 0 &&
                element.newObjectiveTexts != null)
            {
                for (int i = 0;
                     i < element.newObjectiveTexts.Length;
                     i++)
                {
                    string id =
                        element.newObjectiveTexts[i];

                    if (!IsBlank(id))
                        objectiveIds.Add(id.Trim());
                }
            }

            // A past flow-only Element without objective announcements is
            // considered passed once the saved mission cursor is beyond it.
            if (objectiveIds.Count == 0)
                return true;

            for (int i = 0; i < objectiveIds.Count; i++)
            {
                if (dataManager.Data.GetObjectiveState(
                        objectiveIds[i]) !=
                    MissionProgressState.Completed)
                {
                    return false;
                }
            }

            return true;
        }

        private IEnumerator EnterElementRoutine(int index)
        {
            isTransitioning = true;
            isCompletingElement = false;
            completeRequestedDuringTransition = false;
            currentElementIndex = index;

            MissionElement element = elements[index];

            if (logDebug)
                Debug.Log("[MissionFlowManager] Enter element " + index + ": " + element.elementName, this);

            ApplySetActive(element.setActiveFalseOnEnter, false);
            ApplySetActive(element.setActiveTrueOnEnter, true);

            if (element.triggerObjectToEnable != null)
                element.triggerObjectToEnable.SetActive(true);

            if (element.resetRequiredObjectivesOnEnter &&
                element.requiredObjectives != null)
            {
                for (int i = 0; i < element.requiredObjectives.Length; i++)
                {
                    MissionObjective objective =
                        element.requiredObjectives[i];

                    if (objective == null)
                        continue;

                    string objectiveId = objective.ObjectiveId;

                    ResetObjectiveByReflection(objective);

                    if (!IsBlank(objectiveId))
                    {
                        completedObjectiveIds.Remove(objectiveId);
                        RemoveStartedObjectiveRuntime(objectiveId);
                    }
                }
            }

            // 1) Pre flow: conversation/camera trước khi nhận objective.
            if (element.conversationBeforeObjective != null)
                yield return PlayConversation(element.conversationBeforeObjective);

            yield return PlayCameraShots(element.cameraShotsBeforeObjective);

            // 2) New Objective chỉ hiện sau pre conversation/pre camera.
            QueueObjectiveAnnouncements(element);

            if (element.waitForObjectiveToastQueue && objectiveUI != null)
                yield return objectiveUI.WaitUntilIdle();

            // 3) Camera intro sau objective, giữ field cũ cameraShots để không phá setup cũ.
            yield return PlayCameraShots(element.cameraShots);

            isTransitioning = false;
            activeFlowRoutine = null;

            if (completeRequestedDuringTransition)
            {
                completeRequestedDuringTransition = false;
                CompleteCurrentElement();
                yield break;
            }

            if (ShouldAutoComplete(element))
                CompleteCurrentElement();
        }

        private IEnumerator CompleteElementRoutine(int elementIndex)
        {
            isCompletingElement = true;
            currentElementCompleted = true;

            MissionElement element = elements[elementIndex];

            if (logDebug)
            {
                Debug.Log(
                    "[MissionFlowManager] Completing element " +
                    elementIndex +
                    ": " +
                    element.elementName,
                    this
                );
            }

            // Post flow: chạy sau complete nhưng trước khi sang element tiếp theo.
            if (element.conversationAfterComplete != null)
                yield return PlayConversation(element.conversationAfterComplete);

            yield return PlayCameraShots(element.cameraShotsAfterComplete);

            isCompletingElement = false;
            activeFlowRoutine = null;

            if (!element.proceedToNextElementAfterPostFlow)
            {
                if (logDebug)
                {
                    Debug.Log(
                        "[MissionFlowManager] Post flow ended. " +
                        "Not proceeding because Proceed To Next Element After Post Flow is false.",
                        this
                    );
                }

                yield break;
            }

            int nextIndex = elementIndex + 1;
            if (nextIndex >= elements.Length)
            {
                missionRuntimeCompleted = true;
                currentElementIndex = -1;
                currentElementCompleted = false;
                ClearStartedObjectiveRuntime(true);

                if (logDebug)
                {
                    Debug.Log(
                        "[MissionFlowManager] Mission completed. No more elements: " +
                        missionId,
                        this
                    );
                }

                yield break;
            }

            ActivateElementByIndex(nextIndex);
        }

        private IEnumerator PlayConversation(DialogueConversationData conversation)
        {
            if (conversation == null)
                yield break;

            DialogueController controller = dialogueController != null ? dialogueController : DialogueController.Instance;
            if (controller == null)
            {
              //  Debug.LogWarning("[MissionFlowManager] Cannot play conversation because DialogueController is missing: " + conversation.ConversationId, conversation);
                yield break;
            }

            yield return controller.PlayConversationRoutine(conversation);
        }

        private IEnumerator PlayCameraShots(CameraShot[] shots)
        {
            if (shots == null || shots.Length == 0 || cameraDirector == null)
                yield break;

            DialogueController controller = dialogueController != null ? dialogueController : DialogueController.Instance;

            for (int i = 0; i < shots.Length; i++)
            {
                CameraShot shot = shots[i];
                if (shot == null || shot.targetPoint == null)
                    continue;

                if (shot.conversationAfterArrive != null)
                {
                    yield return cameraDirector.PlayCameraShotWithConversation(
                        shot.targetPoint,
                        shot.moveTime,
                        shot.holdTime,
                        shot.conversationAfterArrive,
                        controller,
                        shot.delayBeforeConversation,
                        shot.returnToPlayerAfterShot
                    );
                }
                else
                {
                    if (shot.returnToPlayerAfterShot)
                        yield return cameraDirector.PlayCameraShot(shot.targetPoint, shot.moveTime, shot.holdTime);
                    else
                    {
                        cameraDirector.BeginCameraControl();
                        yield return cameraDirector.MoveToTarget(shot.targetPoint, shot.moveTime);
                        if (shot.holdTime > 0f)
                            yield return new WaitForSeconds(shot.holdTime);
                        cameraDirector.EndCameraControl();
                    }
                }
            }
        }

        private void QueueObjectiveAnnouncements(MissionElement element)
        {
            if (persistentRestoreInProgress)
                return;

            if (objectiveUI == null || element == null)
                return;

            if (element.objectiveAnnouncements != null && element.objectiveAnnouncements.Length > 0)
            {
                for (int i = 0; i < element.objectiveAnnouncements.Length; i++)
                {
                    ObjectiveAnnouncement announcement = element.objectiveAnnouncements[i];
                    if (announcement == null || IsBlank(announcement.text))
                        continue;

                    string objectiveId =
                        IsBlank(announcement.objectiveId)
                            ? announcement.text
                            : announcement.objectiveId;

                    RegisterStartedObjective(
                        objectiveId,
                        announcement.text,
                        announcement.addToActiveList
                    );

                    objectiveUI.QueueObjective(
                        announcement.title,
                        objectiveId,
                        announcement.text,
                        announcement.addToActiveList
                    );
                }

                return;
            }

            if (element.newObjectiveTexts != null)
            {
                for (int i = 0; i < element.newObjectiveTexts.Length; i++)
                {
                    string objectiveText = element.newObjectiveTexts[i];
                    if (IsBlank(objectiveText))
                        continue;

                    RegisterStartedObjective(
                        objectiveText,
                        objectiveText,
                        true
                    );

                    objectiveUI.QueueObjectiveWithId(
                        objectiveText,
                        objectiveText
                    );
                }
            }
        }

        private bool ShouldAutoComplete(MissionElement element)
        {
            if (element == null || !element.autoCompleteIfNoRequiredObjectives)
                return false;

            return element.requiredObjectives == null || element.requiredObjectives.Length == 0;
        }

        private void CheckCurrentElementCompletion()
        {
            if (currentElementIndex < 0 || elements == null || currentElementIndex >= elements.Length)
                return;

            MissionElement element = elements[currentElementIndex];

            if (element.requiredObjectives == null || element.requiredObjectives.Length == 0)
                return;

            for (int i = 0; i < element.requiredObjectives.Length; i++)
            {
                MissionObjective objective = element.requiredObjectives[i];
                if (objective == null)
                    continue;

                if (!ReadObjectiveCompletedByReflection(objective))
                    return;
            }

            if (logDebug)
            {
                Debug.Log(
                    "[MissionFlowManager] All required objectives completed for element " +
                    currentElementIndex +
                    ": " +
                    element.elementName,
                    this
                );
            }

            CompleteCurrentElement();
        }


        // =========================================================================
        // MISSION PERSISTENCE
        // =========================================================================

        /// <summary>
        /// Chụp runtime mission state vào GameSaveData.
        /// KHÔNG tự ghi file. CheckpointManager gọi hàm này trước SaveToDisk().
        /// </summary>
        public void CapturePersistentState(GameSaveData targetData)
        {
            if (targetData == null)
            {
                Debug.LogWarning(
                    "[MissionFlowManager] CapturePersistentState received null data.",
                    this
                );
                return;
            }

            targetData.EnsureLists();

            MissionProgressSaveData missionState =
                new MissionProgressSaveData
                {
                    MissionId = missionId,
                    State = missionRuntimeCompleted
                        ? MissionProgressState.Completed
                        : currentElementIndex >= 0
                            ? MissionProgressState.Started
                            : MissionProgressState.NotStarted,
                    CurrentElementIndex = missionRuntimeCompleted
                        ? -1
                        : currentElementIndex,
                    // If there are still Started objectives, this element is
                    // still the active element from a persistence perspective.
                    // This also prevents saving a transient "completed" flag
                    // while the next objective is already active.
                    CurrentElementCompleted =
                        currentElementCompleted &&
                        startedObjectiveOrder.Count == 0
                };

            missionState.ObjectiveStates.Clear();

            // Completed objectives: scan actual scene state so objectives completed
            // through any caller are not lost.
            MissionObjective[] sceneObjectives =
                FindObjectsByType<MissionObjective>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None
                );

            for (int i = 0; i < sceneObjectives.Length; i++)
            {
                MissionObjective objective = sceneObjectives[i];

                if (objective == null)
                    continue;

                if (!IsKnownObjective(objective.ObjectiveId))
                    continue;

                if (!objective.IsCompleted)
                    continue;

                AddOrUpdateObjectiveSaveState(
                    missionState,
                    objective.ObjectiveId,
                    MissionProgressState.Completed,
                    GetObjectiveDisplayText(
                        objective.ObjectiveId
                    ),
                    GetObjectiveShowInActiveList(
                        objective.ObjectiveId
                    )
                );
            }

            // Started objectives are the objective rows that are still active.
            for (int i = 0; i < startedObjectiveOrder.Count; i++)
            {
                string objectiveId =
                    startedObjectiveOrder[i];

                if (IsBlank(objectiveId))
                    continue;

                if (completedObjectiveIds.Contains(objectiveId))
                    continue;

                string displayText =
                    GetObjectiveDisplayText(objectiveId);

                AddOrUpdateObjectiveSaveState(
                    missionState,
                    objectiveId,
                    MissionProgressState.Started,
                    displayText,
                    GetObjectiveShowInActiveList(objectiveId)
                );
            }

            targetData.UpsertMissionState(missionState);
            CapturePersistentTriggerStates(targetData);

            if (logDebug)
            {
                Debug.Log(
                    "[MissionFlowManager] Mission persistence captured. " +
                    "Mission = " + missionId +
                    ", State = " + missionState.State +
                    ", Element = " + missionState.CurrentElementIndex +
                    ", StartedObjectives = " +
                    CountObjectiveStates(
                        missionState,
                        MissionProgressState.Started
                    ) +
                    ", CompletedObjectives = " +
                    CountObjectiveStates(
                        missionState,
                        MissionProgressState.Completed
                    ),
                    this
                );
            }
        }

        /// <summary>
        /// Apply data đã commit vào runtime Mission.
        /// Không chạy New Objective popup / Conversation / Camera intro.
        /// </summary>
        public void ApplyPersistentMissionState(
            GameSaveData loadedData)
        {
            if (loadedData == null)
                return;

            loadedData.EnsureLists();

            // Clear any normal-runtime popup/list animation before rebuilding
            // from the committed snapshot. Continue must never replay old
            // Objective announcements.
            if (objectiveUI != null)
            {
                objectiveUI.ClearPendingObjectivesForRestore();
            }

            if (MissionObjectiveListUI.Instance != null)
            {
                MissionObjectiveListUI.Instance.ClearAllObjectives();
            }

            MissionProgressSaveData missionState =
                loadedData.GetMissionState(missionId);

            ClearStartedObjectiveRuntime(true);
            completedObjectiveIds.Clear();

            // 1) Resolve Mission runtime structure first.
            // This re-applies which objects should be enabled for the saved
            // current Mission Element.
            if (missionState != null &&
                missionState.State ==
                    MissionProgressState.Started &&
                missionState.CurrentElementIndex >= 0 &&
                elements != null &&
                missionState.CurrentElementIndex < elements.Length)
            {
                currentElementIndex =
                    missionState.CurrentElementIndex;

                currentElementCompleted =
                    missionState.CurrentElementCompleted;

                missionRuntimeCompleted = false;

                RestoreElementRuntime(
                    currentElementIndex
                );
            }
            else if (missionState != null &&
                     missionState.State ==
                         MissionProgressState.Completed)
            {
                missionRuntimeCompleted = true;
                currentElementIndex = -1;
                currentElementCompleted = false;
            }
            else
            {
                missionRuntimeCompleted = false;
                currentElementIndex = -1;
                currentElementCompleted = false;
            }

            // 2) Apply objective states AFTER element activation.
            // Completed objective trigger GameObjects can therefore disable
            // themselves without being immediately re-enabled by the element.
            MissionObjective[] sceneObjectives =
                FindObjectsByType<MissionObjective>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None
                );

            for (int i = 0; i < sceneObjectives.Length; i++)
            {
                MissionObjective objective =
                    sceneObjectives[i];

                if (objective == null)
                    continue;

                MissionProgressState state =
                    loadedData.GetObjectiveState(
                        objective.ObjectiveId
                    );

                objective.ApplyPersistentState(state);

                if (state ==
                    MissionProgressState.Completed)
                {
                    completedObjectiveIds.Add(
                        objective.ObjectiveId
                    );
                }
            }

            // 3) Apply one-time MissionElementTrigger states last.
            // This guarantees an already-consumed trigger does not get re-enabled
            // by RestoreElementRuntime().
            ApplyPersistentMissionTriggers(
                loadedData.PersistentTriggeredTriggerIds
            );

            if (missionState == null ||
                missionState.State ==
                    MissionProgressState.NotStarted)
            {
                if (MissionObjectiveListUI.Instance != null)
                {
                    MissionObjectiveListUI.Instance
                        .ClearAllObjectives();
                }

                return;
            }

            missionState.EnsureLists();

            objectiveShowInActiveList.Clear();

            for (int i = 0;
                 i < missionState.ObjectiveStates.Count;
                 i++)
            {
                MissionObjectiveSaveData objectiveState =
                    missionState.ObjectiveStates[i];

                if (objectiveState == null ||
                    IsBlank(objectiveState.ObjectiveId))
                {
                    continue;
                }

                objectiveShowInActiveList[
                    objectiveState.ObjectiveId
                ] = objectiveState.ShowInActiveList;
            }

            if (missionState.State ==
                MissionProgressState.Completed)
            {
                if (MissionObjectiveListUI.Instance != null)
                {
                    MissionObjectiveListUI.Instance
                        .RestoreFromPersistentState(
                            missionState.ObjectiveStates
                        );
                }

                return;
            }

            RestoreStartedObjectiveRuntime(
                missionState
            );

            if (MissionObjectiveListUI.Instance != null)
            {
                MissionObjectiveListUI.Instance
                    .RestoreFromPersistentState(
                        missionState.ObjectiveStates
                    );
            }

            if (logDebug)
            {
                Debug.Log(
                    "[MissionFlowManager] Mission persistent state applied. " +
                    "Mission = " + missionId +
                    ", Element = " + currentElementIndex +
                    ", ElementCompleted = " +
                    currentElementCompleted,
                    this
                );
            }
        }

        private void HandlePersistentCheckpointApplied(
            CheckpointManager.CheckpointState state)
        {
            ResolveDataManagerAndCheckpointManager();

            if (dataManager == null)
                return;

            persistentRestoreInProgress = true;

            try
            {
                ApplyPersistentMissionState(
                    dataManager.Data
                );
            }
            finally
            {
                persistentRestoreInProgress = false;
            }
        }

        private void HandleCheckpointRestoreRequested(
            CheckpointManager.CheckpointState state)
        {
            // Player death restore must roll Mission back to the last
            // committed save snapshot.
            ResolveDataManagerAndCheckpointManager();

            if (dataManager == null)
                return;

            persistentRestoreInProgress = true;

            try
            {
                ApplyPersistentMissionState(
                    dataManager.Data
                );
            }
            finally
            {
                persistentRestoreInProgress = false;
            }
        }

        private void ResolveDataManagerAndCheckpointManager()
        {
            if (dataManager == null)
                dataManager = GameDataManager.Instance;

            if (dataManager == null)
                dataManager = FindAnyObjectByType<GameDataManager>();

            if (checkpointManager == null)
            {
                checkpointManager =
                    FindAnyObjectByType<CheckpointManager>();

                SubscribeCheckpointEvents();
            }
        }

        private void BuildKnownObjectiveIndex()
        {
            knownObjectiveIds.Clear();
            objectiveDisplayTexts.Clear();

            if (elements == null)
                return;

            for (int i = 0; i < elements.Length; i++)
            {
                MissionElement element = elements[i];

                if (element == null)
                    continue;

                if (element.requiredObjectives != null)
                {
                    for (int j = 0;
                         j < element.requiredObjectives.Length;
                         j++)
                    {
                        MissionObjective objective =
                            element.requiredObjectives[j];

                        if (objective == null)
                            continue;

                        RegisterKnownObjective(
                            objective.ObjectiveId,
                            objective.ObjectiveId
                        );
                    }
                }

                if (element.objectiveAnnouncements != null)
                {
                    for (int j = 0;
                         j < element.objectiveAnnouncements.Length;
                         j++)
                    {
                        ObjectiveAnnouncement announcement =
                            element.objectiveAnnouncements[j];

                        if (announcement == null ||
                            IsBlank(announcement.text))
                        {
                            continue;
                        }

                        string objectiveId =
                            IsBlank(announcement.objectiveId)
                                ? announcement.text
                                : announcement.objectiveId;

                        RegisterKnownObjective(
                            objectiveId,
                            announcement.text
                        );
                    }
                }

                if (element.newObjectiveTexts != null)
                {
                    for (int j = 0;
                         j < element.newObjectiveTexts.Length;
                         j++)
                    {
                        string text =
                            element.newObjectiveTexts[j];

                        if (IsBlank(text))
                            continue;

                        RegisterKnownObjective(
                            text,
                            text
                        );
                    }
                }
            }
        }

        private void RegisterKnownObjective(
            string objectiveId,
            string displayText)
        {
            if (IsBlank(objectiveId))
                return;

            knownObjectiveIds.Add(objectiveId);

            if (!objectiveDisplayTexts.ContainsKey(
                    objectiveId))
            {
                objectiveDisplayTexts[objectiveId] =
                    string.IsNullOrWhiteSpace(displayText)
                        ? objectiveId
                        : displayText;
            }
        }

        private bool IsCommittedObjectiveCompleted(
            string objectiveId)
        {
            if (IsBlank(objectiveId))
                return false;

            ResolveDataManagerAndCheckpointManager();

            if (dataManager == null ||
                !dataManager.HasValidSaveData)
            {
                return false;
            }

            return dataManager.Data.GetObjectiveState(
                objectiveId
            ) == MissionProgressState.Completed;
        }

        private bool IsKnownObjective(string objectiveId)
        {
            return !IsBlank(objectiveId) &&
                   knownObjectiveIds.Contains(objectiveId);
        }

        private void RegisterStartedObjective(
            string objectiveId,
            string displayText,
            bool addToActiveList)
        {
            if (IsBlank(objectiveId))
                return;

            // A committed Completed objective must never be reintroduced as
            // Started by a stale trigger/flow after Continue or checkpoint restore.
            if (IsCommittedObjectiveCompleted(objectiveId))
            {
                if (logDebug)
                {
                    Debug.Log(
                        "[MissionFlowManager] Ignored reactivation of committed Completed objective: " +
                        objectiveId,
                        this
                    );
                }

                return;
            }

            RegisterKnownObjective(
                objectiveId,
                displayText
            );

            if (!startedObjectiveOrder.Contains(objectiveId))
            {
                startedObjectiveOrder.Add(objectiveId);
            }

            objectiveShowInActiveList[objectiveId] =
                addToActiveList;

            if (completedObjectiveIds.Contains(
                    objectiveId))
            {
                completedObjectiveIds.Remove(
                    objectiveId
                );
            }

            // addToActiveList=false vẫn là Started runtime state;
            // UI chỉ không hiển thị nó.
            _ = addToActiveList;
        }

        private void HandleObjectiveCompleted(
            MissionObjective objective)
        {
            if (objective == null)
                return;

            string objectiveId =
                objective.ObjectiveId;

            if (!IsKnownObjective(objectiveId))
                return;

            completedObjectiveIds.Add(objectiveId);
            RemoveStartedObjectiveRuntime(objectiveId);

            if (logDebug)
            {
                Debug.Log(
                    "[MissionFlowManager] Objective state -> Completed: " +
                    objectiveId,
                    this
                );
            }
        }

        private void ClearStartedObjectiveRuntime(
            bool clearUI)
        {
            startedObjectiveOrder.Clear();

            if (clearUI &&
                MissionObjectiveListUI.Instance != null)
            {
                // Do not destroy a completed objective that is already playing
                // its green + slide-out animation.
                MissionObjectiveListUI.Instance
                    .ClearNonCompletingObjectives();
            }
        }

        private void RemoveStartedObjectiveRuntime(
            string objectiveId)
        {
            if (IsBlank(objectiveId))
                return;

            startedObjectiveOrder.Remove(
                objectiveId
            );
        }

        private void RestoreStartedObjectiveRuntime(
            MissionProgressSaveData missionState)
        {
            startedObjectiveOrder.Clear();

            for (int i = 0;
                 i < missionState.ObjectiveStates.Count;
                 i++)
            {
                MissionObjectiveSaveData objectiveState =
                    missionState.ObjectiveStates[i];

                if (objectiveState == null)
                    continue;

                if (objectiveState.State !=
                    MissionProgressState.Started)
                {
                    continue;
                }

                if (IsBlank(objectiveState.ObjectiveId))
                    continue;

                startedObjectiveOrder.Add(
                    objectiveState.ObjectiveId
                );
            }
        }

        private void RestoreElementRuntime(int index)
        {
            if (elements == null ||
                index < 0 ||
                index >= elements.Length)
            {
                return;
            }

            MissionElement element =
                elements[index];

            if (element == null)
                return;

            ApplySetActive(
                element.setActiveFalseOnEnter,
                false
            );

            ApplySetActive(
                element.setActiveTrueOnEnter,
                true
            );

            if (element.triggerObjectToEnable != null)
            {
                element.triggerObjectToEnable
                    .SetActive(true);
            }

            // IMPORTANT:
            // Không gọi ResetObjectiveByReflection().
            // Không SaveCheckpoint().
            // Không gọi conversation/camera/objective popup.
            //
            // Continue/death restore chỉ đưa scene về runtime state
            // tương ứng với snapshot.
        }

        private void CapturePersistentTriggerStates(
            GameSaveData targetData)
        {
            if (targetData == null)
                return;

            targetData.EnsureLists();

            MissionElementTrigger[] triggers =
                FindObjectsByType<MissionElementTrigger>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None
                );

            HashSet<string> committedIds =
                new HashSet<string>(
                    targetData.PersistentTriggeredTriggerIds
                );

            for (int i = 0; i < triggers.Length; i++)
            {
                MissionElementTrigger trigger = triggers[i];

                if (trigger == null ||
                    !trigger.ShouldPersistTriggeredState ||
                    !trigger.HasTriggered)
                {
                    continue;
                }

                string id = trigger.GetPersistentTriggerId();

                if (!IsBlank(id))
                    committedIds.Add(id);
            }

            targetData.PersistentTriggeredTriggerIds =
                new List<string>(committedIds);
        }

        private void ApplyPersistentMissionTriggers(
            List<string> savedTriggerIds)
        {
            HashSet<string> consumedIds =
                new HashSet<string>();

            if (savedTriggerIds != null)
            {
                for (int i = 0;
                     i < savedTriggerIds.Count;
                     i++)
                {
                    string id = savedTriggerIds[i];

                    if (!IsBlank(id))
                        consumedIds.Add(id);
                }
            }

            MissionElementTrigger[] triggers =
                FindObjectsByType<MissionElementTrigger>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None
                );

            for (int i = 0; i < triggers.Length; i++)
            {
                MissionElementTrigger trigger =
                    triggers[i];

                if (trigger == null)
                    continue;

                string id =
                    trigger.GetPersistentTriggerId();

                trigger.ApplyPersistentTriggerState(
                    trigger.ShouldPersistTriggeredState &&
                    consumedIds.Contains(id)
                );
            }
        }

        private void AddOrUpdateObjectiveSaveState(
            MissionProgressSaveData missionState,
            string objectiveId,
            MissionProgressState state,
            string displayText,
            bool showInActiveList)
        {
            if (missionState == null ||
                IsBlank(objectiveId))
            {
                return;
            }

            MissionObjectiveSaveData existing =
                missionState.FindObjective(
                    objectiveId
                );

            if (existing != null)
            {
                // Completed luôn có priority cao hơn Started.
                if (existing.State !=
                        MissionProgressState.Completed ||
                    state ==
                        MissionProgressState.Completed)
                {
                    existing.State = state;
                }

                if (IsBlank(existing.DisplayText))
                {
                    existing.DisplayText =
                        string.IsNullOrWhiteSpace(displayText)
                            ? objectiveId
                            : displayText;
                }

                if (state ==
                    MissionProgressState.Started)
                {
                    existing.ShowInActiveList =
                        showInActiveList;
                }

                return;
            }

            missionState.ObjectiveStates.Add(
                new MissionObjectiveSaveData(
                    objectiveId,
                    state,
                    string.IsNullOrWhiteSpace(displayText)
                        ? objectiveId
                        : displayText,
                    showInActiveList
                )
            );
        }

        private string GetObjectiveDisplayText(
            string objectiveId)
        {
            if (IsBlank(objectiveId))
                return string.Empty;

            string text;

            if (objectiveDisplayTexts.TryGetValue(
                    objectiveId,
                    out text))
            {
                return text;
            }

            return objectiveId;
        }

        private bool GetObjectiveShowInActiveList(
            string objectiveId)
        {
            if (IsBlank(objectiveId))
                return true;

            bool value;

            if (objectiveShowInActiveList.TryGetValue(
                    objectiveId,
                    out value))
            {
                return value;
            }

            return true;
        }

        private int CountObjectiveStates(
            MissionProgressSaveData missionState,
            MissionProgressState state)
        {
            int count = 0;

            if (missionState == null ||
                missionState.ObjectiveStates == null)
            {
                return 0;
            }

            for (int i = 0;
                 i < missionState.ObjectiveStates.Count;
                 i++)
            {
                MissionObjectiveSaveData objectiveState =
                    missionState.ObjectiveStates[i];

                if (objectiveState != null &&
                    objectiveState.State == state)
                {
                    count++;
                }
            }

            return count;
        }

        private void ApplySetActive(GameObject[] objects, bool active)
        {
            if (objects == null)
                return;

            for (int i = 0; i < objects.Length; i++)
            {
                if (objects[i] != null)
                    objects[i].SetActive(active);
            }
        }

        private void ResetObjectiveByReflection(MissionObjective objective)
        {
            if (objective == null)
                return;

            if (TryInvokeNoArg(objective, "ResetObjective"))
                return;

            TrySetBool(objective, "isCompleted", false);
            TrySetBool(objective, "completed", false);
            TrySetBool(objective, "IsCompleted", false);
        }

        private bool ReadObjectiveCompletedByReflection(MissionObjective objective)
        {
            if (objective == null)
                return false;

            bool value;

            if (TryReadBool(objective, "IsCompleted", out value))
                return value;

            if (TryReadBool(objective, "isCompleted", out value))
                return value;

            if (TryReadBool(objective, "completed", out value))
                return value;

            if (TryReadBool(objective, "Completed", out value))
                return value;

            if (logDebug)
            {
                Debug.LogWarning(
                    "[MissionFlowManager] Cannot read completed state from MissionObjective on " +
                    objective.gameObject.name +
                    ". Add bool isCompleted/completed or call MissionFlowManager.CompleteObjectiveById(id) directly.",
                    objective
                );
            }

            return false;
        }

        private void NotifyObjectiveUICompleted(string objectiveId)
        {
            if (IsBlank(objectiveId))
                return;

            if (MissionObjectiveListUI.Instance != null)
                MissionObjectiveListUI.Instance.CompleteObjective(objectiveId);
        }

        private static bool TryReadBool(object target, string memberName, out bool value)
        {
            value = false;

            if (target == null || IsBlank(memberName))
                return false;

            System.Type type = target.GetType();

            FieldInfo field = type.GetField(memberName, InstanceFlags);
            if (field != null && field.FieldType == typeof(bool))
            {
                value = (bool)field.GetValue(target);
                return true;
            }

            PropertyInfo property = type.GetProperty(memberName, InstanceFlags);
            if (property != null && property.PropertyType == typeof(bool) && property.CanRead)
            {
                value = (bool)property.GetValue(target, null);
                return true;
            }

            return false;
        }

        private static bool TrySetBool(object target, string memberName, bool value)
        {
            if (target == null || IsBlank(memberName))
                return false;

            System.Type type = target.GetType();

            FieldInfo field = type.GetField(memberName, InstanceFlags);
            if (field != null && field.FieldType == typeof(bool))
            {
                field.SetValue(target, value);
                return true;
            }

            PropertyInfo property = type.GetProperty(memberName, InstanceFlags);
            if (property != null && property.PropertyType == typeof(bool) && property.CanWrite)
            {
                property.SetValue(target, value, null);
                return true;
            }

            return false;
        }

        private static bool TryInvokeNoArg(object target, string methodName)
        {
            if (target == null || IsBlank(methodName))
                return false;

            MethodInfo method = target.GetType().GetMethod(methodName, InstanceFlags, null, System.Type.EmptyTypes, null);
            if (method == null)
                return false;

            method.Invoke(target, null);
            return true;
        }

        private static bool IsBlank(string value)
        {
            return string.IsNullOrEmpty(value) || value.Trim().Length == 0;
        }
    }
