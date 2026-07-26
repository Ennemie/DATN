using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class EnemyProperties : MonoBehaviour
{
    private GuardState guardState;
    private Slider hpSlider;
    private int hp;
    public int _hp 
    { 
        get { return hp; } 
        set
        {
            if(hp != value)
            {
                hp = value;
                hpSlider.value = hp;
                if(hp <= 0)
                {
                    StunnedHandler();
                }
            }
        }
    }
    void Start()
    {
        guardState = GetComponent<GuardState>();
        hpSlider = transform.Find("Canvas/HP/HP_Slider").GetComponent<Slider>();
        _hp = 100;
        hpSlider.maxValue = _hp;
        hpSlider.value = _hp;
    }
    public void TakeDamage(int damage)
    {
        _hp -= damage;
    }
    private void StunnedHandler()
    {
        guardState.isDead = true;
        hpSlider.gameObject.SetActive(false);
        DOVirtual.DelayedCall(2.133f + 1, () => {
            Destroy(gameObject);
        });
    }
}
