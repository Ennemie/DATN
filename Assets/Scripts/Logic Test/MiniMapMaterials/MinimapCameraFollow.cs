using UnityEngine;

public class MinimapCameraFollow : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject targetMinimap;

    [Header("Settings")]
    [SerializeField] private float height = 80f;

    private void LateUpdate()
    {
        if (targetMinimap == null)
            return;

        Vector3 pos = targetMinimap.transform.position;

        transform.position = new Vector3(
            pos.x,
            height,
            pos.z);

        transform.rotation = Quaternion.Euler(90f, 0f, 0f);
    }
}