// Chức năng: Dữ liệu một cuộc hội thoại đặt trong scene.
// Lưu ý: Dùng MonoBehaviour thay vì ScriptableObject để mỗi line có thể tham chiếu Transform scene như CameraTarget_Room, CameraTarget_Key.
using UnityEngine;

public class DialogueConversationData : MonoBehaviour
{
    [Header("Conversation")]
    [SerializeField] private string conversationId = "CONVERSATION_ID";
    [SerializeField] private bool allowSkip = true;
    [SerializeField] private string skipButtonText = "Bỏ qua hướng dẫn";

    [Header("Typing")]
    [SerializeField] private float defaultSecondsPerCharacter = 0.035f;

    [Header("Camera")]
    [Tooltip("Nếu true, DialogueController sẽ tự trả camera về Player khi hội thoại kết thúc. Với camera shot có dialogue, thường để false và để MissionCameraDirector return sau.")]
    [SerializeField] private bool returnCameraToPlayerOnEnd;

    [Tooltip("Thời gian return camera về Player nếu Return Camera To Player On End = true.")]
    [SerializeField] private float returnCameraMoveTime = 0.75f;

    [Header("Lines")]
    [SerializeField] private DialogueLine[] lines;

    public string ConversationId => conversationId;
    public bool AllowSkip => allowSkip;
    public string SkipButtonText => skipButtonText;
    public float DefaultSecondsPerCharacter => defaultSecondsPerCharacter;
    public bool ReturnCameraToPlayerOnEnd => returnCameraToPlayerOnEnd;
    public float ReturnCameraMoveTime => returnCameraMoveTime;
    public DialogueLine[] Lines => lines;
}
