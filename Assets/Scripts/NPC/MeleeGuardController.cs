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
    [HideInInspector] public bool _isReadyToAttack
    { 
        get { return isReadyToAttack; } 
        set
        {
            if(isReadyToAttack != value)
            {
                isReadyToAttack = value;
                if(isReadyToAttack && !isAttacking && isPlayerOnAttackRange)
                {
                    Attack();
                }
            }
        }
    }
    protected override void Start()
    {
        base.Start();
        agent.stoppingDistance = 1.3f;
        isPlayerOnAttackRange = false;
    }
    void LateUpdate()
    {
        if(isChasing)
        {
            if(isReadyToAttack) return;

            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
            {
                guardState.state = GuardState.State.FightIdle;
            }
            else
            {
                if(guardState.state != GuardState.State.Running)
                {
                    guardState.state = GuardState.State.Running;
                }
            }
            agent.SetDestination(player.transform.position);
        }
    }
    public override void ChasePlayer()
    {
        FocusOnPlayer();
        isChasing = true;
    }
    protected override void Attack()
    {
        if(guardState.isDead) return;
        guardState.state = GuardState.State.Punching;

        if(guardState.isDead) return;
        DOVirtual.DelayedCall(1f, () =>
        {
            if (isPlayerOnAttackRange)
            {
                PlayerProperties.Instance.TakeDamage(10);
            }
        });

        if(guardState.isDead) return;
        DOVirtual.DelayedCall(1.267f, () =>
        {
            isAttacking = false;
            _isReadyToAttack = false;
            guardState.state = GuardState.State.FightIdle;
        });

        if(guardState.isDead) return;
        DOVirtual.DelayedCall(1.267f + attackCoolDown, () =>
        {
            if(isPlayerOnAttackRange)
            {
                _isReadyToAttack = true;
            }
        });
    }
}
