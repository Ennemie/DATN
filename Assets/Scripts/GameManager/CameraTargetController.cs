using System.Collections;
using UnityEngine;
using DG.Tweening;

public class CameraTargetController : MonoBehaviour
{
    public static CameraTargetController instance { get; private set; }
    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    [SerializeField] private Transform player;

    public IEnumerator FocusOnTarGet(Transform target)
    {
        transform.parent = null;
        transform.DOMove(target.position, 1f);
        yield return new WaitForSeconds(1.5f);
        transform.DOMove(player.position, 1f).OnComplete(() =>
        {
            transform.SetParent(player);
            transform.localPosition = new Vector3(0, 0, 0);
        });
    }
}
