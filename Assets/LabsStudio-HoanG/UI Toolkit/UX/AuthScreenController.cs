using System;
using System.Collections;
using Firebase.Auth;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class AuthScreenController : MonoBehaviour
{
    private UIDocument _uiDocument;
    private VisualElement _root;

    // ================= KHỐI PANEL FORMS =================
    private VisualElement _loginForm;
    private VisualElement _registerForm;
    private VisualElement _forgotForm;
    private VisualElement _verifyEmailForm;
    private VisualElement _successForm;
    private VisualElement _errorBanner;
    private VisualElement _loadingOverlay;

    // ================= Ô NHẬP LIỆU (TEXTFIELDS) =================
    private TextField _loginUsernameField, _loginPasswordField;
    private TextField _regUsernameField, _regEmailField, _regPasswordField, _regConfirmField;
    private TextField _forgotEmailField;

    // ================= TOGGLE =================
    private Toggle _toggleRememberMe;
    private Toggle _toggleToS;

    // ================= NÚT TOGGLE MẬT KHẨU =================
    private Button _btnToggleLoginPassword;
    private Button _btnToggleRegPassword;
    private Button _btnToggleRegConfirmPassword;
    private Button _btnSteamAuth;
    private Button _btnGoogleAuth;

    // ================= VĂN BẢN ĐỘNG (LABELS) =================
    private Label _successMessageLabel;
    private Label _inlineErrorLabel;

    // ================= NÚT BẤM (BUTTONS) =================
    private Button _btnLogin, _btnRegister, _btnSendCode;
    private Button _btnSuccessConfirm;

    // --- Nút điều hướng Magic Link (Email Verification) ---
    private Button _btnCheckVerifiedLogin;
    private Button _btnResendVerifyEmail;

    private Button _linkToRegister, _linkToLogin, _linkToLoginFromForgot;
    private Label _linkToForgotPassword;

    // ================= HẰNG SỐ =================
    private const string HideFormClass = "hide-form";
    private const string RememberMeKey = "Auth.RememberMe.Username";
    private const string BootHiddenClass = "panel-boot-hidden";
    private const float ErrorAutoHideSeconds = 4f;

    // ================= EVENT ĐIỀU HƯỚNG =================
    /// <summary>
    /// Được bắn ra khi người dùng đăng nhập thành công VÀ email đã xác thực.
    /// AuthEntryController lắng nghe event này để tắt AuthScreen và hiện lại nút.
    /// </summary>
    public event Action OnLoginSuccess;

    // ================= FIREBASE =================
    private FirebaseAuth _auth;
    private FirebaseUser _user;

    private Coroutine _errorHideCoroutine;

    // ─────────────────────────────────────────────────────────────────────────
    // UNITY LIFECYCLE
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _auth = FirebaseAuth.DefaultInstance;
    }

    private void OnEnable()
    {
        _uiDocument = GetComponent<UIDocument>();
        _root = _uiDocument.rootVisualElement;

        if (_root == null) return;

        MapUIElements();
        BindPasswordToggles();
        RegisterInputChangeHandlers();
        RegisterEvents();

        LoadRememberedAccount();
        PlayBootAnimation(_loginForm);
        HideLoading();
        HideError();
    }

    private void OnDisable()
    {
        if (_errorHideCoroutine != null)
        {
            StopCoroutine(_errorHideCoroutine);
            _errorHideCoroutine = null;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ÁNH XẠ PHẦN TỬ UI
    // ─────────────────────────────────────────────────────────────────────────

    private void MapUIElements()
    {
        // 1. Panels
        _loginForm       = _root.Q<VisualElement>("LoginForm");
        _registerForm    = _root.Q<VisualElement>("RegisterForm");
        _forgotForm      = _root.Q<VisualElement>("ForgotPasswordForm");
        _verifyEmailForm = _root.Q<VisualElement>("VerifyEmailForm");
        _successForm     = _root.Q<VisualElement>("SuccessForm");
        _errorBanner     = _root.Q<VisualElement>("AuthErrorBanner");
        _loadingOverlay  = _root.Q<VisualElement>("LoadingOverlay");

        // 2. TextFields
        _loginUsernameField = _root.Q<TextField>("LoginUsername");
        _loginPasswordField = _root.Q<TextField>("LoginPassword");
        _regUsernameField   = _root.Q<TextField>("RegUsername");
        _regEmailField      = _root.Q<TextField>("RegEmail");
        _regPasswordField   = _root.Q<TextField>("RegPassword");
        _regConfirmField    = _root.Q<TextField>("RegConfirmPassword");
        _forgotEmailField   = _root.Q<TextField>("ForgotEmail");

        // 3. Toggles
        _toggleRememberMe = _root.Q<Toggle>("ToggleRememberMe");
        _toggleToS        = _root.Q<Toggle>("ToggleToS");

        // 4. Password eye buttons
        _btnToggleLoginPassword      = _root.Q<Button>("BtnToggleLoginPassword");
        _btnToggleRegPassword        = _root.Q<Button>("BtnToggleRegPassword");
        _btnToggleRegConfirmPassword = _root.Q<Button>("BtnToggleRegConfirmPassword");
        _btnSteamAuth                = _root.Q<Button>("BtnSteamAuth");
        _btnGoogleAuth               = _root.Q<Button>("BtnGoogleAuth");

        // 5. Labels
        _successMessageLabel = _root.Q<Label>("SuccessMessage");
        _inlineErrorLabel    = _root.Q<Label>("AuthErrorMessage");

        // 6. Action Buttons
        _btnLogin          = _root.Q<Button>("BtnLogin");
        _btnRegister       = _root.Q<Button>("BtnRegister");
        _btnSendCode       = _root.Q<Button>("BtnSendCode");
        _btnSuccessConfirm = _root.Q<Button>("BtnSuccessConfirm");

        // 7. Email Verification Buttons (Magic Link)
        _btnCheckVerifiedLogin = _root.Q<Button>("BtnCheckVerifiedLogin");
        _btnResendVerifyEmail  = _root.Q<Button>("BtnResendVerifyEmail");

        // 8. Navigation links
        _linkToRegister       = _root.Q<Button>("LinkToRegister");
        _linkToLogin          = _root.Q<Button>("LinkToLogin");
        _linkToForgotPassword = _root.Q<Label>("LinkToForgotPassword");
        _linkToLoginFromForgot = _root.Q<Button>("LinkToLoginFromForgot");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ĐĂNG KÝ SỰ KIỆN
    // ─────────────────────────────────────────────────────────────────────────

    private void RegisterInputChangeHandlers()
    {
        RegisterClearErrorOnTyping(_loginUsernameField);
        RegisterClearErrorOnTyping(_loginPasswordField);
        RegisterClearErrorOnTyping(_regUsernameField);
        RegisterClearErrorOnTyping(_regEmailField);
        RegisterClearErrorOnTyping(_regPasswordField);
        RegisterClearErrorOnTyping(_regConfirmField);
        RegisterClearErrorOnTyping(_forgotEmailField);

        _toggleRememberMe?.RegisterValueChangedCallback(_ => HideError());
        _toggleToS?.RegisterValueChangedCallback(_ => HideError());
    }

    private void RegisterClearErrorOnTyping(TextField field)
    {
        field?.RegisterValueChangedCallback(_ => HideError());
    }

    private void RegisterEvents()
    {
        // Điều hướng form
        _linkToRegister?.RegisterCallback<ClickEvent>(_ => SwitchForm(_loginForm, _registerForm));
        _linkToLogin?.RegisterCallback<ClickEvent>(_ => SwitchForm(_registerForm, _loginForm));
        _linkToForgotPassword?.RegisterCallback<ClickEvent>(_ => SwitchForm(_loginForm, _forgotForm));
        _linkToLoginFromForgot?.RegisterCallback<ClickEvent>(_ => SwitchForm(_forgotForm, _loginForm));

        // Social auth (placeholder)
        _btnSteamAuth?.RegisterCallback<ClickEvent>(_ => ShowError("[ FAST AUTH ] Steam login chưa được nối backend."));
        _btnGoogleAuth?.RegisterCallback<ClickEvent>(_ => ShowError("[ FAST AUTH ] Google login chưa được nối backend."));

        // Action buttons
        _btnLogin?.RegisterCallback<ClickEvent>(_ => ProcessLogin());
        _btnRegister?.RegisterCallback<ClickEvent>(_ => ProcessRegister());
        _btnSendCode?.RegisterCallback<ClickEvent>(_ => ProcessForgotPassword());
        _btnSuccessConfirm?.RegisterCallback<ClickEvent>(_ => SwitchForm(_successForm, _loginForm));

        // --- Email Verification (Magic Link) ---
        // Nút "Đã kích hoạt, đăng nhập ngay": reload lại FirebaseUser rồi kiểm tra IsEmailVerified
        _btnCheckVerifiedLogin?.RegisterCallback<ClickEvent>(_ => StartCoroutine(CheckVerifiedAndLogin()));

        // Nút "Gửi lại email": gọi Firebase gửi lại verification email
        _btnResendVerifyEmail?.RegisterCallback<ClickEvent>(_ => StartCoroutine(ResendVerificationEmail()));
    }

    private void BindPasswordToggles()
    {
        BindPasswordToggle(_btnToggleLoginPassword, _loginPasswordField);
        BindPasswordToggle(_btnToggleRegPassword, _regPasswordField);
        BindPasswordToggle(_btnToggleRegConfirmPassword, _regConfirmField);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // XỬ LÝ ĐĂNG NHẬP (FIREBASE)
    // ─────────────────────────────────────────────────────────────────────────

    private void ProcessLogin()
    {
        string email    = _loginUsernameField?.value?.Trim();
        string password = _loginPasswordField?.value;

        if (string.IsNullOrEmpty(email))
        {
            ShowError("[ AUTH ERROR ] Vui lòng nhập email/tài khoản.");
            return;
        }

        if (string.IsNullOrEmpty(password))
        {
            ShowError("[ AUTH ERROR ] Vui lòng nhập mật khẩu.");
            return;
        }

        ShowLoading();
        StartCoroutine(LoginWithFirebase(email, password));
    }

    private IEnumerator LoginWithFirebase(string email, string password)
    {
        var loginTask = _auth.SignInWithEmailAndPasswordAsync(email, password);
        yield return new WaitUntil(() => loginTask.IsCompleted);

        if (loginTask.IsFaulted || loginTask.IsCanceled)
        {
            string errorMsg = loginTask.Exception?.GetBaseException().Message ?? "Lỗi không xác định.";
            ShowError($"[ AUTH ERROR ] {errorMsg}");
            yield break;
        }

        _user = loginTask.Result.User;

        // ── ANTI-BUG: Bắt buộc kiểm tra xác thực email trước khi cho vào game ──
        if (!_user.IsEmailVerified)
        {
            HideLoading();
            ShowError("[ AUTH BLOCKED ] Email chưa được xác thực. Kiểm tra hộp thư và nhấp vào liên kết kích hoạt.");
            SwitchForm(_loginForm, _verifyEmailForm);
            yield break;
        }

        // Email đã xác thực → đăng nhập thành công
        ApplyRememberMeAfterSuccessfulLogin(email);
        NotifyLoginSuccess();
    }

    /// <summary>
    /// Gọi hàm này khi Firebase xác nhận đăng nhập thành công VÀ email đã verified.
    /// Bắn event OnLoginSuccess để AuthEntryController xử lý tiếp (ẩn AuthScreen, load scene...).
    /// </summary>
    public void NotifyLoginSuccess()
    {
        HideLoading();
        HideError();
        OnLoginSuccess?.Invoke();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // XỬ LÝ ĐĂNG KÝ (FIREBASE)
    // ─────────────────────────────────────────────────────────────────────────

    private void ProcessRegister()
    {
        string username       = _regUsernameField?.value?.Trim();
        string email          = _regEmailField?.value?.Trim();
        string password       = _regPasswordField?.value;
        string confirmPassword = _regConfirmField?.value;

        if (string.IsNullOrEmpty(username))
        {
            ShowError("[ REGISTER ERROR ] Vui lòng nhập tên tài khoản.");
            return;
        }

        if (string.IsNullOrEmpty(email))
        {
            ShowError("[ REGISTER ERROR ] Vui lòng nhập email.");
            return;
        }

        if (string.IsNullOrEmpty(password))
        {
            ShowError("[ REGISTER ERROR ] Vui lòng nhập mật khẩu.");
            return;
        }

        if (password.Length < 6)
        {
            ShowError("[ REGISTER ERROR ] Mật khẩu phải có ít nhất 6 ký tự.");
            return;
        }

        if (password != confirmPassword)
        {
            ShowError("[ REGISTER ERROR ] Mật khẩu xác nhận không khớp.");
            return;
        }

        if (!email.Contains("@") || !email.Contains("."))
        {
            ShowError("[ REGISTER ERROR ] Địa chỉ email không hợp lệ.");
            return;
        }

        if (_toggleToS == null || !_toggleToS.value)
        {
            ShowError("Cảnh báo: Bạn phải đồng ý với Điều khoản dịch vụ để tạo danh tính mới.");
            return;
        }

        ShowLoading();
        StartCoroutine(RegisterWithFirebase(email, password, username));
    }

    private IEnumerator RegisterWithFirebase(string email, string password, string displayName)
    {
        var registerTask = _auth.CreateUserWithEmailAndPasswordAsync(email, password);
        yield return new WaitUntil(() => registerTask.IsCompleted);

        if (registerTask.IsFaulted || registerTask.IsCanceled)
        {
            string errorMsg = registerTask.Exception?.GetBaseException().Message ?? "Lỗi không xác định.";
            ShowError($"[ REGISTER ERROR ] {errorMsg}");
            yield break;
        }

        _user = registerTask.Result.User;

        // Cập nhật DisplayName
        var profile = new UserProfile { DisplayName = displayName };
        var updateTask = _user.UpdateUserProfileAsync(profile);
        yield return new WaitUntil(() => updateTask.IsCompleted);

        // Gửi email xác thực (Magic Link)
        var verifyTask = _user.SendEmailVerificationAsync();
        yield return new WaitUntil(() => verifyTask.IsCompleted);

        HideLoading();

        if (verifyTask.IsFaulted)
        {
            // Tài khoản đã tạo nhưng không gửi được email → vẫn chuyển sang VerifyEmailForm
            ShowError("[ WARNING ] Tài khoản đã tạo nhưng không thể gửi email xác thực. Thử gửi lại.");
        }

        // Chuyển sang màn hình chờ xác thực email
        SwitchForm(_registerForm, _verifyEmailForm);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // XỬ LÝ QUÊN MẬT KHẨU (FIREBASE PASSWORD RESET EMAIL)
    // ─────────────────────────────────────────────────────────────────────────

    private void ProcessForgotPassword()
    {
        string email = _forgotEmailField?.value?.Trim();
        if (string.IsNullOrEmpty(email))
        {
            ShowError("[ RECOVERY ERROR ] Vui lòng nhập email liên kết với tài khoản.");
            return;
        }

        ShowLoading();
        StartCoroutine(SendPasswordResetEmail(email));
    }

    private IEnumerator SendPasswordResetEmail(string email)
    {
        var resetTask = _auth.SendPasswordResetEmailAsync(email);
        yield return new WaitUntil(() => resetTask.IsCompleted);

        HideLoading();

        if (resetTask.IsFaulted || resetTask.IsCanceled)
        {
            string errorMsg = resetTask.Exception?.GetBaseException().Message ?? "Lỗi không xác định.";
            ShowError($"[ RECOVERY ERROR ] {errorMsg}");
            yield break;
        }

        // Thông báo thành công và quay về Login
        ShowSuccessScreen("Email khôi phục mật khẩu đã được gửi. Kiểm tra hộp thư và làm theo hướng dẫn để đặt lại mật khẩu.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // EMAIL VERIFICATION — MAGIC LINK
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Người chơi nhấn "ĐÃ KÍCH HOẠT, ĐĂNG NHẬP NGAY":
    /// Reload lại FirebaseUser để lấy trạng thái mới nhất từ server, sau đó kiểm tra IsEmailVerified.
    /// </summary>
    private IEnumerator CheckVerifiedAndLogin()
    {
        if (_user == null)
        {
            ShowError("[ AUTH ERROR ] Phiên làm việc đã hết hạn. Vui lòng đăng nhập lại.");
            SwitchForm(_verifyEmailForm, _loginForm);
            yield break;
        }

        ShowLoading();

        // Bắt buộc reload để lấy trạng thái xác thực mới nhất từ Firebase server
        var reloadTask = _user.ReloadAsync();
        yield return new WaitUntil(() => reloadTask.IsCompleted);

        if (reloadTask.IsFaulted)
        {
            HideLoading();
            ShowError("[ NETWORK ERROR ] Không thể kết nối server để xác nhận. Kiểm tra mạng và thử lại.");
            yield break;
        }

        if (_user.IsEmailVerified)
        {
            // Email đã xác thực → cho vào game
            ApplyRememberMeAfterSuccessfulLogin(_user.Email);
            NotifyLoginSuccess();
        }
        else
        {
            HideLoading();
            ShowError("[ VERIFY PENDING ] Email chưa được xác thực. Kiểm tra hộp thư (kể cả Spam) và nhấp vào liên kết.");
        }
    }

    /// <summary>
    /// Người chơi nhấn "GỬI LẠI EMAIL":
    /// Gọi Firebase gửi lại verification email, sau đó báo thành công.
    /// </summary>
    private IEnumerator ResendVerificationEmail()
    {
        if (_user == null)
        {
            ShowError("[ AUTH ERROR ] Phiên làm việc đã hết hạn. Vui lòng đăng nhập lại.");
            SwitchForm(_verifyEmailForm, _loginForm);
            yield break;
        }

        ShowLoading();

        var resendTask = _user.SendEmailVerificationAsync();
        yield return new WaitUntil(() => resendTask.IsCompleted);

        HideLoading();

        if (resendTask.IsFaulted || resendTask.IsCanceled)
        {
            string errorMsg = resendTask.Exception?.GetBaseException().Message ?? "Lỗi không xác định.";
            ShowError($"[ EMAIL ERROR ] {errorMsg}");
            yield break;
        }

        ShowSuccessScreen("Email xác thực đã được gửi lại thành công. Kiểm tra hộp thư (kể cả Spam) của bạn.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // HELPERS: HIỂN THỊ / ẨN UI
    // ─────────────────────────────────────────────────────────────────────────

    private void SwitchForm(VisualElement currentForm, VisualElement targetForm)
    {
        HideError();
        currentForm?.AddToClassList(HideFormClass);
        targetForm?.RemoveFromClassList(HideFormClass);
        PlayBootAnimation(targetForm);
    }

    private void PlayBootAnimation(VisualElement panel)
    {
        if (panel == null) return;

        panel.AddToClassList(BootHiddenClass);
        panel.schedule.Execute(() => panel.RemoveFromClassList(BootHiddenClass)).StartingIn(16);
    }

    private void ShowLoading()
    {
        if (_loadingOverlay == null) return;

        _loadingOverlay.RemoveFromClassList(HideFormClass);
        _loadingOverlay.BringToFront();
    }

    private void HideLoading()
    {
        _loadingOverlay?.AddToClassList(HideFormClass);
    }

    private void ShowError(string message)
    {
        HideLoading();

        if (_errorBanner == null || _inlineErrorLabel == null) return;

        _inlineErrorLabel.text = message;
        _errorBanner.RemoveFromClassList(HideFormClass);
        _errorBanner.BringToFront();

        if (_errorHideCoroutine != null)
        {
            StopCoroutine(_errorHideCoroutine);
        }

        _errorHideCoroutine = StartCoroutine(HideErrorAfterDelay());
    }

    private void HideError()
    {
        if (_errorHideCoroutine != null)
        {
            StopCoroutine(_errorHideCoroutine);
            _errorHideCoroutine = null;
        }

        if (_errorBanner == null || _inlineErrorLabel == null) return;

        _inlineErrorLabel.text = string.Empty;
        _errorBanner.AddToClassList(HideFormClass);
    }

    private IEnumerator HideErrorAfterDelay()
    {
        yield return new WaitForSecondsRealtime(ErrorAutoHideSeconds);
        HideError();
    }

    private void ShowSuccessScreen(string message)
    {
        if (_successMessageLabel != null)
        {
            _successMessageLabel.text = message;
        }

        _verifyEmailForm?.AddToClassList(HideFormClass);
        _forgotForm?.AddToClassList(HideFormClass);
        _successForm?.RemoveFromClassList(HideFormClass);
        PlayBootAnimation(_successForm);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // HELPERS: LOGIC PHỤ
    // ─────────────────────────────────────────────────────────────────────────

    private void LoadRememberedAccount()
    {
        if (_loginUsernameField == null || _toggleRememberMe == null) return;

        bool hasRememberedUser = PlayerPrefs.HasKey(RememberMeKey);
        if (!hasRememberedUser)
        {
            _toggleRememberMe.value = false;
            return;
        }

        string rememberedUsername = PlayerPrefs.GetString(RememberMeKey, string.Empty);
        if (!string.IsNullOrEmpty(rememberedUsername))
        {
            _loginUsernameField.value = rememberedUsername;
            _toggleRememberMe.value = true;
        }
    }

    private void ApplyRememberMeAfterSuccessfulLogin(string username)
    {
        if (_toggleRememberMe != null && _toggleRememberMe.value)
        {
            PlayerPrefs.SetString(RememberMeKey, username ?? string.Empty);
        }
        else
        {
            PlayerPrefs.DeleteKey(RememberMeKey);
        }

        PlayerPrefs.Save();
    }

    private void BindPasswordToggle(Button toggleButton, TextField passwordField)
    {
        if (toggleButton == null || passwordField == null) return;

        toggleButton.text = string.Empty;

        VisualElement iconElement = toggleButton.Q<VisualElement>(className: "eye-icon");

        void UpdateIconState()
        {
            if (iconElement == null) return;

            if (passwordField.isPasswordField)
            {
                iconElement.RemoveFromClassList("eye-icon-open");
                iconElement.AddToClassList("eye-icon-closed");
            }
            else
            {
                iconElement.RemoveFromClassList("eye-icon-closed");
                iconElement.AddToClassList("eye-icon-open");
            }
        }

        UpdateIconState();

        toggleButton.RegisterCallback<ClickEvent>(_ =>
        {
            passwordField.isPasswordField = !passwordField.isPasswordField;
            UpdateIconState();
        });
    }
}