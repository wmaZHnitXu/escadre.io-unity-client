// Scripts/UI/RegistrationUI.cs
using UnityEngine;
using UnityEngine.UI;
using Assets.Scripts.UILogic;
using TMPro;

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
    private MasterServerApiService masterServerApiService;

    protected override void Awake()
    {
        base.Awake();
        uiManager = FindObjectOfType<UIManager>();
        masterServerApiService = MasterServerApiService.Instance;
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
        
        SetInteractable(true);
    }

    private void ClearInputFields()
    {
        emailInputField.text = "";
        nicknameInputField.text = "";
        passwordInputField.text = "";
        confirmPasswordInputField.text = "";
    }

    private void OnBackButtonClicked()
    {   
        ClearInputFields();
        uiManager.SwitchToScreen(UIScreenType.RegisteredLogin);
    }

    private async void OnRegisterActionButtonClicked()
    {
        string email = emailInputField.text;
        string nickname = nicknameInputField.text;
        string password = passwordInputField.text;
        string confirmPassword = confirmPasswordInputField.text;

        if (string.IsNullOrWhiteSpace(email) || !IsValidEmail(email))
        {
            Debug.LogError("Invalid or empty email.");
            UIManager.Instance.ShowErrorScreen("Ошибка Регистрации", "Пожалуйста, введите корректный email.", null, UIScreenType.Registration);
            return;
        }
        if (string.IsNullOrWhiteSpace(nickname))
        {
            Debug.LogError("Nickname cannot be empty.");
            UIManager.Instance.ShowErrorScreen("Ошибка Регистрации", "Пожалуйста, введите никнейм.", null, UIScreenType.Registration);
            return;
        }
        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
        {
            Debug.LogError("Password is too short (minimum 6 characters) or empty.");
            UIManager.Instance.ShowErrorScreen("Ошибка Регистрации", "Пароль должен содержать не менее 6 символов.", null, UIScreenType.Registration);
            return;
        }
        if (password != confirmPassword)
        {
            Debug.LogError("Passwords do not match.");
            UIManager.Instance.ShowErrorScreen("Ошибка Регистрации", "Пароли не совпадают.", null, UIScreenType.Registration);
            return;
        }

        SetInteractable(false);
        Debug.Log($"Attempting to register: Email='{email}', Nickname='{nickname}'");

        if (masterServerApiService == null)
        {
            Debug.LogError("MasterServerApiService is not available for registration.");
            UIManager.Instance.ShowErrorScreen("Критическая Ошибка", "Сервис регистрации недоступен.", null, UIScreenType.Registration);
            SetInteractable(true);
            // uiManager.ShowLoadingScreen(false);
            return;
        }

        var (success, response, errorMessage) = await masterServerApiService.RegisterAsync(email, nickname, password);

        if (success)
        {
            if (response != null && response.IsSuccess) 
            {
                Debug.Log($"Registration successful (client-side perspective): {errorMessage}");
                ClearInputFields(); 
                uiManager.SwitchToScreen(UIScreenType.EmailConfirmationSent);
            }
            else
            {
                string serverError = "Неизвестная ошибка регистрации.";
                if (response?.Errors != null && response.Errors.Length > 0)
                {
                    serverError = string.Join("\n", response.Errors);
                }
                else if (!string.IsNullOrEmpty(errorMessage))
                {
                    serverError = errorMessage;
                }
                Debug.LogError($"Registration failed on server: {serverError}");
                UIManager.Instance.ShowErrorScreen("Ошибка Регистрации", serverError, null, UIScreenType.Registration);
                SetInteractable(true);
            }
        }
        else
        {
            Debug.LogError($"Registration API call failed: {errorMessage}");

            UIManager.Instance.ShowErrorScreen("Ошибка Регистрации", errorMessage ?? "Не удалось отправить запрос на регистрацию.", null, UIScreenType.Registration);
            SetInteractable(true);
        }
    }
    
    private bool IsValidEmail(string email)
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