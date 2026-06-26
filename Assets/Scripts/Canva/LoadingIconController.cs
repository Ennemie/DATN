using UnityEngine;
using DG.Tweening;

public class LoadingIconController : MonoBehaviour
{
    [SerializeField] private float rotationDuration; // Thời gian xoay một vòng
    private Tween tween;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void OnEnable()
    {
        transform.DOScale(new Vector3(0.15f, 0.15f, 0.15f), 0.8f).From(Vector3.zero).SetEase(Ease.OutBack); // Hiệu ứng phóng to khi xuất hiện
        tween = transform.DOLocalRotate(new Vector3(0, 0, -360f), rotationDuration, RotateMode.FastBeyond360)
            .SetEase(Ease.Linear) // Ensures a consistent rotation speed
            .SetLoops(-1, LoopType.Incremental) // Loops indefinitely
            .SetLink(gameObject); // Links the tween to the GameObject, so it stops when the GameObject is disabled
    }
    void OnDisable()
    {
        if (tween != null && tween.IsActive())
        {
            tween.Kill(); // Dừng tween khi GameObject bị vô hiệu hóa
        }
    }
}
