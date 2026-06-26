using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class SecurityCameraScanTest : MonoBehaviour
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
    [SerializeField] private float fillToRedTime = 0.5f;
    [SerializeField] private float fadeBackTime = 0.35f;

    [Header("Optional Event")]
    [SerializeField] private UnityEvent onDetectionFull;

    private readonly HashSet<Collider> playerCollidersInside = new HashSet<Collider>();

    private Collider scanCollider;
    private Vector3 startLocalEuler;
    private Color startColor;
    private float detection01;
    private bool fullDetectionInvoked;

    private void Awake()
    {
        scanCollider = GetComponent<Collider>();
        scanCollider.isTrigger = true;

        if (scanPivot == null)
            scanPivot = transform.parent;

        if (scanPivot != null)
            startLocalEuler = scanPivot.localEulerAngles;

        if (scanSpriteRenderer != null)
            startColor = scanSpriteRenderer.color;
    }

    private void Update()
    {
        UpdateSweepMovement();
        UpdateDetectionValue();
        UpdateScanColor();
    }

    private void UpdateSweepMovement()
    {
        if (scanPivot == null)
            return;

        float t = Mathf.PingPong(Time.time * sweepSpeed, 1f);

        if (smoothSweep)
            t = Mathf.SmoothStep(0f, 1f, t);

        float angleOffset = Mathf.Lerp(-halfAngle, halfAngle, t);

        Vector3 euler = startLocalEuler;

        switch (sweepAxis)
        {
            case SweepAxis.X:
                euler.x = startLocalEuler.x + angleOffset;
                break;

            case SweepAxis.Y:
                euler.y = startLocalEuler.y + angleOffset;
                break;

            case SweepAxis.Z:
                euler.z = startLocalEuler.z + angleOffset;
                break;
        }

        scanPivot.localEulerAngles = euler;
    }

    private void UpdateDetectionValue()
    {
        bool playerInside = playerCollidersInside.Count > 0;

        if (playerInside)
        {
            detection01 += Time.deltaTime / Mathf.Max(0.01f, fillToRedTime);
        }
        else
        {
            detection01 -= Time.deltaTime / Mathf.Max(0.01f, fadeBackTime);
        }

        detection01 = Mathf.Clamp01(detection01);

        if (detection01 >= 1f && !fullDetectionInvoked)
        {
            fullDetectionInvoked = true;
            Debug.Log("[SecurityCameraScanTest] Player fully detected.");

            if (onDetectionFull != null)
                onDetectionFull.Invoke();
        }

        if (detection01 <= 0f)
        {
            fullDetectionInvoked = false;
        }
    }

    private void UpdateScanColor()
    {
        if (scanSpriteRenderer == null)
            return;

        Color redColor = keepOriginalAlpha
            ? new Color(1f, 0f, 0f, startColor.a)
            : Color.red;

        scanSpriteRenderer.color = Color.Lerp(startColor, redColor, detection01);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsPlayer(other))
        {
            playerCollidersInside.Add(other);
            Debug.Log("[SecurityCameraScanTest] Player entered scan area.");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsPlayer(other))
        {
            playerCollidersInside.Remove(other);
            Debug.Log("[SecurityCameraScanTest] Player exited scan area.");
        }
    }

    private bool IsPlayer(Collider other)
    {
        if (other.CompareTag(playerTag))
            return true;

        if (other.transform.root.CompareTag(playerTag))
            return true;

        return false;
    }

    public float GetDetection01()
    {
        return detection01;
    }

    public void ResetDetection()
    {
        playerCollidersInside.Clear();
        detection01 = 0f;
        fullDetectionInvoked = false;
        UpdateScanColor();
    }
}