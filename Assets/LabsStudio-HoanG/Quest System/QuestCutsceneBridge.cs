// File: QuestCutsceneBridge.cs
// Trách nhiệm (SRP): Cầu nối giữa Quest System và Easy Cutscene System.
//   • Lắng nghe OnTaskActivated → trigger preCutsceneName từ TaskInfo
//   • Lắng nghe OnTaskCompleted → trigger postCutsceneName từ TaskInfo
//
// CÁCH DÙNG: Gắn script này vào cùng GameObject với QuestManager (hoặc riêng).
//            Không cần kéo thả gì trong Inspector — tự động kết nối qua QuestManager.Instance.
//
// DATA FLOW: QuestInfoSO.TaskInfo.preCutsceneName → EcCutsceneManager.InitCutscenes()
//            QuestInfoSO.TaskInfo.postCutsceneName → EcCutsceneManager.InitCutscenes()

using HisaGames.CutsceneManager;
using UnityEngine;

public class QuestCutsceneBridge : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip(
        "Nếu tick, tự động trigger preCutscene khi task mới được kích hoạt.\n" +
        "Bỏ tick nếu bạn muốn trigger cutscene thủ công.")]
    [SerializeField] private bool autoPlayPreCutscene  = true;

    [Tooltip(
        "Nếu tick, tự động trigger postCutscene khi task hoàn thành.\n" +
        "Bỏ tick nếu bạn muốn trigger thủ công.")]
    [SerializeField] private bool autoPlayPostCutscene = true;

    // ── Unity Lifecycle ───────────────────────────────────────────────────────
    private void OnEnable()
    {
        // Đăng ký sau Start để đảm bảo QuestManager đã Awake xong
        QuestManager.Instance.OnTaskActivated.AddListener(OnTaskActivated);
        QuestManager.Instance.OnTaskCompleted.AddListener(OnTaskCompleted);
        QuestManager.Instance.OnSystemInitialized.AddListener(OnSystemInitialized);
    }

    private void OnDisable()
    {
        if (QuestManager.Instance == null) return;
        QuestManager.Instance.OnTaskActivated.RemoveListener(OnTaskActivated);
        QuestManager.Instance.OnTaskCompleted.RemoveListener(OnTaskCompleted);
        QuestManager.Instance.OnSystemInitialized.RemoveListener(OnSystemInitialized);
    }

    // ── Event Handlers ────────────────────────────────────────────────────────

    /// <summary>
    /// Khi task mới được kích hoạt → play preCutsceneName của task đó.
    /// </summary>
    private void OnTaskActivated(Quest quest, QuestTask task)
    {
        if (!autoPlayPreCutscene || task == null) return;
        // Skip nếu đang khôi phục save — tránh play lại cutscene đã xem
        if (QuestManager.Instance.IsRestoringState) return;

        string cutsceneName = task.Info.preCutsceneName;
        if (!string.IsNullOrEmpty(cutsceneName))
            PlayCutscene(cutsceneName, $"[pre] task '{task.Info.displayName}'");
    }

    /// <summary>
    /// Khi task vừa hoàn thành → play postCutsceneName của task đó.
    /// </summary>
    private void OnTaskCompleted(Quest quest, QuestTask task)
    {
        if (!autoPlayPostCutscene || task == null) return;
        if (QuestManager.Instance.IsRestoringState) return;

        string cutsceneName = task.Info.postCutsceneName;
        if (!string.IsNullOrEmpty(cutsceneName))
            PlayCutscene(cutsceneName, $"[post] task '{task.Info.displayName}'");
    }

    /// <summary>
    /// Khi system init xong — nếu đang có quest active được phục hồi,
    /// OnTaskActivated sẽ tự fire nên không cần xử lý thêm ở đây.
    /// </summary>
    private void OnSystemInitialized()
    {
        Debug.Log("[QuestCutsceneBridge] System initialized — cutscene bridge ready.");
    }

    // ── Public API (gọi thủ công nếu cần) ────────────────────────────────────

    /// <summary>
    /// Trigger cutscene thủ công theo tên — dùng khi cần override logic tự động.
    /// </summary>
    public void PlayCutsceneByName(string cutsceneName)
    {
        PlayCutscene(cutsceneName, "manual");
    }

    // ── Private ───────────────────────────────────────────────────────────────
    private void PlayCutscene(string cutsceneName, string reason)
    {
        if (EcCutsceneManager.instance == null)
        {
            Debug.LogWarning($"[QuestCutsceneBridge] EcCutsceneManager không có trong scene! " +
                             $"Cutscene '{cutsceneName}' ({reason}) không thể play.");
            return;
        }

        Debug.Log($"[QuestCutsceneBridge] Playing cutscene '{cutsceneName}' ({reason})");
        EcCutsceneManager.instance.InitCutscenes(cutsceneName);
    }
}
