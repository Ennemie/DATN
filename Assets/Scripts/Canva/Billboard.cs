using UnityEngine;

public class Billboard : MonoBehaviour
{
    private Transform cam;

    void OnEnable()
    {
        cam = Camera.main.transform;
    }

    void LateUpdate()
    {
        transform.forward = cam.forward;
    }
}