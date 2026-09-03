using UnityEngine;

[RequireComponent(typeof(Collider))]
public class MeleeAttackController : MonoBehaviour
{
    private MeleeGuardController legacyMeleeGuardController;
    private EnemyMeleeCombatController performativeCombatController;
    private GuardState guardState;
    private Collider attackTriggerCollider;

    private void Reset()
    {
        Collider col = GetComponent<Collider>();
        attackTriggerCollider = col;

        if (col != null)
            col.isTrigger = true;
    }

    private void Awake()
    {
        legacyMeleeGuardController =
            GetComponentInParent<MeleeGuardController>();

        performativeCombatController =
            GetComponentInParent<EnemyMeleeCombatController>();

        guardState =
            GetComponentInParent<GuardState>();

        Collider col = GetComponent<Collider>();
        attackTriggerCollider = col;

        if (col != null)
            col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other))
            return;

        if (guardState != null &&
            guardState.IsStealthTakedownActive)
        {
            return;
        }

        if (performativeCombatController != null)
        {
            performativeCombatController.SetPlayerInAttackRange(
                other,
                attackTriggerCollider,
                true
            );

            return;
        }

        if (legacyMeleeGuardController != null)
        {
            legacyMeleeGuardController.isPlayerOnAttackRange = true;
            legacyMeleeGuardController._isReadyToAttack = true;
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (!IsPlayer(other))
            return;

        if (performativeCombatController != null)
        {
            performativeCombatController.SetPlayerInAttackRange(
                other,
                attackTriggerCollider,
                true
            );
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayer(other))
            return;

        if (performativeCombatController != null)
        {
            performativeCombatController.SetPlayerInAttackRange(
                other,
                attackTriggerCollider,
                false
            );

            return;
        }

        if (legacyMeleeGuardController != null)
            legacyMeleeGuardController.isPlayerOnAttackRange = false;
    }

    private bool IsPlayer(Collider other)
    {
        if (other == null)
            return false;

        if (other.CompareTag("Player"))
            return true;

        Transform root = other.transform.root;

        return root != null &&
               root.CompareTag("Player");
    }
}
