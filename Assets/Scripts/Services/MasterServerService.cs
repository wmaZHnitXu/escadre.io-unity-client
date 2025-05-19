// Scripts/Services/MasterServerService.cs (Заглушка)
using UnityEngine;
using System.Threading.Tasks; // Для async/await

public class MasterServerService : MonoBehaviour
{
    // Пример метода для анонимного входа
    // Возвращает кортеж: (успех, данные_ответа, сообщение_об_ошибке)
    public async Task<(bool success, TokenResponseDto response, string errorMessage)> GetAnonymousTokenAsync(string nickname)
    {
        // Здесь будет реальный вызов к вашему SignalR хабу
        // hubConnection.InvokeAsync<TokenResponseDto>("GetAnonymousToken", nickname);

        Debug.Log($"MasterServerService: Requesting anonymous token for {nickname}");
        await Task.Delay(1000); // Имитация сетевой задержки

        // Имитация успешного ответа
        if (nickname != "error") // Для теста ошибки
        {
            var mockResponse = new TokenResponseDto
            {
                AccessToken = "fake_anonymous_access_token_" + System.Guid.NewGuid().ToString(),
                AccessTokenExpiration = System.DateTime.UtcNow.AddHours(1), // Пример времени истечения
                NewRefreshToken = "fake_anonymous_new_refresh_token_" + System.Guid.NewGuid().ToString()
            };
            Debug.Log($"mockResponse: {mockResponse}");
            return (true, mockResponse, null);
        }
        else
        {
            return (false, null, "Simulated server error: Invalid nickname.");
        }
    }
}


