// Chức năng: Xoay vùng camera quét qua lại theo bán nguyệt bằng Transform pivot, dùng cho visual 2D đặt phẳng trên map.
// Script này chỉ lo chuyển động quét; phần detect Player nằm ở CameraDetectionZone trên object trigger con.
// Tham chiếu với: CameraDetectionZone thường nằm cùng parent ScanPivot để xoay theo visual.
using UnityEngine;

public class CameraSweep2DVisual : MonoBehaviour
{
    public enum SweepAxis
    {
        X,
        Y,
        Z
    }

    [Header("Sweep")]
    [SerializeField] private Transform scanPivot;
    [SerializeField] private SweepAxis sweepAxis = SweepAxis.Y;
    [SerializeField] private float halfAngle = 75f;
    [SerializeField] private float sweepSpeed = 1f;
    [SerializeField] private bool smoothSweep = true;

    private Vector3 startLocalEuler;

    private void Awake()
    {
        if (scanPivot == null)
            scanPivot = transform;

        startLocalEuler = scanPivot.localEulerAngles;
    }

    private void Update()
    {
        if (scanPivot == null)
            return;

        float t = Mathf.PingPong(Time.time * sweepSpeed, 1f);

        if (smoothSweep)
            t = Mathf.SmoothStep(0f, 1f, t);

        float angleOffset = Mathf.Lerp(-halfAngle, halfAngle, t);
        Vector3 euler = startLocalEuler;

        switch (sweepAxis)
        {
            case SweepAxis.X:
                euler.x = startLocalEuler.x + angleOffset;
                break;

            case SweepAxis.Y:
                euler.y = startLocalEuler.y + angleOffset;
                break;

            case SweepAxis.Z:
                euler.z = startLocalEuler.z + angleOffset;
                break;
        }

        scanPivot.localEulerAngles = euler;
    }
}
