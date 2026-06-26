// Chức năng: Bắt click bất kỳ trên vùng UI hội thoại.
// Gắn lên một Image full-screen raycast target nằm dưới panel thoại nhưng trên gameplay.
using UnityEngine;
using UnityEngine.EventSystems;

public class DialogueClickCatcher : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private DialogueUI dialogueUI;

    private void Awake()
    {
        if (dialogueUI == null)
            dialogueUI = GetComponentInParent<DialogueUI>(true);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (dialogueUI != null)
            dialogueUI.NotifyScreenClicked();
    }
}
