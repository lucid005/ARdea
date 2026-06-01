using System;
using System.Collections;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

public sealed class FirebaseRestService : MonoBehaviour
{
    private FirebaseConfig _config;

    private void Awake()
    {
        _config = FirebaseConfig.Load();
    }

    public IEnumerator SignIn(string email, string password, Action<AuthUser> onSuccess, Action<string> onError)
    {
        var body = JsonUtility.ToJson(new EmailPasswordRequest(email, password, true));
        yield return PostAuth("accounts:signInWithPassword", body, onSuccess, onError);
    }

    public IEnumerator Register(string email, string password, string displayName, Action<AuthUser> onSuccess, Action<string> onError)
    {
        var body = JsonUtility.ToJson(new EmailPasswordRequest(email, password, true));
        AuthUser createdUser = null;
        string error = null;

        yield return PostAuth("accounts:signUp", body, user => createdUser = user, message => error = message);

        if (!string.IsNullOrEmpty(error))
        {
            onError?.Invoke(error);
            yield break;
        }

        createdUser.DisplayName = displayName;
        yield return UpdateAccountProfile(createdUser, displayName);
        onSuccess?.Invoke(createdUser);
    }

    public IEnumerator SendEmailVerification(AuthUser user, Action<string> onSuccess, Action<string> onError)
    {
        if (user == null || string.IsNullOrEmpty(user.IdToken))
        {
            onError?.Invoke("Please log in again before verifying your email.");
            yield break;
        }

        string tokenError = null;
        yield return EnsureFreshToken(user, message => tokenError = message);
        if (!string.IsNullOrEmpty(tokenError))
        {
            onError?.Invoke(tokenError);
            yield break;
        }

        var body = JsonUtility.ToJson(new VerifyEmailRequest("VERIFY_EMAIL", user.IdToken));
        var url = $"https://identitytoolkit.googleapis.com/v1/accounts:sendOobCode?key={_config.ApiKey}";
        using (var request = CreateJsonRequest(url, body))
        {
            yield return request.SendWebRequest();
            var responseText = request.downloadHandler.text;
            if (request.result == UnityWebRequest.Result.Success)
            {
                var response = JsonUtility.FromJson<EmailActionResponse>(responseText);
                var targetEmail = string.IsNullOrEmpty(response?.email) ? user.Email : response.email;
                Debug.Log("[FirebaseRestService] Firebase accepted verification email for " + targetEmail + ".");
                onSuccess?.Invoke(targetEmail);
            }
            else
            {
                Debug.LogWarning("[FirebaseRestService] Verification email request failed (" + request.responseCode + "): " + responseText);
                onError?.Invoke(ParseFirebaseError(responseText));
            }
        }
    }

    public IEnumerator ConfirmEmailVerification(string codeOrLink, AuthUser currentUser, Action<AuthUser> onSuccess, Action<string> onError)
    {
        if (!_config.IsValid)
        {
            onError?.Invoke("Firebase config is missing.");
            yield break;
        }

        var oobCode = ExtractOobCode(codeOrLink);
        if (string.IsNullOrEmpty(oobCode))
        {
            onError?.Invoke("Paste the verification link or the code after oobCode=.");
            yield break;
        }

        var body = JsonUtility.ToJson(new EmailVerificationConfirmRequest(oobCode, true));
        var url = $"https://identitytoolkit.googleapis.com/v1/accounts:update?key={_config.ApiKey}";
        using (var request = CreateJsonRequest(url, body))
        {
            yield return request.SendWebRequest();
            var responseText = request.downloadHandler.text;
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning("[FirebaseRestService] Email verification confirm failed (" + request.responseCode + "): " + responseText);
                onError?.Invoke(ParseFirebaseError(responseText));
                yield break;
            }

            var response = JsonUtility.FromJson<AuthResponse>(responseText);
            var verifiedUser = currentUser ?? new AuthUser();
            if (!string.IsNullOrEmpty(response.localId))
                verifiedUser.LocalId = response.localId;
            if (!string.IsNullOrEmpty(response.email))
                verifiedUser.Email = response.email;
            if (!string.IsNullOrEmpty(response.displayName))
                verifiedUser.DisplayName = response.displayName;
            if (!string.IsNullOrEmpty(response.idToken))
                verifiedUser.IdToken = response.idToken;
            if (!string.IsNullOrEmpty(response.refreshToken))
                verifiedUser.RefreshToken = response.refreshToken;
            verifiedUser.SetTokenExpiryFromSeconds(response.expiresIn);

            Debug.Log("[FirebaseRestService] Firebase confirmed email verification for " + verifiedUser.Email + ".");
            onSuccess?.Invoke(verifiedUser);
        }
    }

    public IEnumerator IsEmailVerified(AuthUser user, Action<bool> onSuccess, Action<string> onError)
    {
        if (user == null || string.IsNullOrEmpty(user.IdToken))
        {
            onError?.Invoke("Please log in again to check email verification.");
            yield break;
        }

        string tokenError = null;
        yield return EnsureFreshToken(user, message => tokenError = message);
        if (!string.IsNullOrEmpty(tokenError))
        {
            onError?.Invoke(tokenError);
            yield break;
        }

        var body = JsonUtility.ToJson(new IdTokenRequest(user.IdToken));
        var url = $"https://identitytoolkit.googleapis.com/v1/accounts:lookup?key={_config.ApiKey}";
        using (var request = CreateJsonRequest(url, body))
        {
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke(ParseFirebaseError(request.downloadHandler.text));
                yield break;
            }

            var response = JsonUtility.FromJson<LookupResponse>(request.downloadHandler.text);
            var verified = response?.users != null && response.users.Length > 0 && response.users[0].emailVerified;
            onSuccess?.Invoke(verified);
        }
    }

    public IEnumerator SendPasswordReset(string email, Action onSuccess, Action<string> onError)
    {
        var body = JsonUtility.ToJson(new PasswordResetRequest("PASSWORD_RESET", email));
        var url = $"https://identitytoolkit.googleapis.com/v1/accounts:sendOobCode?key={_config.ApiKey}";
        using (var request = CreateJsonRequest(url, body))
        {
            yield return request.SendWebRequest();
            var responseText = request.downloadHandler.text;
            if (request.result == UnityWebRequest.Result.Success)
            {
                var response = JsonUtility.FromJson<EmailActionResponse>(responseText);
                Debug.Log("[FirebaseRestService] Firebase accepted password reset email for " + (string.IsNullOrEmpty(response?.email) ? email : response.email) + ".");
                onSuccess?.Invoke();
            }
            else
            {
                Debug.LogWarning("[FirebaseRestService] Password reset request failed (" + request.responseCode + "): " + responseText);
                onError?.Invoke(ParseFirebaseError(responseText));
            }
        }
    }

    public IEnumerator SaveUserProfile(AuthUser user)
    {
        if (user == null || string.IsNullOrEmpty(user.IdToken))
            yield break;

        string tokenError = null;
        yield return EnsureFreshToken(user, message => tokenError = message);
        if (!string.IsNullOrEmpty(tokenError))
        {
            Debug.LogWarning("[FirebaseRestService] Could not refresh auth token: " + tokenError);
            yield break;
        }

        var body = "{\"fields\":{\"email\":{\"stringValue\":\"" + Escape(user.Email) + "\"},\"displayName\":{\"stringValue\":\"" + Escape(user.DisplayName) + "\"}}}";
        var url = $"https://firestore.googleapis.com/v1/projects/{_config.ProjectId}/databases/(default)/documents/users/{user.LocalId}?key={_config.ApiKey}";
        using (var request = CreateJsonRequest(url, body, "PATCH"))
        {
            request.SetRequestHeader("Authorization", "Bearer " + user.IdToken);
            yield return request.SendWebRequest();
        }
    }

    public IEnumerator SyncAppData(AuthUser user, AppDataSnapshot data)
    {
        if (user == null || string.IsNullOrEmpty(user.IdToken) || data == null)
            yield break;

        string tokenError = null;
        yield return EnsureFreshToken(user, message => tokenError = message);
        if (!string.IsNullOrEmpty(tokenError))
        {
            Debug.LogWarning("[FirebaseRestService] Could not refresh auth token: " + tokenError);
            yield break;
        }

        var escapedJson = Escape(JsonUtility.ToJson(data));
        var body = "{\"fields\":{\"json\":{\"stringValue\":\"" + escapedJson + "\"}}}";
        var url = $"https://firestore.googleapis.com/v1/projects/{_config.ProjectId}/databases/(default)/documents/appData/{user.LocalId}?key={_config.ApiKey}";
        using (var request = CreateJsonRequest(url, body, "PATCH"))
        {
            request.SetRequestHeader("Authorization", "Bearer " + user.IdToken);
            yield return request.SendWebRequest();
        }
    }

    public IEnumerator LoadAppData(AuthUser user, Action<AppDataSnapshot> onSuccess, Action<string> onError)
    {
        if (user == null || string.IsNullOrEmpty(user.IdToken))
        {
            onSuccess?.Invoke(null);
            yield break;
        }

        if (!_config.IsValid)
        {
            onError?.Invoke("Firebase config is missing.");
            yield break;
        }

        string tokenError = null;
        yield return EnsureFreshToken(user, message => tokenError = message);
        if (!string.IsNullOrEmpty(tokenError))
        {
            onError?.Invoke(tokenError);
            yield break;
        }

        var url = $"https://firestore.googleapis.com/v1/projects/{_config.ProjectId}/databases/(default)/documents/appData/{user.LocalId}?key={_config.ApiKey}";
        using (var request = UnityWebRequest.Get(url))
        {
            request.SetRequestHeader("Authorization", "Bearer " + user.IdToken);
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                var dataJson = ExtractFirestoreStringField(request.downloadHandler.text, "json");
                if (string.IsNullOrEmpty(dataJson))
                {
                    onSuccess?.Invoke(null);
                    yield break;
                }

                AppDataSnapshot snapshot = null;
                try
                {
                    snapshot = JsonUtility.FromJson<AppDataSnapshot>(dataJson);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[FirebaseRestService] Could not parse remote app data: " + ex.Message);
                }

                onSuccess?.Invoke(snapshot);
                yield break;
            }

            if (request.responseCode == 404)
            {
                onSuccess?.Invoke(null);
                yield break;
            }

            onError?.Invoke(ParseFirebaseError(request.downloadHandler.text));
        }
    }

    private IEnumerator PostAuth(string method, string body, Action<AuthUser> onSuccess, Action<string> onError)
    {
        if (!_config.IsValid)
        {
            onError?.Invoke("Firebase config is missing.");
            yield break;
        }

        var url = $"https://identitytoolkit.googleapis.com/v1/{method}?key={_config.ApiKey}";
        using (var request = CreateJsonRequest(url, body))
        {
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke(ParseFirebaseError(request.downloadHandler.text));
                yield break;
            }

            var response = JsonUtility.FromJson<AuthResponse>(request.downloadHandler.text);
            var user = new AuthUser
            {
                LocalId = response.localId,
                Email = response.email,
                DisplayName = response.displayName,
                IdToken = response.idToken,
                RefreshToken = response.refreshToken
            };
            user.SetTokenExpiryFromSeconds(response.expiresIn);
            onSuccess?.Invoke(user);
        }
    }

    private IEnumerator UpdateAccountProfile(AuthUser user, string displayName)
    {
        if (user == null || string.IsNullOrEmpty(user.IdToken) || string.IsNullOrEmpty(displayName))
            yield break;

        var body = JsonUtility.ToJson(new AccountProfileRequest(user.IdToken, displayName, true));
        var url = $"https://identitytoolkit.googleapis.com/v1/accounts:update?key={_config.ApiKey}";
        using (var request = CreateJsonRequest(url, body))
        {
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
                Debug.LogWarning("[FirebaseRestService] Could not update Firebase profile: " + ParseFirebaseError(request.downloadHandler.text));
        }
    }

    private IEnumerator EnsureFreshToken(AuthUser user, Action<string> onError)
    {
        if (user == null || !user.IsIdTokenExpiredOrNearExpiry())
            yield break;

        if (string.IsNullOrEmpty(user.RefreshToken))
        {
            onError?.Invoke("Please log in again to sync your projects.");
            yield break;
        }

        if (!_config.IsValid)
        {
            onError?.Invoke("Firebase config is missing.");
            yield break;
        }

        var body = "grant_type=refresh_token&refresh_token=" + UnityWebRequest.EscapeURL(user.RefreshToken);
        var url = $"https://securetoken.googleapis.com/v1/token?key={_config.ApiKey}";
        using (var request = new UnityWebRequest(url, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/x-www-form-urlencoded");
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke(ParseFirebaseError(request.downloadHandler.text));
                yield break;
            }

            var response = JsonUtility.FromJson<TokenRefreshResponse>(request.downloadHandler.text);
            var idToken = string.IsNullOrEmpty(response.id_token) ? response.access_token : response.id_token;
            if (string.IsNullOrEmpty(idToken))
            {
                onError?.Invoke("Could not refresh your session. Please log in again.");
                yield break;
            }

            user.IdToken = idToken;
            if (!string.IsNullOrEmpty(response.refresh_token))
                user.RefreshToken = response.refresh_token;

            user.SetTokenExpiryFromSeconds(response.expires_in);
        }
    }

    private static UnityWebRequest CreateJsonRequest(string url, string body, string method = "POST")
    {
        var request = new UnityWebRequest(url, method);
        var bytes = Encoding.UTF8.GetBytes(body);
        request.uploadHandler = new UploadHandlerRaw(bytes);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        return request;
    }

    private static string ParseFirebaseError(string json)
    {
        if (string.IsNullOrEmpty(json))
            return "Firebase request failed.";

        var code = ExtractErrorCode(json);
        switch (code)
        {
            case "EMAIL_EXISTS":
                return "This email is already registered.";
            case "EMAIL_NOT_FOUND":
                return "No account found for this email.";
            case "INVALID_PASSWORD":
            case "INVALID_LOGIN_CREDENTIALS":
                return "Email or password is incorrect.";
            case "INVALID_EMAIL":
                return "Invalid email address.";
            case "MISSING_EMAIL":
                return "Please enter your email.";
            case "MISSING_PASSWORD":
                return "Please enter your password.";
            case "WEAK_PASSWORD":
            case "WEAK_PASSWORD : Password should be at least 6 characters":
                return "Password should be at least 6 characters.";
            case "USER_DISABLED":
                return "This account has been disabled.";
            case "TOO_MANY_ATTEMPTS_TRY_LATER":
                return "Too many attempts. Try again later.";
            case "OPERATION_NOT_ALLOWED":
                return "Email/password login is not enabled in Firebase.";
            case "INVALID_OOB_CODE":
                return "Verification code is invalid. Paste the latest link or request a new email.";
            case "EXPIRED_OOB_CODE":
                return "Verification code expired. Request a new email and try again.";
            case "INVALID_ID_TOKEN":
            case "TOKEN_EXPIRED":
            case "INVALID_REFRESH_TOKEN":
            case "TOKEN_EXPIRED : Token has expired":
                return "Your session expired. Please log in again.";
            case "PERMISSION_DENIED":
                return "Firestore permission denied. Check your database rules.";
        }

        return string.IsNullOrEmpty(code) ? "Firebase request failed." : code.Replace('_', ' ').ToLowerInvariant();
    }

    private static string ExtractErrorCode(string json)
    {
        var marker = "\"message\"";
        var markerIndex = json.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
            return string.Empty;

        var colonIndex = json.IndexOf(':', markerIndex);
        if (colonIndex < 0)
            return string.Empty;

        var firstQuote = json.IndexOf('"', colonIndex + 1);
        if (firstQuote < 0)
            return string.Empty;

        var secondQuote = json.IndexOf('"', firstQuote + 1);
        if (secondQuote < 0)
            return string.Empty;

        return json.Substring(firstQuote + 1, secondQuote - firstQuote - 1);
    }

    private static string Escape(string value)
    {
        return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private static string ExtractOobCode(string codeOrLink)
    {
        if (string.IsNullOrWhiteSpace(codeOrLink))
            return string.Empty;

        var value = codeOrLink.Trim().Replace("&amp;", "&");
        var match = Regex.Match(value, "(?:[?&]|^)oobCode=([^&#\\s]+)", RegexOptions.IgnoreCase);
        if (!match.Success)
            return value;

        try
        {
            return Uri.UnescapeDataString(match.Groups[1].Value);
        }
        catch
        {
            return match.Groups[1].Value;
        }
    }

    private static string ExtractFirestoreStringField(string json, string fieldName)
    {
        if (string.IsNullOrEmpty(json))
            return string.Empty;

        var pattern = "\"" + Regex.Escape(fieldName) + "\"\\s*:\\s*\\{\\s*\"stringValue\"\\s*:\\s*\"((?:\\\\.|[^\"\\\\])*)\"";
        var match = Regex.Match(json, pattern);
        return match.Success ? JsonUnescape(match.Groups[1].Value) : string.Empty;
    }

    private static string JsonUnescape(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var builder = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (c != '\\' || i + 1 >= value.Length)
            {
                builder.Append(c);
                continue;
            }

            var escaped = value[++i];
            switch (escaped)
            {
                case '"': builder.Append('"'); break;
                case '\\': builder.Append('\\'); break;
                case '/': builder.Append('/'); break;
                case 'b': builder.Append('\b'); break;
                case 'f': builder.Append('\f'); break;
                case 'n': builder.Append('\n'); break;
                case 'r': builder.Append('\r'); break;
                case 't': builder.Append('\t'); break;
                case 'u':
                    if (i + 4 < value.Length && int.TryParse(value.Substring(i + 1, 4), System.Globalization.NumberStyles.HexNumber, null, out var codePoint))
                    {
                        builder.Append((char)codePoint);
                        i += 4;
                    }
                    break;
                default:
                    builder.Append(escaped);
                    break;
            }
        }

        return builder.ToString();
    }

    [Serializable]
    private sealed class EmailPasswordRequest
    {
        public string email;
        public string password;
        public bool returnSecureToken;

        public EmailPasswordRequest(string email, string password, bool returnSecureToken)
        {
            this.email = email;
            this.password = password;
            this.returnSecureToken = returnSecureToken;
        }
    }

    [Serializable]
    private sealed class PasswordResetRequest
    {
        public string requestType;
        public string email;

        public PasswordResetRequest(string requestType, string email)
        {
            this.requestType = requestType;
            this.email = email;
        }
    }

    [Serializable]
    private sealed class VerifyEmailRequest
    {
        public string requestType;
        public string idToken;

        public VerifyEmailRequest(string requestType, string idToken)
        {
            this.requestType = requestType;
            this.idToken = idToken;
        }
    }

    [Serializable]
    private sealed class IdTokenRequest
    {
        public string idToken;

        public IdTokenRequest(string idToken)
        {
            this.idToken = idToken;
        }
    }

    [Serializable]
    private sealed class EmailVerificationConfirmRequest
    {
        public string oobCode;
        public bool returnSecureToken;

        public EmailVerificationConfirmRequest(string oobCode, bool returnSecureToken)
        {
            this.oobCode = oobCode;
            this.returnSecureToken = returnSecureToken;
        }
    }

    [Serializable]
    private sealed class AccountProfileRequest
    {
        public string idToken;
        public string displayName;
        public bool returnSecureToken;

        public AccountProfileRequest(string idToken, string displayName, bool returnSecureToken)
        {
            this.idToken = idToken;
            this.displayName = displayName;
            this.returnSecureToken = returnSecureToken;
        }
    }

#pragma warning disable 0649
    [Serializable]
    private sealed class AuthResponse
    {
        public string localId;
        public string email;
        public string displayName;
        public string idToken;
        public string refreshToken;
        public string expiresIn;
    }

    [Serializable]
    private sealed class TokenRefreshResponse
    {
        public string access_token;
        public string expires_in;
        public string id_token;
        public string refresh_token;
    }

    [Serializable]
    private sealed class LookupResponse
    {
        public LookupUser[] users;
    }

    [Serializable]
    private sealed class LookupUser
    {
        public bool emailVerified;
    }

    [Serializable]
    private sealed class EmailActionResponse
    {
        public string email;
    }
#pragma warning restore 0649

    private sealed class FirebaseConfig
    {
        public string ApiKey;
        public string ProjectId;
        public bool IsValid => !string.IsNullOrEmpty(ApiKey) && !string.IsNullOrEmpty(ProjectId);

        public static FirebaseConfig Load()
        {
            var bundledConfig = Resources.Load<TextAsset>("Firebase/google-services");
            if (bundledConfig != null)
                return FromJson(bundledConfig.text);

            var path = System.IO.Path.Combine(Application.dataPath, "google-services.json");
            if (!System.IO.File.Exists(path))
                return new FirebaseConfig();

            return FromJson(System.IO.File.ReadAllText(path));
        }

        private static FirebaseConfig FromJson(string json)
        {
            return new FirebaseConfig
            {
                ApiKey = Extract(json, "\"current_key\": \"", "\""),
                ProjectId = Extract(json, "\"project_id\": \"", "\"")
            };
        }

        private static string Extract(string source, string start, string end)
        {
            var startIndex = source.IndexOf(start, StringComparison.Ordinal);
            if (startIndex < 0)
                return string.Empty;

            startIndex += start.Length;
            var endIndex = source.IndexOf(end, startIndex, StringComparison.Ordinal);
            return endIndex < 0 ? string.Empty : source.Substring(startIndex, endIndex - startIndex);
        }
    }
}
