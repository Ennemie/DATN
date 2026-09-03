using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Stealth Take Down controller cho Player.
///
/// Luồng chính:
/// 1. StealthTakedownTrigger phát hiện Enemy phía trước.
/// 2. Enemy phải đang ở trạng thái Vision WHITE + đã được phép detect lại.
/// 3. Button StealthTakedown được bật.
/// 4. Click -> khóa Player, căn chỉnh Player/Enemy về cùng hướng + khoảng cách chuẩn.
/// 5. Phát StealthTakeDown (Player) và GetStealthTakeDown (Enemy) từ frame 0 trong cùng frame.
/// 6. Enemy giữ pose frame cuối, chờ thêm 5 giây rồi mới temporary-dead.
/// 7. Player trở lại điều khiển bình thường.
///
/// Tham chiếu:
/// - PlayerState / PlayerController
/// - EnemyCheckpointHandler
/// - GuardVisionView
/// - StealthTakedownTrigger
/// - EnemyProperties / GuardState / GuardController / EnemyRoutePerformer
/// </summary>
[RequireComponent(typeof(PlayerController))]
[RequireComponent(typeof(PlayerState))]
public class StealthTakedownController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private StealthTakedownTrigger takedownTrigger;
    [SerializeField] private GameObject stealthTakedownButton;
    [SerializeField] private Button stealthTakedownButtonComponent;

    [Header("Punch Button")]
    [SerializeField] private GameObject punchButton;
    [SerializeField] private Button punchButtonComponent;

    [Header("Performative Melee")]
    [Min(0.01f)]
    [SerializeField] private float punchPairDistance = 0.5f;
    [Min(0.01f)]
    [SerializeField] private float punchPlaybackSpeed = 1.5f;
    [Range(0.05f, 0.95f)]
    [SerializeField] private float punchHitNormalizedTime = 0.5f;
    [Min(0f)]
    [SerializeField] private float punchKnockbackDistance = 0.3f;
    [Min(0.01f)]
    [SerializeField] private float punchKnockbackDuration = 0.12f;
    [SerializeField] private bool requireFistWeaponForPunch = true;

    [Header("Player Punch Animation")]
    [SerializeField] private string punchAttack1AnimatorName = "Attack1";
    [SerializeField] private string punchAttack2AnimatorName = "Attack2";
    [SerializeField] private string punchGetHitAnimatorName = "GetHit";

    [Header("Player Punch Audio")]
    [SerializeField] private AudioClip punchSoundClip1;
    [SerializeField] private AudioClip punchSoundClip2;
    [SerializeField] private AudioClip punchSoundClip3;
    [Range(0f, 1f)]
    [SerializeField] private float punchSoundVolume = 1f;
    [Tooltip("AudioSource nằm trên chính Player. Punch sẽ dùng PlayOneShot trên source này thay vì PlayClipAtPoint.")]
    [SerializeField] private AudioSource punchAudioSource;

    [Header("Enemy Hit Presentation")]
    [Min(0.01f)]
    [SerializeField] private float enemyHitFallbackDuration = 0.65f;

    [Header("Alignment")]
    [Tooltip("Khoảng cách từ Player tới Enemy sau khi snap. Player đứng phía sau Enemy theo Enemy forward.")]
    [Min(0f)]
    [SerializeField] private float playerBehindEnemyDistance = 0.5f;

    // CŨ:
    // [Tooltip("Giữ nguyên Y của Player khi snap. Nếu false, lấy Y của Enemy.")]
    // [SerializeField] private bool keepPlayerY = true;
    //
    // MỚI:
    // Flow mới luôn lấy Player Base Y tại frame 0 làm Y gốc.
    // Y của Player chỉ thay đổi bởi PlayerYJump trong timeline đã cấu hình.

    [Tooltip("Khoá Rigidbody của Player trong suốt action để physics không làm lệch animation pair.")]
    [SerializeField] private bool freezePlayerRigidbodyDuringTakedown = true;

    [Tooltip("Tắt Root Motion Player trong action để vị trí không bị animation tự kéo lệch.")]
    [SerializeField] private bool disablePlayerRootMotion = true;

    [Header("Validation")]
    [Tooltip("Khoảng cách tối đa giữa Player và Enemy tại thời điểm click.")]
    [Min(0.01f)]
    [SerializeField] private float maxInitialDistance = 2.5f;

    [Tooltip("Chênh lệch hướng tối đa giữa hướng Player -> Enemy và Enemy forward. Chỉ dùng nếu bật Use Behind Angle Validation.")]
    [Range(1f, 180f)]
    [SerializeField] private float maxBehindAngle = 60f;

    // MỚI:
    // Trigger StealthTakedown là authority xác định range. Vì vậy mặc định không
    // dùng thêm khoảng cách thứ hai để tránh Button hiện nhưng bị disable dù Enemy
    // vẫn đang nằm hợp lệ trong trigger. Có thể bật lại để tune/debug sau này.
    [Tooltip("Nếu bật, Controller sẽ thêm một lớp kiểm tra khoảng cách ngoài Trigger.")]
    [SerializeField] private bool useMaxInitialDistanceValidation = false;

    // MỚI:
    // Stealth Takedown không yêu cầu Enemy phải đứng đúng một góc cố định trước
    // mặt Player. Player sẽ tự căn hướng về Enemy khi action bắt đầu.
    // Vì vậy mặc định tắt kiểm tra góc.
    [Tooltip("Nếu bật, Controller sẽ yêu cầu Enemy nằm trong Max Behind Angle trước khi click.")]
    [SerializeField] private bool useBehindAngleValidation = false;

    [Header("Audio")]
    [SerializeField] private AudioClip enemyTakedownVoiceClip;

    [Range(0f, 1f)]
    [SerializeField] private float enemyTakedownVoiceVolume = 1f;

    // CŨ:
    // [Header("Player Animation Hold")]
    // [Tooltip("Giữ Player ở frame cuối trong 5 giây chờ Enemy temporary-dead.")]
    // [SerializeField] private bool holdPlayerFinalFrame = true;
    //
    // MỚI:
    // Timeline movement của Player được điều khiển theo frame của animation
    // StealthTakeDown. Enemy mới là bên giữ frame cuối + chờ 5 giây.
    [Header("Player Animation Timeline")]
    [Min(1)]
    [SerializeField] private int playerAnimationTotalFrames = 210;

    [Min(1)]
    [SerializeField] private int playerAnimationFrameRate = 30;

    [Tooltip("Frame cuối của pha di chuyển Player từ vị trí A tới vị trí C - PlayerBehindEnemyDistance.")]
    [Min(1)]
    [SerializeField] private int playerApproachEndFrame = 39;

    [Tooltip("Frame bắt đầu tăng Y của Player.")]
    [Min(0)]
    [SerializeField] private int playerYJumpStartFrame = 40;

    [Tooltip("Số frame dùng để tăng Y từ Current Y lên Current Y + PlayerYJump.")]
    [Min(1)]
    [SerializeField] private int playerYJumpRiseFrames = 6;

    [Tooltip("Độ cao Y cộng thêm trong pha nhảy.")]
    [Min(0f)]
    [SerializeField] private float playerYJump = 0.3f;

    [Tooltip("Frame bắt đầu hạ Y bổ sung.")]
    [Min(0)]
    [SerializeField] private int playerYJumpLowerStartFrame = 100;

    [Tooltip("Số frame dùng để hạ Y từ Current Y + PlayerYJump về Current Y.")]
    [Min(1)]
    [SerializeField] private int playerYJumpLowerFrames = 6;

    [Tooltip("Frame bắt đầu lùi từ C về đúng vị trí A ban đầu.")]
    [Min(0)]
    [SerializeField] private int playerRetreatStartFrame = 155;

    // Chức năng mới:
    // Cấu hình Enemy Stealth Take Down nằm trên EnemyCheckpointHandler,
    // vì đó là script sở hữu lifecycle/animation/death của Enemy.
    // Player controller không giữ các biến Enemy này để tránh hai nơi cùng cấu hình một logic.

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    private PlayerController playerController;
    private PlayerState playerState;
    private PlayerProperties playerProperties;
    private Rigidbody playerRigidbody;
    private Animator playerAnimator;

    private bool takedownInProgress;
    private bool playerFailureInProgress;
    private EnemyCheckpointHandler activeTarget;

    private readonly HashSet<EnemyCheckpointHandler> candidates =
        new HashSet<EnemyCheckpointHandler>();

    private RigidbodyConstraints originalRigidbodyConstraints;
    private bool originalRigidbodyKinematic;
    private bool rigidbodyStateCaptured;
    private bool originalPlayerRootMotion;
    private bool rootMotionStateCaptured;

    private Vector3 originalPlayerPosition;
    private Vector3 playerApproachTargetPosition;
    private Vector3 takedownSharedForward;
    private float playerBaseY;

    private bool playerAnimationFinished;
    private bool playerTimelineInitialized;
    private bool playerCleanupCompleted;
    private int lastTimelinePhase = -1;

    // MỚI:
    // Theo dõi Button thực tế đã được bind listener nào, kể cả khi Button nằm
    // ở child của GameObject được kéo vào Inspector.
    private Button boundStealthTakedownButton;
    private bool buttonBindingWarningLogged;
    private bool lastLoggedButtonVisible;
    private bool lastLoggedButtonInteractable;
    private string lastLoggedButtonDiagnostic = string.Empty;

    private readonly HashSet<EnemyCheckpointHandler> combatCandidates =
        new HashSet<EnemyCheckpointHandler>();

    private bool punchInProgress;
    private Coroutine punchRoutine;
    private Coroutine playerEnemyHitRoutine;
    private bool playerFailureDuringMelee;
    private bool playerMeleeRigidbodyCaptured;
    private RigidbodyConstraints playerMeleeOriginalConstraints;
    private bool playerMeleeOriginalKinematic;
    private bool enemyHitRootMotionCaptured;
    private Animator enemyHitAnimator;
    private bool enemyHitOriginalRootMotion;
    private string lastLoggedPunchDiagnostic = string.Empty;
    private bool punchAudioWarningLogged;

    // Hướng combat được chốt một lần khi bắt đầu Punch Sequence.
    // Sau đó cả Player và Enemy cùng di chuyển theo đúng một hướng này;
    // không tính lại từ vị trí mới để tránh lật hướng giữa các hit.
    private Vector3 punchSharedDirection;

    public bool IsPlayerMeleeActionInProgress =>
        punchInProgress ||
        playerEnemyHitRoutine != null;

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
        playerState = GetComponent<PlayerState>();
        playerProperties = GetComponent<PlayerProperties>();
        playerRigidbody = GetComponent<Rigidbody>();
        playerAnimator = GetComponent<Animator>();

        if (punchAudioSource == null)
            punchAudioSource = GetComponent<AudioSource>();

        if (takedownTrigger == null)
            takedownTrigger = GetComponentInChildren<StealthTakedownTrigger>(true);

        // CŨ:
        // Tìm Button bằng helper runtime và tự chọn Button trong child hierarchy.
        // EnsureStealthTakedownButtonBinding(true);
        //
        // MỚI:
        // Khôi phục chính xác cơ chế binding của bản Controller đã hoạt động.
        // Button phải là component nằm trực tiếp trên GameObject được gán vào
        // stealthTakedownButton.
        if (stealthTakedownButton != null &&
            stealthTakedownButtonComponent == null)
        {
            stealthTakedownButtonComponent =
                stealthTakedownButton.GetComponent<Button>();
        }

        if (stealthTakedownButtonComponent != null)
        {
            stealthTakedownButtonComponent.onClick.RemoveListener(
                OnStealthTakedownButtonPressed
            );

            stealthTakedownButtonComponent.onClick.AddListener(
                OnStealthTakedownButtonPressed
            );

            // MỚI:
            // Lưu lại chính Button vừa bind để diagnostic không ảnh hưởng
            // tới cơ chế Button vốn đã hoạt động.
            boundStealthTakedownButton =
                stealthTakedownButtonComponent;

            // MỚI:
            // Nếu Button có StealthTakedownButtonRelay, giao owner cho relay để
            // diagnostic luôn biết chính xác Controller đang điều khiển Button.
            StealthTakedownButtonRelay relay =
                stealthTakedownButtonComponent.GetComponent<StealthTakedownButtonRelay>();

            if (relay == null && stealthTakedownButton != null)
            {
                relay = stealthTakedownButton.GetComponentInChildren<StealthTakedownButtonRelay>(true);
            }

            if (relay != null)
                relay.SetController(this);

            Debug.Log(
                "[StealthTakedown] BUTTON BOUND (working baseline): " +
                stealthTakedownButtonComponent.name +
                " | Interactable=" +
                stealthTakedownButtonComponent.interactable +
                " | Relay=" +
                (relay != null ? relay.name : "<NONE>"),
                this
            );
        }
        else
        {
            Debug.LogError(
                "[StealthTakedown] BUTTON BIND FAILED: " +
                "stealthTakedownButtonComponent = NULL. " +
                "Hãy gán đúng GameObject chứa Button vào stealthTakedownButton.",
                this
            );
        }

        BindPunchButton();

        SetButtonVisible(false);
        SetPunchButtonState(false, false);
    }

    private void OnEnable()
    {
        PlayerProperties.DeathOrFailureStarted -=
            HandlePlayerDeathOrFailureStarted;

        PlayerProperties.DeathOrFailureStarted +=
            HandlePlayerDeathOrFailureStarted;
    }

    private void OnDisable()
    {
        PlayerProperties.DeathOrFailureStarted -=
            HandlePlayerDeathOrFailureStarted;

        SetButtonVisible(false);
        SetPunchButtonState(false, false);

        StopPunchRoutine();
        StopPlayerEnemyHitRoutine();

        candidates.Clear();
        combatCandidates.Clear();
        activeTarget = null;
    }

// CŨ:     private void Update()
// CŨ:     {
// CŨ:         // Chức năng mới: sau khi một Failure/Death cũ đã restore xong, cho phép
// CŨ:         // một Stealth Take Down mới kết thúc bình thường và mở lại input.
// CŨ:         if (!takedownInProgress &&
// CŨ:             playerProperties != null &&
// CŨ:             !playerProperties.IsDeadOrFailing)
// CŨ:         {
// CŨ:             playerFailureInProgress = false;
// CŨ:         }
//
// CŨ:         if (takedownInProgress)
// CŨ:         {
// CŨ:             UpdatePlayerFinalPose();
// CŨ:             return;
// CŨ:         }
//
// CŨ:         RefreshActiveTarget();
// CŨ:     }
// MỚI:
private void Update()
{
    // Chức năng mới:
    // Trong Stealth Take Down, Player tự do về lại vị trí A tại frame 210.
    // Enemy vẫn có thể đang giữ pose + chờ Temporary Dead, nhưng việc đó không
    // được phép giữ Player ở trạng thái Exclusive nữa.
    if (!takedownInProgress &&
        playerProperties != null &&
        !playerProperties.IsDeadOrFailing)
    {
        playerFailureInProgress = false;
    }

    if (takedownInProgress)
    {
        // Chức năng mới:
        // Điều khiển timeline vị trí Player theo frame 0 -> 210.
        UpdatePlayerAnimationTimeline();
        return;
    }

    RefreshActiveTarget();
    RefreshPunchButtonState();
}

    // Chức năng mới:
    // Được StealthTakedownTrigger gọi khi Enemy đi vào vùng trước mặt Player.
    public void RegisterTarget(EnemyCheckpointHandler handler)
    {
        if (handler == null)
            return;

        bool added = candidates.Add(handler);

        if (logDebug && added)
        {
            Debug.Log(
                "[StealthTakedown] TARGET REGISTERED: " +
                handler.name +
                " | Candidates=" +
                candidates.Count,
                this
            );
        }
    }

    // Chức năng mới:
    // Được StealthTakedownTrigger gọi khi Enemy rời vùng trigger.
    public void UnregisterTarget(EnemyCheckpointHandler handler)
    {
        if (handler == null)
            return;

        bool removed = candidates.Remove(handler);

        if (logDebug && removed)
        {
            Debug.Log(
                "[StealthTakedown] TARGET UNREGISTERED: " +
                handler.name +
                " | Candidates=" +
                candidates.Count,
                this
            );
        }

        if (activeTarget == handler)
            activeTarget = null;
    }

    // Chức năng mới:
    // API public để Unity Button OnClick có thể gọi trực tiếp.
    public void OnStealthTakedownButtonPressed()
    {
        // MỚI:
        // Log luôn tại dòng đầu tiên của callback, không phụ thuộc logDebug.
        // Đây là mốc xác nhận chắc chắn Button Unity đã gọi tới script.
        Debug.Log(
            "[StealthTakedown] BUTTON CALLBACK RECEIVED | " +
            "Button=" +
            (stealthTakedownButtonComponent != null
                ? stealthTakedownButtonComponent.name
                : "<NULL>") +
            " | Interactable=" +
            (stealthTakedownButtonComponent != null
                ? stealthTakedownButtonComponent.interactable.ToString()
                : "<NULL>"),
            this
        );


        if (takedownInProgress)
        {
            if (logDebug)
            {
                Debug.LogWarning(
                    "[StealthTakedown] CLICK REJECTED: takedownInProgress = true.",
                    this
                );
            }

            return;
        }

        EnemyCheckpointHandler target =
            GetBestValidTarget(true);

        if (target == null)
        {
            if (logDebug)
            {
                Debug.LogWarning(
                    "[StealthTakedown] CLICK REJECTED: Không có Enemy hợp lệ. " +
                    "Xem các log validation ngay phía trên.",
                    this
                );
            }

            SetButtonVisible(false);
            return;
        }

        SetButtonVisible(false);
        activeTarget = target;

        if (logDebug)
        {
            Debug.Log(
                "[StealthTakedown] TARGET ACCEPTED: " +
                target.name +
                " -> Starting coroutine | PlayerPos=" + transform.position +
                " | EnemyPos=" + target.transform.position +
                " | EnemyY=" + target.transform.position.y,
                this
            );
        }

        StartCoroutine(
            PerformStealthTakedownRoutine(target)
        );
    }

    public void RegisterCombatTarget(
        EnemyCheckpointHandler handler)
    {
        if (handler == null)
            return;

        if (combatCandidates.Add(handler) &&
            logDebug)
        {
            Debug.Log(
                "[StealthTakedown] PUNCH TARGET REGISTERED: " +
                handler.name +
                " | Candidates=" +
                combatCandidates.Count,
                this
            );
        }
    }

    public void UnregisterCombatTarget(
        EnemyCheckpointHandler handler)
    {
        if (handler == null)
            return;

        if (combatCandidates.Remove(handler) &&
            logDebug)
        {
            Debug.Log(
                "[StealthTakedown] PUNCH TARGET UNREGISTERED: " +
                handler.name +
                " | Candidates=" +
                combatCandidates.Count,
                this
            );
        }
    }

    private void BindPunchButton()
    {
        if (punchButton != null &&
            punchButtonComponent == null)
        {
            punchButtonComponent =
                punchButton.GetComponent<Button>();
        }

        if (punchButtonComponent == null)
        {
            if (logDebug)
            {
                Debug.LogWarning(
                    "[StealthTakedown] PUNCH BUTTON NOT BOUND. " +
                    "Gán GameObject chứa Button vào Punch Button.",
                    this
                );
            }

            return;
        }

        punchButtonComponent.onClick.RemoveListener(
            OnPunchButtonPressed
        );

        punchButtonComponent.onClick.AddListener(
            OnPunchButtonPressed
        );
    }

    public void OnPunchButtonPressed()
    {
        Debug.Log(
            "[StealthTakedown] PUNCH BUTTON CALLBACK RECEIVED",
            this
        );

        TryPunch();
    }

    public bool TryPunch()
    {
        if (punchInProgress ||
            takedownInProgress ||
            playerFailureInProgress)
        {
            return false;
        }

        EnemyCheckpointHandler target =
            GetBestPunchTarget(true);

        if (target == null)
            return false;

        if (stealthTakedownButtonComponent != null)
            stealthTakedownButtonComponent.interactable = false;

        SetPunchButtonState(false, false);
        activeTarget = target;

        punchRoutine =
            StartCoroutine(
                PerformPunchSequence(target)
            );

        return true;
    }

    private void RefreshActiveTarget()
    {
        EnemyCheckpointHandler candidateInTrigger =
            GetClosestCandidateInTrigger();

        EnemyCheckpointHandler best =
            GetBestValidTarget();

        activeTarget = best != null
            ? best
            : candidateInTrigger;

        // Chức năng mới:
        // Button vẫn có thể hiện khi Enemy đang nằm trong trigger, nhưng khi
        // Vision chuyển YELLOW/RED hoặc target không hợp lệ thì Button bị disable.
        // Khi Vision trở lại WHITE + target hợp lệ, Button tự enable lại.
        SetButtonState(
            candidateInTrigger != null,
            best != null
        );

        LogButtonStateIfChanged(
            candidateInTrigger != null,
            best != null,
            BuildButtonDiagnostic(
                candidateInTrigger,
                best
            )
        );
    }

    private void PruneStaleTakedownCandidates()
    {
        if (candidates.Count == 0)
            return;

        if (takedownTrigger == null)
            takedownTrigger = GetComponentInChildren<StealthTakedownTrigger>(true);

        if (takedownTrigger == null)
            return;

        List<EnemyCheckpointHandler> staleTargets = null;

        foreach (EnemyCheckpointHandler candidate in candidates)
        {
            if (candidate == null ||
                !candidate.gameObject.activeInHierarchy ||
                !takedownTrigger.IsTargetCurrentlyInside(candidate))
            {
                if (staleTargets == null)
                    staleTargets = new List<EnemyCheckpointHandler>();

                if (candidate != null &&
                    !staleTargets.Contains(candidate))
                {
                    staleTargets.Add(candidate);
                }
            }
        }

        if (staleTargets == null)
            return;

        for (int i = 0; i < staleTargets.Count; i++)
        {
            EnemyCheckpointHandler stale = staleTargets[i];
            if (stale != null)
                candidates.Remove(stale);
        }

        if (staleTargets.Count > 0 && logDebug)
        {
            Debug.Log(
                "[StealthTakedown] STALE TAKEDOWN TARGETS PRUNED | Count=" +
                staleTargets.Count,
                this
            );
        }
    }

    private EnemyCheckpointHandler GetClosestCandidateInTrigger()
    {
        PruneStaleTakedownCandidates();

        EnemyCheckpointHandler best = null;
        float bestDistance = float.MaxValue;

        candidates.RemoveWhere(
            handler => handler == null ||
                       !handler.gameObject.activeInHierarchy
        );

        foreach (EnemyCheckpointHandler candidate in candidates)
        {
            if (candidate == null ||
                !candidate.gameObject.activeInHierarchy ||
                candidate.IsRuntimeDead ||
                candidate.IsPermanentlyDead ||
                candidate.IsStealthTakedownInProgress)
            {
                continue;
            }

            float distance =
                Vector3.Distance(
                    transform.position,
                    candidate.transform.position
                );

            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = candidate;
            }
        }

        return best;
    }

    private EnemyCheckpointHandler GetBestValidTarget(
        bool logValidation = false)
    {
        PruneStaleTakedownCandidates();

        if (playerProperties != null &&
            playerProperties.IsDeadOrFailing)
        {
            LogValidationFailure(
                logValidation,
                "PlayerProperties.IsDeadOrFailing = true."
            );

            return null;
        }

        if (playerState != null &&
            playerState.IsExclusiveActionState)
        {
            LogValidationFailure(
                logValidation,
                "PlayerState.IsExclusiveActionState = true. CurrentState=" +
                playerState.CurrentState
            );

            return null;
        }

        if (playerController != null &&
            !playerController.acceptInput)
        {
            LogValidationFailure(
                logValidation,
                "PlayerController.acceptInput = false."
            );

            return null;
        }

        if (logValidation)
        {
            Debug.Log(
                "[StealthTakedown] CLICK VALIDATION START | Candidates=" +
                candidates.Count,
                this
            );
        }

        EnemyCheckpointHandler best = null;
        float bestScore = float.MaxValue;

        candidates.RemoveWhere(
            handler => handler == null ||
                       !handler.gameObject.activeInHierarchy
        );

        foreach (EnemyCheckpointHandler candidate in candidates)
        {
            if (!IsValidTarget(
                    candidate,
                    logValidation
                ))
            {
                continue;
            }

            Vector3 flatOffset =
                candidate.transform.position -
                transform.position;

            flatOffset.y = 0f;

            if (flatOffset.sqrMagnitude < 0.0001f)
                continue;

            float distance =
                flatOffset.magnitude;

            float forwardPenalty =
                1f -
                Mathf.Clamp01(
                    Vector3.Dot(
                        transform.forward.normalized,
                        flatOffset.normalized
                    )
                );

            float score =
                distance +
                forwardPenalty;

            if (score < bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best;
    }

    private EnemyCheckpointHandler GetBestPunchTarget(
        bool logValidation = false)
    {
        if (playerProperties != null &&
            playerProperties.IsDeadOrFailing)
        {
            LogValidationFailure(
                logValidation,
                "Punch: PlayerProperties.IsDeadOrFailing = true."
            );

            return null;
        }

        if (playerState != null &&
            playerState.IsExclusiveActionState)
        {
            LogValidationFailure(
                logValidation,
                "Punch: PlayerState already owns an exclusive action."
            );

            return null;
        }

        if (playerController != null &&
            !playerController.acceptInput)
        {
            LogValidationFailure(
                logValidation,
                "Punch: PlayerController.acceptInput = false."
            );

            return null;
        }

        if (requireFistWeaponForPunch &&
            PlayerWeapon.Instance != null &&
            PlayerWeapon.Instance.CurrentWeapon !=
                PlayerWeapon.WeaponType.Fist)
        {
            LogValidationFailure(
                logValidation,
                "Punch: current weapon is not Fist."
            );

            return null;
        }

        EnemyCheckpointHandler best = null;
        float bestDistance = float.MaxValue;

        combatCandidates.RemoveWhere(
            handler =>
                handler == null ||
                !handler.gameObject.activeInHierarchy
        );

        foreach (EnemyCheckpointHandler candidate
                 in combatCandidates)
        {
            if (candidate == null ||
                candidate.IsRuntimeDead ||
                candidate.IsPermanentlyDead ||
                candidate.IsStealthTakedownInProgress)
            {
                continue;
            }

            EnemyMeleeCombatController enemyCombat =
                candidate.GetComponent<
                    EnemyMeleeCombatController
                >();

            if (enemyCombat == null ||
                !enemyCombat.CanReceivePlayerPunch)
            {
                continue;
            }

            EnemyRoutePerformer routePerformer =
                candidate.GetComponent<
                    EnemyRoutePerformer
                >();

            if (routePerformer != null &&
                routePerformer.IsAlarmConfigured)
            {
                continue;
            }

            GuardVisionView vision =
                candidate.GetComponent<
                    GuardVisionView
                >();

            if (vision == null)
            {
                vision =
                    candidate.GetComponentInChildren<
                        GuardVisionView
                    >(true);
            }

            if (vision == null ||
                !vision.CanBePlayerPunchTarget)
            {
                continue;
            }

            Vector3 flatOffset =
                candidate.transform.position -
                transform.position;

            flatOffset.y = 0f;

            float distance =
                flatOffset.magnitude;

            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = candidate;
            }
        }

        if (logValidation)
        {
            Debug.Log(
                "[StealthTakedown] PUNCH VALIDATION RESULT | Target=" +
                (best != null ? best.name : "<NONE>") +
                " | Candidates=" +
                combatCandidates.Count,
                this
            );
        }

        return best;
    }

    private bool IsValidTarget(
        EnemyCheckpointHandler target,
        bool logValidation)
    {
        if (target == null)
        {
            LogValidationFailure(
                logValidation,
                "Target = NULL."
            );

            return false;
        }

        if (!target.gameObject.activeInHierarchy)
        {
            LogValidationFailure(
                logValidation,
                target.name +
                " -> GameObject inactive."
            );

            return false;
        }

        if (target.IsRuntimeDead)
        {
            LogValidationFailure(
                logValidation,
                target.name +
                " -> IsRuntimeDead = true."
            );

            return false;
        }

        if (target.IsPermanentlyDead)
        {
            LogValidationFailure(
                logValidation,
                target.name +
                " -> IsPermanentlyDead = true."
            );

            return false;
        }

        if (target.IsStealthTakedownInProgress)
        {
            LogValidationFailure(
                logValidation,
                target.name +
                " -> IsStealthTakedownInProgress = true."
            );

            return false;
        }

        if (!target.CanBeginStealthTakedown())
        {
            LogValidationFailure(
                logValidation,
                target.name +
                " -> EnemyCheckpointHandler.CanBeginStealthTakedown() = false. " +
                "Kiểm tra Enemy Animator State 'GetStealthTakeDown'."
            );

            return false;
        }

        GuardVisionView vision =
            target.GetComponent<GuardVisionView>();

        if (vision == null)
        {
            vision =
                target.GetComponentInChildren<
                    GuardVisionView
                >(true);
        }

        if (vision == null)
        {
            LogValidationFailure(
                logValidation,
                target.name +
                " -> GuardVisionView không tồn tại."
            );

            return false;
        }

        EnemyRoutePerformer routePerformer =
            target.GetComponent<
                EnemyRoutePerformer
            >();

        if (routePerformer != null &&
            routePerformer.IsChasing)
        {
            LogValidationFailure(
                logValidation,
                target.name +
                " -> EnemyRoutePerformer.IsChasing = true."
            );

            return false;
        }

        if (!vision.CanBeStealthTakedownTarget)
        {
            LogValidationFailure(
                logValidation,
                target.name +
                " -> GuardVisionView.CanBeStealthTakedownTarget = false. " +
                "Vision chưa ở trạng thái WHITE/neutral hoặc đang bị khóa."
            );

            return false;
        }

        Vector3 toEnemy =
            target.transform.position -
            transform.position;

        toEnemy.y = 0f;

        float distance =
            toEnemy.magnitude;

        // CŨ:
        // if (distance > maxInitialDistance)
        // {
        //     LogValidationFailure(...);
        //     return false;
        // }
        //
        // MỚI:
        // Không dùng khoảng cách độc lập khi Trigger đã là range authority.
        // Chỉ bật lớp kiểm tra này khi thực sự cần tune/debug.
        if (useMaxInitialDistanceValidation &&
            distance > maxInitialDistance)
        {
            LogValidationFailure(
                logValidation,
                target.name +
                " -> Distance=" +
                distance.ToString("0.###") +
                " > MaxInitialDistance=" +
                maxInitialDistance.ToString("0.###") +
                "."
            );

            return false;
        }

        // CŨ:
        // float angle = Vector3.Angle(target.transform.forward, toEnemy.normalized);
        // if (angle > maxBehindAngle) return false;
        //
        // MỚI:
        // Không bắt Player phải ở đúng góc trước khi click. Trigger + Vision mới
        // là điều kiện gameplay chính; khi bắt đầu action, Player được căn hướng.
        if (useBehindAngleValidation &&
            toEnemy.sqrMagnitude > 0.0001f)
        {
            float angle =
                Vector3.Angle(
                    target.transform.forward,
                    toEnemy.normalized
                );

            if (angle > maxBehindAngle)
            {
                LogValidationFailure(
                    logValidation,
                    target.name +
                    " -> Angle=" +
                    angle.ToString("0.###") +
                    " > MaxBehindAngle=" +
                    maxBehindAngle.ToString("0.###") +
                    "."
                );

                return false;
            }
        }

        if (logValidation)
        {
            Debug.Log(
                "[StealthTakedown] VALIDATION PASSED: " +
                target.name,
                this
            );
        }

        return true;
    }

    // Chức năng mới:
    // In nguyên nhân target bị loại, nhưng chỉ khi đang debug một lần click.
    // Không spam Console trong Update bình thường.
    private void LogValidationFailure(
        bool enabled,
        string message)
    {
        if (!enabled || !logDebug)
            return;

        Debug.LogWarning(
            "[StealthTakedown] VALIDATION FAILED: " +
            message,
            this
        );
    }

    private void RefreshPunchButtonState()
    {
        if (punchButtonComponent == null &&
            punchButton == null)
        {
            return;
        }

        combatCandidates.RemoveWhere(
            handler =>
                handler == null ||
                !handler.gameObject.activeInHierarchy
        );

        EnemyCheckpointHandler candidate =
            GetClosestCombatCandidate();

        EnemyCheckpointHandler validTarget =
            GetBestPunchTarget();

        bool visible =
            candidate != null;

        bool interactable =
            validTarget != null &&
            !punchInProgress &&
            !takedownInProgress &&
            !playerFailureInProgress;

        SetPunchButtonState(
            visible,
            interactable
        );

        string diagnostic =
            BuildPunchButtonDiagnostic(
                candidate,
                validTarget
            );

        if (logDebug &&
            diagnostic != lastLoggedPunchDiagnostic)
        {
            lastLoggedPunchDiagnostic = diagnostic;

            Debug.Log(
                "[StealthTakedown] PUNCH BUTTON STATE | " +
                "Visible=" + visible +
                " | Interactable=" + interactable +
                " | " + diagnostic,
                this
            );
        }
    }

    private EnemyCheckpointHandler GetClosestCombatCandidate()
    {
        EnemyCheckpointHandler best = null;
        float bestDistance = float.MaxValue;

        foreach (EnemyCheckpointHandler candidate
                 in combatCandidates)
        {
            if (candidate == null ||
                !candidate.gameObject.activeInHierarchy ||
                candidate.IsRuntimeDead ||
                candidate.IsPermanentlyDead ||
                candidate.IsStealthTakedownInProgress)
            {
                continue;
            }

            float distance =
                Vector3.Distance(
                    transform.position,
                    candidate.transform.position
                );

            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = candidate;
            }
        }

        return best;
    }

    private string BuildPunchButtonDiagnostic(
        EnemyCheckpointHandler candidate,
        EnemyCheckpointHandler validTarget)
    {
        if (candidate == null)
            return "Không có Enemy trong PlayerAttackRangeController.";

        if (validTarget != null)
            return "Target hợp lệ = " + validTarget.name + ".";

        EnemyMeleeCombatController combat =
            candidate.GetComponent<
                EnemyMeleeCombatController
            >();

        if (combat == null)
            return "Candidate thiếu EnemyMeleeCombatController.";

        if (combat.IsAttackInProgress)
            return "Enemy đang Attack.";

        GuardVisionView vision =
            candidate.GetComponent<
                GuardVisionView
            >();

        if (vision == null)
        {
            vision =
                candidate.GetComponentInChildren<
                    GuardVisionView
                >(true);
        }

        if (vision == null)
            return "Candidate thiếu GuardVisionView.";

        if (vision.IsWhite)
            return "Vision WHITE -> Stealth Take Down ưu tiên.";

        if (!vision.CanBePlayerPunchTarget)
            return "Vision chưa ở YELLOW/RED hoặc đang bị lock.";

        EnemyRoutePerformer route =
            candidate.GetComponent<
                EnemyRoutePerformer
            >();

        if (route != null &&
            route.IsAlarmConfigured)
        {
            return "Enemy Alarm configured -> Punch disabled.";
        }

        return "Candidate tồn tại nhưng Punch validation chưa pass.";
    }

    private IEnumerator PerformPunchSequence(
        EnemyCheckpointHandler target)
    {
        punchInProgress = true;
        playerFailureDuringMelee = false;

        bool success = false;

        EnemyMeleeCombatController enemyCombat =
            target != null
                ? target.GetComponent<
                    EnemyMeleeCombatController
                >()
                : null;

        EnemyProperties targetProperties =
            target != null
                ? target.GetComponent<
                    EnemyProperties
                >()
                : null;

        try
        {
            if (target == null ||
                enemyCombat == null ||
                targetProperties == null ||
                !enemyCombat.CanReceivePlayerPunch)
            {
                Debug.LogWarning(
                    "[StealthTakedown] PUNCH ABORTED: target invalid.",
                    this
                );

                yield break;
            }

            CapturePlayerMeleeRigidbody();
            LockPlayerForMelee();
            enemyCombat.BeginPlayerPunchSequenceLock();

            int startingEnemyHealth =
                targetProperties.CurrentHealth;

            int firstTwoHitDamage =
                Mathf.FloorToInt(
                    startingEnemyHealth / 3f
                );

            if (firstTwoHitDamage <= 0 &&
                startingEnemyHealth > 0)
            {
                firstTwoHitDamage = 1;
            }

            if (logDebug)
            {
                Debug.Log(
                    "[StealthTakedown] PUNCH SEQUENCE START | " +
                    "Enemy=" + target.name +
                    " | StartingHP=" + startingEnemyHealth +
                    " | Hit1/2Damage=" + firstTwoHitDamage +
                    " | PairDistance=" + punchPairDistance,
                    this
                );
            }

            string[] attackStates =
            {
                punchAttack1AnimatorName,
                punchAttack1AnimatorName,
                punchAttack2AnimatorName
            };

            AudioClip[] punchClips =
            {
                punchSoundClip1,
                punchSoundClip2,
                punchSoundClip3
            };

            for (int hitIndex = 0;
                 hitIndex < 3;
                 hitIndex++)
            {
                if (playerFailureDuringMelee ||
                    target == null ||
                    !target.gameObject.activeInHierarchy ||
                    target.IsRuntimeDead)
                {
                    Debug.LogWarning(
                        "[StealthTakedown] PUNCH SEQUENCE INTERRUPTED at hit " +
                        (hitIndex + 1),
                        this
                    );

                    yield break;
                }

                // Chốt vị trí và hướng đúng một lần ở hit đầu tiên.
                // Các hit sau KHÔNG được căn lại từ position hiện tại, vì làm vậy
                // có thể lật hướng Player/Enemy sau mỗi lần knockback.
                if (hitIndex == 0)
                {
                    if (!AlignPlayerAndEnemyForPunch(
                            target.transform
                        ))
                    {
                        Debug.LogError(
                            "[StealthTakedown] PUNCH ALIGNMENT FAILED | Hit=1",
                            this
                        );

                        yield break;
                    }

                    if (logDebug)
                    {
                        Debug.Log(
                            "[StealthTakedown] PUNCH PAIR LOCKED | " +
                            "Direction=" + punchSharedDirection +
                            " | PlayerPos=" + transform.position +
                            " | EnemyPos=" + target.transform.position +
                            " | Distance=" + Vector3.Distance(transform.position, target.transform.position).ToString("0.###"),
                            this
                        );
                    }
                }

                string stateName =
                    attackStates[hitIndex];

                if (!playerState.BeginMeleeCombatAttack(
                        stateName,
                        punchPlaybackSpeed
                    ))
                {
                    Debug.LogError(
                        "[StealthTakedown] PLAYER PUNCH ANIMATION FAILED | Hit=" +
                        (hitIndex + 1),
                        this
                    );

                    yield break;
                }

                PlayPunchSound(
                    punchClips[hitIndex]
                );

                float attackDuration =
                    GetPlayerAnimatorStateDuration(
                        stateName,
                        punchPlaybackSpeed
                    );

                if (attackDuration <= 0f)
                {
                    Debug.LogError(
                        "[StealthTakedown] PUNCH DURATION FAILED | State=" +
                        stateName,
                        this
                    );

                    yield break;
                }

                float hitDelay =
                    attackDuration *
                    Mathf.Clamp01(
                        punchHitNormalizedTime
                    );

                yield return new WaitForSeconds(
                    hitDelay
                );

                if (playerFailureDuringMelee ||
                    enemyCombat == null)
                {
                    yield break;
                }

                int damage =
                    hitIndex < 2
                        ? firstTwoHitDamage
                        : int.MaxValue;

                if (!enemyCombat.ReceivePlayerPunchHit(
                        hitIndex,
                        damage
                    ))
                {
                    Debug.LogError(
                        "[StealthTakedown] ENEMY REJECTED PLAYER PUNCH HIT | Hit=" +
                        (hitIndex + 1),
                        this
                    );

                    yield break;
                }

                // Cả Player và Enemy cùng tiến/lùi theo cùng một hướng.
                // Khoảng cách giữa hai nhân vật được giữ nguyên trong suốt action.
                StartCoroutine(
                    AdvancePunchPairRoutine(
                        target.transform,
                        punchSharedDirection,
                        punchKnockbackDistance,
                        punchKnockbackDuration
                    )
                );

                float remaining =
                    Mathf.Max(
                        0f,
                        attackDuration - hitDelay
                    );

                yield return new WaitForSeconds(
                    remaining
                );
            }

            success = true;

            if (logDebug)
            {
                Debug.Log(
                    "[StealthTakedown] PUNCH SEQUENCE COMPLETE | Enemy=" +
                    target.name,
                    this
                );
            }
        }
        finally
        {
            if (enemyCombat != null)
            {
                enemyCombat.EndPlayerPunchSequenceLock(
                    success
                );
            }

            FinishPlayerPunchState();
            punchRoutine = null;
        }
    }

    public void BeginEnemyAttackHitPresentation(
        EnemyMeleeCombatController attacker,
        int damage,
        float pairDistance,
        float knockbackDistance,
        float knockbackDuration,
        float hitHoldDuration)
    {
        if (attacker == null ||
            playerProperties == null ||
            playerProperties.IsDeadOrFailing)
        {
            return;
        }

        StopPlayerEnemyHitRoutine();

        playerEnemyHitRoutine =
            StartCoroutine(
                EnemyAttackHitPresentationRoutine(
                    attacker,
                    damage,
                    pairDistance,
                    knockbackDistance,
                    knockbackDuration,
                    hitHoldDuration > 0f
                        ? hitHoldDuration
                        : enemyHitFallbackDuration
                )
            );
    }

    private IEnumerator EnemyAttackHitPresentationRoutine(
        EnemyMeleeCombatController attacker,
        int damage,
        float pairDistance,
        float knockbackDistance,
        float knockbackDuration,
        float hitHoldDuration)
    {
        CapturePlayerMeleeRigidbody();
        LockPlayerForMelee();
        CaptureAndDisableEnemyHitRootMotion(attacker);

        if (!AlignPlayerAndEnemyForEnemyAttack(
                attacker.transform,
                pairDistance
            ))
        {
            Debug.LogError(
                "[StealthTakedown] ENEMY ATTACK ALIGNMENT FAILED | Attacker=" +
                attacker.name,
                this
            );

            FinishPlayerEnemyHitState();
            playerEnemyHitRoutine = null;
            yield break;
        }

        if (!playerState.BeginMeleeCombatHit(
                punchGetHitAnimatorName,
                punchPlaybackSpeed
            ))
        {
            Debug.LogError(
                "[StealthTakedown] PLAYER GETHIT ANIMATION FAILED.",
                this
            );

            FinishPlayerEnemyHitState();
            playerEnemyHitRoutine = null;
            yield break;
        }

        playerProperties.TakeDamage(
            damage
        );

        if (logDebug)
        {
            Debug.Log(
                "[StealthTakedown] PLAYER HIT BY ENEMY | " +
                "Attacker=" + attacker.name +
                " | Damage=" + damage,
                this
            );
        }

        StartCoroutine(
            ApplyPlayerKnockbackRoutine(
                attacker.transform,
                knockbackDistance,
                knockbackDuration
            )
        );

        yield return new WaitForSeconds(
            Mathf.Max(
                0.01f,
                hitHoldDuration
            )
        );

        FinishPlayerEnemyHitState();
        playerEnemyHitRoutine = null;
    }

    private bool AlignPlayerAndEnemyForPunch(
        Transform enemy)
    {
        if (enemy == null)
            return false;

        Vector3 playerToEnemy =
            enemy.position -
            transform.position;

        playerToEnemy.y = 0f;

        if (playerToEnemy.sqrMagnitude <= 0.0001f)
            return false;

        playerToEnemy.Normalize();

        punchSharedDirection =
            playerToEnemy;

        Vector3 playerPosition =
            enemy.position -
            playerToEnemy *
            punchPairDistance;

        playerPosition.y =
            transform.position.y;

        transform.position =
            playerPosition;

        transform.rotation =
            Quaternion.LookRotation(
                playerToEnemy,
                Vector3.up
            );

        enemy.rotation =
            Quaternion.LookRotation(
                -playerToEnemy,
                Vector3.up
            );

        SyncPlayerRigidbody();
        Physics.SyncTransforms();

        return true;
    }

    private bool AlignPlayerAndEnemyForEnemyAttack(
        Transform enemy,
        float pairDistance)
    {
        if (enemy == null)
            return false;

        Vector3 enemyToPlayer =
            transform.position -
            enemy.position;

        enemyToPlayer.y = 0f;

        if (enemyToPlayer.sqrMagnitude <= 0.0001f)
            return false;

        enemyToPlayer.Normalize();

        // Enemy attack must NEVER relocate the Player during the initial catch.
        // The Player remains exactly where the chase trigger caught them.
        Vector3 playerPosition =
            transform.position;

        Vector3 enemyPosition =
            playerPosition -
            enemyToPlayer *
            Mathf.Max(0.01f, pairDistance);

        enemyPosition.y =
            enemy.position.y;

        enemy.position =
            enemyPosition;

        enemy.rotation =
            Quaternion.LookRotation(
                enemyToPlayer,
                Vector3.up
            );

        // Player turns to face Enemy, but position stays untouched.
        transform.rotation =
            Quaternion.LookRotation(
                -enemyToPlayer,
                Vector3.up
            );

        SyncPlayerRigidbody();
        Physics.SyncTransforms();

        return true;
    }

    private float GetPlayerAnimatorStateDuration(
        string stateName,
        float playbackSpeed)
    {
        if (playerAnimator == null ||
            !playerAnimator.enabled)
        {
            return 0f;
        }

        AnimatorStateInfo info =
            playerAnimator.GetCurrentAnimatorStateInfo(0);

        if (!info.IsName(stateName) &&
            info.shortNameHash !=
                Animator.StringToHash(stateName))
        {
            return 0f;
        }

        return info.length /
               Mathf.Max(
                   0.01f,
                   playbackSpeed
               );
    }

    private void PlayPunchSound(
        AudioClip clip)
    {
        if (clip == null)
            return;

        if (punchAudioSource == null)
        {
            if (!punchAudioWarningLogged)
            {
                punchAudioWarningLogged = true;

                Debug.LogWarning(
                    "[StealthTakedown] PUNCH AUDIO SOURCE NULL | " +
                    "Gán AudioSource cho Player hoặc để script tự lấy AudioSource trên chính Player.",
                    this
                );
            }

            return;
        }

        punchAudioSource.PlayOneShot(
            clip,
            punchSoundVolume
        );
    }

    private IEnumerator AdvancePunchPairRoutine(
        Transform enemy,
        Vector3 direction,
        float distance,
        float duration)
    {
        if (enemy == null ||
            distance <= 0f)
        {
            yield break;
        }

        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
        {
            Debug.LogWarning(
                "[StealthTakedown] PUNCH PAIR ADVANCE REJECTED: shared direction invalid.",
                this
            );
            yield break;
        }

        direction.Normalize();

        Vector3 playerStart =
            transform.position;

        Vector3 enemyStart =
            enemy.position;

        Vector3 delta =
            direction * distance;

        Vector3 playerEnd =
            playerStart + delta;

        Vector3 enemyEnd =
            enemyStart + delta;

        Rigidbody enemyRigidbody =
            enemy.GetComponent<Rigidbody>();

        float elapsed = 0f;
        float safeDuration =
            Mathf.Max(
                0.01f,
                duration
            );

        while (elapsed < safeDuration)
        {
            if (enemy == null)
                yield break;

            float t =
                Mathf.Clamp01(
                    elapsed / safeDuration
                );

            t = Mathf.SmoothStep(0f, 1f, t);

            transform.position =
                Vector3.Lerp(
                    playerStart,
                    playerEnd,
                    t
                );

            enemy.position =
                Vector3.Lerp(
                    enemyStart,
                    enemyEnd,
                    t
                );

            SyncPlayerRigidbody();

            if (enemyRigidbody != null)
                enemyRigidbody.position =
                    enemy.position;

            // Không đụng rotation ở đây. Hướng đã được chốt từ đầu sequence.
            Physics.SyncTransforms();

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (enemy != null)
        {
            transform.position =
                playerEnd;

            enemy.position =
                enemyEnd;

            SyncPlayerRigidbody();

            if (enemyRigidbody != null)
                enemyRigidbody.position =
                    enemyEnd;

            Physics.SyncTransforms();
        }
    }

    /// <summary>
    /// Đẩy Player lùi ra khỏi Enemy sau khi Enemy đánh trúng.
    /// Chỉ tác động X/Z, không đổi hướng mặt. Player đang được khóa Rigidbody
    /// trong suốt enemy-hit presentation nên Transform được điều khiển trực tiếp
    /// và đồng bộ lại Rigidbody/Physics ở mỗi frame.
    /// </summary>
    private IEnumerator ApplyPlayerKnockbackRoutine(
        Transform attacker,
        float distance,
        float duration)
    {
        if (attacker == null ||
            playerProperties == null ||
            playerProperties.IsDeadOrFailing)
        {
            yield break;
        }

        float safeDistance =
            Mathf.Max(
                0f,
                distance
            );

        if (safeDistance <= 0f)
        {
            yield break;
        }

        Vector3 knockbackDirection =
            transform.position - attacker.position;

        // Combat của game chỉ dùng X/Z.
        knockbackDirection.y = 0f;

        if (knockbackDirection.sqrMagnitude <= 0.0001f)
        {
            // Fallback an toàn: dùng hướng Player hiện tại nếu hai pivot
            // đang trùng nhau do một frame alignment bất thường.
            knockbackDirection = transform.forward;
            knockbackDirection.y = 0f;
        }

        if (knockbackDirection.sqrMagnitude <= 0.0001f)
        {
            Debug.LogWarning(
                "[StealthTakedown] PLAYER KNOCKBACK REJECTED | " +
                "Không xác định được hướng lùi.",
                this
            );

            yield break;
        }

        knockbackDirection.Normalize();

        Vector3 startPosition =
            transform.position;

        Vector3 endPosition =
            startPosition +
            knockbackDirection *
            safeDistance;

        float safeDuration =
            Mathf.Max(
                0.01f,
                duration
            );

        float elapsed = 0f;

        if (logDebug)
        {
            Debug.Log(
                "[StealthTakedown] PLAYER KNOCKBACK START | " +
                "Distance=" + safeDistance +
                " | Duration=" + safeDuration +
                " | Direction=" + knockbackDirection,
                this
            );
        }

        while (elapsed < safeDuration)
        {
            if (playerFailureDuringMelee ||
                playerProperties.IsDeadOrFailing)
            {
                yield break;
            }

            float t =
                Mathf.Clamp01(
                    elapsed / safeDuration
                );

            t =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    t
                );

            Vector3 nextPosition =
                Vector3.Lerp(
                    startPosition,
                    endPosition,
                    t
                );

            // Giữ nguyên Y.
            nextPosition.y =
                startPosition.y;

            transform.position =
                nextPosition;

            // Không quay Player trong lúc bị hit. Hướng mặt phải giữ nguyên
            // theo pair alignment của Enemy Attack.
            SyncPlayerRigidbody();
            Physics.SyncTransforms();

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (playerFailureDuringMelee ||
            playerProperties.IsDeadOrFailing)
        {
            yield break;
        }

        transform.position =
            endPosition;

        SyncPlayerRigidbody();
        Physics.SyncTransforms();

        if (logDebug)
        {
            Debug.Log(
                "[StealthTakedown] PLAYER KNOCKBACK COMPLETE | " +
                "FinalPosition=" + transform.position,
                this
            );
        }
    }

    private void CapturePlayerMeleeRigidbody()
    {
        if (playerRigidbody == null ||
            playerMeleeRigidbodyCaptured)
        {
            return;
        }

        playerMeleeOriginalConstraints =
            playerRigidbody.constraints;

        playerMeleeOriginalKinematic =
            playerRigidbody.isKinematic;

        playerMeleeRigidbodyCaptured = true;
    }

    private void LockPlayerForMelee()
    {
        if (playerController != null)
            playerController.SetExternalActionLock(true);

        if (playerRigidbody != null)
        {
            playerRigidbody.linearVelocity =
                Vector3.zero;

            playerRigidbody.angularVelocity =
                Vector3.zero;

            playerRigidbody.isKinematic = true;

            playerRigidbody.constraints =
                RigidbodyConstraints.FreezeAll;
        }
    }

    private void SetPlayerMeleePosition(
        Vector3 position)
    {
        transform.position =
            position;

        SyncPlayerRigidbody();
        Physics.SyncTransforms();
    }

    private void SyncPlayerRigidbody()
    {
        if (playerRigidbody == null)
            return;

        playerRigidbody.position =
            transform.position;

        playerRigidbody.rotation =
            transform.rotation;
    }

    private void FinishPlayerPunchState()
    {
        if (playerState != null &&
            (playerState.CurrentState ==
                PlayerState.State.MeleeCombatAttack ||
             playerState.CurrentState ==
                PlayerState.State.MeleeCombatHit))
        {
            playerState.EndMeleeCombatAction();
        }

        RestorePlayerMeleeRigidbody();

        if (playerController != null &&
            !playerFailureDuringMelee &&
            (playerProperties == null ||
             !playerProperties.IsDeadOrFailing))
        {
            playerController.SetExternalActionLock(false);
        }

        punchInProgress = false;
    }

    private void CaptureAndDisableEnemyHitRootMotion(
        EnemyMeleeCombatController attacker)
    {
        enemyHitRootMotionCaptured = false;
        enemyHitAnimator = null;

        if (attacker == null)
            return;

        enemyHitAnimator =
            attacker.GetComponent<Animator>();

        if (enemyHitAnimator == null)
            return;

        enemyHitOriginalRootMotion =
            enemyHitAnimator.applyRootMotion;

        enemyHitAnimator.applyRootMotion = false;
        enemyHitRootMotionCaptured = true;
    }

    private void RestoreEnemyHitRootMotion()
    {
        if (!enemyHitRootMotionCaptured)
            return;

        if (enemyHitAnimator != null)
        {
            enemyHitAnimator.applyRootMotion =
                enemyHitOriginalRootMotion;
        }

        enemyHitAnimator = null;
        enemyHitRootMotionCaptured = false;
    }

    private void FinishPlayerEnemyHitState()
    {
        if (playerState != null &&
            playerState.CurrentState ==
                PlayerState.State.MeleeCombatHit)
        {
            playerState.EndMeleeCombatAction();
        }

        RestorePlayerMeleeRigidbody();
        RestoreEnemyHitRootMotion();

        if (playerController != null &&
            !playerFailureDuringMelee &&
            playerProperties != null &&
            !playerProperties.IsDeadOrFailing)
        {
            playerController.SetExternalActionLock(false);
        }
    }

    private void RestorePlayerMeleeRigidbody()
    {
        if (playerRigidbody == null ||
            !playerMeleeRigidbodyCaptured)
        {
            return;
        }

        playerRigidbody.constraints =
            playerMeleeOriginalConstraints;

        playerRigidbody.isKinematic =
            playerMeleeOriginalKinematic;

        if (!playerRigidbody.isKinematic)
        {
            playerRigidbody.linearVelocity =
                Vector3.zero;

            playerRigidbody.angularVelocity =
                Vector3.zero;
        }

        playerRigidbody.position =
            transform.position;

        playerMeleeRigidbodyCaptured = false;
        Physics.SyncTransforms();
    }

    private void StopPunchRoutine()
    {
        if (punchRoutine != null)
        {
            StopCoroutine(
                punchRoutine
            );

            punchRoutine = null;
        }

        if (playerState != null &&
            (playerState.CurrentState ==
                PlayerState.State.MeleeCombatAttack ||
             playerState.CurrentState ==
                PlayerState.State.MeleeCombatHit))
        {
            playerState.EndMeleeCombatAction();
        }

        RestorePlayerMeleeRigidbody();
        punchInProgress = false;
    }

    private void StopPlayerEnemyHitRoutine()
    {
        if (playerEnemyHitRoutine != null)
        {
            StopCoroutine(
                playerEnemyHitRoutine
            );

            playerEnemyHitRoutine = null;
        }

        FinishPlayerEnemyHitState();
        RestoreEnemyHitRootMotion();
    }

    private void SetPunchButtonState(
        bool visible,
        bool interactable)
    {
        if (punchButton != null)
            punchButton.SetActive(visible);

        if (punchButtonComponent != null)
        {
            punchButtonComponent.interactable =
                visible &&
                interactable;
        }
    }

// CŨ:     private IEnumerator PerformStealthTakedownRoutine(
// CŨ:         EnemyCheckpointHandler target)
// CŨ:     {
// CŨ:         takedownInProgress = true;
// CŨ:         playerFailureInProgress = false;
// CŨ:         bool success = false;
// CŨ:         try
// CŨ:         {
// CŨ:             if (!IsValidTarget(target))
// CŨ:                 yield break;
// CŨ:             PreparePlayerForTakedown();
// CŨ:             AlignCharactersForTakedown(target);
// CŨ:             bool playerAnimationStarted =
// CŨ:                 playerState != null &&
// CŨ:                 playerState.BeginStealthTakeDown();
// CŨ:             if (!playerAnimationStarted)
// CŨ:                 yield break;
// CŨ:             if (!target.BeginRuntimeDeath(true))
// CŨ:                 yield break;
// CŨ:             PlayEnemyTakedownVoice(target);
// CŨ:             success = true;
// CŨ:             while (target != null &&
// CŨ:                    target.IsStealthTakedownInProgress)
// CŨ:             {
// CŨ:                 UpdatePlayerFinalPose();
// CŨ:                 yield return null;
// CŨ:             }
// CŨ:             if (target == null)
// CŨ:                 yield break;
// CŨ:         }
// CŨ:         finally
// CŨ:         {
// CŨ:             if (!success)
// CŨ:             {
// CŨ:                 if (target != null &&
// CŨ:                     target.IsStealthTakedownInProgress)
// CŨ:                     target.CancelStealthTakedownPresentation();
// CŨ:                 if (playerState != null)
// CŨ:                     playerState.EndStealthTakeDown();
// CŨ:             }
// CŨ:             FinishPlayerTakedownState();
// CŨ:         }
// CŨ:     }
// MỚI:
private IEnumerator PerformStealthTakedownRoutine(
    EnemyCheckpointHandler target)
{
    takedownInProgress = true;
    playerFailureInProgress = false;
    playerAnimationFinished = false;
    playerTimelineInitialized = false;
    playerCleanupCompleted = false;
    lastTimelinePhase = -1;

    bool success = false;

    try
    {
        if (!IsValidTarget(
                target,
                true
            ))
        {
            yield break;
        }

        PreparePlayerForTakedown();

        if (logDebug)
        {
            Debug.Log(
                "[StealthTakedown] STAGE 1 PREPARED | Player locked, Rigidbody captured, RootMotion handled.",
                this
            );
        }

        // MỚI:
        // Lưu vị trí A của Player trước khi bắt đầu action.
        // Toàn bộ timeline 0 -> 210 sẽ dùng A làm điểm đầu và điểm cuối.
        originalPlayerPosition =
            transform.position;

        playerBaseY =
            originalPlayerPosition.y;

        AlignCharactersForTakedown(
            target
        );

        if (logDebug)
        {
            Debug.Log(
                "[StealthTakedown] STAGE 2 ALIGNED | A=" + originalPlayerPosition +
                " | C=" + playerApproachTargetPosition +
                " | Forward=" + takedownSharedForward,
                this
            );
        }

        if (logDebug)
        {
            float configuredDuration =
                playerAnimationTotalFrames /
                Mathf.Max(
                    1f,
                    playerAnimationFrameRate
                );

            Debug.Log(
                "[StealthTakedown] Player timeline ready: " +
                playerAnimationTotalFrames +
                " frames @ " +
                playerAnimationFrameRate +
                " FPS (" +
                configuredDuration.ToString(
                    "0.###"
                ) +
                "s).",
                this
            );
        }

        // Chức năng mới:
        // Player animation được phát từ frame 0.
        // PlayerState là owner của animation state Player.
        bool playerAnimationStarted =
            playerState != null &&
            playerState.BeginStealthTakeDown();

        if (!playerAnimationStarted)
        {
            if (logDebug)
            {
                Debug.LogError(
                    "[StealthTakedown] PLAYER ANIMATION START FAILED.",
                    this
                );
            }

            yield break;
        }

        // Chức năng mới:
        // Enemy animation được bắt đầu ngay sau Player, cùng ở frame 0.
        // EnemyCheckpointHandler là owner của Enemy death/stealth presentation.
        if (!target.BeginRuntimeDeath(true))
        {
            if (logDebug)
            {
                Debug.LogError(
                    "[StealthTakedown] ENEMY ANIMATION START FAILED: BeginRuntimeDeath(false).",
                    this
                );
            }

            yield break;
        }

        PlayEnemyTakedownVoice(
            target
        );

        success = true;

        // Chức năng mới:
        // Player timeline phải chạy độc lập tới frame 210.
        // Không chờ Enemy giữ frame cuối 5 giây.
        while (!playerAnimationFinished &&
               !playerFailureInProgress)
        {
            UpdatePlayerAnimationTimeline();

            yield return null;
        }

        // Chức năng mới:
        // Nếu Player đã đạt frame 210 thì Player đã được trả về Idle + unlock.
        // Coroutine chỉ tiếp tục chờ phần Enemy còn lại.
        while (target != null &&
               target.IsStealthTakedownInProgress)
        {
            yield return null;
        }
    }
    finally
    {
        if (!success)
        {
            if (target != null &&
                target.IsStealthTakedownInProgress)
            {
                target.CancelStealthTakedownPresentation();
            }
        }

        // Chức năng mới:
        // Nếu action thất bại trước frame 210, vẫn phải dọn Player.
        // Nếu Player đã về A và hoàn tất rồi thì không cleanup lần hai.
        if (!playerCleanupCompleted)
        {
            FinishPlayerTakedownState();
        }
    }
}

    private void PreparePlayerForTakedown()
    {
        if (playerController != null)
            playerController.SetExternalActionLock(true);

        if (playerRigidbody != null)
        {
            originalRigidbodyConstraints =
                playerRigidbody.constraints;

            originalRigidbodyKinematic =
                playerRigidbody.isKinematic;

            rigidbodyStateCaptured = true;

            playerRigidbody.linearVelocity =
                Vector3.zero;

            playerRigidbody.angularVelocity =
                Vector3.zero;

            if (freezePlayerRigidbodyDuringTakedown)
            {
                playerRigidbody.isKinematic =
                    true;

                playerRigidbody.constraints =
                    RigidbodyConstraints.FreezeAll;
            }
        }

        if (playerAnimator != null &&
            disablePlayerRootMotion)
        {
            originalPlayerRootMotion =
                playerAnimator.applyRootMotion;

            rootMotionStateCaptured =
                true;

            playerAnimator.applyRootMotion =
                false;
        }
    }

// CŨ:     private void AlignCharactersForTakedown(
// CŨ:         EnemyCheckpointHandler target)
// CŨ:     {
// CŨ:         Transform enemy = target.transform;
// CŨ:
// CŨ:         Vector3 sharedForward =
// CŨ:             enemy.forward;
// CŨ:
// CŨ:         sharedForward.y = 0f;
// CŨ:
// CŨ:         if (sharedForward.sqrMagnitude < 0.0001f)
// CŨ:         {
// CŨ:             sharedForward = transform.forward;
// CŨ:             sharedForward.y = 0f;
// CŨ:         }
// CŨ:
// CŨ:         sharedForward.Normalize();
// CŨ:
// CŨ:         Vector3 targetPlayerPosition =
// CŨ:             enemy.position -
// CŨ:             sharedForward * playerBehindEnemyDistance;
// CŨ:
// CŨ:         if (keepPlayerY)
// CŨ:             targetPlayerPosition.y = transform.position.y;
// CŨ:         else
// CŨ:             targetPlayerPosition.y = enemy.position.y;
// CŨ:
// CŨ:         Quaternion sharedRotation =
// CŨ:             Quaternion.LookRotation(
// CŨ:                 sharedForward,
// CŨ:                 Vector3.up
// CŨ:             );
// CŨ:
// CŨ:         enemy.SetPositionAndRotation(
// CŨ:             enemy.position,
// CŨ:             sharedRotation
// CŨ:         );
// CŨ:
// CŨ:         transform.SetPositionAndRotation(
// CŨ:             targetPlayerPosition,
// CŨ:             sharedRotation
// CŨ:         );
// CŨ:
// CŨ:         if (playerRigidbody != null)
// CŨ:         {
// CŨ:             playerRigidbody.position = targetPlayerPosition;
// CŨ:             playerRigidbody.rotation = sharedRotation;
// CŨ:             Physics.SyncTransforms();
// CŨ:         }
// CŨ:     }
// MỚI:
private void AlignCharactersForTakedown(
    EnemyCheckpointHandler target)
{
    Transform enemy =
        target.transform;

    Vector3 sharedForward =
        enemy.forward;

    sharedForward.y = 0f;

    if (sharedForward.sqrMagnitude < 0.0001f)
    {
        sharedForward =
            transform.forward;

        sharedForward.y = 0f;
    }

    sharedForward.Normalize();

    takedownSharedForward =
        sharedForward;

    // MỚI:
    // Tính đúng vị trí C của Player theo Enemy forward.
    // Đây là vị trí mà Player sẽ đi tới trong frame 0 -> 39.
    playerApproachTargetPosition =
        enemy.position -
        sharedForward *
        playerBehindEnemyDistance;

    playerApproachTargetPosition.y =
        playerBaseY;

    Quaternion sharedRotation =
        Quaternion.LookRotation(
            sharedForward,
            Vector3.up
        );

    // CŨ:
    // enemy.SetPositionAndRotation(
    //     enemy.position,
    //     sharedRotation
    // );
    //
    // transform.SetPositionAndRotation(
    //     targetPlayerPosition,
    //     sharedRotation
    // );
    //
    // if (playerRigidbody != null)
    // {
    //     playerRigidbody.position = targetPlayerPosition;
    //     playerRigidbody.rotation = sharedRotation;
    //     Physics.SyncTransforms();
    // }
    //
    // MỚI:
    // Chỉ đồng bộ hướng ở thời điểm bắt đầu.
    // Không snap Player tới C nữa. Player phải thực sự di chuyển
    // từ A -> C theo timeline frame 0 -> 39.
    enemy.rotation =
        sharedRotation;

    transform.rotation =
        sharedRotation;

    if (playerRigidbody != null)
    {
        playerRigidbody.rotation =
            sharedRotation;

        Physics.SyncTransforms();
    }
}

// CŨ:     private void UpdatePlayerFinalPose()
// CŨ:     {
// CŨ:         if (!holdPlayerFinalFrame ||
// CŨ:             playerState == null ||
// CŨ:             playerAnimator == null ||
// CŨ:             playerState.CurrentState != PlayerState.State.StealthTakeDown)
// CŨ:         {
// CŨ:             return;
// CŨ:         }
// CŨ:
// CŨ:         AnimatorStateInfo info =
// CŨ:             playerAnimator.GetCurrentAnimatorStateInfo(0);
// CŨ:
// CŨ:         if (info.IsName("StealthTakeDown") &&
// CŨ:             info.normalizedTime >= 0.999f)
// CŨ:         {
// CŨ:             playerState.HoldStealthTakeDownFinalFrame();
// CŨ:         }
// CŨ:     }
// MỚI:
private void UpdatePlayerAnimationTimeline()
{
    if (playerAnimator == null ||
        playerState == null ||
        playerState.CurrentState !=
            PlayerState.State.StealthTakeDown)
    {
        return;
    }

    if (!playerAnimator.isActiveAndEnabled)
        return;

    AnimatorStateInfo info =
        playerAnimator.GetCurrentAnimatorStateInfo(0);

    if (!info.IsName("StealthTakeDown"))
        return;

    float normalizedTime =
        Mathf.Clamp01(
            info.normalizedTime
        );

    float currentFrame =
        normalizedTime *
        playerAnimationTotalFrames;

    int timelinePhase;
    if (currentFrame < playerYJumpStartFrame)
        timelinePhase = 1;
    else
    {
        int diagnosticJumpPeakFrame =
            playerYJumpStartFrame + Mathf.Max(1, playerYJumpRiseFrames);

        if (currentFrame < diagnosticJumpPeakFrame)
            timelinePhase = 2;
        else if (currentFrame < playerYJumpLowerStartFrame)
            timelinePhase = 3;
        else
        {
            int diagnosticJumpLowerEndFrame =
                playerYJumpLowerStartFrame + Mathf.Max(1, playerYJumpLowerFrames);

            if (currentFrame < diagnosticJumpLowerEndFrame)
                timelinePhase = 4;
            else if (currentFrame < playerRetreatStartFrame)
                timelinePhase = 5;
            else if (currentFrame < playerAnimationTotalFrames)
                timelinePhase = 6;
            else
                timelinePhase = 7;
        }
    }

    if (timelinePhase != lastTimelinePhase)
    {
        lastTimelinePhase = timelinePhase;

        if (logDebug)
        {
            Debug.Log(
                "[StealthTakedown] PLAYER TIMELINE PHASE " +
                timelinePhase +
                " | Frame=" + currentFrame.ToString("0.###") +
                "/" + playerAnimationTotalFrames +
                " | Position=" + transform.position +
                " | BaseY=" + playerBaseY,
                this
            );
        }
    }

    if (!playerTimelineInitialized)
    {
        playerTimelineInitialized =
            true;

        // Frame 0:
        // Player giữ nguyên vị trí A,
        // dùng Y hiện tại làm Base Y.
        SetPlayerTimelinePosition(
            originalPlayerPosition
        );
    }

    Vector3 nextPosition =
        originalPlayerPosition;

    // ------------------------------------------------------------
    // FRAME 0 -> 39:
    // Player đi từ A tới vị trí phía sau Enemy theo PlayerBehindEnemyDistance.
    // ------------------------------------------------------------
    if (currentFrame <
        playerYJumpStartFrame)
    {
        float approachT =
            Mathf.InverseLerp(
                0f,
                playerApproachEndFrame,
                currentFrame
            );

        approachT =
            Mathf.SmoothStep(
                0f,
                1f,
                approachT
            );

        nextPosition =
            Vector3.Lerp(
                originalPlayerPosition,
                playerApproachTargetPosition,
                approachT
            );

        nextPosition.y =
            playerBaseY;

        SetPlayerTimelinePosition(
            nextPosition
        );

        return;
    }

    // ------------------------------------------------------------
    // FRAME 40 -> PEAK:
    // Player tăng Y từ Base Y lên Base Y + PlayerYJump.
    // ------------------------------------------------------------
    int jumpPeakFrame =
        playerYJumpStartFrame +
        Mathf.Max(
            1,
            playerYJumpRiseFrames
        );

    if (currentFrame <
        jumpPeakFrame)
    {
        float riseT =
            Mathf.InverseLerp(
                playerYJumpStartFrame,
                jumpPeakFrame,
                currentFrame
            );

        riseT =
            Mathf.SmoothStep(
                0f,
                1f,
                riseT
            );

        nextPosition =
            playerApproachTargetPosition;

        nextPosition.y =
            Mathf.Lerp(
                playerBaseY,
                playerBaseY +
                    playerYJump,
                riseT
            );

        SetPlayerTimelinePosition(
            nextPosition
        );

        return;
    }

    // ------------------------------------------------------------
    // PEAK -> FRAME 99:
    // Giữ đúng Y = Base Y + PlayerYJump tại vị trí C.
    // ------------------------------------------------------------
    if (currentFrame <
        playerYJumpLowerStartFrame)
    {
        nextPosition =
            playerApproachTargetPosition;

        nextPosition.y =
            playerBaseY +
            playerYJump;

        SetPlayerTimelinePosition(
            nextPosition
        );

        return;
    }

    // ------------------------------------------------------------
    // FRAME 100 -> LOWER PEAK:
    // Hạ Y bổ sung từ Base Y + PlayerYJump về Base Y.
    // ------------------------------------------------------------
    int jumpLowerEndFrame =
        playerYJumpLowerStartFrame +
        Mathf.Max(
            1,
            playerYJumpLowerFrames
        );

    if (currentFrame <
        jumpLowerEndFrame)
    {
        float lowerT =
            Mathf.InverseLerp(
                playerYJumpLowerStartFrame,
                jumpLowerEndFrame,
                currentFrame
            );

        lowerT =
            Mathf.SmoothStep(
                0f,
                1f,
                lowerT
            );

        nextPosition =
            playerApproachTargetPosition;

        nextPosition.y =
            Mathf.Lerp(
                playerBaseY +
                    playerYJump,
                playerBaseY,
                lowerT
            );

        SetPlayerTimelinePosition(
            nextPosition
        );

        return;
    }

    // ------------------------------------------------------------
    // LOWER PEAK -> FRAME 154:
    // Player đứng tại C, Y đã trở về Base Y.
    // ------------------------------------------------------------
    if (currentFrame <
        playerRetreatStartFrame)
    {
        nextPosition =
            playerApproachTargetPosition;

        nextPosition.y =
            playerBaseY;

        SetPlayerTimelinePosition(
            nextPosition
        );

        return;
    }

    // ------------------------------------------------------------
    // FRAME 155 -> 210:
    // Player lùi từ C trở về đúng A.
    // Hướng mặt KHÔNG đổi.
    // ------------------------------------------------------------
    if (currentFrame <
        playerAnimationTotalFrames)
    {
        float retreatT =
            Mathf.InverseLerp(
                playerRetreatStartFrame,
                playerAnimationTotalFrames,
                currentFrame
            );

        retreatT =
            Mathf.SmoothStep(
                0f,
                1f,
                retreatT
            );

        nextPosition =
            Vector3.Lerp(
                playerApproachTargetPosition,
                originalPlayerPosition,
                retreatT
            );

        nextPosition.y =
            playerBaseY;

        SetPlayerTimelinePosition(
            nextPosition
        );

        return;
    }

    // ------------------------------------------------------------
    // FRAME 210:
    // Đảm bảo Player ở chính xác A, kết thúc animation và unlock.
    // Enemy KHÔNG bị kết thúc theo Player.
    // ------------------------------------------------------------
    SetPlayerTimelinePosition(
        originalPlayerPosition
    );

    playerAnimationFinished =
        true;

    FinishPlayerTakedownState();
}

// Chức năng mới:
// Gán Transform/Rigidbody của Player về một điểm timeline duy nhất.
// Rigidbody được cập nhật cùng lúc để tránh lệch giữa Transform và physics.
private void SetPlayerTimelinePosition(
    Vector3 worldPosition)
{
    transform.position =
        worldPosition;

    if (takedownSharedForward.sqrMagnitude >
        0.0001f)
    {
        Quaternion sharedRotation =
            Quaternion.LookRotation(
                takedownSharedForward,
                Vector3.up
            );

        transform.rotation =
            sharedRotation;

        if (playerRigidbody != null)
        {
            playerRigidbody.rotation =
                sharedRotation;
        }
    }

    if (playerRigidbody != null)
    {
        playerRigidbody.position =
            worldPosition;
    }

    Physics.SyncTransforms();
}

    private void PlayEnemyTakedownVoice(
        EnemyCheckpointHandler target)
    {
        if (enemyTakedownVoiceClip == null ||
            target == null)
        {
            return;
        }

        AudioSource targetAudioSource =
            target.GetComponent<AudioSource>();

        if (targetAudioSource == null)
        {
            targetAudioSource =
                target.GetComponentInChildren<AudioSource>();
        }

        if (targetAudioSource == null)
        {
            Debug.LogWarning(
                "[StealthTakedown] ENEMY TAKEDOWN VOICE AUDIO SOURCE NULL | " +
                "Gán AudioSource cho Enemy.",
                target
            );
            return;
        }

        targetAudioSource.PlayOneShot(
            enemyTakedownVoiceClip,
            enemyTakedownVoiceVolume
        );
    }

// CŨ:     private void FinishPlayerTakedownState()
// CŨ:     {
// CŨ:         if (playerState != null &&
// CŨ:             playerState.CurrentState ==
// CŨ:                 PlayerState.State.StealthTakeDown)
// CŨ:         {
// CŨ:             playerState.EndStealthTakeDown();
// CŨ:         }
// CŨ:
// CŨ:         if (playerRigidbody != null && rigidbodyStateCaptured)
// CŨ:         {
// CŨ:             playerRigidbody.linearVelocity = Vector3.zero;
// CŨ:             playerRigidbody.angularVelocity = Vector3.zero;
// CŨ:             playerRigidbody.constraints = originalRigidbodyConstraints;
// CŨ:             playerRigidbody.isKinematic = originalRigidbodyKinematic;
// CŨ:             playerRigidbody.position = transform.position;
// CŨ:         }
// CŨ:
// CŨ:         if (playerAnimator != null && rootMotionStateCaptured)
// CŨ:             playerAnimator.applyRootMotion = originalPlayerRootMotion;
// CŨ:
// CŨ:         if (playerController != null &&
// CŨ:             !playerFailureInProgress)
// CŨ:         {
// CŨ:             playerController.SetExternalActionLock(false);
// CŨ:         }
// CŨ:
// CŨ:         rigidbodyStateCaptured = false;
// CŨ:         rootMotionStateCaptured = false;
// CŨ:
// CŨ:         takedownInProgress = false;
// CŨ:         RefreshActiveTarget();
// CŨ:     }
// MỚI:
private void FinishPlayerTakedownState()
{
    if (playerCleanupCompleted)
        return;

    // Chức năng mới:
    // Khi Player tới frame 210, Player kết thúc action NGAY.
    // Enemy vẫn tiếp tục giữ GetStealthTakeDown frame cuối + delay 5 giây.
    if (playerState != null &&
        playerState.CurrentState ==
            PlayerState.State.StealthTakeDown)
    {
        playerState.EndStealthTakeDown();
    }

    // CŨ:
    // playerRigidbody.linearVelocity = Vector3.zero;
    // playerRigidbody.angularVelocity = Vector3.zero;
    // playerRigidbody.constraints = originalRigidbodyConstraints;
    // playerRigidbody.isKinematic = originalRigidbodyKinematic;
    //
    // MỚI:
    // Restore Rigidbody state theo đúng thứ tự để không gọi linearVelocity/angularVelocity
    // khi Rigidbody vẫn đang kinematic (warning đã xuất hiện trong log test trước).
    if (playerRigidbody != null &&
        rigidbodyStateCaptured)
    {
        playerRigidbody.constraints =
            originalRigidbodyConstraints;

        playerRigidbody.isKinematic =
            originalRigidbodyKinematic;

        if (!playerRigidbody.isKinematic)
        {
            playerRigidbody.linearVelocity =
                Vector3.zero;

            playerRigidbody.angularVelocity =
                Vector3.zero;
        }

        playerRigidbody.position =
            transform.position;
    }

    if (playerAnimator != null &&
        rootMotionStateCaptured)
    {
        playerAnimator.applyRootMotion =
            originalPlayerRootMotion;
    }

    if (playerController != null &&
        !playerFailureInProgress)
    {
        playerController.SetExternalActionLock(
            false
        );
    }

    rigidbodyStateCaptured = false;
    rootMotionStateCaptured = false;

    playerCleanupCompleted = true;
    takedownInProgress = false;

    if (logDebug)
    {
        Debug.Log(
            "[StealthTakedown] PLAYER TIMELINE COMPLETE | Returned to A=" +
            transform.position +
            " | State=" +
            (playerState != null ? playerState.CurrentState.ToString() : "<NULL>") +
            " | AcceptInput=" +
            (playerController != null ? playerController.acceptInput.ToString() : "<NULL>"),
            this
        );
    }

    // Chức năng mới:
    // Sau khi Player hoàn tất ở A, chỉ làm refresh UI/target nếu
    // Enemy đã thực sự ra khỏi stealth presentation.
    // Nếu Enemy còn giữ frame cuối thì nó vẫn bị IsStealthTakedownInProgress chặn.
    RefreshActiveTarget();
}

    private void HandlePlayerDeathOrFailureStarted()
    {
        // Player có thể bị teleported về checkpoint trong khi trigger physics chưa
        // kịp phát OnTriggerExit. Xóa target cache ngay khi Failure/Death bắt đầu
        // để Button không giữ target cũ ở vị trí trước Failure.
        candidates.Clear();
        combatCandidates.Clear();
        activeTarget = null;

        SetButtonVisible(false);
        SetPunchButtonState(false, false);

        if (punchInProgress ||
            playerEnemyHitRoutine != null)
        {
            playerFailureDuringMelee = true;

            StopPunchRoutine();
            StopPlayerEnemyHitRoutine();

            SetButtonVisible(false);
            SetPunchButtonState(false, false);

            if (logDebug)
            {
                Debug.Log(
                    "[StealthTakedown] MELEE ACTION CANCELLED BY PLAYER DEATH/FAILURE.",
                    this
                );
            }
        }

        if (!takedownInProgress)
            return;

        // Chức năng mới:
        // Nếu Player thất bại bởi hệ thống khác trong lúc takedown, action phải hủy ngay.
        // CheckpointManager vẫn là authority restore Enemy sau đó.
        if (activeTarget != null &&
            activeTarget.IsStealthTakedownInProgress)
        {
            activeTarget.CancelStealthTakedownPresentation();
        }

        playerFailureInProgress =
            true;

        SetButtonVisible(false);

        takedownInProgress =
            false;
    }

    // Chức năng mới:
    // Khi Button đang hiện nhưng bị disable, log đúng lý do một lần mỗi khi lý do thay đổi.
    private void LogButtonStateIfChanged(
        bool visible,
        bool interactable,
        string diagnostic)
    {
        if (!logDebug)
            return;

        if (lastLoggedButtonVisible ==
                visible &&
            lastLoggedButtonInteractable ==
                interactable &&
            lastLoggedButtonDiagnostic ==
                diagnostic)
        {
            return;
        }

        lastLoggedButtonVisible =
            visible;

        lastLoggedButtonInteractable =
            interactable;

        lastLoggedButtonDiagnostic =
            diagnostic;

        Debug.Log(
            "[StealthTakedown] BUTTON STATE | " +
            "Visible=" +
            visible +
            " | Interactable=" +
            interactable +
            " | ButtonEnabled=" +
            (
                boundStealthTakedownButton != null &&
                boundStealthTakedownButton.enabled
            ) +
            " | GameObjectActive=" +
            (
                boundStealthTakedownButton != null &&
                boundStealthTakedownButton.gameObject.activeInHierarchy
            ) +
            " | " +
            diagnostic,
            this
        );
    }

    private string BuildButtonDiagnostic(
        EnemyCheckpointHandler candidateInTrigger,
        EnemyCheckpointHandler bestValidTarget)
    {
        if (boundStealthTakedownButton ==
            null)
        {
            return
                "Button component chưa được bind.";
        }

        if (candidateInTrigger == null)
        {
            return
                "Không có Enemy trong StealthTakedownTrigger.";
        }

        if (bestValidTarget != null)
        {
            return
                "Target hợp lệ = " +
                bestValidTarget.name +
                ".";
        }

        if (candidateInTrigger.IsRuntimeDead)
        {
            return
                "Candidate bị loại: IsRuntimeDead = true.";
        }

        if (candidateInTrigger.IsPermanentlyDead)
        {
            return
                "Candidate bị loại: IsPermanentlyDead = true.";
        }

        if (candidateInTrigger.IsStealthTakedownInProgress)
        {
            return
                "Candidate bị loại: đang Stealth Takedown.";
        }

        if (!candidateInTrigger.CanBeginStealthTakedown())
        {
            return
                "Candidate bị loại: EnemyCheckpointHandler validation failed.";
        }

        GuardVisionView vision =
            candidateInTrigger.GetComponent<
                GuardVisionView
            >();

        if (vision == null)
        {
            vision =
                candidateInTrigger.GetComponentInChildren<
                    GuardVisionView
                >(true);
        }

        if (vision == null)
        {
            return
                "Candidate bị loại: thiếu GuardVisionView.";
        }

        if (!vision.CanBeStealthTakedownTarget)
        {
            return
                "Candidate bị loại: Vision không ở trạng thái cho phép Stealth Takedown.";
        }

        Vector3 flatOffset =
            candidateInTrigger.transform.position -
            transform.position;

        flatOffset.y = 0f;

        float distance =
            flatOffset.magnitude;

        // MỚI:
        // Trigger đã quyết định target có nằm trong range hay không.
        // Chỉ áp dụng MaxInitialDistance nếu người làm game chủ động bật nó.
        if (useMaxInitialDistanceValidation &&
            distance > maxInitialDistance)
        {
            return
                "Candidate bị loại: distance=" +
                distance.ToString("0.###") +
                " > max=" +
                maxInitialDistance.ToString("0.###") +
                ".";
        }

        // MỚI:
        // Mặc định bỏ yêu cầu góc. Player sẽ được căn hướng trong action.
        if (useBehindAngleValidation &&
            flatOffset.sqrMagnitude > 0.0001f)
        {
            float angle =
                Vector3.Angle(
                    candidateInTrigger
                        .transform
                        .forward,
                    flatOffset.normalized
                );

            if (angle > maxBehindAngle)
            {
                return
                    "Candidate bị loại: angle=" +
                    angle.ToString("0.###") +
                    " > max=" +
                    maxBehindAngle.ToString("0.###") +
                    ".";
            }
        }

        return
            "Candidate tồn tại nhưng EnemyCheckpointHandler validation failed.";
    }

    // Chức năng mới:
    // Điều khiển trạng thái hiển thị và tương tác của Stealth Takedown Button.
    private void SetButtonVisible(bool visible)
    {
        SetButtonState(
            visible,
            visible
        );
    }

    // Chức năng mới:
    // Tách trạng thái hiển thị và tương tác của Button.
    // Button có thể vẫn hiển thị khi Enemy còn nằm trong trigger,
    // nhưng sẽ không click được nếu target chưa đạt điều kiện stealth.
    private void SetButtonState(
        bool visible,
        bool interactable)
    {
        if (stealthTakedownButton != null)
        {
            stealthTakedownButton.SetActive(
                visible
            );
        }

        if (stealthTakedownButtonComponent != null)
        {
            stealthTakedownButtonComponent.interactable =
                visible &&
                interactable;
        }
    }
}