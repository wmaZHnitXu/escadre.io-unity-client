// Scripts/Services/MasterServerApiService.cs
using UnityEngine;
using System;
using System.Threading.Tasks;
using System.Collections.Generic; // Для List

public class MasterServerApiService : MonoBehaviour
{
    public static MasterServerApiService Instance { get; private set; }

    // Флаги для имитации различных состояний сервера или ошибок
    public bool SimulateLoginError = false;
    public bool SimulateRegistrationError = false;
    public bool SimulateRegistrationRequiresEmailConfirmation = true; // По умолчанию имитируем, что нужна активация
    public bool SimulateAnonymousLoginError = false;
    public bool SimulateRefreshTokenError = false;
    public bool SimulateRequestPasswordResetAccountNotFound = false;
    public bool SimulateResetPasswordError = false;
    public bool SimulateGetServerListError = false; // Для методов, требующих авторизации

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // Имитация задержки сети
    private async Task SimulateNetworkDelay(int milliseconds = 500)
    {
        await Task.Delay(milliseconds);
    }

    // --- Заглушки методов API ---

    public async Task<(bool success, TokenResponseDto response, string errorMessage)> GetAnonymousTokenAsync(string nickname)
    {
        await SimulateNetworkDelay();
        Debug.Log($"[STUB] MasterServerApiService: GetAnonymousTokenAsync called with Nickname: {nickname}");

        if (SimulateAnonymousLoginError || string.IsNullOrWhiteSpace(nickname) || nickname.ToLower() == "error")
        {
            return (false, null, "Stub: Anonymous login failed (simulated error or invalid nickname).");
        }

        var mockResponse = new TokenResponseDto
        {
            AccessToken = "fake_ANONYMOUS_access_token_" + Guid.NewGuid().ToString().Substring(0, 8),
            AccessTokenExpiration = DateTime.UtcNow.AddHours(1),
            NewRefreshToken = "fake_ANONYMOUS_refresh_token_" + Guid.NewGuid().ToString().Substring(0, 8)
            // UserId не устанавливаем явно, SessionManager может создать UserSessionData с userId = "anonymous_" + nickname
        };

        if (SessionManager.Instance != null)
        {
            UserSessionData anonUserData = new UserSessionData { UserId = "anonymous_" + nickname, Nickname = nickname, Email = null };
            SessionManager.Instance.CreateSession(mockResponse.AccessToken, mockResponse.AccessTokenExpiration, mockResponse.NewRefreshToken, anonUserData);
        }
        return (true, mockResponse, null);
    }

    public async Task<(bool success, RegistrationResultDto response, string errorMessage)> RegisterAsync(string email, string nickname, string password)
    {
        await SimulateNetworkDelay();
        Debug.Log($"[STUB] MasterServerApiService: RegisterAsync called with Email: {email}, Nickname: {nickname}");

        if (SimulateRegistrationError || email.ToLower() == "error@example.com")
        {
            return (false, new RegistrationResultDto { IsSuccess = false, Errors = new[] { "Stub: Registration failed (simulated error)." } }, "Stub: Registration failed (simulated error).");
        }

        // Имитируем успешную регистрацию
        var resultDto = new RegistrationResultDto
        {
            IsSuccess = true,
            UserId = "stub_user_id_" + Guid.NewGuid().ToString().Substring(0,5)
            // Errors будет null или пустым
        };
        
        // Сообщение для UI, если требуется подтверждение
        string successMessage = SimulateRegistrationRequiresEmailConfirmation
            ? "Stub: Registration successful. Please check your email to confirm."
            : "Stub: Registration successful. You can now login.";

        // Не создаем сессию здесь, пользователь должен будет залогиниться или подтвердить email
        return (true, resultDto, successMessage); // Передаем сообщение об успехе (или null если не нужно)
    }

    public async Task<(bool success, LoginResponseDto response, string errorMessage)> LoginAsync(string email, string password)
    {
        await SimulateNetworkDelay();
        Debug.Log($"[STUB] MasterServerApiService: LoginAsync called with Email: {email}");

        if (SimulateLoginError || email.ToLower() == "error@example.com")
        {
            return (false, null, "Stub: Login failed (simulated error or invalid credentials).");
        }

        string userId = "stub_user_id_for_" + email.Split('@')[0];
        string userNickname = email.Split('@')[0]; // Простой ник из email для заглушки

        var mockResponse = new LoginResponseDto // Убедитесь, что LoginResponseDto определен и может содержать UserId, Nickname
        {
            AccessToken = "fake_LOGGEDIN_access_token_" + Guid.NewGuid().ToString().Substring(0, 8),
            AccessTokenExpiration = DateTime.UtcNow.AddHours(1),
            NewRefreshToken = "fake_LOGGEDIN_refresh_token_" + Guid.NewGuid().ToString().Substring(0, 8),
            UserId = userId,
            Nickname = userNickname
        };

        if (SessionManager.Instance != null)
        {
            UserSessionData userData = new UserSessionData { UserId = userId, Nickname = userNickname, Email = email };
            SessionManager.Instance.CreateSession(mockResponse.AccessToken, mockResponse.AccessTokenExpiration, mockResponse.NewRefreshToken, userData);
        }
        return (true, mockResponse, null);
    }

    public async Task<(bool success, TokenResponseDto response, string errorMessage)> RefreshTokenAsync(string refreshTokenToRefresh)
    {
        await SimulateNetworkDelay();
        Debug.Log($"[STUB] MasterServerApiService: RefreshTokenAsync called with RefreshToken: {refreshTokenToRefresh?.Substring(0,10)}...");

        if (SimulateRefreshTokenError || string.IsNullOrEmpty(refreshTokenToRefresh) || refreshTokenToRefresh == "error_refresh_token")
        {
            if(SessionManager.Instance != null) SessionManager.Instance.ClearSession(); // Если ошибка, чистим сессию
            return (false, null, "Stub: Refresh token failed (simulated error or invalid token).");
        }

        // Имитируем успешное обновление
        var mockResponse = new TokenResponseDto
        {
            AccessToken = "fake_REFRESHED_access_token_" + Guid.NewGuid().ToString().Substring(0, 8),
            AccessTokenExpiration = DateTime.UtcNow.AddHours(1),
            NewRefreshToken = "fake_NEW_refresh_token_after_refresh_" + Guid.NewGuid().ToString().Substring(0, 8) // Часто возвращается новый RT
        };
        
        if (SessionManager.Instance != null && SessionManager.Instance.CurrentUser != null)
        {
             // Обновляем сессию только токенами, пользователь остается тот же
            SessionManager.Instance.CreateSession(
                mockResponse.AccessToken,
                mockResponse.AccessTokenExpiration,
                mockResponse.NewRefreshToken,
                SessionManager.Instance.CurrentUser
            );
        }
        return (true, mockResponse, null);
    }


    public async Task<(bool success, PasswordResetRequestResultDto response, string errorMessage)> RequestPasswordResetAsync(string email)
    {
        await SimulateNetworkDelay();
        Debug.Log($"[STUB] MasterServerApiService: RequestPasswordResetAsync called for Email: {email}");

        if (SimulateRequestPasswordResetAccountNotFound || email.ToLower() == "notfound@example.com")
        {
            // Имитируем, что аккаунт не найден (сервер может вернуть Succeeded=true, но сообщение другое, или Succeeded=false)
            // Для простоты заглушки, пусть будет Succeeded=false
            return (false, new PasswordResetRequestResultDto { IsSuccess = false, Error = "Stub: Account not found." }, "Stub: Account not found.");
        }
        
        // Имитируем успешную отправку запроса
        return (true, new PasswordResetRequestResultDto { IsSuccess = true, Error = "Stub: If an account exists, an email has been sent." }, null);
    }

    public async Task<(bool success, PasswordResetResultDto response, string errorMessage)> ResetPasswordAsync(string userId, string token, string newPassword)
    {
        await SimulateNetworkDelay();
        Debug.Log($"[STUB] MasterServerApiService: ResetPasswordAsync called for UserId: {userId}, Token: {token}");

        if (SimulateResetPasswordError || token == "invalid_reset_token")
        {
            return (false, new PasswordResetResultDto { IsSuccess = false, Errors = new[]{"Stub: Password reset failed (simulated error or invalid token)."} }, "Stub: Password reset failed.");
        }
        
        return (true, new PasswordResetResultDto { IsSuccess = true }, null);
    }
    
    public async Task<(bool success, string errorMessage)> LogoutAsync()
    {
        await SimulateNetworkDelay();
        Debug.Log("[STUB] MasterServerApiService: LogoutAsync called.");

        if (SessionManager.Instance != null)
        {
            SessionManager.Instance.ClearSession();
        }
        // В реальном приложении EnsureConnectedAsync(forceDisconnect: true) вызывался бы для переподключения без токена.
        // Для заглушки это не так важно, если только другой код не проверяет состояние подключения.
        return (true, null);
    }

    // Пример для авторизованного метода
    public async Task<(bool success, List<GameServerInfoDto> response, string errorMessage)> GetServerListAsync()
    {
        await SimulateNetworkDelay();
        Debug.Log("[STUB] MasterServerApiService: GetServerListAsync called.");

        if (SessionManager.Instance == null || !SessionManager.Instance.IsUserLoggedIn)
        {
            return (false, null, "Stub: User not authenticated to get server list.");
        }
        if (SimulateGetServerListError)
        {
             return (false, null, "Stub: Failed to get server list (simulated error).");
        }

        var mockServers = new List<GameServerInfoDto>
        {
            new GameServerInfoDto { Id = "stub_server_1", Name = "Stub Alpha", CurrentPlayers = 10, MaxPlayers = 50, Ping = 30 },
            new GameServerInfoDto { Id = "stub_server_2", Name = "Stub Beta", CurrentPlayers = 5, MaxPlayers = 20, Ping = 50 }
        };
        return (true, mockServers, null);
    }

    // --- Добавьте заглушки для других методов по аналогии ---
    // public async Task<(bool success, EmailConfirmationResultDto response, string errorMessage)> ConfirmEmailAsync(string userId, string code)
    // {
    //     await SimulateNetworkDelay();
    //     // ... логика заглушки ...
    //     return (true, new EmailConfirmationResultDto { IsSuccess = true }, null);
    // }
}