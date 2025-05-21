// Scripts/UI/EnterNewPasswordUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Assets.Scripts.UILogic;

public class EnterNewPasswordUI : UIScreen
{
    [SerializeField] private TMP_InputField newPasswordInputField;
    [SerializeField] private TMP_InputField confirmNewPasswordInputField;
    [SerializeField] private Button saveNewPasswordButton;

    private UIManager uiManager;
    private MasterServerService masterServerService;
    private string userIdForPasswordReset; // ID пользователя, для которого сбрасывается пароль
    private string resetToken; // Токен сброса пароля

    protected override void Awake()
    {
        base.Awake();
        uiManager = FindObjectOfType<UIManager>();
        masterServerService = FindObjectOfType<MasterServerService>();
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
        string confirmPassword = confirmNewPasswordInputField.text;

        // TODO: Валидация (непустые, совпадение, сложность пароля)
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
        if (string.IsNullOrEmpty(userIdForPasswordReset) || string.IsNullOrEmpty(resetToken))
        {
            Debug.LogError("User ID or Reset Token is missing. Cannot reset password.");
            // TODO: Показать критическую ошибку, возможно, вернуть на логин
            uiManager.SwitchToScreen(UIScreenType.RegisteredLogin);
            return;
        }

        Debug.Log($"Attempting to set new password for UserID: {userIdForPasswordReset}");
        // TODO: Вызвать masterServerService.ResetPasswordAsync(userIdForPasswordReset, resetToken, newPassword);
        // Заглушка:
        SetInteractable(false);
        await System.Threading.Tasks.Task.Delay(1000);

        bool mockResetSuccess = true; // Имитация
        if (mockResetSuccess)
        {
            Debug.Log("Password has been successfully reset (mock). Switching to login screen.");
            // TODO: Показать сообщение об успехе, затем переключить
            uiManager.SwitchToScreen(UIScreenType.RegisteredLogin);
        }
        else
        {
            Debug.LogError("Failed to reset password (mock).");
            // TODO: Показать ошибку
            SetInteractable(true);
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