// Chức năng: Gắn profile nhân vật với camera target trong scene.
// - Gắn script này lên NPC/Player hoặc object đại diện cho actor trong scene.
// - ActorProfile giữ Name/Avatar; Binding này giữ Transform camera target vì target là object thuộc scene.
using UnityEngine;

public class DialogueActorSceneBinding : MonoBehaviour
{
    [Header("Actor")]
    [SerializeField] private DialogueActorProfile actorProfile;

    [Header("Camera")]
    [Tooltip("Điểm camera nên nhìn khi line dùng Camera Mode = Speaker Default. Nếu trống, dùng transform của object này.")]
    [SerializeField] private Transform defaultCameraTarget;

    public DialogueActorProfile ActorProfile => actorProfile;
    public Transform DefaultCameraTarget => defaultCameraTarget != null ? defaultCameraTarget : transform;

    private void OnEnable()
    {
        DialogueActorRegistry.Register(this);
    }

    private void OnDisable()
    {
        DialogueActorRegistry.Unregister(this);
    }

    private void Reset()
    {
        defaultCameraTarget = transform;
    }
}
