using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// Script trung gian điều phối chuỗi hành động 3 giai đoạn từ CleanButton:
///
///   [Giai đoạn 1 - Chưa đăng nhập]
///     → Nhấn nút: Hiển thị AuthScreen (UIDocument), ẩn nút
///
///   [Giai đoạn 2 - AuthScreen đang mở]
///     → Đăng nhập thành công: Tắt AuthScreen, hiện lại nút
///
///   [Giai đoạn 3 - Đã đăng nhập]
///     → Nhấn nút: Hiện UI loading, load scene mới bất đồng bộ
///
/// Cách gắn vào Inspector:
///   1. Gắn script này lên một GameObject (ví dụ: "AuthEntryManager") trong scene.
///   2. Điền các tham chiếu bên dưới trong Inspector.
///   3. Trên CleanButton: Inspector > OnClick > kéo AuthEntryManager vào, chọn OnButtonClicked().
/// </summary>
public class AuthEntryController : MonoBehaviour
{
    // ─── THAM CHIẾU CẦN GÁN TRONG INSPECTOR ───────────────────────────────

    [Header("Auth Screen (UI Toolkit)")]
    [Tooltip("UIDocument chứa AuthScreen.uxml — thường gắn trên GameObject 'AuthScreenPanel'")]
    [SerializeField] private UIDocument authScreenDocument;

    [Tooltip("AuthScreenController gắn trên cùng GameObject với UIDocument trên")]
    [SerializeField] private AuthScreenController authScreenController;

    [Header("Nút bấm UGUI (CleanButton)")]
    [Tooltip("GameObject chứa CleanButton cần ẩn khi AuthScreen mở")]
    [SerializeField] private GameObject entryButtonObject;

    [Header("UI Loading Scene (tuỳ chọn)")]
    [Tooltip("CanvasGroup của màn hình loading — để trống nếu chưa có (sẽ tự skip)")]
    [SerializeField] private CanvasGroup loadingCanvasGroup;

    [Tooltip("Thời gian fade in/out loading UI (giây)")]
    [SerializeField] private float loadingFadeDuration = 0.4f;

    [Header("Cấu hình Scene")]
    [Tooltip("Tên chính xác của scene cần load sau khi đăng nhập thành công (phải có trong Build Settings)")]
    [SerializeField] private string targetSceneName = "<Nhập tên scene>";

    // ─── TRẠNG THÁI NỘI BỘ ────────────────────────────────────────────────

    private enum AuthState
    {
        Idle,          // Chưa đăng nhập, hiển thị nút
        ShowingAuth,   // AuthScreen đang mở, nút ẩn
        LoggedIn,      // Đã đăng nhập xong, hiển thị nút
        LoadingScene   // Đang load scene, không cho tương tác
    }

    private AuthState _state = AuthState.Idle;

    // ─── UNITY LIFECYCLE ──────────────────────────────────────────────────

    private void Awake()
    {
        // Đảm bảo AuthScreen bắt đầu bị ẩn
        SetAuthScreenVisible(false);

        // Đảm bảo loading UI bắt đầu bị ẩn
        if (loadingCanvasGroup != null)
        {
            loadingCanvasGroup.alpha = 0f;
            loadingCanvasGroup.interactable = false;
            loadingCanvasGroup.blocksRaycasts = false;
        }
    }

    private void OnEnable()
    {
        // Đăng ký lắng nghe event đăng nhập thành công từ AuthScreenController
        if (authScreenController != null)
        {
            authScreenController.OnLoginSuccess += HandleLoginSuccess;
        }
        else
        {
            Debug.LogWarning("[AuthEntryController] Chưa gán AuthScreenController vào Inspector!");
        }
    }

    private void OnDisable()
    {
        // Hủy đăng ký để tránh memory leak
        if (authScreenController != null)
        {
            authScreenController.OnLoginSuccess -= HandleLoginSuccess;
        }
    }

    // ─── ĐIỂM VÀO: GỌI TỪ CLEANBUTTON (Inspector → OnClick) ─────────────

    /// <summary>
    /// Hàm public này được gọi từ Inspector > OnClick của CleanButton.
    /// Hành động phụ thuộc vào trạng thái hiện tại.
    /// </summary>
    public void OnButtonClicked()
    {
        switch (_state)
        {
            case AuthState.Idle:
                // Chưa đăng nhập → mở AuthScreen
                OpenAuthScreen();
                break;

            case AuthState.LoggedIn:
                // Đã đăng nhập → bắt đầu load scene
                StartCoroutine(LoadSceneRoutine());
                break;

            case AuthState.ShowingAuth:
            case AuthState.LoadingScene:
                // Đang trong quá trình xử lý → bỏ qua click
                break;
        }
    }

    // ─── GIAI ĐOẠN 1: MỞ AUTH SCREEN ─────────────────────────────────────

    private void OpenAuthScreen()
    {
        _state = AuthState.ShowingAuth;

        // Ẩn nút bấm
        SetEntryButtonVisible(false);

        // Hiện AuthScreen
        SetAuthScreenVisible(true);
    }

    // ─── GIAI ĐOẠN 2: XỬ LÝ SAU KHI ĐĂNG NHẬP THÀNH CÔNG ───────────────

    /// <summary>
    /// Callback được AuthScreenController gọi qua event OnLoginSuccess.
    /// </summary>
    private void HandleLoginSuccess()
    {
        _state = AuthState.LoggedIn;

        // Tắt AuthScreen
        SetAuthScreenVisible(false);

        // Hiện lại nút bấm
        SetEntryButtonVisible(true);
    }

    // ─── GIAI ĐOẠN 3: LOAD SCENE ──────────────────────────────────────────

    private IEnumerator LoadSceneRoutine()
    {
        _state = AuthState.LoadingScene;

        // Ẩn nút bấm
        SetEntryButtonVisible(false);

        // Fade in Loading UI
        yield return StartCoroutine(FadeLoadingUI(1f));

        // Bắt đầu load scene bất đồng bộ
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(targetSceneName);
        if (asyncLoad == null)
        {
            Debug.LogError($"[AuthEntryController] Không tìm thấy scene '{targetSceneName}'. " +
                           "Kiểm tra lại tên scene và Build Settings.");

            // Rollback: quay về trạng thái đã đăng nhập
            yield return StartCoroutine(FadeLoadingUI(0f));
            SetEntryButtonVisible(true);
            _state = AuthState.LoggedIn;
            yield break;
        }

        // Chờ scene load xong (giữ ở 90% để không tự switch scene)
        asyncLoad.allowSceneActivation = false;

        while (asyncLoad.progress < 0.9f)
        {
            yield return null;
        }

        // Scene đã sẵn sàng → kích hoạt chuyển scene
        asyncLoad.allowSceneActivation = true;

        // Chờ thêm 1 frame để scene activate hoàn toàn
        yield return null;

        // Fade out Loading UI (chỉ cần thiết nếu scene mới vẫn dùng chung canvas)
        // Nếu scene mới hoàn toàn mới thì dòng này có thể không chạy nữa — không sao.
        yield return StartCoroutine(FadeLoadingUI(0f));
    }

    // ─── HELPERS ──────────────────────────────────────────────────────────

    private void SetAuthScreenVisible(bool visible)
    {
        if (authScreenDocument == null)
        {
            Debug.LogWarning("[AuthEntryController] Chưa gán authScreenDocument vào Inspector!");
            return;
        }

        // Bật/tắt UIDocument sẽ kích hoạt OnEnable/OnDisable trong AuthScreenController
        authScreenDocument.gameObject.SetActive(visible);
    }

    private void SetEntryButtonVisible(bool visible)
    {
        if (entryButtonObject != null)
        {
            entryButtonObject.SetActive(visible);
        }
        else
        {
            Debug.LogWarning("[AuthEntryController] Chưa gán entryButtonObject vào Inspector!");
        }
    }

    private IEnumerator FadeLoadingUI(float targetAlpha)
    {
        if (loadingCanvasGroup == null)
        {
            // Không có loading UI → skip ngay
            yield break;
        }

        float startAlpha = loadingCanvasGroup.alpha;
        float elapsed = 0f;

        // Bật/tắt block raycasts ngay lập tức
        bool isFadingIn = targetAlpha > startAlpha;
        if (isFadingIn)
        {
            loadingCanvasGroup.blocksRaycasts = true;
        }

        while (elapsed < loadingFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            loadingCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / loadingFadeDuration);
            yield return null;
        }

        loadingCanvasGroup.alpha = targetAlpha;
        loadingCanvasGroup.interactable = isFadingIn;
        loadingCanvasGroup.blocksRaycasts = isFadingIn;
    }
}
