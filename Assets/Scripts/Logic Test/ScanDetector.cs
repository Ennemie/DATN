using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Security camera scan detector connected to the current Player/Checkpoint failure pipeline.
///
/// The old ScanDetector asset used the SecurityCameraScanTest class and raised an optional
/// UnityEvent after the detection meter reached 100%. This replacement keeps:
/// - sweep movement of the scan pivot;
/// - optional scan visual that lerps toward red;
/// - 3D trigger Player detection;
/// but now owns the actual Alarm -> Player Failure -> Checkpoint call.
///
/// Modes:
/// - Fail Immediately = Player entering the detector causes immediate failure.
/// - Fail Immediately = false -> detection fills over Detection Time before failure.
///
/// No MissionFailManager reference is required.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ScanDetector : MonoBehaviour
{
    public enum SweepAxis
    {
        X,
        Y,
        Z
    }

    [Header("Sweep")]
    [SerializeField] private Transform scanPivot;
    [SerializeField] private SweepAxis sweepAxis = SweepAxis.Y;
    [SerializeField] private float halfAngle = 75f;
    [SerializeField] private float sweepSpeed = 1f;
    [SerializeField] private bool smoothSweep = true;

    [Header("Visual")]
    [SerializeField] private SpriteRenderer scanSpriteRenderer;
    [SerializeField] private bool keepOriginalAlpha = true;

    [Header("Detection")]
    [SerializeField] private string playerTag = "Player";

    [Tooltip(
        "Bật: Player va chạm detector là Failure ngay. " +
        "Tắt: detection tăng dần trong thời gian này rồi mới Failure."
    )]
    [SerializeField] private bool failImmediately = true;

    [Min(0.01f)]
    [Tooltip(
        "Chỉ dùng khi Fail Immediately = false. Thời gian Player phải ở trong detector để báo động."
    )]
    [SerializeField] private float detectionTime = 0.5f;

    [Min(0.01f)]
    [SerializeField] private float fadeBackTime = 0.35f;

    [Header("Failure Object State")]
    [Tooltip("SetActive(true) khi Camera bắt đầu Failure.")]
    [SerializeField] private GameObject[] activateObjectsOnFailure;

    [Tooltip("SetActive(false) khi Camera bắt đầu Failure. " +
             "Nếu một object nằm ở cả hai list, list Active được áp dụng sau cùng.")]
    [SerializeField] private GameObject[] deactivateObjectsOnFailure;

    [Header("Runtime (Read Only)")]
    [SerializeField] private float detection01;
    [SerializeField] private bool playerDetected;
    [SerializeField] private bool failureTriggered;

    private readonly HashSet<Collider> playerCollidersInside =
        new HashSet<Collider>();

    private Collider scanCollider;
    private PlayerProperties playerProperties;

    private Vector3 startLocalEuler;
    private Color startColor;

    public float Detection01 => detection01;
    public bool PlayerDetected => playerDetected;
    public bool FailureTriggered => failureTriggered;

    private void Reset()
    {
        Collider col = GetComponent<Collider>();

        if (col != null)
            col.isTrigger = true;
    }

    private void Awake()
    {
        scanCollider = GetComponent<Collider>();

        if (scanCollider != null)
            scanCollider.isTrigger = true;

        if (scanPivot == null)
            scanPivot = transform.parent;

        if (scanPivot != null)
            startLocalEuler = scanPivot.localEulerAngles;

        if (scanSpriteRenderer != null)
            startColor = scanSpriteRenderer.color;

        ResolveReferences();

        UpdateScanColor();
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
        UpdateSweepMovement();

        if (failImmediately)
        {
            UpdateScanColor();
            return;
        }

        UpdateDetectionValue();
        UpdateScanColor();
    }

    private void UpdateSweepMovement()
    {
        if (scanPivot == null)
            return;

        float t =
            Mathf.PingPong(
                Time.time * sweepSpeed,
                1f
            );

        if (smoothSweep)
            t = Mathf.SmoothStep(
                0f,
                1f,
                t
            );

        float angleOffset =
            Mathf.Lerp(
                -halfAngle,
                halfAngle,
                t
            );

        Vector3 euler =
            startLocalEuler;

        switch (sweepAxis)
        {
            case SweepAxis.X:
                euler.x =
                    startLocalEuler.x +
                    angleOffset;
                break;

            case SweepAxis.Y:
                euler.y =
                    startLocalEuler.y +
                    angleOffset;
                break;

            case SweepAxis.Z:
                euler.z =
                    startLocalEuler.z +
                    angleOffset;
                break;
        }

        scanPivot.localEulerAngles =
            euler;
    }

    private void UpdateDetectionValue()
    {
        bool playerInside =
            playerCollidersInside.Count > 0;

        float fillTime =
            Mathf.Max(
                0.01f,
                detectionTime
            );

        float fadeTime =
            Mathf.Max(
                0.01f,
                fadeBackTime
            );

        if (playerInside &&
            !failureTriggered)
        {
            detection01 +=
                Time.deltaTime /
                fillTime;
        }
        else
        {
            detection01 -=
                Time.deltaTime /
                fadeTime;
        }

        detection01 =
            Mathf.Clamp01(
                detection01
            );

        if (detection01 >= 1f &&
            !failureTriggered)
        {
            TriggerFailure();
        }

        if (!playerInside &&
            detection01 <= 0f)
        {
            failureTriggered = false;
        }

        playerDetected =
            playerInside &&
            !failureTriggered;
    }

    private void UpdateScanColor()
    {
        if (scanSpriteRenderer == null)
            return;

        Color redColor =
            keepOriginalAlpha
                ? new Color(
                    1f,
                    0f,
                    0f,
                    startColor.a
                )
                : Color.red;

        scanSpriteRenderer.color =
            Color.Lerp(
                startColor,
                redColor,
                detection01
            );
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other))
            return;

        if (failureTriggered)
            return;

        playerCollidersInside.Add(other);
        playerDetected = true;

        Debug.Log(
            "[ScanDetector] Player entered camera detection zone.",
            this
        );

        if (failImmediately)
            TriggerFailure();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayer(other))
            return;

        playerCollidersInside.Remove(other);

        playerDetected =
            playerCollidersInside.Count > 0;

        if (playerCollidersInside.Count == 0 &&
            !failureTriggered)
        {
            detection01 = 0f;
            UpdateScanColor();
        }
    }

    private void TriggerFailure()
    {
        if (failureTriggered)
            return;

        ResolveReferences();

        if (playerProperties == null)
        {
            Debug.LogWarning(
                "[ScanDetector] PlayerProperties not found. " +
                "Camera cannot start checkpoint failure.",
                this
            );
            return;
        }

        if (playerProperties.IsDeadOrFailing)
            return;

        failureTriggered = true;
        playerDetected = false;
        playerCollidersInside.Clear();
        detection01 = 0f;

        Debug.Log(
            "[ScanDetector] ALARM -> Player Failure -> Checkpoint.",
            this
        );

        // Current architecture:
        // PlayerProperties -> CheckpointManager -> Player/Enemy/Mission restore.
        playerProperties.RequestFailureToCheckpoint();

        // Camera-specific scene reaction.
        ApplyFailureObjectState();

        UpdateScanColor();
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
                    "[ScanDetector] Ignored failure object list entry because it targets the ScanDetector itself.",
                    this
                );
                continue;
            }

            target.SetActive(active);
        }
    }

    private void HandlePlayerDeathOrFailureStarted()
    {
        // Prevent stale colliders and accumulated detection from continuing
        // while the Player failure/death visual is running.
        playerCollidersInside.Clear();
        playerDetected = false;
        detection01 = 0f;

        // Rearm for the next physical camera entry.
        failureTriggered = false;

        UpdateScanColor();
    }

    private void ResolveReferences()
    {
        if (playerProperties == null)
            playerProperties =
                PlayerProperties.Instance;
    }

    public float GetDetection01()
    {
        return detection01;
    }

    public void ResetDetection()
    {
        playerCollidersInside.Clear();
        detection01 = 0f;
        playerDetected = false;
        failureTriggered = false;
        UpdateScanColor();
    }

    private bool IsPlayer(Collider other)
    {
        if (other == null)
            return false;

        if (other.CompareTag(playerTag))
            return true;

        Transform root =
            other.transform.root;

        return root != null &&
               root.CompareTag(playerTag);
    }
}
