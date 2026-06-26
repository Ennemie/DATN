using UnityEngine;
using DG.Tweening;
using UnityEngine.AI;

public class MeleeGuardController : GuardController
{
    private bool isReadyToAttack = false;
    [HideInInspector] public bool _isReadyToAttack
    { 
        get { return isReadyToAttack; } 
        set
        {
            if(isReadyToAttack != value)
            {
                isReadyToAttack = value;
                if(isReadyToAttack)
                {
                    Attack();
                }
                else
                {
                    attackSequence.Kill();
                }
            }
        }
    }
    protected override void Start()
    {
        base.Start();
        agent.stoppingDistance = 1.3f;
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
                guardState.state = GuardState.State.Running;
            }
            agent.SetDestination(player.transform.position);
        }
        Debug.Log("isReadyToAttack: " + isReadyToAttack);
    }
    public override void ChasePlayer()
    {
        FocusOnPlayer();
        isChasing = true;
    }
    protected override void Attack()
    {
        Debug.Log("Attack");
        attackSequence = DOTween.Sequence();
        attackSequence.AppendCallback(() =>
        {
            guardState.state = GuardState.State.Punching;
            DOVirtual.DelayedCall(1.267f, () =>
            {
                guardState.state = GuardState.State.WalkingBackward;
                transform.DOMove(transform.position - Vector3.back, 0.867f)
                    .SetLoops(2, LoopType.Incremental)
                    .SetEase(Ease.Linear)
                    .OnComplete(() =>
                    {                    
                        _isReadyToAttack = false;
                    });
            });
        });
    }
}
