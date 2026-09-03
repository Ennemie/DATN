using UnityEngine;

public class PistolShooting : MonoBehaviour
{
    public static PistolShooting Instance { get; private set; }

    private Animator animator;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }

        animator = GetComponentInChildren<Animator>();
        animator.SetBool("isShooting", false);
    }
    public void Shoot()
    {
        if (!animator.GetBool("isShooting"))
        {
            animator.SetBool("isShooting", true);
        }
    }
    public void StopShooting()
    {
        PlayerController.Instance.isAttacking = false;
        PlayerState.Instance.CurrentState = PlayerState.State.Idle;
        animator.SetBool("isShooting", false);
    }
}
