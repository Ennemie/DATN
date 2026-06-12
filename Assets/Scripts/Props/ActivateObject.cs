using UnityEngine;
using DG.Tweening;
using System.Runtime.CompilerServices;

public class ActivateObject : MonoBehaviour
{
    public enum ActivateType
    {
        Active,
        Move
    }
    [SerializeField] private ActivateType type;

    private Vector3 orgPos;
    [SerializeField] private Vector3 targetPos;
    private bool isActive = false;
    private BoxCollider collider;

    void Start()
    {
        orgPos = transform.localPosition;
        collider = GetComponentInChildren<BoxCollider>();
        if(type == ActivateType.Active) gameObject.SetActive(false);
    }

    public void Activate()
    {
        switch(type)
        {
            case ActivateType.Active:
                DoActive();
                break;
            case ActivateType.Move:
                DoMove();
                break;
        }
    }
    private void DoMove()
    {
        StartCoroutine(CameraTargetController.instance.FocusOnTarGet(transform));
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
    private void DoActive()
    {
        isActive = !isActive;
        gameObject.SetActive(isActive);
    }
}
