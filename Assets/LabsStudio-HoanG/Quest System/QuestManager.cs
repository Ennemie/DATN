using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance { get; private set; }

    [Header("Configuration")]
    [SerializeField] private QuestInfoSO startingQuest; // Nhiệm vụ đầu tiên của game

    private Dictionary<string, Quest> questMap;
    private string currentActiveQuestId;

    [Header("Events")]
    public UnityEvent<Quest> OnQuestStateChanged;
    public UnityEvent<Quest> OnObjectiveProgressed;

    private void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); return; }

        questMap = new Dictionary<string, Quest>();
    }

    // Hàm này sẽ được gọi SAU KHI bạn tải dữ liệu từ Firebase thành công
    public void InitializeSystem(List<QuestSaveData> cloudData)
    {
        // 1. Load toàn bộ ScriptableObject từ thư mục Resources hoặc Addressables
        QuestInfoSO[] allQuests = Resources.LoadAll<QuestInfoSO>("Quests");
        
        // 2. Map dữ liệu đám mây vào logic
        foreach (QuestInfoSO info in allQuests)
        {
            QuestSaveData data = cloudData.Find(q => q.questId == info.Id);
            if (data == null)
            {
                data = new QuestSaveData(info.Id, QuestState.Locked);
                // Mở khóa nhiệm vụ đầu tiên nếu chưa có data
                if (info == startingQuest) data.state = QuestState.CanStart; 
            }
            
            Quest newQuest = new Quest(info, data);
            questMap.Add(info.Id, newQuest);

            if (data.state == QuestState.Active)
            {
                currentActiveQuestId = info.Id;
            }
        }
    }

    // API public cho các hệ thống khác (như Easy Cutscene) gọi vào
    public void AdvanceCurrentQuest()
    {
        if (string.IsNullOrEmpty(currentActiveQuestId)) return;

        Quest activeQuest = questMap[currentActiveQuestId];
        activeQuest.AddProgress(1);

        OnObjectiveProgressed?.Invoke(activeQuest);

        if (activeQuest.SaveData.state == QuestState.Completed)
        {
            CompleteQuest(activeQuest);
        }
        else
        {
            SaveToFirebase();
        }
    }

    private void CompleteQuest(Quest completedQuest)
    {
        currentActiveQuestId = null;
        OnQuestStateChanged?.Invoke(completedQuest);

        // Kích hoạt quest tiếp theo trong chuỗi tuyến tính
        if (completedQuest.Info.nextLinearQuest != null)
        {
            string nextId = completedQuest.Info.nextLinearQuest.Id;
            questMap[nextId].SaveData.state = QuestState.Active;
            currentActiveQuestId = nextId;
            OnQuestStateChanged?.Invoke(questMap[nextId]);
        }

        SaveToFirebase();
    }

    private void SaveToFirebase()
    {
        // Build list dữ liệu động
        List<QuestSaveData> dataToSave = new List<QuestSaveData>();
        foreach (var quest in questMap.Values)
        {
            dataToSave.Add(quest.SaveData);
        }

        // Parse list ra JSON để đẩy qua Firebase SDK
        // Custom wrapper class required for serializing Lists in Unity JSONUtility
        // Firebase database reference push logic goes here...
    }
}