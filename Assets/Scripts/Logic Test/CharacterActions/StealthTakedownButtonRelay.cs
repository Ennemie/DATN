using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Diagnostic relay cho Stealth Takedown Button.
///
/// Chức năng:
/// - Bắt raw pointer click trên chính GameObject chứa Button.
/// - Chỉ log, không tự gọi gameplay action.
/// - Dùng để phân biệt:
///   1. Pointer không tới UI.
///   2. Pointer tới UI nhưng Button không phát onClick.
///
/// Script được StealthTakedownController tự thêm runtime nếu chưa tồn tại.
/// </summary>
[DisallowMultipleComponent]
public sealed class StealthTakedownButtonRelay : MonoBehaviour, IPointerClickHandler, IPointerDownHandler
{
    private StealthTakedownController controller;

    private void Awake()
    {
        // MỚI:
        // Nếu Relay nằm dưới Player, tự tìm Controller để diagnostic không còn
        // báo Controller=<NULL>. Relay vẫn chỉ log, không tự thực hiện gameplay.
        if (controller == null)
            controller = GetComponentInParent<StealthTakedownController>();
    }

    // Chức năng mới:
    // Cho Controller gán owner để relay có thể log đúng component liên quan.
    public void SetController(StealthTakedownController owner)
    {
        controller = owner;
    }

    // Chức năng mới:
    // Xác nhận raw pointer down đã chạm đúng GameObject của Stealth Takedown Button.
    public void OnPointerDown(PointerEventData eventData)
    {
        Debug.Log(
            "[StealthTakedownButtonRelay] POINTER DOWN RECEIVED | " +
            "Object=" + gameObject.name +
            " | ButtonComponent=" +
            (GetComponent<UnityEngine.UI.Button>() != null
                ? GetComponent<UnityEngine.UI.Button>().name
                : "<NULL>") +
            " | Controller=" +
            (controller != null ? controller.name : "<NULL>"),
            this
        );
    }

    // Chức năng mới:
    // Xác nhận raw pointer click đã tới UI object.
    // Không gọi Controller để tránh double execution với Button.onClick.
    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log(
            "[StealthTakedownButtonRelay] POINTER CLICK RECEIVED | " +
            "Object=" + gameObject.name +
            " | ButtonComponent=" +
            (GetComponent<UnityEngine.UI.Button>() != null
                ? GetComponent<UnityEngine.UI.Button>().name
                : "<NULL>") +
            " | ButtonEnabled=" +
            (GetComponent<UnityEngine.UI.Button>() != null
                ? GetComponent<UnityEngine.UI.Button>().enabled.ToString()
                : "<NULL>") +
            " | ButtonInteractable=" +
            (GetComponent<UnityEngine.UI.Button>() != null
                ? GetComponent<UnityEngine.UI.Button>().interactable.ToString()
                : "<NULL>"),
            this
        );
    }
}
