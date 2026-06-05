using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class GameCanvas : MonoBehaviour
{
    public static GameCanvas Instance { get; private set; }

    // Icon color
    private Color iconColor;
    private string hexColor = "#00AEEF";

    [Header("Pause Menu")]
    [SerializeField] private GameObject panel;
    [SerializeField] private GameObject pauseIcon;
    [SerializeField] private GameObject playIcon;

    [Header("Mission")]
    [SerializeField] private Button missionBtn;
    [SerializeField] private Image missionIcon;
    [SerializeField] private Image missionCompleteIcon;
    [SerializeField] private RectTransform missionTextBox;
    [SerializeField] private TMP_Text missionText;
    private Vector2 missionTextBoxOGPos = new Vector2(-84.01496f,0);
    private float missionTextBoxOGWidth = 0;
    private Vector2 missionTextBoxTargetPos = new Vector2(6.2691f, 0);
    private float missionTextBoxTargetWidth = 365;
    private float missionBoxAnimDuration = 0.3f;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        TogglePauseIcon(false);
        iconColor = ColorUtility.TryParseHtmlString(hexColor, out var color) ? color : Color.white;
        ShowMissionBox(false);
    }

    // Pause menu
    private void TogglePauseIcon(bool isPause)
    {
        pauseIcon.SetActive(!isPause);
        playIcon.SetActive(isPause);
        panel.SetActive(isPause);
    }

    public void TogglePauseMenu()
    {
        if (GameManager.Instance.state == GameManager.GameState.Paused)
        {
            GameManager.Instance.state = GameManager.GameState.Playing;
            TogglePauseIcon(false);
        }
        else
        {
            GameManager.Instance.state = GameManager.GameState.Paused;
            TogglePauseIcon(true);
        }
    }
    // Pause menu ----

    // Mission
    public void ToggleShowMission()
    {
        if(!missionTextBox.gameObject.activeSelf)
        {
            ShowMissionBox(true);
        }
        else
        {
            ShowMissionBox(false);
        }
    }
    public void ShowMissionBox(bool show)
    {
        DOTween.Kill(missionTextBox);
        if (show)
        {
            missionIcon.color = iconColor;
            missionBtn.interactable = false;
            missionTextBox.gameObject.SetActive(true);
            missionText.gameObject.SetActive(false);

            missionTextBox.anchoredPosition = missionTextBoxOGPos;
            missionTextBox.sizeDelta = new Vector2(missionTextBoxOGWidth, missionTextBox.sizeDelta.y);

            // Chạy hiệu ứng mở ra (Dùng OutQuad cho cảm giác mở nhanh rồi dừng mượt)
            missionTextBox.DOAnchorPos(missionTextBoxTargetPos, missionBoxAnimDuration).SetEase(Ease.OutQuad);
            missionTextBox.DOSizeDelta(new Vector2(missionTextBoxTargetWidth, missionTextBox.sizeDelta.y), missionBoxAnimDuration).SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    missionText.gameObject.SetActive(true);
                    missionText.text = MissionManager.instance.GetTitle();
                    missionBtn.interactable = true;
                });
        }
        else
        {
            missionIcon.color = Color.white;
            missionBtn.interactable = false;
            // Ẩn chữ ngay lập tức để khi khung thu nhỏ lại nhìn không bị lem chữ ra ngoài
            missionText.gameObject.SetActive(false);

            // Chạy hiệu ứng đóng lại (Nên dùng InQuad: bắt đầu chậm và biến mất nhanh dần, tạo cảm giác dứt khoát)
            missionTextBox.DOAnchorPos(missionTextBoxOGPos, missionBoxAnimDuration).SetEase(Ease.InQuad);
            missionTextBox.DOSizeDelta(new Vector2(missionTextBoxOGWidth, missionTextBox.sizeDelta.y), missionBoxAnimDuration).SetEase(Ease.InQuad)
                .OnComplete(() =>
                {
                    missionBtn.interactable = true;
                    missionTextBox.gameObject.SetActive(false);
                });
        }
    }

    public IEnumerator ShowNextMission()
    {
        missionIcon.gameObject.SetActive(false);
        missionCompleteIcon.gameObject.SetActive(true);
        missionText.color = Color.yellow;
        if (!missionTextBox.gameObject.activeSelf)
        {
            ShowMissionBox(true);
        }

        yield return new WaitForSeconds(2f);
        missionCompleteIcon.gameObject.SetActive(false);
        missionIcon.gameObject.SetActive(true);
        missionIcon.color = Color.white;
        ShowMissionBox(false);

        yield return new WaitForSeconds(2f);
        missionText.color = iconColor;
        MissionManager.instance.AssignNextMission();
    }
}
