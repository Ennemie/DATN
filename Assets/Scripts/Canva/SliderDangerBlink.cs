using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Slider))]
public class SliderDangerBlink : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Slider slider;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image fillImage;
    [SerializeField] private Image dangerOverlayPanel;

    [Header("Normal Colors")]
    [SerializeField] private Color normalBackgroundColor = new Color(0.45f, 0.45f, 0.45f, 1f);
    [SerializeField] private Color normalFillColor = new Color(0.92f, 0.92f, 0.92f, 1f);

    [Header("Danger Colors")]
    [SerializeField] private Color brightRed = new Color(1f, 0.12f, 0.12f, 1f);
    [SerializeField] private Color darkRed = new Color(0.35f, 0f, 0f, 1f);

    [Header("Danger Settings")]
    [Range(0f, 1f)]
    [SerializeField] private float dangerThreshold = 0.3f;

    [Tooltip("Thời gian chuyển giữa 2 trạng thái đỏ. Tăng lên nếu muốn blink chậm hơn.")]
    [SerializeField] private float blinkTransitionDuration = 0.8f;

    [Tooltip("Thời gian đứng yên giữa mỗi lần đổi màu.")]
    [SerializeField] private float blinkHoldDuration = 0.3f;

    [Tooltip("Thời gian chuyển từ trạng thái danger về bình thường.")]
    [SerializeField] private float returnTransitionDuration = 0.3f;

    [Tooltip("Dùng thời gian không bị ảnh hưởng bởi Time.timeScale.")]
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Overlay Alpha Bands")]
    [Tooltip("Khi slider từ 0.3 -> 0.2")]
    [Range(0f, 1f)]
    [SerializeField] private float lowBandMinAlpha = 0.4f;
    [Range(0f, 1f)]
    [SerializeField] private float lowBandMaxAlpha = 0.6f;

    [Tooltip("Khi slider từ 0.2 -> 0.1")]
    [Range(0f, 1f)]
    [SerializeField] private float midBandMinAlpha = 0.6f;
    [Range(0f, 1f)]
    [SerializeField] private float midBandMaxAlpha = 0.8f;

    [Tooltip("Khi slider dưới 0.1")]
    [Range(0f, 1f)]
    [SerializeField] private float criticalBandMinAlpha = 0.8f;
    [Range(0f, 1f)]
    [SerializeField] private float criticalBandMaxAlpha = 1f;

    private Coroutine blinkRoutine;
    private Coroutine returnRoutine;
    private bool isDangerState;

    private void Reset()
    {
        slider = GetComponent<Slider>();
    }

    private void Awake()
    {
        if (slider == null)
        {
            slider = GetComponent<Slider>();
        }
    }

    private void Start()
    {
        if (dangerOverlayPanel != null)
        {
            dangerOverlayPanel.raycastTarget = false;
        }

        ApplyNormalColorsInstant();
    }

    private void Update()
    {
        if (slider == null)
            return;

        bool shouldBeDanger = slider.value <= dangerThreshold;

        if (shouldBeDanger && !isDangerState)
        {
            EnterDangerState();
        }
        else if (!shouldBeDanger && isDangerState)
        {
            ExitDangerState();
        }
    }

    private void EnterDangerState()
    {
        isDangerState = true;

        if (returnRoutine != null)
        {
            StopCoroutine(returnRoutine);
            returnRoutine = null;
        }

        if (blinkRoutine != null)
        {
            StopCoroutine(blinkRoutine);
        }

        blinkRoutine = StartCoroutine(DangerBlinkLoop());
    }

    private void ExitDangerState()
    {
        isDangerState = false;

        if (blinkRoutine != null)
        {
            StopCoroutine(blinkRoutine);
            blinkRoutine = null;
        }

        if (returnRoutine != null)
        {
            StopCoroutine(returnRoutine);
        }

        returnRoutine = StartCoroutine(ReturnToNormalRoutine());
    }

    private IEnumerator DangerBlinkLoop()
    {
        bool stateA = true;

        while (isDangerState)
        {
            GetOverlayAlphaRange(out float minAlpha, out float maxAlpha);

            if (stateA)
            {
                // State A:
                // Background -> bright red
                // Fill       -> dark red
                // Overlay     -> from min alpha to max alpha
                yield return TransitionColors(
                    brightRed,
                    darkRed,
                    minAlpha,
                    maxAlpha,
                    blinkTransitionDuration
                );
            }
            else
            {
                // State B:
                // Background -> dark red
                // Fill       -> bright red
                // Overlay     -> from max alpha to min alpha
                yield return TransitionColors(
                    darkRed,
                    brightRed,
                    maxAlpha,
                    minAlpha,
                    blinkTransitionDuration
                );
            }

            if (!isDangerState)
                yield break;

            yield return Wait(blinkHoldDuration);
            stateA = !stateA;
        }

        blinkRoutine = null;
    }

    private void GetOverlayAlphaRange(out float minAlpha, out float maxAlpha)
    {
        float value = slider != null ? slider.value : 1f;

        // 0.3 -> 0.2 : 40% - 60%
        if (value <= 0.1f)
        {
            minAlpha = criticalBandMinAlpha;
            maxAlpha = criticalBandMaxAlpha;
        }
        else if (value <= 0.2f)
        {
            minAlpha = midBandMinAlpha;
            maxAlpha = midBandMaxAlpha;
        }
        else
        {
            minAlpha = lowBandMinAlpha;
            maxAlpha = lowBandMaxAlpha;
        }

        if (maxAlpha < minAlpha)
        {
            float temp = minAlpha;
            minAlpha = maxAlpha;
            maxAlpha = temp;
        }
    }

    private IEnumerator ReturnToNormalRoutine()
    {
        Color bgStart = backgroundImage != null ? backgroundImage.color : normalBackgroundColor;
        Color fillStart = fillImage != null ? fillImage.color : normalFillColor;
        float overlayStartAlpha = dangerOverlayPanel != null ? dangerOverlayPanel.color.a : 0f;

        float elapsed = 0f;
        float duration = Mathf.Max(0.0001f, returnTransitionDuration);

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            if (backgroundImage != null)
            {
                backgroundImage.color = Color.Lerp(bgStart, normalBackgroundColor, smoothT);
            }

            if (fillImage != null)
            {
                fillImage.color = Color.Lerp(fillStart, normalFillColor, smoothT);
            }

            if (dangerOverlayPanel != null)
            {
                SetOverlayAlpha(Mathf.Lerp(overlayStartAlpha, 0f, smoothT));
            }

            elapsed += DeltaTime();
            yield return null;
        }

        ApplyNormalColorsInstant();
        returnRoutine = null;
    }

    private IEnumerator TransitionColors(Color targetBackground, Color targetFill, float startOverlayAlpha, float targetOverlayAlpha, float duration)
    {
        Color bgStart = backgroundImage != null ? backgroundImage.color : normalBackgroundColor;
        Color fillStart = fillImage != null ? fillImage.color : normalFillColor;

        float elapsed = 0f;
        duration = Mathf.Max(0.0001f, duration);

        while (elapsed < duration && isDangerState)
        {
            float t = elapsed / duration;
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            if (backgroundImage != null)
            {
                backgroundImage.color = Color.Lerp(bgStart, targetBackground, smoothT);
            }

            if (fillImage != null)
            {
                fillImage.color = Color.Lerp(fillStart, targetFill, smoothT);
            }

            if (dangerOverlayPanel != null)
            {
                SetOverlayAlpha(Mathf.Lerp(startOverlayAlpha, targetOverlayAlpha, smoothT));
            }

            elapsed += DeltaTime();
            yield return null;
        }

        if (isDangerState)
        {
            if (backgroundImage != null)
            {
                backgroundImage.color = targetBackground;
            }

            if (fillImage != null)
            {
                fillImage.color = targetFill;
            }

            if (dangerOverlayPanel != null)
            {
                SetOverlayAlpha(targetOverlayAlpha);
            }
        }
    }

    private void ApplyNormalColorsInstant()
    {
        if (backgroundImage != null)
        {
            backgroundImage.color = normalBackgroundColor;
        }

        if (fillImage != null)
        {
            fillImage.color = normalFillColor;
        }

        if (dangerOverlayPanel != null)
        {
            SetOverlayAlpha(0f);
            dangerOverlayPanel.raycastTarget = false;
        }
    }

    private void SetOverlayAlpha(float alpha)
    {
        if (dangerOverlayPanel == null)
            return;

        Color c = dangerOverlayPanel.color;
        c.a = alpha;
        dangerOverlayPanel.color = c;
    }

    private IEnumerator Wait(float seconds)
    {
        if (seconds <= 0f)
            yield break;

        if (useUnscaledTime)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }
        else
        {
            yield return new WaitForSeconds(seconds);
        }
    }

    private float DeltaTime()
    {
        return useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
    }
}
