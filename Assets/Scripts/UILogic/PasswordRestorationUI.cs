// Scripts/UI/PasswordRestorationUI.cs
using UnityEngine;
using UnityEngine.UI;
using Assets.Scripts.UILogic;
using TMPro;

public class PasswordRestorationUI : UIScreen
{
    [Header("UI Elements - Password Restoration")]
    [SerializeField] private TMP_InputField emailInputField;
    [SerializeField] private Button backButton;
    [SerializeField] private Button sendButton;

    private UIManager uiManager;
    private MasterServerApiService masterServerApiService;

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
        backButton?.onClick.AddListener(OnBackButtonClicked);
        sendButton?.onClick.AddListener(OnSendButtonClicked);
    }

    protected override void OnShow()
    {
        base.OnShow();
        Debug.Log("Password Restoration Screen Shown.");
        emailInputField.text = ""; // Очистка поля
    }

    private void OnBackButtonClicked()
    {
        uiManager.SwitchToScreen(UIScreenType.RegisteredLogin);
    }

    private async void OnSendButtonClicked()
    {
        string email = emailInputField.text;
        // ... (валидация email) ...

        Debug.Log($"Requesting password reset for Email='{email}'");
        SetInteractable(false);
        var (success, response, errorMessage) = await masterServerApiService.RequestPasswordResetAsync(email);
        SetInteractable(true); // Разблокируем UI после ответа
        // TODO: uiManager.ShowLoadingScreen(false);

        if (success && response != null && response.IsSuccess) // Сервер всегда возвращает "успех" здесь
        {
            Debug.Log($"Password reset request processed for {email}. Server message: {response.Error}. Switching to PasswordResetEmailSent screen.");
            PasswordResetEmailSentUI passwordResetScreen = uiManager.GetScreenByType(UIScreenType.PasswordResetEmailSent) as PasswordResetEmailSentUI;
            if (passwordResetScreen != null)
            {
                passwordResetScreen.SetUserEmail(email);
            }
            uiManager.SwitchToScreen(UIScreenType.PasswordResetEmailSent);
        }
        else
        {
            Debug.LogError($"Password reset request call failed: {errorMessage}");
            // TODO: Показать ошибку пользователю (например, "Не удалось отправить запрос. Проверьте соединение.")
        }
    }

    private void SetInteractable(bool state)
    {
        emailInputField.interactable = state;
        backButton.interactable = state;
        sendButton.interactable = state;
        if(canvasGroup != null) canvasGroup.interactable = state;
    }
}