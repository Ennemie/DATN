using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Performative melee authority for the new Enemy route system.
///
/// This controller owns the paired Enemy Attack presentation and receives
/// the Player's three-hit Punch sequence. EnemyRoutePerformer remains the
/// normal movement authority, while this controller temporarily locks it
/// during paired animation alignment/knockback.
/// </summary>
[DisallowMultipleComponent]
public class EnemyMeleeCombatController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnemyCheckpointHandler enemyCheckpointHandler;
    [SerializeField] private EnemyRoutePerformer routePerformer;
    [SerializeField] private EnemyProperties enemyProperties;
    [SerializeField] private GuardVisionView visionView;
    [SerializeField] private Animator animator;

    [Header("Enemy Attack")]
    [SerializeField] private string attack1AnimatorName = "Attack1";
    [SerializeField] private string attack2AnimatorName = "Attack2";
    [SerializeField] private string getHitAnimatorName = "GetHit";

    [Tooltip("Damage dealt to Player by each Enemy attack.")]
    [Min(0)]
    [SerializeField] private int enemyAttackDamage = 10;

    [Tooltip("Attack1/Attack2/GetHit playback speed.")]
    [Min(0.01f)]
    [SerializeField] private float attackPlaybackSpeed = 1.5f;

    [Tooltip("Normalized point of Enemy Attack animation where the Player is hit.")]
    [Range(0.05f, 0.95f)]
    [SerializeField] private float attackHitNormalizedTime = 0.5f;

    [Tooltip("Cooldown starts after the Enemy attack finishes.")]
    [Min(0f)]
    [SerializeField] private float attackCooldown = 0.5f;

    [Tooltip("Distance between Player and Enemy while paired attack animation plays.")]
    [Min(0.01f)]
    [SerializeField] private float pairDistance = 0.5f;

    [Header("Player Knockback")]
    [Min(0f)]
    [SerializeField] private float playerKnockbackDistance = 0.3f;

    [Min(0.01f)]
    [SerializeField] private float playerKnockbackDuration = 0.12f;

    [Header("Enemy Attack Audio")]
    [Tooltip("AudioSource nằm trên chính Enemy. Attack sẽ dùng PlayOneShot trên source này.")]
    [SerializeField] private AudioSource attackAudioSource;
    [SerializeField] private AudioClip enemyAttackSoundClip;

    [Range(0f, 1f)]
    [SerializeField] private float enemyAttackSoundVolume = 1f;

    [Header("Enemy Attack Trigger")]
    [Tooltip("Collider trigger trước mặt Enemy. Mặc định tự tìm từ MeleeAttackController.")]
    [SerializeField] private Collider attackRangeTrigger;

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    private readonly HashSet<Collider> playerCollidersInside =
        new HashSet<Collider>();

    private bool attackInProgress;
    private float nextAttackAllowedAt;
    private Coroutine attackRoutine;
    private Coroutine enemyGetHitPresentationRoutine;
    private bool visionLockedForPlayerPunch;

    public bool IsAttackInProgress =>
        attackInProgress;

    public bool IsAttackCoolingDown =>
        Time.time < nextAttackAllowedAt;

    public bool CanReceivePlayerPunch =>
        !attackInProgress &&
        enemyCheckpointHandler != null &&
        enemyProperties != null &&
        !enemyCheckpointHandler.IsRuntimeDead &&
        !enemyCheckpointHandler.IsPermanentlyDead;

    private void Awake()
    {
        ResolveReferences();

        if (attackAudioSource == null)
            attackAudioSource = GetComponent<AudioSource>();

        if (attackRangeTrigger == null)
        {
            MeleeAttackController meleeTrigger =
                GetComponentInChildren<MeleeAttackController>(true);

            if (meleeTrigger != null)
                attackRangeTrigger =
                    meleeTrigger.GetComponent<Collider>();
        }
    }

    private void OnEnable()
    {
        ResolveReferences();

        PlayerProperties.DeathOrFailureStarted -=
            HandlePlayerDeathOrFailureStarted;

        PlayerProperties.DeathOrFailureStarted +=
            HandlePlayerDeathOrFailureStarted;
    }

    private void OnDisable()
    {
        if (attackRoutine != null)
        {
            StopCoroutine(
                attackRoutine
            );

            attackRoutine = null;
        }

        attackInProgress = false;
        StopEnemyGetHitPresentation();

        if (routePerformer != null &&
            routePerformer.IsMeleeCombatLocked)
        {
            routePerformer.StopForCheckpointRestore();
        }

        if (animator != null)
            animator.speed = 1f;

        PlayerProperties.DeathOrFailureStarted -=
            HandlePlayerDeathOrFailureStarted;

        playerCollidersInside.Clear();
    }

    public void SetPlayerInAttackRange(
        Collider playerCollider,
        bool inside)
    {
        SetPlayerInAttackRange(
            playerCollider,
            null,
            inside
        );
    }

    public void SetPlayerInAttackRange(
        Collider playerCollider,
        Collider triggerCollider,
        bool inside)
    {
        if (playerCollider == null ||
            !IsPlayer(playerCollider))
        {
            return;
        }

        if (triggerCollider != null)
            attackRangeTrigger = triggerCollider;

        if (inside)
            playerCollidersInside.Add(playerCollider);
        else
            playerCollidersInside.Remove(playerCollider);

        if (inside)
            TryStartEnemyAttack();
    }

    private void Update()
    {
        IsPlayerCurrentlyInside();

        if (!attackInProgress)
            TryStartEnemyAttack();
    }

    private void TryStartEnemyAttack()
    {
        if (attackInProgress ||
            Time.time < nextAttackAllowedAt)
        {
            return;
        }

        if (!IsPlayerCurrentlyInside())
            return;

        if (enemyCheckpointHandler == null ||
            enemyCheckpointHandler.IsRuntimeDead ||
            enemyCheckpointHandler.IsPermanentlyDead)
        {
            return;
        }

        if (routePerformer == null ||
            !routePerformer.enabled ||
            !routePerformer.IsChasing ||
            routePerformer.IsMeleeCombatLocked)
        {
            return;
        }

        // Alarm enemies do not enter the normal melee chase pipeline.
        if (routePerformer.IsAlarmConfigured)
            return;

        PlayerProperties player =
            PlayerProperties.Instance;

        if (player == null ||
            player.IsDeadOrFailing)
        {
            return;
        }

        PlayerState playerState =
            player.GetComponent<PlayerState>();

        if (playerState != null &&
            playerState.IsExclusiveActionState)
        {
            return;
        }

        StealthTakedownController playerCombat =
            player.GetComponent<StealthTakedownController>();

        if (playerCombat != null &&
            playerCombat.IsPlayerMeleeActionInProgress)
        {
            return;
        }

        attackRoutine =
            StartCoroutine(
                PerformEnemyAttackRoutine()
            );
    }

    private IEnumerator PerformEnemyAttackRoutine()
    {
        attackInProgress = true;

        if (routePerformer != null)
            routePerformer.BeginMeleeCombatLock();

        PlayerProperties player =
            PlayerProperties.Instance;

        if (player == null)
        {
            FinishEnemyAttack(false);
            yield break;
        }

        StealthTakedownController playerCombat =
            player.GetComponent<StealthTakedownController>();

        string attackStateName =
            Random.Range(0, 2) == 0
                ? attack1AnimatorName
                : attack2AnimatorName;

        float attackDuration =
            GetAnimatorStateDuration(
                attackStateName,
                attackPlaybackSpeed
            );

        if (attackDuration <= 0f)
        {
            Debug.LogError(
                "[EnemyMeleeCombatController] ENEMY ATTACK ABORTED: invalid animation state/duration | State=" +
                attackStateName,
                this
            );

            FinishEnemyAttack(false);
            yield break;
        }

        if (!AlignEnemyAndPlayer(player.transform))
        {
            Debug.LogError(
                "[EnemyMeleeCombatController] ENEMY ATTACK ABORTED: pair alignment failed.",
                this
            );

            FinishEnemyAttack(false);
            yield break;
        }

        if (!PlayAnimatorOneShot(
                attackStateName,
                attackPlaybackSpeed
            ))
        {
            Debug.LogError(
                "[EnemyMeleeCombatController] ENEMY ATTACK ABORTED: Animator state rejected | State=" +
                attackStateName,
                this
            );

            FinishEnemyAttack(false);
            yield break;
        }

        PlaySound(
            enemyAttackSoundClip,
            transform.position,
            enemyAttackSoundVolume
        );

        float hitDelay =
            attackDuration *
            Mathf.Clamp01(attackHitNormalizedTime);

        yield return new WaitForSeconds(hitDelay);

        if (player == null ||
            player.IsDeadOrFailing)
        {
            FinishEnemyAttack(true);
            yield break;
        }

        float remainingAttackTime =
            Mathf.Max(
                0f,
                attackDuration - hitDelay
            );

        if (playerCombat != null)
        {
            playerCombat.BeginEnemyAttackHitPresentation(
                this,
                enemyAttackDamage,
                pairDistance,
                playerKnockbackDistance,
                playerKnockbackDuration,
                remainingAttackTime
            );
        }
        else
        {
            player.TakeDamage(
                enemyAttackDamage
            );
        }

        if (logDebug)
        {
            Debug.Log(
                "[EnemyMeleeCombatController] ENEMY HIT PLAYER | " +
                "Enemy=" + gameObject.name +
                " | Attack=" + attackStateName +
                " | Damage=" + enemyAttackDamage +
                " | HitDelay=" + hitDelay.ToString("0.###") +
                "s",
                this
            );
        }

        yield return new WaitForSeconds(
            remainingAttackTime
        );

        FinishEnemyAttack(true);
    }

    private void FinishEnemyAttack(
        bool startCooldown)
    {
        StopAttackRoutineReferenceOnly();

        attackInProgress = false;

        if (animator != null)
            animator.speed = 1f;

        if (startCooldown)
        {
            nextAttackAllowedAt =
                Time.time +
                Mathf.Max(
                    0f,
                    attackCooldown
                );
        }

        bool resumeChase =
            routePerformer != null &&
            routePerformer.enabled &&
            !routePerformer.IsAlarmConfigured &&
            PlayerProperties.Instance != null &&
            !PlayerProperties.Instance.IsDeadOrFailing &&
            routePerformer.IsChasing;

        if (routePerformer != null)
        {
            routePerformer.EndMeleeCombatLock(
                resumeChase
            );
        }

        if (logDebug)
        {
            Debug.Log(
                "[EnemyMeleeCombatController] ENEMY ATTACK COMPLETE | " +
                "Enemy=" + gameObject.name +
                " | ResumeChase=" + resumeChase +
                " | NextAttackAt=" +
                nextAttackAllowedAt.ToString("0.###"),
                this
            );
        }
    }

    public void BeginPlayerPunchSequenceLock()
    {
        if (routePerformer != null &&
            !routePerformer.IsMeleeCombatLocked)
        {
            routePerformer.BeginMeleeCombatLock();
        }

        LockVisionForPlayerPunch();

        nextAttackAllowedAt =
            Mathf.Max(
                nextAttackAllowedAt,
                Time.time
            );
    }

    public void EndPlayerPunchSequenceLock(
        bool sequenceSucceeded)
    {
        StopEnemyGetHitPresentation();

        if (routePerformer != null &&
            routePerformer.IsMeleeCombatLocked)
        {
            bool enemyIsDead =
                enemyCheckpointHandler != null &&
                (enemyCheckpointHandler.IsRuntimeDead ||
                 enemyCheckpointHandler.IsPermanentlyDead);

            if (enemyIsDead)
            {
                routePerformer.StopForCheckpointRestore();
            }
            else
            {
                UnlockVisionAfterPlayerPunch();

                bool resumeChase =
                    sequenceSucceeded &&
                    PlayerProperties.Instance != null &&
                    !PlayerProperties.Instance.IsDeadOrFailing &&
                    routePerformer.IsChasing;

                routePerformer.EndMeleeCombatLock(
                    resumeChase
                );
            }
        }

        if (routePerformer == null)
            UnlockVisionAfterPlayerPunch();

        if (logDebug)
        {
            Debug.Log(
                "[EnemyMeleeCombatController] PLAYER PUNCH LOCK END | " +
                "Enemy=" + gameObject.name +
                " | SequenceSucceeded=" + sequenceSucceeded,
                this
            );
        }
    }

    public bool ReceivePlayerPunchHit(
        int hitIndex,
        int damage)
    {
        if (!CanReceivePlayerPunch)
        {
            if (logDebug)
            {
                Debug.LogWarning(
                    "[EnemyMeleeCombatController] PLAYER PUNCH HIT REJECTED | " +
                    "Enemy=" + gameObject.name +
                    " | HitIndex=" + hitIndex +
                    " | AttackInProgress=" + attackInProgress,
                    this
                );
            }

            return false;
        }

        PlayerProperties player =
            PlayerProperties.Instance;

        if (player == null)
            return false;

        // Pair alignment đã được Player chốt một lần khi bắt đầu sequence.
        // Không căn lại ở từng hit, nếu không direction có thể bị lật khi cặp
        // nhân vật đã cùng tiến sang vị trí mới.
        damage =
            Mathf.Max(
                0,
                damage
            );

        if (hitIndex < 2)
        {
            enemyProperties.ApplyCombatDamageWithoutDeath(
                damage
            );

            StartEnemyGetHitPresentation();

            // Không đẩy riêng Enemy. Player controller sẽ dịch chuyển cả cặp
            // theo cùng một delta để giữ pair distance + hướng ổn định.

            if (logDebug)
            {
                Debug.Log(
                    "[EnemyMeleeCombatController] PLAYER PUNCH HIT " +
                    (hitIndex + 1) +
                    "/3 | Enemy=" + gameObject.name +
                    " | Damage=" + damage +
                    " | HP=" +
                    enemyProperties.CurrentHealth,
                    this
                );
            }

            return true;
        }

        enemyProperties.ApplyCombatDamageWithoutDeath(
            enemyProperties.CurrentHealth
        );

        StopEnemyGetHitPresentation();

        if (logDebug)
        {
            Debug.Log(
                "[EnemyMeleeCombatController] PLAYER PUNCH HIT 3/3 | Enemy=" +
                gameObject.name +
                " | HP=0 | Stunned starting.",
                this
            );
        }

        if (enemyCheckpointHandler == null ||
            !enemyCheckpointHandler.BeginMeleeStunnedDeath(
                attackPlaybackSpeed
            ))
        {
            Debug.LogError(
                "[EnemyMeleeCombatController] PLAYER PUNCH FINAL HIT FAILED: BeginMeleeStunnedDeath rejected.",
                this
            );

            return false;
        }

        // Player controller dịch chuyển cả cặp theo shared direction ở ngay hit event.
        return true;
    }

    private void LockVisionForPlayerPunch()
    {
        if (visionLockedForPlayerPunch)
            return;

        if (visionView == null)
            return;

        visionView.SetRestoreLock(true);
        visionLockedForPlayerPunch = true;

        if (logDebug)
        {
            Debug.Log(
                "[EnemyMeleeCombatController] VISION HIDDEN: Enemy is being punched | Enemy=" +
                gameObject.name,
                this
            );
        }
    }

    private void UnlockVisionAfterPlayerPunch()
    {
        if (!visionLockedForPlayerPunch)
            return;

        if (visionView != null)
            visionView.SetRestoreLock(false);

        visionLockedForPlayerPunch = false;
    }

    private void StartEnemyGetHitPresentation()
    {
        StopEnemyGetHitPresentation();

        if (animator == null ||
            !animator.enabled ||
            animator.runtimeAnimatorController == null)
        {
            Debug.LogError(
                "[EnemyMeleeCombatController] GET HIT FAILED: Enemy Animator unavailable.",
                this
            );

            return;
        }

        if (!PlayAnimatorOneShot(
                getHitAnimatorName,
                attackPlaybackSpeed
            ))
        {
            Debug.LogError(
                "[EnemyMeleeCombatController] GET HIT FAILED: Animator state rejected | State=" +
                getHitAnimatorName,
                this
            );

            return;
        }

        float duration =
            GetAnimatorStateDuration(
                getHitAnimatorName,
                attackPlaybackSpeed
            );

        enemyGetHitPresentationRoutine =
            StartCoroutine(
                MaintainEnemyGetHitPresentation(
                    Mathf.Max(0.01f, duration)
                )
            );
    }

    private IEnumerator MaintainEnemyGetHitPresentation(
        float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (enemyCheckpointHandler == null ||
                enemyCheckpointHandler.IsRuntimeDead ||
                enemyCheckpointHandler.IsPermanentlyDead)
            {
                yield break;
            }

            if (animator == null ||
                !animator.enabled)
            {
                yield break;
            }

            AnimatorStateInfo info =
                animator.GetCurrentAnimatorStateInfo(0);

            bool isStillGetHit =
                info.IsName(getHitAnimatorName) ||
                info.shortNameHash ==
                    Animator.StringToHash(getHitAnimatorName);

            if (!isStillGetHit)
            {
                if (logDebug)
                {
                    Debug.LogWarning(
                        "[EnemyMeleeCombatController] GET HIT WAS OVERRIDDEN; restoring GetHit state | Enemy=" +
                        gameObject.name,
                        this
                    );
                }

                PlayAnimatorOneShot(
                    getHitAnimatorName,
                    attackPlaybackSpeed
                );
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        enemyGetHitPresentationRoutine = null;
    }

    private void StopEnemyGetHitPresentation()
    {
        if (enemyGetHitPresentationRoutine != null)
        {
            StopCoroutine(
                enemyGetHitPresentationRoutine
            );

            enemyGetHitPresentationRoutine = null;
        }
    }

    private bool AlignEnemyAndPlayer(
        Transform player)
    {
        if (player == null)
            return false;

        Vector3 enemyToPlayer =
            player.position -
            transform.position;

        enemyToPlayer.y = 0f;

        if (enemyToPlayer.sqrMagnitude <= 0.0001f)
            enemyToPlayer = transform.forward;

        enemyToPlayer.y = 0f;

        if (enemyToPlayer.sqrMagnitude <= 0.0001f)
            return false;

        enemyToPlayer.Normalize();

        Vector3 playerPosition =
            player.position;

        Vector3 enemyPosition =
            playerPosition -
            enemyToPlayer *
            pairDistance;

        enemyPosition.y =
            transform.position.y;

        transform.position =
            enemyPosition;

        transform.rotation =
            Quaternion.LookRotation(
                enemyToPlayer,
                Vector3.up
            );

        player.rotation =
            Quaternion.LookRotation(
                -enemyToPlayer,
                Vector3.up
            );

        Rigidbody playerRb =
            player.GetComponent<Rigidbody>();

        if (playerRb != null)
        {
            playerRb.position =
                player.position;

            playerRb.rotation =
                player.rotation;
        }

        Physics.SyncTransforms();

        return true;
    }

    private bool IsPlayerCurrentlyInside()
    {
        if (playerCollidersInside.Count == 0)
            return false;

        List<Collider> staleColliders = null;

        foreach (Collider collider in playerCollidersInside)
        {
            if (collider == null ||
                !collider.enabled ||
                !collider.gameObject.activeInHierarchy ||
                !IsColliderActuallyOverlappingAttackTrigger(collider))
            {
                if (staleColliders == null)
                    staleColliders = new List<Collider>();

                if (collider != null)
                    staleColliders.Add(collider);
            }
        }

        if (staleColliders != null)
        {
            for (int i = 0; i < staleColliders.Count; i++)
                playerCollidersInside.Remove(staleColliders[i]);
        }

        return playerCollidersInside.Count > 0;
    }

    private bool IsColliderActuallyOverlappingAttackTrigger(
        Collider playerCollider)
    {
        if (playerCollider == null ||
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
                playerCollider,
                playerCollider.transform.position,
                playerCollider.transform.rotation,
                out _,
                out _
            );
        }
        catch
        {
            return attackRangeTrigger.bounds.Intersects(
                playerCollider.bounds
            );
        }
    }

    private bool IsPlayer(
        Collider other)
    {
        if (other == null)
            return false;

        if (other.CompareTag("Player"))
            return true;

        Transform root =
            other.transform.root;

        return root != null &&
               root.CompareTag("Player");
    }

    private bool PlayAnimatorOneShot(
        string stateName,
        float playbackSpeed)
    {
        if (animator == null ||
            !animator.enabled ||
            animator.runtimeAnimatorController == null ||
            string.IsNullOrWhiteSpace(stateName))
        {
            return false;
        }

        try
        {
            animator.speed =
                Mathf.Max(
                    0.01f,
                    playbackSpeed
                );

            animator.Play(
                stateName,
                0,
                0f
            );

            animator.Update(0f);
        }
        catch (System.Exception ex)
        {
            Debug.LogError(
                "[EnemyMeleeCombatController] Animator.Play failed | " +
                "State=" + stateName +
                " | Exception=" + ex.Message,
                this
            );

            animator.speed = 1f;
            return false;
        }

        AnimatorStateInfo info =
            animator.GetCurrentAnimatorStateInfo(0);

        return info.IsName(stateName) ||
               info.shortNameHash ==
                   Animator.StringToHash(stateName);
    }

    private float GetAnimatorStateDuration(
        string stateName,
        float playbackSpeed)
    {
        if (animator == null ||
            !animator.enabled ||
            animator.runtimeAnimatorController == null ||
            string.IsNullOrWhiteSpace(stateName))
        {
            return 0f;
        }

        try
        {
            animator.Play(
                stateName,
                0,
                0f
            );

            animator.Update(0f);
        }
        catch
        {
            return 0f;
        }

        AnimatorStateInfo info =
            animator.GetCurrentAnimatorStateInfo(0);

        bool accepted =
            info.IsName(stateName) ||
            info.shortNameHash ==
                Animator.StringToHash(stateName);

        if (!accepted)
            return 0f;

        return info.length /
               Mathf.Max(
                   0.01f,
                   playbackSpeed
               );
    }

    private void PlaySound(
        AudioClip clip,
        Vector3 worldPosition,
        float volume)
    {
        if (clip == null)
            return;

        if (attackAudioSource == null)
        {
            Debug.LogWarning(
                "[EnemyMeleeCombatController] ATTACK AUDIO SOURCE NULL | " +
                "Gán AudioSource cho chính Enemy.",
                this
            );
            return;
        }

        attackAudioSource.PlayOneShot(
            clip,
            volume
        );
    }

    private void StopAttackRoutine()
    {
        if (attackRoutine != null)
        {
            StopCoroutine(
                attackRoutine
            );

            attackRoutine = null;
        }

        if (animator != null)
            animator.speed = 1f;

        if (routePerformer != null &&
            routePerformer.IsMeleeCombatLocked)
        {
            routePerformer.EndMeleeCombatLock(
                false
            );
        }

        attackInProgress = false;

        if (enemyCheckpointHandler == null ||
            (!enemyCheckpointHandler.IsRuntimeDead &&
             !enemyCheckpointHandler.IsPermanentlyDead))
        {
            UnlockVisionAfterPlayerPunch();
        }
    }

    private void StopAttackRoutineReferenceOnly()
    {
        attackRoutine = null;
    }

    private void HandlePlayerDeathOrFailureStarted()
    {
        if (!attackInProgress)
            return;

        if (logDebug)
        {
            Debug.Log(
                "[EnemyMeleeCombatController] PLAYER DEATH/FAILURE -> cancel Enemy attack | Enemy=" +
                gameObject.name,
                this
            );
        }

        StopAttackRoutine();
        playerCollidersInside.Clear();
    }

    private void ResolveReferences()
    {
        if (enemyCheckpointHandler == null)
            enemyCheckpointHandler =
                GetComponent<EnemyCheckpointHandler>();

        if (routePerformer == null)
            routePerformer =
                GetComponent<EnemyRoutePerformer>();

        if (enemyProperties == null)
            enemyProperties =
                GetComponent<EnemyProperties>();

        if (visionView == null)
            visionView =
                GetComponent<GuardVisionView>();

        if (animator == null)
            animator =
                GetComponent<Animator>();
    }
}
