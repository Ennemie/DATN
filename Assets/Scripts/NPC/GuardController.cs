using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using DG.Tweening;

/// <summary>
/// Guard controller that manages guard state and vision
/// </summary>
public class GuardController : MonoBehaviour
{
    protected GameObject player;
    protected Vector3 playerPos;
    protected GuardState guardState;
    protected NavMeshAgent agent;
    protected Tween moveTween;
    protected float speed = 4f;
    protected bool isChasing = false;
    protected float distance;

    // Properties
    private EnemyProperties enemyProperties;

    protected virtual void Start()
    {
        guardState = GetComponent<GuardState>();
        player = GameObject.FindGameObjectWithTag("Player");
        enemyProperties = GetComponent<EnemyProperties>();
        agent = GetComponent<NavMeshAgent>();
        guardState.state = GuardState.State.Guarding;
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("SoundWave"))
        {
            Debug.Log("Guard detected sound wave");
            if(guardState.isDetectPlayer) return;
            guardState.state = GuardState.State.DetectSoundWave;
        }
    }
    protected void FocusOnPlayer()
    {
        transform.DOLookAt(playerPos, 0.5f);
    }
    public void TakeDamage(int damage)
    {
        enemyProperties.TakeDamage(damage);
    }
    public virtual void ChasePlayer(){}
    protected virtual void Attack(){}
}
