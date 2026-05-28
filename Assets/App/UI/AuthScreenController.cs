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
    [SerializeField] private string nextSceneName = "ARDesigner";

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
    private Button _signInBtn;
    private Button _guestBtn;
    private Label _guestLabel;
    private Label _errorLabel;
    private bool _isBusy;

    private void OnEnable()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        if (uiDocument == null)
        {
            Debug.LogError("[AuthScreen] UIDocument is missing.");
            return;
        }

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

        var root = uiDocument.rootVisualElement;

        _card = root.Q<VisualElement>("card");
        _emailField = root.Q<TextField>("email-field");
        _passwordField = root.Q<TextField>("password-field");
        _signInBtn = root.Q<Button>("signin-btn");
        _guestBtn = root.Q<Button>("guest-btn");
        _guestLabel = root.Q<Label>("guest-btn");
        _errorLabel = root.Q<Label>("error-label");

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

        if (backBtn != null) backBtn.clicked += NavigateBack;
        if (welcomeLoginBtn != null) welcomeLoginBtn.clicked += () => NavigateTo(loginScreen);
        if (registerBtn != null) registerBtn.clicked += OnRegisterButtonClicked;
        if (loginLabel != null) loginLabel.RegisterCallback<ClickEvent>(OnLoginLabelClicked);
        if (_signInBtn != null) _signInBtn.clicked += OnSignInClicked;
        if (_guestBtn != null) _guestBtn.clicked += OnGuestClicked;
        if (_guestLabel != null) _guestLabel.RegisterCallback<ClickEvent>(OnGuestLabelClicked);
        if (signUpBtn != null) signUpBtn.RegisterCallback<ClickEvent>(OnSignUpClicked);
        if (forgotBtn != null) forgotBtn.RegisterCallback<ClickEvent>(OnForgotClicked);
        if (sendCodeBtn != null) sendCodeBtn.clicked += () => NavigateTo(otpVerificationScreen);
        if (verifyBtn != null) verifyBtn.clicked += () => NavigateTo(createNewPasswordScreen);
        if (resetPasswordBtn != null) resetPasswordBtn.clicked += () => NavigateTo(passwordChangedScreen);
        if (backLoginBtn != null) backLoginBtn.clicked += () => NavigateTo(loginScreen);
        if (resendBtn != null) resendBtn.RegisterCallback<ClickEvent>(OnResendClicked);

        if (_passwordField != null)
            _passwordField.RegisterCallback<KeyDownEvent>(OnPasswordKeyDown);

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
        var loginLabel = root.Q<Label>("login-btn");
        var registerBtn = root.Q<Button>("register-btn");

        if (backBtn != null) backBtn.clicked -= NavigateBack;
        if (registerBtn != null) registerBtn.clicked -= OnRegisterButtonClicked;
        if (_signInBtn != null) _signInBtn.clicked -= OnSignInClicked;
        if (_guestBtn != null) _guestBtn.clicked -= OnGuestClicked;
        if (_guestLabel != null) _guestLabel.UnregisterCallback<ClickEvent>(OnGuestLabelClicked);
        if (signUpBtn != null) signUpBtn.UnregisterCallback<ClickEvent>(OnSignUpClicked);
        if (forgotBtn != null) forgotBtn.UnregisterCallback<ClickEvent>(OnForgotClicked);
        if (resendBtn != null) resendBtn.UnregisterCallback<ClickEvent>(OnResendClicked);
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
        NavigateTo(_currentScreen == registerScreen ? otpVerificationScreen : registerScreen);
    }

    private void OnLoginLabelClicked(ClickEvent evt)
    {
        NavigateTo(loginScreen);
    }

    private void OnResendClicked(ClickEvent evt)
    {
        Debug.Log("[AuthScreen] Resend verification code.");
    }

    private IEnumerator SignInRoutine()
    {
        SetBusy(true);
        yield return new WaitForSeconds(1.5f);
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

    private void OnAuthSuccess()
    {
        Debug.Log("[AuthScreen] Auth success, loading AR scene.");
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
        if (_signInBtn != null) _signInBtn.SetEnabled(!busy);
        if (_guestBtn != null) _guestBtn.SetEnabled(!busy);
        if (_guestLabel != null) _guestLabel.SetEnabled(!busy);

        if (_signInBtn == null)
            return;

        _signInBtn.text = busy ? "Signing in..." : "Login";

        if (busy)
            _signInBtn.AddToClassList("btn-primary--loading");
        else
            _signInBtn.RemoveFromClassList("btn-primary--loading");
    }

    private void ShowError(string message)
    {
        if (_errorLabel == null)
            return;

        _errorLabel.text = message;
        _errorLabel.RemoveFromClassList("hidden");
    }

    private void HideError()
    {
        if (_errorLabel == null)
            return;

        _errorLabel.AddToClassList("hidden");
        _errorLabel.text = string.Empty;
    }
}
