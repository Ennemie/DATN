using System.Collections;
using UnityEngine;
using DG.Tweening;
using TreeEditor;

public class CommanderController : MonoBehaviour
{
    public static CommanderController Instance { get; private set; }

    private Animator animator;
    [HideInInspector]public enum State
    {
        Sitting,
        StandUp,
        Idle,
        Talking
    }
    private State _state;
    public State state
    {
        get
        {
            return _state;
        }
        set
        {
            if (_state == value) return;
            _state = value;
            switch (_state)
            {
                case State.Sitting:
                    animator.CrossFade("Sitting", 0.1f);
                    break;
                case State.StandUp:
                    animator.CrossFade("Stand up", 0.1f);
                    StartCoroutine(StandUpToTalk());
                    break;
                case State.Idle:
                    animator.CrossFade("Idle", 0.5f);
                    break;
                case State.Talking:
                    animator.CrossFade("Talk", 0.1f);
                    break;
            }
        }
    }
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    private void Start()
    {
        state = State.Sitting;
        animator = GetComponent<Animator>();
    }
    private IEnumerator StandUpToTalk()
    {
        yield return new WaitForSeconds(0.6f);
        LookAtPlayer();
        state = State.Talking;
    }
    public void LookAtPlayer()
    {
        // Lấy vị trí người chơi
        Vector3 targetPos = PlayerProperties.Instance.transform.position;

        // Triệt tiêu trục Y để chỉ xoay quanh trục đứng (giống direction.y = 0)
        targetPos.y = transform.position.y;

        // Kiểm tra khoảng cách để tránh rung lắc khi quá gần
        if (Vector3.Distance(transform.position, targetPos) > 0.1f)
        {
            // DOLookAt(vị trí, thời gian xoay)
            // .SetEase(Ease.OutQuad) giúp xoay mượt ở điểm dừng
            transform.DOLookAt(targetPos, 0.5f).SetEase(Ease.OutQuad);
        }
    }
}
