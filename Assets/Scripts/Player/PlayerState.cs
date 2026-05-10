using System.Collections;
using UnityEngine;

public class PlayerState : MonoBehaviour
{
    public static PlayerState Instance { get; private set; }
    private Animator animator;

    // 3 Trạng thái
    public enum State { Idle, Running, Attack }
    private State _currentState = State.Idle;

    public State CurrentState
    {
        get => _currentState;
        set
        {
            if (_currentState == value) return;
            _currentState = value;
            UpdateAnimation(); // Cập nhật khi đổi trạng thái
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        animator = GetComponent<Animator>();
    }

    // Hàm này sẽ được gọi từ PlayerWeapon hoặc mỗi khi bạn đổi vũ khí
    public void UpdateAnimation()
    {
        if (animator == null) return;

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
        else if (state == State.Running) animator.CrossFade("Fist_Running", 0.01f);
        else if (state == State.Attack)
        {
            animator.CrossFade("Fist_Punch", 0.01f);
            StartCoroutine(ResetAttackState(1f));
        }
    }

    private void PlayKnifeAnim(State state)
    {
        if (state == State.Idle) animator.CrossFade("Knife_Idle", 0.05f);
        else if (state == State.Running) animator.CrossFade("Fist_Running", 0.01f);
        else if (state == State.Attack)
        {
            animator.CrossFade("Knife_Stab", 0.1f);
            StartCoroutine(ResetAttackState(1.3f));
        }
    }

    private void PlayPistolAnim(State state)
    {
        if (state == State.Idle) animator.CrossFade("Pistol_Idle", 0.1f);
        else if (state == State.Running) animator.CrossFade("Pistol_Running", 0.1f);
    }

    private void PlayShotgunAnim(State state)
    {
        if (state == State.Idle)
        {
            animator.CrossFade("Shotgun_Idle", 0.1f);
            PlayerWeapon.Instance.UpdateShotgunAnimation("Idle");
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
    }

    private IEnumerator ResetAttackState(float delay)
    {
        yield return new WaitForSeconds(delay);
        PlayerController.Instance.isAttacking = false;
        CurrentState = State.Idle;
    }
}