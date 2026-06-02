using UnityEngine;

public class GameCanvas : MonoBehaviour
{
    public static GameCanvas Instance { get; private set; }

    // Pause Menu
    [SerializeField] private GameObject panel;
    [SerializeField] private GameObject pauseIcon;
    [SerializeField] private GameObject playIcon;

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
    }

    void Start()
    {
        TogglePauseIcon(false);
    }
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
}
