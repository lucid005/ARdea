using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Controls the auth UI flow by swapping UIDocument VisualTreeAssets.
/// </summary>
public class AuthScreenController : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private string nextSceneName = "MainApp";

    [Header("Screens")]
    [SerializeField] private VisualTreeAsset welcomeScreen;
    [SerializeField] private VisualTreeAsset loginScreen;
    [SerializeField] private VisualTreeAsset registerScreen;
    [SerializeField] private VisualTreeAsset forgotPasswordScreen;
    [SerializeField] private VisualTreeAsset otpVerificationScreen;
    [SerializeField] private VisualTreeAsset createNewPasswordScreen;
    [SerializeField] private VisualTreeAsset passwordChangedScreen;

    private readonly Stack<VisualTreeAsset> _history = new Stack<VisualTreeAsset>();

    private VisualTreeAsset _currentScreen;
    private VisualElement _card;
    private TextField _emailField;
    private TextField _passwordField;
    private TextField _usernameField;
    private TextField _confirmPasswordField;
    private TextField _verificationCodeField;
    private Button _signInBtn;
    private Button _guestBtn;
    private Label _guestLabel;
    private Label _errorLabel;
    private Label _resendVerificationLabel;
    private Label _successTitleLabel;
    private Label _successSubtitleLabel;
    private FirebaseRestService _firebase;
    private readonly List<Button> _busyButtons = new List<Button>();
    private AuthUser _pendingVerificationUser;
    private bool _isBusy;
    private string _successTitle = "Check your email";
    private string _successSubtitle = "We sent a secure Firebase email to your address.";

    private void OnEnable()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        if (uiDocument == null)
        {
            Debug.LogError("[AuthScreen] UIDocument is missing.");
            return;
        }

        _firebase = GetComponent<FirebaseRestService>();
        if (_firebase == null)
            _firebase = gameObject.AddComponent<FirebaseRestService>();

        _currentScreen = uiDocument.visualTreeAsset;

        if (_currentScreen == null && welcomeScreen != null)
        {
            _currentScreen = welcomeScreen;
            uiDocument.visualTreeAsset = welcomeScreen;
        }

        StartCoroutine(BindNextFrame());
    }

    private void OnDisable()
    {
        UnbindButtons();
    }

    private IEnumerator BindNextFrame()
    {
        yield return null;
        BindCurrentScreen();
    }

    private void BindCurrentScreen()
    {
        UnbindButtons();
        _busyButtons.Clear();

        var root = uiDocument.rootVisualElement;

        _card = root.Q<VisualElement>("card");
        _emailField = root.Q<TextField>("email-field");
        _passwordField = root.Q<TextField>("password-field");
        _usernameField = root.Q<TextField>("username-field");
        _confirmPasswordField = root.Q<TextField>("confirm-password-field");
        _verificationCodeField = root.Q<TextField>("verification-code-field");
        _signInBtn = root.Q<Button>("signin-btn");
        _guestBtn = root.Q<Button>("guest-btn");
        _guestLabel = root.Q<Label>("guest-btn");
        _errorLabel = root.Q<Label>("error-label");
        _resendVerificationLabel = root.Q<Label>("resend-verification-btn");
        _successTitleLabel = root.Q<Label>("success-title");
        _successSubtitleLabel = root.Q<Label>("success-subtitle");

        var backBtn = root.Q<Button>("back-btn");
        var welcomeLoginBtn = root.Q<Button>("login-btn");
        var registerBtn = root.Q<Button>("register-btn");
        var loginLabel = root.Q<Label>("login-btn");
        var signUpBtn = root.Q<Label>("signup-btn");
        var forgotBtn = root.Q<Label>("forgot-btn");
        var sendCodeBtn = root.Q<Button>("send-code-btn");
        var verifyBtn = root.Q<Button>("verify-btn");
        var resetPasswordBtn = root.Q<Button>("reset-password-btn");
        var backLoginBtn = root.Q<Button>("back-login-btn");
        var resendBtn = root.Q<Label>("resend-btn");

        TrackBusyButton(_signInBtn);
        TrackBusyButton(_guestBtn);
        TrackBusyButton(registerBtn);
        TrackBusyButton(sendCodeBtn);
        TrackBusyButton(verifyBtn);
        TrackBusyButton(resetPasswordBtn);

        if (backBtn != null) backBtn.clicked += NavigateBack;
        if (welcomeLoginBtn != null) welcomeLoginBtn.clicked += () => NavigateTo(loginScreen);
        if (registerBtn != null) registerBtn.clicked += OnRegisterButtonClicked;
        if (loginLabel != null) loginLabel.RegisterCallback<ClickEvent>(OnLoginLabelClicked);
        if (_signInBtn != null) _signInBtn.clicked += OnSignInClicked;
        if (_guestBtn != null) _guestBtn.clicked += OnGuestClicked;
        if (_guestLabel != null) _guestLabel.RegisterCallback<ClickEvent>(OnGuestLabelClicked);
        if (signUpBtn != null) signUpBtn.RegisterCallback<ClickEvent>(OnSignUpClicked);
        if (forgotBtn != null) forgotBtn.RegisterCallback<ClickEvent>(OnForgotClicked);
        if (sendCodeBtn != null) sendCodeBtn.clicked += OnSendPasswordResetClicked;
        if (verifyBtn != null) verifyBtn.clicked += OnVerifyEmailCodeClicked;
        if (resetPasswordBtn != null) resetPasswordBtn.clicked += () => NavigateTo(passwordChangedScreen);
        if (backLoginBtn != null) backLoginBtn.clicked += () => NavigateTo(loginScreen);
        if (resendBtn != null) resendBtn.RegisterCallback<ClickEvent>(OnResendClicked);
        if (_resendVerificationLabel != null) _resendVerificationLabel.RegisterCallback<ClickEvent>(OnResendVerificationClicked);

        if (_passwordField != null)
            _passwordField.RegisterCallback<KeyDownEvent>(OnPasswordKeyDown);

        ApplySuccessMessage();
        PlayCardAnimationIfPresent();
    }

    private void UnbindButtons()
    {
        if (uiDocument == null || uiDocument.rootVisualElement == null)
            return;

        var root = uiDocument.rootVisualElement;

        var backBtn = root.Q<Button>("back-btn");
        var signUpBtn = root.Q<Label>("signup-btn");
        var forgotBtn = root.Q<Label>("forgot-btn");
        var resendBtn = root.Q<Label>("resend-btn");
        var resendVerificationBtn = root.Q<Label>("resend-verification-btn");
        var loginLabel = root.Q<Label>("login-btn");
        var registerBtn = root.Q<Button>("register-btn");
        var verifyBtn = root.Q<Button>("verify-btn");

        if (backBtn != null) backBtn.clicked -= NavigateBack;
        if (registerBtn != null) registerBtn.clicked -= OnRegisterButtonClicked;
        if (verifyBtn != null) verifyBtn.clicked -= OnVerifyEmailCodeClicked;
        if (_signInBtn != null) _signInBtn.clicked -= OnSignInClicked;
        if (_guestBtn != null) _guestBtn.clicked -= OnGuestClicked;
        if (_guestLabel != null) _guestLabel.UnregisterCallback<ClickEvent>(OnGuestLabelClicked);
        if (signUpBtn != null) signUpBtn.UnregisterCallback<ClickEvent>(OnSignUpClicked);
        if (forgotBtn != null) forgotBtn.UnregisterCallback<ClickEvent>(OnForgotClicked);
        if (resendBtn != null) resendBtn.UnregisterCallback<ClickEvent>(OnResendClicked);
        if (resendVerificationBtn != null) resendVerificationBtn.UnregisterCallback<ClickEvent>(OnResendVerificationClicked);
        if (loginLabel != null) loginLabel.UnregisterCallback<ClickEvent>(OnLoginLabelClicked);
        if (_passwordField != null) _passwordField.UnregisterCallback<KeyDownEvent>(OnPasswordKeyDown);
    }

    private void NavigateTo(VisualTreeAsset screen)
    {
        if (screen == null)
        {
            Debug.LogWarning("[AuthScreen] Target screen is not assigned.");
            return;
        }

        if (_currentScreen != null && _currentScreen != screen)
            _history.Push(_currentScreen);

        _isBusy = false;
        _currentScreen = screen;
        uiDocument.visualTreeAsset = screen;
        StartCoroutine(BindNextFrame());
    }

    private void NavigateBack()
    {
        if (_history.Count > 0)
        {
            var previous = _history.Pop();
            _currentScreen = previous;
            uiDocument.visualTreeAsset = previous;
            StartCoroutine(BindNextFrame());
            return;
        }

        if (_currentScreen != welcomeScreen && welcomeScreen != null)
            NavigateTo(welcomeScreen);
    }

    private void PlayCardAnimationIfPresent()
    {
        if (_card == null)
            return;

        _card.RemoveFromClassList("card--visible");
        StartCoroutine(ShowCardNextFrame());
    }

    private IEnumerator ShowCardNextFrame()
    {
        yield return null;
        if (_card != null)
            _card.AddToClassList("card--visible");
    }

    private void OnPasswordKeyDown(KeyDownEvent e)
    {
        if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
            OnSignInClicked();
    }

    private void OnSignInClicked()
    {
        if (_isBusy) return;

        HideError();

        var email = _emailField?.value?.Trim() ?? string.Empty;
        var password = _passwordField?.value ?? string.Empty;

        if (string.IsNullOrEmpty(email))
        {
            ShowError("Please enter your email.");
            return;
        }

        if (!email.Contains("@"))
        {
            ShowError("Please enter a valid email.");
            return;
        }

        if (password.Length < 6)
        {
            ShowError("Password must be at least 6 characters.");
            return;
        }

        StartCoroutine(SignInRoutine());
    }

    private void OnGuestClicked()
    {
        if (_isBusy) return;
        StartCoroutine(GuestSignInRoutine());
    }

    private void OnGuestLabelClicked(ClickEvent evt)
    {
        OnGuestClicked();
    }

    private void OnForgotClicked(ClickEvent evt)
    {
        NavigateTo(forgotPasswordScreen);
    }

    private void OnSignUpClicked(ClickEvent evt)
    {
        NavigateTo(registerScreen);
    }

    private void OnRegisterButtonClicked()
    {
        if (_currentScreen != registerScreen)
        {
            NavigateTo(registerScreen);
            return;
        }

        OnRegisterSubmitClicked();
    }

    private void OnLoginLabelClicked(ClickEvent evt)
    {
        NavigateTo(loginScreen);
    }

    private void OnResendClicked(ClickEvent evt)
    {
        if (_currentScreen == otpVerificationScreen && _pendingVerificationUser != null)
        {
            if (_isBusy)
                return;

            StartCoroutine(SendVerificationEmailRoutine(_pendingVerificationUser, false));
            return;
        }

        OnSendPasswordResetClicked();
    }

    private void OnResendVerificationClicked(ClickEvent evt)
    {
        if (_isBusy)
            return;

        if (_pendingVerificationUser == null || string.IsNullOrEmpty(_pendingVerificationUser.IdToken))
        {
            ShowError("Enter your email and password, then press Login to send a verification email.");
            return;
        }

        StartCoroutine(SendVerificationEmailRoutine(_pendingVerificationUser, false));
    }

    private void OnVerifyEmailCodeClicked()
    {
        if (_isBusy)
            return;

        HideError();

        var codeOrLink = _verificationCodeField?.value?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(codeOrLink))
        {
            ShowError("Paste the verification link or the code after oobCode=.");
            return;
        }

        if (_pendingVerificationUser == null)
        {
            ShowError("Please sign in or create an account again, then paste the verification code.");
            return;
        }

        StartCoroutine(ConfirmEmailVerificationRoutine(codeOrLink));
    }

    private IEnumerator SignInRoutine()
    {
        SetBusy(true);
        AuthUser user = null;
        string error = null;

        yield return _firebase.SignIn(
            _emailField.value.Trim(),
            _passwordField.value,
            result => user = result,
            message => error = message);

        if (!string.IsNullOrEmpty(error))
        {
            SetBusy(false);
            ShowError(error);
            yield break;
        }

        var isVerified = false;
        yield return _firebase.IsEmailVerified(user, result => isVerified = result, message => error = message);
        if (!string.IsNullOrEmpty(error))
        {
            SetBusy(false);
            ShowError(error);
            yield break;
        }

        if (!isVerified)
        {
            _pendingVerificationUser = user;
            yield return SendVerificationEmailRoutine(user, true);
            yield break;
        }

        _pendingVerificationUser = null;
        AppState.SetUser(user);
        yield return RestoreOrSeedRemoteData(user);
        SetBusy(false);
        OnAuthSuccess();
    }

    private IEnumerator GuestSignInRoutine()
    {
        SetBusy(true);
        yield return new WaitForSeconds(0.8f);
        SetBusy(false);
        OnAuthSuccess();
    }

    private void OnRegisterSubmitClicked()
    {
        if (_isBusy) return;

        HideError();

        var name = _usernameField?.value?.Trim() ?? string.Empty;
        var email = _emailField?.value?.Trim() ?? string.Empty;
        var password = _passwordField?.value ?? string.Empty;
        var confirmPassword = _confirmPasswordField?.value ?? string.Empty;

        if (string.IsNullOrEmpty(name))
        {
            ShowError("Please enter your name.");
            return;
        }

        if (!email.Contains("@"))
        {
            ShowError("Please enter a valid email.");
            return;
        }

        if (password.Length < 6)
        {
            ShowError("Password must be at least 6 characters.");
            return;
        }

        if (password != confirmPassword)
        {
            ShowError("Passwords do not match.");
            return;
        }

        StartCoroutine(RegisterRoutine(name, email, password));
    }

    private IEnumerator RegisterRoutine(string name, string email, string password)
    {
        SetBusy(true);
        AuthUser user = null;
        string error = null;

        yield return _firebase.Register(email, password, name, result => user = result, message => error = message);
        if (!string.IsNullOrEmpty(error))
        {
            SetBusy(false);
            ShowError(error);
            yield break;
        }

        yield return _firebase.SaveUserProfile(user);
        yield return _firebase.SyncAppData(user, LocalAppStore.LoadData());
        string verificationEmail = null;
        yield return _firebase.SendEmailVerification(user, emailAddress => verificationEmail = emailAddress, message => error = message);
        SetBusy(false);

        if (!string.IsNullOrEmpty(error))
        {
            ShowError(error);
            yield break;
        }

        _pendingVerificationUser = user;
        NavigateTo(otpVerificationScreen);
        Debug.Log("[AuthScreen] Verification email accepted for " + (string.IsNullOrEmpty(verificationEmail) ? email : verificationEmail) + ".");
    }

    private IEnumerator SendVerificationEmailRoutine(AuthUser user, bool navigateToOtp)
    {
        SetBusy(true);
        string error = null;
        string targetEmail = null;
        yield return _firebase.SendEmailVerification(user, emailAddress => targetEmail = emailAddress, message => error = message);
        SetBusy(false);

        if (!string.IsNullOrEmpty(error))
        {
            ShowError(error);
            SetResendVerificationVisible(false);
            yield break;
        }

        if (navigateToOtp)
        {
            NavigateTo(otpVerificationScreen);
            yield break;
        }

        SetResendVerificationVisible(_currentScreen == loginScreen);
        ShowError("Firebase accepted a verification email for " + targetEmail + ". Check Inbox, Spam, and Promotions.");
    }

    private IEnumerator ConfirmEmailVerificationRoutine(string codeOrLink)
    {
        SetBusy(true);
        AuthUser verifiedUser = null;
        string error = null;
        yield return _firebase.ConfirmEmailVerification(
            codeOrLink,
            _pendingVerificationUser,
            result => verifiedUser = result,
            message => error = message);

        if (!string.IsNullOrEmpty(error))
        {
            var alreadyVerified = false;
            var lookupError = string.Empty;
            yield return _firebase.IsEmailVerified(_pendingVerificationUser, result => alreadyVerified = result, message => lookupError = message);
            if (!alreadyVerified)
            {
                SetBusy(false);
                ShowError(error);
                yield break;
            }

            verifiedUser = _pendingVerificationUser;
            error = null;
        }

        var isVerified = false;
        yield return _firebase.IsEmailVerified(verifiedUser, result => isVerified = result, message => error = message);
        if (!string.IsNullOrEmpty(error))
        {
            SetBusy(false);
            ShowError(error);
            yield break;
        }

        if (!isVerified)
        {
            SetBusy(false);
            ShowError("Firebase has not marked this email as verified yet. Try the latest verification link.");
            yield break;
        }

        _pendingVerificationUser = null;
        AppState.SetUser(verifiedUser);
        yield return RestoreOrSeedRemoteData(verifiedUser);
        SetBusy(false);
        OnAuthSuccess();
    }

    private IEnumerator RestoreOrSeedRemoteData(AuthUser user)
    {
        AppDataSnapshot remoteData = null;
        string error = null;

        yield return _firebase.LoadAppData(user, data => remoteData = data, message => error = message);

        if (!string.IsNullOrEmpty(error))
        {
            Debug.LogWarning("[AuthScreen] Remote app data load failed: " + error);
            yield break;
        }

        if (remoteData != null)
        {
            LocalAppStore.SaveData(remoteData);
            yield break;
        }

        yield return _firebase.SyncAppData(user, LocalAppStore.LoadData());
    }

    private void OnSendPasswordResetClicked()
    {
        var email = _emailField?.value?.Trim() ?? string.Empty;
        if (!email.Contains("@"))
        {
            ShowError("Please enter your email first.");
            return;
        }

        StartCoroutine(SendPasswordResetRoutine(email));
    }

    private IEnumerator SendPasswordResetRoutine(string email)
    {
        SetBusy(true);
        string error = null;
        yield return _firebase.SendPasswordReset(email, () => { }, message => error = message);
        SetBusy(false);

        if (!string.IsNullOrEmpty(error))
        {
            ShowError(error);
            yield break;
        }

        ShowSuccessScreen(
            "Check your email",
            "We sent a secure Firebase password reset link to " + email + ".");
    }

    private void OnAuthSuccess()
    {
        Debug.Log("[AuthScreen] Auth success, loading main app scene.");
        StartCoroutine(ExitAndLoad());
    }

    private IEnumerator ExitAndLoad()
    {
        if (_card != null)
        {
            _card.RemoveFromClassList("card--visible");
            yield return new WaitForSeconds(0.35f);
        }

        UnityEngine.SceneManagement.SceneManager.LoadScene(nextSceneName);
    }

    private void SetBusy(bool busy)
    {
        _isBusy = busy;
        foreach (var button in _busyButtons)
        {
            if (button != null)
                button.SetEnabled(!busy);
        }

        if (_guestLabel != null) _guestLabel.SetEnabled(!busy);

        if (_signInBtn == null)
            return;

        _signInBtn.text = busy ? "Signing in..." : "Login";

        if (busy)
            _signInBtn.AddToClassList("btn-primary--loading");
        else
            _signInBtn.RemoveFromClassList("btn-primary--loading");
    }

    private void TrackBusyButton(Button button)
    {
        if (button != null && !_busyButtons.Contains(button))
            _busyButtons.Add(button);
    }

    private void ShowSuccessScreen(string title, string subtitle)
    {
        _successTitle = title;
        _successSubtitle = subtitle;
        NavigateTo(passwordChangedScreen);
    }

    private void ApplySuccessMessage()
    {
        if (_successTitleLabel != null)
            _successTitleLabel.text = _successTitle;

        if (_successSubtitleLabel != null)
            _successSubtitleLabel.text = _successSubtitle;
    }

    private void ShowError(string message)
    {
        if (_errorLabel == null)
            return;

        _errorLabel.text = message;
        _errorLabel.RemoveFromClassList("hidden");
    }

    private void SetResendVerificationVisible(bool visible)
    {
        if (_resendVerificationLabel != null)
            _resendVerificationLabel.EnableInClassList("hidden", !visible);
    }

    private void HideError()
    {
        if (_errorLabel == null)
            return;

        _errorLabel.AddToClassList("hidden");
        _errorLabel.text = string.Empty;
        SetResendVerificationVisible(false);
    }
}
