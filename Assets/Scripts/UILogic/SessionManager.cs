// Scripts/Managers/SessionManager.cs
using UnityEngine;
using System;

// Простой DTO для информации о пользователе (можно расширить)
[System.Serializable]
public class UserSessionData
{
    public string UserId { get; set; }
    public string Nickname { get; set; }
    public string Email { get; set; }
    // Можно добавить другие поля, например, роли, дата регистрации и т.д.
}

public class SessionManager : MonoBehaviour
{
    public static SessionManager Instance { get; private set; }

    public string AccessToken { get; private set; }
    public string RefreshToken { get; private set; }
    public DateTime AccessTokenExpiration { get; private set; }
    public UserSessionData CurrentUser { get; private set; }

    public bool IsUserLoggedIn => !string.IsNullOrEmpty(AccessToken) && AccessTokenExpiration > DateTime.UtcNow;
    // Более строгая проверка может включать и наличие CurrentUser.UserId

    private const string RefreshTokenPlayerPrefsKey = "UserRefreshToken";
    private const string UserIdPlayerPrefsKey = "UserIdForRefreshToken"; // Чтобы знать, чей это RefreshToken

    // События для оповещения других частей игры об изменении состояния сессии
    public event Action OnLoginStateChanged; // Вызывается при логине/логауте

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadRefreshToken(); // Пытаемся загрузить RefreshToken при старте
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // Вызывается после успешного логина или обновления токена
    public void CreateSession(string accessToken, DateTime accessTokenExpiration, string refreshToken, UserSessionData userData)
    {
        AccessToken = accessToken;
        AccessTokenExpiration = accessTokenExpiration;
        RefreshToken = refreshToken; // Новый RefreshToken может приходить не всегда, только при первом логине или если старый инвалидирован
        CurrentUser = userData;

        if (!string.IsNullOrEmpty(refreshToken) && userData != null && !string.IsNullOrEmpty(userData.UserId))
        {
            SaveRefreshToken(refreshToken, userData.UserId);
        }
        
        Debug.Log($"Session created/updated for User: {CurrentUser?.Nickname}. Access token expires at: {AccessTokenExpiration}");
        OnLoginStateChanged?.Invoke();
    }

    // Обновление только AccessToken (например, после использования RefreshToken)
    public void UpdateAccessToken(string newAccessToken, DateTime newAccessTokenExpiration)
    {
        AccessToken = newAccessToken;
        AccessTokenExpiration = newAccessTokenExpiration;
        Debug.Log($"Access token updated. Expires at: {newAccessTokenExpiration}");
        // OnLoginStateChanged не вызываем, так как пользователь остался тот же, просто токен обновился
    }


    public void ClearSession()
    {
        AccessToken = null;
        RefreshToken = null;
        AccessTokenExpiration = DateTime.MinValue;
        CurrentUser = null;

        PlayerPrefs.DeleteKey(RefreshTokenPlayerPrefsKey);
        PlayerPrefs.DeleteKey(UserIdPlayerPrefsKey);
        PlayerPrefs.Save(); // Сохраняем изменения в PlayerPrefs

        Debug.Log("Session cleared (Logout).");
        OnLoginStateChanged?.Invoke();
    }

    private void SaveRefreshToken(string refreshTokenToSave, string userId)
    {
        try
        {
            // В реальном проекте здесь должно быть шифрование refreshTokenToSave перед сохранением
            PlayerPrefs.SetString(RefreshTokenPlayerPrefsKey, refreshTokenToSave);
            PlayerPrefs.SetString(UserIdPlayerPrefsKey, userId); // Сохраняем UserId, чтобы не пытаться восстановить сессию не того юзера
            PlayerPrefs.Save();
            Debug.Log("Refresh token saved to PlayerPrefs.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error saving refresh token: {ex.Message}");
        }
    }

    private void LoadRefreshToken()
    {
        if (PlayerPrefs.HasKey(RefreshTokenPlayerPrefsKey) && PlayerPrefs.HasKey(UserIdPlayerPrefsKey))
        {
            try
            {
                // В реальном проекте здесь должно быть дешифрование
                RefreshToken = PlayerPrefs.GetString(RefreshTokenPlayerPrefsKey);
                string storedUserId = PlayerPrefs.GetString(UserIdPlayerPrefsKey);
                // Пока просто загружаем, логика восстановления сессии будет в другом месте (например, в MasterServerApiService при старте)
                Debug.Log($"Refresh token loaded from PlayerPrefs for UserId: {storedUserId}.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error loading refresh token: {ex.Message}");
                PlayerPrefs.DeleteKey(RefreshTokenPlayerPrefsKey); // Удаляем поврежденный ключ
                PlayerPrefs.DeleteKey(UserIdPlayerPrefsKey);
            }
        }
    }

    public string GetStoredUserIdForRefreshToken()
    {
        return PlayerPrefs.GetString(UserIdPlayerPrefsKey, null);
    }

    // Метод для проверки, нужно ли обновлять AccessToken
    public bool IsAccessTokenExpiredOrNearingExpiration(float bufferSeconds = 60f) // Проверяем за 60 сек до истечения
    {
        if (string.IsNullOrEmpty(AccessToken)) return true; // Если токена нет, считаем его истекшим
        return DateTime.UtcNow >= AccessTokenExpiration.AddSeconds(-bufferSeconds);
    }
}