using UnityEngine;

// ========================================================
// TRIGGER KÍCH HOẠT NHIỆM VỤ (MISSION CHECK)
// Gắn vào các Object/Trigger trong Map.
// Khi điều kiện gameplay thỏa mãn, báo lên MissionManager.
// ========================================================
public class MissionCheck : MonoBehaviour
{
    // [TC1] Định danh bền vững bằng string ID thay vì int index.
    // Phải khớp chính xác với missionId trong danh sách của MissionManager.
    [SerializeField] private string missionId;

    private bool _isMissionComplete;
    [HideInInspector] public bool isMissionComplete
    {
        get { return _isMissionComplete; }
        set
        {
            // Chỉ kích hoạt một lần duy nhất khi chuyển từ false → true
            if (value == true && !_isMissionComplete)
            {
                _isMissionComplete = true;
                Debug.Log($"<color=lime>[MissionCheck]</color> ★ Trigger kích hoạt trên \"{gameObject.name}\" | " +
                          $"missionId=\"{missionId}\" | isMissionComplete: false → true");
                SendMissionComplete();
            }
        }
    }

    [SerializeField] private bool isSceneComplete = false;
    [SerializeField] private string nextSceneName;

    // ========================================================
    // [TC4] TỰ KIỂM TRA TRẠNG THÁI KHI SCENE ĐƯỢC LOAD
    // Nếu nhiệm vụ này đã hoàn thành ở file save cũ,
    // tự động vô hiệu hóa Trigger để tránh người chơi lặp lại sự kiện.
    // ========================================================
    void Start()
    {
        if (MissionManager.instance != null &&
            MissionManager.instance.IsMissionCompleted(missionId))
        {
            _isMissionComplete = true;
            Debug.Log($"<color=gray>[MissionCheck]</color> Start() trên \"{gameObject.name}\" | " +
                      $"missionId=\"{missionId}\" đã hoàn thành ở save cũ → SetActive(false)");
            // Vô hiệu hóa hoàn toàn GameObject chứa Trigger này
            gameObject.SetActive(false);
        }
        else
        {
            Debug.Log($"<color=cyan>[MissionCheck]</color> Start() trên \"{gameObject.name}\" | " +
                      $"missionId=\"{missionId}\" chưa hoàn thành → Trigger sẵn sàng");
        }
    }

    /// <summary>
    /// [TC1] Gửi thông báo hoàn thành lên MissionManager bằng string ID.
    /// </summary>
    private void SendMissionComplete()
    {
        Debug.Log($"<color=yellow>[MissionCheck]</color> Đang gửi CompleteMission(\"{missionId}\") lên MissionManager | " +
                  $"isSceneComplete={isSceneComplete} | nextScene=\"{nextSceneName}\"");
        MissionManager.instance.CompleteMission(missionId, isSceneComplete, nextSceneName);
    }
}
