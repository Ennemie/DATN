// Chức năng: Vùng camera quét dạng trigger; nếu Player đứng trong vùng đủ thời gian detectionTime thì gọi MissionFailManager để fade đen và respawn checkpoint.
// Đồng thời đổi màu SpriteRenderer của vùng quét dần sang đỏ trong detectionTime, ví dụ 0.5s đạt full đỏ.
// Tham chiếu với: MissionFailManager; object này cần Collider 3D IsTrigger=true, không dùng Collider2D nếu game là 3D/top-down.
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class CameraDetectionZone : MonoBehaviour
{
    [Header("Fail Manager")]
    [SerializeField] private MissionFailManager failManager;

    [Header("Detection")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private float detectionTime = 0.5f;
    [SerializeField] private float fadeBackTime = 0.35f;

    [Header("Visual")]
    [SerializeField] private SpriteRenderer scanSpriteRenderer;
    [SerializeField] private bool useCurrentColorAsNormal = true;
    [SerializeField] private Color normalColor = new Color(1f, 1f, 1f, 0.35f);
    [SerializeField] private Color detectedColor = new Color(1f, 0f, 0f, 0.35f);

    private readonly HashSet<Collider> playerCollidersInside = new HashSet<Collider>();
    private Collider triggerCollider;
    private float detection01;
    private bool hasTriggeredFail;

    private void Reset()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;
    }

    private void Awake()
    {
        triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null)
            triggerCollider.isTrigger = true;

        if (scanSpriteRenderer != null && useCurrentColorAsNormal)
            normalColor = scanSpriteRenderer.color;

        UpdateVisualColor();
    }

    private void Update()
    {
        bool playerInside = playerCollidersInside.Count > 0;

        if (playerInside && !hasTriggeredFail)
        {
            detection01 += Time.deltaTime / Mathf.Max(0.01f, detectionTime);
        }
        else
        {
            detection01 -= Time.deltaTime / Mathf.Max(0.01f, fadeBackTime);
        }

        detection01 = Mathf.Clamp01(detection01);
        UpdateVisualColor();

        if (detection01 >= 1f && !hasTriggeredFail)
        {
            hasTriggeredFail = true;
            playerCollidersInside.Clear();
            detection01 = 0f;
            UpdateVisualColor();

            Debug.Log("[CameraDetectionZone] Player fully detected.", this);

            if (failManager != null)
                failManager.FailAndReturnToCheckpoint();
            else
                Debug.LogWarning("[CameraDetectionZone] Missing MissionFailManager.", this);
        }

        if (!playerInside && detection01 <= 0f)
            hasTriggeredFail = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsPlayer(other))
            playerCollidersInside.Add(other);
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsPlayer(other))
            playerCollidersInside.Remove(other);
    }

    public void ResetDetection()
    {
        playerCollidersInside.Clear();
        detection01 = 0f;
        hasTriggeredFail = false;
        UpdateVisualColor();
    }

    private void UpdateVisualColor()
    {
        if (scanSpriteRenderer == null)
            return;

        scanSpriteRenderer.color = Color.Lerp(normalColor, detectedColor, detection01);
    }

    private bool IsPlayer(Collider other)
    {
        if (other.CompareTag(playerTag))
            return true;

        Transform root = other.transform.root;
        return root != null && root.CompareTag(playerTag);
    }
}
