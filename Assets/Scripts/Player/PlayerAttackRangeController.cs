using UnityEngine;

public class PlayerAttackRangeController : MonoBehaviour
{
    public static PlayerAttackRangeController Instance { get; private set; }
    private GuardController guardController = null;
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
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy"))
        {
            other.TryGetComponent<GuardController>(out guardController);
        }
    }
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Enemy"))
        {
            guardController = null;
        }
    }
    public void HitEnemy()
    {
        if (guardController != null)
        {
            guardController.TakeDamage(20);
        }
    }
}
