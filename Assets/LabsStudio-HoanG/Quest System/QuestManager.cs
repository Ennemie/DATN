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

// ══════════════════════════════════════════════════════════════════════════════
// SERIALIZABLE EVENT WRAPPERS
// Unity Inspector chỉ hiện UnityEvent<T> đúng khi được bọc trong [Serializable] subclass.
// Khai báo ở đây (ngoài class) để các script khác cũng dùng được.
// ══════════════════════════════════════════════════════════════════════════════

/// <summary>Event khi 1 Quest thay đổi state (started / completed).</summary>
[System.Serializable]
public class QuestEvent : UnityEvent<Quest> { }

/// <summary>Event khi 1 Task trong Quest thay đổi (completed / progressed).</summary>
[System.Serializable]
public class QuestTaskEvent : UnityEvent<Quest, QuestTask> { }

/// <summary>Event khi 1 Quest được mở khóa (Locked → CanStart).</summary>
[System.Serializable]
public class QuestUnlockedEvent : UnityEvent<Quest> { }

/// <summary>Event khi phần thưởng quest được phát cho player (sau quest complete).</summary>
[System.Serializable]
public class QuestRewardEvent : UnityEvent<Quest> { }

public class QuestManager : MonoBehaviour, ISaveable
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

    /// <summary>
    /// True trong khoảng thời gian RestoreState() đang chạy.<br/>
    /// QuestCutsceneBridge dùng flag này để bỏ qua cutscene khi khôi phục save
    /// (tránh play lại cutscene đã xem rồi).
    /// </summary>
    public bool IsRestoringState { get; private set; }

    // ── Public Events ─────────────────────────────────────────────────────────
    // Dùng [Serializable] subclass → Unity Inspector hiện đúng:
    //   • Section "Dynamic Quest"      : listener nhận tham số Quest từ runtime
    //   • Section "Static Parameters"  : listener gọi hàm không tham số (vd: SetActive, Play)

    [Header("━━━  EVENTS  ━━━")]

    [Tooltip(
        "[QUEST STARTED]\n" +
        "Fire khi quest chuyển CanStart → Active.\n" +
        "→ Dùng để: hiện Quest Journal, animation nhận nhiệm vụ, bật minimap marker.")]
    public QuestEvent OnQuestStarted;

    [Tooltip(
        "[TASK COMPLETED]\n" +
        "Fire khi 1 task hoàn thành, chuyển sang task tiếp theo.\n" +
        "→ Dùng để: hiện checkmark UI, âm thanh tick, cập nhật task list.")]
    public QuestTaskEvent OnTaskCompleted;

    [Tooltip(
        "[QUEST COMPLETED]\n" +
        "Fire khi toàn bộ quest hoàn thành (kể cả tự động mở quest tiếp theo).\n" +
        "→ Dùng để: hiện màn hình phần thưởng, unlock nội dung mới, lưu thành tích.")]
    public QuestEvent OnQuestCompleted;

    [Tooltip(
        "[TASK PROGRESSED]\n" +
        "Fire mỗi khi tiến độ task tăng (kể cả khi task vừa hoàn thành).\n" +
        "→ Dùng để: cập nhật thanh progress bar, text '2/5', minimap counter.")]
    public QuestTaskEvent OnTaskProgressed;

    [Tooltip(
        "[QUEST UNLOCKED]\n" +
        "Fire khi quest chuyển Locked → CanStart (có thể nhận nhiệm vụ).\n" +
        "→ Dùng để: hiện dấu '!' trên NPC, notification 'Nhiệm vụ mới', highlight quest giver trên minimap.")]
    public QuestUnlockedEvent OnQuestUnlocked;

    [Tooltip(
        "[TASK ACTIVATED]\n" +
        "Fire khi 1 task bắt đầu trở thành task đang active (task 0 khi nhận quest, task kế khi task trước xong).\n" +
        "→ Dùng để: tự động mở cutscene (preCutsceneName), bật minimap marker mới, cập nhật NPC dialogue.\n" +
        "--- Kết nối với: QuestCutsceneBridge.PlayPreCutscene(Quest, QuestTask) ---")]
    public QuestTaskEvent OnTaskActivated;

    [Tooltip(
        "[SYSTEM INITIALIZED]\n" +
        "Fire 1 lần duy nhất sau khi InitializeSystem() hoàn tất (build xong toàn bộ quest map).\n" +
        "→ Dùng để: UI Quest Journal rebuild danh sách, Firebase ready indicator, khởi động minimap markers.")]
    public UnityEvent OnSystemInitialized;

    [Tooltip(
        "[REWARD GRANTED]\n" +
        "Fire sau khi phần thưởng đã được cộng vào player (sau quest complete).\n" +
        "→ Dùng để: hiện popup thưởng, animation xu, sound đặc biệt.")]
    public QuestRewardEvent OnRewardGranted;

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
            return;
        }
    }

    private void Start()
    {
        // Đăng ký vào SaveManager sau Awake để đảm bảo SaveManager.Instance đã tồn tại
        if (SaveManager.Instance != null)
            SaveManager.Instance.RegisterSaveable(this);
        else
            Debug.LogWarning("[QuestManager] SaveManager.Instance chưa có. Hãy đảm bảo SaveManager có trong Scene.");
    }

    private void OnDestroy()
    {
        if (SaveManager.Instance != null)
            SaveManager.Instance.UnregisterSaveable(this);
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

        // Pass 1: build toàn bộ quest map trước
        foreach (QuestInfoSO info in allQuestInfos)
        {
            // Tìm save data tương ứng từ Firebase, hoặc tạo mới
            QuestSaveData saveData = cloudData?.Find(q => q.questId == info.Id);

            if (saveData == null)
            {
                // Game mới: quest đầu tiên tự động CanStart, còn lại Locked
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

        // Pass 2: fire events sau khi toàn bộ quest map đã sẵn sàng
        foreach (Quest quest in questMap.Values)
        {
            if (quest.State == QuestState.CanStart)
            {
                OnQuestUnlocked?.Invoke(quest);
                Debug.Log($"[QuestManager] Quest sẵn sàng nhận: '{quest.Info.displayName}'");
            }
            else if (quest.State == QuestState.Active)
            {
                // Khôi phục trạng thái active: fire OnTaskActivated để hệ thống khôi phục
                // (vd: minimap hiện lại marker, NPC giữ đuóng dialogue)
                OnTaskActivated?.Invoke(quest, quest.CurrentTask);
                Debug.Log($"[QuestManager] Khôi phục quest active: '{quest.Info.displayName}' " +
                          $"→ Task: '{quest.CurrentTask?.Info.displayName}'");
            }
        }

        Debug.Log($"[QuestManager] Đã khởi tạo {questMap.Count} quest. Active: {activeQuestId ?? "Không có"}");
        OnSystemInitialized?.Invoke();  // thông báo cho UI/Firebase sẵn sàng
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
        // Fire OnTaskActivated cho task đầu tiên → trigger preCutsceneName của task 0
        OnTaskActivated?.Invoke(quest, quest.CurrentTask);
        TriggerSave();
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

        if (!quest.Unlock()) return; // quest.Unlock() tự log warning nếu sai state

        OnQuestUnlocked?.Invoke(quest);
        TriggerSave();
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
                TriggerSave();
                break;

            case TaskCompletionResult.TaskCompleted:
                // completedTask là task vừa xong, quest.CurrentTask là task mới bắt đầu
                OnTaskCompleted?.Invoke(quest, completedTask);
                OnTaskProgressed?.Invoke(quest, quest.CurrentTask);
                // Fire OnTaskActivated → trigger preCutsceneName của task mới
                OnTaskActivated?.Invoke(quest, quest.CurrentTask);
                TriggerSave();
                break;

            case TaskCompletionResult.QuestCompleted:
                OnTaskCompleted?.Invoke(quest, completedTask);
                OnQuestCompleted?.Invoke(quest);
                activeQuestId = null;

                // Phát thưởng cho player
                DistributeRewards(quest);

                // Tự động mở khóa quest tiếp theo trong chuỗi tuyến tính
                if (quest.Info.nextLinearQuest != null)
                {
                    string nextId = quest.Info.nextLinearQuest.Id;
                    if (questMap.TryGetValue(nextId, out Quest nextQuest))
                    {
                        if (nextQuest.Unlock())
                        {
                            OnQuestUnlocked?.Invoke(nextQuest);
                            Debug.Log($"[QuestManager] Mở khóa quest tiếp theo: '{nextQuest.Info.displayName}'");
                        }
                    }
                }

                TriggerSave();
                break;

            case TaskCompletionResult.QuestNotActive:
                Debug.LogWarning($"[QuestManager] Quest '{quest.Info.displayName}' không ở trạng thái Active.");
                break;

            case TaskCompletionResult.AlreadyCompleted:
                Debug.Log($"[QuestManager] Quest '{quest.Info.displayName}' đã hoàn thành rồi.");
                break;
        }
    }

    // ── ISaveable Implementation ────────────────────────────────────────────────────

    /// <summary>
    /// SaveManager gọi hàm này khi cần lưu: "Đổ dữ liệu quest vào GameSaveDTO."
    /// </summary>
    public void CaptureState(GameSaveDTO currentSave)
    {
        currentSave.questSaves = questMap.Values
            .Select(q => q.SaveData)
            .ToList();

        Debug.Log($"[QuestManager] CaptureState: đã đổ {currentSave.questSaves.Count} quest vào GameSaveDTO.");
    }

    /// <summary>
    /// SaveManager gọi hàm này sau khi load xong: "Lấy dữ liệu quest từ GameSaveDTO ra dùng."
    /// </summary>
    public void RestoreState(GameSaveDTO currentSave)
    {
        // Set flag trước khi init — QuestCutsceneBridge sẽ skip auto-trigger cutscene
        IsRestoringState = true;

        InitializeSystem(currentSave.questSaves ?? new System.Collections.Generic.List<QuestSaveData>());

        IsRestoringState = false; // Reset sau khi init xong
        Debug.Log($"[QuestManager] RestoreState: khôi phục {currentSave.questSaves?.Count ?? 0} quest.");
    }

    private void TriggerSave()
    {
        if (SaveManager.Instance != null)
            SaveManager.Instance.TriggerSaveGame();
        else
            Debug.LogWarning("[QuestManager] TriggerSave: SaveManager.Instance là null.");
    }

    /// <summary>
    /// Cộng phần thưởng của quest vào CurrentState của SaveManager.
    /// UI lắng nghe OnRewardGranted để hiện animation/popup.
    /// </summary>
    private void DistributeRewards(Quest quest)
    {
        var info = quest.Info;

        // Kiểm tra có thưởng không trước khi xử lý
        bool hasReward = info.intelPointsReward > 0
                      || info.weaponTokenReward > 0
                      || info.experienceReward  > 0
                      || (info.itemRewards != null && info.itemRewards.Length > 0);

        if (!hasReward)
        {
            Debug.Log($"[QuestManager] Quest '{info.displayName}' không có phần thưởng.");
            return;
        }

        // Cộng vào SaveManager.CurrentState (RAM) — TriggerSave sẽ flush xuống file/Firebase
        if (SaveManager.Instance?.CurrentState?.resources != null)
        {
            var res = SaveManager.Instance.CurrentState.resources;
            res.intelPoints   += info.intelPointsReward;
            res.weaponTokens  += info.weaponTokenReward;
            res.experience    += info.experienceReward;
        }
        else
        {
            Debug.LogWarning("[QuestManager] DistributeRewards: SaveManager.CurrentState.resources là null. " +
                             "Phần thưởng số không được lưu.");
        }

        // Item rewards: spawn/unlock (logic tuzỳ hệ thống item của game)
        if (info.itemRewards != null)
        {
            foreach (var item in info.itemRewards)
            {
                if (item != null)
                    Debug.Log($"[QuestManager] TODO: Grant item '{item.name}' cho player.");
                    // Ví dụ: InventoryManager.Instance.AddItem(item);
            }
        }

        Debug.Log($"[QuestManager] Phần thưởng '{info.displayName}': " +
                  $"+{info.intelPointsReward} Intel | " +
                  $"+{info.weaponTokenReward} Tokens | " +
                  $"+{info.experienceReward} EXP");

        OnRewardGranted?.Invoke(quest);
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