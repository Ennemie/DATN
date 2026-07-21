// File: NpcQuestInteractor.cs
// Trách nhiệm (SRP): Component gắn lên NPC prefab để kết nối NPC với Quest System.
//   • Tự động hiện/ẩn icon "!" khi quest liên quan đến NPC này thay đổi state
//   • Cung cấp method Interact() để player kích hoạt quest khi nói chuyện
//
// CÁCH DÙNG:
//   1. Gắn script này lên NPC GameObject
//   2. Điền npcId = đúng ID trong QuestInfoSO.questGiver.npcId
//   3. Gắn questIndicator (icon "!" object, có thể dùng Billboard)
//   4. Gọi Interact() từ hệ thống dialogue/input khi player tương tác

using UnityEngine;
using UnityEngine.Events;

public class NpcQuestInteractor : MonoBehaviour
{
    // ── Inspector ────────────────────────────────────────────────────────────────
    [Header("Identity")]
    [Tooltip(
        "ID của NPC này — phải khớp CHÍNH XÁC với npcId trong QuestInfoSO.questGiver hoặc linkedNpc.\n" +
        "Dùng để QuestManager tìm đúng NPC khi fire events.")]
    public string npcId;

    [Header("Quest Indicator")]
    [Tooltip("GameObject hiện dấu '!' khi có quest có thể nhận (CanStart + NPC này là questGiver).")]
    [SerializeField] private GameObject questAvailableIndicator;

    [Tooltip("GameObject hiện dấu '...' hoặc icon khác khi quest đang Active và NPC này là linkedNpc của task hiện tại.")]
    [SerializeField] private GameObject questActiveIndicator;

    [Header("Events (Optional)")]
    [Tooltip("Fire khi NPC này trở thành quest giver có thể tương tác.")]
    public UnityEvent OnBecomeQuestGiver;

    [Tooltip("Fire khi player tương tác và StartQuest thành công.")]
    public UnityEvent OnQuestAccepted;

    [Tooltip("Fire khi player tương tác và hoàn thành/tiến triển Task (khi NPC là mục tiêu của task đang Active).")]
    public UnityEvent OnTaskInteracted;

    // ── Runtime State ─────────────────────────────────────────────────────────
    private Quest _pendingQuest;       // Quest đang chờ nhận (questGiver = NPC này)
    private bool  _isQuestGiverReady; // có quest để phát không

    // ── Unity Lifecycle ───────────────────────────────────────────────────────
    private void Start()
    {
        SetIndicators(available: false, active: false);

        if (QuestManager.Instance == null)
        {
            Debug.LogWarning($"[NpcQuestInteractor:{npcId}] QuestManager.Instance chưa có trong Scene.");
            return;
        }

        QuestManager.Instance.OnQuestUnlocked.AddListener(OnQuestUnlocked);
        QuestManager.Instance.OnQuestStarted.AddListener(OnQuestStarted);
        QuestManager.Instance.OnQuestCompleted.AddListener(OnQuestCompleted);
        QuestManager.Instance.OnTaskActivated.AddListener(OnTaskActivated);
        QuestManager.Instance.OnSystemInitialized.AddListener(OnSystemInitialized);
    }

    private void OnDestroy()
    {
        if (QuestManager.Instance == null) return;
        QuestManager.Instance.OnQuestUnlocked.RemoveListener(OnQuestUnlocked);
        QuestManager.Instance.OnQuestStarted.RemoveListener(OnQuestStarted);
        QuestManager.Instance.OnQuestCompleted.RemoveListener(OnQuestCompleted);
        QuestManager.Instance.OnTaskActivated.RemoveListener(OnTaskActivated);
        QuestManager.Instance.OnSystemInitialized.RemoveListener(OnSystemInitialized);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Gọi từ hệ thống input / dialogue khi player bấm tương tác với NPC này.<br/>
    /// Tự động tìm quest phù hợp và gọi StartQuest.
    /// </summary>
    public void Interact()
    {
        // 1. Ưu tiên kiểm tra: Có quest nào đang chờ nhận không?
        if (_isQuestGiverReady && _pendingQuest != null)
        {
            Debug.Log($"[NpcQuestInteractor:{npcId}] Player nhận quest: '{_pendingQuest.Info.displayName}'");
            QuestManager.Instance.StartQuest(_pendingQuest.Info.Id);
            OnQuestAccepted?.Invoke();
            return;
        }

        // 2. Nếu không có quest để nhận, kiểm tra xem có task nào đang cần NPC này không?
        Quest activeQuest = QuestManager.Instance.GetActiveQuest();
        if (activeQuest != null && activeQuest.CurrentTask != null)
        {
            // Nếu NPC ID của task hiện tại khớp với NPC này
            if (activeQuest.CurrentTask.Info.linkedNpc?.npcId == npcId)
            {
                Debug.Log($"[NpcQuestInteractor:{npcId}] Player tương tác task: '{activeQuest.CurrentTask.Info.displayName}'");
                QuestManager.Instance.ProgressCurrentTask(1);
                OnTaskInteracted?.Invoke();
                return;
            }
        }

        // 3. Không có quest nhận, cũng không có task liên quan
        Debug.Log($"[NpcQuestInteractor:{npcId}] Interact() — không có hành động quest nào khả dụng.");
    }

    /// <summary>Kiểm tra NPC này hiện có quest đang chờ nhận không.</summary>
    public bool HasPendingQuest => _isQuestGiverReady && _pendingQuest != null;

    /// <summary>Quest đang chờ nhận. Dùng để hệ thống dialogue lấy data (displayName, description...).</summary>
    public Quest PendingQuest => _pendingQuest;

    // ── Event Handlers ─────────────────────────────────────────────────────────

    /// <summary>Khi system init xong: quét lại tất cả quest đang CanStart để khôi phục indicator.</summary>
    private void OnSystemInitialized()
    {
        foreach (Quest quest in QuestManager.Instance.GetQuestsByState(QuestState.CanStart))
        {
            CheckIfQuestGiver(quest);
        }
    }

    /// <summary>Khi 1 quest mới mở khóa — kiểm tra xem NPC này có phải là quest giver không.</summary>
    private void OnQuestUnlocked(Quest quest)
    {
        CheckIfQuestGiver(quest);
    }

    /// <summary>Khi quest bắt đầu — nếu NPC này là quest giver thì ẩn icon "!".</summary>
    private void OnQuestStarted(Quest quest)
    {
        if (_pendingQuest == quest)
        {
            _pendingQuest = null;
            _isQuestGiverReady = false;
            SetIndicators(available: false, active: false);
        }
    }

    /// <summary>Khi task mới active — nếu task này có linkedNpc là NPC này thì hiện indicator.</summary>
    private void OnTaskActivated(Quest quest, QuestTask task)
    {
        if (task == null) return;

        bool isLinkedToThisTask = task.Info.linkedNpc?.npcId == npcId;
        SetIndicators(available: _isQuestGiverReady, active: isLinkedToThisTask);
    }

    /// <summary>Khi quest complete — reset hết indicator của NPC này.</summary>
    private void OnQuestCompleted(Quest quest)
    {
        if (_pendingQuest == quest || quest.Info.questGiver?.npcId == npcId)
        {
            _pendingQuest = null;
            _isQuestGiverReady = false;
            SetIndicators(available: false, active: false);
        }
    }

    // ── Private Helpers ────────────────────────────────────────────────────────

    private void CheckIfQuestGiver(Quest quest)
    {
        // NPC này là quest giver của quest này không?
        if (quest.Info.questGiver?.npcId != npcId) return;

        _pendingQuest = quest;
        _isQuestGiverReady = true;
        SetIndicators(available: true, active: false);
        OnBecomeQuestGiver?.Invoke();

        Debug.Log($"[NpcQuestInteractor:{npcId}] Trở thành quest giver cho: '{quest.Info.displayName}'");
    }

    private void SetIndicators(bool available, bool active)
    {
        if (questAvailableIndicator != null)
            questAvailableIndicator.SetActive(available);

        if (questActiveIndicator != null)
            questActiveIndicator.SetActive(active && !available);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(npcId))
            Debug.LogWarning($"[NpcQuestInteractor] GameObject '{gameObject.name}': npcId đang trống!", this);
    }
#endif
}
