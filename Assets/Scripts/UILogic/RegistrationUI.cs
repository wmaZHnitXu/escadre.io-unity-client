// Scripts/UI/RegistrationUI.cs
using UnityEngine;
using UnityEngine.UI;
using Assets.Scripts.UILogic; // Убедитесь, что этот using нужен и правильный
using TMPro;
// using Assets.Scripts.UILogic; // Дублирующийся using, можно убрать

public class RegistrationUI : UIScreen
{
    [Header("UI Elements - Registration")]
    [SerializeField] private TMP_InputField emailInputField;
    [SerializeField] private TMP_InputField nicknameInputField;
    [SerializeField] private TMP_InputField passwordInputField;
    [SerializeField] private TMP_InputField confirmPasswordInputField;
    [SerializeField] private Button backButton;
    [SerializeField] private Button registerActionButton;

    private UIManager uiManager;
    // private MasterServerService masterServerService; // Старое имя, у вас используется MasterServerApiService
    private MasterServerApiService masterServerApiService; // Правильное имя сервиса

    protected override void Awake()
    {
        base.Awake();
        uiManager = FindObjectOfType<UIManager>();
        // masterServerService = FindObjectOfType<MasterServerService>(); // Старое имя
        masterServerApiService = MasterServerApiService.Instance; // Используем Singleton Instance
        if (masterServerApiService == null)
        {
            Debug.LogError("MasterServerApiService not found in the scene or not initialized!");
        }
    }

    private void Start()
    {
        backButton?.onClick.AddListener(OnBackButtonClicked);
        registerActionButton?.onClick.AddListener(OnRegisterActionButtonClicked);
    }

    protected override void OnShow()
    {
        base.OnShow();
        Debug.Log("Registration Screen Shown.");
        emailInputField.text = "";
        nicknameInputField.text = "";
        passwordInputField.text = "";
        confirmPasswordInputField.text = "";
        SetInteractable(true); // Убедимся, что UI интерактивен при показе
    }

    private void OnBackButtonClicked()
    {
        uiManager.SwitchToScreen(UIScreenType.RegisteredLogin);
    }

    private async void OnRegisterActionButtonClicked()
    {
        string email = emailInputField.text;
        string nickname = nicknameInputField.text;
        string password = passwordInputField.text;
        string confirmPassword = confirmPasswordInputField.text;

        if (string.IsNullOrWhiteSpace(email) || !IsValidEmail(email)) // Добавим простую валидацию email
        {
            Debug.LogError("Invalid or empty email.");
            // TODO: Показать ошибку пользователю (например, через UIManager.ShowNotification)
            return;
        }
        if (string.IsNullOrWhiteSpace(nickname))
        {
            Debug.LogError("Nickname cannot be empty.");
            return;
        }
        if (string.IsNullOrWhiteSpace(password) || password.Length < 6) // Пример минимальной длины пароля
        {
            Debug.LogError("Password is too short (minimum 6 characters) or empty.");
            return;
        }
        if (password != confirmPassword)
        {
            Debug.LogError("Passwords do not match.");
            return;
        }

        SetInteractable(false); // Блокируем UI
        Debug.Log($"Attempting to register: Email='{email}', Nickname='{nickname}'");

        // --- РЕАЛЬНЫЙ ВЫЗОВ ---
        if (masterServerApiService == null)
        {
            Debug.LogError("MasterServerApiService is not available for registration.");
            SetInteractable(true);
            return;
        }

        // Используем обновленный MasterServerApiService.RegisterAsync, который работает с событиями
        var (success, response, errorMessage) = await masterServerApiService.RegisterAsync(email, nickname, password);

        if (success)
        {
            // Серверный метод Register в MasterHub отправляет только сообщение "User registered successfully..."
            // Он НЕ возвращает RegistrationResultDto напрямую.
            // Логика обработки ответа должна быть в MasterServerApiService.HandleRegistrationSuccess/Failed
            // и _registrationTcs.TrySetResult.
            // Здесь мы проверяем результат, который вернул TaskCompletionSource.
            Debug.Log($"Registration successful (client-side perspective): {errorMessage}"); // errorMessage здесь будет сообщением от сервера
            // Предполагаем, что сервер ТРЕБУЕТ подтверждения email (так настроено в Program.cs)
            uiManager.SwitchToScreen(UIScreenType.EmailConfirmationSent);
        }
        else
        {
            Debug.LogError($"Registration failed: {errorMessage}");
            // TODO: Показать пользователю errorMessage
            SetInteractable(true);
        }
        // SetInteractable(true); // Разблокируем, если не было перехода на другой экран (уже сделано в else)
    }
    
    private bool IsValidEmail(string email) // Простая проверка формата email
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }

    private void SetInteractable(bool state)
    {
        emailInputField.interactable = state;
        nicknameInputField.interactable = state;
        passwordInputField.interactable = state;
        confirmPasswordInputField.interactable = state;
        backButton.interactable = state;
        registerActionButton.interactable = state;
        if(canvasGroup != null) canvasGroup.interactable = state;
    }
}