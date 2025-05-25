// Scripts/Services/MasterServerConnection.cs
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client; // Проверьте правильность using для вашего пакета
using System;
using System.Threading.Tasks;
using UnityEngine; // Для Debug.Log

public class MasterServerConnection
{
    private HubConnection connection;
    public bool IsConnected => connection?.State == HubConnectionState.Connected;

    public event Action<string> OnConnectionError; // Событие для ошибок подключения

    // События для ответов от сервера, которые не являются прямым результатом InvokeAsync
    // Например, если сервер сам пушит какие-то данные
    // public event Action<GameServerInfoDto> OnServerStatusUpdate;

    public async Task ConnectAsync(string hubUrl, string accessToken = null)
    {
        if (IsConnected)
        {
            Debug.Log("Already connected to Master Server.");
            return;
        }

        var hubConnectionBuilder = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                if (!string.IsNullOrEmpty(accessToken))
                {
                    // Для SignalR токен обычно передается в QueryString или как Bearer токен в заголовках,
                    // если используется HTTP-based транспорт (WebSockets обычно так и делают).
                    // Для передачи через QueryString:
                    // (URL уже должен содержать ?access_token=... или &access_token=...)
                    // На сервере в Program.cs уже настроен прием из QueryString:
                    // options.Events.OnMessageReceived = context => {
                    //     var accessToken = context.Request.Query["access_token"]; ... }

                    // Если ваш клиент SignalR поддерживает установку заголовков для WebSockets:
                    // options.AccessTokenProvider = () => Task.FromResult(accessToken);
                    // или
                    // options.Headers["Authorization"] = $"Bearer {accessToken}";
                }
            })
            .WithAutomaticReconnect(); // Настроить политику реконнекта

        // Если используете MessagePack для бинарной сериализации (быстрее JSON)
        // .AddMessagePackProtocol();

        connection = hubConnectionBuilder.Build();

        connection.Closed += async (error) =>
        {
            Debug.LogError($"Master Server connection closed: {error?.Message}");
            OnConnectionError?.Invoke(error?.Message ?? "Connection closed");
            // Можно попробовать реконнект здесь, но WithAutomaticReconnect уже должен это делать
            // await Task.Delay(new Random().Next(0, 5) * 1000);
            // await ConnectAsync(hubUrl, accessToken); // Осторожно с рекурсией
        };

        // Подписка на методы, вызываемые сервером (Clients.Caller.SendAsync, Clients.All.SendAsync)
        // connection.On<GameServerInfoDto>("ReceiveServerStatusUpdate", (serverInfo) =>
        // {
        //    OnServerStatusUpdate?.Invoke(serverInfo);
        // });
        // connection.On<string>("ReceiveChatMessage", (message) => { /* ... */ });

        try
        {
            await connection.StartAsync();
            Debug.Log("Successfully connected to Master Server.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error connecting to Master Server: {ex.Message}");
            OnConnectionError?.Invoke(ex.Message);
        }
    }

    // Обобщенный метод для вызова методов хаба
    public async Task<(bool success, TResponse response, string errorMessage)> InvokeHubMethodAsync<TResponse>(string methodName, params object[] args)
    {
        if (!IsConnected)
        {
            Debug.LogError("Not connected to Master Server. Cannot invoke method.");
            return (false, default(TResponse), "Not connected to server.");
        }

        try
        {
            TResponse result = await connection.InvokeAsync<TResponse>(methodName, args);
            return (true, result, null);
        }
        catch (HubException hubEx) // Ошибки, брошенные хабом на сервере
        {
            Debug.LogError($"HubException invoking {methodName}: {hubEx.Message}");
            return (false, default(TResponse), hubEx.Message);
        }
        catch (Exception ex) // Другие ошибки (сеть, сериализация и т.д.)
        {
            Debug.LogError($"Exception invoking {methodName}: {ex.Message}");
            return (false, default(TResponse), ex.Message);
        }
    }
    
    // Метод для вызова хаб-методов, которые не возвращают значение (void на сервере)
    public async Task<(bool success, string errorMessage)> SendHubMethodAsync(string methodName, params object[] args)
    {
        if (!IsConnected)
        {
            Debug.LogError("Not connected to Master Server. Cannot send method.");
            return (false, "Not connected to server.");
        }

        try
        {
            await connection.SendAsync(methodName, args);
            return (true, null);
        }
        catch (HubException hubEx)
        {
            Debug.LogError($"HubException sending {methodName}: {hubEx.Message}");
            return (false, hubEx.Message);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Exception sending {methodName}: {ex.Message}");
            return (false, ex.Message);
        }
    }


    public async Task DisconnectAsync()
    {
        if (connection != null)
        {
            await connection.StopAsync();
            await connection.DisposeAsync();
            connection = null;
            Debug.Log("Disconnected from Master Server.");
        }
    }
}