// Chức năng: Chỉ lo phần hiển thị UI hội thoại: avatar, name, content, hint nhấp tiếp tục, nút skip.
// Không xử lý mission, không tự complete objective.
using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DialogueUI : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject root;
    [SerializeField] private CanvasGroup rootCanvasGroup;
    [SerializeField] private bool hideOnAwake = true;

    [Header("Dialogue References")]
    [SerializeField] private Image avatarImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text contentText;

    [Header("Continue Hint")]
    [SerializeField] private GameObject continueHintRoot;
    [SerializeField] private TMP_Text continueHintText;
    [SerializeField] private CanvasGroup continueHintCanvasGroup;
    [SerializeField] private string continueHintMessage = "Nhấp bất kỳ đâu để tiếp tục";
    [SerializeField] private float hintBlinkSpeed = 2.5f;

    [Header("Skip")]
    [SerializeField] private Button skipButton;
    [SerializeField] private TMP_Text skipButtonText;

    public event Action ScreenClicked;
    public event Action SkipClicked;

    private Coroutine blinkRoutine;

    private void Awake()
    {
        if (root == null)
            root = gameObject;

        if (rootCanvasGroup == null && root != null)
            rootCanvasGroup = root.GetComponent<CanvasGroup>();

        if (continueHintCanvasGroup == null && continueHintRoot != null)
            continueHintCanvasGroup = continueHintRoot.GetComponent<CanvasGroup>();

        if (skipButton != null)
            skipButton.onClick.AddListener(NotifySkipClicked);

        if (hideOnAwake)
            HideImmediate();
    }

    public void Show()
    {
        if (root != null)
            root.SetActive(true);

        if (rootCanvasGroup != null)
        {
            rootCanvasGroup.alpha = 1f;
            rootCanvasGroup.interactable = true;
            rootCanvasGroup.blocksRaycasts = true;
        }
    }

    public void HideImmediate()
    {
        SetContinueHintVisible(false);

        if (rootCanvasGroup != null)
        {
            rootCanvasGroup.alpha = 0f;
            rootCanvasGroup.interactable = false;
            rootCanvasGroup.blocksRaycasts = false;
        }

        if (root != null)
            root.SetActive(false);
    }

    public void SetActor(DialogueActorProfile actor)
    {
        if (nameText != null)
            nameText.text = actor != null ? actor.DisplayName : string.Empty;

        if (avatarImage != null)
        {
            Sprite avatar = actor != null ? actor.Avatar : null;
            avatarImage.sprite = avatar;
            avatarImage.enabled = avatar != null;
        }
    }

    public void SetContent(string content)
    {
        if (contentText != null)
            contentText.text = content ?? string.Empty;
    }

    public void SetContinueHintVisible(bool visible)
    {
        if (continueHintText != null)
            continueHintText.text = continueHintMessage;

        if (blinkRoutine != null)
        {
            StopCoroutine(blinkRoutine);
            blinkRoutine = null;
        }

        if (continueHintRoot != null)
            continueHintRoot.SetActive(visible);

        if (continueHintCanvasGroup != null)
        {
            continueHintCanvasGroup.alpha = visible ? 1f : 0f;
            if (visible)
                blinkRoutine = StartCoroutine(BlinkHintRoutine());
        }
    }

    public void ConfigureSkip(bool allowSkip, string buttonText)
    {
        if (skipButton != null)
            skipButton.gameObject.SetActive(allowSkip);

        if (skipButtonText != null)
            skipButtonText.text = string.IsNullOrWhiteSpace(buttonText) ? "Bỏ qua hướng dẫn" : buttonText;
    }

    public void NotifyScreenClicked()
    {
        ScreenClicked?.Invoke();
    }

    public void NotifySkipClicked()
    {
        SkipClicked?.Invoke();
    }

    private IEnumerator BlinkHintRoutine()
    {
        while (true)
        {
            if (continueHintCanvasGroup != null)
            {
                float t = (Mathf.Sin(Time.unscaledTime * hintBlinkSpeed) + 1f) * 0.5f;
                continueHintCanvasGroup.alpha = Mathf.Lerp(0.25f, 1f, t);
            }

            yield return null;
        }
    }
}
