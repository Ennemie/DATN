using UnityEngine;
using UnityEngine.UI;

public class PlayerProperties : MonoBehaviour
{
    public static PlayerProperties Instance { get; private set; }

    private Slider hpBar;
    private int hp;
    public int _hp
    {
        get { return hp; }
        set
        {
            hp = Mathf.Clamp(value, 0, 100);
            UpdateHpBar();
            if (hp <= 0)
            {
                // Handle player death here
                Debug.Log("Player is dead!");
            }
        }
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    void Start()
    {
        hpBar = transform.Find("Canvas/HP/HP_Slider").GetComponent<Slider>();
        _hp = 100;
    }

    private void UpdateHpBar()
    {
        if (hpBar != null)
        {
            hpBar.value = hp;
        }
    }
    public void TakeDamage(int damage)
    {
        _hp -= damage;
    }
    public void Heal(int amount)
    {
        _hp += amount;
    }
}
