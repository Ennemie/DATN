using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Chức năng mới:
/// Enemy/NPC perform theo Route Elements mà KHÔNG dùng NavMesh cho route movement.
///
/// Route:
/// Element 0 -> 1 -> 2 -> ... -> N
/// rồi:
/// Element N -> ... -> 2 -> 1 -> 0
/// và loop.
///
/// Mỗi Element:
/// - Point
/// - Is Interacting
/// - Interaction Time
/// - Animator Name
/// - Loop / OneShotKeepFinalFrame
/// - Audio Clip (loop trong lúc Interaction)
///
/// Khi GameObject được SetActive(true):
/// - route reset về Element 0
/// - Enemy đi từ vị trí hiện tại tới Element 0
/// - không teleport về Element 0
/// </summary>
[DisallowMultipleComponent]
public class EnemyRoutePerformer :
    MonoBehaviour,
    IEnemyVisionDetectionReceiver
{
    public enum InteractionPlaybackMode
    {
        Loop,
        OneShotKeepFinalFrame
    }

    public enum DetectionReaction
    {
        ChasePlayer,
        AlarmFailure
    }

    [Serializable]
    public class RouteElement
    {
        [Header("Point")]
        public Transform point;

        [Header("Interaction")]
        public bool isInteracting = false;

        [Min(0f)]
        public float interactionTime = 0f;

        [Tooltip(
            "Tên Animator State dùng khi Is Interacting = true."
        )]
        public string animatorName =
            "Interacting";

        public InteractionPlaybackMode playback =
            InteractionPlaybackMode.Loop;

        [Header("Interaction Audio")]
        [Tooltip(
            "Audio Clip phát LOOP trong toàn bộ Interaction Time."
        )]
        public AudioClip interactionAudioClip;
    }

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 1.5f;

    [SerializeField] private float rotationSpeed = 8f;

    [Min(0.001f)]
    [SerializeField] private float arriveDistance =
        0.05f;

    [Header("Route Elements")]
    [SerializeField] private RouteElement[] elements;

    [Header("Animator")]
    [SerializeField] private Animator animator;

    [SerializeField] private string walkingAnimatorName =
        "Walking";

    [Header("Interaction Audio Source")]
    [Tooltip(
        "AudioSource dùng để phát Interaction Audio Clip. " +
        "Mỗi Element cung cấp Clip riêng."
    )]
    [SerializeField] private AudioSource interactionAudioSource;

    [Header("Vision")]
    [SerializeField] private GuardVisionView visionView;

    [Header("Detection")]
    [SerializeField] private bool alarmDetection =
        false;

    [SerializeField] private DetectionReaction detectionReaction =
        DetectionReaction.ChasePlayer;

    [Header("Direct Chase")]
    [SerializeField] private float chaseSpeed = 3.5f;

    [SerializeField] private float chaseRotationSpeed =
        10f;

    [SerializeField] private float chaseStopDistance =
        1.3f;

    [SerializeField] private string chaseAnimatorName =
        "Running";

    [Header("Legacy Conflict Protection")]
    [Tooltip(
        "EnemyRoutePerformer không dùng GuardState/GuardController/MeleeGuardController. " +
        "Nếu các component legacy đang nằm trên cùng object, script sẽ tự tắt chúng " +
        "để tránh hai hệ thống cùng điều khiển Transform."
    )]
    [SerializeField] private bool disableLegacyEnemyMovement =
        true;

    [Header("NavMesh Safety")]
    [Tooltip(
        "EnemyRoutePerformer không dùng NavMesh cho route/chase. " +
        "Nếu prefab có NavMeshAgent, script sẽ tắt Agent."
    )]
    [SerializeField] private bool disableNavMeshAgent =
        true;

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    private int currentElementIndex;
    private int travelDirection = 1;

    private bool isRunningRoute;
    private bool isInteracting;
    private bool isChasing;

    private bool meleeCombatLocked;
    private bool meleeWasChasingBeforeLock;

    public bool IsChasing => isChasing;
    public bool IsMeleeCombatLocked => meleeCombatLocked;
    public bool IsAlarmConfigured =>
        alarmDetection ||
        detectionReaction == DetectionReaction.AlarmFailure;

    // Chức năng mới:
    // Khóa Route Performer trong thời gian Stealth Take Down để không tranh Transform
    // với bước căn chỉnh vị trí của StealthTakedownController.
    // Tham chiếu: EnemyCheckpointHandler, StealthTakedownController.
    private bool stealthTakedownLocked;
    public bool IsStealthTakedownLocked => stealthTakedownLocked;

    private Coroutine interactionRoutine;

    // Chức năng mới:
    // Cho phép restart thủ công animation di chuyển nếu Animator State Walking
    // không được đánh dấu Loop trong Animation Clip.
    private bool movementAnimationWasRestartedThisFrame;

    private NavMeshAgent navMeshAgent;
    private GuardState legacyGuardState;
    private GuardController legacyGuardController;

    private readonly System.Collections.Generic.List<MonoBehaviour>
        disabledLegacyComponents =
        new System.Collections.Generic.List<MonoBehaviour>();

    private void Awake()
    {
        ResolveReferences();

        if (disableLegacyEnemyMovement)
            DisableLegacyEnemyMovement();

        if (disableNavMeshAgent &&
            navMeshAgent != null)
        {
            navMeshAgent.enabled = false;
        }
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (disableLegacyEnemyMovement)
            DisableLegacyEnemyMovement();

        if (disableNavMeshAgent &&
            navMeshAgent != null)
        {
            navMeshAgent.enabled = false;
        }

        SubscribeRuntimeEvents();

        ResetAndStartRoute();
    }

    private void OnDisable()
    {
        StopCurrentRoutine();
        StopInteractionAudio();
        UnsubscribeRuntimeEvents();

        isRunningRoute = false;
        isInteracting = false;
        isChasing = false;
        meleeCombatLocked = false;
        meleeWasChasingBeforeLock = false;
    }

    // CŨ:
    // private void Update()
    // {
    //     if (isChasing)
    //     {
    //         ...
    //     }
    // }
    //
    // MỚI:
    // Chức năng mới: Route Performer tuyệt đối không được MoveTowards/đổi animation movement
    // trong lúc Stealth Take Down đang khóa Transform.
    // Tham chiếu: EnemyCheckpointHandler, StealthTakedownController.
    private void Update()
    {
        if (stealthTakedownLocked ||
            meleeCombatLocked)
        {
            return;
        }

        if (isChasing)
        {
            UpdateDirectChase();
            return;
        }

        if (!isRunningRoute ||
            isInteracting ||
            elements == null ||
            elements.Length == 0)
        {
            return;
        }

        MoveToCurrentElement();
    }

    /// <summary>
    /// Chức năng mới:
    /// Dừng hoàn toàn Route Performer trong lúc Player Death/Failure
    /// hoặc CheckpointManager đang thực hiện restore.
    ///
    /// Điều chỉnh:
    /// - Không đổi currentElementIndex.
    /// - Không teleport.
    /// - Không restore vị trí.
    /// - Chỉ dừng movement/interaction/chase/audio.
    /// </summary>
    public void StopForCheckpointRestore()
    {
        isRunningRoute = false;
        isInteracting = false;
        isChasing = false;
        meleeCombatLocked = false;
        meleeWasChasingBeforeLock = false;

        StopCurrentRoutine();
        StopInteractionAudio();

        currentElementIndex =
            Mathf.Clamp(
                currentElementIndex,
                0,
                Mathf.Max(0, elements != null ? elements.Length - 1 : 0)
            );

        SetAnimatorSpeed(1f);

        if (animator != null &&
            HasAnimatorState("Idle"))
        {
            animator.Play(
                "Idle",
                0,
                0f
            );
        }
    }

    // Chức năng mới:
    // Khóa movement/interaction/chase của Route Performer mà không disable component.
    public void BeginStealthTakedownLock()
    {
        stealthTakedownLocked = true;
        isRunningRoute = false;
        isInteracting = false;
        isChasing = false;

        StopCurrentRoutine();
        StopInteractionAudio();
        SetAnimatorSpeed(1f);
    }

    // Chức năng mới:
    // Gỡ khóa khi restore xong. ResetAndStartRoute() sẽ tái khởi động route.
    public void EndStealthTakedownLock()
    {
        stealthTakedownLocked = false;
    }

    public void BeginMeleeCombatLock()
    {
        meleeWasChasingBeforeLock = isChasing;
        meleeCombatLocked = true;

        isRunningRoute = false;
        isInteracting = false;

        StopCurrentRoutine();
        StopInteractionAudio();

        if (logDebug)
        {
            Debug.Log(
                "[EnemyRoutePerformer] MELEE LOCK START | Enemy=" +
                gameObject.name +
                " | WasChasing=" +
                meleeWasChasingBeforeLock,
                this
            );
        }
    }

    public void EndMeleeCombatLock(
        bool resumeChase)
    {
        meleeCombatLocked = false;
        isInteracting = false;

        if (resumeChase &&
            meleeWasChasingBeforeLock &&
            !stealthTakedownLocked)
        {
            isChasing = true;
            isRunningRoute = false;
            SetAnimatorSpeed(1f);
            PlayAnimatorIfChanged(
                chaseAnimatorName
            );
        }
        else
        {
            isChasing = false;

            if (!isRunningRoute)
                ResetAndStartRoute();
        }

        meleeWasChasingBeforeLock = false;

        if (logDebug)
        {
            Debug.Log(
                "[EnemyRoutePerformer] MELEE LOCK END | Enemy=" +
                gameObject.name +
                " | ResumeChase=" + resumeChase,
                this
            );
        }
    }

    /// <summary>
    /// Chức năng mới:
    /// Reset route về Element 0 và bắt đầu perform.
    ///
    /// Điều chỉnh quan trọng:
    /// Không teleport Enemy về Element 0.
    /// Enemy sẽ đi từ vị trí hiện tại tới Element 0.
    /// </summary>
    // CŨ:
    // public void ResetAndStartRoute()
    // {
    //     StopCurrentRoutine();
    //     ...
    // }
    //
    // MỚI:
    // Restore xong mới gỡ Stealth Take Down lock; sau đó route được phép khởi động lại.
    // Tham chiếu: EnemyCheckpointHandler.
    public void ResetAndStartRoute()
    {
        stealthTakedownLocked = false;

        StopCurrentRoutine();
        StopInteractionAudio();

        isRunningRoute = false;
        isInteracting = false;
        isChasing = false;

        currentElementIndex = 0;
        travelDirection = 1;

        SetAnimatorSpeed(1f);

        if (!HasValidRoute())
            return;

        PlayAnimatorIfChanged(
            walkingAnimatorName
        );

        movementAnimationWasRestartedThisFrame = false;
        isRunningRoute = true;
    }

    /// <summary>
    /// Chức năng mới:
    /// Callback RED từ GuardVisionView.
    ///
    /// Alarm:
    ///     Player Failure.
    ///
    /// Không Alarm:
    ///     Direct Chase bằng Transform/MoveTowards.
    /// </summary>
    // CŨ:
    // public void OnPlayerVisionConfirmed(Vector3 playerPosition)
    // {
    //     ...
    // }
    //
    // MỚI:
    // RED callback bị bỏ qua trong lúc Stealth Take Down để không Chase/Failure lại.
    // Tham chiếu: GuardVisionView, EnemyCheckpointHandler.
    public void OnPlayerVisionConfirmed(
        Vector3 playerPosition)
    {
        if (stealthTakedownLocked)
            return;

        if (PlayerProperties.Instance == null)
            return;

        if (alarmDetection ||
            detectionReaction ==
            DetectionReaction.AlarmFailure)
        {
            StopCurrentRouteForDetection();

            PlayerProperties.Instance
                .RequestFailureToCheckpoint();

            return;
        }

        if (detectionReaction ==
            DetectionReaction.ChasePlayer)
        {
            BeginDirectChase();
        }
    }

    private void BeginDirectChase()
    {
        StopCurrentRoutine();
        StopInteractionAudio();

        isRunningRoute = false;
        isInteracting = false;
        isChasing = true;

        SetAnimatorSpeed(1f);

        PlayAnimatorIfChanged(
            chaseAnimatorName
        );
    }

    private void UpdateDirectChase()
    {
        if (PlayerProperties.Instance == null)
        {
            StopDirectChase();
            return;
        }

        if (PlayerProperties.Instance.IsDeadOrFailing)
        {
            StopDirectChase();
            return;
        }

        Vector3 targetPosition =
            PlayerProperties.Instance
                .transform.position;

        Vector3 flatDirection =
            targetPosition -
            transform.position;

        flatDirection.y = 0f;

        float distance =
            flatDirection.magnitude;

        if (distance > chaseStopDistance)
        {
            Vector3 direction =
                flatDirection.normalized;

            transform.position =
                Vector3.MoveTowards(
                    transform.position,
                    targetPosition,
                    chaseSpeed *
                    Time.deltaTime
                );

            RotateTowards(
                direction,
                chaseRotationSpeed
            );

            PlayAnimatorIfChanged(
                chaseAnimatorName
            );
        }
        else if (
            flatDirection.sqrMagnitude >
            0.0001f)
        {
            RotateTowards(
                flatDirection.normalized,
                chaseRotationSpeed
            );
        }
    }

    private void MoveToCurrentElement()
    {
        if (currentElementIndex < 0 ||
            currentElementIndex >= elements.Length)
        {
            ResetAndStartRoute();
            return;
        }

        RouteElement element =
            elements[currentElementIndex];

        if (element == null ||
            element.point == null)
        {
            AdvanceToNextElement();
            return;
        }

        Vector3 targetPosition =
            element.point.position;

        Vector3 direction =
            targetPosition -
            transform.position;

        direction.y = 0f;

        float distance =
            direction.magnitude;

        if (distance <= arriveDistance)
        {
            transform.position =
                new Vector3(
                    targetPosition.x,
                    transform.position.y,
                    targetPosition.z
                );

            BeginElementAction(
                element
            );

            return;
        }

        EnsureMovementAnimatorIsPlaying();

        if (direction.sqrMagnitude >
            0.0001f)
        {
            RotateTowards(
                direction.normalized,
                rotationSpeed
            );
        }

        transform.position =
            Vector3.MoveTowards(
                transform.position,
                targetPosition,
                moveSpeed *
                Time.deltaTime
            );
    }

    private void BeginElementAction(
        RouteElement element)
    {
        if (element.isInteracting)
        {
            isInteracting = true;

            interactionRoutine =
                StartCoroutine(
                    RunInteraction(
                        element
                    )
                );

            return;
        }

        AdvanceToNextElement();
    }

    /// <summary>
    /// Chức năng mới:
    /// Chạy Interaction tại waypoint.
    ///
    /// - Loop: restart animation mỗi cycle.
    /// - OneShotKeepFinalFrame: chạy một lần và giữ frame cuối.
    /// - Audio Clip của Element được loop trong Interaction Time.
    /// </summary>
    private IEnumerator RunInteraction(
        RouteElement element)
    {
        SetAnimatorSpeed(1f);

        string stateName =
            string.IsNullOrWhiteSpace(
                element.animatorName
            )
                ? "Interacting"
                : element.animatorName;

        float interactionTime =
            Mathf.Max(
                0f,
                element.interactionTime
            );

        PlayInteractionAudio(
            element.interactionAudioClip
        );

        if (interactionTime <= 0f)
        {
            PlayAnimatorFromStart(
                stateName
            );

            StopInteractionAudio();

            isInteracting = false;
            interactionRoutine = null;

            AdvanceToNextElement();
            yield break;
        }

        if (!HasAnimatorState(stateName))
        {
            Debug.LogWarning(
                "[EnemyRoutePerformer] Animator State không tồn tại: " +
                stateName +
                " trên " +
                gameObject.name,
                this
            );

            yield return new WaitForSeconds(
                interactionTime
            );

            StopInteractionAudio();

            isInteracting = false;
            interactionRoutine = null;

            AdvanceToNextElement();
            yield break;
        }

        if (element.playback ==
            InteractionPlaybackMode.Loop)
        {
            yield return RunLoopInteraction(
                stateName,
                interactionTime
            );
        }
        else
        {
            yield return RunOneShotKeepFinalFrame(
                stateName,
                interactionTime
            );
        }

        SetAnimatorSpeed(1f);
        StopInteractionAudio();

        isInteracting = false;
        interactionRoutine = null;

        AdvanceToNextElement();
    }

    private IEnumerator RunLoopInteraction(
        string stateName,
        float interactionTime)
    {
        float elapsed = 0f;

        while (elapsed < interactionTime)
        {
            PlayAnimatorFromStart(
                stateName
            );

            yield return null;

            float clipLength =
                GetCurrentStateLength();

            if (clipLength <= 0.01f)
                clipLength = 0.1f;

            float remaining =
                interactionTime -
                elapsed;

            float cycleTime =
                Mathf.Min(
                    clipLength,
                    remaining
                );

            yield return new WaitForSeconds(
                cycleTime
            );

            elapsed += cycleTime;
        }
    }

    private IEnumerator RunOneShotKeepFinalFrame(
        string stateName,
        float interactionTime)
    {
        PlayAnimatorFromStart(
            stateName
        );

        yield return null;

        float clipLength =
            GetCurrentStateLength();

        if (clipLength <= 0.01f)
            clipLength = interactionTime;

        float playTime =
            Mathf.Min(
                interactionTime,
                clipLength
            );

        yield return new WaitForSeconds(
            playTime
        );

        if (playTime >= clipLength)
        {
            // Chức năng mới:
            // Giữ frame cuối của animation trong phần thời gian Interaction còn lại.
            SetAnimatorSpeed(0f);

            float remaining =
                Mathf.Max(
                    0f,
                    interactionTime -
                    clipLength
                );

            if (remaining > 0f)
            {
                yield return new WaitForSeconds(
                    remaining
                );
            }

            SetAnimatorSpeed(1f);
        }
    }

    private void AdvanceToNextElement()
    {
        if (!HasValidRoute())
            return;

        if (elements.Length == 1)
        {
            currentElementIndex = 0;
            travelDirection = 1;
            return;
        }

        int nextIndex =
            currentElementIndex +
            travelDirection;

        if (nextIndex >= 0 &&
            nextIndex < elements.Length)
        {
            currentElementIndex =
                nextIndex;

            return;
        }

        if (travelDirection > 0)
        {
            travelDirection = -1;
            currentElementIndex =
                elements.Length - 2;

            return;
        }

        travelDirection = 1;
        currentElementIndex = 1;
    }

    private void StopDirectChase()
    {
        isChasing = false;
        isInteracting = false;
        isRunningRoute = true;

        SetAnimatorSpeed(1f);

        PlayAnimatorIfChanged(
            walkingAnimatorName
        );
    }

    private void StopCurrentRouteForDetection()
    {
        isRunningRoute = false;
        isInteracting = false;

        StopCurrentRoutine();
        StopInteractionAudio();
    }

    private void StopCurrentRoutine()
    {
        if (interactionRoutine != null)
        {
            StopCoroutine(
                interactionRoutine
            );

            interactionRoutine = null;
        }

        SetAnimatorSpeed(1f);
    }

    private bool HasValidRoute()
    {
        if (elements == null ||
            elements.Length == 0)
        {
            Debug.LogWarning(
                "[EnemyRoutePerformer] Chưa có Route Elements: " +
                gameObject.name,
                this
            );

            return false;
        }

        return true;
    }

    private void RotateTowards(
        Vector3 direction,
        float rotationSpeedValue)
    {
        if (direction.sqrMagnitude <=
            0.0001f)
        {
            return;
        }

        Quaternion targetRotation =
            Quaternion.LookRotation(
                direction
            );

        transform.rotation =
            Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotationSpeedValue *
                Time.deltaTime
            );
    }

    // Chức năng mới:
    // Chỉ chuyển Animator State khi cần và tự xử lý trường hợp Animation Clip
    // Walking không được import ở chế độ Loop.
    //
    // Điều chỉnh:
    // - Không gọi CrossFade mỗi frame.
    // - Nếu state Walking đã đứng ở frame cuối vì clip không Loop,
    //   tự Play lại từ frame 0 để Enemy vẫn có animation đi bộ liên tục.
    // - Ép Animator.speed về 1 nếu một Interaction trước đó đã giữ speed = 0.
    private void PlayAnimatorIfChanged(
        string stateName)
    {
        if (animator == null ||
            string.IsNullOrWhiteSpace(
                stateName))
        {
            return;
        }

        if (!HasAnimatorState(stateName))
            return;

        if (animator.speed <= 0f)
            animator.speed = 1f;

        AnimatorStateInfo info =
            animator.GetCurrentAnimatorStateInfo(0);

        if (!info.IsName(stateName))
        {
            movementAnimationWasRestartedThisFrame = true;

            animator.Play(
                stateName,
                0,
                0f
            );

            return;
        }

        movementAnimationWasRestartedThisFrame = false;
    }

    // Chức năng mới:
    // Đảm bảo Animation Walking thực sự tiếp tục phát khi Enemy đang di chuyển.
    // Nếu state không Loop và normalizedTime đã tới frame cuối,
    // script tự chạy lại từ frame đầu thay vì đứng tại frame cuối.
    private void EnsureMovementAnimatorIsPlaying()
    {
        if (animator == null ||
            string.IsNullOrWhiteSpace(
                walkingAnimatorName))
        {
            return;
        }

        if (!HasAnimatorState(walkingAnimatorName))
            return;

        if (animator.speed <= 0f)
            animator.speed = 1f;

        AnimatorStateInfo info =
            animator.GetCurrentAnimatorStateInfo(0);

        if (!info.IsName(walkingAnimatorName))
        {
            animator.Play(
                walkingAnimatorName,
                0,
                0f
            );

            return;
        }

        // Unity có thể trả normalizedTime > 1 nếu state loop.
        // Chỉ restart thủ công khi state thực sự không Loop.
        if (!info.loop &&
            info.normalizedTime >= 0.999f &&
            !movementAnimationWasRestartedThisFrame)
        {
            movementAnimationWasRestartedThisFrame = true;

            animator.Play(
                walkingAnimatorName,
                0,
                0f
            );

            return;
        }

        movementAnimationWasRestartedThisFrame = false;
    }

    private void PlayAnimatorFromStart(
        string stateName)
    {
        if (animator == null ||
            string.IsNullOrWhiteSpace(
                stateName))
        {
            return;
        }

        if (!HasAnimatorState(stateName))
            return;

        animator.Play(
            stateName,
            0,
            0f
        );
    }

    private bool HasAnimatorState(
        string stateName)
    {
        if (animator == null ||
            animator.runtimeAnimatorController ==
            null)
        {
            return false;
        }

        return animator.HasState(
            0,
            Animator.StringToHash(
                stateName
            )
        );
    }

    private float GetCurrentStateLength()
    {
        if (animator == null)
            return 0f;

        AnimatorStateInfo info =
            animator.GetCurrentAnimatorStateInfo(0);

        return info.length;
    }

    private void SetAnimatorSpeed(
        float speedValue)
    {
        if (animator != null)
            animator.speed = speedValue;
    }

    private void PlayInteractionAudio(
        AudioClip clip)
    {
        if (interactionAudioSource == null)
        {
            if (clip != null)
            {
                Debug.LogWarning(
                    "[EnemyRoutePerformer] Có Interaction Audio Clip nhưng chưa gán AudioSource: " +
                    gameObject.name,
                    this
                );
            }

            return;
        }

        interactionAudioSource.Stop();
        interactionAudioSource.clip = clip;
        interactionAudioSource.loop = true;

        if (clip != null)
            interactionAudioSource.Play();
    }

    private void StopInteractionAudio()
    {
        if (interactionAudioSource == null)
            return;

        interactionAudioSource.Stop();
        interactionAudioSource.clip = null;
        interactionAudioSource.loop = false;
    }

    private void ResolveReferences()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        if (visionView == null)
            visionView =
                GetComponent<GuardVisionView>();

        if (navMeshAgent == null)
            navMeshAgent =
                GetComponent<NavMeshAgent>();

        if (legacyGuardState == null)
            legacyGuardState =
                GetComponent<GuardState>();

        if (legacyGuardController == null)
            legacyGuardController =
                GetComponent<GuardController>();

        if (interactionAudioSource == null)
            interactionAudioSource =
                GetComponent<AudioSource>();

    }

    private void DisableLegacyEnemyMovement()
    {
        // Chức năng mới:
        // EnemyRoutePerformer là movement authority của prefab mới.
        // GuardState/GuardController/MeleeGuardController nếu còn nằm trên prefab
        // sẽ bị tắt để tránh DOTween/NavMesh cùng điều khiển Transform.
        if (legacyGuardState != null &&
            legacyGuardState.enabled)
        {
            legacyGuardState.enabled = false;
            disabledLegacyComponents.Add(
                legacyGuardState
            );
        }

        if (legacyGuardController != null &&
            legacyGuardController !=
            (MonoBehaviour)legacyGuardState &&
            legacyGuardController.enabled)
        {
            legacyGuardController.enabled = false;
            disabledLegacyComponents.Add(
                legacyGuardController
            );
        }
    }

    private void SubscribeRuntimeEvents()
    {
        PlayerProperties.DeathOrFailureStarted -=
            HandlePlayerDeathOrFailureStarted;

        PlayerProperties.DeathOrFailureStarted +=
            HandlePlayerDeathOrFailureStarted;
    }

    private void UnsubscribeRuntimeEvents()
    {
        PlayerProperties.DeathOrFailureStarted -=
            HandlePlayerDeathOrFailureStarted;
    }

    private void HandlePlayerDeathOrFailureStarted()
    {
        if (!isActiveAndEnabled)
            return;

        // Điều chỉnh:
        // Player Death/Failure chỉ STOP Route/Chase ngay lập tức.
        // Tuyệt đối KHÔNG reset position/route tại event này.
        // EnemyCheckpointHandler và CheckpointManager mới quyết định khi nào restore.
        StopForCheckpointRestore();
    }

    private void OnValidate()
    {
        moveSpeed =
            Mathf.Max(
                0f,
                moveSpeed
            );

        chaseSpeed =
            Mathf.Max(
                0f,
                chaseSpeed
            );

        arriveDistance =
            Mathf.Max(
                0.001f,
                arriveDistance
            );

        chaseStopDistance =
            Mathf.Max(
                0f,
                chaseStopDistance
            );
    }
}
