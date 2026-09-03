using System;
using UnityEngine;
using UnityEngine.AI;
using DG.Tweening;

/// <summary>
/// Controller quản lý state, điều tra và Chase của Guard.
/// </summary>
public class GuardController : MonoBehaviour
{
    protected GameObject player;
    protected Vector3 playerPos;
    protected GuardState guardState;
    protected NavMeshAgent agent;
    protected Tween moveTween;
    protected float speed = 4f;
    protected bool isChasing = false;
    protected bool isInvestigating = false;
    protected float distance;

    // Chức năng mới:
    // Khóa toàn bộ AI movement trong lúc CheckpointManager đang restore Enemy.
    // Tham chiếu: EnemyCheckpointHandler.
    protected bool checkpointRestoreLock = false;

    private EnemyProperties enemyProperties;

    protected virtual void Start()
    {
        guardState = GetComponent<GuardState>();
        player = GameObject.FindGameObjectWithTag("Player");
        enemyProperties = GetComponent<EnemyProperties>();
        agent = GetComponent<NavMeshAgent>();

        if (guardState != null)
            guardState.state = GuardState.State.Guarding;

        if (agent != null)
        {
            // Điều chỉnh:
            // Patrol của GuardState dùng DOTween trên Transform.
            // NavMesh chỉ được chạy khi thực sự Chase/Investigation.
            agent.ResetPath();
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }
    }

    private void Update()
    {
        // Chức năng mới:
        // Trong phase vàng, Enemy chỉ điều tra vị trí cuối cùng của Player.
        // isChasing vẫn phải là false.
        if (checkpointRestoreLock || !isInvestigating)
            return;

        if (checkpointRestoreLock ||
            (guardState != null && guardState.IsStealthTakedownActive) ||
            guardState == null ||
            guardState.isDead)
        {
            StopInvestigationMovement();
            return;
        }

        if (agent == null ||
            !agent.enabled ||
            !agent.isOnNavMesh)
        {
            return;
        }

        agent.isStopped = false;

        if (playerPos != Vector3.zero)
            agent.SetDestination(playerPos);

        if (guardState.state != GuardState.State.Running)
            guardState.state = GuardState.State.Running;
    }

    private void OnTriggerStay(Collider other)
    {
        if (checkpointRestoreLock ||
            guardState == null ||
            guardState.isDead)
        {
            return;
        }

        if (other.CompareTag("SoundWave"))
        {
            Debug.Log("Guard detected sound wave");

            if (guardState.isDetectPlayer)
                return;

            guardState.state = GuardState.State.DetectSoundWave;
        }
    }

    // CŨ:
    // protected void FocusOnPlayer()
    // {
    //     transform.DOLookAt(playerPos, 0.5f);
    // }
    //
    // MỚI:
    // Điều chỉnh: playerPos cũ không được cập nhật ổn định và có thể bằng Vector3.zero.
    // Khi Focus được gọi, lấy vị trí Player hiện tại.
    protected void FocusOnPlayer()
    {
        if (player == null)
            player = GameObject.FindGameObjectWithTag("Player");

        if (player == null)
            return;

        playerPos = player.transform.position;

        Vector3 lookTarget = new Vector3(
            playerPos.x,
            transform.position.y,
            playerPos.z
        );

        transform.DOKill();
        transform.DOLookAt(lookTarget, 0.5f);
    }

    // CŨ:
    // public void TakeDamage(int damage)
    // {
    //     enemyProperties.TakeDamage(damage);
    // }
    //
    // MỚI:
    // Chức năng mới: bỏ qua hit đến trễ khi Enemy đã vào death/restore state.
    public void TakeDamage(int damage)
    {
        if (guardState != null &&
            (guardState.isDead ||
             guardState.IsStealthTakedownActive))
            return;

        if (checkpointRestoreLock)
            return;

        if (enemyProperties != null)
            enemyProperties.TakeDamage(damage);
    }

    /// <summary>
    /// Chức năng mới:
    /// Bắt đầu điều tra vị trí cuối cùng mà Vision vừa thấy Player.
    /// Đây KHÔNG phải Chase.
    ///
    /// Tham chiếu:
    /// - GuardVisionView
    /// - GuardState
    /// - NavMeshAgent
    /// </summary>
    public virtual void BeginInvestigation(Vector3 lastKnownPosition)
    {
        if (checkpointRestoreLock)
            return;

        if (guardState != null && guardState.isDead)
            return;

        isChasing = false;
        isInvestigating = true;
        playerPos = lastKnownPosition;

        if (moveTween != null && moveTween.IsActive())
            moveTween.Kill();

        transform.DOKill();

        if (agent != null &&
            agent.enabled &&
            agent.isOnNavMesh)
        {
            agent.ResetPath();
            agent.isStopped = false;
            agent.velocity = Vector3.zero;
            agent.SetDestination(lastKnownPosition);
        }

        if (guardState != null)
            guardState.state = GuardState.State.Running;
    }

    /// <summary>
    /// Chức năng mới:
    /// Cập nhật vị trí cuối cùng nhìn thấy trong phase vàng.
    /// Không bật isChasing.
    /// </summary>
    public virtual void UpdateInvestigationTarget(Vector3 lastKnownPosition)
    {
        if (checkpointRestoreLock || !isInvestigating)
            return;

        playerPos = lastKnownPosition;

        if (agent != null &&
            agent.enabled &&
            agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.SetDestination(lastKnownPosition);
        }
    }

    /// <summary>
    /// Chức năng mới:
    /// Kết thúc điều tra và trả quyền Transform cho GuardState Patrol.
    /// </summary>
    public virtual void EndInvestigationAndReturnToGuarding()
    {
        isInvestigating = false;
        isChasing = false;
        playerPos = Vector3.zero;

        StopNavigation();

        if (guardState != null && !guardState.isDead)
            guardState.RestartPatrolAfterExternalInterruption();
    }

    /// <summary>
    /// Chức năng mới:
    /// Dùng khi Player chết/thua hoặc Enemy chết.
    /// Chỉ ngắt Chase/Investigation và dừng NavMesh.
    /// </summary>
    public virtual void StopAllMovementImmediately()
    {
        isChasing = false;
        isInvestigating = false;
        playerPos = Vector3.zero;
        distance = 0f;

        if (moveTween != null && moveTween.IsActive())
            moveTween.Kill();

        transform.DOKill();
        StopNavigation();
    }

    /// <summary>
    /// Chức năng mới:
    /// Mở khóa restore trước khi CheckpointManager chỉnh Position/State.
    /// </summary>
    public virtual void BeginCheckpointRestore()
    {
        checkpointRestoreLock = true;

        isChasing = false;
        isInvestigating = false;
        playerPos = Vector3.zero;

        if (moveTween != null && moveTween.IsActive())
            moveTween.Kill();

        transform.DOKill();
        StopNavigation();
    }

    /// <summary>
    /// Chức năng mới:
    /// Kết thúc restore.
    ///
    /// Điều chỉnh:
    /// NavMesh vẫn dừng vì GuardState Patrol dùng DOTween.
    /// Patrol được GuardState.RestartPatrol... khởi động lại.
    /// </summary>
    public virtual void EndCheckpointRestore()
    {
        checkpointRestoreLock = false;

        isChasing = false;
        isInvestigating = false;
        playerPos = Vector3.zero;

        StopNavigation();
    }

    private void StopNavigation()
    {
        if (agent == null)
            return;

        if (agent.enabled)
        {
            if (agent.isOnNavMesh)
                agent.ResetPath();

            agent.velocity = Vector3.zero;
            agent.isStopped = true;
        }
    }

    private void StopInvestigationMovement()
    {
        isInvestigating = false;
        isChasing = false;
        playerPos = Vector3.zero;
        StopNavigation();
    }

    // Chức năng mới:
    // Khóa Chase/Investigation/Navigation trong toàn bộ thời gian Stealth Take Down.
    // Tham chiếu: EnemyCheckpointHandler, MeleeGuardController.
    public virtual void BeginStealthTakedownLock()
    {
        checkpointRestoreLock = true;
        isChasing = false;
        isInvestigating = false;
        playerPos = Vector3.zero;

        if (moveTween != null && moveTween.IsActive())
            moveTween.Kill();

        transform.DOKill();
        StopNavigation();
    }

    // Chức năng mới:
    // Gỡ khóa Stealth Take Down sau khi restore hoặc action kết thúc.
    public virtual void EndStealthTakedownLock()
    {
        checkpointRestoreLock = false;
        isChasing = false;
        isInvestigating = false;
        playerPos = Vector3.zero;
        StopNavigation();
    }

    // CŨ:
    // public virtual void ResetRuntimeState()
    // {
    //     isChasing = false;
    //     playerPos = Vector3.zero;
    //     distance = 0f;
    //
    //     if (moveTween != null && moveTween.IsActive())
    //         moveTween.Kill();
    //
    //     transform.DOKill();
    //
    //     if (agent != null)
    //     {
    //         agent.ResetPath();
    //         agent.velocity = Vector3.zero;
    //         agent.isStopped = false;
    //     }
    // }
    //
    // MỚI:
    // Điều chỉnh: NavMesh dừng khi Patrol để không tranh quyền Transform với DOTween.
    // Chỉ Chase/Investigation mới mở lại NavMesh.
    public virtual void ResetRuntimeState()
    {
        isChasing = false;
        isInvestigating = false;
        playerPos = Vector3.zero;
        distance = 0f;

        if (moveTween != null && moveTween.IsActive())
            moveTween.Kill();

        transform.DOKill();

        StopNavigation();
    }

    public virtual void ChasePlayer()
    {
        // Base GuardController không tự Chase.
        // MeleeGuardController override hàm này để sử dụng NavMesh.
    }

    protected virtual void Attack()
    {
    }
}
