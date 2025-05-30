// Scripts/Services/MasterServerApiService.cs
using UnityEngine;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;

public class MasterServerApiService : MonoBehaviour
{
    public static MasterServerApiService Instance { get; private set; }

    private MasterServerConnection connection;
    public string MasterServerUrl = "http://localhost:5076/masterhub"; // Укажите ваш URL
    private TaskCompletionSource<(bool success, RegistrationResultDto response, string errorMessage)> _registrationTcs;
    private TaskCompletionSource<(bool success, LoginResponseDto response, string errorMessage)> _loginTcs;

    private bool isRefreshingToken = false; // Флаг, чтобы избежать одновременных запросов на обновление токена

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            connection = new MasterServerConnection();
            connection.OnConnectionError += HandleConnectionError;

            // Подписка на события регистрации из MasterServerConnection
            connection.OnRegistrationSuccess += HandleRegistrationSuccess;
            connection.OnRegistrationFailed += HandleRegistrationFailed;
            connection.OnLoginSuccess += HandleLoginSuccess;
            connection.OnLoginFailed += HandleLoginFailed;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private async void Start()
    {
        //При старте приложения пытаемся восстановить сессию, если есть RefreshToken
        if (SessionManager.Instance != null && !string.IsNullOrEmpty(SessionManager.Instance.RefreshToken))
        {
            Debug.Log("Found stored refresh token. Attempting to refresh session on start...");
            await TryRefreshTokenAsync(); // Это также вызовет EnsureConnectedAsync с новым токеном, если успешно
        }
        else
        {
            await EnsureConnectedAsync(); // Обычное подключение без токена
        }
        Debug.LogWarning("[MasterServerApiService.Start] Automatic connection on start is TEMPORARILY DISABLED for debugging.");
    }

    private async Task EnsureConnectedAsync(string accessTokenToUse = null, bool forceDisconnect = false)
    {
        // Логи для отслеживания состояния
        Debug.Log($"[EnsureConnectedAsync ENTRY] IsConnected: {connection.IsConnected}, forceDisconnect: {forceDisconnect}, accessTokenToUse: '{accessTokenToUse ?? "NULL"}'");

        if (forceDisconnect && connection.IsConnected)
        {
            Debug.Log("[EnsureConnectedAsync] Force disconnecting...");
            await connection.DisconnectAsync();
            Debug.Log($"[EnsureConnectedAsync] After DisconnectAsync. IsConnected: {connection.IsConnected}");
        }

        // Если не подключены (или только что отключились принудительно)
        if (!connection.IsConnected)
        {
            Debug.Log("[EnsureConnectedAsync] Not connected. Attempting to connect...");
            string tokenForProvider = accessTokenToUse ?? SessionManager.Instance?.AccessToken;
            // Убедимся, что передаем null, если токен пустой или пробельный
            if (string.IsNullOrWhiteSpace(tokenForProvider)) 
            {
                tokenForProvider = null;
            }
            Debug.Log($"[EnsureConnectedAsync] Token for AccessTokenProvider: '{tokenForProvider ?? "NULL"}'");
            await connection.ConnectAsync(MasterServerUrl, tokenForProvider);
        }
        else
        {
            // Уже подключены, и не было forceDisconnect, или forceDisconnect был, но мы все равно подключены (что странно)
            // Возможно, нужно проверить, изменился ли токен, если не было forceDisconnect
            string currentTokenOnConnection = SessionManager.Instance?.AccessToken; // Предположим, что соединение использует этот токен
            if (accessTokenToUse != null && accessTokenToUse != currentTokenOnConnection)
            {
                Debug.LogWarning($"[EnsureConnectedAsync] Already connected, but requested token '{accessTokenToUse}' differs from current session token '{currentTokenOnConnection}'. Reconnecting with new token.");
                await connection.DisconnectAsync();
                await connection.ConnectAsync(MasterServerUrl, accessTokenToUse);
            }
            else
            {
                Debug.Log("[EnsureConnectedAsync] Already connected and token state seems consistent (or no new token specified). No action taken.");
            }
        }
        Debug.Log($"[EnsureConnectedAsync EXIT] IsConnected: {connection.IsConnected}");
    }

    private void HandleConnectionError(string errorMessage)
    {
        Debug.LogError($"MasterServerApiService: Connection Error: {errorMessage}");
        // TODO: Показать пользователю сообщение об ошибке соединения
    }
    private void HandleRegistrationSuccess(string serverMessage) // Сервер шлет простое сообщение
    {
        if (_registrationTcs == null || _registrationTcs.Task.IsCompleted) return;

        // На сервере в MasterHub.Register в случае успеха отправляется:
        // await Clients.Caller.SendAsync("RegistrationSuccess", "User registered successfully. Please check your email to confirm your account.");
        // UserId не передается в этом сообщении.
        // Если он нужен на клиенте сразу после регистрации, серверный метод должен быть изменен.
        // Пока что создаем DTO без UserId.
        var resultDto = new RegistrationResultDto 
        { 
            IsSuccess = true, 
            UserId = null, // UserId не приходит от сервера в этом событии
            Errors = null
        };
        _registrationTcs.TrySetResult((true, resultDto, serverMessage));
    }
    private void HandleRegistrationFailed(List<string> errors) // Сервер шлет список ошибок
    {
        if (_registrationTcs == null || _registrationTcs.Task.IsCompleted) return;

        var resultDto = new RegistrationResultDto 
        { 
            IsSuccess = false, 
            UserId = null,
            Errors = errors?.ToArray() // Преобразуем List<string> в string[]
        };
        _registrationTcs.TrySetResult((false, resultDto, string.Join("; ", errors ?? new List<string>())));
    }
    private void HandleLoginSuccess(LoginResponseDto loginResponse)
    {
        if (_loginTcs == null || _loginTcs.Task.IsCompleted) return;
        _loginTcs.TrySetResult((true, loginResponse, null));
    }

    private void HandleLoginFailed(string errorMessage)
    {
        if (_loginTcs == null || _loginTcs.Task.IsCompleted) return;
        _loginTcs.TrySetResult((false, null, errorMessage));
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
        await EnsureConnectedAsync(); 
        if (!connection.IsConnected) return (false, null, "Failed to connect to server.");

        var requestDto = new AnonymousTokenRequestDto { Nickname = nickname };
        Debug.Log($"[GetAnonymousTokenAsync] Sending Nickname in DTO: '{requestDto.Nickname}'");

        // Используем InvokeHubMethodAsync, который передает аргументы
        var (callSuccess, actualResponse, callError) = await connection.InvokeHubMethodAsync<TokenResponseDto>("GetAnonymousToken", requestDto);

        if (callSuccess && actualResponse != null)
        {
            Debug.Log($"[GetAnonymousTokenAsync] GetAnonymousToken SUCCEEDED. AccessToken: {actualResponse.AccessToken}");
            
            UserSessionData anonUserData = new UserSessionData { UserId = "anonymous_" + nickname, Nickname = nickname };
            SessionManager.Instance.CreateSession(
                actualResponse.AccessToken,
                actualResponse.AccessTokenExpiration,
                actualResponse.NewRefreshToken, 
                anonUserData
            );
            await EnsureConnectedAsync(SessionManager.Instance.AccessToken, forceDisconnect: true); 
            return (true, actualResponse, null);
        }
        else
        {
            Debug.LogError($"[GetAnonymousTokenAsync] GetAnonymousToken FAILED. Error: {callError}");
            return (false, null, callError);
        }
    }
    
    public async Task<(bool success, RegistrationResultDto response, string errorMessage)> RegisterAsync(string email, string nickname, string password)
    {
        await EnsureConnectedAsync(forceDisconnect: true);
        if (!connection.IsConnected) 
        {
            return (false, new RegistrationResultDto { IsSuccess = false, Errors = new[]{"Connection failed."} }, "Failed to connect to server.");
        }

        _registrationTcs = new TaskCompletionSource<(bool success, RegistrationResultDto response, string errorMessage)>();
        
        var requestDto = new RegisterRequestDto { Email = email, Nickname = nickname, Password = password };
        
        // Используем SendHubMethodAsync, так как серверный Register не возвращает Task<T>
        var (sendSuccess, sendError) = await connection.SendHubMethodAsync("Register", requestDto);

        if (!sendSuccess)
        {
            // Если сам вызов SendAsync провалился (например, отвалилось соединение в момент вызова)
            // _registrationTcs.TrySetCanceled(); // или TrySetException, если это более уместно
            return (false, new RegistrationResultDto { IsSuccess = false, Errors = new[]{sendError ?? "Failed to send request."} }, sendError ?? "Failed to send registration request.");
        }

        // Ожидаем результат из HandleRegistrationSuccess или HandleRegistrationFailed
        // Можно добавить таймаут для _registrationTcs.Task
        try
        {
            // Пример с таймаутом в 15 секунд
            var completedTask = await Task.WhenAny(_registrationTcs.Task, Task.Delay(TimeSpan.FromSeconds(15)));
            if (completedTask == _registrationTcs.Task)
            {
                return await _registrationTcs.Task;
            }
            else
            {
                Debug.LogError("Registration request timed out.");
                _registrationTcs.TrySetCanceled(); // Отменяем TCS, чтобы избежать утечек, если он не завершился
                return (false, new RegistrationResultDto { IsSuccess = false, Errors = new[]{"Request timed out."} }, "Registration request timed out.");
            }
        }
        catch (TaskCanceledException)
        {
            Debug.LogWarning("Registration task was cancelled (likely due to timeout or external cancellation).");
            return (false, new RegistrationResultDto { IsSuccess = false, Errors = new[]{"Request cancelled."} }, "Registration request cancelled.");
        }
    }

   public async Task<(bool success, LoginResponseDto response, string errorMessage)> LoginAsync(string identifier, string password) // изменил email на identifier для универсальности
    {
        await EnsureConnectedAsync(forceDisconnect: true); // Логин - без предыдущего токена
        if (!connection.IsConnected) 
        {
            return (false, null, "Failed to connect to server for login.");
        }

        _loginTcs = new TaskCompletionSource<(bool success, LoginResponseDto response, string errorMessage)>();
        
        var requestDto = new LoginRequestDto { Identifier = identifier, Password = password };
        Debug.Log($"[LoginAsync] Attempting to login with Identifier: '{identifier}'");

        // Используем SendHubMethodAsync, так как серверный Login не возвращает Task<T>
        var (sendSuccess, sendError) = await connection.SendHubMethodAsync("Login", requestDto);

        if (!sendSuccess)
        {
            // Если сам вызов SendAsync провалился
            return (false, null, sendError ?? "Failed to send login request.");
        }

        // Ожидаем результат из HandleLoginSuccess или HandleLoginFailed
        try
        {
            var completedTask = await Task.WhenAny(_loginTcs.Task, Task.Delay(TimeSpan.FromSeconds(15))); // Таймаут 15 секунд
            if (completedTask == _loginTcs.Task)
            {
                var result = await _loginTcs.Task; // Получаем результат от TCS

                // Логика создания сессии, если логин успешен
                if (result.success && result.response != null && !string.IsNullOrEmpty(result.response.AccessToken) && SessionManager.Instance != null)
                {
                    Debug.Log("Login successful, creating session.");
                    UserSessionData userData = new UserSessionData {
                        UserId = result.response.UserId,
                        Nickname = result.response.Nickname, // Nickname должен приходить от сервера в LoginResponseDto
                        Email = (identifier.Contains("@") ? identifier : SessionManager.Instance.CurrentUser?.Email) // Пытаемся определить Email
                    };
                    SessionManager.Instance.CreateSession(
                        result.response.AccessToken,
                        result.response.AccessTokenExpiration,
                        result.response.RefreshToken, // Используем RefreshToken из ответа
                        userData
                    );
                    await EnsureConnectedAsync(SessionManager.Instance.AccessToken, forceDisconnect: true); // Переподключаемся с новым токеном
                }
                return result;
            }
            else
            {
                Debug.LogError("Login request timed out.");
                _loginTcs.TrySetCanceled();
                return (false, null, "Login request timed out.");
            }
        }
        catch (TaskCanceledException)
        {
            Debug.LogWarning("Login task was cancelled.");
            return (false, null, "Login request cancelled.");
        }
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
    private void OnDestroy()
    {
        if (connection != null)
        {
            connection.OnConnectionError -= HandleConnectionError; // Если вы его еще где-то используете
            connection.OnRegistrationSuccess -= HandleRegistrationSuccess;
            connection.OnRegistrationFailed -= HandleRegistrationFailed;
            connection.OnLoginSuccess -= HandleLoginSuccess;
            connection.OnLoginFailed -= HandleLoginFailed;
            // Отписка от других событий, если они есть
        }

        _loginTcs?.TrySetCanceled();
        // Если _registrationTcs может остаться "висеть" при уничтожении объекта, его стоит отменить
        _registrationTcs?.TrySetCanceled();
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