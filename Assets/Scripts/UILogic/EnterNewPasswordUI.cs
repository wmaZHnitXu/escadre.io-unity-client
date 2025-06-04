// Scripts/UI/EnterNewPasswordUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Assets.Scripts.UILogic; // Для UIScreen, UIScreenType, UIManager
using System.Linq; // Для string.Join при отображении нескольких ошибок

public class EnterNewPasswordUI : UIScreen
{
    [Header("UI Elements")]
    [SerializeField] private TMP_InputField userIdInputField; // Если вы используете поля для ввода
    [SerializeField] private TMP_InputField tokenInputField;  // Если вы используете поля для ввода
    [SerializeField] private TMP_InputField newPasswordInputField;
    [SerializeField] private TMP_InputField confirmNewPasswordInputField;
    [SerializeField] private Button saveNewPasswordButton;
    // [SerializeField] private Button backButton; // Если есть кнопка "Назад" на этом экране

    private UIManager uiManager;
    private MasterServerApiService masterServerApiService;

    // Если вы решите передавать userId и token через PrepareForPasswordReset, а не поля ввода:
    // private string userIdForPasswordReset; 
    // private string resetToken;

    protected override void Awake()
    {
        base.Awake();
        uiManager = UIManager.Instance;
        masterServerApiService = MasterServerApiService.Instance;
        if (masterServerApiService == null)
        {
            Debug.LogError("MasterServerApiService.Instance is null in EnterNewPasswordUI!");
        }
    }

    private void Start()
    {
        saveNewPasswordButton?.onClick.AddListener(OnSaveNewPasswordClicked);
        // backButton?.onClick.AddListener(OnBackButtonClicked); // Если есть кнопка "Назад"
    }

    // Метод для передачи данных, если не используете InputFields для userId/token
    // public void PrepareForPasswordReset(string userId, string token)
    // {
    //     userIdForPasswordReset = userId;
    //     resetToken = token;
    //     if (userIdInputField != null) userIdInputField.text = userId; // Можно предзаполнить поля, если они есть
    //     if (tokenInputField != null) tokenInputField.text = token;
    //     Debug.Log($"EnterNewPasswordUI prepared for UserID: {userId}, Token (length): {token?.Length}");
    // }

    protected override void OnShow()
    {
        base.OnShow();
        Debug.Log("Enter New Password Screen Shown.");
        // Очищаем только поля паролей, userId и token должны сохраняться (если они вводятся/передаются)
        newPasswordInputField.text = "";
        confirmNewPasswordInputField.text = "";
        SetInteractable(true);
    }

    // private void OnBackButtonClicked() // Если есть кнопка "Назад"
    // {
    //     ClearInputFields();
    //     uiManager.SwitchToScreen(UIScreenType.RegisteredLogin); // или PasswordRestoration
    // }

    private async void OnSaveNewPasswordClicked()
    {
        // Получаем userId и token. Если используете PrepareForPasswordReset, то из полей класса.
        // Если используете InputFields, то из них.
        string userId = userIdInputField.text.Trim(); 
        string token = tokenInputField.text.Trim();  
        // Если PrepareForPasswordReset должен быть основным способом, то:
        // string userId = userIdForPasswordReset;
        // string token = resetToken;

        string newPassword = newPasswordInputField.text;
        string confirmPassword = confirmNewPasswordInputField.text;

        // --- КЛИЕНТСКАЯ ВАЛИДАЦИЯ ---
        if (string.IsNullOrWhiteSpace(userId))
        {
            UIManager.Instance.ShowErrorScreen("Ошибка Сброса", "ID пользователя не указан.", null, UIScreenType.EnterNewPassword);
            return;
        }
        if (string.IsNullOrWhiteSpace(token))
        {
            UIManager.Instance.ShowErrorScreen("Ошибка Сброса", "Токен сброса не указан.", null, UIScreenType.EnterNewPassword);
            return;
        }
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6) // Минимальная длина пароля
        {
            UIManager.Instance.ShowErrorScreen("Ошибка Ввода", "Новый пароль должен содержать не менее 6 символов.", null, UIScreenType.EnterNewPassword);
            return;
        }
        if (newPassword != confirmPassword)
        {
            UIManager.Instance.ShowErrorScreen("Ошибка Ввода", "Введенные пароли не совпадают.", null, UIScreenType.EnterNewPassword);
            return;
        }
        // --- КОНЕЦ КЛИЕНТСКОЙ ВАЛИДАЦИИ ---

        Debug.Log($"Attempting to set new password for UserID: {userId}");
        SetInteractable(false);
        // uiManager.ShowLoadingScreen(true);

        if (masterServerApiService == null)
        {
            Debug.LogError("MasterServerApiService is null.");
            UIManager.Instance.ShowErrorScreen("Критическая Ошибка", "Сервис недоступен.", null, UIScreenType.EnterNewPassword);
            SetInteractable(true);
            // uiManager.ShowLoadingScreen(false);
            return;
        }

        var (success, response, errorMessage) = await masterServerApiService.ResetPasswordAsync(userId, token, newPassword); 
    
        // uiManager.ShowLoadingScreen(false);

        if (success && response != null && response.IsSuccess)
        {
            Debug.Log("Password has been successfully reset. Switching to login screen.");
            // TODO: Показать временное сообщение "Пароль успешно изменен!" перед переходом
            // Например, можно добавить параметр в SwitchToScreen или использовать отдельный сервис уведомлений.
            // Пока что просто переключаем.
            ClearInputFields(); 
            UIManager.Instance.ShowErrorScreen("Успех!", "Пароль успешно изменен. Теперь вы можете войти.", 
                onBackAction: () => uiManager.SwitchToScreen(UIScreenType.RegisteredLogin), // При нажатии "Назад" на сообщении об успехе
                screenToReturnTo: UIScreenType.RegisteredLogin); // Куда вернет UIManager, если onBackAction null (не используется здесь)
            // SetInteractable(true); // Не нужно, т.к. переходим или показываем сообщение об успехе как модальное
        }
        else
        {
            string errorToDisplay = "Не удалось сбросить пароль."; 
            if (response?.Errors != null && response.Errors.Any()) // Используем Any() для string[]
            {
                // Здесь будут ошибки от Identity, например "Invalid token." или критерии сложности пароля.
                errorToDisplay = string.Join("\n", response.Errors);
            }
            else if (!string.IsNullOrEmpty(response?.Error))
            {
                errorToDisplay = response.Error;
            }
            else if (!string.IsNullOrEmpty(errorMessage))
            {
                errorToDisplay = errorMessage;
            }
            Debug.LogError($"Failed to reset password: {errorToDisplay}");
            UIManager.Instance.ShowErrorScreen("Ошибка Сброса Пароля", errorToDisplay, null, UIScreenType.EnterNewPassword);
            SetInteractable(true); 
        }
    }
    
    private void ClearInputFields() 
    {
        if(userIdInputField != null) userIdInputField.text = "";
        if(tokenInputField != null) tokenInputField.text = "";
        if(newPasswordInputField != null) newPasswordInputField.text = "";
        if(confirmNewPasswordInputField != null) confirmNewPasswordInputField.text = "";
    }

    private void SetInteractable(bool state)
    {
        if (userIdInputField != null) userIdInputField.interactable = state;
        if (tokenInputField != null) tokenInputField.interactable = state;
        newPasswordInputField.interactable = state;
        confirmNewPasswordInputField.interactable = state;
        saveNewPasswordButton.interactable = state;
        // if (backButton != null) backButton.interactable = state;
        if(canvasGroup != null) canvasGroup.interactable = state;
    }
}