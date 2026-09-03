using UnityEngine;
using DG.Tweening;
using UnityEngine.AI;
using Unity.VisualScripting;

public class MeleeGuardController : GuardController
{
    [HideInInspector] public bool isPlayerOnAttackRange;
    [SerializeField] private float attackCoolDown;
    private bool isReadyToAttack = false;
    private bool isAttacking = false;

    // Chức năng mới:
    // Giữ delayed call của attack để restore checkpoint có thể hủy callback damage/state cũ.
    private Tween attackDamageTween;
    private Tween attackReleaseTween;
    private Tween attackReadyTween;

    [HideInInspector] public bool _isReadyToAttack
    {
        get { return isReadyToAttack; }
        set
        {
            if (isReadyToAttack != value)
            {
                isReadyToAttack = value;

                // CŨ:
                // if (isReadyToAttack &&
                //     !isAttacking &&
                //     isPlayerOnAttackRange &&
                //     !checkpointRestoreLock &&
                //     guardState != null &&
                //     !guardState.isDead)
                // {
                //     Attack();
                // }
                //
                // MỚI:
                // Không được tự kích hoạt Punch khi Enemy đang bị Stealth Take Down.
                // Tham chiếu: GuardState, EnemyCheckpointHandler.
                if (isReadyToAttack &&
                    !isAttacking &&
                    isPlayerOnAttackRange &&
                    !checkpointRestoreLock &&
                    guardState != null &&
                    !guardState.isDead &&
                    !guardState.IsStealthTakedownActive)
                {
                    Attack();
                }
            }
        }
    }

    protected override void Start()
    {
        base.Start();

        if (agent != null)
            agent.stoppingDistance = 1.3f;

        isPlayerOnAttackRange = false;
    }

    void LateUpdate()
    {
        // Chức năng mới:
        // Enemy chết hoặc đang restore thì tuyệt đối không SetDestination(Player).
        // CŨ:
        // if (checkpointRestoreLock ||
        //     guardState == null ||
        //     guardState.isDead ||
        //     agent == null ||
        //     !agent.enabled ||
        //     agent.isStopped ||
        //     player == null)
        // {
        //     return;
        // }
        //
        // MỚI:
        // LateUpdate cũng bị khóa để không SetDestination(Player) trong takedown.
        // Tham chiếu: GuardState, EnemyCheckpointHandler.
        if (checkpointRestoreLock ||
            (guardState != null && guardState.IsStealthTakedownActive) ||
            guardState == null ||
            guardState.isDead ||
            agent == null ||
            !agent.enabled ||
            agent.isStopped ||
            player == null)
        {
            return;
        }

        if (isChasing)
        {
            if (isReadyToAttack)
                return;

            if (!agent.pathPending &&
                agent.remainingDistance <= agent.stoppingDistance)
            {
                guardState.state = GuardState.State.FightIdle;
            }
            else
            {
                if (guardState.state != GuardState.State.Running)
                    guardState.state = GuardState.State.Running;
            }

            playerPos = player.transform.position;
            agent.SetDestination(playerPos);
        }
    }

    // CŨ:
    // public override void ChasePlayer()
    // {
    //     FocusOnPlayer();
    //     isChasing = true;
    // }
    //
    // MỚI:
    // Chức năng mới:
    // - Chỉ Chase khi không death/restore.
    // - Tắt Investigation.
    // - Bật lại NavMesh đúng lúc.
    // - Đặt destination bằng vị trí Player hiện tại.
    public override void ChasePlayer()
    {
        // CŨ:
        // if (checkpointRestoreLock)
        //     return;
        //
        // if (guardState != null && guardState.isDead)
        //     return;
        //
        // MỚI:
        // Không Chase khi Enemy đang bị Stealth Take Down khóa.
        if (checkpointRestoreLock)
            return;

        if (guardState != null &&
            (guardState.isDead ||
             guardState.IsStealthTakedownActive))
            return;

        if (player == null)
            player = GameObject.FindGameObjectWithTag("Player");

        if (player == null)
            return;

        if (agent == null ||
            !agent.enabled ||
            !agent.isOnNavMesh)
        {
            return;
        }

        isInvestigating = false;
        isChasing = true;
        playerPos = player.transform.position;

        if (moveTween != null && moveTween.IsActive())
            moveTween.Kill();

        transform.DOKill();

        agent.isStopped = false;
        agent.velocity = Vector3.zero;
        agent.ResetPath();
        agent.SetDestination(playerPos);

        FocusOnPlayer();
    }

    // CŨ:
    // protected override void Attack()
    // {
    //     if(guardState.isDead) return;
    //     guardState.state = GuardState.State.Punching;
    //
    //     if(guardState.isDead) return;
    //     DOVirtual.DelayedCall(1f, () =>
    //     {
    //         if (isPlayerOnAttackRange)
    //         {
    //             PlayerProperties.Instance.TakeDamage(10);
    //         }
    //     });
    //
    //     if(guardState.isDead) return;
    //     DOVirtual.DelayedCall(1.267f, () =>
    //     {
    //         isAttacking = false;
    //         _isReadyToAttack = false;
    //         guardState.state = GuardState.State.FightIdle;
    //     });
    //
    //     if(guardState.isDead) return;
    //     DOVirtual.DelayedCall(1.267f + attackCoolDown, () =>
    //     {
    //         if(isPlayerOnAttackRange)
    //         {
    //             _isReadyToAttack = true;
    //         }
    //     });
    // }
    //
    // MỚI:
    // Điều chỉnh: giữ nguyên timing attack cũ; quản lý Tween để restore/death
    // có thể hủy callback damage/state cũ.
    protected override void Attack()
    {
        // CŨ:
        // if (guardState == null ||
        //     guardState.isDead ||
        //     checkpointRestoreLock)
        // {
        //     return;
        // }
        //
        // MỚI:
        // Attack không thể bắt đầu trong Stealth Take Down.
        if (guardState == null ||
            guardState.isDead ||
            guardState.IsStealthTakedownActive ||
            checkpointRestoreLock)
        {
            return;
        }

        guardState.state = GuardState.State.Punching;

        if (guardState.isDead)
            return;

        if (attackDamageTween != null && attackDamageTween.IsActive())
            attackDamageTween.Kill();

        attackDamageTween = DOVirtual.DelayedCall(1f, () =>
        {
            if (isPlayerOnAttackRange &&
                !guardState.isDead &&
                !checkpointRestoreLock &&
                PlayerProperties.Instance != null)
            {
                PlayerProperties.Instance.TakeDamage(10);
            }
        });

        if (guardState.isDead)
            return;

        if (attackReleaseTween != null && attackReleaseTween.IsActive())
            attackReleaseTween.Kill();

        attackReleaseTween = DOVirtual.DelayedCall(1.267f, () =>
        {
            isAttacking = false;
            _isReadyToAttack = false;

            // MỚI:
            // Delayed callback cũ không được trả Enemy về FightIdle giữa Stealth Take Down.
            if (!guardState.isDead &&
                !guardState.IsStealthTakedownActive &&
                !checkpointRestoreLock)
                guardState.state = GuardState.State.FightIdle;
        });

        if (guardState.isDead)
            return;

        if (attackReadyTween != null && attackReadyTween.IsActive())
            attackReadyTween.Kill();

        attackReadyTween = DOVirtual.DelayedCall(1.267f + attackCoolDown, () =>
        {
            if (isPlayerOnAttackRange &&
                !guardState.isDead &&
                !guardState.IsStealthTakedownActive &&
                !checkpointRestoreLock)
            {
                _isReadyToAttack = true;
            }
        });
    }

    // Chức năng mới: Reset runtime attack của MeleeGuard khi restore/checkpoint hoặc Player failure.
    public override void ResetRuntimeState()
    {
        if (attackDamageTween != null && attackDamageTween.IsActive())
            attackDamageTween.Kill();

        if (attackReleaseTween != null && attackReleaseTween.IsActive())
            attackReleaseTween.Kill();

        if (attackReadyTween != null && attackReadyTween.IsActive())
            attackReadyTween.Kill();

        attackDamageTween = null;
        attackReleaseTween = null;
        attackReadyTween = null;

        isPlayerOnAttackRange = false;
        isAttacking = false;
        isReadyToAttack = true;

        base.ResetRuntimeState();
    }
}
