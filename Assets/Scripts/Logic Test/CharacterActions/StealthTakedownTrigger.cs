using UnityEngine;

/// <summary>
/// Trigger con đặt trước mặt Player để phát hiện Enemy có thể Stealth Take Down.
///
/// Setup:
/// - Gắn Collider (Is Trigger = true) vào child của Player.
/// - Gắn script này vào đúng child đó.
/// - StealthTakedownController trên Player sẽ nhận Enemy root từ trigger.
///
/// Tham chiếu:
/// - StealthTakedownController
/// - EnemyCheckpointHandler
/// </summary>
[RequireComponent(typeof(Collider))]
public class StealthTakedownTrigger : MonoBehaviour
{
    [SerializeField] private StealthTakedownController controller;
    private Collider triggerCollider;

    private void Reset()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;
    }

    private void Awake()
    {
        if (controller == null)
            controller = GetComponentInParent<StealthTakedownController>();

        triggerCollider = GetComponent<Collider>();

        if (triggerCollider != null)
            triggerCollider.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        TryRegister(other);
    }

    private void OnTriggerStay(Collider other)
    {
        // Chức năng mới: giữ detection ổn định nếu Enemy vừa restore hoặc vừa bật lại collider.
        TryRegister(other);
    }

    private void OnTriggerExit(Collider other)
    {
        if (controller == null)
            return;

        EnemyCheckpointHandler handler =
            ResolveEnemyHandler(other);

        if (handler != null)
            controller.UnregisterTarget(handler);
    }

    public bool IsTargetCurrentlyInside(
        EnemyCheckpointHandler target)
    {
        if (target == null ||
            triggerCollider == null ||
            !triggerCollider.enabled ||
            !triggerCollider.gameObject.activeInHierarchy)
        {
            return false;
        }

        Collider[] targetColliders =
            target.GetComponentsInChildren<Collider>(true);

        for (int i = 0; i < targetColliders.Length; i++)
        {
            Collider targetCollider =
                targetColliders[i];

            if (targetCollider == null ||
                targetCollider.isTrigger ||
                !targetCollider.enabled ||
                !targetCollider.gameObject.activeInHierarchy)
            {
                continue;
            }

            try
            {
                if (Physics.ComputePenetration(
                        triggerCollider,
                        triggerCollider.transform.position,
                        triggerCollider.transform.rotation,
                        targetCollider,
                        targetCollider.transform.position,
                        targetCollider.transform.rotation,
                        out _,
                        out _
                    ))
                {
                    return true;
                }
            }
            catch
            {
                if (triggerCollider.bounds.Intersects(
                        targetCollider.bounds
                    ))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void TryRegister(Collider other)
    {
        if (controller == null)
            return;

        EnemyCheckpointHandler handler =
            ResolveEnemyHandler(other);

        if (handler != null)
            controller.RegisterTarget(handler);
    }

    private EnemyCheckpointHandler ResolveEnemyHandler(Collider other)
    {
        if (other == null)
            return null;

        Transform root = other.transform.root;
        if (root == null)
            return null;

        EnemyCheckpointHandler handler =
            root.GetComponent<EnemyCheckpointHandler>();

        if (handler == null &&
            !root.CompareTag("Enemy"))
        {
            return null;
        }

        return handler;
    }
}
