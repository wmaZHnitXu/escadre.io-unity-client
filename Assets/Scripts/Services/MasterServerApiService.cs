// Scripts/Services/MasterServerApiService.cs
using UnityEngine;
using System; // Для Uri.EscapeDataString
using System.Threading.Tasks;
using System.Collections.Generic; // Для List<GameServerInfoDto>

// Если ваши DTO находятся в пространстве имен, добавьте using:
// using YourGameName.DTOs; // Например

public class MasterServerApiService : MonoBehaviour
{
    public static MasterServerApiService Instance { get; private set; }

    private MasterServerConnection connection;
    public string MasterServerUrl = "ws://localhost:5163/masterhub"; // Укажите ваш реальный URL (ws:// или wss://)
    
    // Предполагается, что SessionManager хранит токен
    // public SessionManager sessionManager; // Присвойте в Awake или через FindObjectOfType

    private string currentAccessToken; // Простое хранение токена внутри сервиса для примера

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            connection = new MasterServerConnection();
            connection.OnConnectionError += HandleConnectionError;
            // sessionManager = FindObjectOfType<SessionManager>(); // Если используете SessionManager
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private async void Start()
    {
        // Попытка подключиться при старте (без токена)
        await EnsureConnectedAsync();
    }

    private async Task EnsureConnectedAsync(string accessTokenToUse = null)
    {
        if (connection.IsConnected)
        {
            // Если уже подключены и новый токен не предоставлен, или он совпадает с текущим, ничего не делаем
            if (string.IsNullOrEmpty(accessTokenToUse) || accessTokenToUse == currentAccessToken)
            {
                return;
            }
            // Если предоставлен новый токен, отличный от текущего, нужно переподключиться
            Debug.Log("Access token changed, reconnecting...");
            await connection.DisconnectAsync(); // Отключаемся
        }

        // Используем предоставленный токен или текущий сохраненный, если есть
        string tokenForConnection = accessTokenToUse ?? currentAccessToken;
        currentAccessToken = tokenForConnection; // Обновляем текущий токен

        string urlToConnect = MasterServerUrl;
        if (!string.IsNullOrEmpty(tokenForConnection))
        {
            // Добавляем токен в query string
            char separator = urlToConnect.Contains("?") ? '&' : '?';
            urlToConnect += $"{separator}access_token={Uri.EscapeDataString(tokenForConnection)}";
        }
        
        Debug.Log($"Attempting to connect to: {urlToConnect}");
        await connection.ConnectAsync(urlToConnect);
    }


    private void HandleConnectionError(string errorMessage)
    {
        Debug.LogError($"MasterServerApiService: Connection Error: {errorMessage}");
        // TODO: Показать пользователю сообщение об ошибке соединения
        // uiManager.ShowErrorPopup($"Connection error: {errorMessage}");
    }

    // --- Методы API ---

    public async Task<(bool success, TokenResponseDto response, string errorMessage)> GetAnonymousTokenAsync(string nickname)
    {
        await EnsureConnectedAsync(); // Убедимся, что подключены (без токена или с текущим)
        if (!connection.IsConnected) return (false, null, "Failed to connect to server.");

        var requestDto = new AnonymousTokenRequestDto { Nickname = nickname };
        var result = await connection.InvokeHubMethodAsync<TokenResponseDto>("GetAnonymousToken", requestDto);

        if (result.success && result.response != null)
        {
            currentAccessToken = result.response.AccessToken; // Сохраняем анонимный токен
            // Для анонимного токена переподключение с токеном может быть излишним,
            // т.к. он уже использовался для этого вызова, если сервер его требует.
            // Но если последующие вызовы требуют токен в URL, то это нужно.
            // await EnsureConnectedAsync(currentAccessToken); // Переподключиться с этим токеном
        }
        return result;
    }

    public async Task<(bool success, RegistrationResultDto response, string errorMessage)> RegisterAsync(string email, string nickname, string password)
    {
        await EnsureConnectedAsync();
        if (!connection.IsConnected) return (false, null, "Failed to connect to server.");
        
        var requestDto = new RegisterRequestDto { Email = email, Nickname = nickname, Password = password };
        return await connection.InvokeHubMethodAsync<RegistrationResultDto>("Register", requestDto);
    }

    public async Task<(bool success, LoginResponseDto response, string errorMessage)> LoginAsync(string email, string password)
    {
        // Для логина всегда подключаемся без токена сначала
        await connection.DisconnectAsync(); // Гарантированно отключаемся, если были подключены
        currentAccessToken = null; // Сбрасываем старый токен
        await EnsureConnectedAsync(); // Подключаемся без токена

        if (!connection.IsConnected) return (false, null, "Failed to connect to server for login.");

        var requestDto = new LoginRequestDto { Identifier = email, Password = password };
        var result = await connection.InvokeHubMethodAsync<LoginResponseDto>("Login", requestDto);

        if (result.success && result.response != null && !string.IsNullOrEmpty(result.response.AccessToken))
        {
            Debug.Log("Login successful, will use new access token for subsequent calls.");
            currentAccessToken = result.response.AccessToken; // Сохраняем токен
            // Переподключаться не обязательно сразу, токен будет использован при следующем EnsureConnectedAsync
            // или если хаб-методы требуют токен, передаваемый НЕ через URL при установке соединения.
            // Но для SignalR с JWT через QueryString, да, нужно переподключиться, чтобы URL содержал токен.
            await EnsureConnectedAsync(currentAccessToken); // Переподключаемся с новым токеном
        }
        return result;
    }

    public async Task<(bool success, PasswordResetRequestResultDto response, string errorMessage)> RequestPasswordResetAsync(string email)
    {
        await EnsureConnectedAsync();
        if (!connection.IsConnected) return (false, null, "Failed to connect to server.");
        
        // На сервере метод RequestPasswordReset(string email)
        return await connection.InvokeHubMethodAsync<PasswordResetRequestResultDto>("RequestPasswordReset", email);
    }

    public async Task<(bool success, PasswordResetResultDto response, string errorMessage)> ResetPasswordAsync(string userId, string token, string newPassword)
    {
        await EnsureConnectedAsync();
        if (!connection.IsConnected) return (false, null, "Failed to connect to server.");
        
        // На сервере метод ResetPassword(string userId, string code, string newPassword)
        // 'token' с клиента здесь это 'code' для сервера
        return await connection.InvokeHubMethodAsync<PasswordResetResultDto>("ResetPassword", userId, token, newPassword);
    }

    public async Task<(bool success, List<GameServerInfoDto> response, string errorMessage)> GetServerListAsync()
    {
        // Этот метод требует авторизации, EnsureConnectedAsync попытается использовать currentAccessToken
        await EnsureConnectedAsync(); 
        if (!connection.IsConnected)
        {
             // Если currentAccessToken пуст, соединение будет без токена, и сервер вернет ошибку авторизации
            Debug.LogWarning("Attempting GetServerList. Connection might not be authorized if no token is present.");
            return (false, null, "Not connected or not authorized.");
        }
        return await connection.InvokeHubMethodAsync<List<GameServerInfoDto>>("GetServerList");
    }

    public async Task<(bool success, string errorMessage)> LogoutAsync()
    {
        // Logout может требовать токен, чтобы сервер инвалидировал сессию/refresh token
        await EnsureConnectedAsync(); 
        if (!connection.IsConnected)
        {
            Debug.LogWarning("Attempting to logout while not connected. Clearing local token.");
            currentAccessToken = null;
            // sessionManager?.ClearSession();
            return (true, null);
        }

        var result = await connection.SendHubMethodAsync("Logout"); // Logout на сервере может быть void

        if (result.success)
        {
            Debug.Log("Logout successful on server. Clearing local token and reconnecting anonymously.");
            currentAccessToken = null;
            // sessionManager?.ClearSession();
            await EnsureConnectedAsync(); // Переподключаемся без токена (в анонимном режиме)
        }
        return result;
    }

    private async void OnApplicationQuit()
    {
        if (connection != null && connection.IsConnected)
        {
            await connection.DisconnectAsync();
        }
    }
}

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