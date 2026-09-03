using System.Collections;
using UnityEngine;

public class PlayerState : MonoBehaviour
{
    public static PlayerState Instance { get; private set; }
    private Animator animator;

    // CŨ:
    // public enum State
    // {
    //     Idle,
    //     Crouching,
    //     Running,
    //     Attack,
    //     Talking,
    //     Interacting,
    //     StealthAttack,
    //     Dead
    // }
    //
    // MỚI:
    // Thêm StealthTakeDown làm state độc quyền để PlayerController không
    // tự ghi đè animation/logic khi Player đang thực hiện cặp animation.
    public enum State
    {
        Idle,
        Crouching,
        Running,
        Attack,
        Talking,
        Interacting,
        StealthAttack,
        StealthTakeDown,
        MeleeCombatAttack,
        MeleeCombatHit,
        Dead
    }

    // CŨ:
    // return _currentState == State.Talking ||
    //        _currentState == State.Interacting ||
    //        _currentState == State.StealthAttack ||
    //        _currentState == State.Dead;
    //
    // MỚI:
    // StealthTakeDown được coi là Exclusive Action State.
    // Tham chiếu: StealthTakedownController + PlayerController.
    public bool IsExclusiveActionState
    {
        get
        {
            return _currentState == State.Talking ||
                   _currentState == State.Interacting ||
                   _currentState == State.StealthAttack ||
                   _currentState == State.StealthTakeDown ||
                   _currentState == State.MeleeCombatAttack ||
                   _currentState == State.MeleeCombatHit ||
                   _currentState == State.Dead;
        }
    }
    private State _currentState = State.Idle;

    public State CurrentState
    {
        get => _currentState;
        set
        {
            if (_currentState == value) return;
            _currentState = value;
            UpdateAnimation();
        }
    }

    private void Awake()
    {
        if(Instance == null) {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
        animator = GetComponent<Animator>();
    }

    // Chức năng mới: các helper state dùng cho Interaction / Chốc thuốc / Death.
    // Tham chiếu:
    // - MissionInteractObjective
    // - PlayerController
    // - PlayerState
    // Dùng để các gameplay system mới yêu cầu animation mà không phải thao tác trực tiếp Animator.
    public void BeginInteracting()
    {
        CurrentState = State.Interacting;
    }

    public void EndInteracting()
    {
        if (CurrentState != State.Interacting)
            return;

        CurrentState = State.Idle;
    }

    public void BeginStealthAttack()
    {
        CurrentState = State.StealthAttack;
    }

    public void EndStealthAttack()
    {
        if (CurrentState != State.StealthAttack)
            return;

        CurrentState = State.Idle;
    }

    public bool BeginMeleeCombatAttack(
        string animatorStateName,
        float playbackSpeed)
    {
        if (!CanUseMeleeAnimator())
            return false;

        CurrentState = State.MeleeCombatAttack;
        return PlayMeleeState(
            animatorStateName,
            playbackSpeed
        );
    }

    public bool BeginMeleeCombatHit(
        string animatorStateName,
        float playbackSpeed)
    {
        if (!CanUseMeleeAnimator())
            return false;

        CurrentState = State.MeleeCombatHit;
        return PlayMeleeState(
            animatorStateName,
            playbackSpeed
        );
    }

    public void EndMeleeCombatAction()
    {
        if (CurrentState != State.MeleeCombatAttack &&
            CurrentState != State.MeleeCombatHit)
        {
            return;
        }

        if (animator != null)
            animator.speed = 1f;

        CurrentState = State.Idle;
    }

    private bool CanUseMeleeAnimator()
    {
        if (animator == null ||
            !animator.enabled ||
            animator.runtimeAnimatorController == null)
        {
            Debug.LogError(
                "[PlayerState] Cannot start melee presentation: Animator unavailable.",
                this
            );

            return false;
        }

        return true;
    }

    private bool PlayMeleeState(
        string animatorStateName,
        float playbackSpeed)
    {
        if (string.IsNullOrWhiteSpace(animatorStateName))
            return false;

        try
        {
            animator.speed =
                Mathf.Max(
                    0.01f,
                    playbackSpeed
                );

            animator.Play(
                animatorStateName,
                0,
                0f
            );

            animator.Update(0f);
        }
        catch (System.Exception ex)
        {
            Debug.LogError(
                "[PlayerState] Melee Animator.Play failed | State=" +
                animatorStateName +
                " | Exception=" + ex.Message,
                this
            );

            animator.speed = 1f;
            CurrentState = State.Idle;
            return false;
        }

        AnimatorStateInfo info =
            animator.GetCurrentAnimatorStateInfo(0);

        bool accepted =
            info.IsName(animatorStateName) ||
            info.shortNameHash ==
                Animator.StringToHash(animatorStateName);

        if (!accepted)
        {
            Debug.LogError(
                "[PlayerState] Melee Animator state rejected | Requested=" +
                animatorStateName +
                " | CurrentShortHash=" +
                info.shortNameHash +
                " | CurrentFullHash=" +
                info.fullPathHash,
                this
            );

            animator.speed = 1f;
            CurrentState = State.Idle;
            return false;
        }

        return true;
    }

    // CŨ:
    // public bool BeginStealthTakeDown() dùng Animator.HasState(shortNameHash)
    // để chặn trước khi Play.
    //
    // MỚI:
    // Phát trực tiếp state StealthTakeDown từ frame 0 giống EnemyCheckpointHandler.
    // Sau Play() + Update(0f), kiểm tra state thực tế để tránh false-negative do
    // short-name hash. Nếu Play không vào đúng state thì rollback về Idle.
    // Tham chiếu: StealthTakedownController + EnemyCheckpointHandler.
    public bool BeginStealthTakeDown()
    {
        const string stealthStateName = "StealthTakeDown";

        if (animator == null)
        {
            Debug.LogError(
                "[PlayerState] Animator không tồn tại, không thể bắt đầu StealthTakeDown.",
                this
            );
            return false;
        }

        if (!animator.enabled)
        {
            Debug.LogError(
                "[PlayerState] Animator đang disabled, không thể bắt đầu StealthTakeDown.",
                this
            );
            return false;
        }

        if (animator.runtimeAnimatorController == null)
        {
            Debug.LogError(
                "[PlayerState] runtimeAnimatorController = NULL, không thể bắt đầu StealthTakeDown.",
                this
            );
            return false;
        }

        // MỚI:
        // Chuyển PlayerState sang StealthTakeDown trước, nhưng không tin rằng
        // UpdateAnimation đã chọn đúng state. Play trực tiếp lại ngay sau đó.
        CurrentState = State.StealthTakeDown;

        animator.speed = 1f;

        try
        {
            animator.Play(
                stealthStateName,
                0,
                0f
            );

            animator.Update(0f);
        }
        catch (System.Exception ex)
        {
            Debug.LogError(
                "[PlayerState] StealthTakeDown Animator.Play failed: " +
                ex.Message,
                this
            );

            CurrentState = State.Idle;
            return false;
        }

        AnimatorStateInfo stateInfo =
            animator.GetCurrentAnimatorStateInfo(0);

        bool stateAccepted =
            stateInfo.IsName(stealthStateName) ||
            stateInfo.shortNameHash ==
                Animator.StringToHash(stealthStateName);

        if (!stateAccepted)
        {
            Debug.LogError(
                "[PlayerState] StealthTakeDown Animator.Play không đưa Player vào đúng state | " +
                "CurrentFullPathHash=" + stateInfo.fullPathHash +
                " | CurrentShortNameHash=" + stateInfo.shortNameHash,
                this
            );

            CurrentState = State.Idle;
            return false;
        }

        if (PlayerController.Instance != null)
            PlayerController.Instance.acceptInput = false;

        Debug.Log(
            "[PlayerState] STEALTH ANIMATION STARTED | " +
            "State=" + stealthStateName +
            " | Layer=0" +
            " | NormalizedTime=" + stateInfo.normalizedTime.ToString("0.000") +
            " | FullPathHash=" + stateInfo.fullPathHash +
            " | ShortNameHash=" + stateInfo.shortNameHash,
            this
        );

        return true;
    }

    // Chức năng mới:
    // Kết thúc Stealth Take Down ngay tại frame 210.
    // Player chuyển về Idle và PlayerController được phép nhận input trở lại.
    public void EndStealthTakeDown()
    {
        if (CurrentState != State.StealthTakeDown)
            return;

        CurrentState = State.Idle;
    }

    // Hàm này sẽ được gọi từ PlayerWeapon hoặc mỗi khi bạn đổi vũ khí
    public void UpdateAnimation()
    {
        if (animator == null) return;

        // Performative melee owns Attack1/Attack2/GetHit directly.
        // The generic weapon animation switch must not overwrite these states.
        if (CurrentState == State.MeleeCombatAttack ||
            CurrentState == State.MeleeCombatHit)
        {
            return;
        }

        // Chức năng mới:
        // Chỉ Dead chạy x2 tốc độ. Mọi state còn lại giữ tốc độ 1 như Animator gốc.
        // CŨ:
        // Animator.speed không bị thay đổi tại đây.
        //
        // MỚI:
        animator.speed = CurrentState == State.Dead ? 2f : 1f;

        // Lấy vũ khí hiện tại từ Singleton PlayerWeapon
        var weapon = PlayerWeapon.Instance.CurrentWeapon;

        switch (weapon)
        {
            case PlayerWeapon.WeaponType.Fist:
                PlayFistAnim(CurrentState);
                break;
            case PlayerWeapon.WeaponType.Knife:
                PlayKnifeAnim(CurrentState);
                break;
            case PlayerWeapon.WeaponType.Pistol:
                PlayPistolAnim(CurrentState);
                break;
            case PlayerWeapon.WeaponType.Shotgun:
                PlayShotgunAnim(CurrentState);
                break;
        }
    }

    private void PlayFistAnim(State state)
    {
        if (state == State.Idle) animator.CrossFade("Fist_Idle", 0.1f);
        else if (state == State.Crouching) animator.CrossFade("Crouched_Walk", 0.02f);
        else if (state == State.Running) animator.CrossFade("Fist_Running", 0.01f);
        else if (state == State.Attack)
        {
            animator.CrossFade("Fist_Punch", 0.01f);
            StartCoroutine(ResetAttackState(1f));
        }
        else if (state == State.Talking)
        {
            animator.CrossFade("Talk", 0.1f);
            PlayerController.Instance.acceptInput = false;
        }
        // Chức năng mới: Interaction dùng chung một animation cho mọi loại vũ khí.
        else if (state == State.Interacting)
        {
            animator.CrossFade("Interacting", 0.05f);
            PlayerController.Instance.acceptInput = false;
        }
        // Chức năng mới: Chốc thuốc dùng chung một animation cho mọi loại vũ khí.
        else if (state == State.StealthAttack)
        {
            animator.CrossFade("Stealth_Attack", 0.05f);
            PlayerController.Instance.acceptInput = false;
        }
        // CŨ:
        // else if (state == State.StealthTakeDown)
        // {
        //     animator.Play("StealthTakeDown", 0, 0f);
        //     PlayerController.Instance.acceptInput = false;
        // }
        //
        // MỚI:
        // StealthTakeDown được StartStealthTakeDown() phát trực tiếp để kiểm tra
        // state thành công. UpdateAnimation chỉ giữ animation state tại đây cho
        // tương thích với CurrentState setter; không dùng HasState làm gate.
        else if (state == State.StealthTakeDown)
        {
            animator.Play(
                "StealthTakeDown",
                0,
                0f
            );

            if (PlayerController.Instance != null)
                PlayerController.Instance.acceptInput = false;
        }
        // Chức năng mới: Death dùng chung một animation cho mọi loại vũ khí.
        else if (state == State.Dead)
        {
            animator.CrossFade("Dead", 0.05f);
            PlayerController.Instance.acceptInput = false;
        }
    }

    private void PlayKnifeAnim(State state)
    {
        if (state == State.Idle) animator.CrossFade("Knife_Idle", 0.05f);
        else if (state == State.Crouching) animator.CrossFade("Crouched_Walk", 0.02f);
        else if (state == State.Running) animator.CrossFade("Fist_Running", 0.01f);
        else if (state == State.Attack)
        {
            animator.CrossFade("Knife_Stab", 0.1f);
            StartCoroutine(ResetAttackState(1.3f));
        }
        else if(state == State.Talking) 
        {
            animator.CrossFade("Knife_Idle", 0.1f);
            PlayerController.Instance.acceptInput = false;
        }
        // Chức năng mới: Interaction dùng chung một animation cho mọi loại vũ khí.
        else if (state == State.Interacting)
        {
            animator.CrossFade("Interacting", 0.05f);
            PlayerController.Instance.acceptInput = false;
        }
        // Chức năng mới: Chốc thuốc dùng chung một animation cho mọi loại vũ khí.
        else if (state == State.StealthAttack)
        {
            animator.CrossFade("Stealth_Attack", 0.05f);
            PlayerController.Instance.acceptInput = false;
        }
        // Chức năng mới:
        // Stealth Take Down dùng cùng một animation state cho mọi loại vũ khí.
        // Play trực tiếp từ frame 0 để đồng bộ với EnemyCheckpointHandler.
        // Tham chiếu: StealthTakedownController.
        else if (state == State.StealthTakeDown)
        {
            animator.Play(
                "StealthTakeDown",
                0,
                0f
            );

            PlayerController.Instance.acceptInput = false;
        }
        // Chức năng mới: Death dùng chung một animation cho mọi loại vũ khí.
        else if (state == State.Dead)
        {
            animator.CrossFade("Dead", 0.05f);
            PlayerController.Instance.acceptInput = false;
        }
    }

    private void PlayPistolAnim(State state)
    {
        if (state == State.Idle) animator.CrossFade("Pistol_Idle", 0.05f);
        else if (state == State.Crouching) animator.CrossFade("Crouched_Walk", 0.02f);
        else if (state == State.Running) animator.CrossFade("Pistol_Running", 0.1f);
        else if(state == State.Talking) 
        {
            animator.CrossFade("Pistol_Idle", 0.1f);
            PlayerController.Instance.acceptInput = false;
        }
        // Chức năng mới: Interaction dùng chung một animation cho mọi loại vũ khí.
        else if (state == State.Interacting)
        {
            animator.CrossFade("Interacting", 0.05f);
            PlayerController.Instance.acceptInput = false;
        }
        // Chức năng mới: Chốc thuốc dùng chung một animation cho mọi loại vũ khí.
        else if (state == State.StealthAttack)
        {
            animator.CrossFade("Stealth_Attack", 0.05f);
            PlayerController.Instance.acceptInput = false;
        }
        // Chức năng mới:
        // Stealth Take Down dùng cùng một animation state cho mọi loại vũ khí.
        // Play trực tiếp từ frame 0 để đồng bộ với EnemyCheckpointHandler.
        // Tham chiếu: StealthTakedownController.
        else if (state == State.StealthTakeDown)
        {
            animator.Play(
                "StealthTakeDown",
                0,
                0f
            );

            PlayerController.Instance.acceptInput = false;
        }
        // Chức năng mới: Death dùng chung một animation cho mọi loại vũ khí.
        else if (state == State.Dead)
        {
            animator.CrossFade("Dead", 0.05f);
            PlayerController.Instance.acceptInput = false;
        }
    }

    private void PlayShotgunAnim(State state)
    {
        if (state == State.Idle)
        {
            animator.CrossFade("Shotgun_Idle", 0.1f);
            PlayerWeapon.Instance.UpdateShotgunAnimation("Idle");
        }
        else if (state == State.Crouching)
        {
            animator.CrossFade("Crouched_Walk", 0.02f);
            PlayerWeapon.Instance.UpdateShotgunAnimation("Crouch");
        }
        else if (state == State.Running)
        {
            animator.CrossFade("Shotgun_Running", 0.1f);
            PlayerWeapon.Instance.UpdateShotgunAnimation("Running");
        }
        else if (state == State.Attack)
        {
            animator.CrossFade("Shotgun_Shoot", 0.01f);
            PlayerWeapon.Instance.UpdateShotgunAnimation("Shooting");
            StartCoroutine(ResetAttackState(1.1f));
        }
        else if(state == State.Talking)
        {
            animator.CrossFade("Shotgun_Idle", 0.1f);
            PlayerWeapon.Instance.UpdateShotgunAnimation("Idle");
            PlayerController.Instance.acceptInput = false;
        }
        // Chức năng mới: Interaction dùng chung một animation cho mọi loại vũ khí.
        else if (state == State.Interacting)
        {
            animator.CrossFade("Interacting", 0.05f);
            PlayerController.Instance.acceptInput = false;
        }
        // Chức năng mới: Chốc thuốc dùng chung một animation cho mọi loại vũ khí.
        else if (state == State.StealthAttack)
        {
            animator.CrossFade("Stealth_Attack", 0.05f);
            PlayerController.Instance.acceptInput = false;
        }
        // Chức năng mới:
        // Stealth Take Down dùng cùng một animation state cho mọi loại vũ khí.
        // Play trực tiếp từ frame 0 để đồng bộ với EnemyCheckpointHandler.
        // Tham chiếu: StealthTakedownController.
        else if (state == State.StealthTakeDown)
        {
            animator.Play(
                "StealthTakeDown",
                0,
                0f
            );

            PlayerController.Instance.acceptInput = false;
        }
        // Chức năng mới: Death dùng chung một animation cho mọi loại vũ khí.
        else if (state == State.Dead)
        {
            animator.CrossFade("Dead", 0.05f);
            PlayerWeapon.Instance.UpdateShotgunAnimation("Idle");
            PlayerController.Instance.acceptInput = false;
        }
    }

    // Cũ:
    // private IEnumerator ResetAttackState(float delay)
    // {
    //     yield return new WaitForSeconds(delay);
    //     PlayerController.Instance.isAttacking = false;
    //     CurrentState = State.Idle;
    // }
    //
    // Mới:
    // Điều chỉnh: tránh coroutine attack đến trễ ép Player về Idle sau khi state mới đã tiếp quản.
    // such as Interacting, StealthAttack, or Dead has already taken ownership.
    private IEnumerator ResetAttackState(float delay)
    {
        yield return new WaitForSeconds(delay);

        PlayerController.Instance.isAttacking = false;

        if (!IsExclusiveActionState)
            CurrentState = State.Idle;
    }
}