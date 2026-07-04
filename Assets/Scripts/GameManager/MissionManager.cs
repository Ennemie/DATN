using System.Collections.Generic;
using UnityEngine;

// ========================================================
// TRUNG TÂM ĐIỀU PHỐI NHIỆM VỤ (MISSION MANAGER)
// Kế thừa ISaveable để tham gia hệ thống Save/Load lõi.
// Quản lý nhiệm vụ bằng string ID bền vững, đồng bộ SQL.
// ========================================================
public class MissionManager : MonoBehaviour, ISaveable
{
    public static MissionManager instance { get; private set; }

    [SerializeField] private BoxCollider nextSceneCollider;
    [HideInInspector] public string nextSceneName;

    // --- DANH SÁCH NHIỆM VỤ (Cấu hình trong Inspector) ---
    [SerializeField] private List<Mission> missions;

    // [TC1] ID nhiệm vụ hiện tại thay vì index số nguyên bấp bênh.
    // Được khôi phục từ save hoặc gán mặc định trong RestoreState.
    private string currentMissionId;

    // ========================================================
    // VÒNG ĐỜI UNITY
    // ========================================================
    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            Debug.Log("<color=cyan>[MissionManager]</color> Singleton khởi tạo thành công. " +
                       $"Tổng số nhiệm vụ trong Inspector: {missions.Count}");
        }
        else
        {
            Debug.LogWarning("<color=yellow>[MissionManager]</color> Phát hiện bản trùng lặp → Destroy(gameObject)");
            Destroy(gameObject);
            return;
        }
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        // [TC3] KHÔNG reset cứng currentMissionId ở đây.
        // Dữ liệu đã được RestoreState nạp trước đó (SaveManager chạy trước nhờ ExecutionOrder).
        // Chỉ xử lý logic UI và Collider an toàn dựa trên trạng thái đã có.
        nextSceneCollider.enabled = false;
        Debug.Log("<color=cyan>[MissionManager]</color> Start() hoàn tất. " +
                  $"currentMissionId = \"{currentMissionId}\" | nextSceneCollider = disabled");
    }

    // ========================================================
    // [TC2] ĐĂNG KÝ VÀO HỆ THỐNG SAVE/LOAD
    // SaveManager.Instance được gán [DefaultExecutionOrder(-100)]
    // nên luôn sẵn sàng trước khi OnEnable của script này chạy.
    // ========================================================
    void OnEnable()
    {
        SaveManager.Instance.RegisterSaveable(this);
        Debug.Log("<color=cyan>[MissionManager]</color> OnEnable → Đã đăng ký vào SaveManager (RegisterSaveable)");
    }

    void OnDisable()
    {
        SaveManager.Instance.UnregisterSaveable(this);
        Debug.Log("<color=gray>[MissionManager]</color> OnDisable → Đã hủy đăng ký khỏi SaveManager (UnregisterSaveable)");
    }

    // ========================================================
    // [TC2] ĐÓNG GÓI DỮ LIỆU TỪ RAM VÀO DTO ĐỂ LƯU
    // ========================================================
    public void CaptureState(GameSaveDTO currentSave)
    {
        Debug.Log("<color=yellow>[MissionManager]</color> ▶ CaptureState BẮT ĐẦU — Đang đổ dữ liệu RAM → DTO...");

        // --- Lưu tiến trình nhiệm vụ hiện tại ---
        string oldChapter = currentSave.progress.currentChapter;
        currentSave.progress.currentChapter = currentMissionId;
        Debug.Log($"<color=yellow>[MissionManager]</color>   ├─ progress.currentChapter: \"{oldChapter}\" → \"{currentMissionId}\"");

        // --- Lưu danh sách trạng thái từng nhiệm vụ ---
        // QUAN TRỌNG: Clear khuôn cũ trước khi đổ dữ liệu mới,
        // tránh dữ liệu bị trùng lặp nếu Save được gọi nhiều lần.
        int oldCount = currentSave.missions.Count;
        currentSave.missions.Clear();

        foreach (Mission m in missions)
        {
            currentSave.missions.Add(new MissionData
            {
                missionId     = m.missionId,
                mainClear     = m.isCompleted,
                stealthBonus  = m.stealthBonus,
                allIntelFound = m.allIntelFound
            });
            Debug.Log($"<color=yellow>[MissionManager]</color>   ├─ Ghi nhiệm vụ: \"{m.missionId}\" | " +
                      $"mainClear={m.isCompleted} | stealth={m.stealthBonus} | intel={m.allIntelFound}");
        }

        Debug.Log($"<color=yellow>[MissionManager]</color>   └─ missions.Count: {oldCount} → {currentSave.missions.Count}");
        Debug.Log("<color=yellow>[MissionManager]</color> ■ CaptureState HOÀN TẤT");
    }

    // ========================================================
    // [TC2] KHÔI PHỤC TRẠNG THÁI TỪ DTO VỀ RAM
    // ========================================================
    public void RestoreState(GameSaveDTO currentSave)
    {
        Debug.Log("<color=cyan>[MissionManager]</color> ▶ RestoreState BẮT ĐẦU — Đang nạp dữ liệu DTO → RAM...");

        // --- Khôi phục ID nhiệm vụ hiện tại ---
        string oldMissionId = currentMissionId;

        // [TC3] Nếu save rỗng (game mới), gán mặc định là nhiệm vụ đầu tiên.
        if (!string.IsNullOrEmpty(currentSave.progress.currentChapter))
        {
            currentMissionId = currentSave.progress.currentChapter;
            Debug.Log($"<color=cyan>[MissionManager]</color>   ├─ currentMissionId: \"{oldMissionId}\" → \"{currentMissionId}\" (từ save)");
        }
        else
        {
            // Game mới: chưa có save, gán nhiệm vụ đầu tiên trong danh sách.
            if (missions.Count > 0)
                currentMissionId = missions[0].missionId;
            Debug.Log($"<color=cyan>[MissionManager]</color>   ├─ currentMissionId: \"{oldMissionId}\" → \"{currentMissionId}\" (game mới, gán mặc định)");
        }

        // --- Khôi phục trạng thái từng nhiệm vụ ---
        // [TC2] Dùng .Find() để map theo ID, bất chấp thứ tự Inspector bị xáo trộn.
        Debug.Log($"<color=cyan>[MissionManager]</color>   ├─ Số nhiệm vụ trong DTO cần khôi phục: {currentSave.missions.Count}");

        foreach (MissionData data in currentSave.missions)
        {
            Mission target = missions.Find(m => m.missionId == data.missionId);
            if (target != null)
            {
                bool oldCompleted = target.isCompleted;
                bool oldStealth = target.stealthBonus;
                bool oldIntel = target.allIntelFound;

                target.isCompleted    = data.mainClear;
                target.stealthBonus   = data.stealthBonus;
                target.allIntelFound  = data.allIntelFound;

                Debug.Log($"<color=cyan>[MissionManager]</color>   ├─ Khôi phục \"{data.missionId}\": " +
                          $"mainClear: {oldCompleted}→{data.mainClear} | " +
                          $"stealth: {oldStealth}→{data.stealthBonus} | " +
                          $"intel: {oldIntel}→{data.allIntelFound}");
            }
            else
            {
                Debug.LogWarning($"<color=yellow>[MissionManager]</color>   ├─ ⚠ Không tìm thấy mission \"{data.missionId}\" trong Inspector! " +
                                  "Có thể đã bị xóa hoặc đổi tên ID.");
            }
        }

        Debug.Log("<color=cyan>[MissionManager]</color> ■ RestoreState HOÀN TẤT");
    }

    // ========================================================
    // API CÔNG KHAI – TRUY VẤN NHIỆM VỤ
    // ========================================================

    /// <summary>
    /// [TC4] API cho các Trigger/Object ngoài Map tra cứu trạng thái nhiệm vụ.
    /// Trả về true nếu nhiệm vụ có ID này đã hoàn thành.
    /// </summary>
    public bool IsMissionCompleted(string missionId)
    {
        Mission m = missions.Find(x => x.missionId == missionId);
        return m != null && m.isCompleted;
    }

    /// <summary>
    /// Hiển thị UI nhiệm vụ hiện tại nếu chưa hoàn thành.
    /// </summary>
    public void ShowCurrentMission()
    {
        Mission current = GetCurrentMission();
        if (current == null || current.isCompleted) return;
        GameCanvas.Instance.ShowMissionBox(true);
    }

    /// <summary>
    /// Lấy tiêu đề nhiệm vụ hiện tại.
    /// </summary>
    public string GetTitle()
    {
        Mission current = GetCurrentMission();
        return current != null ? current.title : "";
    }

    // ========================================================
    // API CÔNG KHAI – HOÀN THÀNH & CHUYỂN NHIỆM VỤ
    // ========================================================

    /// <summary>
    /// [TC1] Hoàn thành nhiệm vụ theo string ID thay vì int index.
    /// Được gọi từ MissionCheck khi Trigger kích hoạt.
    /// </summary>
    public void CompleteMission(string missionId, bool isSceneComplete, string _nextSceneName)
    {
        // Tìm nhiệm vụ bằng ID bền vững
        Mission target = missions.Find(m => m.missionId == missionId);
        if (target == null)
        {
            Debug.LogError($"<color=red>[MissionManager]</color> CompleteMission THẤT BẠI: " +
                           $"Không tìm thấy mission có ID \"{missionId}\" trong danh sách!");
            return;
        }
        if (target.isCompleted)
        {
            Debug.Log($"<color=gray>[MissionManager]</color> CompleteMission bỏ qua: " +
                      $"\"{missionId}\" đã hoàn thành từ trước.");
            return;
        }

        // Chỉ cho phép hoàn thành nhiệm vụ đang active (tuần tự)
        if (missionId != currentMissionId)
        {
            Debug.LogWarning($"<color=yellow>[MissionManager]</color> CompleteMission bỏ qua: " +
                             $"\"{missionId}\" không phải nhiệm vụ hiện tại (đang active: \"{currentMissionId}\")");
            return;
        }

        target.isCompleted = true;
        Debug.Log($"<color=lime>[MissionManager]</color> ★ HOÀN THÀNH NHIỆM VỤ: \"{missionId}\" | " +
                  $"isCompleted: false → true | isSceneComplete={isSceneComplete}");

        StartCoroutine(GameCanvas.Instance.ShowNextMission());

        if (isSceneComplete)
        {
            nextSceneCollider.enabled = true;
            nextSceneName = _nextSceneName;
            Debug.Log($"<color=lime>[MissionManager]</color>   └─ Mở khóa chuyển scene → \"{_nextSceneName}\" | " +
                      "nextSceneCollider = enabled");
        }
    }

    /// <summary>
    /// Chuyển sang nhiệm vụ kế tiếp trong danh sách.
    /// </summary>
    public void AssignNextMission()
    {
        // Tìm vị trí hiện tại trong danh sách để xác định phần tử kế tiếp
        int currentIndex = missions.FindIndex(m => m.missionId == currentMissionId);
        if (currentIndex >= 0 && currentIndex < missions.Count - 1)
        {
            string oldId = currentMissionId;
            currentMissionId = missions[currentIndex + 1].missionId;
            Debug.Log($"<color=lime>[MissionManager]</color> ★ CHUYỂN NHIỆM VỤ: " +
                      $"\"{oldId}\" → \"{currentMissionId}\"");
            GameCanvas.Instance.ShowMissionBox(true);
        }
        else
        {
            Debug.Log($"<color=gray>[MissionManager]</color> AssignNextMission: " +
                      $"Đã ở nhiệm vụ cuối cùng \"{currentMissionId}\", không còn nhiệm vụ kế tiếp.");
        }
    }

    // ========================================================
    // HÀM NỘI BỘ
    // ========================================================

    /// <summary>
    /// Tìm Mission object trong RAM theo currentMissionId.
    /// </summary>
    private Mission GetCurrentMission()
    {
        return missions.Find(m => m.missionId == currentMissionId);
    }

    // ========================================================
    // CẤU TRÚC DỮ LIỆU NHIỆM VỤ (Inspector-friendly)
    // ========================================================
    [System.Serializable]
    public class Mission
    {
        // [TC1] Định danh bền vững – dùng để map với MissionData.missionId trong DTO/SQL.
        // Ví dụ: "mission_hack_server", "mission_escape_lab"
        public string missionId;

        public string title;
        [HideInInspector] public bool isCompleted = false;

        // Các trường phụ map đầy đủ với MissionData trong DTO
        [HideInInspector] public bool stealthBonus = false;
        [HideInInspector] public bool allIntelFound = false;

        public GameObject target;
    }
}
