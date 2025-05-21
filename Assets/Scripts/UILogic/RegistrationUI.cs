// Scripts/UI/RegistrationUI.cs
using UnityEngine;
using UnityEngine.UI;
using Assets.Scripts.UILogic;
using TMPro;
using Assets.Scripts.UILogic;

public class RegistrationUI : UIScreen
{
    [Header("UI Elements - Registration")]
    [SerializeField] private TMP_InputField emailInputField;
    [SerializeField] private TMP_InputField nicknameInputField;
    [SerializeField] private TMP_InputField passwordInputField;
    [SerializeField] private TMP_InputField confirmPasswordInputField;
    [SerializeField] private Button backButton;
    [SerializeField] private Button registerActionButton; // Кнопка "Зарегистрироваться" (действия)

    private UIManager uiManager;
    private MasterServerService masterServerService; // Для отправки запроса на регистрацию

    protected override void Awake()
    {
        base.Awake();
        uiManager = FindObjectOfType<UIManager>();
        masterServerService = FindObjectOfType<MasterServerService>();
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
        // Очистка полей при показе
        emailInputField.text = "";
        nicknameInputField.text = "";
        passwordInputField.text = "";
        confirmPasswordInputField.text = "";
    }

    private void OnBackButtonClicked()
    {
        uiManager.SwitchToScreen(UIScreenType.RegisteredLogin); // Возвращаемся на экран логина
    }

    private async void OnRegisterActionButtonClicked()
    {
        string email = emailInputField.text;
        string nickname = nicknameInputField.text;
        string password = passwordInputField.text;
        string confirmPassword = confirmPasswordInputField.text;

        // TODO: Добавить валидацию полей (непустые, email-формат, совпадение паролей и т.д.)
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(nickname) || string.IsNullOrWhiteSpace(password))
        {
            Debug.LogError("Registration fields cannot be empty.");
            // TODO: Показать ошибку пользователю
            return;
        }
        if (password != confirmPassword)
        {
            Debug.LogError("Passwords do not match.");
            // TODO: Показать ошибку пользователю
            return;
        }

        Debug.Log($"Attempting to register: Email='{email}', Nickname='{nickname}'");
        // TODO: Вызвать masterServerService.RegisterAsync(email, nickname, password);
        // Заглушка:
        SetInteractable(false); // Блокируем UI на время "запроса"
        // TODO: Заменить на реальный вызов masterServerService.RegisterAsync(email, nickname, password);
        // И реальная логика должна быть:
        // 1. Отправка запроса на регистрацию.
        // 2. Если сервер требует подтверждения email:
        //    - Сервер НЕ логинит пользователя сразу.
        //    - Сервер отправляет письмо.
        //    - Клиент показывает экран EmailConfirmationSent.
        // 3. Если сервер НЕ требует подтверждения (или оно опционально и выключено):
        //    - Сервер может сразу залогинить и вернуть токены.
        //    - Клиент переходит на UserProfile или RegisteredLogin (для ввода данных).

        // ЗАГЛУШКА для сценария с подтверждением email:
        await System.Threading.Tasks.Task.Delay(1000); // Имитация запроса к серверу

        bool mockServerRequiresEmailConfirmation = true; // Предположим, сервер требует подтверждения
        bool mockRegistrationRequestSentSuccessfully = true; // Запрос на регистрацию прошел (не сама регистрация)

        if (mockRegistrationRequestSentSuccessfully)
        {
            if (mockServerRequiresEmailConfirmation)
            {
                Debug.Log("Registration request sent (mock). Email confirmation required. Switching to EmailConfirmationSent screen.");
                uiManager.SwitchToScreen(UIScreenType.EmailConfirmationSent);
            }
            else
            {
                Debug.Log("Registration successful and auto-logged in (mock). Switching to UserProfile.");
                // Здесь должна быть логика сохранения токенов, если сервер их вернул
                uiManager.SwitchToScreen(UIScreenType.UserProfileUI);
            }
        }
        else
        {
            Debug.LogError("Registration request failed (mock).");
            // TODO: Показать ошибку пользователю
            SetInteractable(true);
        }
         //SetInteractable(true); // Разблокируем в любом случае (если не было перехода)
    }
    
    private void SetInteractable(bool state)
    {
        // Простой способ заблокировать основные элементы
        emailInputField.interactable = state;
        nicknameInputField.interactable = state;
        passwordInputField.interactable = state;
        confirmPasswordInputField.interactable = state;
        backButton.interactable = state;
        registerActionButton.interactable = state;
        if(canvasGroup != null) canvasGroup.interactable = state; // Глобально для панели
    }
}