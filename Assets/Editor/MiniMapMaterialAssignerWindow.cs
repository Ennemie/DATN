using UnityEngine;
using UnityEditor;

public class MiniMapMaterialAssignerWindow : EditorWindow
{
    private Transform rootParent;
    private Material miniMapMaterial;
    private bool includeInactive = true;
    private bool affectSkinnedMeshRenderer = false;

    [MenuItem("Tools/Mini Map/Material Assigner")]
    public static void Open()
    {
        GetWindow<MiniMapMaterialAssignerWindow>("MiniMap Material");
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Mini Map Material Assigner", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Gán 1 material duy nhất cho toàn bộ MeshRenderer con bên trong một parent, kể cả hierarchy nhiều tầng.",
            MessageType.Info
        );

        EditorGUILayout.Space(6);
        rootParent = (Transform)EditorGUILayout.ObjectField("Parent Root", rootParent, typeof(Transform), true);
        miniMapMaterial = (Material)EditorGUILayout.ObjectField("MiniMap Material", miniMapMaterial, typeof(Material), false);

        EditorGUILayout.Space(6);
        includeInactive = EditorGUILayout.ToggleLeft("Include Inactive Objects", includeInactive);
        affectSkinnedMeshRenderer = EditorGUILayout.ToggleLeft("Also Affect SkinnedMeshRenderer", affectSkinnedMeshRenderer);

        EditorGUILayout.Space(10);

        using (new EditorGUI.DisabledScope(rootParent == null || miniMapMaterial == null))
        {
            if (GUILayout.Button("Gán Material Cho Tất Cả Object Con", GUILayout.Height(32)))
            {
                AssignMaterialRecursive();
            }
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.HelpBox(
            "Chỉ các object có MeshRenderer/SkinnedMeshRenderer mới bị đổi material. Các parent trống sẽ bị bỏ qua.",
            MessageType.None
        );
    }

    private void AssignMaterialRecursive()
    {
        if (rootParent == null || miniMapMaterial == null)
        {
            Debug.LogWarning("[MiniMapMaterialAssigner] Thiếu Parent Root hoặc MiniMap Material.");
            return;
        }

        int meshRendererCount = 0;
        int skinnedRendererCount = 0;
        int changedCount = 0;

        Undo.RegisterFullObjectHierarchyUndo(rootParent.gameObject, "Assign MiniMap Material");

        Transform[] allChildren = rootParent.GetComponentsInChildren<Transform>(includeInactive);

        foreach (Transform child in allChildren)
        {
            if (child == null)
                continue;

            MeshRenderer meshRenderer = child.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                meshRendererCount++;

                Material[] materials = new Material[1];
                materials[0] = miniMapMaterial;

                meshRenderer.sharedMaterials = materials;
                EditorUtility.SetDirty(meshRenderer);
                changedCount++;
            }

            if (affectSkinnedMeshRenderer)
            {
                SkinnedMeshRenderer skinnedMeshRenderer = child.GetComponent<SkinnedMeshRenderer>();
                if (skinnedMeshRenderer != null)
                {
                    skinnedRendererCount++;

                    Material[] materials = new Material[1];
                    materials[0] = miniMapMaterial;

                    skinnedMeshRenderer.sharedMaterials = materials;
                    EditorUtility.SetDirty(skinnedMeshRenderer);
                    changedCount++;
                }
            }
        }

        EditorUtility.SetDirty(rootParent);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"[MiniMapMaterialAssigner] Done. Root: {rootParent.name} | MeshRenderer changed: {meshRendererCount} | " +
            $"SkinnedMeshRenderer changed: {skinnedRendererCount} | Total assignments: {changedCount}"
        );
    }
}
