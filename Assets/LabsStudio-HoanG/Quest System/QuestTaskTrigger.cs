// File: QuestTaskTrigger.cs
// Trách nhiệm (SRP): Component đa dụng dùng để tăng tiến trình (progress) của 1 Task.
//   • Có thể gắn vào vùng trigger (Collider), Enemy, hoặc Item pickup.
//   • Chỉ trigger nếu thỏa mãn điều kiện: Quest đang Active và Task hiện tại khớp với mục tiêu.
//
// CÁCH DÙNG:
//   - Để trong Enemy: Gọi TriggerTask() khi Enemy chết (qua UnityEvent hoặc Code).
//   - Để trong Item: Gọi TriggerTask() khi nhặt item.
//   - Trigger Zone: Kéo hàm TriggerTask() vào sự kiện OnTriggerEnter.

using UnityEngine;
using UnityEngine.Events;

public class QuestTaskTrigger : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Số lượng progress cộng thêm vào task khi kích hoạt trigger này.")]
    public int progressAmount = 1;

    [Tooltip(
        "Nếu check, trigger này sẽ tự hủy (hoặc vô hiệu hóa) sau khi kích hoạt thành công 1 lần.\n" +
        "Ví dụ: nhặt đồ xong thì biến mất.")]
    public bool disableAfterTrigger = true;

    [Header("Events")]
    [Tooltip("Bắn ra khi kích hoạt thành công (dùng để play sound, sinh VFX, xoá Object...).")]
    public UnityEvent OnTriggerSuccess;

    [Tooltip("Bắn ra khi kích hoạt thất bại (vì chưa có task nào yêu cầu).")]
    public UnityEvent OnTriggerFailed;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Gọi hàm này từ bên ngoài (khi quái chết, khi nhặt đồ, khi chạm vùng...).
    /// Nó sẽ +progressAmount vào task đang active.
    /// </summary>
    public void TriggerTask()
    {
        Quest activeQuest = QuestManager.Instance?.GetActiveQuest();

        if (activeQuest == null || activeQuest.CurrentTask == null)
        {
            Debug.Log($"[QuestTaskTrigger] Hụt: Không có Quest/Task nào đang Active.");
            OnTriggerFailed?.Invoke();
            return;
        }

        Debug.Log($"[QuestTaskTrigger] Thành công: Tăng tiến độ cho task '{activeQuest.CurrentTask.Info.displayName}'.");
        
        // Cộng progress
        QuestManager.Instance.ProgressCurrentTask(progressAmount);
        OnTriggerSuccess?.Invoke();

        if (disableAfterTrigger)
        {
            gameObject.SetActive(false);
        }
    }
}
