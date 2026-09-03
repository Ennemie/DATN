using UnityEngine;

/// <summary>
/// NEW FEATURE: Stable persistent identity for an Enemy.
/// 
/// The ID identifies the Enemy itself, not its checkpoint.
/// Example:
///     ENEMY_VILLA_001
/// 
/// CheckpointManager uses this ID when a dead Enemy is committed as permanent.
/// The GameSaveData stores the ID in PermanentDeadEnemyIds.
/// </summary>
public class EnemySaveIdentity : MonoBehaviour
{
    [Header("Persistent Identity")]
    [Tooltip(
        "Unique ID for this exact Enemy instance. " +
        "Do not reuse the same ID on two persistent enemies. " +
        "Keep the ID stable after a save exists."
    )]
    [SerializeField] private string enemyId;

    private bool warnedAboutFallback;

    public string PersistentEnemyId
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(enemyId))
                return enemyId.Trim();

            if (!warnedAboutFallback)
            {
                warnedAboutFallback = true;

                Debug.LogWarning(
                    "[EnemySaveIdentity] Enemy ID is empty on '" +
                    gameObject.name +
                    "'. A hierarchy-path fallback is being used. " +
                    "Assign a permanent unique ID in the Inspector for real persistence.",
                    this
                );
            }

            return BuildHierarchyFallback();
        }
    }

    private string BuildHierarchyFallback()
    {
        string path = gameObject.name;
        Transform current = transform.parent;

        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return gameObject.scene.name + "/" + path;
    }
}
