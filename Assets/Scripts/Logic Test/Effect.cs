using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class Effect : MonoBehaviour
{
    [Header("Kích hoạt")]
    public bool playOnStart = true;
    public float delayBeforeActive = 0f;
    public float delayBeforeEffect = 0f;

    [Header("Fade Options")]
    public bool useFadeIn = false;
    public bool useFadeOut = false;
    public float fadeDuration = 1f;

    [Header("Slide Options")]
    public bool slideInFromLeft = false;
    public bool slideInFromRight = false;
    public bool slideInFromTop = false;
    public bool slideInFromBottom = false;

    public bool slideOutToLeft = false;
    public bool slideOutToRight = false;
    public bool slideOutToTop = false;
    public bool slideOutToBottom = false;

    public float slideDuration = 1f;

    [Header("Zoom In Options")]
    public bool useZoomIn = false;
    public float zoomDuration = 0.5f;
    public Vector3 zoomStartScale = new Vector3(3, 3, 1);

    [Header("Pop Bounce Options")]
    public bool usePopBounce = false;
    public float popBounceDuration = 1f;

    [Header("Misc")]
    public bool resetOnDisable = true; // nếu true: khi object bị disable sẽ reset về trạng thái gốc

    // Internal
    private CanvasGroup canvasGroup;
    private RectTransform rect;
    private Vector2 initialAnchoredPos;
    private Vector3 initialScale;
    private float initialAlpha;
    private Vector2 slideStartPos;
    private List<Coroutine> activeCoroutines = new List<Coroutine>();

    void Awake()
    {
        rect = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

        // Lưu trạng thái "gốc" chỉ 1 lần ở Awake
        initialAnchoredPos = rect.anchoredPosition;
        initialScale = transform.localScale;
        initialAlpha = canvasGroup.alpha;
    }

    void OnEnable()
    {
        // Reset về trạng thái gốc mỗi lần bật (để chạy hiệu ứng đúng vị trí ban đầu)
        StopAllCoroutines();
        activeCoroutines.Clear();
        rect.anchoredPosition = initialAnchoredPos;
        transform.localScale = initialScale;
        canvasGroup.alpha = initialAlpha;

        if (playOnStart)
            StartCoroutine(PlayEffect());
    }

    void OnDisable()
    {
        // Khi bị disable, đảm bảo reset lại (để lần sau bật lên đúng vị trí)
        if (resetOnDisable)
        {
            // Không StartCoroutine ở đây vì object đang bị tắt, chỉ cần set giá trị nội bộ
            rect.anchoredPosition = initialAnchoredPos;
            transform.localScale = initialScale;
            canvasGroup.alpha = initialAlpha;
            StopAllCoroutines();
            activeCoroutines.Clear();
        }
    }

    public void Play()
    {
        StopAllCoroutines();
        activeCoroutines.Clear();
        // Reset luôn trước khi chơi để tránh trạng thái còn dư từ lần trước
        rect.anchoredPosition = initialAnchoredPos;
        transform.localScale = initialScale;
        canvasGroup.alpha = initialAlpha;

        StartCoroutine(PlayEffect());
    }

    IEnumerator PlayEffect()
    {
        if (delayBeforeActive > 0f)
        {
            // Nếu object đang inactive, bật lại sau delay
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
                yield return new WaitForSeconds(delayBeforeActive);
            }
            else
            {
                yield return new WaitForSeconds(delayBeforeActive);
            }
        }

        if (delayBeforeEffect > 0f)
            yield return new WaitForSeconds(delayBeforeEffect);

        // Chạy tất cả effect song song (không yield)
        if (useFadeIn)
            activeCoroutines.Add(StartCoroutine(Fade(0f, 1f, fadeDuration)));

        if (useFadeOut)
            activeCoroutines.Add(StartCoroutine(Fade(1f, 0f, fadeDuration)));

        if (slideInFromLeft)
            activeCoroutines.Add(StartCoroutine(SlideFromDirection(Vector2.left, slideDuration)));

        if (slideInFromRight)
            activeCoroutines.Add(StartCoroutine(SlideFromDirection(Vector2.right, slideDuration)));

        if (slideInFromTop)
            activeCoroutines.Add(StartCoroutine(SlideFromDirection(Vector2.up, slideDuration)));

        if (slideInFromBottom)
            activeCoroutines.Add(StartCoroutine(SlideFromDirection(Vector2.down, slideDuration)));

        if (slideOutToLeft)
            activeCoroutines.Add(StartCoroutine(SlideToDirection(Vector2.left, slideDuration)));

        if (slideOutToRight)
            activeCoroutines.Add(StartCoroutine(SlideToDirection(Vector2.right, slideDuration)));

        if (slideOutToTop)
            activeCoroutines.Add(StartCoroutine(SlideToDirection(Vector2.up, slideDuration)));

        if (slideOutToBottom)
            activeCoroutines.Add(StartCoroutine(SlideToDirection(Vector2.down, slideDuration)));

        if (useZoomIn)
            activeCoroutines.Add(StartCoroutine(ZoomInEffect()));

        if (usePopBounce)
            activeCoroutines.Add(StartCoroutine(PopBounceEffect()));

        // Chờ tất cả coroutine hoàn thành
        while (activeCoroutines.Count > 0)
        {
            activeCoroutines.RemoveAll(c => c == null);
            yield return null;
        }

        // Nếu có SlideOut, tắt object
        if (slideOutToLeft || slideOutToRight || slideOutToTop || slideOutToBottom)
        {
            gameObject.SetActive(false);
        }
    }

    IEnumerator Fade(float from, float to, float duration)
    {
        float timer = 0f;
        canvasGroup.alpha = from;
        while (timer < duration)
        {
            canvasGroup.alpha = Mathf.Lerp(from, to, timer / duration);
            timer += Time.deltaTime;
            yield return null;
        }
        canvasGroup.alpha = to;
    }

    IEnumerator SlideFromDirection(Vector2 dir, float duration)
    {
        // Tạo offset lớn dựa trên màn hình + kích thước rect (dùng đúng trục)
        Vector2 offset = new Vector2(
            dir.x * (Screen.width + rect.rect.width),
            dir.y * (Screen.height + rect.rect.height)
        );

        slideStartPos = initialAnchoredPos + offset;
        rect.anchoredPosition = slideStartPos;

        float timer = 0f;
        while (timer < duration)
        {
            rect.anchoredPosition = Vector2.Lerp(slideStartPos, initialAnchoredPos, timer / duration);
            timer += Time.deltaTime;
            yield return null;
        }
        rect.anchoredPosition = initialAnchoredPos;
    }

    IEnumerator SlideToDirection(Vector2 dir, float duration)
    {
        Vector2 offset = new Vector2(
            dir.x * (Screen.width + rect.rect.width),
            dir.y * (Screen.height + rect.rect.height)
        );

        Vector2 targetPos = initialAnchoredPos + offset;
        float timer = 0f;
        Vector2 startPos = rect.anchoredPosition;

        while (timer < duration)
        {
            rect.anchoredPosition = Vector2.Lerp(startPos, targetPos, timer / duration);
            timer += Time.deltaTime;
            yield return null;
        }
        rect.anchoredPosition = targetPos;
    }

    public void PlaySlideOutRight()
    {
        StopAllCoroutines();
        activeCoroutines.Clear();
        StartCoroutine(SlideOutRightRoutine());
    }

    IEnumerator SlideOutRightRoutine()
    {
        yield return StartCoroutine(SlideToDirection(Vector2.right, slideDuration));
        gameObject.SetActive(false);
    }

    IEnumerator ZoomInEffect()
    {
        Vector3 startScale = zoomStartScale;
        Vector3 endScale = initialScale;

        float timer = 0f;
        while (timer < zoomDuration)
        {
            transform.localScale = Vector3.Lerp(startScale, endScale, timer / zoomDuration);
            timer += Time.deltaTime;
            yield return null;
        }
        transform.localScale = endScale;
    }

    IEnumerator PopBounceEffect()
    {
        float phaseDuration = popBounceDuration / 5f;

        Vector3 scale0 = initialScale * 0f;
        Vector3 scale150 = initialScale * 1.5f;
        Vector3 scale75 = initialScale * 0.75f;
        Vector3 scale125 = initialScale * 1.25f;
        Vector3 scale90 = initialScale * 0.9f;
        Vector3 scale100 = initialScale;

        transform.localScale = scale0;

        yield return StartCoroutine(ScaleRoutine(scale0, scale150, phaseDuration));
        yield return StartCoroutine(ScaleRoutine(scale150, scale75, phaseDuration));
        yield return StartCoroutine(ScaleRoutine(scale75, scale125, phaseDuration));
        yield return StartCoroutine(ScaleRoutine(scale125, scale90, phaseDuration));
        yield return StartCoroutine(ScaleRoutine(scale90, scale100, phaseDuration));

        transform.localScale = initialScale;
    }

    IEnumerator ScaleRoutine(Vector3 from, Vector3 to, float duration)
    {
        if (duration <= 0f)
        {
            transform.localScale = to;
            yield break;
        }

        float timer = 0f;
        while (timer < duration)
        {
            transform.localScale = Vector3.Lerp(from, to, timer / duration);
            timer += Time.deltaTime;
            yield return null;
        }

        transform.localScale = to;
    }
}