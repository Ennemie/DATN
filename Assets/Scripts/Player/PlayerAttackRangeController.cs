using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PlayerAttackRangeController : MonoBehaviour
{
    public static PlayerAttackRangeController Instance { get; private set; }

    private readonly Dictionary<Collider, EnemyCheckpointHandler> colliderTargets =
        new Dictionary<Collider, EnemyCheckpointHandler>();

    private readonly Dictionary<EnemyCheckpointHandler, int> targetOverlapCounts =
        new Dictionary<EnemyCheckpointHandler, int>();

    private StealthTakedownController controller;
    private Collider attackRangeTrigger;

    private void Reset()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        controller =
            GetComponentInParent<StealthTakedownController>();

        attackRangeTrigger =
            GetComponent<Collider>();

        if (attackRangeTrigger != null)
            attackRangeTrigger.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        RegisterCollider(other);
    }

    private void Update()
    {
        if (colliderTargets.Count == 0)
            return;

        List<Collider> staleColliders = null;

        foreach (KeyValuePair<Collider, EnemyCheckpointHandler> pair
                 in colliderTargets)
        {
            Collider collider = pair.Key;
            EnemyCheckpointHandler handler = pair.Value;

            if (collider == null ||
                !collider.enabled ||
                !collider.gameObject.activeInHierarchy ||
                handler == null ||
                handler.IsRuntimeDead ||
                handler.IsPermanentlyDead ||
                !IsColliderActuallyInside(collider))
            {
                if (staleColliders == null)
                    staleColliders =
                        new List<Collider>();

                if (collider != null &&
                    !staleColliders.Contains(collider))
                {
                    staleColliders.Add(collider);
                }
            }
        }

        if (staleColliders == null)
            return;

        for (int i = 0;
             i < staleColliders.Count;
             i++)
        {
            UnregisterCollider(staleColliders[i]);
        }
    }

    private void OnTriggerStay(Collider other)
    {
        RegisterCollider(other);
    }

    private void OnTriggerExit(Collider other)
    {
        UnregisterCollider(other);
    }

    private void RegisterCollider(Collider other)
    {
        if (other == null ||
            colliderTargets.ContainsKey(other))
        {
            return;
        }

        EnemyCheckpointHandler handler =
            ResolveEnemyHandler(other);

        if (handler == null)
            return;

        colliderTargets[other] = handler;

        if (!targetOverlapCounts.TryGetValue(
                handler,
                out int count))
        {
            count = 0;
        }

        count++;
        targetOverlapCounts[handler] = count;

        if (count == 1 &&
            controller != null)
        {
            controller.RegisterCombatTarget(handler);
        }
    }

    private void UnregisterCollider(Collider other)
    {
        if (other == null)
            return;

        if (!colliderTargets.TryGetValue(
                other,
                out EnemyCheckpointHandler handler))
        {
            return;
        }

        colliderTargets.Remove(other);

        if (!targetOverlapCounts.TryGetValue(
                handler,
                out int count))
        {
            return;
        }

        count--;

        if (count <= 0)
        {
            targetOverlapCounts.Remove(handler);

            if (controller != null)
            {
                controller.UnregisterCombatTarget(
                    handler
                );
            }
        }
        else
        {
            targetOverlapCounts[handler] = count;
        }
    }

    private bool IsColliderActuallyInside(
        Collider enemyCollider)
    {
        if (enemyCollider == null ||
            enemyCollider.isTrigger ||
            attackRangeTrigger == null ||
            !attackRangeTrigger.enabled ||
            !attackRangeTrigger.gameObject.activeInHierarchy)
        {
            return false;
        }

        try
        {
            return Physics.ComputePenetration(
                attackRangeTrigger,
                attackRangeTrigger.transform.position,
                attackRangeTrigger.transform.rotation,
                enemyCollider,
                enemyCollider.transform.position,
                enemyCollider.transform.rotation,
                out _,
                out _
            );
        }
        catch
        {
            return attackRangeTrigger.bounds.Intersects(
                enemyCollider.bounds
            );
        }
    }

    private EnemyCheckpointHandler ResolveEnemyHandler(
        Collider other)
    {
        if (other == null)
            return null;

        Transform root = other.transform.root;

        if (root == null)
            return null;

        EnemyCheckpointHandler handler =
            root.GetComponent<EnemyCheckpointHandler>();

        if (handler == null &&
            !root.CompareTag("Enemy"))
        {
            return null;
        }

        return handler;
    }

    public void HitEnemy()
    {
        if (controller != null &&
            controller.TryPunch())
        {
            return;
        }

        Debug.Log(
            "[PlayerAttackRangeController] HitEnemy() called but no unified Punch was accepted.",
            this
        );
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnDisable()
    {
        if (controller != null)
        {
            foreach (EnemyCheckpointHandler handler
                     in targetOverlapCounts.Keys)
            {
                if (handler != null)
                    controller.UnregisterCombatTarget(handler);
            }
        }

        colliderTargets.Clear();
        targetOverlapCounts.Clear();
    }
}
