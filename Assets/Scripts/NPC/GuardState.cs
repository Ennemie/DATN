using DG.Tweening;
using TMPro;
using Unity.AppUI.Core;
using UnityEngine;

public class GuardState : MonoBehaviour
{
    private enum guardType { Melee, Shooter }
    [SerializeField] private guardType _guardType;
    private GuardController guardController;
    private GuardVisionView guardFOV;
    private MeshRenderer guardFOVRenderer;
    protected GameObject player;
    [HideInInspector] public bool isDead = false;
    private bool isDetectSoundWave = false;
    [HideInInspector] public bool isDetectPlayer = false;

    // Chức năng mới:
    // Khóa toàn bộ state/patrol/combat trong lúc Enemy đang thực hiện Stealth Take Down.
    // Tham chiếu: EnemyCheckpointHandler, StealthTakedownController, MeleeGuardController.
    private bool isStealthTakedownActive = false;
    public bool IsStealthTakedownActive => isStealthTakedownActive;

    // Sử dụng biến này để quản lý CHUNG cho cả DOMove, DOLookAt và DOVirtual.DelayedCall
    private Tween activeTween;

    // Movement
    private Vector3 guardStartPoint;
    private Vector3 guardEndPoint;
    private float walkSpeed = 1.5f;
    private float runSpeed = 4f;

    private Animator anim;

    [HideInInspector] public enum State
    {
        Idle,
        FightIdle,
        DetectPlayer,
        Guarding,
        DetectSoundWave,
        Running,
        Punching,
        WalkingBackward,
        TakingPunch,
        Stunned,
        Dying,
        StealthDying
    }

    [HideInInspector] public State _state;
    public State state
    {
        get { return _state; }
        set
        {
            // CŨ:
            // if (isDead)
            // {
            //     if (anim != null)
            //         anim.CrossFade("Stunned", 0.01f);
            //     return;
            // }
            //
            // MỚI:
            // Ngoài trạng thái dead, Stealth Take Down cũng phải là một state độc quyền
            // để Patrol/Combat không chen vào giữa animation ghép cặp.
            if (isDead)
            {
                if (anim != null)
                    anim.CrossFade("Stunned", 0.01f);
                return;
            }

            if (isStealthTakedownActive)
                return;

            if (_state == State.DetectSoundWave)
            {
                ClearAllTweens();
                _state = value;
                UpdateBehaviour(value);
                return;
            }

            if (_state != value)
            {
                ClearAllTweens();
                _state = value;
                UpdateBehaviour(value);
            }
        }
    }

    // Chức năng mới: API tích hợp checkpoint/death cho Enemy.
    // Tham chiếu:
    // - EnemyProperties
    // - EnemyCheckpointHandler
    // - CheckpointManager
    public State CurrentState => _state;

    // Chức năng mới:
    // Khóa Enemy khỏi mọi state AI nhưng không đánh dấu runtimeDead;
    // EnemyCheckpointHandler sẽ quyết định thời điểm temporary-dead sau cùng.
    // Tham chiếu: EnemyCheckpointHandler, GuardController, MeleeGuardController.
    public void BeginStealthTakedownLock()
    {
        ClearAllTweens();
        isStealthTakedownActive = true;
        isDetectPlayer = false;
        isDetectSoundWave = false;
        _state = State.StealthDying;

        if (anim != null)
            anim.speed = 1f;
    }

    // Chức năng mới:
    // Gỡ cờ khóa Stealth Take Down khi checkpoint restore hoàn tất.
    public void EndStealthTakedownLock()
    {
        isStealthTakedownActive = false;
    }

    // CŨ:
    // public void EnterDeathState(State deathState)
    // {
    //     ClearAllTweens();
    //     _state = deathState;
    //     isDead = true;
    //     if (anim != null)
    //         anim.speed = deathState == State.Stunned ? 2f : 1f;
    //     UpdateBehaviour(deathState);
    // }
    //
    // MỚI:
    // EnemyCheckpointHandler là authority duy nhất phát animation GetStealthTakeDown.
    // GuardState chỉ khóa AI/state khi StealthDying, tránh việc GuardState gọi
    // Animator.Play lần thứ hai và làm trục animation bị tranh quyền.
    // Tham chiếu: EnemyCheckpointHandler -> BeginRuntimeDeath().
    public void EnterDeathState(State deathState)
    {
        ClearAllTweens();
        _state = deathState;
        isDead = true;

        if (deathState == State.StealthDying)
        {
            isStealthTakedownActive = true;
            isDetectPlayer = false;
            isDetectSoundWave = false;

            if (anim != null)
                anim.speed = 1f;

            // Không gọi UpdateBehaviour(StealthDying) ở đây.
            // EnemyCheckpointHandler sẽ Play animation đúng một lần.
            return;
        }

        if (anim != null)
            anim.speed = 2f;

        UpdateBehaviour(deathState);
    }

    /// <summary>
    /// Chức năng mới:
    /// Xóa combat/detection flags và đưa Enemy trở lại Guarding như lúc mới vào scene.
    /// Dùng cho temporary-death checkpoint restore.
    /// </summary>
    public void ResetForCheckpointRestore()
    {
        ClearAllTweens();

        // CŨ:
        // isDead = false;
        // isDetectPlayer = false;
        // isDetectSoundWave = false;
        //
        // MỚI:
        // Restore cũng xóa lock của Stealth Take Down trước khi Patrol hoạt động lại.
        isDead = false;
        isStealthTakedownActive = false;
        isDetectPlayer = false;
        isDetectSoundWave = false;

        // Điều chỉnh:
        // Restore xong phải trả tốc độ Animator về 1.
        if (anim != null)
            anim.speed = 1f;

        // Chức năng mới:
        // Re-arm Vision thật sự, không chỉ bật component.
        if (guardFOV != null)
            guardFOV.ResetToNeutral(true);
        else
            ActiveFOV(true);

        RestartPatrolAfterExternalInterruption();
    }

    // Chức năng mới:
    // Ép tạo lại Patrol A/B sau khi Chase, Investigation hoặc Restore đã ngắt tween.
    public void RestartPatrolAfterExternalInterruption()
    {
        if (isDead)
            return;

        ClearAllTweens();

        isDetectPlayer = false;
        isDetectSoundWave = false;

        if (anim != null)
        {
            anim.speed = 1f;
            anim.CrossFade("Idle", 0.05f);
        }

        // Chức năng mới:
        // Patrol dùng DOTween, vì vậy NavMesh phải dừng để không tranh Transform.
        if (guardController != null)
            guardController.ResetRuntimeState();

        _state = State.Guarding;
        Guarding();
    }

    private void Start()
    {
        anim = GetComponent<Animator>();
        guardStartPoint = transform.position;

        Transform endPoint = transform.Find("GuardEndPoint");
        if (endPoint != null)
            guardEndPoint = endPoint.position;
        else
            guardEndPoint = guardStartPoint;

        player = GameObject.FindGameObjectWithTag("Player");
        guardFOV = GetComponent<GuardVisionView>();
        guardFOVRenderer = GetComponent<MeshRenderer>();

        switch (_guardType)
        {
            case guardType.Melee:
                guardController = GetComponent<MeleeGuardController>();
                break;

            case guardType.Shooter:
                guardController = GetComponent<GuardController>();
                break;
        }

        if (guardController == null)
            guardController = GetComponent<GuardController>();

        state = State.Guarding;
    }

    private void UpdateBehaviour(State newState)
    {
        if (anim == null)
            return;

        switch (newState)
        {
            case State.Idle:
                anim.CrossFade("Idle", 0.1f);
                break;

            case State.FightIdle:
                anim.CrossFade("FightIdle", 0.1f);
                break;

            case State.DetectPlayer:
                DetectPlayer();
                break;

            case State.Guarding:
                Guarding();
                break;

            case State.DetectSoundWave:
                DetectSoundWave();
                break;

            case State.Running:
                anim.CrossFade("Running", 0.1f);
                break;

            case State.WalkingBackward:
                anim.CrossFade("Walking Backward", 0.1f);
                break;

            case State.Punching:
                anim.CrossFade("Punch", 0.1f);
                break;

            case State.TakingPunch:
                anim.CrossFade("TakingPunch", 0.1f);
                break;

            case State.Stunned:
                anim.CrossFade("Stunned", 0.1f);
                break;

            case State.Dying:
                anim.CrossFade("Dying", 0.1f);
                break;

            case State.StealthDying:
                // CŨ:
                // anim.CrossFade("StealthDying", 0.1f);
                //
                // CŨ (sửa trước):
                // anim.Play("GetStealthTakeDown", 0, 0f);
                //
                // MỚI:
                // Không phát animation tại GuardState. GuardState chỉ giữ state khóa;
                // EnemyCheckpointHandler chịu trách nhiệm Play GetStealthTakeDown và
                // đồng bộ frame 0 với Player.
                break;
        }
    }

    private void Guarding()
    {
        // Kiểm tra khoảng cách gần đúng thay vì so sánh bằng tuyệt đối "==" của Vector3
        if (Vector3.Distance(transform.position, guardStartPoint) > 0.1f)
            GuardingToStart();
        else
            GuradingToEnd();
    }

    private void GuardingToStart()
    {
        ClearAllTweens();

        Vector3 lookTarget = new Vector3(
            guardStartPoint.x,
            transform.position.y,
            guardStartPoint.z
        );

        activeTween = transform.DOLookAt(lookTarget, 0.5f).OnComplete(() =>
        {
            if (isDead)
                return;

            if (anim != null)
                anim.CrossFade("Walking", 0.1f);

            activeTween = transform.DOMove(
                    guardStartPoint,
                    walkSpeed
                )
                .SetSpeedBased()
                .SetEase(Ease.Linear)
                .OnComplete(() =>
                {
                    if (isDead)
                        return;

                    if (anim != null)
                        anim.CrossFade("Idle", 0.1f);

                    // Lưu DelayedCall vào activeTween để có thể hủy nếu bị đánh động giữa chừng
                    activeTween = DOVirtual.DelayedCall(
                        2f,
                        GuradingToEnd
                    );
                });
        });
    }

    private void GuradingToEnd()
    {
        ClearAllTweens();

        Vector3 lookTarget = new Vector3(
            guardEndPoint.x,
            transform.position.y,
            guardEndPoint.z
        );

        activeTween = transform.DOLookAt(lookTarget, 0.5f).OnComplete(() =>
        {
            if (isDead)
                return;

            if (anim != null)
                anim.CrossFade("Walking", 0.1f);

            activeTween = transform.DOMove(
                    guardEndPoint,
                    walkSpeed
                )
                .SetSpeedBased()
                .SetEase(Ease.Linear)
                .OnComplete(() =>
                {
                    if (isDead)
                        return;

                    if (anim != null)
                        anim.CrossFade("Idle", 0.1f);

                    // Lưu DelayedCall vào activeTween
                    activeTween = DOVirtual.DelayedCall(
                        2f,
                        GuardingToStart
                    );
                });
        });
    }

    private void DetectSoundWave()
    {
        isDetectSoundWave = true;

        if (anim != null)
            anim.CrossFade("Idle", 0.1f);

        FocusOnPlayer();
    }

    private void FocusOnPlayer()
    {
        if (player == null)
            player = GameObject.FindGameObjectWithTag("Player");

        if (player == null)
            return;

        Vector3 targetPosition = new Vector3(
            player.transform.position.x,
            transform.position.y,
            player.transform.position.z
        );

        activeTween = transform.DOLookAt(targetPosition, 0.5f).OnComplete(() =>
        {
            if (isDead)
                return;

            activeTween = DOVirtual.DelayedCall(5f, () =>
            {
                isDetectSoundWave = false;

                if (!isDetectPlayer && !isDead)
                    RestartPatrolAfterExternalInterruption();
            });
        });
    }

    // Hàm bổ trợ giúp dọn dẹp sạch sẽ mọi Tween/DelayedCall cũ tránh xung đột
    private void ClearAllTweens()
    {
        if (activeTween != null && activeTween.IsActive())
        {
            activeTween.Kill();
            activeTween = null;
        }

        transform.DOKill();
    }

    private void DetectPlayer()
    {
        ClearAllTweens();
        isDetectPlayer = true;

        // Chức năng mới:
        // RED đã được GuardVisionView xác nhận.
        // GuardState chỉ còn cầu nối sang controller Chase.
        ActiveFOV(true);

        if (guardController != null)
            guardController.ChasePlayer();
    }

    private void ActiveFOV(bool isActive)
    {
        if (guardFOVRenderer != null)
            guardFOVRenderer.enabled = isActive;

        if (guardFOV != null)
            guardFOV.enabled = isActive;
    }
}
