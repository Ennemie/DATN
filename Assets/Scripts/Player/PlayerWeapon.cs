using System;
using UnityEngine;

public class PlayerWeapon : MonoBehaviour
{
    public static PlayerWeapon Instance { get; private set; }

    [SerializeField] private GameObject knifeObj;
    [SerializeField] private GameObject pistolObj;
    [SerializeField] private GameObject shotgun_IdleObj;
    [SerializeField] private GameObject shotgun_RunningObj;
    [SerializeField] private GameObject shotgun_ShootingObj;

    public enum WeaponType
    {
        Fist,
        Knife,
        Pistol,
        Shotgun
    }

    [SerializeField] private WeaponType _currentWeapon = WeaponType.Fist;

    public WeaponType CurrentWeapon
    {
        get => _currentWeapon;
        set
        {
            if (_currentWeapon == value) return;
            _currentWeapon = value;

            // Mỗi khi đổi vũ khí, yêu cầu PlayerState cập nhật lại Animation ngay
            if (PlayerState.Instance != null)
            {
                PlayerState.Instance.UpdateAnimation();
            }
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        DisableAllWeaponsOnSpawn();
    }

    // Hàm hỗ trợ để đổi vũ khí từ các script khác (ví dụ: nhấn phím 1, 2, 3)
    public void ChangeWeapon(WeaponType newWeapon)
    {
        CurrentWeapon = newWeapon;
        Debug.Log("Đã chuyển sang vũ khí: " + newWeapon);
        switch (newWeapon)
        {
            case WeaponType.Fist:
                knifeObj.SetActive(false);
                pistolObj.SetActive(false);
                shotgun_IdleObj.SetActive(false);
                shotgun_RunningObj.SetActive(false);
                shotgun_ShootingObj.SetActive(false);
                break;
            case WeaponType.Knife:
                knifeObj.SetActive(true);
                pistolObj.SetActive(false);
                shotgun_IdleObj.SetActive(false);
                shotgun_RunningObj.SetActive(false);
                shotgun_ShootingObj.SetActive(false);
                break;
            case WeaponType.Pistol:
                knifeObj.SetActive(false);
                pistolObj.SetActive(true);
                shotgun_IdleObj.SetActive(false);
                shotgun_RunningObj.SetActive(false);
                shotgun_ShootingObj.SetActive(false);
                break;
            case WeaponType.Shotgun:
                knifeObj.SetActive(false);
                pistolObj.SetActive(false);
                shotgun_IdleObj.SetActive(true);
                shotgun_RunningObj.SetActive(false);
                shotgun_ShootingObj.SetActive(false);
                break;
        }
    }
    public void UpdateShotgunAnimation(string value)
    {
        if (CurrentWeapon != WeaponType.Shotgun) return;
        if (value == "Idle")
        {
            shotgun_IdleObj.SetActive(true);
            shotgun_RunningObj.SetActive(false);
            shotgun_ShootingObj.SetActive(false);
        }
        else if (value == "Running")
        {
             shotgun_IdleObj.SetActive(false);
             shotgun_RunningObj.SetActive(true);
             shotgun_ShootingObj.SetActive(false);
        }
        else if (value == "Shooting")
        {
             shotgun_IdleObj.SetActive(false);
             shotgun_RunningObj.SetActive(false);
             shotgun_ShootingObj.SetActive(true);
        }
    }
    private void DisableAllWeaponsOnSpawn()
    {
        knifeObj.SetActive(false);
        pistolObj.SetActive(false);
        shotgun_IdleObj.SetActive(false);
        shotgun_RunningObj.SetActive(false);
        shotgun_ShootingObj.SetActive(false);
    }
}