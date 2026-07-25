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

            [Header("Checkpoint")]
            public bool saveCheckpointOnEnter;
            public Transform checkpointPoint;
            public string checkpointId;
            public string checkpointMessage = "Checkpoint";

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
            public string[] newObjectiveTexts;
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

        private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public int CurrentElementIndex => currentElementIndex;
        public Transform CurrentCheckpoint => currentCheckpoint;
        public string CurrentCheckpointId => currentCheckpointId;
        public bool IsBusy => isTransitioning || isCompletingElement;

        private void Awake()
        {
            if (objectiveUI == null)
                objectiveUI = FindFirstObjectByType<MissionObjectiveUI>();

            if (checkpointToast == null)
                checkpointToast = FindFirstObjectByType<MissionCheckpointToast>();

            if (failManager == null)
                failManager = FindFirstObjectByType<MissionFailManager>();

            if (cameraDirector == null)
                cameraDirector = FindFirstObjectByType<MissionCameraDirector>();

            if (dialogueController == null)
                dialogueController = FindFirstObjectByType<DialogueController>();
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
            ActivateElementByIndex(startElementIndex);
        }

        public void StartMissionAtElement(int index)
        {
            ActivateElementByIndex(index);
        }

        public void ActivateElementByIndex(int index)
        {
        Debug.Log("[MissionFlow] ActivateElementByIndex " + index);
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

            activeFlowRoutine = StartCoroutine(EnterElementRoutine(index));
        }

        public void CompleteCurrentElement()
        {
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

            if (element.resetRequiredObjectivesOnEnter && element.requiredObjectives != null)
            {
                for (int i = 0; i < element.requiredObjectives.Length; i++)
                {
                    if (element.requiredObjectives[i] != null)
                        ResetObjectiveByReflection(element.requiredObjectives[i]);
                }
            }

            if (element.saveCheckpointOnEnter)
                SaveCheckpoint(element.checkpointPoint, element.checkpointId, element.checkpointMessage);

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

            MissionElement element = elements[elementIndex];

            if (logDebug)
                //Debug.Log("[MissionFlowManager] Completed element " + elementIndex + ": " + element.elementName, this);

            // Post flow: chạy sau complete nhưng trước khi sang element tiếp theo.
            if (element.conversationAfterComplete != null)
                yield return PlayConversation(element.conversationAfterComplete);

            yield return PlayCameraShots(element.cameraShotsAfterComplete);

            isCompletingElement = false;
            activeFlowRoutine = null;

            if (!element.proceedToNextElementAfterPostFlow)
            {
                if (logDebug)
                   // Debug.Log("[MissionFlowManager] Post flow ended. Not proceeding because Proceed To Next Element After Post Flow is false.", this);

                yield break;
            }

            int nextIndex = elementIndex + 1;
            if (nextIndex >= elements.Length)
            {
                if (logDebug)
                    Debug.Log("[MissionFlowManager] Mission completed. No more elements.", this);

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
            if (objectiveUI == null || element == null)
                return;

            if (element.objectiveAnnouncements != null && element.objectiveAnnouncements.Length > 0)
            {
                for (int i = 0; i < element.objectiveAnnouncements.Length; i++)
                {
                    ObjectiveAnnouncement announcement = element.objectiveAnnouncements[i];
                    if (announcement == null || IsBlank(announcement.text))
                        continue;

                    string objectiveId = IsBlank(announcement.objectiveId) ? announcement.text : announcement.objectiveId;
                    objectiveUI.QueueObjective(announcement.title, objectiveId, announcement.text, announcement.addToActiveList);
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

                    objectiveUI.QueueObjectiveWithId(objectiveText, objectiveText);
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
              //  Debug.Log("[MissionFlowManager] All required objectives completed for element " + currentElementIndex + ": " + element.elementName, this);

            CompleteCurrentElement();
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
