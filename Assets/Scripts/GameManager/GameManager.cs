using System;
using System.Collections;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [HideInInspector] public enum GameState
    {
        Playing,
        Paused,
        GameOver
    }
    private GameState _state;
    public GameState state
    {
        get => _state;
        set
        {
            _state = value;
            switch (_state)
            {
                case GameState.Playing:
                    ResumeGame();
                    break;
                case GameState.Paused:
                    PauseGame();
                    break;
                case GameState.GameOver:
                    break;
            }
        }
    }

    // Objects will be disabled when the game is paused
    [SerializeField] private GameObject[] objectsToDisableOnPause;
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
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 120;
    }
    void Start()
    {
        StartCoroutine(WaitToShowMission());
    }
    private IEnumerator WaitToShowMission()
    {
        yield return new WaitForSeconds(1f);
        MissionManager.instance.ShowCurrentMission();
    }
    private void ResumeGame()
    {
        Time.timeScale = 1f;
        PlayerCanvasController.Instance.Enable(true);
    }
    private void PauseGame()
    {
        Time.timeScale = 0f;
        PlayerCanvasController.Instance.Enable(false);
        foreach (GameObject obj in objectsToDisableOnPause)
        {
            if (!obj.activeSelf) continue;
            obj.SetActive(false);
        }
    }
}
