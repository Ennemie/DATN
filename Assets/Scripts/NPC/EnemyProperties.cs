using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Enemy health/properties.
///
/// Phần tích hợp checkpoint được giữ tại điểm giao HP/death:
/// - Damage bình thường vẫn dùng TakeDamage().
/// - HP bằng 0 đi vào death presentation thông qua GuardState.
/// - EnemyCheckpointHandler quản lý lifecycle temporary/permanent.
/// 
/// Các lời gọi combat TakeDamage(int) hiện tại vẫn hợp lệ.
/// </summary>
public class EnemyProperties : MonoBehaviour
{
    private GuardState guardState;
    private Slider hpSlider;
    private EnemyCheckpointHandler checkpointHandler;

    private int hp;

    [SerializeField] private int maxHealth = 100;

    public int CurrentHealth => hp;
    public int MaxHealth => maxHealth;

    public int _hp 
    { 
        get { return hp; }
        set
        {
            if(hp != value)
            {
                hp = Mathf.Clamp(value, 0, maxHealth);

                if (hpSlider != null)
                    hpSlider.value = hp;

                // CŨ:
                // if(hp <= 0)
                // {
                //     StunnedHandler();
                // }
                //
                // MỚI:
                // HP zero now enters the explicit death pipeline so the
                // EnemyCheckpointHandler can keep the object restorable.
                if(hp <= 0)
                {
                    StunnedHandler(false);
                }
            }
        }
    }

    private void Start()
    {
        guardState = GetComponent<GuardState>();
        checkpointHandler = GetComponent<EnemyCheckpointHandler>();

        Transform hpTransform = transform.Find("Canvas/HP/HP_Slider");

        if (hpTransform != null)
            hpSlider = hpTransform.GetComponent<Slider>();

        _hp = maxHealth;

        if (hpSlider != null)
        {
            hpSlider.maxValue = maxHealth;
            hpSlider.value = maxHealth;
        }
    }

    public void TakeDamage(int damage)
    {
        if (checkpointHandler != null &&
            checkpointHandler.IsRuntimeDead)
        {
            return;
        }

        _hp -= Mathf.Max(0, damage);
    }

    /// <summary>
    /// Applies melee damage without invoking the normal HP=0 death setter.
    /// The performative melee controller owns the final third-hit death.
    /// </summary>
    public int ApplyCombatDamageWithoutDeath(int damage)
    {
        if (checkpointHandler != null &&
            checkpointHandler.IsRuntimeDead)
        {
            return hp;
        }

        damage =
            Mathf.Max(
                0,
                damage
            );

        hp =
            Mathf.Clamp(
                hp - damage,
                0,
                maxHealth
            );

        if (hpSlider != null)
        {
            hpSlider.maxValue = maxHealth;
            hpSlider.value = hp;
        }

        return hp;
    }

    /// <summary>
    /// Chức năng mới: hạ Enemy bằng chốc thuốc ngay lập tức.
    ///
    /// Tham chiếu:
    /// - EnemyCheckpointHandler
    /// - GuardState
    /// - hệ thống Assassin/Player stealth trong tương lai
    ///
    /// Khác với TakeDamage(), nhánh này dùng StealthDying và vẫn đi qua lifecycle checkpoint.
    /// </summary>
    public void StealthTakedown()
    {
        if (checkpointHandler != null &&
            checkpointHandler.IsRuntimeDead)
        {
            return;
        }

        hp = 0;

        if (hpSlider != null)
            hpSlider.value = 0f;

        StunnedHandler(true);
    }

    /// <summary>
    /// Chức năng mới: restore HP mà không kích hoạt lại death logic khi HP = 0.
    /// Chỉ được EnemyCheckpointHandler sử dụng.
    /// </summary>
    public void SetHealthForCheckpointRestore(int health)
    {
        hp = Mathf.Clamp(
            health,
            0,
            maxHealth
        );

        if (hpSlider != null)
        {
            hpSlider.maxValue = maxHealth;
            hpSlider.value = hp;
            hpSlider.gameObject.SetActive(true);
        }
    }

    // CŨ:
    // private void StunnedHandler()
    // {
    //     guardState.isDead = true;
    //     hpSlider.gameObject.SetActive(false);
    //     DOVirtual.DelayedCall(2.133f + 1, () => {
    //         Destroy(gameObject);
    //     });
    // }
    //
    // MỚI:
    // The existing timing is preserved for the death animation window.
    // EnemyCheckpointHandler hiện chịu trách nhiệm quyết định object còn có thể restore hay không.
    // Chức năng mới:
    // Điểm giao giữa HP = 0 và EnemyCheckpointHandler.
    // Điều chỉnh: không gọi Dying cho death thường; Handler sẽ dừng AI rồi phát Stunned ngay.
    // CŨ:
    // private void StunnedHandler()
    // {
    //     guardState.isDead = true;
    //     hpSlider.gameObject.SetActive(false);
    //     DOVirtual.DelayedCall(2.133f + 1, () => {
    //         Destroy(gameObject);
    //     });
    // }
    //
    // MỚI:
    // private void StunnedHandler(bool stealthDeath)
    // {
    //     guardState.EnterDeathState(
    //         stealthDeath
    //             ? GuardState.State.StealthDying
    //             : GuardState.State.Dying
    //     );
    //     ...
    // }
    private void StunnedHandler(bool stealthDeath)
    {
        if (checkpointHandler != null)
        {
            // Điều chỉnh:
            // Giữ hành vi cũ là ẩn HP bar ngay khi Enemy chết, nhưng giao death animation
            // và lifecycle cho EnemyCheckpointHandler.
            if (hpSlider != null)
                hpSlider.gameObject.SetActive(false);

            checkpointHandler.BeginRuntimeDeath(stealthDeath);
            return;
        }

        // Fallback cho Enemy chưa migrate: giữ đường đi cũ, nhưng death thường dùng Stunned.
        if (guardState != null)
            guardState.EnterDeathState(
                stealthDeath
                    ? GuardState.State.StealthDying
                    : GuardState.State.Stunned
            );

        if (hpSlider != null)
            hpSlider.gameObject.SetActive(false);

        DOVirtual.DelayedCall(2.133f + 1f, () =>
        {
            Destroy(gameObject);
        });
    }
}
