using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Laser detection zone connected to the current Player/Checkpoint failure pipeline.
///
/// Modes:
/// - Fail Immediately = Player enters the laser -> alarm/failure immediately -> checkpoint.
/// - Damage = Player remains inside -> take X damage every X seconds.
///
/// Failure mode also supports two scene-object lists:
/// - Activate Objects On Failure
/// - Deactivate Objects On Failure
///
/// This script no longer depends on MissionFailManager.
/// It uses PlayerProperties.RequestFailureToCheckpoint(), which is the current
/// Alarm/Failure entry point used by GuardVisionView / EnemyRoutePerformer.
/// </summary>
[RequireComponent(typeof(Collider))]
public class LaserDetectionZone : MonoBehaviour
{
    [Header("Mode")]
    [Tooltip(
        "Bật: Player va chạm Laser -> Fail ngay và về Checkpoint. " +
        "Tắt: Laser gây damage theo interval."
    )]
    [SerializeField] private bool failImmediately = true;

    [Header("Player Damage")]
    [Min(0.01f)]
    [Tooltip("Chỉ dùng khi Fail Immediately = false. Khoảng thời gian giữa hai lần gây damage.")]
    [SerializeField] private float damageInterval = 1f;

    [Min(0)]
    [Tooltip("Chỉ dùng khi Fail Immediately = false. Số HP bị trừ mỗi tick.")]
    [SerializeField] private int damagePerTick = 10;

    [Header("Failure Object State")]
    [Tooltip("Chỉ dùng khi Fail Immediately = true. SetActive(true) khi Failure được kích hoạt.")]
    [SerializeField] private GameObject[] activateObjectsOnFailure;

    [Tooltip("Chỉ dùng khi Fail Immediately = true. SetActive(false) khi Failure được kích hoạt. " +
             "Nếu một object nằm ở cả hai list, list Active được áp dụng sau cùng.")]
    [SerializeField] private GameObject[] deactivateObjectsOnFailure;

    [Header("References")]
    [SerializeField] private PlayerLaserReceiver playerLaserReceiver;

    [Header("Runtime (Read Only)")]
    [SerializeField] private bool playerDetected = false;
    [SerializeField] private bool playerLost = false;
    [SerializeField] private float damageTimer = 0f;

    public bool PlayerDetected => playerDetected;
    public bool PlayerLost => playerLost;

    private Collider zoneCollider;
    private PlayerProperties playerProperties;

    private readonly HashSet<Collider> playerCollidersInside =
        new HashSet<Collider>();

    private void Reset()
    {
        Collider col = GetComponent<Collider>();

        if (col != null)
            col.isTrigger = true;
    }

    private void Awake()
    {
        zoneCollider = GetComponent<Collider>();

        if (zoneCollider != null)
            zoneCollider.isTrigger = true;

        ResolveReferences();
    }

    private void OnEnable()
    {
        PlayerProperties.DeathOrFailureStarted -=
            HandlePlayerDeathOrFailureStarted;

        PlayerProperties.DeathOrFailureStarted +=
            HandlePlayerDeathOrFailureStarted;
    }

    private void OnDisable()
    {
        PlayerProperties.DeathOrFailureStarted -=
            HandlePlayerDeathOrFailureStarted;
    }

    private void Update()
    {
        if (playerLost ||
            playerCollidersInside.Count == 0)
        {
            return;
        }

        ResolveReferences();

        if (playerProperties == null)
            return;

        if (playerProperties.IsDeadOrFailing)
            return;

        if (failImmediately)
            return;

        float interval =
            Mathf.Max(
                0.01f,
                damageInterval
            );

        damageTimer += Time.deltaTime;

        if (damageTimer < interval)
            return;

        while (damageTimer >= interval)
        {
            damageTimer -= interval;

            if (playerProperties == null ||
                playerProperties.IsDeadOrFailing)
            {
                return;
            }

            if (damagePerTick > 0)
            {
                playerProperties.TakeDamage(
                    damagePerTick
                );
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other))
            return;

        if (playerLost)
            return;

        playerCollidersInside.Add(other);
        playerDetected = true;

        if (failImmediately)
        {
            CauseImmediateFailure();
            return;
        }

        // Damage mode:
        // first hit occurs after one full damage interval.
        damageTimer = 0f;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayer(other))
            return;

        playerCollidersInside.Remove(other);

        if (playerCollidersInside.Count == 0)
        {
            playerDetected = false;
            damageTimer = 0f;
        }

        if (playerLaserReceiver != null &&
            playerCollidersInside.Count == 0)
        {
            playerLaserReceiver.ResetLaserState();
        }
    }

    private void CauseImmediateFailure()
    {
        if (playerLost)
            return;

        ResolveReferences();

        if (playerProperties == null)
        {
            Debug.LogWarning(
                "[LaserDetectionZone] PlayerProperties not found. " +
                "Laser cannot start checkpoint failure.",
                this
            );
            return;
        }

        if (playerProperties.IsDeadOrFailing)
            return;

        playerLost = true;
        playerDetected = false;
        damageTimer = 0f;
        playerCollidersInside.Clear();

        Debug.Log(
            "[LaserDetectionZone] ALARM -> Player Failure -> Checkpoint.",
            this
        );

        // Current architecture:
        // PlayerProperties -> CheckpointManager -> domain restore.
        playerProperties.RequestFailureToCheckpoint();

        // Keep the supplied receiver flagged for the failure window.
        // The shared DeathOrFailureStarted event can reset it before this point.
        if (playerLaserReceiver != null)
            playerLaserReceiver.SetDetected(true);

        // Detector-specific scene object reaction happens after failure starts.
        ApplyFailureObjectState();
    }

    private void ApplyFailureObjectState()
    {
        ApplySetActiveSafe(
            deactivateObjectsOnFailure,
            false
        );

        ApplySetActiveSafe(
            activateObjectsOnFailure,
            true
        );
    }

    private void ApplySetActiveSafe(
        GameObject[] objects,
        bool active)
    {
        if (objects == null)
            return;

        for (int i = 0; i < objects.Length; i++)
        {
            GameObject target = objects[i];

            if (target == null)
                continue;

            if (target == gameObject)
            {
                Debug.LogWarning(
                    "[LaserDetectionZone] Ignored failure object list entry because it targets the LaserDetectionZone itself.",
                    this
                );
                continue;
            }

            target.SetActive(active);
        }
    }

    public void ResetDetection()
    {
        playerCollidersInside.Clear();
        playerDetected = false;
        playerLost = false;
        damageTimer = 0f;

        if (playerLaserReceiver != null)
            playerLaserReceiver.ResetLaserState();
    }

    private void HandlePlayerDeathOrFailureStarted()
    {
        // Both modes must stop using stale trigger state while Player is
        // inside the shared Death/Failure sequence.
        playerCollidersInside.Clear();
        playerDetected = false;
        damageTimer = 0f;

        // Rearm the zone for a future, new physical entry.
        // Because the collider set is cleared, this does not immediately
        // re-trigger from the same collision.
        playerLost = false;

        if (playerLaserReceiver != null)
            playerLaserReceiver.ResetLaserState();
    }

    private void ResolveReferences()
    {
        if (playerProperties == null)
            playerProperties = PlayerProperties.Instance;

        if (playerLaserReceiver == null)
            playerLaserReceiver =
                FindAnyObjectByType<PlayerLaserReceiver>();
    }

    private bool IsPlayer(Collider other)
    {
        if (other == null)
            return false;

        if (other.CompareTag("Player"))
            return true;

        Transform root = other.transform.root;

        return root != null &&
               root.CompareTag("Player");
    }
}
