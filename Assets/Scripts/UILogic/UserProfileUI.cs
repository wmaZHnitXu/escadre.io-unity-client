using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic; // Для TMP_Dropdown
using Assets.Scripts.UILogic;

public class UserProfileUI : UIScreen
{
    [Header("UI Elements - User Profile")]
    [SerializeField] private TMP_Text userNameText; // Для "Your Name"
    [SerializeField] private Button statsButton;
    [SerializeField] private Button logoutButton;
    [SerializeField] private Button playButton; // Большая кнопка ">"

    // Дропдауны, если они специфичны для этого экрана
    [SerializeField] private TMP_Dropdown loginMethodDropdown;
    [SerializeField] private TMP_Dropdown serverDropdown;

    // Предполагаем, что есть некий SessionManager для хранения данных о пользователе
    // private SessionManager sessionManager;
    private UIManager uiManager;
    private MasterServerService masterServerService; // Для логаута

    protected override void Awake()
    {
        base.Awake();
        uiManager = FindObjectOfType<UIManager>();
        masterServerService = FindObjectOfType<MasterServerService>();
        // sessionManager = FindObjectOfType<SessionManager>(); // Или SessionManager.Instance
    }

    private void Start()
    {
        statsButton?.onClick.AddListener(OnStatsButtonClicked);
        logoutButton?.onClick.AddListener(OnLogoutButtonClicked);
        playButton?.onClick.AddListener(OnPlayButtonClicked);

        loginMethodDropdown?.onValueChanged.AddListener(OnLoginMethodChanged);
        // serverDropdown?.onValueChanged.AddListener(OnServerSelected);
    }

    protected override void OnShow()
    {
        base.OnShow();
        Debug.Log("User Profile Screen Shown.");
        // Отображаем имя пользователя (пока заглушка)
        // string currentUserName = sessionManager?.CurrentUser?.Nickname ?? "Player";
        string currentUserName = PlayerPrefs.GetString("LastLoggedInNickname", "Player"); // Простая заглушка с PlayerPrefs
        userNameText.text = currentUserName;

        // Настраиваем дропдауны для этого экрана
        InitializeLoginMethodDropdown();
        // PopulateServerDropdown(); // Если нужен список серверов
    }

    private void OnStatsButtonClicked()
    {
        Debug.Log("Stats button clicked. (Not implemented)");
        // TODO: Переключиться на экран статистики
        // uiManager.SwitchToScreen(UIScreenType.PlayerStats);
    }

    private async void OnLogoutButtonClicked()
    {
        Debug.Log("Logout button clicked.");
        // TODO: Вызвать masterServerService.LogoutAsync();
        // После логаута очистить сессию и вернуться на экран логина
        // sessionManager?.ClearSession();
        PlayerPrefs.DeleteKey("UserAccessToken"); // Пример очистки токена
        PlayerPrefs.DeleteKey("UserRefreshToken");
        PlayerPrefs.DeleteKey("LastLoggedInNickname");

        uiManager.SwitchToScreen(UIScreenType.AnonymousLogin); // Или RegisteredLogin
    }

    private void OnPlayButtonClicked()
    {
        Debug.Log("Play button clicked. (Not implemented)");
        // TODO: Логика начала игры, возможно, с использованием выбранного сервера
        // string selectedServerId = GetSelectedServerId();
        // ConnectToGameServer(selectedServerId);
    }

    private void OnLoginMethodChanged(int index)
    {
        if (loginMethodDropdown == null) return;
        string selectedMethod = loginMethodDropdown.options[index].text;
        Debug.Log($"UserProfileUI: Login method changed to: {selectedMethod}");

        if (selectedMethod == "Без аккаунта")
        {
            // Если пользователь выбрал "Без аккаунта" будучи залогиненным,
            // это фактически означает логаут и переход на анонимный вход.
            OnLogoutButtonClicked(); // Выполняем полный логаут
            // UIManager уже переключит экран в OnLogoutButtonClicked
        }
    }

    private void InitializeLoginMethodDropdown()
    {
        if (loginMethodDropdown == null) return;
        loginMethodDropdown.ClearOptions();
        List<TMP_Dropdown.OptionData> options = new List<TMP_Dropdown.OptionData>
        {
            new TMP_Dropdown.OptionData("Без аккаунта"), // Опция для "быстрого" логаута и перехода
            new TMP_Dropdown.OptionData("С аккаунтом")
        };
        loginMethodDropdown.AddOptions(options);

        // На этом экране по умолчанию должно быть "С аккаунтом"
        for (int i = 0; i < loginMethodDropdown.options.Count; i++)
        {
            if (loginMethodDropdown.options[i].text == "С аккаунтом")
            {
                loginMethodDropdown.SetValueWithoutNotify(i); // Установить без вызова onValueChanged
                break;
            }
        }
        loginMethodDropdown.RefreshShownValue();
    }
    // ... методы для serverDropdown, если нужны ...
}
