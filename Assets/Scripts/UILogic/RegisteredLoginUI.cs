// Scripts/UI/RegisteredLoginUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro; // Если будете использовать TextMeshPro элементы
using Assets.Scripts.UILogic;
using System.Collections.Generic;
using Assets.Scripts.UILogic;

public class RegisteredLoginUI : UIScreen // Наследуемся от UIScreen
{
    [Header("UI Elements - Registered Login")]
    [SerializeField] private TMP_InputField emailInputField;
    [SerializeField] private TMP_InputField passwordInputField;
    [SerializeField] private Button loginButton;
    [SerializeField] private Button registerButton; // Кнопка "Зарегистрироваться"
    [SerializeField] private Button forgotPasswordButton; // Кнопка "Забыли пароль?"
    [SerializeField] private TMP_Dropdown loginMethodDropdown; // Тот же самый, что и на экране анонимного входа
    [SerializeField] private TMP_Dropdown serverDropdown; // И этот тоже

    // Ссылки на менеджеры и сервисы, если они нужны прямо здесь
    private UIManager uiManager;
    private MasterServerService masterServerService;
    // ... и т.д.

    protected override void Awake()
    {
        base.Awake(); // Важно вызывать базовый Awake
        uiManager = FindObjectOfType<UIManager>();
        masterServerService = FindObjectOfType<MasterServerService>();
        // ... инициализация других сервисов ...
    }

    private void Start()
    {
        // Подписываемся на события кнопок
        loginButton?.onClick.AddListener(OnLoginButtonClicked);
        registerButton?.onClick.AddListener(OnRegisterButtonClicked);
        forgotPasswordButton?.onClick.AddListener(OnForgotPasswordClicked);

        // Выпадающие списки (логика может быть общей или специфичной)
        loginMethodDropdown?.onValueChanged.AddListener(OnLoginMethodChanged);
        // serverDropdown?.onValueChanged.AddListener(OnServerSelected); // Если нужна обработка

        // Инициализация выпадающих списков (если они должны быть здесь)
        // Обычно, если это те же самые элементы, что и на AnonymousLogin,
        // их состояние может управляться UIManager или общим родительским скриптом,
        // или же они просто дублируются на каждой панели.
        // Для простоты предположим, что они здесь тоже есть.
        InitializeLoginMethodDropdown();
        // PopulateServerDropdown(); // Метод для заполнения списка серверов, если он тут нужен
    }

    protected override void OnShow()
    {
        base.OnShow();
        Debug.Log("Registered Login Screen Shown.");
        // Установить значение "С аккаунтом" в выпадающем списке, если он управляется этим скриптом
        if (loginMethodDropdown != null)
        {
            // Найти индекс опции "С аккаунтом"
            for (int i = 0; i < loginMethodDropdown.options.Count; i++)
            {
                if (loginMethodDropdown.options[i].text == "С аккаунтом")
                {
                    loginMethodDropdown.SetValueWithoutNotify(i); // Установить без вызова onValueChanged
                    break;
                }
            }
        }
    }

    protected override void OnHide()
    {
        base.OnHide();
        Debug.Log("Registered Login Screen Hidden.");
        // Можно сбросить поля ввода
        // emailInputField.text = "";
        // passwordInputField.text = "";
    }

    private async void OnLoginButtonClicked() // Делаем асинхронным для заглушки
    {
        string email = emailInputField.text;
        string password = passwordInputField.text;

        // TODO: Валидация
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            Debug.LogError("Email and Password cannot be empty.");
            // TODO: Показать ошибку пользователю
            return;
        }

        Debug.Log($"Login attempt: Email='{email}', Password='{password}'");

        // TODO: Вызвать masterServerService.LoginAsync(email, password);
        // Заглушка:
        SetInteractable(false); // Блокируем UI
        await System.Threading.Tasks.Task.Delay(1000); // Имитация запроса

        // Имитируем успешный логин (например, если email не "error@example.com")
        bool mockLoginSuccess = (email != "error@example.com");

        if (mockLoginSuccess)
        {
            Debug.Log("Login successful (mock). Switching to User Profile.");
            // TODO: Сохранить токены ответа сервера, имя пользователя и т.д. в SessionManager
            // sessionManager.SetUserSession(response.AccessToken, response.RefreshToken, response.User.Nickname);
            PlayerPrefs.SetString("UserAccessToken", "mock_access_token_for_" + email); // Простая заглушка
            PlayerPrefs.SetString("LastLoggedInNickname", email.Split('@')[0]); // Имя из email для примера
            PlayerPrefs.Save();

            uiManager.SwitchToScreen(UIScreenType.UserProfileUI);
        }
        else
        {
            Debug.LogError("Login failed (mock). Invalid credentials or server error.");
            // TODO: Показать ошибку пользователю
            SetInteractable(true); // Разблокируем UI если ошибка
        }
        // SetInteractable(true); // Если не было перехода, то разблокировать
    }

    private void OnRegisterButtonClicked()
    {
        Debug.Log("Register button clicked. Switching to Registration Screen.");
        uiManager.SwitchToScreen(UIScreenType.Registration);
    }

    private void OnForgotPasswordClicked()
    {
        Debug.Log("Forgot Password button clicked. Switching to Password Restoration Screen.");
        uiManager.SwitchToScreen(UIScreenType.PasswordRestoration);
    }

    // Обработка изменения метода входа на этом экране
    private void OnLoginMethodChanged(int index)
    {
        if (uiManager == null) uiManager = FindObjectOfType<UIManager>();

        string selectedMethod = loginMethodDropdown.options[index].text;
        Debug.Log($"RegisteredLoginUI: Login method changed to: {selectedMethod}");

        if (selectedMethod == "Без аккаунта")
        {
            uiManager.SwitchToScreen(UIScreenType.AnonymousLogin);
        }
        // Если "С аккаунтом", то остаемся на этом экране
    }

    private void SetInteractable(bool state)
    {
        emailInputField.interactable = state;
        passwordInputField.interactable = state;
        loginButton.interactable = state;
        registerButton.interactable = state;
        forgotPasswordButton.interactable = state;
        loginMethodDropdown.interactable = state;
        serverDropdown.interactable = state;
         if(canvasGroup != null) canvasGroup.interactable = state;
    }

    private void InitializeLoginMethodDropdown()
    {
        // Точно такая же логика, как в AnonymousLoginUI
        // В идеале, эту логику можно вынести в UIManager или общий компонент,
        // если дропдаун один и тот же на разных экранах.
        // Пока для простоты дублируем:
        if (loginMethodDropdown == null) return;

        loginMethodDropdown.ClearOptions();
        List<TMP_Dropdown.OptionData> options = new List<TMP_Dropdown.OptionData>
        {
            new TMP_Dropdown.OptionData("Без аккаунта"),
            new TMP_Dropdown.OptionData("С аккаунтом")
        };
        loginMethodDropdown.AddOptions(options);
        // Устанавливаем значение по умолчанию для этого экрана
        for (int i = 0; i < loginMethodDropdown.options.Count; i++)
        {
            if (loginMethodDropdown.options[i].text == "С аккаунтом")
            {
                loginMethodDropdown.value = i;
                break;
            }
        }
        loginMethodDropdown.RefreshShownValue();
    }

    // ... Другая логика для этого экрана (валидация, взаимодействие с сервером и т.д.) ...
}