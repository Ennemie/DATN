using NUnit.Framework;
using System;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;
using DG.Tweening;

public class PlayerCanvasController : MonoBehaviour
{
    public static PlayerCanvasController Instance { get; private set; }

    // Weapons Hub
    [SerializeField] private GameObject weaponsHub;
    [SerializeField] private RawImage fistIcon;
    [SerializeField] private RawImage knifeIcon;
    [SerializeField] private RawImage pistolIcon;
    [SerializeField] private RawImage shotgunIcon;
    private string colorHex = "#00AEEF";
    private Color iconColor;
    private Vector2 weaponsHubOGPos;

    // Inventory
    [HideInInspector] public bool isInventoryOpen = false;
    [SerializeField] private RawImage inventoryImage;
    [SerializeField] private List<GameObject> inventoryItems;

    // Crouch Icon
    [SerializeField] private Image crouchIcon;
    [SerializeField] private Image downIcon;

    void Awake()
    {
        iconColor = ColorUtility.TryParseHtmlString(colorHex, out Color color) ? color : Color.white;
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
        weaponsHubOGPos = weaponsHub.transform.localPosition;
    }
    private void Start()
    {
        UpdateWeaponIcons();
        HideInventoryBox();
    }

    // Weapons Hub
    public void ShowWeaponsHubOnMouse(Vector2 mousePos)
    {
        mousePos.y = mousePos.y + 80;
        weaponsHub.transform.position = mousePos;
    }
    public void RepositionWeaponsHub()
    {
        weaponsHub.transform.localPosition = weaponsHubOGPos;
    }
    private void ResetWeaponIconsColor()
    {
        fistIcon.color = Color.white;
        knifeIcon.color = Color.white;
        pistolIcon.color = Color.white;
        shotgunIcon.color = Color.white;
    }
    public void ChangeToFist()
    {
        PlayerWeapon.Instance.ChangeWeapon(PlayerWeapon.WeaponType.Fist);
    }
    public void ChangeToKnife()
    {
        PlayerWeapon.Instance.ChangeWeapon(PlayerWeapon.WeaponType.Knife);
    }
    public void ChangeToPistol()
    {
        PlayerWeapon.Instance.ChangeWeapon(PlayerWeapon.WeaponType.Pistol);
    }
    public void ChangeToShotgun()
    {
        PlayerWeapon.Instance.ChangeWeapon(PlayerWeapon.WeaponType.Shotgun);
    }
    public void UpdateWeaponIcons()
    {
        ResetWeaponIconsColor();
        switch (PlayerWeapon.Instance.CurrentWeapon)
        {
            case PlayerWeapon.WeaponType.Fist:
                fistIcon.color = iconColor;
                break;
            case PlayerWeapon.WeaponType.Knife:
                knifeIcon.color = iconColor;
                break;
            case PlayerWeapon.WeaponType.Pistol:
                pistolIcon.color = iconColor;
                break;
            case PlayerWeapon.WeaponType.Shotgun:
                shotgunIcon.color = iconColor;
                break;
        }
    }
    // Weapons Hub end

    // Inventory
    public void ToggleInventory()
    {
        if(!isInventoryOpen)
        {
            isInventoryOpen = true;
            inventoryImage.color = iconColor;
        }
        else
        {
            isInventoryOpen = false;
            inventoryImage.color = Color.white;
        }
        StartCoroutine(WaitToDislayInventoryItem(isInventoryOpen));
    }
    private IEnumerator WaitToDislayInventoryItem(bool isOpen)
    {
        foreach (var item in inventoryItems)
        {
            yield return new WaitForSeconds(0.1f);
            item.SetActive(isOpen);
        }
    }
    private void HideInventoryBox()
    {
        foreach (var item in inventoryItems)
        {
            item.SetActive(false);
        }
    }
    public void Enable(bool enabled)
    {
        gameObject.SetActive(enabled);
    }
    public void UpdateCrouchIcon(bool isCrouching)
    {
        crouchIcon.color = isCrouching ? iconColor : Color.white;
        downIcon.color = isCrouching ? iconColor : Color.white;
    }
}
