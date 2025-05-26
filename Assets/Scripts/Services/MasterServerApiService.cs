// Scripts/Services/MasterServerApiService.cs
using UnityEngine;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;

public class MasterServerApiService : MonoBehaviour
{
    public static MasterServerApiService Instance { get; private set; }

    private MasterServerConnection connection;
    public string MasterServerUrl = "ws://localhost:5163/masterhub"; // Укажите ваш URL

    private bool isRefreshingToken = false; // Флаг, чтобы избежать одновременных запросов на обновление токена

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            connection = new MasterServerConnection();
            connection.OnConnectionError += HandleConnectionError;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private async void Start()
    {
        // При старте приложения пытаемся восстановить сессию, если есть RefreshToken
        if (SessionManager.Instance != null && !string.IsNullOrEmpty(SessionManager.Instance.RefreshToken))
        {
            Debug.Log("Found stored refresh token. Attempting to refresh session on start...");
            await TryRefreshTokenAsync(); // Это также вызовет EnsureConnectedAsync с новым токеном, если успешно
        }
        else
        {
            await EnsureConnectedAsync(); // Обычное подключение без токена
        }
    }

    private async Task EnsureConnectedAsync(string accessTokenToUse = null, bool forceDisconnect = false)
    {
        string tokenForConnection = accessTokenToUse ?? SessionManager.Instance?.AccessToken;

        if (connection.IsConnected)
        {
            if (forceDisconnect || (SessionManager.Instance != null && tokenForConnection != SessionManager.Instance.AccessToken && !string.IsNullOrEmpty(SessionManager.Instance.AccessToken))) // Если токен изменился (например, после логина/логаута)
            {
                Debug.Log("Token state changed or forced disconnect. Reconnecting...");
                await connection.DisconnectAsync();
            }
            else if (!forceDisconnect) // Если уже подключены и токен не менялся, и не было форс-дисконнекта
            {
                return;
            }
        }
        
        // Если после возможных дисконнектов мы всё еще не подключены или был форс-дисконнект
        if (!connection.IsConnected || forceDisconnect) {
            string urlToConnect = MasterServerUrl;
            if (!string.IsNullOrEmpty(tokenForConnection))
            {
                char separator = urlToConnect.Contains("?") ? '&' : '?';
                urlToConnect += $"{separator}access_token={Uri.EscapeDataString(tokenForConnection)}";
            }
            
            Debug.Log($"Attempting to connect to: {urlToConnect}");
            await connection.ConnectAsync(urlToConnect);
        }
    }

    private void HandleConnectionError(string errorMessage)
    {
        Debug.LogError($"MasterServerApiService: Connection Error: {errorMessage}");
        // TODO: Показать пользователю сообщение об ошибке соединения
    }

    // --- Логика обновления токена ---
    private async Task<bool> TryRefreshTokenAsync()
    {
        if (SessionManager.Instance == null || string.IsNullOrEmpty(SessionManager.Instance.RefreshToken))
        {
            Debug.LogWarning("No refresh token available to refresh session.");
            return false;
        }

        if (isRefreshingToken)
        {
            Debug.LogWarning("Token refresh already in progress.");
            // Можно добавить ожидание завершения текущего рефреша, если это критично
            await Task.Delay(100); // Простое ожидание
            return SessionManager.Instance.IsUserLoggedIn; // Возвращаем текущее состояние
        }

        isRefreshingToken = true;
        Debug.Log("Attempting to refresh access token...");

        var requestDto = new RefreshTokenRequestDto { RefreshToken = SessionManager.Instance.RefreshToken };
        
        // Для вызова RefreshToken нам не нужен текущий AccessToken в URL,
        // поэтому подключаемся без него, если еще не подключены.
        // Либо используем уже существующее соединение, если оно есть.
        if(!connection.IsConnected) await EnsureConnectedAsync(forceDisconnect: true); // Подключаемся без токена для запроса рефреша

        if (!connection.IsConnected)
        {
            Debug.LogError("Cannot refresh token: not connected to server.");
            isRefreshingToken = false;
            return false;
        }

        var (success, response, error) = await connection.InvokeHubMethodAsync<TokenResponseDto>("RefreshToken", requestDto);

        if (success && response != null)
        {
            Debug.Log("Token refreshed successfully.");
            // Обновляем только AccessToken и его время жизни. Пользователь остается тот же.
            // RefreshToken тоже может обновиться на сервере, и сервер его вернет.
            SessionManager.Instance.CreateSession( // Используем CreateSession для обновления и RefreshToken, если он новый
                response.AccessToken,
                response.AccessTokenExpiration,
                string.IsNullOrEmpty(response.NewRefreshToken) ? SessionManager.Instance.RefreshToken : response.NewRefreshToken, // Используем новый RT, если есть
                SessionManager.Instance.CurrentUser // Данные пользователя не меняются при рефреше токена
            );
            await EnsureConnectedAsync(SessionManager.Instance.AccessToken, forceDisconnect: true); // Переподключаемся с новым access токеном
            isRefreshingToken = false;
            return true;
        }
        else
        {
            Debug.LogError($"Failed to refresh token: {error}. Clearing session.");
            SessionManager.Instance.ClearSession(); // Если не удалось обновить, разлогиниваем
            await EnsureConnectedAsync(forceDisconnect: true); // Переподключаемся без токена
            // TODO: Перенаправить на экран логина
            // UIManager.Instance.SwitchToScreen(UIScreenType.RegisteredLogin);
            isRefreshingToken = false;
            return false;
        }
    }

    // Обертка для вызова методов, требующих авторизации
    private async Task<(bool success, T response, string errorMessage)> InvokeAuthorizedHubMethodAsync<T>(string methodName, params object[] args)
    {
        if (SessionManager.Instance == null)
            return (false, default(T), "SessionManager not available.");

        if (SessionManager.Instance.IsAccessTokenExpiredOrNearingExpiration())
        {
            bool refreshed = await TryRefreshTokenAsync();
            if (!refreshed && !SessionManager.Instance.IsUserLoggedIn) // Если не удалось обновить и мы разлогинены
            {
                return (false, default(T), "Session expired or token refresh failed. Please login again.");
            }
        }
        // После попытки рефреша (или если он не нужен), EnsureConnectedAsync должен быть вызван с актуальным токеном
        // (он вызывается внутри TryRefreshTokenAsync или при обычном старте/логине)
        // Но для уверенности, перед каждым авторизованным вызовом, убедимся, что подключение актуально с текущим токеном сессии
        await EnsureConnectedAsync(SessionManager.Instance.AccessToken);


        if (!connection.IsConnected) return (false, default(T), "Not connected to server for authorized call.");
        if (!SessionManager.Instance.IsUserLoggedIn && methodName != "GetAnonymousToken") // Проверяем логин для всех, кроме получения анонимного токена
        {
             Debug.LogWarning($"Attempting to call authorized method '{methodName}' while not logged in.");
             return (false, default(T), "User not logged in.");
        }

        return await connection.InvokeHubMethodAsync<T>(methodName, args);
    }
     private async Task<(bool success, string errorMessage)> SendAuthorizedHubMethodAsync(string methodName, params object[] args)
    {
        if (SessionManager.Instance == null)
            return (false, "SessionManager not available.");

        if (SessionManager.Instance.IsAccessTokenExpiredOrNearingExpiration())
        {
            bool refreshed = await TryRefreshTokenAsync();
            if (!refreshed && !SessionManager.Instance.IsUserLoggedIn)
            {
                return (false, "Session expired or token refresh failed. Please login again.");
            }
        }
        await EnsureConnectedAsync(SessionManager.Instance.AccessToken);

        if (!connection.IsConnected) return (false, "Not connected to server for authorized call.");
        if (!SessionManager.Instance.IsUserLoggedIn)
        {
             Debug.LogWarning($"Attempting to send authorized method '{methodName}' while not logged in.");
             return (false, "User not logged in.");
        }
        return await connection.SendHubMethodAsync(methodName, args);
    }


    // --- Методы API ---

    public async Task<(bool success, TokenResponseDto response, string errorMessage)> GetAnonymousTokenAsync(string nickname)
    {
        await EnsureConnectedAsync(forceDisconnect:true); // Анонимный токен - подключаемся без существующего токена
        if (!connection.IsConnected) return (false, null, "Failed to connect to server.");

        var requestDto = new AnonymousTokenRequestDto { Nickname = nickname };
        var result = await connection.InvokeHubMethodAsync<TokenResponseDto>("GetAnonymousToken", requestDto);

        if (result.success && result.response != null && SessionManager.Instance != null)
        {
            // Анонимный пользователь - UserId и Nickname могут быть специфичными
            UserSessionData anonUserData = new UserSessionData { UserId = "anonymous_" + nickname, Nickname = nickname };
            SessionManager.Instance.CreateSession(
                result.response.AccessToken,
                result.response.AccessTokenExpiration,
                result.response.NewRefreshToken, // Анонимы тоже могут иметь RefreshToken
                anonUserData
            );
            // Для анонимного токена переподключение с токеном, если он используется для последующих вызовов.
            await EnsureConnectedAsync(SessionManager.Instance.AccessToken, forceDisconnect: true);
        }
        return result;
    }

    public async Task<(bool success, RegistrationResultDto response, string errorMessage)> RegisterAsync(string email, string nickname, string password)
    {
        await EnsureConnectedAsync(forceDisconnect: true); // Регистрация - обычно без токена
        if (!connection.IsConnected) return (false, null, "Failed to connect to server.");
        
        var requestDto = new RegisterRequestDto { Email = email, Nickname = nickname, Password = password };
        return await connection.InvokeHubMethodAsync<RegistrationResultDto>("Register", requestDto);
    }

    public async Task<(bool success, LoginResponseDto response, string errorMessage)> LoginAsync(string email, string password)
    {
        await EnsureConnectedAsync(forceDisconnect: true); // Логин - без предыдущего токена
        if (!connection.IsConnected) return (false, null, "Failed to connect to server for login.");

        var requestDto = new LoginRequestDto { Identifier = email, Password = password };
        var result = await connection.InvokeHubMethodAsync<LoginResponseDto>("Login", requestDto);

        if (result.success && result.response != null && !string.IsNullOrEmpty(result.response.AccessToken) && SessionManager.Instance != null)
        {
            Debug.Log("Login successful, creating session.");
            UserSessionData userData = new UserSessionData {
                // Предполагаем, что LoginResponseDto содержит UserId и Nickname
                // Если нет, их нужно получить из другого источника или оставить null
                UserId = result.response.UserId, // Убедитесь, что это поле есть в вашем LoginResponseDto
                Nickname = result.response.Nickname ?? email.Split('@')[0], // Предполагаем вложенный UserInfoDto или берем из email
                Email = email
            };
            SessionManager.Instance.CreateSession(
                result.response.AccessToken,
                result.response.AccessTokenExpiration,
                result.response.NewRefreshToken,
                userData
            );
            await EnsureConnectedAsync(SessionManager.Instance.AccessToken, forceDisconnect: true); // Переподключаемся с новым токеном
        }
        return result;
    }

    public async Task<(bool success, PasswordResetRequestResultDto response, string errorMessage)> RequestPasswordResetAsync(string email)
    {
        await EnsureConnectedAsync(forceDisconnect:true); // Запрос на сброс - без токена
        if (!connection.IsConnected) return (false, null, "Failed to connect to server.");
        
        return await connection.InvokeHubMethodAsync<PasswordResetRequestResultDto>("RequestPasswordReset", email);
    }

    public async Task<(bool success, PasswordResetResultDto response, string errorMessage)> ResetPasswordAsync(string userId, string token, string newPassword)
    {
        await EnsureConnectedAsync(forceDisconnect:true); // Сброс - без токена
        if (!connection.IsConnected) return (false, null, "Failed to connect to server.");
        
        return await connection.InvokeHubMethodAsync<PasswordResetResultDto>("ResetPassword", userId, token, newPassword);
    }

    public async Task<(bool success, List<GameServerInfoDto> response, string errorMessage)> GetServerListAsync()
    {
        // Используем обертку для авторизованных вызовов
        return await InvokeAuthorizedHubMethodAsync<List<GameServerInfoDto>>("GetServerList");
    }

    public async Task<(bool success, string errorMessage)> LogoutAsync()
    {
        // Используем обертку (если Logout требует быть авторизованным для инвалидации серверной сессии)
        // или вызываем напрямую, если он публичный, но тогда ClearSession нужно делать по-другому.
        // Предположим, Logout на сервере требует авторизации для инвалидации RefreshToken.
        var result = await SendAuthorizedHubMethodAsync("Logout");

        // Вне зависимости от успеха на сервере (может быть уже невалидный токен), чистим локальную сессию.
        if(SessionManager.Instance != null) SessionManager.Instance.ClearSession();
        await EnsureConnectedAsync(forceDisconnect: true); // Переподключаемся без токена

        return result; // Возвращаем результат операции с сервером
    }

    private async void OnApplicationQuit()
    {
        if (connection != null && connection.IsConnected)
        {
            await connection.DisconnectAsync();
        }
    }
}

// Не забудьте DTO для RefreshTokenRequestDto:
// public class RefreshTokenRequestDto { public string RefreshToken { get; set; } }

// Убедитесь, что LoginResponseDto содержит UserId и, возможно, вложенный UserInfoDto с Nickname,
// если вы хотите это сохранять в UserSessionData при логине.
// public class LoginResponseDto : TokenResponseDto
// {
//     public string UserId { get; set; } // Пример
//     public UserInfoDto User { get; set; } // Пример
// }
// public class UserInfoDto { public string Nickname { get; set; } /* ... */ }

// Убедитесь, что все эти DTO определены в вашем проекте Unity (папка Scripts/DTOs):
// - AnonymousTokenRequestDto
// - RegisterRequestDto
// - LoginRequestDto
// - TokenResponseDto
// - LoginResponseDto (может наследоваться от TokenResponseDto)
// - GameServerInfoDto
// - RegistrationResultDto
// - PasswordResetRequestResultDto
// - PasswordResetResultDto
// - (AuthResultDto, EmailConfirmationResultDto - если понадобятся для других методов)