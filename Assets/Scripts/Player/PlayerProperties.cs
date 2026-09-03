using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Player health/properties with checkpoint persistence and reusable death/failure sequence.
/// </summary>
public class PlayerProperties : MonoBehaviour
{
    public static PlayerProperties Instance { get; private set; }

    // Chức năng mới:
    // Báo ngay khi Player bắt đầu Death/Failure.
    // EnemyCheckpointHandler dùng tín hiệu này để ngắt Chase ngay lập tức.
    public static event Action DeathOrFailureStarted;

    private Vector3 respawnPosition;

    [SerializeField] private Slider hpBar;

    [Header("Checkpoint / Death Integration")]
    [SerializeField] private CheckpointManager checkpointManager;
    [SerializeField] private DeathSpreadEffectController deathSpreadEffect;

    [Tooltip("Fallback spawn if a checkpoint is unavailable. Normally this is only used for safety.")]
    [SerializeField] private Transform fallbackSpawnPoint;

    [SerializeField] private int maxHealth = 100;

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    private int hp;
    private bool isInDeathSequence;
    private bool isApplyingCheckpointRestore;

    public int CurrentHealth => hp;
    public int MaxHealth => maxHealth;
    public bool IsDeadOrFailing => isInDeathSequence;

    public int _hp
    {
        get { return hp; }
        set
        {
            if (hp != value)
            {
                hp = Mathf.Clamp(
                    value,
                    0,
                    maxHealth
                );

                UpdateHpBar();

                // CŨ:
                // if (hp <= 0)
                // {
                //     transform.position = respawnPosition;
                //     hp = 100;
                //     UpdateHpBar();
                // }
                //
                // MỚI:
                // Chỉ HP = 0 thật sự mới phát Dead animation.
                if (hp <= 0 &&
                    !isInDeathSequence &&
                    !isApplyingCheckpointRestore)
                {
                    BeginDeathOrFailureSequence(true);
                }
            }
        }
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        ResolveCheckpointManager();
        SubscribeCheckpointEvents();
    }

    private void Start()
    {
        ResolveCheckpointManager();
        SubscribeCheckpointEvents();

        // CŨ:
        // _hp = 100;
        //
        // MỚI:
        // New Game vẫn khởi tạo full HP.
        // Continue sẽ overwrite bằng committed health.
        _hp = maxHealth;

        if (fallbackSpawnPoint == null)
            fallbackSpawnPoint = transform;

        respawnPosition =
            fallbackSpawnPoint.position;
    }

    private void OnDestroy()
    {
        UnsubscribeCheckpointEvents();

        if (Instance == this)
            Instance = null;
    }

    public void TakeDamage(int damage)
    {
        if (isInDeathSequence)
            return;

        _hp -= Mathf.Max(
            0,
            damage
        );
    }

    public void Heal(int amount)
    {
        if (isInDeathSequence)
            return;

        _hp += Mathf.Max(
            0,
            amount
        );
    }

    /// <summary>
    /// Chức năng mới:
    /// Alarm/mission detection đánh thua Player.
    ///
    /// Điều chỉnh:
    /// Failure không phải Death.
    /// Không chuyển PlayerState sang Dead.
    /// Vẫn dùng DeathSpread + Checkpoint restore.
    /// </summary>
    public void RequestFailureToCheckpoint()
    {
        if (isInDeathSequence)
            return;

        BeginDeathOrFailureSequence(
            false
        );
    }

    /// <summary>
    /// Chức năng mới:
    /// Áp dụng HP đã commit mà không kích hoạt Death.
    /// </summary>
    public void ApplyCommittedHealth(
        int savedHealth)
    {
        isApplyingCheckpointRestore = true;

        hp = Mathf.Clamp(
            savedHealth,
            0,
            maxHealth
        );

        UpdateHpBar();

        isApplyingCheckpointRestore = false;
    }

    // CŨ:
    // private void BeginDeathOrFailureSequence()
    // {
    //     if (isInDeathSequence)
    //         return;
    //
    //     isInDeathSequence = true;
    //
    //     ResolveCheckpointManager();
    //     SubscribeCheckpointEvents();
    //
    //     if (PlayerController.Instance != null)
    //     {
    //         PlayerController.Instance.SetExternalActionLock(true);
    //         PlayerController.Instance.isAttacking = false;
    //     }
    //
    //     if (PlayerState.Instance != null)
    //         PlayerState.Instance.CurrentState = PlayerState.State.Dead;
    //
    //     if (deathSpreadEffect != null)
    //     {
    //         deathSpreadEffect.PlaySequence(
    //             HandleDeathSequenceFullyCovered,
    //             HandleDeathSequenceFinished
    //         );
    //     }
    //     else
    //     {
    //         HandleDeathSequenceFullyCovered();
    //         HandleDeathSequenceFinished();
    //     }
    // }
    //
    // MỚI:
    // Chức năng mới:
    // isActualDeath = true khi HP = 0.
    // isActualDeath = false khi Alarm/Failure.
    private void BeginDeathOrFailureSequence(
        bool isActualDeath)
    {
        if (isInDeathSequence)
            return;

        isInDeathSequence = true;

        ResolveCheckpointManager();
        SubscribeCheckpointEvents();

        // Chức năng mới:
        // Enemy nhận tín hiệu ngay tại thời điểm Failure/Death bắt đầu.
        DeathOrFailureStarted?.Invoke();

        if (PlayerController.Instance != null)
        {
            PlayerController.Instance
                .SetExternalActionLock(true);

            PlayerController.Instance.isAttacking =
                false;
        }

        // CŨ:
        // if (PlayerState.Instance != null)
        //     PlayerState.Instance.CurrentState = PlayerState.State.Dead;
        //
        // MỚI:
        // Chỉ Death thật sự mới play Dead animation.
        if (isActualDeath)
        {
            if (PlayerState.Instance != null)
            {
                PlayerState.Instance.CurrentState =
                    PlayerState.State.Dead;
            }
        }
        else
        {
            if (PlayerState.Instance != null &&
                PlayerState.Instance.CurrentState ==
                PlayerState.State.Dead)
            {
                PlayerState.Instance.CurrentState =
                    PlayerState.State.Idle;
            }
        }

        if (deathSpreadEffect != null)
        {
            deathSpreadEffect.PlaySequence(
                HandleDeathSequenceFullyCovered,
                HandleDeathSequenceFinished
            );
        }
        else
        {
            HandleDeathSequenceFullyCovered();
            HandleDeathSequenceFinished();
        }
    }

    private void HandleDeathSequenceFullyCovered()
    {
        bool restored = false;

        ResolveCheckpointManager();

        if (checkpointManager != null)
        {
            restored =
                checkpointManager
                    .RestoreCheckpoint();
        }

        if (!restored)
        {
            Debug.LogWarning(
                "[PlayerProperties] Death/failure restore requested but no valid CheckpointManager checkpoint exists. " +
                "Using fallback spawn and full health.",
                this
            );

            ApplyFallbackRestore();
        }
    }

    private void HandleCheckpointRestore(
        CheckpointManager.CheckpointState checkpointState)
    {
        if (checkpointState.CheckpointTransform == null)
            return;

        isApplyingCheckpointRestore =
            true;

        if (PlayerController.Instance != null)
        {
            PlayerController.Instance
                .SetExternalActionLock(true);

            PlayerController.Instance
                .ResetAfterCheckpointRestore();
        }

        transform.SetPositionAndRotation(
            checkpointState.CheckpointTransform.position,
            checkpointState.CheckpointTransform.rotation
        );

        ResolveDataManagerHealthAndApply();

        if (PlayerState.Instance != null)
        {
            PlayerState.Instance.CurrentState =
                PlayerState.State.Idle;
        }

        if (PlayerController.Instance != null)
        {
            PlayerController.Instance.acceptInput =
                true;
        }

        isApplyingCheckpointRestore =
            false;

        if (logDebug)
        {
            Debug.Log(
                "[PlayerProperties] Player restored to checkpoint. " +
                "Position = " + transform.position +
                ", Health = " + hp,
                this
            );
        }
    }

    private void ResolveDataManagerHealthAndApply()
    {
        GameDataManager dataManager =
            GameDataManager.Instance;

        if (dataManager != null &&
            dataManager.HasValidSaveData)
        {
            ApplyCommittedHealth(
                dataManager
                    .GetPlayerHealthOrDefault(
                        maxHealth
                    )
            );
        }
        else
        {
            ApplyCommittedHealth(
                maxHealth
            );
        }
    }

    private void ApplyFallbackRestore()
    {
        isApplyingCheckpointRestore =
            true;

        if (fallbackSpawnPoint != null)
        {
            transform.SetPositionAndRotation(
                fallbackSpawnPoint.position,
                fallbackSpawnPoint.rotation
            );
        }
        else
        {
            transform.position =
                respawnPosition;
        }

        hp = maxHealth;

        UpdateHpBar();

        if (PlayerState.Instance != null)
        {
            PlayerState.Instance.CurrentState =
                PlayerState.State.Idle;
        }

        if (PlayerController.Instance != null)
            PlayerController.Instance
                .ResetAfterCheckpointRestore();

        isApplyingCheckpointRestore =
            false;

        isInDeathSequence =
            false;
    }

    private void HandleDeathSequenceFinished()
    {
        isInDeathSequence =
            false;

        if (PlayerController.Instance != null)
            PlayerController.Instance.acceptInput =
                true;

        if (PlayerState.Instance != null &&
            PlayerState.Instance.CurrentState ==
            PlayerState.State.Dead)
        {
            PlayerState.Instance.CurrentState =
                PlayerState.State.Idle;
        }
    }

    private void ResolveCheckpointManager()
    {
        if (checkpointManager != null)
            return;

        checkpointManager =
            FindAnyObjectByType<CheckpointManager>();
    }

    private void SubscribeCheckpointEvents()
    {
        if (checkpointManager == null)
            return;

        checkpointManager
            .CheckpointRestoreRequested -=
            HandleCheckpointRestore;

        checkpointManager
            .CheckpointRestoreRequested +=
            HandleCheckpointRestore;
    }

    private void UnsubscribeCheckpointEvents()
    {
        if (checkpointManager == null)
            return;

        checkpointManager
            .CheckpointRestoreRequested -=
            HandleCheckpointRestore;
    }

    private void UpdateHpBar()
    {
        if (hpBar != null)
        {
            hpBar.value =
                maxHealth <= 0
                    ? 0f
                    : (float)hp /
                      maxHealth;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("CheckPoint"))
        {
            // CŨ:
            // respawnPosition = other.transform.position;
            //
            // MỚI:
            // Giữ fallback cũ để scene cũ không bị phá.
            // CheckpointManager vẫn là authority chính.
            respawnPosition =
                other.transform.position;
        }
    }
}
