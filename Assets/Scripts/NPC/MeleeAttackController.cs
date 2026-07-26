using UnityEngine;

public class MeleeAttackController : MonoBehaviour
{
    private MeleeGuardController meleeGuardController;
    private bool isAttack = false;

    void Start()
    {
        meleeGuardController = GetComponentInParent<MeleeGuardController>();
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            meleeGuardController.isPlayerOnAttackRange = true;
            meleeGuardController._isReadyToAttack = true;
        }
    }
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            meleeGuardController.isPlayerOnAttackRange = false;
        }
    }
}
