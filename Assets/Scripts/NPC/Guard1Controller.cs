using System.Collections;
using UnityEngine;

/// <summary>
/// Guard controller that manages guard state and vision
/// </summary>
public class Guard1Controller : MonoBehaviour
{
    private GuardState guardState;
    // private NavMeshAgent agent;

    private void Start()
    {
        guardState = GetComponent<GuardState>();
        // agent = GetComponent<NavMeshAgent>();

        guardState.state = GuardState.State.Guarding;
        
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("SoundWave"))
        {
            if(guardState.isDetectPlayer) return;
            guardState.state = GuardState.State.DetectSoundWave;
        }
    }
}
