using DG.Tweening;
using TMPro;
using Unity.AppUI.Core;
using UnityEngine;

public class GuardState : MonoBehaviour
{

    // Movement
    private Vector3 guardStartPoint;
    private Vector3 guardEndPoint;
    private float walkSpeed = 1.5f;
    private float runSpeed = 4f;


    private Animator anim;
    public enum State
    {
        Idle,
        FightIdle,
        Guarding,
        Running,
        Punching,
        TakingPunch,
        Stunned,
        Dying,
        StealthDying
    }
    public State _state;
    public State state
    {
        get { return _state; }
        set
        {
            if (_state != value)
            {
                _state = value;
                UpdateBehaviour(value);
            }
        }
    }

    private void Start()
    {
        anim = GetComponent<Animator>();
        guardStartPoint = transform.position;
        guardEndPoint = transform.Find("GuardEndPoint").position;
    }
    private void UpdateBehaviour(State newState)
    {
        switch(newState) {
            case State.Idle:
                anim.CrossFade("Idle", 0.1f);
                break;
            case State.FightIdle:
                anim.CrossFade("FightIdle", 0.1f);
                break;
            case State.Guarding:
                Guarding();
                break;
            case State.Running:
                anim.CrossFade("Running", 0.1f);
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
                anim.CrossFade("StealthDying", 0.1f);
                break;
        }
    }
    private void Guarding()
    {
        if(transform.position != guardStartPoint) GuardingToStart();
        else GuradingToEnd();

    }
    private void GuardingToStart()
    {
        transform.DOLookAt(guardStartPoint, 0.3f);
        anim.CrossFade("Walking", 0.1f);
        transform.DOMove(guardStartPoint, walkSpeed)
            .SetSpeedBased()
            .SetEase(Ease.Linear)
            .OnComplete(() =>
            {
                anim.CrossFade("Idle", 0.1f);
                DOVirtual.DelayedCall(2f, GuradingToEnd);
            });
    }
    private void GuradingToEnd()
    {
        transform.DOLookAt(guardEndPoint, 0.3f);
        anim.CrossFade("Walking", 0.1f);
        transform.DOMove(guardEndPoint, walkSpeed)
            .SetSpeedBased()
            .SetEase(Ease.Linear)
            .OnComplete(() =>
            {
                anim.CrossFade("Idle", 0.1f);
                DOVirtual.DelayedCall(2f, GuardingToStart);
            });
    }
}
