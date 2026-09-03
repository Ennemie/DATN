// Chức năng: Một dòng thoại trong một cuộc hội thoại.
// Speaker chỉ quyết định Name/Avatar. Camera target là phần riêng để line có thể nhìn NPC, Player, phòng, chìa khóa, cửa, v.v.
using System;
using UnityEngine;

public enum DialogueCameraMode
{
    None,
    SpeakerDefault,
    CustomTarget,
    KeepCurrent,
    ReturnToPlayer
}

[Serializable]
public class DialogueLine
{
    [Header("Text")]
    public DialogueActorProfile speaker;
    [TextArea(2, 5)] public string content;

    [Header("Camera")]
    public DialogueCameraMode cameraMode = DialogueCameraMode.SpeakerDefault;
    public Transform customCameraTarget;
    public float cameraMoveTime = 0.75f;
    public bool waitForCameraBeforeTyping = true;

    [Header("Typing Override")]
    [Tooltip("Nếu <= 0, dùng tốc độ của Actor Profile; nếu Actor cũng <= 0, dùng tốc độ của Conversation.")]
    public float secondsPerCharacterOverride = -1f;

    [Header("Timing")]
    [Tooltip("Chờ thêm trước khi cho phép click qua line kế tiếp sau khi chữ đã hiện đầy đủ.")]
    public float waitAfterTypingBeforeContinue = 0f;

    // =========================================================================
    // Chức năng mới - Conversation Element Object Activation
    // =========================================================================
    // Hai list này được thêm ở CUỐI class để không làm thay đổi/ghi đè
    // các field conversation/camera/typing/timing đã có trong từng element cũ.
    [Header("Objects On Enter")]
    [Tooltip(
        "Khi line này bắt đầu, các Object trong list sẽ được SetActive(true)."
    )]
    public GameObject[] setActiveTrueOnEnter;

    [Tooltip(
        "Khi line này bắt đầu, các Object trong list sẽ được SetActive(false)."
    )]
    public GameObject[] setActiveFalseOnEnter;
}
