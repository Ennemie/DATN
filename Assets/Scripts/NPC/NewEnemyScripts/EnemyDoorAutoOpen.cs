using UnityEngine;

/// <summary>
/// Chức năng mới:
/// Tự động mở DoorV1Controller hoặc DoorV2Controller khi EnemyRoutePerformer
/// đi vào vùng cửa.
///
/// Điều chỉnh:
/// - Hỗ trợ OnTriggerEnter khi Unity Physics phát trigger event.
/// - Có thêm OverlapBox polling để vẫn hoạt động với Enemy di chuyển trực tiếp
///   bằng Transform/MoveTowards mà không cần Rigidbody.
/// - Chỉ kiểm tra Root Enemy có EnemyRoutePerformer.
/// - Không đụng InteractController của Player.
/// </summary>
[RequireComponent(typeof(Collider))]
public class EnemyDoorAutoOpen : MonoBehaviour
{
    [Header("Door")]
    [Tooltip(
        "Nếu để trống, script tự tìm DoorV1Controller/DoorV2Controller trên object hoặc parent."
    )]
    [SerializeField] private DoorV1Controller doorV1;

    [SerializeField] private DoorV2Controller doorV2;

    [Header("Detection")]
    [Tooltip(
        "Chỉ Root Enemy có EnemyRoutePerformer mới được mở cửa."
    )]
    [SerializeField] private bool requireRoutePerformer =
        true;

    [SerializeField] private bool oneTime =
        false;

    [Tooltip(
        "Dùng polling OverlapBox để không phụ thuộc Rigidbody/OnTriggerEnter."
    )]
    [SerializeField] private bool useOverlapPolling =
        true;

    [Min(0.01f)]
    [SerializeField] private float pollingInterval =
        0.05f;

    private Collider triggerCollider;
    private BoxCollider boxCollider;

    private bool hasOpened;
    private float nextPollingTime;

    private readonly Collider[] overlapResults =
        new Collider[32];

    private void Reset()
    {
        triggerCollider =
            GetComponent<Collider>();

        if (triggerCollider != null)
            triggerCollider.isTrigger =
                true;
    }

    private void Awake()
    {
        triggerCollider =
            GetComponent<Collider>();

        boxCollider =
            triggerCollider as BoxCollider;

        if (triggerCollider != null)
            triggerCollider.isTrigger =
                true;

        ResolveDoor();
    }

    private void FixedUpdate()
    {
        if (!useOverlapPolling)
            return;

        if (oneTime && hasOpened)
            return;

        if (Time.fixedTime < nextPollingTime)
            return;

        nextPollingTime =
            Time.fixedTime +
            Mathf.Max(
                0.01f,
                pollingInterval
            );

        CheckOverlapForEnemy();
    }

    private void OnTriggerEnter(
        Collider other)
    {
        if (oneTime && hasOpened)
            return;

        TryOpenFromCollider(other);
    }

    private void OnTriggerStay(
        Collider other)
    {
        if (oneTime && hasOpened)
            return;

        // Fallback thêm cho trường hợp Enemy đi xuyên nhanh qua trigger.
        TryOpenFromCollider(other);
    }

    private void CheckOverlapForEnemy()
    {
        if (boxCollider == null)
            return;

        Vector3 worldCenter =
            boxCollider.transform.TransformPoint(
                boxCollider.center
            );

        Vector3 worldHalfExtents =
            Vector3.Scale(
                boxCollider.size * 0.5f,
                AbsVector(
                    boxCollider.transform.lossyScale
                )
            );

        int hitCount =
            Physics.OverlapBoxNonAlloc(
                worldCenter,
                worldHalfExtents,
                overlapResults,
                boxCollider.transform.rotation,
                Physics.AllLayers,
                QueryTriggerInteraction.Collide
            );

        for (int i = 0;
             i < hitCount;
             i++)
        {
            Collider hit =
                overlapResults[i];

            if (hit == null)
                continue;

            if (TryOpenFromCollider(hit))
                return;
        }
    }

    private bool TryOpenFromCollider(
        Collider other)
    {
        if (other == null)
            return false;

        Transform root =
            other.transform.root;

        if (root == null)
            return false;

        EnemyRoutePerformer performer =
            root.GetComponent<
                EnemyRoutePerformer
            >();

        if (requireRoutePerformer &&
            performer == null)
        {
            return false;
        }

        if (performer == null)
            return false;

        OpenDoor();

        if (doorV1 != null ||
            doorV2 != null)
        {
            hasOpened = true;
            return true;
        }

        return false;
    }

    private void ResolveDoor()
    {
        if (doorV1 == null)
            doorV1 =
                GetComponent<DoorV1Controller>();

        if (doorV1 == null)
            doorV1 =
                GetComponentInParent<
                    DoorV1Controller
                >();

        if (doorV2 == null)
            doorV2 =
                GetComponent<DoorV2Controller>();

        if (doorV2 == null)
            doorV2 =
                GetComponentInParent<
                    DoorV2Controller
                >();
    }

    private void OpenDoor()
    {
        ResolveDoor();

        // Nếu cửa có cả hai script, ưu tiên DoorV1.
        if (doorV1 != null)
        {
            doorV1.isOpen = true;
            return;
        }

        if (doorV2 != null)
        {
            doorV2.isOpen = true;
        }
    }

    private static Vector3 AbsVector(
        Vector3 value)
    {
        return new Vector3(
            Mathf.Abs(value.x),
            Mathf.Abs(value.y),
            Mathf.Abs(value.z)
        );
    }
}
