// File: QuestManager.cs
// Trách nhiệm (SRP): Điểm giao tiếp duy nhất giữa Quest System và phần còn lại của game.
//   • Khởi tạo quest map từ Resources + Firebase data
//   • Expose API rõ ràng cho UI, Cutscene, NPC, Gameplay systems
//   • Fire events khi state thay đổi
//   • Gọi Firebase save
//
// KHÔNG chứa logic tiến độ task/quest — việc đó là của Quest và QuestTask.

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

public class QuestManager : MonoBehaviour
{
    // ── Singleton ─────────────────────────────────────────────────────────────
    public static QuestManager Instance { get; private set; }

    // ── Inspector Configuration ───────────────────────────────────────────────
    [Header("Configuration")]
    [Tooltip("Quest đầu tiên tự động mở khóa (CanStart) khi khởi động game mới.")]
    [SerializeField] private QuestInfoSO startingQuest;

    [Tooltip("Đường dẫn trong Resources/ chứa tất cả QuestInfoSO assets. Mặc định: 'Quests'")]
    [SerializeField] private string questResourcesPath = "Quests";

    // ── Internal State ────────────────────────────────────────────────────────
    private Dictionary<string, Quest> questMap = new Dictionary<string, Quest>();
    private string activeQuestId;

    // ── Public Events ─────────────────────────────────────────────────────────
    [Header("Events")]
    [Tooltip("Khi 1 quest được kích hoạt (CanStart → Active).")]
    public UnityEvent<Quest> OnQuestStarted;

    [Tooltip("Khi 1 task của quest active hoàn thành (chuyển sang task tiếp theo).")]
    public UnityEvent<Quest, QuestTask> OnTaskCompleted;

    [Tooltip("Khi toàn bộ quest hoàn thành (kể cả việc mở quest tiếp theo).")]
    public UnityEvent<Quest> OnQuestCompleted;

    [Tooltip("Khi tiến độ task thay đổi (dùng để update UI progress bar / text).")]
    public UnityEvent<Quest, QuestTask> OnTaskProgressed;

    // ─────────────────────────────────────────────────────────────────────────
    #region Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region Initialization API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Khởi tạo toàn bộ Quest System sau khi đã load data từ Firebase.<br/>
    /// Gọi hàm này trong callback thành công của Firebase Auth/Load.
    /// </summary>
    /// <param name="cloudData">Dữ liệu quest save lấy từ Firebase. Truyền list rỗng nếu game mới.</param>
    public void InitializeSystem(List<QuestSaveData> cloudData)
    {
        questMap.Clear();
        activeQuestId = null;

        // Load toàn bộ QuestInfoSO từ Resources/Quests/
        QuestInfoSO[] allQuestInfos = Resources.LoadAll<QuestInfoSO>(questResourcesPath);

        if (allQuestInfos.Length == 0)
            Debug.LogWarning($"[QuestManager] Không tìm thấy QuestInfoSO nào trong Resources/{questResourcesPath}/");

        foreach (QuestInfoSO info in allQuestInfos)
        {
            // Tìm save data tương ứng từ Firebase, hoặc tạo mới
            QuestSaveData saveData = cloudData?.Find(q => q.questId == info.Id);

            if (saveData == null)
            {
                var initialState = (info == startingQuest) ? QuestState.CanStart : QuestState.Locked;
                saveData = new QuestSaveData(info.Id, initialState, info.tasks.Count);
            }
            else
            {
                // Migration: nếu taskSaves null (dữ liệu cũ), tạo mới
                if (saveData.taskSaves == null || saveData.taskSaves.Count == 0)
                {
                    saveData.taskSaves = new List<TaskSaveData>(info.tasks.Count);
                    for (int i = 0; i < info.tasks.Count; i++)
                        saveData.taskSaves.Add(new TaskSaveData(info.tasks[i].taskId));
                }
            }

            Quest quest = new Quest(info, saveData);
            questMap[info.Id] = quest;

            if (saveData.state == QuestState.Active)
                activeQuestId = info.Id;
        }

        Debug.Log($"[QuestManager] Đã khởi tạo {questMap.Count} quest. Active: {activeQuestId ?? "Không có"}");
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region Query API  (Chỉ đọc — không thay đổi state)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Trả về quest đang Active. Null nếu không có quest nào đang chạy.</summary>
    public Quest GetActiveQuest()
    {
        if (string.IsNullOrEmpty(activeQuestId)) return null;
        questMap.TryGetValue(activeQuestId, out Quest q);
        return q;
    }

    /// <summary>Trả về task đang được thực hiện trong quest active. Null nếu không có.</summary>
    public QuestTask GetCurrentTask()
    {
        return GetActiveQuest()?.CurrentTask;
    }

    /// <summary>Tra cứu quest theo ID. Trả về null nếu không tồn tại.</summary>
    public Quest GetQuestById(string questId)
    {
        questMap.TryGetValue(questId, out Quest q);
        return q;
    }

    /// <summary>Trả về danh sách tất cả quest đang được track.</summary>
    public IEnumerable<Quest> GetAllQuests() => questMap.Values;

    /// <summary>Trả về danh sách quest theo state cụ thể.</summary>
    public IEnumerable<Quest> GetQuestsByState(QuestState state) =>
        questMap.Values.Where(q => q.State == state);

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region Action API  (Thay đổi state — dùng từ UI, Cutscene, NPC, Gameplay)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Kích hoạt quest (chuyển từ CanStart → Active).<br/>
    /// Dùng khi player nói chuyện với NPC nhận nhiệm vụ.
    /// </summary>
    public void StartQuest(string questId)
    {
        if (!questMap.TryGetValue(questId, out Quest quest))
        {
            Debug.LogWarning($"[QuestManager] StartQuest: Không tìm thấy quest ID '{questId}'");
            return;
        }

        if (quest.State != QuestState.CanStart)
        {
            Debug.LogWarning($"[QuestManager] StartQuest: Quest '{quest.Info.displayName}' " +
                             $"không ở trạng thái CanStart (hiện tại: {quest.State})");
            return;
        }

        quest.Activate();
        activeQuestId = questId;

        OnQuestStarted?.Invoke(quest);
        SaveToFirebase();
    }

    /// <summary>
    /// Tăng tiến độ task hiện tại của quest đang Active.<br/>
    /// Dùng khi gameplay event xảy ra (vd: nhặt vật phẩm, tiêu diệt kẻ địch...).
    /// </summary>
    /// <param name="amount">Lượng progress tăng thêm (mặc định 1).</param>
    public void ProgressCurrentTask(int amount = 1)
    {
        Quest quest = GetActiveQuest();
        if (quest == null)
        {
            Debug.Log("[QuestManager] ProgressCurrentTask: Không có quest nào đang Active.");
            return;
        }

        QuestTask taskBefore = quest.CurrentTask;
        TaskCompletionResult result = quest.ProgressCurrentTask(amount);
        HandleTaskResult(quest, taskBefore, result);
    }

    /// <summary>
    /// Hoàn thành ngay lập tức task hiện tại — không cần đủ progress.<br/>
    /// <b>Dùng trong cutscene event (cutscenePostEvent) để advance quest sau mỗi cảnh phim.</b><br/>
    /// Ví dụ: kéo vào ô "cutscenePostEvent" của EcCutscene trong Inspector.
    /// </summary>
    public void ForceCompleteCurrentTask()
    {
        Quest quest = GetActiveQuest();
        if (quest == null)
        {
            Debug.Log("[QuestManager] ForceCompleteCurrentTask: Không có quest nào đang Active.");
            return;
        }

        QuestTask taskBefore = quest.CurrentTask;
        TaskCompletionResult result = quest.ForceCompleteCurrentTask();
        HandleTaskResult(quest, taskBefore, result);
    }

    /// <summary>
    /// Mở khóa quest (Locked → CanStart) — dùng khi điều kiện mở khóa được thỏa mãn.
    /// </summary>
    public void UnlockQuest(string questId)
    {
        if (!questMap.TryGetValue(questId, out Quest quest))
        {
            Debug.LogWarning($"[QuestManager] UnlockQuest: Không tìm thấy quest ID '{questId}'");
            return;
        }

        if (quest.State != QuestState.Locked)
        {
            Debug.LogWarning($"[QuestManager] UnlockQuest: Quest '{quest.Info.displayName}' " +
                             $"không ở trạng thái Locked (hiện tại: {quest.State})");
            return;
        }

        quest.SaveData.state = QuestState.CanStart;
        SaveToFirebase();
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region Private Helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Xử lý kết quả sau khi gọi Progress/ForceComplete — fire event và save.</summary>
    private void HandleTaskResult(Quest quest, QuestTask completedTask, TaskCompletionResult result)
    {
        switch (result)
        {
            case TaskCompletionResult.Progressed:
                OnTaskProgressed?.Invoke(quest, quest.CurrentTask);
                SaveToFirebase();
                break;

            case TaskCompletionResult.TaskCompleted:
                // completedTask là task vừa xong, quest.CurrentTask là task mới bắt đầu
                OnTaskCompleted?.Invoke(quest, completedTask);
                OnTaskProgressed?.Invoke(quest, quest.CurrentTask);
                SaveToFirebase();
                break;

            case TaskCompletionResult.QuestCompleted:
                OnTaskCompleted?.Invoke(quest, completedTask);
                OnQuestCompleted?.Invoke(quest);
                activeQuestId = null;

                // Tự động mở quest tiếp theo trong chuỗi tuyến tính
                if (quest.Info.nextLinearQuest != null)
                {
                    string nextId = quest.Info.nextLinearQuest.Id;
                    if (questMap.TryGetValue(nextId, out Quest nextQuest))
                    {
                        nextQuest.SaveData.state = QuestState.CanStart;
                        Debug.Log($"[QuestManager] Mở khóa quest tiếp theo: '{nextQuest.Info.displayName}'");
                    }
                }

                SaveToFirebase();
                break;

            case TaskCompletionResult.QuestNotActive:
                Debug.LogWarning($"[QuestManager] Quest '{quest.Info.displayName}' không ở trạng thái Active.");
                break;

            case TaskCompletionResult.AlreadyCompleted:
                Debug.Log($"[QuestManager] Quest '{quest.Info.displayName}' đã hoàn thành rồi.");
                break;
        }
    }

    private void SaveToFirebase()
    {
        List<QuestSaveData> dataToSave = questMap.Values
            .Select(q => q.SaveData)
            .ToList();

        // TODO: Serialize dataToSave thành JSON và đẩy lên Firebase
        // Ví dụ: FirebaseDatabase.DefaultInstance.GetReference("users/{uid}/quests")
        //        .SetValueAsync(JsonUtility.ToJson(new Wrapper { quests = dataToSave }));
        Debug.Log($"[QuestManager] Saving {dataToSave.Count} quests to Firebase...");
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region Editor Debug Buttons (ContextMenu)
    // ─────────────────────────────────────────────────────────────────────────

    [ContextMenu("DEBUG: Log Active Quest Info")]
    private void Debug_LogActiveQuest()
    {
        Quest q = GetActiveQuest();
        if (q == null) { Debug.Log("[Debug] Không có quest nào đang Active."); return; }

        Debug.Log($"[Debug] Active Quest: '{q.Info.displayName}' | State: {q.State}");
        Debug.Log($"[Debug] Current Task ({q.CurrentTaskIndex + 1}/{q.Tasks.Count}): " +
                  $"'{q.CurrentTask?.Info.displayName}' | " +
                  $"Progress: {q.CurrentTask?.ProgressText}");
    }

    [ContextMenu("DEBUG: Force Complete Current Task")]
    private void Debug_ForceCompleteTask()
    {
        ForceCompleteCurrentTask();
    }

    [ContextMenu("DEBUG: Progress Current Task +1")]
    private void Debug_ProgressTask()
    {
        ProgressCurrentTask(1);
    }

    [ContextMenu("DEBUG: Log All Quests")]
    private void Debug_LogAllQuests()
    {
        if (questMap.Count == 0) { Debug.Log("[Debug] Quest map trống."); return; }
        foreach (var kvp in questMap)
        {
            Quest q = kvp.Value;
            Debug.Log($"[Debug] [{q.State}] '{q.Info.displayName}' " +
                      $"| Task {q.CurrentTaskIndex}/{q.Tasks.Count}");
        }
    }

    #endregion
}