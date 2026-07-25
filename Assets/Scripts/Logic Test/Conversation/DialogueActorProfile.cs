// Chức năng: Hồ sơ nhân vật nói chuyện.
// - Chỉ lưu dữ liệu dùng lại nhiều lần: Actor Id, tên hiển thị, avatar.
// - Không lưu Transform scene ở đây để tránh asset ScriptableObject tham chiếu scene bị mất.
// - Camera target mặc định của actor được gán bằng DialogueActorSceneBinding trong scene.
using UnityEngine;

[CreateAssetMenu(fileName = "DialogueActorProfile", menuName = "Cipher 007/Conversation/Dialogue Actor Profile")]
public class DialogueActorProfile : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string actorId = "ACTOR_ID";
    [SerializeField] private string displayName = "Actor Name";

    [Header("Portrait")]
    [SerializeField] private Sprite avatar;

    [Header("Typing")]
    [Tooltip("Nếu <= 0, DialogueController sẽ dùng tốc độ mặc định của Conversation.")]
    [SerializeField] private float defaultSecondsPerCharacter = -1f;

    public string ActorId => actorId;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? actorId : displayName;
    public Sprite Avatar => avatar;
    public float DefaultSecondsPerCharacter => defaultSecondsPerCharacter;
}
