using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class GuardVisionView : MonoBehaviour
{
    private GuardState guardState;

    [Header("Vision Properties")]
    [SerializeField] private float visionLength = 10f;
    [SerializeField] private float visionWidth = 5f;
    [SerializeField] private float visionHeight = 3f;
    
    [Header("Visual Properties")]
    [SerializeField] private float opacity = 0.3f;
    [SerializeField] private Color visionColor = Color.red;
    
    [Header("Occlusion")]
    [SerializeField] private LayerMask wallLayer;
    [SerializeField] private bool useWallOcclusion = true;
    [SerializeField] private float occlusionCheckDensity = 5f;

    private Mesh visionMesh;
    private Material visionMaterial;
    private MeshRenderer meshRenderer;
    private bool visionDisabled;

    private void OnValidate()
    {
        CleanupLegacyFovCollider();
    }

    private void OnEnable()
    {
        CleanupLegacyFovCollider();
        Vector3 localPosition = transform.localPosition;
        transform.localPosition = new Vector3(localPosition.x, 0.01f, localPosition.z);
        visionDisabled = false;
        InitializeMesh();
    }

    private void Start()
    {
        guardState = GetComponent<GuardState>();
        InitializeMesh();
    }

    private void InitializeMesh()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();
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

        meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer.material.name == "Default Material")
        {
            visionMaterial = new Material(Shader.Find("Standard"));
            meshRenderer.material = visionMaterial;
        }
        else
        {
            visionMaterial = meshRenderer.material;
        }

        UpdateMaterialProperties();
        GenerateVisionMesh();
    }

    private void CleanupLegacyFovCollider()
    {
        Transform colliderTransform = transform.Find("FOV Collider");
        if (colliderTransform != null)
        {
            if (Application.isPlaying)
            {
                Destroy(colliderTransform.gameObject);
            }
            else
            {
                DestroyImmediate(colliderTransform.gameObject);
            }
        }
    }

    private void Update()
    {
        GenerateVisionMesh();
        UpdateMaterialProperties();
    }

    private void GenerateVisionMesh()
    {
        if (visionDisabled)
        {
            return;
        }

        visionMesh.Clear();

        int sampleCount = Mathf.Max(2, Mathf.CeilToInt(occlusionCheckDensity));
        Vector3[] vertices = new Vector3[sampleCount + 1];
        int[] triangles = new int[(sampleCount - 1) * 3];

        vertices[0] = Vector3.zero;

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (sampleCount == 1) ? 0f : (float)i / (sampleCount - 1);
            float xPos = Mathf.Lerp(-visionWidth / 2f, visionWidth / 2f, t);
            Vector3 targetPoint = new Vector3(xPos, 0f, visionLength);
            Vector3 rayDirection = targetPoint.normalized;

            Vector3 finalPoint = targetPoint;
            if (useWallOcclusion)
            {
                Vector3 worldDirection = transform.TransformDirection(rayDirection);
                RaycastHit[] hits = Physics.RaycastAll(transform.position, worldDirection, visionLength, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                float closestHitDistance = float.MaxValue;
                bool hasClosestHit = false;
                RaycastHit closestHit = default;

                for (int hitIndex = 0; hitIndex < hits.Length; hitIndex++)
                {
                    RaycastHit hit = hits[hitIndex];

                    if (hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform))
                    {
                        continue;
                    }

                    if (hit.distance < closestHitDistance)
                    {
                        closestHitDistance = hit.distance;
                        closestHit = hit;
                        hasClosestHit = true;
                    }
                }

                if (hasClosestHit)
                {
                    finalPoint = transform.InverseTransformPoint(closestHit.point);

                    if (closestHit.collider.CompareTag("Player"))
                    {
                        guardState.state = GuardState.State.DetectPlayer;
                        DisableTriangleView();
                        return;
                    }
                }
            }

            vertices[i + 1] = finalPoint;
        }

        for (int i = 0; i < sampleCount - 1; i++)
        {
            int triIndex = i * 3;
            triangles[triIndex] = 0;
            triangles[triIndex + 1] = i + 1;
            triangles[triIndex + 2] = i + 2;
        }

        visionMesh.vertices = vertices;
        visionMesh.triangles = triangles;
        visionMesh.RecalculateNormals();
        visionMesh.RecalculateBounds();
    }

    private void DisableTriangleView()
    {
        visionDisabled = true;

        if (meshRenderer != null)
        {
            meshRenderer.enabled = false;
        }

        visionMesh.Clear();
    }

    private void ApplyWallOcclusion()
    {
        // Occlusion is applied during mesh generation.
    }

    private void UpdateMaterialProperties()
    {
        if (visionMaterial == null) return;

        Color colorWithAlpha = visionColor;
        colorWithAlpha.a = Mathf.Clamp01(opacity);
        visionMaterial.SetColor("_Color", colorWithAlpha);

        visionMaterial.SetFloat("_Mode", 3);
        visionMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        visionMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        visionMaterial.SetInt("_ZWrite", 0);
        visionMaterial.DisableKeyword("_ALPHATEST_ON");
        visionMaterial.EnableKeyword("_ALPHABLEND_ON");
        visionMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        visionMaterial.renderQueue = 3000;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1, 0, 0, 0.3f);

        Vector3 apex = transform.position;
        Vector3 baseLeft = transform.TransformPoint(new Vector3(-visionWidth / 2f, 0f, visionLength));
        Vector3 baseRight = transform.TransformPoint(new Vector3(visionWidth / 2f, 0f, visionLength));

        Gizmos.DrawLine(apex, baseLeft);
        Gizmos.DrawLine(apex, baseRight);
        Gizmos.DrawLine(baseLeft, baseRight);
    }

    public void SetVisionLength(float length)
    {
        visionLength = Mathf.Clamp(length, 0.1f, 100f);
    }

    public void SetVisionWidth(float width)
    {
        visionWidth = Mathf.Clamp(width, 0.1f, 50f);
    }

    public void SetVisionHeight(float height)
    {
        visionHeight = Mathf.Clamp(height, 0.1f, 50f);
    }

    public void SetOpacity(float newOpacity)
    {
        opacity = Mathf.Clamp01(newOpacity);
    }

    public void SetVisionColor(Color color)
    {
        visionColor = color;
    }
}
