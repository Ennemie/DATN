using UnityEngine;

[DefaultExecutionOrder(10000)]
public class FreezeCameraRotation : MonoBehaviour
{
    [Header("Freeze Rotation")]
    [SerializeField] private bool useCurrentRotationOnStart = true;

    [SerializeField] private Vector3 fixedEulerRotation = new Vector3(45f, 0f, 0f);

    private Quaternion fixedRotation;

    private void Awake()
    {
        if (useCurrentRotationOnStart)
        {
            fixedRotation = transform.rotation;
        }
        else
        {
            fixedRotation = Quaternion.Euler(fixedEulerRotation);
        }
    }

    private void LateUpdate()
    {
        LockRotation();
    }

    private void OnPreCull()
    {
        LockRotation();
    }

    private void LockRotation()
    {
        transform.rotation = fixedRotation;
    }
}