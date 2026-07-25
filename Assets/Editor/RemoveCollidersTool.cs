using UnityEngine;
using UnityEditor;

public class RemoveCollidersTool : EditorWindow
{
    private Transform root;

    private bool removeColliders = true;
    private bool removeRigidbodies = true;

    [MenuItem("Tools/Remove Components")]
    public static void ShowWindow()
    {
        GetWindow<RemoveCollidersTool>("Remove Components");
    }

    private void OnGUI()
    {
        GUILayout.Label("Remove Components From Children", EditorStyles.boldLabel);

        root = (Transform)EditorGUILayout.ObjectField(
            "Root Object",
            root,
            typeof(Transform),
            true);

        EditorGUILayout.Space();

        removeColliders = EditorGUILayout.ToggleLeft("Remove Colliders", removeColliders);
        removeRigidbodies = EditorGUILayout.ToggleLeft("Remove Rigidbodies", removeRigidbodies);

        EditorGUILayout.Space();

        GUI.enabled = removeColliders || removeRigidbodies;

        if (GUILayout.Button("Remove Selected Components"))
        {
            if (root == null)
            {
                Debug.LogWarning("Please assign a Root Object.");
                return;
            }

            int removedColliders = 0;
            int removedRigidbodies = 0;

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Remove Components");

            if (removeColliders)
            {
                Collider[] colliders3D = root.GetComponentsInChildren<Collider>(true);
                foreach (Collider c in colliders3D)
                {
                    Undo.DestroyObjectImmediate(c);
                    removedColliders++;
                }

                Collider2D[] colliders2D = root.GetComponentsInChildren<Collider2D>(true);
                foreach (Collider2D c in colliders2D)
                {
                    Undo.DestroyObjectImmediate(c);
                    removedColliders++;
                }
            }

            if (removeRigidbodies)
            {
                Rigidbody[] rigidbodies3D = root.GetComponentsInChildren<Rigidbody>(true);
                foreach (Rigidbody rb in rigidbodies3D)
                {
                    Undo.DestroyObjectImmediate(rb);
                    removedRigidbodies++;
                }

                Rigidbody2D[] rigidbodies2D = root.GetComponentsInChildren<Rigidbody2D>(true);
                foreach (Rigidbody2D rb in rigidbodies2D)
                {
                    Undo.DestroyObjectImmediate(rb);
                    removedRigidbodies++;
                }
            }

            Debug.Log(
                $"Done!\n" +
                $"- Removed Colliders: {removedColliders}\n" +
                $"- Removed Rigidbodies: {removedRigidbodies}"
            );
        }

        GUI.enabled = true;
    }
}