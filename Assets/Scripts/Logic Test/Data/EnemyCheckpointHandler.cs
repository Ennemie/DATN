using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Chức năng mới: cầu nối Enemy với hệ thống checkpoint/restore.
///
/// - Lưu Transform ban đầu làm baseline restore.
/// - Theo dõi Enemy chết runtime nhưng không Destroy để còn revive.
/// - Enemy chết và đã đi qua checkpoint sẽ được ghi Permanent Dead.
/// - Continue / Player death: permanent thì giữ dead, còn temporary thì trở về baseline.
/// - AI được trả về behavior lúc bắt đầu scene.
/// - Khi Temporary Dead hoàn tất, Enemy có thể chạy hai list object Active/Inactive
///   đúng một lần cho mỗi death cycle.
/// 
/// Điều chỉnh quan trọng:
/// Player DeathOrFailureStarted chỉ dùng để STOP AI ngay lập tức.
/// Không restore Position/HP tại thời điểm bắt đầu DeathSpread.
/// Restore chỉ diễn ra khi CheckpointManager phát CheckpointRestoreRequested
/// hoặc PersistentCheckpointApplied.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EnemySaveIdentity))]
public class EnemyCheckpointHandler : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnemySaveIdentity saveIdentity;
    [SerializeField] private EnemyProperties enemyProperties;
    [SerializeField] private GuardState guardState;
    [SerializeField] private GuardController guardController;
    [SerializeField] private EnemyRoutePerformer routePerformer;

    [Header("Death Handling")]
    [Tooltip(
        "Thời gian giữ Enemy sau khi animation Stunned x2 bắt đầu trước khi tắt body/collider."
    )]
    [Min(0f)]
    [SerializeField] private float deathDisableDelay =
        1.1f;

    [Tooltip(
        "Sau thời gian death, tắt renderer/collider/controller để Enemy không còn tương tác."
    )]
    [SerializeField] private bool disableEnemyAfterDeathAnimation =
        true;

    [Header("Temporary Dead Object State")]
    [Tooltip(
        "Danh sách GameObject được SetActive(true) đúng một lần khi Enemy vừa hoàn tất Temporary Dead."
    )]
    [SerializeField] private GameObject[] activateObjectsOnTemporaryDead;

    [Tooltip(
        "Danh sách GameObject được SetActive(false) đúng một lần khi Enemy vừa hoàn tất Temporary Dead. " +
        "Nếu một object nằm ở cả hai list, list Active được áp dụng sau cùng."
    )]
    [SerializeField] private GameObject[] deactivateObjectsOnTemporaryDead;

    [Header("Stealth Take Down")]
    [Tooltip("Animator State của Enemy cho Stealth Take Down. Nếu State nằm trong Sub-State Machine, có thể dùng Full Path ở field bên dưới.")]
    [SerializeField] private string stealthTakeDownAnimationName =
        "GetStealthTakeDown";

    // MỚI:
    // Tùy chọn Full Animator State Path, ví dụ:
    // Base Layer.Stealth.GetStealthTakeDown
    // Để trống thì hệ thống thử short state name trên toàn bộ Animator layer (-1).
    [Tooltip("Full Animator State Path tùy chọn. Để trống để tự thử state name trên mọi layer.")]
    [SerializeField] private string stealthTakeDownAnimationStatePath = string.Empty;

    // MỚI:
    // -1 = Animator tự tìm layer chứa state. >= 0 = ép dùng layer cụ thể.
    [Tooltip("Animator Layer. -1 = tự tìm trên mọi layer.")]
    [SerializeField] private int stealthTakeDownAnimatorLayer = -1;

    [Tooltip("Tổng số frame animation Stealth Take Down. Clip hiện tại là 210 frame.")]
    [Min(1)]
    [SerializeField] private int stealthTakeDownTotalFrames =
        210;

    [Tooltip("Y cố định của Enemy trong toàn bộ Stealth Take Down.")]
    [SerializeField] private float stealthTakeDownLockedY =
        0f;

    [Tooltip("Thời gian giữ pose cuối sau khi animation đạt frame cuối trước khi Temporary Dead.")]
    [Min(0f)]
    [SerializeField] private float stealthTakeDownPostAnimationDelay =
        5f;

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    private CheckpointManager checkpointManager;
    private GameDataManager dataManager;

    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private GuardState.State initialGuardState =
        GuardState.State.Guarding;

    private int initialHealth = 100;
    private bool initialBaselineCaptured;
    private bool runtimeDead;
    private bool permanentlyDead;

    // Chức năng mới:
    // Mỗi lần Enemy đi vào Temporary Dead, hai list object death-state chỉ được
    // thực thi một lần. Khi Enemy được restore về baseline, cờ này được reset
    // để lần chết tiếp theo có thể thực thi lại.
    private bool temporaryDeadObjectStateApplied;

    private Coroutine deathDisableRoutine;
    private Coroutine meleeStunnedDeathRoutine;

    [Header("Melee Stunned Presentation")]
    [Min(0.01f)]
    [SerializeField] private float meleeCombatAnimationSpeed = 1.5f;

    [Min(0f)]
    [SerializeField] private float meleeStunnedPostAnimationDelay = 0.25f;

    private NavMeshAgent agent;
    private GuardVisionView guardVisionView;
    private Animator animator;

    private Collider[] allColliders;
    private Renderer[] allRenderers;

    private bool[] initialColliderEnabled;
    private bool[] initialRendererEnabled;

    private bool initialGuardControllerEnabled;
    private bool initialGuardStateEnabled;
    private bool initialEnemyPropertiesEnabled;
    private bool initialAgentEnabled;
    private bool initialVisionEnabled;
    private bool initialAnimatorEnabled;
    private bool initialRoutePerformerEnabled;
    private bool initialAnimatorRootMotion;

    private Coroutine stealthTakeDownRoutine;
    private bool stealthTakedownInProgress;
    private bool stealthTakedownFinalFrameReached;

    // MỚI:
    // Lưu layer/path thực tế sau khi Animator đã nhận animation để coroutine
    // theo dõi đúng layer thay vì giả định luôn là layer 0.
    private int activeStealthAnimatorLayer = 0;
    private string activeStealthAnimatorStateName = string.Empty;

    public string PersistentEnemyId =>
        saveIdentity != null
            ? saveIdentity.PersistentEnemyId
            : string.Empty;

    public bool IsRuntimeDead =>
        runtimeDead;

    public bool IsPermanentlyDead =>
        permanentlyDead;

    // Chức năng mới:
    // Cho StealthTakedownController biết Enemy còn đang trong presentation
    // hay đã hoàn tất Temporary Dead.
    public bool IsStealthTakedownInProgress =>
        stealthTakedownInProgress;

    // CŨ:
    // Kiểm tra tối thiểu trước khi Player bắt đầu Stealth Take Down.
    // Hàm cũ dùng Animator.HasState() để quyết định target có hợp lệ hay không.
    //
    // MỚI:
    // Validation này chỉ kiểm tra điều kiện runtime cơ bản của Enemy.
    // Không dùng HasState() ở đây nữa vì State có thể nằm trong cấu trúc Animator
    // mà short-name hash không resolve được như clip/state mà Artist đang dùng.
    // Việc xác nhận animation thực tế được chuyển vào StartStealthTakedownPresentation().
    // Tham chiếu: StealthTakedownController gọi hàm này trước khi bắt đầu action.
    public bool CanBeginStealthTakedown()
    {
        ResolveReferences();

        if (runtimeDead || permanentlyDead)
            return false;

        if (stealthTakedownInProgress)
            return false;

        if (animator == null ||
            !animator.enabled ||
            animator.runtimeAnimatorController == null)
        {
            return false;
        }

        return true;
    }

    public bool ShouldCommitAsPermanentDeath =>
        runtimeDead &&
        !string.IsNullOrWhiteSpace(
            PersistentEnemyId
        );

    private void Awake()
    {
        ResolveReferences();
        ResolveManagers();

        // Transform được chụp trước khi logic di chuyển bắt đầu.
        initialPosition =
            transform.position;

        initialRotation =
            transform.rotation;

        CaptureComponentEnableStates();

        // Chức năng mới:
        // Nếu prefab là loại Route Performer, movement authority là
        // EnemyRoutePerformer. Legacy GuardState/NavMesh sẽ không được re-enable.
        if (routePerformer != null &&
            routePerformer.enabled)
        {
            initialGuardControllerEnabled = false;
            initialGuardStateEnabled = false;
            initialAgentEnabled = false;
        }

        if (ShouldStartInactiveForLoadedContinue())
        {
            permanentlyDead = true;
            runtimeDead = true;

            ApplyDisabledRuntimeState();

            gameObject.SetActive(false);
        }
    }

    private bool ShouldStartInactiveForLoadedContinue()
    {
        if (dataManager == null ||
            !dataManager.ContinueRestorePending)
        {
            return false;
        }

        string enemyId =
            PersistentEnemyId;

        if (string.IsNullOrWhiteSpace(
            enemyId
        ))
        {
            return false;
        }

        return dataManager.IsEnemyPermanentlyDead(
            enemyId
        );
    }

    private IEnumerator Start()
    {
        ResolveReferences();

        yield return null;

        ResolveManagers();
        SubscribeCheckpointEvents();

        if (enemyProperties != null)
            initialHealth =
                enemyProperties.CurrentHealth;

        initialGuardState =
            GuardState.State.Guarding;

        initialBaselineCaptured = true;
    }

    private void OnEnable()
    {
        ResolveReferences();
        ResolveManagers();
        SubscribeCheckpointEvents();
    }

    private void OnDisable()
    {
        UnsubscribeCheckpointEvents();
    }

    private void OnDestroy()
    {
        UnsubscribeCheckpointEvents();
    }

    /// <summary>
    /// Chức năng mới:
    /// Bắt đầu death pipeline của Enemy.
    ///
    /// Điều chỉnh:
    /// - Stop Chase/NavMesh ngay khi HP = 0.
    /// - Không restore ở đây.
    /// - Chờ CheckpointManager phát restore event sau đó.
    /// </summary>
// CŨ:     public void BeginRuntimeDeath(
// CŨ:         bool stealthDeath)
// CŨ:     {
// CŨ:         if (runtimeDead)
// CŨ:             return;
//
// CŨ:         runtimeDead = true;
//
// CŨ:         ResolveReferences();
// CŨ:         StopDeathDisableRoutine();
//
// CŨ:         StopEnemyMovementImmediately();
//
// CŨ:         if (guardVisionView != null)
// CŨ:         {
// CŨ:             guardVisionView.SetRestoreLock(
// CŨ:                 true
// CŨ:             );
//
// CŨ:             guardVisionView.enabled = false;
// CŨ:         }
//
// CŨ:         if (guardState != null)
// CŨ:         {
// CŨ:             guardState.EnterDeathState(
// CŨ:                 stealthDeath
// CŨ:                     ? GuardState.State.StealthDying
// CŨ:                     : GuardState.State.Stunned
// CŨ:             );
// CŨ:         }
//
// CŨ:         if (deathDisableDelay <= 0f)
// CŨ:         {
// CŨ:             FinalizeRuntimeDeath();
// CŨ:         }
// CŨ:         else
// CŨ:         {
// CŨ:             deathDisableRoutine =
// CŨ:                 StartCoroutine(
// CŨ:                     FinalizeRuntimeDeathAfterDelay()
// CŨ:                 );
// CŨ:         }
// CŨ:     }
// MỚI:
// public bool BeginRuntimeDeath(
//     bool stealthDeath)
// {
//     if (runtimeDead)
//         return false;
//
//     runtimeDead = true;
//
//     ResolveReferences();
//     StopDeathDisableRoutine();
//
//     // CŨ:
//     // StopEnemyMovementImmediately();
//     //
//     // if (guardVisionView != null)
//     // {
//     //     guardVisionView.SetRestoreLock(true);
//     //     guardVisionView.enabled = false;
//     // }
//     //
//     // if (guardState != null)
//     // {
//     //     guardState.EnterDeathState(
//     //         stealthDeath
//     //             ? GuardState.State.StealthDying
//     //             : GuardState.State.Stunned
//     //     );
//     // }
//     //
//     // if (deathDisableDelay <= 0f)
//     //     FinalizeRuntimeDeath();
//     // else
//     //     deathDisableRoutine = StartCoroutine(FinalizeRuntimeDeathAfterDelay());
//     //
//     // MỚI:
//     // Tách rõ hai death pipeline:
//     // - Death thường giữ timing cũ.
//     // - Stealth Take Down chạy GetStealthTakeDown tới frame cuối,
//     //   giữ pose thêm stealthTakeDownPostAnimationDelay rồi mới Temporary Dead.
//     StopEnemyMovementImmediately();
//
//     if (guardVisionView != null)
//     {
//         guardVisionView.SetRestoreLock(
//             true
//         );
//
//         guardVisionView.enabled = false;
//     }
//
//     if (guardState != null)
//     {
//         guardState.EnterDeathState(
//             stealthDeath
//                 ? GuardState.State.StealthDying
//                 : GuardState.State.Stunned
//         );
//     }
//
//     if (stealthDeath)
//     {
//         StartStealthTakedownPresentation();
//         return true;
//     }
//
//     if (deathDisableDelay <= 0f)
//     {
//         FinalizeRuntimeDeath();
//     }
//     else
//     {
//         deathDisableRoutine =
//             StartCoroutine(
//                 FinalizeRuntimeDeathAfterDelay()
//             );
//     }
//
//     return true;
// }
// MỚI:
// Chức năng mới:
// Giữ nguyên death pipeline hiện tại, chỉ bổ sung log chi tiết để xác nhận
// StealthTakedownController đã thực sự gọi sang Enemy hay chưa và Enemy đang ở trạng thái nào.
public bool BeginRuntimeDeath(
    bool stealthDeath)
{
    if (runtimeDead)
    {
        if (logDebug)
        {
            Debug.LogWarning(
                "[EnemyCheckpointHandler] BeginRuntimeDeath REJECTED: runtimeDead=true | " +
                "Enemy=" + gameObject.name +
                " | stealthDeath=" + stealthDeath,
                this
            );
        }

        return false;
    }

    runtimeDead = true;

    ResolveReferences();
    StopDeathDisableRoutine();

    // Chức năng mới:
    // Log trước khi khóa AI và bắt đầu presentation.
    if (logDebug)
    {
        Debug.Log(
            "[EnemyCheckpointHandler] BeginRuntimeDeath ACCEPTED | " +
            "Enemy=" + gameObject.name +
            " | stealthDeath=" + stealthDeath +
            " | Position=" + transform.position +
            " | Animator=" +
            (animator != null ? animator.name : "<NULL>"),
            this
        );
    }

    StopEnemyMovementImmediately();

    if (guardVisionView != null)
    {
        guardVisionView.SetRestoreLock(true);
        guardVisionView.enabled = false;
    }

    if (guardState != null)
    {
        guardState.EnterDeathState(
            stealthDeath
                ? GuardState.State.StealthDying
                : GuardState.State.Stunned
        );
    }

    // CŨ:
    // if (stealthDeath)
    // {
    //     StartStealthTakedownPresentation();
    //     return true;
    // }
    //
    // MỚI:
    // Stealth Take Down phải xác nhận animation thực sự bắt đầu trước khi
    // coi Enemy là đã đi vào death presentation. Nếu Animator không tìm thấy
    // state, rollback ngay về runtime baseline thay vì vô tình giết/disable Enemy.
    if (stealthDeath)
    {
        if (!StartStealthTakedownPresentation())
        {
            runtimeDead = false;
            RestoreInitialRuntimeState();

            if (logDebug)
            {
                Debug.LogError(
                    "[EnemyCheckpointHandler] STEALTH TAKEDOWN START FAILED -> Enemy restored to runtime baseline | " +
                    "Enemy=" + gameObject.name,
                    this
                );
            }

            return false;
        }

        return true;
    }

    if (deathDisableDelay <= 0f)
    {
        FinalizeRuntimeDeath();
    }
    else
    {
        deathDisableRoutine =
            StartCoroutine(
                FinalizeRuntimeDeathAfterDelay()
            );
    }

    return true;
}


    // CŨ:
    // private bool StartStealthTakedownPresentation()
    // {
    //     ...
    //     animator.Play(stealthTakeDownAnimationName, 0, 0f);
    //     ...
    // }
    //
    // MỚI:
    // Animator state của Enemy có thể nằm ở layer khác hoặc nằm trong
    // Sub-State Machine. Không còn hard-code layer 0 + short name.
    // Hệ thống thử Full Path nếu được cấu hình; nếu không, dùng short state name
    // với layer -1 để Unity tự tìm state trên các layer, sau đó xác định layer thực tế.
    // Tham chiếu: StealthTakedownController -> EnemyCheckpointHandler.
    private bool StartStealthTakedownPresentation()
    {
        StopStealthTakedownPresentationRoutine();

        stealthTakedownInProgress = true;
        stealthTakedownFinalFrameReached = false;
        activeStealthAnimatorLayer = 0;
        activeStealthAnimatorStateName = stealthTakeDownAnimationName;

        if (animator == null)
        {
            Debug.LogError(
                "[EnemyCheckpointHandler] STEALTH START FAILED: Animator = NULL | Enemy=" +
                gameObject.name,
                this
            );

            stealthTakedownInProgress = false;
            return false;
        }

        if (!animator.enabled)
        {
            Debug.LogError(
                "[EnemyCheckpointHandler] STEALTH START FAILED: Animator disabled | Enemy=" +
                gameObject.name,
                this
            );

            stealthTakedownInProgress = false;
            return false;
        }

        if (animator.runtimeAnimatorController == null)
        {
            Debug.LogError(
                "[EnemyCheckpointHandler] STEALTH START FAILED: runtimeAnimatorController = NULL | Enemy=" +
                gameObject.name,
                this
            );

            stealthTakedownInProgress = false;
            return false;
        }

        animator.applyRootMotion = false;
        animator.speed = 1f;

        // Y phải được khóa trước khi Play để frame 0 đã ở đúng mặt phẳng gameplay.
        SetEnemyStealthLockedPosition();

        if (logDebug)
        {
            Debug.Log(
                "[EnemyCheckpointHandler] STEALTH ANIMATOR REQUEST | " +
                "Enemy=" + gameObject.name +
                " | StateName='" + stealthTakeDownAnimationName + "'" +
                " | StatePath='" +
                (string.IsNullOrWhiteSpace(stealthTakeDownAnimationStatePath)
                    ? "<auto>"
                    : stealthTakeDownAnimationStatePath) + "'" +
                " | RequestedLayer=" + stealthTakeDownAnimatorLayer,
                this
            );
        }

        bool playAccepted = false;
        System.Exception lastException = null;

        try
        {
            // Ưu tiên Full Path nếu người làm game đã cấu hình.
            if (!string.IsNullOrWhiteSpace(stealthTakeDownAnimationStatePath))
            {
                int requestedLayer =
                    stealthTakeDownAnimatorLayer >= 0
                        ? stealthTakeDownAnimatorLayer
                        : -1;

                animator.Play(
                    stealthTakeDownAnimationStatePath,
                    requestedLayer,
                    0f
                );

                animator.Update(0f);

                for (int layer = 0;
                     layer < animator.layerCount;
                     layer++)
                {
                    AnimatorStateInfo pathInfo =
                        animator.GetCurrentAnimatorStateInfo(layer);

                    if (pathInfo.IsName(stealthTakeDownAnimationName) ||
                        pathInfo.IsName(stealthTakeDownAnimationStatePath) ||
                        pathInfo.shortNameHash ==
                            Animator.StringToHash(stealthTakeDownAnimationName))
                    {
                        activeStealthAnimatorLayer = layer;
                        playAccepted = true;
                        break;
                    }
                }
            }

            // Fallback: short state name trên layer tự tìm.
            if (!playAccepted)
            {
                animator.Play(
                    stealthTakeDownAnimationName,
                    -1,
                    0f
                );

                animator.Update(0f);

                int stateHash =
                    Animator.StringToHash(
                        stealthTakeDownAnimationName
                    );

                for (int layer = 0;
                     layer < animator.layerCount;
                     layer++)
                {
                    AnimatorStateInfo info =
                        animator.GetCurrentAnimatorStateInfo(layer);

                    if (info.IsName(stealthTakeDownAnimationName) ||
                        info.shortNameHash == stateHash)
                    {
                        activeStealthAnimatorLayer = layer;
                        playAccepted = true;
                        break;
                    }
                }
            }
        }
        catch (System.Exception ex)
        {
            lastException = ex;
        }

        if (!playAccepted)
        {
            string exceptionText =
                lastException != null
                    ? " | Exception=" + lastException.Message
                    : string.Empty;

            Debug.LogError(
                "[EnemyCheckpointHandler] STEALTH START FAILED: Animator state could not be entered | " +
                "Enemy=" + gameObject.name +
                " | RequestedState='" + stealthTakeDownAnimationName + "'" +
                " | RequestedPath='" + stealthTakeDownAnimationStatePath + "'" +
                " | LayerRequest=" + stealthTakeDownAnimatorLayer +
                " | LayerCount=" + animator.layerCount +
                exceptionText,
                this
            );

            LogAnimatorLayersForDiagnosis();

            stealthTakedownInProgress = false;
            return false;
        }

        AnimatorStateInfo startedState =
            animator.GetCurrentAnimatorStateInfo(
                activeStealthAnimatorLayer
            );

        activeStealthAnimatorStateName =
            stealthTakeDownAnimationName;

        if (logDebug)
        {
            Debug.Log(
                "[EnemyCheckpointHandler] STEALTH ANIMATION STARTED | " +
                "Enemy=" + gameObject.name +
                " | Layer=" + activeStealthAnimatorLayer +
                " (" + animator.GetLayerName(activeStealthAnimatorLayer) + ")" +
                " | State=" + stealthTakeDownAnimationName +
                " | FullPathHash=" + startedState.fullPathHash +
                " | ShortNameHash=" + startedState.shortNameHash +
                " | LockedY=" + stealthTakeDownLockedY +
                " | TotalFrames=" + stealthTakeDownTotalFrames,
                this
            );

            AnimatorClipInfo[] clips =
                animator.GetCurrentAnimatorClipInfo(
                    activeStealthAnimatorLayer
                );

            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i].clip != null)
                {
                    Debug.Log(
                        "[EnemyCheckpointHandler] STEALTH CLIP INFO | " +
                        "Enemy=" + gameObject.name +
                        " | Layer=" + activeStealthAnimatorLayer +
                        " | Clip=" + clips[i].clip.name +
                        " | Length=" + clips[i].clip.length + "s" +
                        " | FrameEstimate=" +
                        Mathf.RoundToInt(
                            clips[i].clip.length * 30f
                        ),
                        this
                    );
                }
            }
        }

        stealthTakeDownRoutine =
            StartCoroutine(
                RunStealthTakedownPresentation()
            );

        if (logDebug)
        {
            Debug.Log(
                "[EnemyCheckpointHandler] STEALTH PRESENTATION COROUTINE STARTED | Enemy=" +
                gameObject.name +
                " | ActiveLayer=" + activeStealthAnimatorLayer,
                this
            );
        }

        return true;
    }

    // Chức năng mới:
    // Khi Enemy Animator không tìm thấy state, in toàn bộ layer name và current state hash.
    // Mục tiêu là lần test sau có đủ thông tin để biết state nằm sai layer/path hay thật sự không tồn tại.
    private void LogAnimatorLayersForDiagnosis()
    {
        if (animator == null || !logDebug)
            return;

        for (int layer = 0; layer < animator.layerCount; layer++)
        {
            AnimatorStateInfo info =
                animator.GetCurrentAnimatorStateInfo(layer);

            Debug.LogError(
                "[EnemyCheckpointHandler] ANIMATOR LAYER DIAGNOSTIC | " +
                "Enemy=" + gameObject.name +
                " | LayerIndex=" + layer +
                " | LayerName=" + animator.GetLayerName(layer) +
                " | CurrentFullPathHash=" + info.fullPathHash +
                " | CurrentShortNameHash=" + info.shortNameHash,
                this
            );
        }

        RuntimeAnimatorController controller =
            animator.runtimeAnimatorController;

        if (controller != null && controller.animationClips != null)
        {
            foreach (AnimationClip clip in controller.animationClips)
            {
                if (clip != null)
                {
                    Debug.Log(
                        "[EnemyCheckpointHandler] ANIMATOR CLIP AVAILABLE | " +
                        "Enemy=" + gameObject.name +
                        " | Clip=" + clip.name +
                        " | Length=" + clip.length + "s",
                        this
                    );
                }
            }
        }
    }

    // Chức năng mới:
    // Theo dõi Enemy animation theo frame.
    // Từ frame 0 tới frame cuối: ép Y cố định.
    // Khi đạt frame cuối: giữ pose, chờ 5 giây rồi FinalizeRuntimeDeath().
    private IEnumerator RunStealthTakedownPresentation()
    {
        while (animator != null &&
               animator.enabled &&
               !stealthTakedownFinalFrameReached)
        {
            SetEnemyStealthLockedPosition();

            AnimatorStateInfo info =
                animator.GetCurrentAnimatorStateInfo(
                    activeStealthAnimatorLayer
                );

            bool stillInStealthState =
                info.IsName(stealthTakeDownAnimationName) ||
                info.shortNameHash ==
                    Animator.StringToHash(
                        stealthTakeDownAnimationName
                    );

            if (!stillInStealthState)
            {
                if (logDebug)
                {
                    Debug.LogError(
                        "[EnemyCheckpointHandler] STEALTH ANIMATION INTERRUPTED | " +
                        "Enemy=" + gameObject.name +
                        " | ExpectedState=" + stealthTakeDownAnimationName +
                        " | Layer=" + activeStealthAnimatorLayer +
                        " | CurrentFullPathHash=" + info.fullPathHash +
                        " | CurrentShortNameHash=" + info.shortNameHash,
                        this
                    );
                }

                break;
            }

            float normalizedTime =
                Mathf.Max(
                    0f,
                    info.normalizedTime
                );

            float currentFrame =
                normalizedTime *
                stealthTakeDownTotalFrames;

            if (currentFrame >=
                stealthTakeDownTotalFrames)
            {
                // Chức năng mới:
                // Ép animation về cuối clip rồi dừng speed = 0 để pose nằm
                // chính xác ở frame cuối trong thời gian chờ.
                animator.Play(
                    activeStealthAnimatorStateName,
                    activeStealthAnimatorLayer,
                    1f
                );

                animator.Update(0f);
                animator.speed = 0f;

                SetEnemyStealthLockedPosition();

                stealthTakedownFinalFrameReached = true;

                if (logDebug)
                {
                    Debug.Log(
                        "[EnemyCheckpointHandler] STEALTH ANIMATION FINAL FRAME REACHED | " +
                        "Enemy=" + gameObject.name +
                        " | Frame=" + currentFrame.ToString("0.###") +
                        " / " + stealthTakeDownTotalFrames +
                        " | Layer=" + activeStealthAnimatorLayer +
                        " | LockedY=" + stealthTakeDownLockedY,
                        this
                    );
                }

                break;
            }

            yield return null;
        }

        if (!stealthTakedownFinalFrameReached)
        {
            stealthTakeDownRoutine = null;
            stealthTakedownInProgress = false;

            if (logDebug)
            {
                Debug.LogError(
                    "[EnemyCheckpointHandler] STEALTH PRESENTATION ABORTED BEFORE FINAL FRAME | " +
                    "Enemy=" + gameObject.name,
                    this
                );
            }

            yield break;
        }

        float remainingDelay =
            Mathf.Max(
                0f,
                stealthTakeDownPostAnimationDelay
            );

        if (logDebug)
        {
            Debug.Log(
                "[EnemyCheckpointHandler] STEALTH FINAL POSE HOLD STARTED | " +
                "Enemy=" + gameObject.name +
                " | Delay=" + remainingDelay + "s",
                this
            );
        }

        while (remainingDelay > 0f)
        {
            SetEnemyStealthLockedPosition();

            remainingDelay -= Time.deltaTime;
            yield return null;
        }

        SetEnemyStealthLockedPosition();

        stealthTakeDownRoutine = null;
        stealthTakedownInProgress = false;

        // MỚI:
        // Xác nhận đã chờ xong hậu animation trước khi Enemy bị disable.
        if (logDebug)
        {
            Debug.Log(
                "[EnemyCheckpointHandler] STEALTH POST-ANIMATION DELAY COMPLETE | " +
                "Enemy=" + gameObject.name +
                " | Delay=" + stealthTakeDownPostAnimationDelay + "s",
                this
            );
        }

        // Enemy chỉ trở thành Temporary Dead SAU khi đã giữ pose đủ 5 giây.
        FinalizeRuntimeDeath();
    }

    // Chức năng mới:
    // Ép Enemy giữ Y cố định mà không làm thay đổi X/Z.
    private void SetEnemyStealthLockedPosition()
    {
        Vector3 lockedPosition =
            transform.position;

        lockedPosition.y =
            stealthTakeDownLockedY;

        transform.position =
            lockedPosition;
    }

    // Chức năng mới:
    // Hủy presentation đang chạy khi Player Failure xảy ra.
    // Không tự revive Enemy; CheckpointManager vẫn là authority restore.
    public void CancelStealthTakedownPresentation()
    {
        StopStealthTakedownPresentationRoutine();
        StopMeleeStunnedDeathRoutine();

        stealthTakedownInProgress = false;
        stealthTakedownFinalFrameReached = false;

        if (animator != null)
        {
            animator.speed = 1f;
            animator.applyRootMotion =
                initialAnimatorRootMotion;
        }
    }

    // Chức năng mới:
    // Dừng riêng coroutine Stealth Take Down trước khi restore/disable.
    private void StopStealthTakedownPresentationRoutine()
    {
        if (stealthTakeDownRoutine != null)
        {
            StopCoroutine(
                stealthTakeDownRoutine
            );

            stealthTakeDownRoutine = null;
        }
    }

    private void StopEnemyMovementImmediately()
    {
        if (routePerformer != null &&
            routePerformer.enabled)
        {
            routePerformer.StopForCheckpointRestore();
        }

        if (guardController != null)
            guardController.StopAllMovementImmediately();

        if (agent != null)
        {
            if (agent.enabled &&
                agent.isOnNavMesh)
            {
                agent.ResetPath();
            }

            agent.velocity =
                Vector3.zero;

            agent.isStopped = true;
        }
    }

    public void PerformStealthTakedown()
    {
        if (enemyProperties != null)
            enemyProperties.StealthTakedown();
        else
            BeginRuntimeDeath(true);
    }

    public void MarkPermanentAtCheckpoint()
    {
        permanentlyDead =
            runtimeDead;
    }

    public void RestoreFromCommittedData()
    {
        ResolveReferences();
        ResolveManagers();

        StopDeathDisableRoutine();

        bool savedPermanentDeath =
            dataManager != null &&
            dataManager.IsEnemyPermanentlyDead(
                PersistentEnemyId
            );

        permanentlyDead =
            savedPermanentDeath;

        if (savedPermanentDeath)
        {
            runtimeDead = true;

            ApplyDisabledRuntimeState();

            if (gameObject.activeSelf)
                gameObject.SetActive(false);

            if (logDebug)
            {
                Debug.Log(
                    "[EnemyCheckpointHandler] Permanent dead restored: " +
                    PersistentEnemyId,
                    this
                );
            }

            return;
        }

        RestoreInitialRuntimeState();
    }

    private void RestoreInitialRuntimeState()
    {
        // Chức năng mới:
        // Restore phải hủy hoàn toàn presentation Stealth Take Down cũ
        // trước khi đặt Transform về baseline.
        StopStealthTakedownPresentationRoutine();
        StopMeleeStunnedDeathRoutine();
        stealthTakedownInProgress = false;
        stealthTakedownFinalFrameReached = false;

        if (!initialBaselineCaptured)
        {
            initialHealth = 100;
            initialGuardState =
                GuardState.State.Guarding;
        }

        // Chức năng mới:
        // Chỉ khóa movement trong thời gian restore.
        // Không dùng event DeathOrFailureStarted để restore sớm.
        if (routePerformer != null &&
            routePerformer.enabled)
        {
            routePerformer.StopForCheckpointRestore();
        }

        if (guardController != null)
            guardController.BeginCheckpointRestore();

        if (guardVisionView != null)
            guardVisionView.SetRestoreLock(true);

        runtimeDead = false;
        permanentlyDead = false;

        // Chức năng mới:
        // Cho phép hai list Temporary Dead chạy lại ở lần chết tiếp theo.
        temporaryDeadObjectStateApplied = false;

        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        transform.SetPositionAndRotation(
            initialPosition,
            initialRotation
        );

        EnableInitialComponents();

        if (enemyProperties != null)
        {
            enemyProperties
                .SetHealthForCheckpointRestore(
                    initialHealth
                );
        }

        // Legacy Enemy:
        // reset controller/state.
        if (routePerformer == null ||
            !routePerformer.enabled)
        {
            if (guardController != null)
                guardController.ResetRuntimeState();

            if (guardState != null)
            {
                guardState.transform.position =
                    initialPosition;

                guardState.transform.rotation =
                    initialRotation;

                guardState.ResetForCheckpointRestore();
            }

            if (agent != null)
            {
                if (agent.enabled &&
                    agent.isOnNavMesh)
                {
                    agent.Warp(
                        initialPosition
                    );

                    agent.ResetPath();
                }

                agent.isStopped = true;
                agent.velocity = Vector3.zero;
            }
        }

        if (routePerformer != null &&
            initialRoutePerformerEnabled)
        {
            // Chức năng mới:
            // Chỉ bật Route Performer ở cuối restore để OnEnable không chạy
            // giữa lúc Position/HP/Animator còn đang được khôi phục.
            routePerformer.enabled = true;

            // Route bắt đầu SAU KHI Position/HP/Animator đã ổn định.
            routePerformer.StopForCheckpointRestore();
            routePerformer.ResetAndStartRoute();

            if (agent != null)
                agent.enabled = false;
        }

        if (animator != null)
        {
            animator.enabled =
                initialAnimatorEnabled;

            animator.speed = 1f;
            animator.applyRootMotion =
                initialAnimatorRootMotion;
        }

        if (guardVisionView != null)
        {
            guardVisionView.enabled = initialVisionEnabled;

            // Vision hiển thị WHITE sau restore.
            // Player phải rời FOV rồi mới được detect lại.
            guardVisionView.SetRestoreLock(false);
            guardVisionView.ResetToNeutral(true);
        }

        if (guardController != null &&
            (routePerformer == null ||
             !routePerformer.enabled))
        {
            guardController.EndCheckpointRestore();
        }

        if (logDebug)
        {
            Debug.Log(
                "[EnemyCheckpointHandler] Temporary death/restore completed: " +
                PersistentEnemyId,
                this
            );
        }
    }

    private void FinalizeRuntimeDeath()
    {
        deathDisableRoutine = null;
        StopMeleeStunnedDeathRoutine();

        if (logDebug)
        {
            Debug.Log(
                "[EnemyCheckpointHandler] TEMPORARY DEAD FINALIZED | Enemy=" +
                gameObject.name,
                this
            );
        }

        if (disableEnemyAfterDeathAnimation)
            ApplyDisabledRuntimeState();

        // Chức năng mới:
        // Temporary Dead là thời điểm chính thức thực thi hai list object
        // Active/Inactive của riêng Enemy. Đây là one-shot cho mỗi death cycle.
        ApplyTemporaryDeadObjectState();
    }

    private IEnumerator FinalizeRuntimeDeathAfterDelay()
    {
        yield return new WaitForSeconds(
            deathDisableDelay
        );

        FinalizeRuntimeDeath();
    }

    private void ApplyTemporaryDeadObjectState()
    {
        if (temporaryDeadObjectStateApplied)
            return;

        temporaryDeadObjectStateApplied = true;

        ApplySetActiveSafe(
            deactivateObjectsOnTemporaryDead,
            false
        );

        ApplySetActiveSafe(
            activateObjectsOnTemporaryDead,
            true
        );

        if (logDebug)
        {
            int activeCount =
                activateObjectsOnTemporaryDead != null
                    ? activateObjectsOnTemporaryDead.Length
                    : 0;

            int inactiveCount =
                deactivateObjectsOnTemporaryDead != null
                    ? deactivateObjectsOnTemporaryDead.Length
                    : 0;

            Debug.Log(
                "[EnemyCheckpointHandler] Temporary Dead object state applied | " +
                "Enemy=" + gameObject.name +
                " | Activate=" + activeCount +
                " | Deactivate=" + inactiveCount,
                this
            );
        }
    }

    private void ApplySetActiveSafe(
        GameObject[] objects,
        bool active)
    {
        if (objects == null)
            return;

        for (int i = 0; i < objects.Length; i++)
        {
            GameObject target = objects[i];

            if (target == null)
                continue;

            // Không cho list tự disable Enemy đang chạy death pipeline.
            if (target == gameObject)
            {
                Debug.LogWarning(
                    "[EnemyCheckpointHandler] Ignored Temporary Dead object list entry because it targets the Enemy itself.",
                    this
                );
                continue;
            }

            target.SetActive(active);
        }
    }

    private void ApplyDisabledRuntimeState()
    {
        // Chức năng mới:
        // Khi Enemy chuyển Temporary Dead, presentation coroutine không được
        // tiếp tục ghi Transform sau khi component đã bị disable.
        StopStealthTakedownPresentationRoutine();
        stealthTakedownInProgress = false;
        stealthTakedownFinalFrameReached = true;

        // Route Performer không bị disable khi Enemy chết tạm thời.
        // Nó được giữ enabled nhưng StopForCheckpointRestore() đã dừng hoàn toàn
        // để CheckpointManager có thể gọi restore mà không bị OnEnable race.
        if (routePerformer != null)
            routePerformer.StopForCheckpointRestore();

        if (guardController != null)
            guardController.enabled = false;

        if (guardState != null)
            guardState.enabled = false;

        if (enemyProperties != null)
            enemyProperties.enabled = false;

        if (agent != null)
            agent.enabled = false;

        if (guardVisionView != null)
        {
            guardVisionView.SetRestoreLock(
                true
            );

            guardVisionView.enabled = false;
        }

        if (animator != null)
            animator.enabled = false;

        if (allColliders != null)
        {
            for (int i = 0;
                 i < allColliders.Length;
                 i++)
            {
                if (allColliders[i] != null)
                    allColliders[i].enabled =
                        false;
            }
        }

        if (allRenderers != null)
        {
            for (int i = 0;
                 i < allRenderers.Length;
                 i++)
            {
                if (allRenderers[i] != null)
                    allRenderers[i].enabled =
                        false;
            }
        }
    }

    private void EnableInitialComponents()
    {
        // Chức năng mới:
        // Route Performer phải được giữ DISABLED trong lúc restore.
        // Nếu bật quá sớm, OnEnable() của Route Performer sẽ tự ResetAndStartRoute()
        // trước khi Position/HP hoàn tất, tạo race với CheckpointManager.
        // Handler sẽ bật lại ở cuối RestoreInitialRuntimeState().
        if (routePerformer != null)
            routePerformer.enabled = false;

        if (routePerformer == null ||
            !initialRoutePerformerEnabled)
        {
            if (guardController != null)
                guardController.enabled =
                    initialGuardControllerEnabled;

            if (guardState != null)
                guardState.enabled =
                    initialGuardStateEnabled;

            if (agent != null)
                agent.enabled =
                    initialAgentEnabled;
        }
        else
        {
            // Chức năng mới:
            // Route Performer là movement authority.
            // Legacy movement components tuyệt đối không được bật lại.
            if (guardController != null)
                guardController.enabled = false;

            if (guardState != null)
                guardState.enabled = false;

            if (agent != null)
                agent.enabled = false;
        }

        if (enemyProperties != null)
            enemyProperties.enabled =
                initialEnemyPropertiesEnabled;

        if (guardVisionView != null)
            guardVisionView.enabled =
                initialVisionEnabled;

        if (animator != null)
            animator.enabled =
                initialAnimatorEnabled;

        RestoreComponentArrays();
    }

    private void RestoreComponentArrays()
    {
        if (allColliders != null &&
            initialColliderEnabled != null)
        {
            for (int i = 0;
                 i < allColliders.Length;
                 i++)
            {
                if (allColliders[i] != null &&
                    i < initialColliderEnabled.Length)
                {
                    allColliders[i].enabled =
                        initialColliderEnabled[i];
                }
            }
        }

        if (allRenderers != null &&
            initialRendererEnabled != null)
        {
            for (int i = 0;
                 i < allRenderers.Length;
                 i++)
            {
                if (allRenderers[i] != null &&
                    i < initialRendererEnabled.Length)
                {
                    allRenderers[i].enabled =
                        initialRendererEnabled[i];
                }
            }
        }
    }

    private void CaptureComponentEnableStates()
    {
        agent =
            GetComponent<NavMeshAgent>();

        guardVisionView =
            GetComponent<GuardVisionView>();

        animator =
            GetComponent<Animator>();

        allColliders =
            GetComponentsInChildren<Collider>(
                true
            );

        allRenderers =
            GetComponentsInChildren<Renderer>(
                true
            );

        initialGuardControllerEnabled =
            guardController == null ||
            guardController.enabled;

        initialGuardStateEnabled =
            guardState == null ||
            guardState.enabled;

        initialEnemyPropertiesEnabled =
            enemyProperties == null ||
            enemyProperties.enabled;

        initialAgentEnabled =
            agent == null ||
            agent.enabled;

        initialVisionEnabled =
            guardVisionView == null ||
            guardVisionView.enabled;

        initialAnimatorEnabled =
            animator == null ||
            animator.enabled;

        initialAnimatorRootMotion =
            animator != null &&
            animator.applyRootMotion;

        initialRoutePerformerEnabled =
            routePerformer == null ||
            routePerformer.enabled;

        initialColliderEnabled =
            new bool[
                allColliders.Length
            ];

        for (int i = 0;
             i < allColliders.Length;
             i++)
        {
            initialColliderEnabled[i] =
                allColliders[i] != null &&
                allColliders[i].enabled;
        }

        initialRendererEnabled =
            new bool[
                allRenderers.Length
            ];

        for (int i = 0;
             i < allRenderers.Length;
             i++)
        {
            initialRendererEnabled[i] =
                allRenderers[i] != null &&
                allRenderers[i].enabled;
        }
    }

    private void ResolveReferences()
    {
        if (saveIdentity == null)
            saveIdentity =
                GetComponent<EnemySaveIdentity>();

        if (enemyProperties == null)
            enemyProperties =
                GetComponent<EnemyProperties>();

        if (guardState == null)
            guardState =
                GetComponent<GuardState>();

        if (guardController == null)
            guardController =
                GetComponent<GuardController>();

        if (routePerformer == null)
            routePerformer =
                GetComponent<EnemyRoutePerformer>();

        if (guardVisionView == null)
            guardVisionView =
                GetComponent<GuardVisionView>();

        if (agent == null)
            agent =
                GetComponent<NavMeshAgent>();

        if (animator == null)
            animator =
                GetComponent<Animator>();
    }

    private void ResolveManagers()
    {
        if (checkpointManager == null)
            checkpointManager =
                FindAnyObjectByType<CheckpointManager>();

        if (dataManager == null)
            dataManager =
                GameDataManager.Instance;

        if (dataManager == null)
            dataManager =
                FindAnyObjectByType<GameDataManager>();
    }

    private void SubscribeCheckpointEvents()
    {
        if (checkpointManager != null)
        {
            checkpointManager
                .CheckpointRestoreRequested -=
                HandleCheckpointRestoreRequested;

            checkpointManager
                .CheckpointRestoreRequested +=
                HandleCheckpointRestoreRequested;

            checkpointManager
                .PersistentCheckpointApplied -=
                HandlePersistentCheckpointApplied;

            checkpointManager
                .PersistentCheckpointApplied +=
                HandlePersistentCheckpointApplied;
        }

        // Chức năng mới:
        // Player Death/Failure chỉ STOP movement.
        // Không gọi RestoreFromCommittedData ở đây.
        PlayerProperties.DeathOrFailureStarted -=
            HandlePlayerDeathOrFailureStarted;

        PlayerProperties.DeathOrFailureStarted +=
            HandlePlayerDeathOrFailureStarted;
    }

    private void UnsubscribeCheckpointEvents()
    {
        if (checkpointManager != null)
        {
            checkpointManager
                .CheckpointRestoreRequested -=
                HandleCheckpointRestoreRequested;

            checkpointManager
                .PersistentCheckpointApplied -=
                HandlePersistentCheckpointApplied;
        }

        PlayerProperties.DeathOrFailureStarted -=
            HandlePlayerDeathOrFailureStarted;
    }

    private void HandleCheckpointRestoreRequested(
        CheckpointManager.CheckpointState checkpoint)
    {
        // Chức năng mới:
        // Restore thật sự chỉ diễn ra khi CheckpointManager phát event.
        RestoreFromCommittedData();
    }

    private void HandlePersistentCheckpointApplied(
        CheckpointManager.CheckpointState checkpoint)
    {
        // Continue restore = snapshot đã commit.
        RestoreFromCommittedData();
    }

    private void HandlePlayerDeathOrFailureStarted()
    {
        if (!isActiveAndEnabled)
            return;

        // Điều chỉnh:
        // Đúng thời điểm Death/Failure bắt đầu, chỉ ngắt movement/chase.
        // KHÔNG restore Position/HP/route ở đây.
        // Restore thật sự chỉ chạy khi CheckpointManager phát restore event.
        StopEnemyMovementImmediately();
    }

    /// <summary>
    /// Final Enemy response to the third Player Punch:
    /// Stunned at 1.5x -> freeze on last frame -> existing Temporary Dead.
    /// </summary>
    public bool BeginMeleeStunnedDeath(
        float playbackSpeed = -1f)
    {
        if (runtimeDead)
        {
            if (logDebug)
            {
                Debug.LogWarning(
                    "[EnemyCheckpointHandler] MELEE STUNNED DEATH REJECTED: runtimeDead=true | Enemy=" +
                    gameObject.name,
                    this
                );
            }

            return false;
        }

        ResolveReferences();
        StopDeathDisableRoutine();
        StopMeleeStunnedDeathRoutine();

        runtimeDead = true;
        stealthTakedownInProgress = false;
        stealthTakedownFinalFrameReached = false;

        StopEnemyMovementImmediately();

        if (guardVisionView != null)
        {
            guardVisionView.SetRestoreLock(true);
            guardVisionView.enabled = false;
        }

        if (guardState != null)
        {
            guardState.isDead = true;
            guardState.EnterDeathState(
                GuardState.State.Stunned
            );
        }

        if (animator == null ||
            !animator.enabled ||
            animator.runtimeAnimatorController == null)
        {
            Debug.LogError(
                "[EnemyCheckpointHandler] MELEE STUNNED DEATH FAILED: Animator unavailable | Enemy=" +
                gameObject.name,
                this
            );

            runtimeDead = false;

            if (guardState != null)
                guardState.isDead = false;

            return false;
        }

        float speed =
            playbackSpeed > 0f
                ? playbackSpeed
                : meleeCombatAnimationSpeed;

        meleeStunnedDeathRoutine =
            StartCoroutine(
                RunMeleeStunnedDeathPresentation(
                    speed
                )
            );

        return true;
    }

    private IEnumerator RunMeleeStunnedDeathPresentation(
        float playbackSpeed)
    {
        const string stateName = "Stunned";

        try
        {
            animator.applyRootMotion = false;
            animator.speed =
                Mathf.Max(
                    0.01f,
                    playbackSpeed
                );

            animator.Play(
                stateName,
                0,
                0f
            );

            animator.Update(0f);

            AnimatorStateInfo startedInfo =
                animator.GetCurrentAnimatorStateInfo(0);

            bool accepted =
                startedInfo.IsName(stateName) ||
                startedInfo.shortNameHash ==
                    Animator.StringToHash(stateName);

            if (!accepted)
            {
                Debug.LogError(
                    "[EnemyCheckpointHandler] MELEE STUNNED STATE FAILED | Enemy=" +
                    gameObject.name +
                    " | State=" + stateName,
                    this
                );

                runtimeDead = false;

                if (guardState != null)
                    guardState.isDead = false;

                meleeStunnedDeathRoutine = null;
                animator.speed = 1f;
                yield break;
            }

            float playbackDuration =
                startedInfo.length /
                Mathf.Max(
                    0.01f,
                    playbackSpeed
                );

            if (logDebug)
            {
                Debug.Log(
                    "[EnemyCheckpointHandler] MELEE STUNNED PLAY START | " +
                    "Enemy=" + gameObject.name +
                    " | ClipLength=" +
                    startedInfo.length.ToString("0.###") +
                    "s | Speed=" +
                    playbackSpeed.ToString("0.###") +
                    "x | Duration=" +
                    playbackDuration.ToString("0.###") +
                    "s",
                    this
                );
            }

            yield return new WaitForSeconds(
                Mathf.Max(
                    0.01f,
                    playbackDuration
                )
            );

            if (animator == null ||
                !animator.enabled)
            {
                runtimeDead = false;

                if (guardState != null)
                    guardState.isDead = false;

                meleeStunnedDeathRoutine = null;
                yield break;
            }

            animator.Play(
                stateName,
                0,
                1f
            );

            animator.Update(0f);
            animator.speed = 0f;

            if (logDebug)
            {
                Debug.Log(
                    "[EnemyCheckpointHandler] MELEE STUNNED FINAL FRAME REACHED | Enemy=" +
                    gameObject.name,
                    this
                );
            }

            float hold =
                Mathf.Max(
                    0f,
                    meleeStunnedPostAnimationDelay
                );

            if (hold > 0f)
            {
                yield return new WaitForSeconds(
                    hold
                );
            }

            animator.speed = 1f;
            meleeStunnedDeathRoutine = null;

            FinalizeRuntimeDeath();
        }
        finally
        {
            if (!runtimeDead &&
                meleeStunnedDeathRoutine != null)
            {
                meleeStunnedDeathRoutine = null;

                if (animator != null)
                    animator.speed = 1f;
            }
        }
    }

    public void CancelMeleeStunnedDeathPresentation()
    {
        StopMeleeStunnedDeathRoutine();

        runtimeDead = false;

        if (guardState != null)
            guardState.isDead = false;

        if (guardVisionView != null)
            guardVisionView.SetRestoreLock(false);
    }

    private void StopMeleeStunnedDeathRoutine()
    {
        if (meleeStunnedDeathRoutine != null)
        {
            StopCoroutine(
                meleeStunnedDeathRoutine
            );

            meleeStunnedDeathRoutine = null;
        }

        if (animator != null)
            animator.speed = 1f;
    }

    private void StopDeathDisableRoutine()
    {
        if (deathDisableRoutine != null)
        {
            StopCoroutine(
                deathDisableRoutine
            );

            deathDisableRoutine = null;
        }
    }
}
