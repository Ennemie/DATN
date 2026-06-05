using System;
using System.Collections;
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
    private VisualElement _resetForm;
    private VisualElement _verifyEmailForm;
    private VisualElement _successForm; // Panel mới bổ sung
    private VisualElement _errorBanner;
    private VisualElement _loadingOverlay;

    // ================= Ô NHẬP LIỆU (TEXTFIELDS) =================
    private TextField _loginUsernameField, _loginPasswordField;
    private TextField _regUsernameField, _regEmailField, _regPasswordField, _regConfirmField;
    private TextField _forgotEmailField;
    private TextField _resetNewPasswordField, _resetConfirmPasswordField;
    private TextField[] _resetOtpBoxes;
    private TextField[] _verifyOtpBoxes;

    // ================= TOGGLE =================
    private Toggle _toggleRememberMe;
    private Toggle _toggleToS;

    // ================= NÚT TOGGLE MẬT KHẨU =================
    private Button _btnToggleLoginPassword;
    private Button _btnToggleRegPassword;
    private Button _btnToggleRegConfirmPassword;
    private Button _btnToggleResetNewPassword;
    private Button _btnToggleResetConfirmPassword;
    private Button _btnSteamAuth;
    private Button _btnGoogleAuth;

    // ================= VĂN BẢN ĐỘNG (LABELS) =================
    private Label _successMessageLabel; // Label hiển thị nội dung thông báo thành công
    private Label _inlineErrorLabel;

    // ================= NÚT BẤM (BUTTONS) =================
    private Button _btnLogin, _btnRegister, _btnSendCode, _btnConfirmReset, _btnResendOTP;
    private Button _btnConfirmVerify, _btnResendVerifyOTP;
    private Button _btnSuccessConfirm; // Nút xác nhận trên màn hình thành công

    private Button _linkToRegister, _linkToLogin, _linkToLoginFromForgot, _linkToLoginFromReset, _linkToLoginFromVerify;
    private Label _linkToForgotPassword;

    private const string HideFormClass = "hide-form";
    private const string RememberMeKey = "Auth.RememberMe.Username";
    private const string BootHiddenClass = "panel-boot-hidden";
    private const int OtpBoxCount = 6;
    private const float ErrorAutoHideSeconds = 4f;
    private const int OtpCountdownSeconds = 60;

    private Coroutine _errorHideCoroutine;
    private Coroutine _otpCountdownResetCoroutine;
    private Coroutine _otpCountdownVerifyCoroutine;
    private DateTime _otpResetEndTimeUtc;
    private DateTime _otpVerifyEndTimeUtc;

    void OnEnable()
    {
        _uiDocument = GetComponent<UIDocument>();
        _root = _uiDocument.rootVisualElement;

        if (_root == null) return;

        // 1. Ánh xạ các Panels
        _loginForm = _root.Q<VisualElement>("LoginForm");
        _registerForm = _root.Q<VisualElement>("RegisterForm");
        _forgotForm = _root.Q<VisualElement>("ForgotPasswordForm");
        _resetForm = _root.Q<VisualElement>("ResetPasswordForm");
        _verifyEmailForm = _root.Q<VisualElement>("VerifyEmailForm");
        _successForm = _root.Q<VisualElement>("SuccessForm");
        _errorBanner = _root.Q<VisualElement>("AuthErrorBanner");
        _loadingOverlay = _root.Q<VisualElement>("LoadingOverlay");

        // 2. Ánh xạ TextFields
        _loginUsernameField = _root.Q<TextField>("LoginUsername");
        _loginPasswordField = _root.Q<TextField>("LoginPassword");
        _regUsernameField = _root.Q<TextField>("RegUsername");
        _regEmailField = _root.Q<TextField>("RegEmail");
        _regPasswordField = _root.Q<TextField>("RegPassword");
        _regConfirmField = _root.Q<TextField>("RegConfirmPassword");
        _forgotEmailField = _root.Q<TextField>("ForgotEmail");
        _resetNewPasswordField = _root.Q<TextField>("ResetNewPassword");
        _resetConfirmPasswordField = _root.Q<TextField>("ResetConfirmPassword");

        // 2b. Ánh xạ Toggle & nút mật khẩu
        _toggleRememberMe = _root.Q<Toggle>("ToggleRememberMe");
        _toggleToS = _root.Q<Toggle>("ToggleToS");
        _btnToggleLoginPassword = _root.Q<Button>("BtnToggleLoginPassword");
        _btnToggleRegPassword = _root.Q<Button>("BtnToggleRegPassword");
        _btnToggleRegConfirmPassword = _root.Q<Button>("BtnToggleRegConfirmPassword");
        _btnToggleResetNewPassword = _root.Q<Button>("BtnToggleResetNewPassword");
        _btnToggleResetConfirmPassword = _root.Q<Button>("BtnToggleResetConfirmPassword");
        _btnSteamAuth = _root.Q<Button>("BtnSteamAuth");
        _btnGoogleAuth = _root.Q<Button>("BtnGoogleAuth");

        // 3. Ánh xạ Labels
        _successMessageLabel = _root.Q<Label>("SuccessMessage");
        _inlineErrorLabel = _root.Q<Label>("AuthErrorMessage");

        // 4. Ánh xạ Buttons
        _btnLogin = _root.Q<Button>("BtnLogin");
        _btnRegister = _root.Q<Button>("BtnRegister");
        _btnSendCode = _root.Q<Button>("BtnSendCode");
        _btnConfirmReset = _root.Q<Button>("BtnConfirmReset");
        _btnResendOTP = _root.Q<Button>("BtnResendOTP");
        _btnConfirmVerify = _root.Q<Button>("BtnConfirmVerify");
        _btnResendVerifyOTP = _root.Q<Button>("BtnResendVerifyOTP");
        _btnSuccessConfirm = _root.Q<Button>("BtnSuccessConfirm");

        // 5. Ánh xạ Links điều hướng tĩnh
        _linkToRegister = _root.Q<Button>("LinkToRegister");
        _linkToLogin = _root.Q<Button>("LinkToLogin");
        _linkToForgotPassword = _root.Q<Label>("LinkToForgotPassword");
        _linkToLoginFromForgot = _root.Q<Button>("LinkToLoginFromForgot");
        _linkToLoginFromReset = _root.Q<Button>("LinkToLoginFromReset");
        _linkToLoginFromVerify = _root.Q<Button>("LinkToLoginFromVerify");

        // Cập nhật lại lệnh gọi hàm (Không truyền text string vào nữa)
        BindPasswordToggle(_btnToggleLoginPassword, _loginPasswordField);
        BindPasswordToggle(_btnToggleRegPassword, _regPasswordField);
        BindPasswordToggle(_btnToggleRegConfirmPassword, _regConfirmField);
        BindPasswordToggle(_btnToggleResetNewPassword, _resetNewPasswordField);
        BindPasswordToggle(_btnToggleResetConfirmPassword, _resetConfirmPasswordField);

        _resetOtpBoxes = BindOtpBoxGroup("ResetOTPBoxContainer", "ResetOTP_");
        _verifyOtpBoxes = BindOtpBoxGroup("VerifyOTPBoxContainer", "VerifyOTP_");

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

        if (_otpCountdownResetCoroutine != null)
        {
            StopCoroutine(_otpCountdownResetCoroutine);
            _otpCountdownResetCoroutine = null;
        }

        if (_otpCountdownVerifyCoroutine != null)
        {
            StopCoroutine(_otpCountdownVerifyCoroutine);
            _otpCountdownVerifyCoroutine = null;
        }
    }

    private void RegisterInputChangeHandlers()
    {
        RegisterClearErrorOnTyping(_loginUsernameField);
        RegisterClearErrorOnTyping(_loginPasswordField);
        RegisterClearErrorOnTyping(_regUsernameField);
        RegisterClearErrorOnTyping(_regEmailField);
        RegisterClearErrorOnTyping(_regPasswordField);
        RegisterClearErrorOnTyping(_regConfirmField);
        RegisterClearErrorOnTyping(_forgotEmailField);
        RegisterClearErrorOnTyping(_resetNewPasswordField);
        RegisterClearErrorOnTyping(_resetConfirmPasswordField);
        RegisterClearErrorOnTyping(_resetOtpBoxes);
        RegisterClearErrorOnTyping(_verifyOtpBoxes);

        _toggleRememberMe?.RegisterValueChangedCallback(_ => HideError());
        _toggleToS?.RegisterValueChangedCallback(_ => HideError());
    }

    private void RegisterClearErrorOnTyping(TextField field)
    {
        field?.RegisterValueChangedCallback(_ => HideError());
    }

    private void RegisterClearErrorOnTyping(TextField[] fields)
    {
        if (fields == null) return;

        for (int index = 0; index < fields.Length; index++)
        {
            fields[index]?.RegisterValueChangedCallback(_ => HideError());
        }
    }

    private void RegisterEvents()
    {
        // Hệ thống click điều hướng form cơ bản
        _linkToRegister?.RegisterCallback<ClickEvent>(evt => SwitchForm(_loginForm, _registerForm));
        _linkToLogin?.RegisterCallback<ClickEvent>(evt => SwitchForm(_registerForm, _loginForm));
        _linkToForgotPassword?.RegisterCallback<ClickEvent>(evt => SwitchForm(_loginForm, _forgotForm));
        _linkToLoginFromForgot?.RegisterCallback<ClickEvent>(evt => SwitchForm(_forgotForm, _loginForm));
        _linkToLoginFromReset?.RegisterCallback<ClickEvent>(evt => SwitchForm(_resetForm, _loginForm));
        _linkToLoginFromVerify?.RegisterCallback<ClickEvent>(evt => SwitchForm(_verifyEmailForm, _loginForm));

        _btnSteamAuth?.RegisterCallback<ClickEvent>(_ => ShowError("[ FAST AUTH ] Steam login chưa được nối backend."));
        _btnGoogleAuth?.RegisterCallback<ClickEvent>(_ => ShowError("[ FAST AUTH ] Google login chưa được nối backend."));

        // Click các nút xử lý tác vụ dữ liệu
        _btnLogin?.RegisterCallback<ClickEvent>(evt => ProcessLogin());
        _btnRegister?.RegisterCallback<ClickEvent>(evt => ProcessRegister());
        _btnSendCode?.RegisterCallback<ClickEvent>(evt => ProcessForgotPassword());
        
        // Đăng ký sự kiện nút Kích hoạt & nút Đặt lại mật khẩu mới
        _btnConfirmVerify?.RegisterCallback<ClickEvent>(evt => ProcessConfirmVerify());
        _btnConfirmReset?.RegisterCallback<ClickEvent>(evt => ProcessResetPassword());

        // Gửi lại OTP kích hoạt hoặc reset
        _btnResendOTP?.RegisterCallback<ClickEvent>(evt => StartCountdown(_btnResendOTP, ref _otpCountdownResetCoroutine, false));
        _btnResendVerifyOTP?.RegisterCallback<ClickEvent>(evt => StartCountdown(_btnResendVerifyOTP, ref _otpCountdownVerifyCoroutine, true));

        // Nút tắt màn hình thông báo thành công để quay lại Login
        _btnSuccessConfirm?.RegisterCallback<ClickEvent>(evt => SwitchForm(_successForm, _loginForm));
    }

    // ================= HÀM XỬ LÝ ẢNH MẮT (ĐÃ CẬP NHẬT) =================
    private void BindPasswordToggle(Button toggleButton, TextField passwordField)
    {
        if (toggleButton == null || passwordField == null) return;

        // Chắc chắn xóa trắng text nếu UXML còn sót
        toggleButton.text = string.Empty;

        // Lấy Element con chứa Icon
        VisualElement iconElement = toggleButton.Q<VisualElement>(className: "eye-icon");

        // Hàm cục bộ (Local Function) để cập nhật class ảnh
        void UpdateIconState()
        {
            if (iconElement != null)
            {
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
        }

        // Gọi lần đầu để thiết lập Icon lúc bật game lên
        UpdateIconState();

        // Xử lý sự kiện click
        toggleButton.RegisterCallback<ClickEvent>(_ =>
        {
            passwordField.isPasswordField = !passwordField.isPasswordField;
            UpdateIconState();
        });
    }

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

    private void ProcessLogin()
    {
        string username = _loginUsernameField?.value?.Trim();
        string password = _loginPasswordField?.value;

        if (string.IsNullOrEmpty(username))
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
        // TODO: Gọi backend xác thực tại đây.

        ApplyRememberMeAfterSuccessfulLogin(username);
        HideLoading();
    }

    private void ProcessRegister()
    {
        string username = _regUsernameField?.value?.Trim();
        string email = _regEmailField?.value?.Trim();
        string password = _regPasswordField?.value;
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

        if (password != confirmPassword)
        {
            ShowError("[ REGISTER ERROR ] Mật khẩu xác nhận không khớp.");
            return;
        }

        if (_toggleToS == null || !_toggleToS.value)
        {
            ShowError("Cảnh báo: Bạn phải đồng ý với Điều khoản dịch vụ để tạo danh tính mới.");
            return;
        }

        ShowLoading();
        // TODO: Gọi backend tạo tài khoản và gửi OTP.
        HideLoading();
        
        // Chuyển sang form nhập OTP xác thực email
        SwitchForm(_registerForm, _verifyEmailForm);
        StartCountdown(_btnResendVerifyOTP, ref _otpCountdownVerifyCoroutine, true);
    }

    // Luồng xử lý khi người chơi bấm nút "KÍCH HOẠT" tài khoản mới
    private void ProcessConfirmVerify()
    {
        string otp = CollectOtpCode(_verifyOtpBoxes);
        if (string.IsNullOrEmpty(otp) || otp.Length < 6)
        {
            ShowError("[ VERIFY ERROR ] Mã OTP kích hoạt không hợp lệ.");
            return;
        }

        // GIẢ LẬP: API Backend phản hồi kích hoạt thành công tài khoản
        ShowSuccessScreen("Tài khoản của bạn đã được kích hoạt hoàn tất. Hãy đăng nhập để bắt đầu trải nghiệm trò chơi.");
        ClearOtpBoxes(_verifyOtpBoxes);
    }

    private void ProcessForgotPassword()
    {
        string email = _forgotEmailField?.value?.Trim();
        if (string.IsNullOrEmpty(email))
        {
            ShowError("[ RECOVERY ERROR ] Vui lòng nhập email liên kết với tài khoản.");
            return;
        }

        ShowLoading();
        // TODO: Gọi backend gửi mã khôi phục.
        HideLoading();

        // Chuyển sang form đặt lại mật khẩu mật mã mới
        SwitchForm(_forgotForm, _resetForm);
        StartCountdown(_btnResendOTP, ref _otpCountdownResetCoroutine, false);
    }

    // Luồng xử lý khi người chơi bấm nút "HOÀN TẤT" đặt lại mật khẩu mới
    private void ProcessResetPassword()
    {
        string otp = CollectOtpCode(_resetOtpBoxes);
        string newPass = _resetNewPasswordField?.value;
        string confirmPass = _resetConfirmPasswordField?.value;

        if (string.IsNullOrEmpty(otp) || string.IsNullOrEmpty(newPass) || newPass != confirmPass)
        {
            ShowError("[ RESET ERROR ] OTP hoặc mật khẩu mới không hợp lệ.");
            return;
        }

        // GIẢ LẬP: API Backend cập nhật mật khẩu mới thành công
        ShowSuccessScreen("Mật khẩu bảo mật của tài khoản đã thay đổi thành công. Vui lòng sử dụng mật khẩu mới để đăng nhập.");
        ClearOtpBoxes(_resetOtpBoxes);
        ClearResetFields();
    }

    // Hàm gọi hiển thị thông báo trung gian linh hoạt mẫu UX
    private void ShowSuccessScreen(string message)
    {
        if (_successMessageLabel != null)
        {
            _successMessageLabel.text = message;
        }

        // Ẩn tất cả các panel hành động cũ đang mở ra
        _verifyEmailForm?.AddToClassList(HideFormClass);
        _resetForm?.AddToClassList(HideFormClass);

        // Hiển thị panel thông báo thành công
        _successForm?.RemoveFromClassList(HideFormClass);
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

    private void StartCountdown(Button btn, ref Coroutine coroutineRef, bool isVerifyCountdown)
    {
        if (coroutineRef != null) StopCoroutine(coroutineRef);

        if (isVerifyCountdown)
        {
            _otpVerifyEndTimeUtc = DateTime.UtcNow.AddSeconds(OtpCountdownSeconds);
        }
        else
        {
            _otpResetEndTimeUtc = DateTime.UtcNow.AddSeconds(OtpCountdownSeconds);
        }

        coroutineRef = StartCoroutine(OTPCountdownRoutine(btn, isVerifyCountdown));
    }

    private IEnumerator OTPCountdownRoutine(Button targetBtn, bool isVerifyCountdown)
    {
        if (targetBtn == null) yield break;
        targetBtn.SetEnabled(false);

        DateTime targetTime = isVerifyCountdown ? _otpVerifyEndTimeUtc : _otpResetEndTimeUtc;

        while (true)
        {
            TimeSpan remaining = targetTime - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                break;
            }

            int secondsLeft = Mathf.CeilToInt((float)remaining.TotalSeconds);
            targetBtn.text = $"GỬI LẠI ({secondsLeft}s)";
            yield return new WaitForSecondsRealtime(1f);
        }

        targetBtn.text = "GỬI LẠI MÃ";
        targetBtn.SetEnabled(true);
    }

    private TextField[] BindOtpBoxGroup(string containerName, string fieldPrefix)
    {
        VisualElement container = _root.Q<VisualElement>(containerName);
        if (container == null)
        {
            return null;
        }

        TextField[] boxes = new TextField[OtpBoxCount];
        for (int index = 0; index < OtpBoxCount; index++)
        {
            TextField box = container.Q<TextField>($"{fieldPrefix}{index + 1}");
            if (box == null)
            {
                continue;
            }

            int capturedIndex = index;
            boxes[index] = box;
            box.RegisterValueChangedCallback(evt => OnOtpBoxValueChanged(boxes, capturedIndex, evt.previousValue, evt.newValue));
        }

        return boxes;
    }

    private void OnOtpBoxValueChanged(TextField[] boxes, int index, string previousValue, string newValue)
    {
        if (boxes == null || index < 0 || index >= boxes.Length)
        {
            return;
        }

        if (string.IsNullOrEmpty(newValue))
        {
            if (!string.IsNullOrEmpty(previousValue) && index > 0)
            {
                boxes[index - 1]?.Focus();
            }

            return;
        }

        if (index == 0 && newValue.Length >= OtpBoxCount)
        {
            for (int i = 0; i < boxes.Length; i++)
            {
                string digit = i < newValue.Length ? newValue[i].ToString() : string.Empty;
                boxes[i]?.SetValueWithoutNotify(digit);
            }

            boxes[Mathf.Min(OtpBoxCount - 1, newValue.Length - 1)]?.Focus();
            return;
        }

        string sanitized = newValue.Substring(0, 1);
        if (newValue.Length > 1)
        {
            boxes[index]?.SetValueWithoutNotify(sanitized);
        }

        if (index < boxes.Length - 1)
        {
            boxes[index + 1]?.Focus();
        }
    }

    private string CollectOtpCode(TextField[] boxes)
    {
        if (boxes == null)
        {
            return string.Empty;
        }

        string otp = string.Empty;
        for (int i = 0; i < boxes.Length; i++)
        {
            string value = boxes[i]?.value ?? string.Empty;
            if (!string.IsNullOrEmpty(value))
            {
                otp += value.Substring(0, 1);
            }
        }

        return otp;
    }

    private void ClearOtpBoxes(TextField[] boxes)
    {
        if (boxes == null)
        {
            return;
        }

        for (int i = 0; i < boxes.Length; i++)
        {
            boxes[i]?.SetValueWithoutNotify(string.Empty);
        }
    }

    private void ClearResetFields()
    {
        if (_resetNewPasswordField != null) _resetNewPasswordField.value = string.Empty;
        if (_resetConfirmPasswordField != null) _resetConfirmPasswordField.value = string.Empty;
        if (_forgotEmailField != null) _forgotEmailField.value = string.Empty;
    }
}