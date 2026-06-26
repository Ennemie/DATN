// Chức năng:
// Objective hoàn thành khi Player đứng trong trigger, bấm đúng phím tương tác,
// sau đó chờ handling slider chạy đủ thời gian.
// Nếu trong lúc handling có bất cứ phím/chuột nào được bấm thêm, quá trình bị hủy.
//
// BẢN SỬA:
// - Không cần gán UI thủ công cho từng object nữa.
// - Tự tìm UI theo tên object trong scene, kể cả object đang inactive:
//   + InteractableObjects  : Root prompt + CanvasGroup
//   + InteractiveTitle     : TMP Title
//   + InteractiveContent   : TMP Content
//   + SliderProgress       : Slider
//   + TimeSliderProgress   : TMP đếm ngược thời gian handle
// - Slider tăng từ 0 -> 100.
// - TimeSliderProgress hiển thị 3.0s -> 2.9s -> ... -> 0.0s.
// - Vẫn cho phép gán thủ công nếu muốn override.
//
// Gán cho: tủ chìa khóa, cửa, công tắc, laptop, NPC, bảng điều khiển.
// Yêu cầu: script kế thừa MissionObjective. MissionObjective cần có IsCompleted và CompleteObjective().

using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Collider))]
public class MissionInteractObjective : MissionObjective
{
    [Header("Player Detect")]
    [SerializeField] private string playerTag = "Player";

    [Header("Interaction Content")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private string titleTextValue = "TƯƠNG TÁC";
    [SerializeField] private string actionText = "lấy chìa khóa";
    [SerializeField] private string contentFormat = "Click {0} để {1}";

    [Header("Handling")]
    [SerializeField] private float handlingTime = 3f;
    [SerializeField] private bool cancelHandlingOnAnyKeyOrMouseDown = true;
    [SerializeField] private bool cancelHandlingOnExitTrigger = true;
    [SerializeField] private bool hidePromptAfterComplete = true;
    [SerializeField] private bool disableGameObjectAfterComplete;

    [Header("Auto Find UI By Name")]
    [SerializeField] private bool autoFindUiOnAwake = true;
    [SerializeField] private bool autoFindUiAgainIfMissing = true;

    [SerializeField] private string promptRootObjectName = "InteractableObjects";
    [SerializeField] private string titleTextObjectName = "InteractiveTitle";
    [SerializeField] private string contentTextObjectName = "InteractiveContent";
    [SerializeField] private string sliderObjectName = "SliderProgress";
    [SerializeField] private string timeTextObjectName = "TimeSliderProgress";

    [Header("Player Canvas UI - Optional Manual Override")]
    [Tooltip("Root prompt. Nếu để trống, script tự tìm object tên InteractableObjects.")]
    [SerializeField] private GameObject promptRoot;

    [Tooltip("CanvasGroup trên Prompt Root. Nếu để trống, script tự lấy từ InteractableObjects hoặc tự AddComponent.")]
    [SerializeField] private CanvasGroup promptCanvasGroup;

    [Tooltip("TMP Title. Nếu để trống, script tự tìm object tên InteractiveTitle.")]
    [SerializeField] private TMP_Text titleText;

    [Tooltip("TMP Content. Nếu để trống, script tự tìm object tên InteractiveContent.")]
    [SerializeField] private TMP_Text contentText;

    [Tooltip("Slider. Nếu để trống, script tự tìm object tên SliderProgress.")]
    [SerializeField] private Slider handlingSlider;

    [Tooltip("TMP đếm ngược thời gian handle. Nếu để trống, script tự tìm object tên TimeSliderProgress.")]
    [SerializeField] private TMP_Text timeSliderProgressText;

    [Header("Fade")]
    [SerializeField] private float fadeInDuration = 0.15f;
    [SerializeField] private float fadeOutDuration = 0.15f;

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    private bool playerInside;
    private bool isHandling;
    private Collider triggerCollider;

    private Coroutine fadeRoutine;
    private Coroutine handlingRoutine;

    private void Awake()
    {
        triggerCollider = GetComponent<Collider>();
        triggerCollider.isTrigger = true;

        if (autoFindUiOnAwake)
            AutoFindUiReferences();

        SetupPromptCanvasGroup();

        HidePromptImmediate();
        HideSliderImmediate();
    }

    private void Update()
    {
        if (!playerInside || IsCompleted || isHandling)
            return;

        if (WasInteractKeyPressedThisFrame())
            StartHandling();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other))
            return;

        playerInside = true;

        if (logDebug)
            Debug.Log("[MissionInteractObjective] Player entered interaction zone: " + name);

        if (!IsCompleted)
            ShowPrompt();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayer(other))
            return;

        playerInside = false;

        if (logDebug)
            Debug.Log("[MissionInteractObjective] Player exited interaction zone: " + name);

        if (cancelHandlingOnExitTrigger && isHandling)
            CancelHandling("Player exited trigger.");

        HidePrompt();
    }

    private void StartHandling()
    {
        if (isHandling || IsCompleted)
            return;

        if (handlingTime <= 0f)
        {
            FinishHandling();
            return;
        }

        if (handlingRoutine != null)
            StopCoroutine(handlingRoutine);

        handlingRoutine = StartCoroutine(HandlingRoutine());
    }

    private IEnumerator HandlingRoutine()
    {
        isHandling = true;

        int startFrame = Time.frameCount;
        float elapsed = 0f;

        ShowSlider();

        SetSliderProgress01(0f);
        SetTimeRemainingText(handlingTime);

        if (logDebug)
            Debug.Log("[MissionInteractObjective] Start handling: " + actionText + " / time = " + handlingTime);

        while (elapsed < handlingTime)
        {
            // Không hủy ngay frame bấm E để bắt đầu handling.
            if (cancelHandlingOnAnyKeyOrMouseDown &&
                Time.frameCount > startFrame &&
                WasAnyKeyOrMousePressedThisFrame())
            {
                CancelHandling("Any key or mouse pressed during handling.");
                yield break;
            }

            elapsed += Time.deltaTime;

            float progress01 = Mathf.Clamp01(elapsed / handlingTime);
            float remaining = Mathf.Max(0f, handlingTime - elapsed);

            SetSliderProgress01(progress01);
            SetTimeRemainingText(remaining);

            yield return null;
        }

        SetSliderProgress01(1f);
        SetTimeRemainingText(0f);

        FinishHandling();
    }

    private void FinishHandling()
    {
        if (IsCompleted)
            return;

        isHandling = false;
        handlingRoutine = null;

        if (logDebug)
            Debug.Log("[MissionInteractObjective] Handling finished. Complete objective.");

        CompleteObjective();

        if (hidePromptAfterComplete)
            HidePrompt();

        HideSliderImmediate();

        if (disableGameObjectAfterComplete)
            gameObject.SetActive(false);
    }

    private void CancelHandling(string reason)
    {
        if (!isHandling)
            return;

        if (handlingRoutine != null)
        {
            StopCoroutine(handlingRoutine);
            handlingRoutine = null;
        }

        isHandling = false;

        HideSliderImmediate();

        if (logDebug)
            Debug.Log("[MissionInteractObjective] Handling canceled: " + reason);
    }

    private void ShowPrompt()
    {
        EnsureUiReferences();

        SetupPromptCanvasGroup();
        ApplyPromptText();

        if (promptRoot != null && !promptRoot.activeSelf)
            promptRoot.SetActive(true);

        // Khi mới vào vùng chỉ hiện text prompt. Slider/time vẫn ẩn cho tới khi bấm E.
        HideSliderImmediate();

        FadePromptTo(1f, fadeInDuration, false);
    }

    private void HidePrompt()
    {
        HideSliderImmediate();
        FadePromptTo(0f, fadeOutDuration, true);
    }

    private void HidePromptImmediate()
    {
        if (promptCanvasGroup != null)
        {
            promptCanvasGroup.alpha = 0f;
            promptCanvasGroup.interactable = false;
            promptCanvasGroup.blocksRaycasts = false;
        }

        if (promptRoot != null)
            promptRoot.SetActive(false);
    }

    private void FadePromptTo(float targetAlpha, float duration, bool disableAfterFade)
    {
        EnsureUiReferences();
        SetupPromptCanvasGroup();

        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);

        fadeRoutine = StartCoroutine(FadePromptRoutine(targetAlpha, duration, disableAfterFade));
    }

    private IEnumerator FadePromptRoutine(float targetAlpha, float duration, bool disableAfterFade)
    {
        if (promptCanvasGroup == null)
        {
            if (disableAfterFade && promptRoot != null)
                promptRoot.SetActive(false);

            yield break;
        }

        if (promptRoot != null && !promptRoot.activeSelf)
            promptRoot.SetActive(true);

        float startAlpha = promptCanvasGroup.alpha;
        float elapsed = 0f;

        promptCanvasGroup.interactable = false;
        promptCanvasGroup.blocksRaycasts = false;

        if (duration <= 0f)
        {
            promptCanvasGroup.alpha = targetAlpha;
        }
        else
        {
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                promptCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, Smooth01(t));
                yield return null;
            }

            promptCanvasGroup.alpha = targetAlpha;
        }

        if (disableAfterFade && promptRoot != null)
            promptRoot.SetActive(false);

        fadeRoutine = null;
    }

    private void ApplyPromptText()
    {
        string keyLabel = GetKeyLabel();

        if (titleText != null)
            titleText.text = titleTextValue;

        if (contentText != null)
            contentText.text = string.Format(contentFormat, keyLabel, actionText);
    }

    private void ShowSlider()
    {
        EnsureUiReferences();

        if (promptRoot != null && !promptRoot.activeSelf)
            promptRoot.SetActive(true);

        if (handlingSlider != null)
        {
            handlingSlider.gameObject.SetActive(true);
            handlingSlider.minValue = 0f;
            handlingSlider.maxValue = 100f;
            handlingSlider.value = 0f;
        }

        if (timeSliderProgressText != null)
        {
            timeSliderProgressText.gameObject.SetActive(true);
            SetTimeRemainingText(handlingTime);
        }
    }

    private void HideSliderImmediate()
    {
        if (handlingSlider != null)
        {
            handlingSlider.value = 0f;
            handlingSlider.gameObject.SetActive(false);
        }

        if (timeSliderProgressText != null)
        {
            timeSliderProgressText.text = "";
            timeSliderProgressText.gameObject.SetActive(false);
        }
    }

    private void SetSliderProgress01(float progress01)
    {
        if (handlingSlider == null)
            return;

        handlingSlider.minValue = 0f;
        handlingSlider.maxValue = 100f;
        handlingSlider.value = Mathf.Clamp01(progress01) * 100f;
    }

    private void SetTimeRemainingText(float remainingSeconds)
    {
        if (timeSliderProgressText == null)
            return;

        timeSliderProgressText.text = Mathf.Max(0f, remainingSeconds).ToString("0.0") + "s";
    }

    private void EnsureUiReferences()
    {
        if (!autoFindUiAgainIfMissing)
            return;

        if (promptRoot == null ||
            promptCanvasGroup == null ||
            titleText == null ||
            contentText == null ||
            handlingSlider == null ||
            timeSliderProgressText == null)
        {
            AutoFindUiReferences();
        }
    }

    private void AutoFindUiReferences()
    {
        if (promptRoot == null)
            promptRoot = FindSceneGameObjectByName(promptRootObjectName);

        if (titleText == null)
            titleText = FindSceneComponentByObjectName<TMP_Text>(titleTextObjectName);

        if (contentText == null)
            contentText = FindSceneComponentByObjectName<TMP_Text>(contentTextObjectName);

        if (handlingSlider == null)
            handlingSlider = FindSceneComponentByObjectName<Slider>(sliderObjectName);

        if (timeSliderProgressText == null)
            timeSliderProgressText = FindSceneComponentByObjectName<TMP_Text>(timeTextObjectName);

        SetupPromptCanvasGroup();

        if (logDebug)
        {
            if (promptRoot == null)
                Debug.LogWarning("[MissionInteractObjective] Cannot find prompt root object named: " + promptRootObjectName);

            if (titleText == null)
                Debug.LogWarning("[MissionInteractObjective] Cannot find TMP title object named: " + titleTextObjectName);

            if (contentText == null)
                Debug.LogWarning("[MissionInteractObjective] Cannot find TMP content object named: " + contentTextObjectName);

            if (handlingSlider == null)
                Debug.LogWarning("[MissionInteractObjective] Cannot find Slider object named: " + sliderObjectName);

            if (timeSliderProgressText == null)
                Debug.LogWarning("[MissionInteractObjective] Cannot find TMP time object named: " + timeTextObjectName);
        }
    }

    private void SetupPromptCanvasGroup()
    {
        if (promptCanvasGroup != null)
            return;

        if (promptRoot != null)
        {
            promptCanvasGroup = promptRoot.GetComponent<CanvasGroup>();
            if (promptCanvasGroup == null)
                promptCanvasGroup = promptRoot.AddComponent<CanvasGroup>();
        }
    }

    private static GameObject FindSceneGameObjectByName(string targetName)
    {
        if (string.IsNullOrWhiteSpace(targetName))
            return null;

        // GameObject.Find không tìm được object inactive.
        // Resources.FindObjectsOfTypeAll tìm được cả inactive trong scene.
        Transform[] allTransforms = Resources.FindObjectsOfTypeAll<Transform>();

        for (int i = 0; i < allTransforms.Length; i++)
        {
            Transform t = allTransforms[i];
            if (t == null)
                continue;

            if (t.hideFlags != HideFlags.None)
                continue;

            if (!t.gameObject.scene.IsValid())
                continue;

            if (t.name == targetName)
                return t.gameObject;
        }

        return null;
    }

    private static T FindSceneComponentByObjectName<T>(string targetName) where T : Component
    {
        GameObject foundObject = FindSceneGameObjectByName(targetName);
        if (foundObject == null)
            return null;

        return foundObject.GetComponent<T>();
    }

    private bool IsPlayer(Collider other)
    {
        if (other.CompareTag(playerTag))
            return true;

        if (other.transform.root != null && other.transform.root.CompareTag(playerTag))
            return true;

        return false;
    }

    private string GetKeyLabel()
    {
        if (interactKey >= KeyCode.A && interactKey <= KeyCode.Z)
            return interactKey.ToString();

        if (interactKey >= KeyCode.Alpha0 && interactKey <= KeyCode.Alpha9)
            return interactKey.ToString().Replace("Alpha", "");

        return interactKey.ToString();
    }

    private bool WasInteractKeyPressedThisFrame()
    {
        bool pressed = false;

#if ENABLE_LEGACY_INPUT_MANAGER
        pressed |= Input.GetKeyDown(interactKey);
#endif

#if ENABLE_INPUT_SYSTEM
        pressed |= WasKeyPressedThisFrameNewInput(interactKey);
#endif

        return pressed;
    }

    private bool WasAnyKeyOrMousePressedThisFrame()
    {
        bool pressed = false;

#if ENABLE_LEGACY_INPUT_MANAGER
        pressed |= Input.anyKeyDown;
#endif

#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;

        if (keyboard != null)
            pressed |= keyboard.anyKey.wasPressedThisFrame;

        if (mouse != null)
        {
            pressed |= mouse.leftButton.wasPressedThisFrame;
            pressed |= mouse.rightButton.wasPressedThisFrame;
            pressed |= mouse.middleButton.wasPressedThisFrame;
            pressed |= mouse.backButton.wasPressedThisFrame;
            pressed |= mouse.forwardButton.wasPressedThisFrame;
        }
#endif

        return pressed;
    }

#if ENABLE_INPUT_SYSTEM
    private bool WasKeyPressedThisFrameNewInput(KeyCode key)
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return false;

        switch (key)
        {
            case KeyCode.A: return keyboard.aKey.wasPressedThisFrame;
            case KeyCode.B: return keyboard.bKey.wasPressedThisFrame;
            case KeyCode.C: return keyboard.cKey.wasPressedThisFrame;
            case KeyCode.D: return keyboard.dKey.wasPressedThisFrame;
            case KeyCode.E: return keyboard.eKey.wasPressedThisFrame;
            case KeyCode.F: return keyboard.fKey.wasPressedThisFrame;
            case KeyCode.G: return keyboard.gKey.wasPressedThisFrame;
            case KeyCode.H: return keyboard.hKey.wasPressedThisFrame;
            case KeyCode.I: return keyboard.iKey.wasPressedThisFrame;
            case KeyCode.J: return keyboard.jKey.wasPressedThisFrame;
            case KeyCode.K: return keyboard.kKey.wasPressedThisFrame;
            case KeyCode.L: return keyboard.lKey.wasPressedThisFrame;
            case KeyCode.M: return keyboard.mKey.wasPressedThisFrame;
            case KeyCode.N: return keyboard.nKey.wasPressedThisFrame;
            case KeyCode.O: return keyboard.oKey.wasPressedThisFrame;
            case KeyCode.P: return keyboard.pKey.wasPressedThisFrame;
            case KeyCode.Q: return keyboard.qKey.wasPressedThisFrame;
            case KeyCode.R: return keyboard.rKey.wasPressedThisFrame;
            case KeyCode.S: return keyboard.sKey.wasPressedThisFrame;
            case KeyCode.T: return keyboard.tKey.wasPressedThisFrame;
            case KeyCode.U: return keyboard.uKey.wasPressedThisFrame;
            case KeyCode.V: return keyboard.vKey.wasPressedThisFrame;
            case KeyCode.W: return keyboard.wKey.wasPressedThisFrame;
            case KeyCode.X: return keyboard.xKey.wasPressedThisFrame;
            case KeyCode.Y: return keyboard.yKey.wasPressedThisFrame;
            case KeyCode.Z: return keyboard.zKey.wasPressedThisFrame;

            case KeyCode.Alpha0: return keyboard.digit0Key.wasPressedThisFrame;
            case KeyCode.Alpha1: return keyboard.digit1Key.wasPressedThisFrame;
            case KeyCode.Alpha2: return keyboard.digit2Key.wasPressedThisFrame;
            case KeyCode.Alpha3: return keyboard.digit3Key.wasPressedThisFrame;
            case KeyCode.Alpha4: return keyboard.digit4Key.wasPressedThisFrame;
            case KeyCode.Alpha5: return keyboard.digit5Key.wasPressedThisFrame;
            case KeyCode.Alpha6: return keyboard.digit6Key.wasPressedThisFrame;
            case KeyCode.Alpha7: return keyboard.digit7Key.wasPressedThisFrame;
            case KeyCode.Alpha8: return keyboard.digit8Key.wasPressedThisFrame;
            case KeyCode.Alpha9: return keyboard.digit9Key.wasPressedThisFrame;

            case KeyCode.Space: return keyboard.spaceKey.wasPressedThisFrame;
            case KeyCode.Return: return keyboard.enterKey.wasPressedThisFrame;
            case KeyCode.Escape: return keyboard.escapeKey.wasPressedThisFrame;
            case KeyCode.Tab: return keyboard.tabKey.wasPressedThisFrame;
            case KeyCode.Backspace: return keyboard.backspaceKey.wasPressedThisFrame;
            case KeyCode.LeftShift: return keyboard.leftShiftKey.wasPressedThisFrame;
            case KeyCode.RightShift: return keyboard.rightShiftKey.wasPressedThisFrame;
            case KeyCode.LeftControl: return keyboard.leftCtrlKey.wasPressedThisFrame;
            case KeyCode.RightControl: return keyboard.rightCtrlKey.wasPressedThisFrame;
            case KeyCode.LeftAlt: return keyboard.leftAltKey.wasPressedThisFrame;
            case KeyCode.RightAlt: return keyboard.rightAltKey.wasPressedThisFrame;

            default:
                Debug.LogWarning("[MissionInteractObjective] New Input System key is not mapped yet: " + key);
                return false;
        }
    }
#endif

    private float Smooth01(float t)
    {
        return t * t * (3f - 2f * t);
    }
}
