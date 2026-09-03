using UnityEngine;

/// <summary>
/// Guard Vision View.
/// 
/// Chức năng:
/// - WHITE  = trạng thái bình thường.
/// - YELLOW = phát hiện Player, bắt đầu xác nhận / điều tra.
/// - RED    = đã xác nhận Player.
/// - Legacy Guard: RED -> GuardState.DetectPlayer.
/// - Alarm: RED -> Player Failure, không phát Dead animation.
/// - Enemy mới có thể nhận RED thông qua IEnemyVisionDetectionReceiver.
/// 
/// Lưu ý:
/// - Vision chỉ kiểm tra Player; movement/AI reaction nằm ở script khác.
/// - Không tự dùng NavMesh.
/// </summary>
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class GuardVisionView : MonoBehaviour
{
    private GuardState guardState;
    private GuardController guardController;
    private MeshRenderer meshRenderer;

    [Header("Vision Properties")]
    [SerializeField] private float visionLength = 10f;
    [SerializeField] private float visionWidth = 5f;
    [SerializeField] private float visionHeight = 3f;

    [Header("Visual Properties")]
    [SerializeField] private float opacity = 0.3f;
    [SerializeField] private Color visionColor = Color.white;
    [SerializeField] private Color yellowVisionColor =
        new Color(1f, 0.9f, 0.2f, 1f);
    [SerializeField] private Color redVisionColor = Color.red;

    [Header("Detection Timing")]
    [Tooltip("Thời gian Player phải nằm liên tục trong Vision vàng trước khi chuyển đỏ.")]
    [Min(0.01f)]
    [SerializeField] private float confirmationTime = 0.5f;

    [Tooltip("Sau khi chuyển đỏ, Player phải tiếp tục bị xác nhận trong khoảng thời gian này mới bị xử lý.")]
    [Min(0.01f)]
    [SerializeField] private float redHoldTime = 0.5f;

    [Tooltip("Sau khi mất dấu Player, Enemy giữ trạng thái điều tra vàng trong thời gian này.")]
    [Min(0.01f)]
    [SerializeField] private float investigationDuration = 5f;

    [Header("Alarm Detection")]
    [Tooltip(
        "Legacy Guard: khi Vision chuyển RED thì Player chạy Failure/Checkpoint. " +
        "Không phát Dead animation."
    )]
    [SerializeField] private bool causeImmediatePlayerFailureOnDetection = false;

    [Header("Occlusion")]
    [SerializeField] private LayerMask wallLayer;
    [SerializeField] private bool useWallOcclusion = true;
    [SerializeField] private float occlusionCheckDensity = 5f;

    private Mesh visionMesh;
    private Material visionMaterial;

    private bool visionDisabled;
    private bool restoreLocked;
    private bool detectionArmed = true;
    private bool playerWasVisibleLastFrame;

    private Vector3 lastDetectedPlayerPosition;
    private float yellowStartedAt = -1f;
    private float lastSeenPlayerAt = -1f;
    private float redStartedAt = -1f;

    private IEnemyVisionDetectionReceiver detectionReceiver;

    private enum VisionAlertState
    {
        White,
        Yellow,
        Red
    }

    private VisionAlertState alertState =
        VisionAlertState.White;

    // Chức năng mới:
    // API công khai cho StealthTakedownController kiểm tra Vision trước khi hiện button.
    public bool IsWhite =>
        !visionDisabled &&
        !restoreLocked &&
        alertState == VisionAlertState.White;

    public bool CanBeStealthTakedownTarget =>
        IsWhite && detectionArmed;

    public bool IsYellow =>
        !visionDisabled &&
        !restoreLocked &&
        alertState == VisionAlertState.Yellow;

    public bool IsRed =>
        !visionDisabled &&
        !restoreLocked &&
        alertState == VisionAlertState.Red;

    public bool CanBePlayerPunchTarget =>
        !visionDisabled &&
        !restoreLocked &&
        detectionArmed &&
        (alertState == VisionAlertState.Yellow ||
         alertState == VisionAlertState.Red);

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnValidate()
    {
        CleanupLegacyFovCollider();
    }

    private void OnEnable()
    {
        CleanupLegacyFovCollider();

        Vector3 localPosition = transform.localPosition;
        transform.localPosition = new Vector3(
            localPosition.x,
            0.01f,
            localPosition.z
        );

        ResolveReferences();

        if (!restoreLocked)
            ResetToNeutral(false);

        InitializeMesh();
    }

    private void Start()
    {
        ResolveReferences();
        InitializeMesh();
    }

    private void ResolveReferences()
    {
        if (guardState == null)
            guardState = GetComponent<GuardState>();

        if (guardController == null)
            guardController = GetComponent<GuardController>();

        if (detectionReceiver == null)
            detectionReceiver =
                GetComponent<IEnemyVisionDetectionReceiver>();
    }

    private void InitializeMesh()
    {
        MeshFilter meshFilter =
            GetComponent<MeshFilter>();

        if (meshFilter.sharedMesh == null)
        {
            visionMesh = new Mesh();
            visionMesh.name = "GuardVisionMesh";
            meshFilter.mesh = visionMesh;
        }
        else
        {
            visionMesh = meshFilter.mesh;
        }

        meshRenderer =
            GetComponent<MeshRenderer>();

        if (meshRenderer.material.name == "Default Material")
        {
            visionMaterial =
                new Material(Shader.Find("Standard"));

            meshRenderer.material =
                visionMaterial;
        }
        else
        {
            visionMaterial =
                meshRenderer.material;
        }

        UpdateMaterialProperties();
        GenerateVisionMesh();
    }

    private void CleanupLegacyFovCollider()
    {
        Transform colliderTransform =
            transform.Find("FOV Collider");

        if (colliderTransform == null)
            return;

        if (Application.isPlaying)
            Destroy(colliderTransform.gameObject);
        else
            DestroyImmediate(colliderTransform.gameObject);
    }

    private void Update()
    {
        ResolveReferences();

        if (visionDisabled)
            return;

        HandleVisionState();
        UpdateMaterialProperties();
    }

    private void HandleVisionState()
    {
        bool playerVisible =
            TryDetectPlayer(out Vector3 detectedPosition);

        if (alertState == VisionAlertState.White)
        {
            if (!detectionArmed)
            {
                // Chức năng mới:
                // Sau restore, Player phải thật sự rời vùng nhìn mới arm Vision.
                if (!playerVisible)
                    detectionArmed = true;
                else
                    return;
            }

            if (!playerVisible)
                return;

            lastDetectedPlayerPosition =
                detectedPosition;

            lastSeenPlayerAt =
                Time.time;

            EnterYellow(
                detectedPosition
            );

            playerWasVisibleLastFrame = true;
            return;
        }

        if (alertState == VisionAlertState.Yellow)
        {
            if (playerVisible)
            {
                lastDetectedPlayerPosition =
                    detectedPosition;

                lastSeenPlayerAt =
                    Time.time;

                if (restoreLocked ||
                    (PlayerProperties.Instance != null &&
                     PlayerProperties.Instance.IsDeadOrFailing))
                {
                    return;
                }

                if (!detectionArmed)
                    return;

                // Player đã rời rồi quay lại:
                // bộ đếm vàng phải bắt đầu lại từ đầu.
                if (!playerWasVisibleLastFrame)
                    yellowStartedAt = Time.time;

                playerWasVisibleLastFrame =
                    true;

                UpdateInvestigationTarget(
                    detectedPosition
                );

                // Chỉ sau 0.5s liên tục trong vàng mới chuyển RED.
                if (yellowStartedAt >= 0f &&
                    Time.time - yellowStartedAt >=
                    confirmationTime)
                {
                    EnterRedDetection();
                }

                return;
            }

            // Không còn thấy Player trong màu vàng:
            // giữ vàng và tiếp tục điều tra trong thời gian 5s.
            playerWasVisibleLastFrame =
                false;

            if (lastSeenPlayerAt >= 0f &&
                Time.time - lastSeenPlayerAt >=
                investigationDuration)
            {
                ReturnToWhiteAndPatrol();
            }

            return;
        }

        if (alertState == VisionAlertState.Red)
        {
            // Chức năng mới:
            // RED chưa xử lý ngay. Player phải tiếp tục nằm trong Vision
            // thêm redHoldTime (mặc định 0.5s).
            if (!playerVisible)
            {
                // Player trốn trong lúc đang RED hold:
                // quay về YELLOW và tiếp tục điều tra, không Fail/Chase.
                alertState =
                    VisionAlertState.Yellow;

                redStartedAt = -1f;
                yellowStartedAt = -1f;
                playerWasVisibleLastFrame =
                    false;

                SetVisionColor(
                    yellowVisionColor
                );

                return;
            }

            lastDetectedPlayerPosition =
                detectedPosition;

            lastSeenPlayerAt =
                Time.time;

            if (restoreLocked ||
                (PlayerProperties.Instance != null &&
                 PlayerProperties.Instance.IsDeadOrFailing))
            {
                return;
            }

            if (redStartedAt >= 0f &&
                Time.time - redStartedAt >=
                redHoldTime)
            {
                ConfirmRedDetection();
            }

            return;
        }
    }

    private bool TryDetectPlayer(
        out Vector3 detectedPosition)
    {
        detectedPosition = Vector3.zero;

        if (restoreLocked)
            return false;

        if (PlayerProperties.Instance != null &&
            PlayerProperties.Instance.IsDeadOrFailing)
        {
            return false;
        }

        int sampleCount =
            Mathf.Max(
                2,
                Mathf.CeilToInt(
                    occlusionCheckDensity
                )
            );

        for (int i = 0;
             i < sampleCount;
             i++)
        {
            float t =
                sampleCount == 1
                    ? 0f
                    : (float)i /
                      (sampleCount - 1);

            float xPos =
                Mathf.Lerp(
                    -visionWidth / 2f,
                    visionWidth / 2f,
                    t
                );

            Vector3 targetPoint =
                new Vector3(
                    xPos,
                    0f,
                    visionLength
                );

            Vector3 worldDirection =
                transform.TransformDirection(
                    targetPoint.normalized
                );

            RaycastHit[] hits =
                Physics.RaycastAll(
                    transform.position,
                    worldDirection,
                    visionLength,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore
                );

            float closestHitDistance =
                float.MaxValue;

            bool hasClosestHit = false;
            RaycastHit closestHit = default;

            for (int hitIndex = 0;
                 hitIndex < hits.Length;
                 hitIndex++)
            {
                RaycastHit hit =
                    hits[hitIndex];

                if (hit.collider == null)
                    continue;

                if (hit.collider.transform == transform ||
                    hit.collider.transform.IsChildOf(transform))
                {
                    continue;
                }

                if (hit.distance <
                    closestHitDistance)
                {
                    closestHitDistance =
                        hit.distance;

                    closestHit = hit;
                    hasClosestHit = true;
                }
            }

            if (!hasClosestHit)
                continue;

            Vector3 finalPoint =
                transform.InverseTransformPoint(
                    closestHit.point
                );

            if (IsPlayerCollider(
                    closestHit.collider))
            {
                detectedPosition =
                    PlayerProperties.Instance != null
                        ? PlayerProperties.Instance.transform.position
                        : transform.TransformPoint(finalPoint);

                return true;
            }
        }

        return false;
    }

    private bool IsPlayerCollider(
        Collider collider)
    {
        if (collider == null)
            return false;

        if (collider.CompareTag("Player"))
            return true;

        Transform root =
            collider.transform.root;

        return root != null &&
               root.CompareTag("Player");
    }

    private void EnterYellow(
        Vector3 detectedPosition)
    {
        alertState =
            VisionAlertState.Yellow;

        yellowStartedAt =
            Time.time;

        lastSeenPlayerAt =
            Time.time;

        lastDetectedPlayerPosition =
            detectedPosition;

        SetVisionColor(
            yellowVisionColor
        );

        // Chức năng mới:
        // Enemy legacy dừng Patrol và điều tra vị trí cuối cùng thấy Player.
        if (detectionReceiver == null &&
            guardController != null)
        {
            guardController.BeginInvestigation(
                detectedPosition
            );
        }
    }

    private void UpdateInvestigationTarget(
        Vector3 detectedPosition)
    {
        lastDetectedPlayerPosition =
            detectedPosition;

        // Enemy mới tự quản lý Investigation qua callback riêng.
        if (detectionReceiver != null)
            return;

        if (guardController != null)
        {
            guardController.UpdateInvestigationTarget(
                detectedPosition
            );
        }
    }

    private void EnterRedDetection()
    {
        if (alertState == VisionAlertState.Red)
            return;

        alertState =
            VisionAlertState.Red;

        redStartedAt =
            Time.time;

        playerWasVisibleLastFrame =
            true;

        SetVisionColor(
            redVisionColor
        );

        // Chức năng mới:
        // RED mới chỉ là "đã xác nhận", chưa Failure/Chase ngay.
        // Phải giữ RED thêm redHoldTime.
    }

    private void ConfirmRedDetection()
    {
        if (alertState != VisionAlertState.Red)
            return;

        if (redStartedAt < 0f ||
            Time.time - redStartedAt <
            redHoldTime)
        {
            return;
        }

        if (detectionReceiver != null)
        {
            // Enemy mới nhận RED trực tiếp.
            detectionReceiver.OnPlayerVisionConfirmed(
                lastDetectedPlayerPosition
            );

            return;
        }

        // CŨ:
        // guardState.state = GuardState.State.DetectPlayer;
        //
        // if (causeImmediatePlayerFailureOnDetection &&
        //     PlayerProperties.Instance != null)
        // {
        //     PlayerProperties.Instance.RequestFailureToCheckpoint();
        // }
        //
        // MỚI:
        // Điều chỉnh:
        // Alarm chỉ Failure sau khi RED giữ đủ redHoldTime.
        // Failure không phát Dead animation.
        if (causeImmediatePlayerFailureOnDetection)
        {
            if (PlayerProperties.Instance != null)
                PlayerProperties.Instance
                    .RequestFailureToCheckpoint();
        }
        else if (guardState != null)
        {
            guardState.state =
                GuardState.State.DetectPlayer;
        }
    }

    private void ReturnToWhiteAndPatrol()
    {
        alertState =
            VisionAlertState.White;

        yellowStartedAt = -1f;
        redStartedAt = -1f;
        lastSeenPlayerAt = -1f;
        lastDetectedPlayerPosition =
            Vector3.zero;

        playerWasVisibleLastFrame =
            false;

        SetVisionColor(
            visionColor
        );

        if (detectionReceiver == null &&
            guardController != null)
        {
            guardController
                .EndInvestigationAndReturnToGuarding();
        }

        detectionArmed = true;
    }

    /// <summary>
    /// Chức năng mới:
    /// Reset Vision về WHITE.
    ///
    /// requirePlayerExit = true:
    /// - Vision hiển thị WHITE.
    /// - Không detect Player ngay nếu Player đang đứng trong FOV lúc restore.
    /// - Chỉ sau khi Player rời FOV mới arm lại.
    /// </summary>
    public void ResetToNeutral(
        bool requirePlayerExit = false)
    {
        visionDisabled = false;

        alertState =
            VisionAlertState.White;

        yellowStartedAt = -1f;
        redStartedAt = -1f;
        lastSeenPlayerAt = -1f;
        lastDetectedPlayerPosition =
            Vector3.zero;

        playerWasVisibleLastFrame =
            false;

        detectionArmed =
            !requirePlayerExit;

        // CŨ:
        // visionColor có thể đang bị đổi runtime sang vàng/đỏ.
        //
        // MỚI:
        // Điều chỉnh: WHITE là màu mặc định sau restore.
        visionColor = Color.white;
        SetVisionColor(visionColor);

        if (meshRenderer != null)
            meshRenderer.enabled = true;

        if (visionMesh != null)
            GenerateVisionMesh();
    }

    /// <summary>
    /// Chức năng mới:
    /// Khóa Vision trong lúc Enemy death/restore.
    /// Không cho OnEnable/Update phát detection giữa chuỗi restore.
    /// </summary>
    public void SetRestoreLock(
        bool locked)
    {
        restoreLocked = locked;

        if (locked)
        {
            alertState =
                VisionAlertState.White;

            yellowStartedAt = -1f;
            lastSeenPlayerAt = -1f;
            lastDetectedPlayerPosition =
                Vector3.zero;

            playerWasVisibleLastFrame =
                false;

            detectionArmed = false;

            if (meshRenderer != null)
                meshRenderer.enabled = false;
        }
        else
        {
            visionDisabled = false;
            if (meshRenderer != null)
                meshRenderer.enabled = true;
        }
    }

    public void SetVisionLength(
        float length)
    {
        visionLength =
            Mathf.Clamp(
                length,
                0.1f,
                100f
            );
    }

    public void SetVisionWidth(
        float width)
    {
        visionWidth =
            Mathf.Clamp(
                width,
                0.1f,
                50f
            );
    }

    public void SetVisionHeight(
        float height)
    {
        visionHeight =
            Mathf.Clamp(
                height,
                0.1f,
                50f
            );
    }

    public void SetOpacity(
        float newOpacity)
    {
        opacity =
            Mathf.Clamp01(newOpacity);
    }

    private void SetVisionColor(
        Color color)
    {
        visionColor =
            color;

        UpdateMaterialProperties();
    }

    private void UpdateMaterialProperties()
    {
        if (visionMaterial == null)
            return;

        Color colorWithAlpha =
            visionColor;

        colorWithAlpha.a =
            Mathf.Clamp01(opacity);

        visionMaterial.SetColor(
            "_Color",
            colorWithAlpha
        );

        visionMaterial.SetFloat(
            "_Mode",
            3
        );

        visionMaterial.SetInt(
            "_SrcBlend",
            (int)UnityEngine.Rendering.BlendMode.SrcAlpha
        );

        visionMaterial.SetInt(
            "_DstBlend",
            (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha
        );

        visionMaterial.SetInt(
            "_ZWrite",
            0
        );

        visionMaterial.DisableKeyword(
            "_ALPHATEST_ON"
        );

        visionMaterial.EnableKeyword(
            "_ALPHABLEND_ON"
        );

        visionMaterial.DisableKeyword(
            "_ALPHAPREMULTIPLY_ON"
        );

        visionMaterial.renderQueue =
            3000;
    }

    private void GenerateVisionMesh()
    {
        if (visionMesh == null ||
            visionDisabled)
        {
            return;
        }

        visionMesh.Clear();

        int sampleCount =
            Mathf.Max(
                2,
                Mathf.CeilToInt(
                    occlusionCheckDensity
                )
            );

        Vector3[] vertices =
            new Vector3[sampleCount + 1];

        int[] triangles =
            new int[(sampleCount - 1) * 3];

        vertices[0] =
            Vector3.zero;

        for (int i = 0;
             i < sampleCount;
             i++)
        {
            float t =
                sampleCount == 1
                    ? 0f
                    : (float)i /
                      (sampleCount - 1);

            float xPos =
                Mathf.Lerp(
                    -visionWidth / 2f,
                    visionWidth / 2f,
                    t
                );

            Vector3 targetPoint =
                new Vector3(
                    xPos,
                    0f,
                    visionLength
                );

            Vector3 rayDirection =
                targetPoint.normalized;

            Vector3 finalPoint =
                targetPoint;

            if (useWallOcclusion)
            {
                Vector3 worldDirection =
                    transform.TransformDirection(
                        rayDirection
                    );

                RaycastHit[] hits =
                    Physics.RaycastAll(
                        transform.position,
                        worldDirection,
                        visionLength,
                        Physics.DefaultRaycastLayers,
                        QueryTriggerInteraction.Ignore
                    );

                float closestHitDistance =
                    float.MaxValue;

                bool hasClosestHit = false;
                RaycastHit closestHit =
                    default;

                for (int hitIndex = 0;
                     hitIndex < hits.Length;
                     hitIndex++)
                {
                    RaycastHit hit =
                        hits[hitIndex];

                    if (hit.collider == null)
                        continue;

                    if (hit.collider.transform ==
                            transform ||
                        hit.collider.transform
                            .IsChildOf(transform))
                    {
                        continue;
                    }

                    if (hit.distance <
                        closestHitDistance)
                    {
                        closestHitDistance =
                            hit.distance;

                        closestHit =
                            hit;

                        hasClosestHit =
                            true;
                    }
                }

                if (hasClosestHit)
                {
                    finalPoint =
                        transform.InverseTransformPoint(
                            closestHit.point
                        );
                }
            }

            vertices[i + 1] =
                finalPoint;
        }

        for (int i = 0;
             i < sampleCount - 1;
             i++)
        {
            int triIndex =
                i * 3;

            triangles[triIndex] = 0;
            triangles[triIndex + 1] =
                i + 1;

            triangles[triIndex + 2] =
                i + 2;
        }

        visionMesh.vertices =
            vertices;

        visionMesh.triangles =
            triangles;

        visionMesh.RecalculateNormals();
        visionMesh.RecalculateBounds();

        if (meshRenderer != null)
        {
            meshRenderer.enabled =
                !restoreLocked;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color =
            new Color(
                1f,
                1f,
                1f,
                0.3f
            );

        Vector3 apex =
            transform.position;

        Vector3 baseLeft =
            transform.TransformPoint(
                new Vector3(
                    -visionWidth / 2f,
                    0f,
                    visionLength
                )
            );

        Vector3 baseRight =
            transform.TransformPoint(
                new Vector3(
                    visionWidth / 2f,
                    0f,
                    visionLength
                )
            );

        Gizmos.DrawLine(
            apex,
            baseLeft
        );

        Gizmos.DrawLine(
            apex,
            baseRight
        );

        Gizmos.DrawLine(
            baseLeft,
            baseRight
        );
    }
}

/// <summary>
/// Chức năng mới:
/// Contract giữa GuardVisionView và các Enemy mới.
/// GuardVisionView chỉ quyết định "RED đã xác nhận".
/// Enemy tự quyết định Chase hay Failure.
/// </summary>
public interface IEnemyVisionDetectionReceiver
{
    void OnPlayerVisionConfirmed(
        Vector3 playerPosition
    );
}
