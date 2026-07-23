// File: QuestSystemTester.cs
// Dùng để test Quest System trực tiếp trong Unity Editor mà không cần Firebase.
// Gắn script này vào cùng GameObject với QuestManager, hoặc GameObject riêng trong scene test.

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Script test nội bộ cho Quest System.<br/>
/// Kéo thả QuestInfoSO vào đây, nhấn Play, dùng các nút ContextMenu để test.
/// </summary>
public class QuestSystemTester : MonoBehaviour
{
    [Header("Test Configuration")]
    [Tooltip("Kéo QuestInfoSO của bạn vào đây để test (có thể nhiều quest).")]
    [SerializeField] private QuestInfoSO[] questsToTest;

    [Tooltip("Quest sẽ tự động được StartQuest() khi nhấn nút test bên dưới.")]
    [SerializeField] private string questIdToStart;

    [Header("Auto-Start on Play")]
    [Tooltip("Nếu tick, sẽ tự khởi tạo QuestManager và start quest khi vào Play Mode.")]
    [SerializeField] private bool autoInitOnPlay = true;

    // ── Unity Lifecycle ───────────────────────────────────────────────────────
    private void Start()
    {
        // Đăng ký lắng nghe events sau khi tất cả Manager đã Awake xong
        if (QuestManager.Instance != null)
            RegisterEvents();

        if (autoInitOnPlay)
            InitializeWithEmptyData();
    }

    private void OnDestroy()
    {
        if (QuestManager.Instance != null)
            UnregisterEvents();
    }

    // ── Public Test Methods (gọi từ Button UI hoặc ContextMenu) ──────────────

    [ContextMenu("STEP 1 — Init System (Empty / New Game Data)")]
    public void InitializeWithEmptyData()
    {
        if (QuestManager.Instance == null)
        {
            Debug.LogError("[Tester] Không tìm thấy QuestManager.Instance. " +
                           "Hãy đảm bảo QuestManager đã có trong Scene.");
            return;
        }

        // Truyền list rỗng = giả lập game mới, không có dữ liệu Firebase
        QuestManager.Instance.InitializeSystem(new List<QuestSaveData>());
        RegisterEvents();

        Debug.Log("[Tester] ✅ Khởi tạo QuestManager thành công với dữ liệu trống (game mới).");
        Debug_PrintAllQuests();
    }

    [ContextMenu("STEP 2 — Start Quest (dùng questIdToStart ở trên)")]
    public void StartTestQuest()
    {
        if (string.IsNullOrEmpty(questIdToStart))
        {
            Debug.LogWarning("[Tester] Chưa điền questIdToStart trong Inspector!");
            return;
        }
        QuestManager.Instance.StartQuest(questIdToStart);
    }

    [ContextMenu("STEP 3 — Progress Current Task (+1)")]
    public void ProgressTask()
    {
        QuestManager.Instance.ProgressCurrentTask(1);
    }

    [ContextMenu("STEP 4 — Force Complete Current Task (Skip)")]
    public void ForceComplete()
    {
        QuestManager.Instance.ForceCompleteCurrentTask();
    }

    [ContextMenu("--- Print All Quest States ---")]
    public void Debug_PrintAllQuests()
    {
        Debug.Log("══════════════════════════════════════════");
        Debug.Log("            QUEST MAP STATUS              ");
        Debug.Log("══════════════════════════════════════════");

        foreach (Quest quest in QuestManager.Instance.GetAllQuests())
        {
            string stateIcon = quest.State switch
            {
                QuestState.Locked    => "🔒",
                QuestState.CanStart  => "🟡",
                QuestState.Active    => "🔵",
                QuestState.Completed => "✅",
                _                    => "❓"
            };

            Debug.Log($"{stateIcon} [{quest.State}] Quest: '{quest.Info.displayName}'");

            for (int i = 0; i < quest.Tasks.Count; i++)
            {
                QuestTask task = quest.Tasks[i];
                string taskIcon = task.IsCompleted ? "✔" : (i == quest.CurrentTaskIndex ? "▶" : "○");
                Debug.Log($"   {taskIcon} Task {i + 1}: '{task.Info.displayName}' | {task.ProgressText}");
            }
        }

        Debug.Log("══════════════════════════════════════════");
    }

    // ── Event Handlers ────────────────────────────────────────────────────────
    private void RegisterEvents()
    {
        QuestManager.Instance.OnQuestStarted.AddListener(OnQuestStarted);
        QuestManager.Instance.OnTaskCompleted.AddListener(OnTaskCompleted);
        QuestManager.Instance.OnQuestCompleted.AddListener(OnQuestCompleted);
        QuestManager.Instance.OnTaskProgressed.AddListener(OnTaskProgressed);
        QuestManager.Instance.OnQuestUnlocked.AddListener(OnQuestUnlocked);
        QuestManager.Instance.OnTaskActivated.AddListener(OnTaskActivated);
        QuestManager.Instance.OnSystemInitialized.AddListener(OnSystemInitialized);
    }

    private void UnregisterEvents()
    {
        QuestManager.Instance.OnQuestStarted.RemoveListener(OnQuestStarted);
        QuestManager.Instance.OnTaskCompleted.RemoveListener(OnTaskCompleted);
        QuestManager.Instance.OnQuestCompleted.RemoveListener(OnQuestCompleted);
        QuestManager.Instance.OnTaskProgressed.RemoveListener(OnTaskProgressed);
        QuestManager.Instance.OnQuestUnlocked.RemoveListener(OnQuestUnlocked);
        QuestManager.Instance.OnTaskActivated.RemoveListener(OnTaskActivated);
        QuestManager.Instance.OnSystemInitialized.RemoveListener(OnSystemInitialized);
    }

    private void OnQuestStarted(Quest quest)
        => Debug.Log($"<color=cyan>[EVENT] Quest Started: '{quest.Info.displayName}'</color>");

    private void OnTaskProgressed(Quest quest, QuestTask task)
        => Debug.Log($"<color=white>[EVENT] Task Progressed: '{task?.Info.displayName}' | {task?.ProgressText}</color>");

    private void OnTaskCompleted(Quest quest, QuestTask task)
        => Debug.Log($"<color=yellow>[EVENT] Task Completed: '{task?.Info.displayName}'</color>");

    private void OnQuestCompleted(Quest quest)
        => Debug.Log($"<color=green>[EVENT] Quest Completed: '{quest.Info.displayName}'</color>");

    private void OnQuestUnlocked(Quest quest)
        => Debug.Log($"<color=orange>[EVENT] Quest Unlocked: '{quest.Info.displayName}' — Quest Giver: '{quest.Info.questGiver?.displayName}'</color>");

    private void OnTaskActivated(Quest quest, QuestTask task)
        => Debug.Log($"<color=#00BFFF>[EVENT] ▶ Task Activated: '{task?.Info.displayName}' " +
                     $"| preCutscene: '{task?.Info.preCutsceneName}' " +
                     $"| Minimap: {task?.Info.targetLocationName}</color>");

    private void OnSystemInitialized()
        => Debug.Log($"<color=#AAAAAA>[EVENT] ⚙ System Initialized — {QuestManager.Instance.GetAllQuests().Count()} quests loaded.</color>");
}
