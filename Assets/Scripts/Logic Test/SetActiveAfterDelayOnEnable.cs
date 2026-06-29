using System.Collections;
using UnityEngine;

public class SetActiveAfterDelayOnEnable : MonoBehaviour
{
    [Header("Delay")]
    [SerializeField] private float delay = 1f;

    [Header("Objects To Activate")]
    [SerializeField] private GameObject[] listActivate;

    [Header("Objects To Deactivate")]
    [SerializeField] private GameObject[] listDeactivate;

    private Coroutine delayRoutine;

    private void OnEnable()
    {
        if (delayRoutine != null)
            StopCoroutine(delayRoutine);

        delayRoutine = StartCoroutine(DelayRoutine());
    }

    private IEnumerator DelayRoutine()
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (listActivate != null)
        {
            for (int i = 0; i < listActivate.Length; i++)
            {
                if (listActivate[i] != null)
                    listActivate[i].SetActive(true);
            }
        }

        if (listDeactivate != null)
        {
            for (int i = 0; i < listDeactivate.Length; i++)
            {
                if (listDeactivate[i] != null)
                    listDeactivate[i].SetActive(false);
            }
        }

        delayRoutine = null;
    }

    private void OnDisable()
    {
        if (delayRoutine != null)
        {
            StopCoroutine(delayRoutine);
            delayRoutine = null;
        }
    }
}