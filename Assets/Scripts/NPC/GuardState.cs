using DG.Tweening;
using TMPro;
using Unity.AppUI.Core;
using UnityEngine;

public class GuardState : MonoBehaviour
{
    private enum guardType {Melee, Shooter}
    [SerializeField] private guardType _guardType;
    private GuardController guardController;

    private GuardVisionView guardFOV;
    private MeshRenderer guardFOVRenderer;
    protected GameObject player;
    private bool isDetectSoundWave = false;
    [HideInInspector] public bool isDetectPlayer = false;
    
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
            if(_state == State.DetectSoundWave)
            {
                ClearAllTweens(); 
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

    private void Start()
    {
        anim = GetComponent<Animator>();
        guardStartPoint = transform.position;
        guardEndPoint = transform.Find("GuardEndPoint").position;
        player = GameObject.FindGameObjectWithTag("Player");
        guardFOV = GetComponent<GuardVisionView>();
        guardFOVRenderer = GetComponent<MeshRenderer>();
        state = State.Guarding;
        switch(_guardType)
        {
            case guardType.Melee:
                guardController = GetComponent<MeleeGuardController>();
                break;
            case guardType.Shooter:
                guardController = GetComponent<GuardController>();
                break;
        }
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
                anim.CrossFade("StealthDying", 0.1f);
                break;
        }
    }

    private void Guarding()
    {
        // Kiểm tra khoảng cách gần đúng thay vì so sánh bằng tuyệt đối "==" của Vector3 (tránh sai số float)
        if(Vector3.Distance(transform.position, guardStartPoint) > 0.1f) 
            GuardingToStart();
        else 
            GuradingToEnd();
    }

    private void GuardingToStart()
    {
        ClearAllTweens();

        // Đồng bộ trục Y khi nhìn điểm xuất phát
        Vector3 lookTarget = new Vector3(guardStartPoint.x, transform.position.y, guardStartPoint.z);

        activeTween = transform.DOLookAt(lookTarget, 0.5f).OnComplete(() =>
        {
            anim.CrossFade("Walking", 0.1f);
            activeTween = transform.DOMove(guardStartPoint, walkSpeed)
                .SetSpeedBased()
                .SetEase(Ease.Linear)
                .OnComplete(() =>
                {
                    anim.CrossFade("Idle", 0.1f);
                    // Lưu DelayedCall vào activeTween để có thể hủy nếu bị đánh động giữa chừng
                    activeTween = DOVirtual.DelayedCall(2f, GuradingToEnd);
                });
        });
    }

    private void GuradingToEnd()
    {
        ClearAllTweens();

        // Đồng bộ trục Y khi nhìn điểm kết thúc
        Vector3 lookTarget = new Vector3(guardEndPoint.x, transform.position.y, guardEndPoint.z);

        activeTween = transform.DOLookAt(lookTarget, 0.5f).OnComplete(() =>
        {
            anim.CrossFade("Walking", 0.1f);
            activeTween = transform.DOMove(guardEndPoint, walkSpeed)
                .SetSpeedBased()
                .SetEase(Ease.Linear)
                .OnComplete(() =>
                {
                    anim.CrossFade("Idle", 0.1f);
                    // Lưu DelayedCall vào activeTween
                    activeTween = DOVirtual.DelayedCall(2f, GuardingToStart);
                });
        });
    }

    private void DetectSoundWave()
    {
        isDetectSoundWave = true;
        anim.CrossFade("Idle", 0.1f);
        FocusOnPlayer();
    }
    private void FocusOnPlayer()
    {
        Vector3 targetPosition = new Vector3(player.transform.position.x, transform.position.y, player.transform.position.z);
        activeTween = transform.DOLookAt(targetPosition, 0.5f).OnComplete(() =>
        {
            activeTween =DOVirtual.DelayedCall(5f, () =>
            {
                isDetectSoundWave = false;
                if(!isDetectPlayer) 
                {
                    state = State.Guarding;
                }
            });
        });
    }

    // Hàm bổ trợ giúp dọn dẹp sạch sẽ mọi Tween/DelayedCall cũ tránh xung đột
    private void ClearAllTweens()
    {
        if (activeTween != null && activeTween.IsActive())
        {
            activeTween.Kill();
        }
        transform.DOKill(); // Diệt thêm các tween vãng lai bám trên transform (nếu có)
    }
    private void DetectPlayer()
    {
        ClearAllTweens();
        isDetectPlayer = true;
        ActiveFOV(false);
        anim.CrossFade("FightIdle", 0.1f);
        guardController.ChasePlayer();
    }
    private void ActiveFOV(bool isActive)
    {
        guardFOVRenderer.enabled = isActive;
        guardFOV.enabled = isActive;
    }
}