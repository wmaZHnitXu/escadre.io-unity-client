// Scripts/UI/EnterNewPasswordUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Assets.Scripts.UILogic;

public class EnterNewPasswordUI : UIScreen
{
    [SerializeField] private TMP_InputField newPasswordInputField;
    [SerializeField] private TMP_InputField confirmNewPasswordInputField;
    [SerializeField] private TMP_InputField userIdInputField;
    [SerializeField] private TMP_InputField tokenInputField;
    [SerializeField] private Button saveNewPasswordButton;

    private UIManager uiManager;
    private MasterServerApiService masterServerApiService;
    private string userIdForPasswordReset; // ID пользователя, для которого сбрасывается пароль
    private string resetToken; // Токен сброса пароля

    protected override void Awake()
    {
        base.Awake();
        uiManager = FindObjectOfType<UIManager>();
        masterServerApiService = MasterServerApiService.Instance;
        if (masterServerApiService == null)
        {
            Debug.LogError($"{this.GetType().Name}: MasterServerApiService.Instance is null!");
        }
    }

    private void Start()
    {
        saveNewPasswordButton?.onClick.AddListener(OnSaveNewPasswordClicked);
    }

    // Метод для передачи данных, необходимых для сброса пароля
    public void PrepareForPasswordReset(string userId, string token)
    {
        userIdForPasswordReset = userId;
        resetToken = token;
        Debug.Log($"EnterNewPasswordUI prepared for UserID: {userId}, Token: {token}");
    }

    protected override void OnShow()
    {
        base.OnShow();
        Debug.Log("Enter New Password Screen Shown.");
        newPasswordInputField.text = "";
        confirmNewPasswordInputField.text = "";
    }

    private async void OnSaveNewPasswordClicked()
    {
        string newPassword = newPasswordInputField.text;
        string confirmPassword = confirmNewPasswordInputField.text; // Вы уже считывали его для валидации

        string userId = userIdInputField.text; // Предполагаем, что userIdInputField существует
        string token = tokenInputField.text;   // Предполагаем, что tokenInputField существует

        // Ваша валидация (оставляем ее как есть)
        if (string.IsNullOrWhiteSpace(newPassword) || string.IsNullOrWhiteSpace(confirmPassword))
        {
            Debug.LogError("Password fields cannot be empty.");
            // TODO: Показать ошибку
            return;
        }
        if (newPassword != confirmPassword)
        {
            Debug.LogError("New passwords do not match.");
            // TODO: Показать ошибку
            return;
        }
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token)) // Изменил userIdForPasswordReset на userId, tokenForReset на token
        {
            Debug.LogError("User ID or Reset Token is missing. Cannot reset password.");
            // TODO: Показать критическую ошибку, возможно, вернуть на логин
            uiManager.SwitchToScreen(UIScreenType.RegisteredLogin);
            return;
        }
        
        // TODO: Добавить валидацию сложности пароля, если требуется

        Debug.Log($"Attempting to set new password for UserID: {userId}");
        SetInteractable(false);
        // TODO: uiManager.ShowLoadingScreen(true); // Если будете реализовывать

        var (success, response, errorMessage) = await masterServerApiService.ResetPasswordAsync(userId, token, newPassword);
        // TODO: uiManager.ShowLoadingScreen(false);

        if (success && response != null && response.IsSuccess)
        {
            Debug.Log("Password has been successfully reset. Switching to login screen.");
            // TODO: Показать сообщение об успехе (например, "Пароль успешно изменен!")
            uiManager.SwitchToScreen(UIScreenType.RegisteredLogin);
        }
        else
        {
            string errorToDisplay = errorMessage;
            // Проверяем ошибки из DTO, если они есть
            if (response?.Errors != null && response.Errors.Length > 0)
            {
                errorToDisplay = string.Join("\n", response.Errors);
            }
            else if (!string.IsNullOrEmpty(response?.Error)) // response?.Error - это одиночная строка ошибки из DTO
            {
                errorToDisplay = response.Error;
            }
            // Если в DTO ошибок нет, но errorMessage (от TCS) есть, используем его
            else if (string.IsNullOrEmpty(errorToDisplay) && !string.IsNullOrEmpty(errorMessage))
            {
                errorToDisplay = errorMessage;
            }
            // Если совсем ничего нет, общее сообщение
            else if (string.IsNullOrEmpty(errorToDisplay))
            {
                errorToDisplay = "Failed to reset password due to an unknown error.";
            }

            Debug.LogError($"Failed to reset password: {errorToDisplay}");
            // TODO: Показать пользователю errorToDisplay
            // SetInteractable(true); // Уже сделано выше
        }
    }
    
    private void SetInteractable(bool state)
    {
        newPasswordInputField.interactable = state;
        confirmNewPasswordInputField.interactable = state;
        saveNewPasswordButton.interactable = state;
        if(canvasGroup != null) canvasGroup.interactable = state;
    }
}