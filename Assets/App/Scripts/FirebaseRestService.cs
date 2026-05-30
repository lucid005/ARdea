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
        onSuccess?.Invoke(createdUser);
    }

    public IEnumerator SendPasswordReset(string email, Action onSuccess, Action<string> onError)
    {
        var body = JsonUtility.ToJson(new PasswordResetRequest("PASSWORD_RESET", email));
        var url = $"https://identitytoolkit.googleapis.com/v1/accounts:sendOobCode?key={_config.ApiKey}";
        using (var request = CreateJsonRequest(url, body))
        {
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success)
                onSuccess?.Invoke();
            else
                onError?.Invoke(ParseFirebaseError(request.downloadHandler.text));
        }
    }

    public IEnumerator SaveUserProfile(AuthUser user)
    {
        if (user == null || string.IsNullOrEmpty(user.IdToken))
            yield break;

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
            onSuccess?.Invoke(new AuthUser
            {
                LocalId = response.localId,
                Email = response.email,
                DisplayName = response.displayName,
                IdToken = response.idToken,
                RefreshToken = response.refreshToken
            });
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
            case "INVALID_ID_TOKEN":
            case "TOKEN_EXPIRED":
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
    private sealed class AuthResponse
    {
        public string localId;
        public string email;
        public string displayName;
        public string idToken;
        public string refreshToken;
    }

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
