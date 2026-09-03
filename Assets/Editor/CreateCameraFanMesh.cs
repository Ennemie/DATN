using UnityEngine;
using UnityEditor;

public static class CreateCameraFanMesh
{
    [MenuItem("Tools/Camera Detection/Create Fan Mesh")]
    private static void CreateFanMesh()
    {
        const string folderPath = "Assets/Meshes";

        if (!AssetDatabase.IsValidFolder("Assets/Meshes"))
        {
            AssetDatabase.CreateFolder("Assets", "Meshes");
        }

        float length = 10f;
        float halfAngle = 45f;
        float thickness = 0.05f;
        int arcSegments = 32;

        Mesh mesh = CreateFanPrism(
            length,
            halfAngle,
            thickness,
            arcSegments
        );

        string assetPath =
            AssetDatabase.GenerateUniqueAssetPath(
                folderPath + "/CameraDetectionFan.asset"
            );

        AssetDatabase.CreateAsset(mesh, assetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.FocusProjectWindow();
        Selection.activeObject = mesh;

        Debug.Log(
            "[CreateCameraFanMesh] Created fan mesh: " +
            assetPath
        );
    }

    private static Mesh CreateFanPrism(
        float length,
        float halfAngle,
        float thickness,
        int arcSegments)
    {
        halfAngle = Mathf.Clamp(
            halfAngle,
            0.1f,
            179f
        );

        length = Mathf.Max(
            0.01f,
            length
        );

        thickness = Mathf.Max(
            0.001f,
            thickness
        );

        arcSegments = Mathf.Max(
            3,
            arcSegments
        );

        int perimeterPointCount =
            arcSegments + 2;

        Vector3[] vertices =
            new Vector3[perimeterPointCount * 2];

        int[] triangles =
            new int[
                (perimeterPointCount - 2) * 3 * 2
                + perimeterPointCount * 6
            ];

        float halfThickness =
            thickness * 0.5f;

        // =========================================================
        // TOP / BOTTOM POLYGON
        //
        // First point = center
        // Next points = arc
        // =========================================================

        vertices[0] =
            new Vector3(
                0f,
                -halfThickness,
                0f
            );

        for (int i = 0; i <= arcSegments; i++)
        {
            float normalized =
                (float)i / arcSegments;

            float angle =
                Mathf.Lerp(
                    -halfAngle,
                    halfAngle,
                    normalized
                );

            float radians =
                angle * Mathf.Deg2Rad;

            float x =
                Mathf.Sin(radians) * length;

            float z =
                Mathf.Cos(radians) * length;

            vertices[i + 1] =
                new Vector3(
                    x,
                    -halfThickness,
                    z
                );
        }

        int bottomOffset =
            perimeterPointCount;

        vertices[bottomOffset] =
            new Vector3(
                0f,
                halfThickness,
                0f
            );

        for (int i = 0; i <= arcSegments; i++)
        {
            float normalized =
                (float)i / arcSegments;

            float angle =
                Mathf.Lerp(
                    -halfAngle,
                    halfAngle,
                    normalized
                );

            float radians =
                angle * Mathf.Deg2Rad;

            float x =
                Mathf.Sin(radians) * length;

            float z =
                Mathf.Cos(radians) * length;

            vertices[
                bottomOffset + i + 1
            ] =
                new Vector3(
                    x,
                    halfThickness,
                    z
                );
        }

        int triangleIndex = 0;

        // =========================================================
        // BOTTOM / TOP
        // =========================================================

        for (int i = 1; i < perimeterPointCount - 1; i++)
        {
            triangles[triangleIndex++] = 0;
            triangles[triangleIndex++] = i + 1;
            triangles[triangleIndex++] = i;
        }

        for (int i = 1; i < perimeterPointCount - 1; i++)
        {
            triangles[triangleIndex++] =
                bottomOffset;

            triangles[triangleIndex++] =
                bottomOffset + i;

            triangles[triangleIndex++] =
                bottomOffset + i + 1;
        }

        // =========================================================
        // OUTER / SIDE WALLS
        // =========================================================

        for (int i = 0; i < perimeterPointCount; i++)
        {
            int next =
                (i + 1) %
                perimeterPointCount;

            int bottomCurrent = i;
            int bottomNext = next;

            int topCurrent =
                bottomOffset + i;

            int topNext =
                bottomOffset + next;

            triangles[triangleIndex++] =
                bottomCurrent;

            triangles[triangleIndex++] =
                topCurrent;

            triangles[triangleIndex++] =
                topNext;

            triangles[triangleIndex++] =
                bottomCurrent;

            triangles[triangleIndex++] =
                topNext;

            triangles[triangleIndex++] =
                bottomNext;
        }

        Mesh mesh = new Mesh();

        mesh.name =
            "CameraDetectionFan";

        mesh.vertices =
            vertices;

        mesh.triangles =
            triangles;

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }
}