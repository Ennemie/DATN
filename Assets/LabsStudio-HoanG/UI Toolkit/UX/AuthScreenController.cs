using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement; // Dùng nếu bạn muốn chuyển Scene sau khi đăng nhập thành công

[RequireComponent(typeof(UIDocument))]
public class AuthScreenController : MonoBehaviour
{
    private UIDocument _uiDocument;
    private VisualElement _root;

    // Các khối Panel Form chính
    private VisualElement _loginForm;
    private VisualElement _registerForm;

    // Các Nút bấm (Buttons)
    private Button _btnLogin;
    private Button _btnRegister;
    private Button _linkToRegister;
    private Button _linkToLogin;

    // Các Ô nhập liệu (TextFields) để lấy text người chơi gõ
    private TextField _loginUsernameField;
    private TextField _loginPasswordField;
    private TextField _regUsernameField;
    private TextField _regEmailField;
    private TextField _regPasswordField;
    private TextField _regConfirmField;

    private const string HideFormClass = "hide-form";

    void OnEnable()
    {
        // 1. Khởi tạo và lấy thành phần Gốc (Root) của UI Toolkit
        _uiDocument = GetComponent<UIDocument>();
        _root = _uiDocument.rootVisualElement;

        if (_root == null)
        {
            Debug.LogError("[SYSTEM REBOOT]: Không tìm thấy Root VisualElement!");
            return;
        }

        // 2. Tìm kiếm các thành phần Panel theo đúng Tên (Name) trong UXML
        _loginForm = _root.Q<VisualElement>("LoginForm");
        _registerForm = _root.Q<VisualElement>("RegisterForm");

        // 3. Tìm kiếm các Nút bấm tương tác
        _btnLogin = _root.Q<Button>("BtnLogin");
        _btnRegister = _root.Q<Button>("BtnRegister");
        _linkToRegister = _root.Q<Button>("LinkToRegister");
        _linkToLogin = _root.Q<Button>("LinkToLogin");

        // 4. Tìm kiếm các trường nhập liệu dữ liệu hệ thống
        _loginUsernameField = _root.Q<TextField>("LoginUsername");
        _loginPasswordField = _root.Q<TextField>("LoginPassword");
        _regUsernameField = _root.Q<TextField>("RegUsername");
        _regEmailField = _root.Q<TextField>("RegEmail");
        _regPasswordField = _root.Q<TextField>("RegPassword");
        _regConfirmField = _root.Q<TextField>("RegConfirmPassword");

        // 5. Đăng ký sự kiện Click cho nút bấm chuyển đổi qua lại giữa các màn hình
        if (_linkToRegister != null) _linkToRegister.clicked += SwitchToRegister;
        if (_linkToLogin != null) _linkToLogin.clicked += SwitchToLogin;

        // 6. Đăng ký sự kiện Xử lý Dữ liệu khi người dùng ấn Xác nhận Form
        if (_btnLogin != null) _btnLogin.clicked += ProcessLogin;
        if (_btnRegister != null) _btnRegister.clicked += ProcessRegister;

        Debug.Log("[SYSTEM ONLINE]: Hệ thống xác thực Cyberpunk sẵn sàng.");
    }

    void OnDisable()
    {
        // Giải phóng và hủy đăng ký sự kiện để tối ưu hóa bộ nhớ cho RAM
        if (_linkToRegister != null) _linkToRegister.clicked -= SwitchToRegister;
        if (_linkToLogin != null) _linkToLogin.clicked -= SwitchToLogin;
        if (_btnLogin != null) _btnLogin.clicked -= ProcessLogin;
        if (_btnRegister != null) _btnRegister.clicked -= ProcessRegister;
    }

    // ================= CHỨC NĂNG ẨN/HIỆN PHÂN THÂN FORM =================

    private void SwitchToRegister()
    {
        // Thêm class hide-form vào Login để ẩn nó, và xóa khỏi Register để hiện nó lên
        _loginForm?.AddToClassList(HideFormClass);
        _registerForm?.RemoveFromClassList(HideFormClass);
        Debug.Log("[SYSTEM UPDATE]: Chuyển hướng sang cổng tạo IDENTITY mới.");
    }

    private void SwitchToLogin()
    {
        // Ngược lại, ẩn Register và đưa Login hiển thị trở lại
        _registerForm?.AddToClassList(HideFormClass);
        _loginForm?.RemoveFromClassList(HideFormClass);
        Debug.Log("[SYSTEM UPDATE]: Quay về cổng xác thực AUTHENTICATION.");
    }

    // ================= LOGIC XỬ LÝ DỮ LIỆU ĐĂNG NHẬP =================

    private void ProcessLogin()
    {
        string username = _loginUsernameField?.value?.Trim();
        string password = _loginPasswordField?.value;

        // Kiểm tra lỗi để trống trường nhập liệu
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            Debug.LogError("[SYSTEM ERROR]: IDENTITY hoặc ACCESS KEY không được để trống!");
            return;
        }

        Debug.Log($"[SYSTEM ACCESS]: Gửi yêu cầu kiểm tra tài khoản: {username} lên Database...");

        // TẠI ĐÂY: Bạn kết nối code này với API Server (Firebase, PlayFab, Node.js...) hoặc kiểm tra cứng
        bool isSuccess = true; // Giả lập đăng nhập thành công

        if (isSuccess)
        {
            Debug.Log("[ACCESS GRANTED]: Đăng nhập thành công! Đang tải Scene tiếp theo...");
            // Ví dụ chuyển sang Scene màn hình chính của game:
            // SceneManager.LoadScene("MainMenuPlayGame");
        }
        else
        {
            Debug.LogError("[SYSTEM ERROR]: Khóa truy cập ACCESS KEY hoặc IDENTITY sai!");
        }
    }

    // ================= LOGIC XỬ LÝ DỮ LIỆU ĐĂNG KÝ =================

    private void ProcessRegister()
    {
        string uName = _regUsernameField?.value?.Trim();
        string email = _regEmailField?.value?.Trim();
        string pass = _regPasswordField?.value;
        string confirmPass = _regConfirmField?.value;

        // 1. Kiểm tra điền thiếu thông tin
        if (string.IsNullOrEmpty(uName) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(pass))
        {
            Debug.LogError("[SYSTEM ERROR]: Vui lòng điền đầy đủ tất cả các trường thông tin!");
            return;
        }

        // 2. Kiểm tra mật khẩu nhập lại có khớp không
        if (pass != confirmPass)
        {
            Debug.LogError("[SYSTEM ERROR]: Xác nhận mật khẩu không trùng khớp!");
            return;
        }

        Debug.Log($"[SYSTEM ACCESS]: Tiến hành nạp dữ liệu Đăng ký mới. User: {uName}, Email: {email}");

        // TẠI ĐÂY: Viết logic đẩy dữ liệu đăng ký này lên Server của bạn.
        Debug.Log("[DATABASE UPDATE]: Tạo Identity mới thành công! Bạn có thể quay lại Đăng nhập.");
        SwitchToLogin(); // Đăng ký xong tự động quay lại màn hình đăng nhập
    }
}