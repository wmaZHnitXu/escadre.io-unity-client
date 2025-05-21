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
    private MasterServerService masterServerService; // Для отправки запроса на сброс

    protected override void Awake()
    {
        base.Awake();
        uiManager = FindObjectOfType<UIManager>();
        masterServerService = FindObjectOfType<MasterServerService>();
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

        // TODO: Вызвать masterServerService.RequestPasswordResetAsync(email);
        // В реальном сервисе этот метод должен вернуть результат, указывающий,
        // был ли найден аккаунт и отправлено ли письмо.
        // (результат типа PasswordResetRequestResult из вашего саммари по мастер-серверу)

        // ЗАГЛУШКА:
        await System.Threading.Tasks.Task.Delay(1000);
        // Имитируем разные ответы сервера
        // string testEmail = "exists@example.com";
        // string testEmailNotFound = "notexists@example.com";

        bool mockAccountExists = (email != "notfound@example.com"); // Имитация
        bool mockEmailSentSuccessfully = true; // Предположим, если аккаунт есть, письмо отправляется

        if (mockAccountExists)
        {
            if (mockEmailSentSuccessfully)
            {
                Debug.Log("Account found and password reset email sent (mock). Switching to PasswordResetEmailSent screen.");
                // Получаем экземпляр экрана PasswordResetEmailSentUI, чтобы передать email
                PasswordResetEmailSentUI passwordResetScreen = uiManager.GetScreenByType(UIScreenType.PasswordResetEmailSent) as PasswordResetEmailSentUI;
                if (passwordResetScreen != null)
                {
                    passwordResetScreen.SetUserEmail(email); // Передаем email на следующий экран
                }
                uiManager.SwitchToScreen(UIScreenType.PasswordResetEmailSent);
            }
            else
            {
                 Debug.LogError("Account found, but failed to send password reset email (mock).");
                // TODO: Показать ошибку (например, "Не удалось отправить письмо, попробуйте позже")
                SetInteractable(true);
            }
        }
        else // Аккаунт не найден
        {
            Debug.Log("Account not found (mock). Switching to AccountNotFound screen.");
            AccountNotFoundUI notFoundScreen = uiManager.GetScreenByType(UIScreenType.AccountNotFound) as AccountNotFoundUI;
            if (notFoundScreen != null)
            {
                notFoundScreen.SetMissingEmail(email);
            }
            uiManager.SwitchToScreen(UIScreenType.AccountNotFound);
        }
        // SetInteractable(true); // Если не было перехода, разблокировать
    }

    private void SetInteractable(bool state)
    {
        emailInputField.interactable = state;
        backButton.interactable = state;
        sendButton.interactable = state;
        if(canvasGroup != null) canvasGroup.interactable = state;
    }
}