using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TutorialSequenceController : MonoBehaviour
{
    [Serializable]
    public class TutorialPage
    {
        [Header("Root")]
        public GameObject root;

        [Header("Backdrop / Panel")]
        public RectTransform panelRect;

        [Header("Step Number Object")]
        public GameObject numberObject;
        public RectTransform numberRect;

        [NonSerialized] public CanvasGroup rootGroup;
        [NonSerialized] public CanvasGroup numberGroup;

        [NonSerialized] public RectSnapshot panelSnapshot;
        [NonSerialized] public RectSnapshot numberSnapshot;
    }

    [Serializable]
    public struct RectSnapshot
    {
        public Vector2 anchorMin;
        public Vector2 anchorMax;
        public Vector2 anchoredPosition;
        public Vector3 anchoredPosition3D;
        public Vector2 sizeDelta;
        public Vector2 offsetMin;
        public Vector2 offsetMax;
        public Vector2 pivot;
        public Vector3 localScale;
        public Quaternion localRotation;

        public static RectSnapshot Capture(RectTransform rt)
        {
            RectSnapshot s = new RectSnapshot();

            if (rt == null)
                return s;

            s.anchorMin = rt.anchorMin;
            s.anchorMax = rt.anchorMax;
            s.anchoredPosition = rt.anchoredPosition;
            s.anchoredPosition3D = rt.anchoredPosition3D;
            s.sizeDelta = rt.sizeDelta;
            s.offsetMin = rt.offsetMin;
            s.offsetMax = rt.offsetMax;
            s.pivot = rt.pivot;
            s.localScale = rt.localScale;
            s.localRotation = rt.localRotation;
            return s;
        }

        public static RectSnapshot Lerp(RectSnapshot a, RectSnapshot b, float t)
        {
            RectSnapshot s = new RectSnapshot();
            s.anchorMin = Vector2.LerpUnclamped(a.anchorMin, b.anchorMin, t);
            s.anchorMax = Vector2.LerpUnclamped(a.anchorMax, b.anchorMax, t);
            s.anchoredPosition = Vector2.LerpUnclamped(a.anchoredPosition, b.anchoredPosition, t);
            s.anchoredPosition3D = Vector3.LerpUnclamped(a.anchoredPosition3D, b.anchoredPosition3D, t);
            s.sizeDelta = Vector2.LerpUnclamped(a.sizeDelta, b.sizeDelta, t);
            s.offsetMin = Vector2.LerpUnclamped(a.offsetMin, b.offsetMin, t);
            s.offsetMax = Vector2.LerpUnclamped(a.offsetMax, b.offsetMax, t);
            s.pivot = Vector2.LerpUnclamped(a.pivot, b.pivot, t);
            s.localScale = Vector3.LerpUnclamped(a.localScale, b.localScale, t);
            s.localRotation = Quaternion.LerpUnclamped(a.localRotation, b.localRotation, t);
            return s;
        }

        public static void Apply(RectTransform rt, RectSnapshot s)
        {
            if (rt == null)
                return;

            rt.anchorMin = s.anchorMin;
            rt.anchorMax = s.anchorMax;
            rt.anchoredPosition = s.anchoredPosition;
            rt.anchoredPosition3D = s.anchoredPosition3D;
            rt.sizeDelta = s.sizeDelta;
            rt.offsetMin = s.offsetMin;
            rt.offsetMax = s.offsetMax;
            rt.pivot = s.pivot;
            rt.localScale = s.localScale;
            rt.localRotation = s.localRotation;
        }
    }

    [Header("Tutorial Pages (4 pages)")]
    [SerializeField] private TutorialPage[] pages = new TutorialPage[4];

    [Header("Advance Button")]
    [SerializeField] private Button advanceButton;
    [SerializeField] private TMP_Text advanceButtonLabel;
    [SerializeField] private string nextLabel = "Kế tiếp";
    [SerializeField] private string finishLabel = "H.tất";

    [Header("Canvas Root")]
    [SerializeField] private GameObject canvasRoot;

    [Header("Animation")]
    [SerializeField, Range(0.05f, 2f)] private float transitionDuration = 0.5f;
    [SerializeField] private Vector2 numberSlideInFromLeft = new Vector2(-420f, 0f);
    [SerializeField] private Vector2 numberSlideOutToRight = new Vector2(420f, 0f);

    [Header("Tutorial State")]
    [SerializeField] private bool autoStartTutorial = true;
    [SerializeField] private bool skipIfAlreadyCompleted = true;
    [SerializeField] private string completedPrefsKey = "TutorialCompleted";

    [Header("Main Panel")]
    [SerializeField] private int mainPanelIndex = 0;

    private int currentIndex = 0;
    private bool isTransitioning = false;
    private Coroutine transitionRoutine;

    private TutorialPage MainPanelPage
    {
        get
        {
            if (pages == null || pages.Length == 0)
                return null;

            int safeIndex = Mathf.Clamp(mainPanelIndex, 0, pages.Length - 1);
            return pages[safeIndex];
        }
    }

    private void Awake()
    {
        CacheAll();
        BindButton();
        InitPages();
    }

    private void Start()
    {
        if (skipIfAlreadyCompleted && PlayerPrefs.GetInt(completedPrefsKey, 0) == 1)
        {
            if (canvasRoot != null)
                canvasRoot.SetActive(false);
            return;
        }

        if (autoStartTutorial)
        {
            ShowPageImmediate(0);
        }
        else
        {
            SetAllPagesInactive();
        }

        RefreshAdvanceLabel();
    }

    private void OnDestroy()
    {
        if (advanceButton != null)
            advanceButton.onClick.RemoveListener(HandleAdvanceClicked);
    }

    private void CacheAll()
    {
        if (pages == null)
            pages = new TutorialPage[0];

        for (int i = 0; i < pages.Length; i++)
        {
            TutorialPage p = pages[i];
            if (p == null)
                continue;

            if (p.root != null)
                p.rootGroup = GetOrAddCanvasGroup(p.root);

            if (p.numberObject != null)
                p.numberGroup = GetOrAddCanvasGroup(p.numberObject);

            if (p.panelRect != null)
                p.panelSnapshot = RectSnapshot.Capture(p.panelRect);

            if (p.numberRect != null)
                p.numberSnapshot = RectSnapshot.Capture(p.numberRect);
        }
    }

    private void BindButton()
    {
        if (advanceButton != null)
        {
            advanceButton.onClick.RemoveListener(HandleAdvanceClicked);
            advanceButton.onClick.AddListener(HandleAdvanceClicked);
        }
    }

    private void InitPages()
    {
        if (pages == null || pages.Length == 0)
            return;

        for (int i = 0; i < pages.Length; i++)
        {
            TutorialPage p = pages[i];
            if (p == null)
                continue;

            if (p.root != null)
            {
                p.root.SetActive(false);
                if (p.rootGroup != null)
                {
                    p.rootGroup.alpha = 0f;
                    p.rootGroup.interactable = false;
                    p.rootGroup.blocksRaycasts = false;
                }
            }

            if (p.numberObject != null)
            {
                p.numberObject.SetActive(false);
                if (p.numberGroup != null)
                {
                    p.numberGroup.alpha = 0f;
                    p.numberGroup.interactable = false;
                    p.numberGroup.blocksRaycasts = false;
                }
            }
        }

        if (advanceButton != null)
            advanceButton.interactable = true;
    }

    private void SetAllPagesInactive()
    {
        if (pages == null)
            return;

        for (int i = 0; i < pages.Length; i++)
        {
            TutorialPage p = pages[i];
            if (p == null)
                continue;

            if (p.root != null)
                p.root.SetActive(false);

            if (p.numberObject != null)
                p.numberObject.SetActive(false);
        }
    }

    private void EnsureCurrentPageVisible()
    {
        if (pages == null || pages.Length == 0)
            return;

        if (currentIndex < 0 || currentIndex >= pages.Length)
            currentIndex = 0;

        TutorialPage currentPage = pages[currentIndex];
        if (currentPage == null)
            return;

        if (currentPage.root != null)
            currentPage.root.SetActive(true);

        if (currentPage.numberObject != null)
            currentPage.numberObject.SetActive(true);

        if (currentPage.rootGroup != null)
        {
            currentPage.rootGroup.alpha = 1f;
            currentPage.rootGroup.interactable = true;
            currentPage.rootGroup.blocksRaycasts = true;
        }

        if (currentPage.numberGroup != null)
        {
            currentPage.numberGroup.alpha = 1f;
            currentPage.numberGroup.interactable = true;
            currentPage.numberGroup.blocksRaycasts = true;
        }
    }

    private void ShowPageImmediate(int index)
    {
        if (pages == null || pages.Length == 0)
            return;

        index = Mathf.Clamp(index, 0, pages.Length - 1);
        currentIndex = index;

        SetAllPagesInactive();
        EnsureCurrentPageVisible();

        TutorialPage currentPage = pages[currentIndex];
        if (currentPage == null)
            return;

        TutorialPage mainPanelPage = MainPanelPage;
        if (mainPanelPage != null && mainPanelPage.panelRect != null)
            RectSnapshot.Apply(mainPanelPage.panelRect, currentPage.panelSnapshot);

        if (currentPage.numberRect != null)
            RectSnapshot.Apply(currentPage.numberRect, currentPage.numberSnapshot);

        RefreshAdvanceLabel();
    }

    private void HandleAdvanceClicked()
    {
        if (isTransitioning)
            return;

        if (pages == null || pages.Length == 0)
            return;

        if (currentIndex >= pages.Length - 1)
        {
            FinishTutorial();
            return;
        }

        if (transitionRoutine != null)
            StopCoroutine(transitionRoutine);

        transitionRoutine = StartCoroutine(TransitionToPage(currentIndex + 1));
    }

    private IEnumerator TransitionToPage(int nextIndex)
    {
        isTransitioning = true;

        if (advanceButton != null)
            advanceButton.interactable = false;

        if (pages == null || pages.Length == 0)
        {
            isTransitioning = false;
            if (advanceButton != null)
                advanceButton.interactable = true;
            yield break;
        }

        if (currentIndex < 0 || currentIndex >= pages.Length)
            currentIndex = 0;

        if (nextIndex < 0 || nextIndex >= pages.Length)
        {
            isTransitioning = false;
            if (advanceButton != null)
                advanceButton.interactable = true;
            yield break;
        }

        TutorialPage currentPage = pages[currentIndex];
        TutorialPage nextPage = pages[nextIndex];
        TutorialPage mainPanelPage = MainPanelPage;

        if (currentPage == null || nextPage == null || mainPanelPage == null)
        {
            isTransitioning = false;
            if (advanceButton != null)
                advanceButton.interactable = true;
            yield break;
        }

        // Panel 1 là object chính, panel 2-3-4 chỉ lấy snapshot để morph.
        if (mainPanelPage.root != null)
            mainPanelPage.root.SetActive(true);

        if (mainPanelPage.rootGroup != null)
        {
            mainPanelPage.rootGroup.alpha = 1f;
            mainPanelPage.rootGroup.interactable = true;
            mainPanelPage.rootGroup.blocksRaycasts = true;
        }

        if (currentPage.numberObject != null)
            currentPage.numberObject.SetActive(true);

        if (nextPage.numberObject != null)
            nextPage.numberObject.SetActive(true);

        RectSnapshot startPanel = mainPanelPage.panelRect != null
            ? RectSnapshot.Capture(mainPanelPage.panelRect)
            : currentPage.panelSnapshot;

        RectSnapshot endPanel = nextPage.panelSnapshot;

        RectSnapshot startCurrentNumber = currentPage.numberRect != null
            ? RectSnapshot.Capture(currentPage.numberRect)
            : currentPage.numberSnapshot;

        RectSnapshot endNextNumber = nextPage.numberSnapshot;

        RectSnapshot hiddenStartCurrentNumber = startCurrentNumber;
        hiddenStartCurrentNumber.anchoredPosition = startCurrentNumber.anchoredPosition + numberSlideOutToRight;

        RectSnapshot hiddenEndNextNumber = endNextNumber;
        hiddenEndNextNumber.anchoredPosition = endNextNumber.anchoredPosition + numberSlideInFromLeft;

        float elapsed = 0f;

        while (elapsed < transitionDuration)
        {
            float t = elapsed / transitionDuration;
            float eased = EaseInOutCubic(t);

            // Chỉ panel chính morph theo snapshot của panel tiếp theo.
            if (mainPanelPage.panelRect != null)
            {
                RectSnapshot panelNow = RectSnapshot.Lerp(startPanel, endPanel, eased);
                RectSnapshot.Apply(mainPanelPage.panelRect, panelNow);
            }

            // Number giữ logic active/inactive như cũ: number hiện tại trượt ra, number tiếp theo trượt vào.
            if (currentPage.numberRect != null)
            {
                RectSnapshot currentNumberNow = RectSnapshot.Lerp(hiddenStartCurrentNumber, currentPage.numberSnapshot, 1f - eased);
                currentPage.numberRect.anchoredPosition = Vector2.LerpUnclamped(
                    startCurrentNumber.anchoredPosition,
                    startCurrentNumber.anchoredPosition + numberSlideOutToRight,
                    eased
                );
                currentPage.numberRect.anchoredPosition3D = Vector3.LerpUnclamped(
                    startCurrentNumber.anchoredPosition3D,
                    startCurrentNumber.anchoredPosition3D + new Vector3(numberSlideOutToRight.x, numberSlideOutToRight.y, 0f),
                    eased
                );
                if (currentPage.numberGroup != null)
                    currentPage.numberGroup.alpha = 1f - eased;
            }

            if (nextPage.numberRect != null)
            {
                nextPage.numberRect.anchoredPosition = Vector2.LerpUnclamped(
                    endNextNumber.anchoredPosition + numberSlideInFromLeft,
                    endNextNumber.anchoredPosition,
                    eased
                );
                nextPage.numberRect.anchoredPosition3D = Vector3.LerpUnclamped(
                    endNextNumber.anchoredPosition3D + new Vector3(numberSlideInFromLeft.x, numberSlideInFromLeft.y, 0f),
                    endNextNumber.anchoredPosition3D,
                    eased
                );
                if (nextPage.numberGroup != null)
                    nextPage.numberGroup.alpha = eased;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (mainPanelPage.panelRect != null)
            RectSnapshot.Apply(mainPanelPage.panelRect, endPanel);

        if (currentPage.numberObject != null)
            currentPage.numberObject.SetActive(false);

        if (nextPage.numberObject != null)
            nextPage.numberObject.SetActive(true);

        if (nextPage.numberRect != null)
            RectSnapshot.Apply(nextPage.numberRect, nextPage.numberSnapshot);

        if (currentPage.numberGroup != null)
        {
            currentPage.numberGroup.alpha = 0f;
            currentPage.numberGroup.interactable = false;
            currentPage.numberGroup.blocksRaycasts = false;
        }

        if (nextPage.numberGroup != null)
        {
            nextPage.numberGroup.alpha = 1f;
            nextPage.numberGroup.interactable = true;
            nextPage.numberGroup.blocksRaycasts = true;
        }

        currentIndex = nextIndex;
        RefreshAdvanceLabel();

        isTransitioning = false;

        if (advanceButton != null)
            advanceButton.interactable = true;
    }

    private void FinishTutorial()
    {
        PlayerPrefs.SetInt(completedPrefsKey, 1);
        PlayerPrefs.Save();

        if (canvasRoot != null)
            canvasRoot.SetActive(false);

        if (advanceButton != null)
            advanceButton.interactable = false;
    }

    private void RefreshAdvanceLabel()
    {
        if (advanceButtonLabel == null || pages == null || pages.Length == 0)
            return;

        bool isLast = currentIndex >= pages.Length - 1;
        advanceButtonLabel.text = isLast ? finishLabel : nextLabel;
    }

    private CanvasGroup GetOrAddCanvasGroup(GameObject go)
    {
        if (go == null)
            return null;

        CanvasGroup cg = go.GetComponent<CanvasGroup>();
        if (cg == null)
            cg = go.AddComponent<CanvasGroup>();

        return cg;
    }

    private float EaseInOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return t < 0.5f
            ? 4f * t * t * t
            : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (pages == null)
            return;

        for (int i = 0; i < pages.Length; i++)
        {
            if (pages[i] == null)
                continue;

            if (pages[i].root != null && pages[i].rootGroup == null)
                pages[i].rootGroup = pages[i].root.GetComponent<CanvasGroup>();

            if (pages[i].numberObject != null && pages[i].numberGroup == null)
                pages[i].numberGroup = pages[i].numberObject.GetComponent<CanvasGroup>();
        }

        if (mainPanelIndex < 0)
            mainPanelIndex = 0;

        if (pages.Length > 0 && mainPanelIndex >= pages.Length)
            mainPanelIndex = pages.Length - 1;
    }
#endif
}