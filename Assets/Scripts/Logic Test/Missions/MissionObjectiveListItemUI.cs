// Chức năng: UI item đại diện cho 1 nhiệm vụ đang active ở góc trái màn hình.
// - Gắn lên RootObject/template của từng dòng nhiệm vụ.
// - Template có thể để inactive trong hierarchy; MissionObjectiveListUI sẽ Instantiate clone và SetActive(true) cho clone.
// - Quản lý TMP text nhiệm vụ, icon incomplete, icon completed, màu vàng/xanh, animation move, và destroy clone sau khi complete.
// Gán cho: Prefab root hoặc RootObject template inactive của từng dòng objective.
// Tham chiếu với: MissionObjectiveListUI sẽ Instantiate template này, gọi Initialize(), MarkCompleted(), MoveTo(), MoveRoutine(), DestroySelf().
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MissionObjectiveListItemUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform rectTransform;
    [SerializeField] private TMP_Text objectiveText;
    [SerializeField] private Image incompleteImage;
    [SerializeField] private Image completedImage;

    [Header("Debug")]
    [SerializeField] private string objectiveId;
    [SerializeField] private bool isCompleted;

    private Coroutine moveRoutine;

    public string ObjectiveId => objectiveId;
    public bool IsCompleted => isCompleted;
    public RectTransform RectTransform
    {
        get
        {
            if (rectTransform == null)
                rectTransform = transform as RectTransform;

            return rectTransform;
        }
    }

    private void Reset()
    {
        rectTransform = transform as RectTransform;
        objectiveText = GetComponentInChildren<TMP_Text>(true);
    }

    public void Initialize(string id, string text, Color activeTextColor)
    {
        // Bảo hiểm thêm: nếu clone được tạo từ template inactive thì vẫn bắt buộc bật clone.
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        objectiveId = string.IsNullOrWhiteSpace(id) ? text : id;
        isCompleted = false;

        if (objectiveText != null)
        {
            objectiveText.text = text;
            objectiveText.color = activeTextColor;
            objectiveText.gameObject.SetActive(true);
        }

        if (incompleteImage != null)
            incompleteImage.gameObject.SetActive(true);

        if (completedImage != null)
            completedImage.gameObject.SetActive(false);
    }

    public void MarkCompleted(Color completedTextColor)
    {
        if (isCompleted)
            return;

        isCompleted = true;

        if (objectiveText != null)
            objectiveText.color = completedTextColor;

        if (incompleteImage != null)
            incompleteImage.gameObject.SetActive(false);

        if (completedImage != null)
            completedImage.gameObject.SetActive(true);
    }

    public void StopMoveAnimation()
    {
        if (moveRoutine != null)
        {
            StopCoroutine(moveRoutine);
            moveRoutine = null;
        }
    }

    public void MoveTo(Vector2 targetAnchoredPosition, float duration)
    {
        StopMoveAnimation();
        moveRoutine = StartCoroutine(MoveRoutine(targetAnchoredPosition, duration));
    }

    public IEnumerator MoveRoutine(Vector2 targetAnchoredPosition, float duration)
    {
        RectTransform rt = RectTransform;
        if (rt == null)
            yield break;

        Vector2 start = rt.anchoredPosition;
        float elapsed = 0f;

        if (duration <= 0f)
        {
            rt.anchoredPosition = targetAnchoredPosition;
            yield break;
        }

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = Smooth01(t);
            rt.anchoredPosition = Vector2.LerpUnclamped(start, targetAnchoredPosition, t);
            yield return null;
        }

        rt.anchoredPosition = targetAnchoredPosition;
        moveRoutine = null;
    }

    public void DestroySelf()
    {
        Destroy(gameObject);
    }

    private float Smooth01(float t)
    {
        return t * t * (3f - 2f * t);
    }
}
