using UnityEngine;
using DG.Tweening;
using System.Runtime.CompilerServices;

public class ActivateObject : MonoBehaviour
{
    private Vector3 orgPos;
    [SerializeField] private Vector3 targetPos;
    private bool isActive = false;
    private BoxCollider collider;

    void Start()
    {
        orgPos = transform.localPosition;
        collider = GetComponentInChildren<BoxCollider>();
    }

    public void Activate()
    {
        Debug.Log("Activating: wall");
        transform.DOKill();
        if (!isActive)
        {
            collider.enabled = false;
            transform.DOLocalMove(targetPos, 0.5f).SetEase(Ease.OutQuad);
            isActive = true;
        }
        else
        {
            transform.DOLocalMove(orgPos, 0.5f).SetEase(Ease.OutQuad).OnComplete(() => collider.enabled = true);
            isActive = false;
        }
    }
}
