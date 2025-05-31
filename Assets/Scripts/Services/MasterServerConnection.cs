// Scripts/Services/MasterServerConnection.cs
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client; // Проверьте правильность using для вашего пакета
using System;
using System.Threading.Tasks;
using UnityEngine; // Для Debug.Log
using System.Collections.Generic; 
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;


public class MasterServerConnection
{
    private HubConnection connection;
    public bool IsConnected => connection?.State == HubConnectionState.Connected;

    public event Action<string> OnConnectionError;

    // !!!!! ОБЪЯВЛЕНИЕ СОБЫТИЙ ДЛЯ РЕГИСТРАЦИИ !!!!!
    public event Action<string> OnRegistrationSuccess;      // Для успешной регистрации (сообщение от сервера)
    public event Action<List<string>> OnRegistrationFailed; // Для неуспешной регистрации (список ошибок от сервера)
    public event Action<LoginResponseDto> OnLoginSuccess;
    public event Action<string> OnLoginFailed;
    public event Action<string> OnPasswordResetRequested;
    public event Action<string> OnPasswordResetSuccess;
    public event Action<string> OnPasswordResetFailed; // Сервер шлет одну строку ошибки
    // !!!!! КОНЕЦ ОБЪЯВЛЕНИЯ СОБЫТИЙ !!!!!

    public async Task ConnectAsync(string hubUrl, string accessTokenForProvider = null)
    {
        if (IsConnected)
        {
            Debug.Log("Already connected to Master Server.");
            return;
        }

        Debug.Log($"[MasterServerConnection.ConnectAsync] hubUrl for WithUrl: '{hubUrl}', accessTokenForProvider: '{(accessTokenForProvider == null ? "NULL" : accessTokenForProvider)}'");

        var hubConnectionBuilder = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                Debug.Log($"[MasterServerConnection.ConnectAsync WithUrl Options] Configuring options. AccessTokenForProvider is '{(accessTokenForProvider == null ? "NULL" : accessTokenForProvider)}'");
                options.AccessTokenProvider = () => Task.FromResult(accessTokenForProvider);
            })
            .WithAutomaticReconnect();

        // !!!!! НАСТРОЙКА JSON ПРОТОКОЛА КЛИЕНТА !!!!!
        hubConnectionBuilder.AddJsonProtocol(options =>
        {
            // Эти опции будут использоваться для СЕРИАЛИЗАЦИИ запросов клиента и ДЕСЕРИАЛИЗАЦИИ ответов сервера
            options.PayloadSerializerOptions.PropertyNameCaseInsensitive = true; 
            // По умолчанию клиентская библиотека УЖЕ должна использовать PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            // для отправляемых данных. А PropertyNameCaseInsensitive = true поможет при приеме.
            // options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase; // Можно явно указать, если есть сомнения
        });
        // !!!!! КОНЕЦ НАСТРОЙКИ JSON ПРОТОКОЛА !!!!!

        connection = hubConnectionBuilder.Build();

        connection.Closed += async (error) =>
        {
            Debug.LogError($"Master Server connection closed: {error?.Message}");
            OnConnectionError?.Invoke(error?.Message ?? "Connection closed");
        };

        // !!!!! ПОДПИСКА НА СЕРВЕРНЫЕ СОБЫТИЯ ДЛЯ РЕГИСТРАЦИИ И ВЫЗОВ C# СОБЫТИЙ !!!!!
        connection.On<string>("RegistrationSuccess", (messageFromServer) => {
            Debug.Log($"[SignalR Event Received] RegistrationSuccess: {messageFromServer}");
            OnRegistrationSuccess?.Invoke(messageFromServer); // Вызываем наше C# событие
        });
        connection.On<List<string>>("RegistrationFailed", (errorsFromServer) => {
            Debug.LogWarning($"[SignalR Event Received] RegistrationFailed: {string.Join(", ", errorsFromServer)}");
            OnRegistrationFailed?.Invoke(errorsFromServer); // Вызываем наше C# событие
        });
        connection.On<LoginResponseDto>("LoginSuccess", (loginResponse) => {
        Debug.Log($"[SignalR Event Received] LoginSuccess. UserId: {loginResponse?.UserId}");
        OnLoginSuccess?.Invoke(loginResponse);
        });
        connection.On<string>("LoginFailed", (errorMessage) => {
            Debug.LogWarning($"[SignalR Event Received] LoginFailed: {errorMessage}");
            OnLoginFailed?.Invoke(errorMessage);
        });
        connection.On<string>("PasswordResetRequested", (serverMessage) => {
        Debug.Log($"[SignalR Event Received] PasswordResetRequested: {serverMessage}");
        OnPasswordResetRequested?.Invoke(serverMessage);
        });
        connection.On<string>("PasswordResetSuccess", (serverMessage) => {
            Debug.Log($"[SignalR Event Received] PasswordResetSuccess: {serverMessage}");
            OnPasswordResetSuccess?.Invoke(serverMessage);
        });
        connection.On<string>("PasswordResetFailed", (serverErrorMessage) => {
            Debug.LogWarning($"[SignalR Event Received] PasswordResetFailed: {serverErrorMessage}");
            OnPasswordResetFailed?.Invoke(serverErrorMessage);
        });
        // !!!!! КОНЕЦ ПОДПИСКИ НА СЕРВЕРНЫЕ СОБЫТИЯ !!!!!

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
    public async Task<(bool success, TResponse response, string errorMessage)> InvokeHubMethodAsync<TResponse>(string methodName) // БЕЗ params object[] args
{
    if (!IsConnected)
    {
        Debug.LogError("Not connected to Master Server. Cannot invoke method.");
        return (false, default(TResponse), "Not connected to server.");
    }

    try
    {
        // Используем перегрузку InvokeAsync, которая не принимает массив args
        TResponse result = await connection.InvokeAsync<TResponse>(methodName);
        return (true, result, null);
    }
    catch (HubException hubEx)
    {
        Debug.LogError($"HubException invoking {methodName} (no-args): {hubEx.Message}");
        return (false, default(TResponse), hubEx.Message);
    }
    catch (Exception ex)
    {
        Debug.LogError($"Exception invoking {methodName} (no-args): {ex.Message}");
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
            // Отписываемся от всех серверных обработчиков перед стопом, чтобы избежать ошибок
            // если соединение уже закрывается или null.
            // Это более безопасно делать здесь или в DisposeAsync, если бы он был IAsyncDisposable.
            connection.Remove("RegistrationSuccess"); // Удаляем по имени серверного метода
            connection.Remove("RegistrationFailed");
            connection.Remove("LoginSuccess");
            connection.Remove("LoginFailed");
            connection.Remove("PasswordResetRequested");
            connection.Remove("PasswordResetSuccess");
            connection.Remove("PasswordResetFailed");
            await connection.StopAsync();
            await connection.DisposeAsync();
            connection = null;
            Debug.Log("Disconnected from Master Server.");
        }
    }
}