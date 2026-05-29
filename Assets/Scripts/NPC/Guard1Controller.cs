using System.Collections;
using UnityEngine;

public class Guard1Controller : MonoBehaviour
{
    private GuardState guardState;
    void Start()
    {
        guardState = GetComponent<GuardState>();
        guardState.state = GuardState.State.Idle;
        StartCoroutine(Delay());
    }

    IEnumerator Delay()
    {
        yield return new WaitForSeconds(2f);
        guardState.state = GuardState.State.Guarding;
    }
}
