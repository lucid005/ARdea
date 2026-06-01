using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Templates.AR;

/// <summary>
/// Temporary front-end-only UI flow for the app shell.
/// </summary>
public class UIWorkflowManager : MonoBehaviour
{
    [Header("Flow Canvases")]
    [SerializeField] GameObject m_StartingCanvas;
    [SerializeField] GameObject m_AuthenticationCanvas;
    [SerializeField] GameObject m_AppUICanvas;
    [SerializeField] float m_StartingCanvasDuration = 5f;

    [Header("Authentication Screens")]
    [SerializeField] GameObject m_AuthenticationPanel;
    [SerializeField] GameObject m_LoginCanvas;
    [SerializeField] GameObject m_SignupCanvas;
    [SerializeField] GameObject m_ForgotPasswordCanvas;
    [SerializeField] GameObject m_OtpVerifyCanvas;
    [SerializeField] GameObject m_ChangedCanvas;

    bool m_IsWired;
    Coroutine m_StartingCanvasRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (FindObjectOfType<UIWorkflowManager>(true) != null)
            return;

        var mainUI = FindObjectByExactName("Main UI");
        var host = mainUI != null ? mainUI : new GameObject("UI Workflow Manager");
        host.AddComponent<UIWorkflowManager>();
    }

    void Awake()
    {
        DiscoverReferences();
        WireButtons();
        ShowStarting();
    }

    void OnEnable()
    {
        if (!m_IsWired)
            return;

        ShowStarting();
    }

    void DiscoverReferences()
    {
        m_StartingCanvas = m_StartingCanvas != null ? m_StartingCanvas : FindObjectByExactName("Starting Canvas");
        m_AuthenticationPanel = m_AuthenticationPanel != null ? m_AuthenticationPanel : FindObjectByExactName("Authentication Panel");
        if (m_AuthenticationPanel == null)
            m_AuthenticationPanel = FindObjectByExactName("Autentication Panel");

        m_LoginCanvas = m_LoginCanvas != null ? m_LoginCanvas : FindObjectByExactName("Login Panel");
        if (m_LoginCanvas == null)
            m_LoginCanvas = FindObjectByExactName("Login Canvas");

        m_SignupCanvas = m_SignupCanvas != null ? m_SignupCanvas : FindObjectByExactName("Signup Panel");
        if (m_SignupCanvas == null)
            m_SignupCanvas = FindObjectByExactName("Signup Canvas");

        m_ForgotPasswordCanvas = m_ForgotPasswordCanvas != null ? m_ForgotPasswordCanvas : FindObjectByExactName("Forgot Password Panel");
        if (m_ForgotPasswordCanvas == null)
            m_ForgotPasswordCanvas = FindObjectByExactName("Forgot Pwd Canvas");

        m_OtpVerifyCanvas = m_OtpVerifyCanvas != null ? m_OtpVerifyCanvas : FindObjectByExactName("OTP Verification Panel");
        if (m_OtpVerifyCanvas == null)
            m_OtpVerifyCanvas = FindObjectByExactName("OTP Verify Canvas");

        m_ChangedCanvas = m_ChangedCanvas != null ? m_ChangedCanvas : FindObjectByExactName("Changed");
        if (m_ChangedCanvas == null)
            m_ChangedCanvas = FindObjectByExactName("Changed Canvas");
        if (m_ChangedCanvas == null)
            m_ChangedCanvas = FindObjectByExactName("Create new password");

        m_AuthenticationCanvas = m_AuthenticationCanvas != null
            ? m_AuthenticationCanvas
            : FindObjectByExactName("Authentication");

        if (m_AuthenticationCanvas == null)
            m_AuthenticationCanvas = FindObjectByExactName("Authentication Canvas");

        if (m_AuthenticationCanvas == null && m_AuthenticationPanel != null && m_AuthenticationPanel.transform.parent != null)
            m_AuthenticationCanvas = m_AuthenticationPanel.transform.parent.gameObject;

        if (m_AuthenticationCanvas == null)
            m_AuthenticationCanvas = m_AuthenticationPanel != null ? m_AuthenticationPanel : m_LoginCanvas;

        m_AppUICanvas = m_AppUICanvas != null ? m_AppUICanvas : FindObjectByExactName("AppUI Canvas");
        if (m_AppUICanvas == null)
            m_AppUICanvas = FindObjectByExactName("App UI Canvas");
        if (m_AppUICanvas == null)
            m_AppUICanvas = FindArTemplateCanvas();
    }

    void WireButtons()
    {
        WireAuthenticationPanelButtons();

        WireButtonsContaining(m_LoginCanvas, ShowAppUI, "login", "log in");
        WireButtonsContaining(m_LoginCanvas, ShowSignup, "register", "signup", "sign up");
        WireButtonsContaining(m_LoginCanvas, ShowForgotPassword, "forgot");

        WireButtonsContaining(m_SignupCanvas, ShowAppUI, "register", "signup", "sign up");
        WireButtonsContaining(m_SignupCanvas, ShowLogin, "login", "log in");

        WireButtonsContaining(m_ForgotPasswordCanvas, ShowOtpVerify, "send code", "code");
        WireButtonsContaining(m_ForgotPasswordCanvas, ShowLogin, "login", "log in");

        WireButtonsContaining(m_OtpVerifyCanvas, ShowChanged, "verify");
        WireButtonsContaining(m_OtpVerifyCanvas, ShowLogin, "login", "log in");

        WireButtonsContaining(m_ChangedCanvas, ShowLogin, "login", "log in");

        m_IsWired = true;
    }

    void WireAuthenticationPanelButtons()
    {
        if (m_AuthenticationPanel == null)
            return;

        foreach (var button in m_AuthenticationPanel.GetComponentsInChildren<Button>(true))
        {
            var buttonName = button.name.Trim();
            var labelText = GetButtonLabelText(button).Trim();

            if (EqualsAny(buttonName, "Login", "Login Button", "Login Panel Button") ||
                EqualsAny(labelText, "Login", "Log In"))
            {
                WireButtonExclusive(button, ShowLogin);
                continue;
            }

            if (EqualsAny(buttonName, "Register", "Register Button", "Signup Button", "Signup Panel Button") ||
                EqualsAny(labelText, "Register", "Register Now", "Signup", "Sign Up"))
            {
                WireButtonExclusive(button, ShowSignup);
            }
        }
    }

    public void ShowStarting()
    {
        StopStartingCanvasRoutine();
        SetActiveSafe(m_AppUICanvas, false);
        SetAuthVisible(false);
        SetActiveSafe(m_StartingCanvas, true);
        m_StartingCanvasRoutine = StartCoroutine(ShowAuthenticationAfterDelay());
    }

    public void ShowAuthentication()
    {
        StopStartingCanvasRoutine();
        SetActiveSafe(m_AppUICanvas, false);
        SetActiveSafe(m_StartingCanvas, false);

        if (m_AuthenticationPanel != null)
        {        
            SetActiveSafe(m_AuthenticationCanvas, true);
            SetActiveSafe(m_AuthenticationPanel, true);
            HideAuthScreens();
        }
        else
        {
            ShowLogin();
        }
    }

    public void ShowAppUI()
    {
        StopStartingCanvasRoutine();
        SetActiveSafe(m_StartingCanvas, false);
        SetAuthVisible(false);
        SetActiveSafe(m_AppUICanvas, true);
    }

    public void ShowLogin()
    {
        SetActiveSafe(m_AuthenticationCanvas, true);
        SetActiveSafe(m_AuthenticationPanel, false);
        ShowOnlyAuthScreen(m_LoginCanvas);
    }

    public void ShowSignup()
    {
        SetActiveSafe(m_AuthenticationCanvas, true);
        SetActiveSafe(m_AuthenticationPanel, false);
        ShowOnlyAuthScreen(m_SignupCanvas);
    }

    public void ShowForgotPassword()
    {
        SetActiveSafe(m_AuthenticationCanvas, true);
        SetActiveSafe(m_AuthenticationPanel, false);
        ShowOnlyAuthScreen(m_ForgotPasswordCanvas);
    }

    public void ShowOtpVerify()
    {
        SetActiveSafe(m_AuthenticationCanvas, true);
        SetActiveSafe(m_AuthenticationPanel, false);
        ShowOnlyAuthScreen(m_OtpVerifyCanvas);
    }

    public void ShowChanged()
    {
        SetActiveSafe(m_AuthenticationCanvas, true);
        SetActiveSafe(m_AuthenticationPanel, false);
        ShowOnlyAuthScreen(m_ChangedCanvas);
    }

    void ShowOnlyAuthScreen(GameObject screenToShow)
    {
        SetActiveSafe(m_StartingCanvas, false);
        SetActiveSafe(m_AppUICanvas, false);

        SetActiveSafe(m_LoginCanvas, screenToShow == m_LoginCanvas);
        SetActiveSafe(m_SignupCanvas, screenToShow == m_SignupCanvas);
        SetActiveSafe(m_ForgotPasswordCanvas, screenToShow == m_ForgotPasswordCanvas);
        SetActiveSafe(m_OtpVerifyCanvas, screenToShow == m_OtpVerifyCanvas);
        SetActiveSafe(m_ChangedCanvas, screenToShow == m_ChangedCanvas);
    }

    void SetAuthVisible(bool isVisible)
    {
        SetActiveSafe(m_AuthenticationCanvas, isVisible);
        SetActiveSafe(m_AuthenticationPanel, isVisible);
        SetActiveSafe(m_LoginCanvas, false);
        SetActiveSafe(m_SignupCanvas, false);
        SetActiveSafe(m_ForgotPasswordCanvas, false);
        SetActiveSafe(m_OtpVerifyCanvas, false);
        SetActiveSafe(m_ChangedCanvas, false);
    }

    void HideAuthScreens()
    {
        SetActiveSafe(m_LoginCanvas, false);
        SetActiveSafe(m_SignupCanvas, false);
        SetActiveSafe(m_ForgotPasswordCanvas, false);
        SetActiveSafe(m_OtpVerifyCanvas, false);
        SetActiveSafe(m_ChangedCanvas, false);
    }

    IEnumerator ShowAuthenticationAfterDelay()
    {
        yield return new WaitForSeconds(m_StartingCanvasDuration);
        m_StartingCanvasRoutine = null;
        ShowAuthentication();
    }

    void StopStartingCanvasRoutine()
    {
        if (m_StartingCanvasRoutine == null)
            return;

        StopCoroutine(m_StartingCanvasRoutine);
        m_StartingCanvasRoutine = null;
    }

    static void WireButtonsContaining(GameObject root, UnityEngine.Events.UnityAction action, params string[] keywords)
    {
        if (root == null)
            return;

        foreach (var button in root.GetComponentsInChildren<Button>(true))
        {
            var searchableText = GetButtonSearchableText(button);
            foreach (var keyword in keywords)
            {
                if (!string.IsNullOrWhiteSpace(keyword) &&
                    searchableText.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    WireButton(button, action);
                    break;
                }
            }
        }
    }

    static string GetButtonSearchableText(Button button)
    {
        var searchableText = button.name;
        foreach (var label in button.GetComponentsInChildren<TMPro.TMP_Text>(true))
        {
            if (!string.IsNullOrWhiteSpace(label.text))
                searchableText += $" {label.text.Trim()}";
        }

        return searchableText;
    }

    static string GetButtonLabelText(Button button)
    {
        foreach (var label in button.GetComponentsInChildren<TMPro.TMP_Text>(true))
        {
            if (!string.IsNullOrWhiteSpace(label.text))
                return label.text;
        }

        return string.Empty;
    }

    static bool EqualsAny(string value, params string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (value.Equals(candidate, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    static void WireNamedButton(GameObject root, string objectName, UnityEngine.Events.UnityAction action)
    {
        if (root == null)
            return;

        foreach (var button in root.GetComponentsInChildren<Button>(true))
        {
            if (button.name.Equals(objectName, StringComparison.OrdinalIgnoreCase))
                WireButton(button, action);
        }
    }

    static void WireButtonsWithText(GameObject root, string buttonText, UnityEngine.Events.UnityAction action)
    {
        if (root == null)
            return;

        foreach (var button in root.GetComponentsInChildren<Button>(true))
        {
            var label = button.GetComponentInChildren<TMPro.TMP_Text>(true);
            if (label != null && label.text.Trim().Equals(buttonText, StringComparison.OrdinalIgnoreCase))
                WireButton(button, action);
        }
    }

    static void WireButton(Button button, UnityEngine.Events.UnityAction action)
    {
        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    void WireButtonExclusive(Button button, UnityEngine.Events.UnityAction action)
    {
        button.onClick.RemoveListener(ShowAuthentication);
        button.onClick.RemoveListener(ShowLogin);
        button.onClick.RemoveListener(ShowSignup);
        button.onClick.RemoveListener(ShowForgotPassword);
        button.onClick.RemoveListener(ShowOtpVerify);
        button.onClick.RemoveListener(ShowChanged);
        button.onClick.RemoveListener(ShowAppUI);
        button.onClick.AddListener(action);
    }

    static void SetActiveSafe(GameObject target, bool isActive)
    {
        if (target != null && target.activeSelf != isActive)
            target.SetActive(isActive);
    }

    static GameObject FindObjectByExactName(string objectName)
    {
        foreach (var transform in FindObjectsOfType<Transform>(true))
        {
            if (transform.name.Equals(objectName, StringComparison.OrdinalIgnoreCase))
                return transform.gameObject;
        }

        return null;
    }

    static GameObject FindArTemplateCanvas()
    {
        var manager = FindObjectOfType<ARTemplateMenuManager>(true);
        if (manager == null)
            return null;

        var canvas = manager.GetComponentInParent<Canvas>(true);
        return canvas != null ? canvas.gameObject : manager.gameObject;
    }
}
