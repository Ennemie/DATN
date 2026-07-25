using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-1000)]
public class WallBlackFlameSpawner : MonoBehaviour
{
    [Header("Search")]
    [Tooltip("Any GameObject whose name contains this text will be treated as a wall.")]
    [SerializeField] private string wallNameToken = "wall";

    [Tooltip("If enabled, only root wall objects will be processed. Nested wall objects are ignored to avoid duplicates.")]
    [SerializeField] private bool onlyRootWallObjects = true;

    [Tooltip("Include inactive objects while searching the scene.")]
    [SerializeField] private bool includeInactiveObjects = true;

    [Header("Generated Flame Quad")]
    [Tooltip("Material used by the generated quads.")]
    [SerializeField] private Material flameMaterial;

    [Tooltip("Generated quad is padded slightly larger than the wall bounds.")]
    [SerializeField] private Vector2 extraPadding = new Vector2(0.15f, 0.15f);

    [Tooltip("Extra scale percentage added on top of the calculated wall size. 50 = 1.5x.")]
    [Range(0f, 500f)]
    [SerializeField] private float extraScalePercent = 50f;

    [Tooltip("World Y position for the generated flame quads.")]
    [SerializeField] private float flameWorldY = 0.5f;

    [Tooltip("Rotate the generated quad on X so it lies under the ground plane.")]
    [SerializeField] private float flameRotationX = 90f;

    [Tooltip("Small Z offset so the flame quad renders above the fog quad / ground.")]
    [SerializeField] private float localZOffset = -0.02f;

    [Tooltip("Parent the generated quad under the wall object.")]
    [SerializeField] private bool parentUnderWall = true;

    [Tooltip("Use the wall object layer for the generated quad.")]
    [SerializeField] private bool copyLayerFromWall = true;

    [Tooltip("Sorting layer name for the generated MeshRenderer.")]
    [SerializeField] private string sortingLayerName = "Default";

    [Tooltip("Sorting order for the generated MeshRenderer.")]
    [SerializeField] private int sortingOrder = 5000;

    [Tooltip("Name prefix used to identify generated quads.")]
    [SerializeField] private string generatedPrefix = "__BlackFlame_";

    [Header("Bounds Resolution")]
    [Tooltip("Prefer Collider2D bounds when available.")]
    [SerializeField] private bool preferCollider2D = true;

    [Tooltip("Fall back to Renderer bounds if no Collider2D is found.")]
    [SerializeField] private bool fallbackToRendererBounds = true;

    [Tooltip("If nothing is found, use the Transform scale as a rough fallback.")]
    [SerializeField] private bool fallbackToTransformScale = true;

    [Header("Lifecycle")]
    [Tooltip("Destroy previously generated quads before generating again.")]
    [SerializeField] private bool clearPreviousGenerated = true;

    [Tooltip("Run automatically when the scene is loaded.")]
    [SerializeField] private bool generateOnSceneLoad = true;

    [Tooltip("Run automatically in Start() as a fallback.")]
    [SerializeField] private bool generateOnStart = true;

    [Tooltip("Prevent duplicate generation on the same component instance.")]
    [SerializeField] private bool generateOnlyOnce = true;

    private bool _generatedThisSession;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void GenerateAllAfterSceneLoad()
    {
        if (!Application.isPlaying)
            return;

        var spawners = Object.FindObjectsByType<WallBlackFlameSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (var spawner in spawners)
        {
            if (spawner == null)
                continue;

            if (spawner.generateOnSceneLoad)
                spawner.Generate();
        }
    }

    private void Start()
    {
        if (!Application.isPlaying)
            return;

        if (generateOnStart)
            Generate();
    }

    [ContextMenu("Generate Now")]
    public void Generate()
    {
        if (!Application.isPlaying)
            return;

        if (generateOnlyOnce && _generatedThisSession)
            return;

        if (flameMaterial == null)
        {
            Debug.LogError($"[{nameof(WallBlackFlameSpawner)}] Missing flame material on '{name}'.", this);
            return;
        }

        var allTransforms = Object.FindObjectsByType<Transform>(includeInactiveObjects ? FindObjectsInactive.Include : FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        int spawnedCount = 0;

        for (int i = 0; i < allTransforms.Length; i++)
        {
            Transform t = allTransforms[i];
            if (t == null)
                continue;

            GameObject go = t.gameObject;

            if (!go.scene.isLoaded)
                continue;

            if (!NameLooksLikeWall(go.name))
                continue;

            if (onlyRootWallObjects && HasWallAncestor(t))
                continue;

            if (clearPreviousGenerated)
                ClearGeneratedChildren(t);

            if (!TryResolveWorldBounds(t, out Bounds bounds))
                continue;

            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = generatedPrefix + go.name;
            quad.hideFlags = HideFlags.None;

            if (copyLayerFromWall)
                quad.layer = go.layer;

            Transform quadTransform = quad.transform;

            float scaleMultiplier = 1f + Mathf.Max(0f, extraScalePercent) / 100f;

            Vector3 desiredWorldSize = new Vector3(
                Mathf.Max(0.01f, bounds.size.x + extraPadding.x),
                Mathf.Max(0.01f, bounds.size.y + extraPadding.y),
                1f) * scaleMultiplier;

            Vector3 spawnWorldPosition = new Vector3(bounds.center.x, flameWorldY, bounds.center.z) + new Vector3(0f, 0f, localZOffset);
            Quaternion spawnWorldRotation = Quaternion.Euler(flameRotationX, 0f, 0f);

            if (parentUnderWall)
            {
                quadTransform.SetParent(t, true);
                quadTransform.position = spawnWorldPosition;
                quadTransform.rotation = spawnWorldRotation;
                quadTransform.localScale = CalculateLocalScaleForParent(t, desiredWorldSize);
            }
            else
            {
                quadTransform.SetParent(null, true);
                quadTransform.position = spawnWorldPosition;
                quadTransform.rotation = spawnWorldRotation;
                quadTransform.localScale = desiredWorldSize;
            }

            var renderer = quad.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = flameMaterial;
                renderer.sortingLayerName = sortingLayerName;
                renderer.sortingOrder = sortingOrder;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            var collider = quad.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            spawnedCount++;
        }

        _generatedThisSession = true;
        Debug.Log($"[{nameof(WallBlackFlameSpawner)}] Generated {spawnedCount} black flame quads.", this);
    }

    [ContextMenu("Clear Generated")]
    public void ClearGenerated()
    {
        if (!Application.isPlaying)
            return;

        var allTransforms = Object.FindObjectsByType<Transform>(includeInactiveObjects ? FindObjectsInactive.Include : FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        int removed = 0;

        for (int i = 0; i < allTransforms.Length; i++)
        {
            Transform t = allTransforms[i];
            if (t == null)
                continue;

            if (!t.gameObject.scene.isLoaded)
                continue;

            if (!NameLooksLikeWall(t.name))
                continue;

            removed += ClearGeneratedChildren(t);
        }

        _generatedThisSession = false;
        Debug.Log($"[{nameof(WallBlackFlameSpawner)}] Cleared {removed} generated quads.", this);
    }

    private int ClearGeneratedChildren(Transform wall)
    {
        int removed = 0;

        for (int i = wall.childCount - 1; i >= 0; i--)
        {
            Transform child = wall.GetChild(i);
            if (child == null)
                continue;

            if (!child.name.StartsWith(generatedPrefix))
                continue;

            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);

            removed++;
        }

        return removed;
    }

    private bool NameLooksLikeWall(string objectName)
    {
        return !string.IsNullOrEmpty(objectName) &&
               objectName.IndexOf(wallNameToken, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private bool HasWallAncestor(Transform t)
    {
        Transform p = t.parent;
        while (p != null)
        {
            if (NameLooksLikeWall(p.name))
                return true;

            p = p.parent;
        }

        return false;
    }

    private bool TryResolveWorldBounds(Transform wall, out Bounds bounds)
    {
        bounds = default;

        bool found = false;

        if (preferCollider2D)
        {
            var colliders = wall.GetComponentsInChildren<Collider2D>(includeInactiveObjects);
            for (int i = 0; i < colliders.Length; i++)
            {
                var c = colliders[i];
                if (c == null || !c.gameObject.scene.isLoaded)
                    continue;

                if (!found)
                {
                    bounds = c.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(c.bounds);
                }
            }
        }

        if (!found && fallbackToRendererBounds)
        {
            var renderers = wall.GetComponentsInChildren<Renderer>(includeInactiveObjects);
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null || !r.gameObject.scene.isLoaded)
                    continue;

                if (!found)
                {
                    bounds = r.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(r.bounds);
                }
            }
        }

        if (!found && fallbackToTransformScale)
        {
            Vector3 pos = wall.position;
            Vector3 scale = wall.lossyScale;

            bounds = new Bounds(
                pos,
                new Vector3(
                    Mathf.Max(0.1f, Mathf.Abs(scale.x)),
                    Mathf.Max(0.1f, Mathf.Abs(scale.y)),
                    1f));

            found = true;
        }

        return found;
    }

    private Vector3 CalculateLocalScaleForParent(Transform wall, Vector3 desiredWorldSize)
    {
        Vector3 parentScale = wall.lossyScale;

        float sx = Mathf.Abs(parentScale.x);
        float sy = Mathf.Abs(parentScale.y);

        if (sx < 0.0001f) sx = 1f;
        if (sy < 0.0001f) sy = 1f;

        return new Vector3(
            desiredWorldSize.x / sx,
            desiredWorldSize.y / sy,
            1f);
    }
}
