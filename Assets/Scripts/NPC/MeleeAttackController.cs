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
            meleeGuardController._isReadyToAttack = true;
        }
    }
    private void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            
        }
    }
}
