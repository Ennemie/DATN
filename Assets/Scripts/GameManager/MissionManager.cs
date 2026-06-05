using System.Collections.Generic;
using UnityEngine;

public class MissionManager : MonoBehaviour
{
    public static MissionManager instance { get; private set; }

    [SerializeField] private BoxCollider nextSceneCollider;
    [HideInInspector] public string nextSceneName;
    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
        DontDestroyOnLoad(gameObject);
    }
    void Start()
    {
        nextSceneCollider.enabled = false;
        currentMissionIndex = 0;
    }

    [SerializeField] private List<Mission> missions;
    private int currentMissionIndex;

    public void ShowCurrentMission()
    {
        if (missions[currentMissionIndex].isCompleted) return;
        GameCanvas.Instance.ShowMissionBox(true);
    }
    public string GetTitle()
    {
        return missions[currentMissionIndex].title;
    }
    public void CompleteCurrentMission(int missionIndex, bool isSceneComplete, string _nextSceneName)
    {
        if (missions[missionIndex].isCompleted == true) return;
        if (missionIndex != currentMissionIndex) return;

        missions[missionIndex].isCompleted = true;
        StartCoroutine(GameCanvas.Instance.ShowNextMission());
        if (isSceneComplete)
        {
            nextSceneCollider.enabled = true;
            nextSceneName = _nextSceneName;
        }
    }
    public void AssignNextMission()
    {
        if (currentMissionIndex < missions.Count - 1)
        {
            currentMissionIndex++;
            GameCanvas.Instance.ShowMissionBox(true);
        }
    }

    [System.Serializable]
    public class Mission
    {
        public string title;
        [HideInInspector] public bool isCompleted = false;
        public GameObject target;
    }
}
