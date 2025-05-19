// Scripts/Services/MasterServerService.cs (Заглушка)
using UnityEngine;
using System.Threading.Tasks; // Для async/await
using System.Collections.Generic;


public class ServerListService : MonoBehaviour
{
    public async Task<(bool success, List<GameServerInfoDto> servers, string errorMessage)> GetServerListAsync()
    {
        // Здесь будет реальный вызов к вашему SignalR хабу
        // hubConnection.InvokeAsync<List<GameServerInfoDto>>("GetServerList");

        Debug.Log("ServerListService: Requesting server list...");
        await Task.Delay(500); // Имитация сетевой задержки

        // Имитация успешного ответа
        var mockServers = new List<GameServerInfoDto>
        {
            new GameServerInfoDto { Id = "moscow_real", Name = "Москва (Real)", Address = "1.2.3.4", Port = 7777, CurrentPlayers = 15, MaxPlayers = 60, Ping = 20 },
            new GameServerInfoDto { Id = "europe_real", Name = "Европа (Real)", Address = "5.6.7.8", Port = 7778, CurrentPlayers = 30, MaxPlayers = 60, Ping = 45 },
            new GameServerInfoDto { Id = "usa_east_real", Name = "США Восток (Real)", Address = "9.1.2.3", Port = 7779, CurrentPlayers = 10, MaxPlayers = 60, Ping = 90 }
        };
        return (true, mockServers, null);
        // Имитация ошибки:
        // return (false, null, "Simulated error: Could not fetch server list.");
    }
}