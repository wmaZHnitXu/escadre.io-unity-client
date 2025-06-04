// Scripts/UI/UserProfileUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic; // Для TMP_Dropdown
using Assets.Scripts.UILogic;

public class UserProfileUI : UIScreen
{
    [Header("UI Elements")]
    [SerializeField] private TMP_Text userNameText;
    [SerializeField] private Button statsButton;
    [SerializeField] private Button logoutButton;
    [SerializeField] private Button playButton; // Большая кнопка ">"

    [SerializeField] private TMP_Dropdown loginMethodDropdown; // Для возможности "выйти" через выбор "Без аккаунта"
    [SerializeField] private TMP_Dropdown serverDropdown; // Если нужен выбор сервера

    private UIManager uiManager;
    // private ServerListService serverListService; // Если сервер-лист нужен здесь
    // private List<GameServerInfoDto> availableServers = new List<GameServerInfoDto>(); // Если нужен сервер-лист

    protected override void Awake()
    {
        base.Awake();
        uiManager = UIManager.Instance;
        // serverListService = FindObjectOfType<ServerListService>();
    }

    private void Start()
    {
        statsButton?.onClick.AddListener(OnStatsButtonClicked);
        logoutButton?.onClick.AddListener(OnLogoutButtonClicked);
        playButton?.onClick.AddListener(OnPlayButtonClicked);
        loginMethodDropdown?.onValueChanged.AddListener(OnLoginMethodChanged    );
        // serverDropdown?.onValueChanged.AddListener(OnServerSelected);
    }

    protected override void OnShow()
    {
        base.OnShow();
        Debug.Log("User Profile Screen Shown.");
        UpdateUserInfo();
        InitializeLoginMethodDropdown();
        // FetchAndDisplayServerList(); // Если нужен список серверов
        SetUIInteractable(true);
    }

    private void UpdateUserInfo()
    {
        if (SessionManager.Instance != null && SessionManager.Instance.CurrentUser != null)
        {
            userNameText.text = SessionManager.Instance.CurrentUser.Nickname ?? "Игрок";
        }
        else
        {
            userNameText.text = "Гость"; // Или что-то другое для неавторизованного/анонимного
        }
    }

    private void OnStatsButtonClicked()
    {
        Debug.Log("Stats button clicked. Switching to PlayerStats screen.");
        uiManager.SwitchToScreen(UIScreenType.PlayerStats);
    }

    private async void OnLogoutButtonClicked()
    {
        Debug.Log("Logout button clicked.");
        SetUIInteractable(false);
        // TODO: uiManager.ShowLoadingScreen(true);

        // MasterServerApiService.LogoutAsync должен очистить сессию в SessionManager
        var (success, errorMessage) = await MasterServerApiService.Instance.LogoutAsync();

        if (success)
        {
            Debug.Log("Logout successful via API.");
        }
        else
        {
            Debug.LogError($"Logout failed via API: {errorMessage}");
            // Даже если на сервере ошибка, мы выходим локально
        }
        // MasterServerApiService.LogoutAsync уже должен был переключить на AnonymousLogin или подобный
        // или SessionManager.OnLoginStateChanged может это сделать.
        // На всякий случай, если этого не произошло:
        if (SessionManager.Instance == null || !SessionManager.Instance.IsUserLoggedIn) // Проверяем, что сессия действительно очищена
        {
            uiManager.SwitchToScreen(UIScreenType.AnonymousLogin);
        }
        else
        {
           // Что-то пошло не так, сессия не очищена
           SetUIInteractable(true);
        }

        // TODO: uiManager.ShowLoadingScreen(false);
        // SetUIInteractable(true) здесь не нужно, т.к. мы переключаем экран
    }

    private void OnPlayButtonClicked()
    {
        Debug.Log($"Login successful! User: {SessionManager.Instance?.CurrentUser?.Nickname}");
        uiManager.SwitchToScreen(UIScreenType.GameUI); // Или UIScreenType.MainMenu
    }
    
    private void InitializeLoginMethodDropdown()
    {
        if (loginMethodDropdown == null) return;
        loginMethodDropdown.ClearOptions();
        List<TMP_Dropdown.OptionData> options = new List<TMP_Dropdown.OptionData>
        {
            new TMP_Dropdown.OptionData("Без аккаунта"),
            new TMP_Dropdown.OptionData("С аккаунтом")
        };
        loginMethodDropdown.AddOptions(options);

        // Устанавливаем значение в зависимости от текущего состояния сессии
        if (SessionManager.Instance != null && SessionManager.Instance.IsUserLoggedIn && SessionManager.Instance.CurrentUser?.UserId != null && !SessionManager.Instance.CurrentUser.UserId.StartsWith("anonymous_"))
        {
            // Если пользователь залогинен с реальным аккаунтом
            for (int i = 0; i < loginMethodDropdown.options.Count; i++)
            {
                if (loginMethodDropdown.options[i].text == "С аккаунтом")
                {
                    loginMethodDropdown.SetValueWithoutNotify(i);
                    break;
                }
            }
        }
        else
        {
            // Если анонимный или не залогинен (хотя на этот экран без сессии не должны попадать)
             for (int i = 0; i < loginMethodDropdown.options.Count; i++)
            {
                if (loginMethodDropdown.options[i].text == "Без аккаунта")
                {
                    loginMethodDropdown.SetValueWithoutNotify(i);
                    break;
                }
            }
        }
        loginMethodDropdown.RefreshShownValue();
    }


    private void OnLoginMethodChanged(int index)
    {
        if (loginMethodDropdown == null) return;
        string selectedMethod = loginMethodDropdown.options[index].text;
        Debug.Log($"UserProfileUI: Login method changed to: {selectedMethod}");

        if (selectedMethod == "Без аккаунта")
        {
            // Если пользователь выбрал "Без аккаунта" на этом экране,
            // это эквивалентно логауту (если он был залогинен с аккаунтом)
            // и переходу на анонимный режим.
            if (SessionManager.Instance != null && SessionManager.Instance.IsUserLoggedIn && SessionManager.Instance.CurrentUser?.UserId != null && !SessionManager.Instance.CurrentUser.UserId.StartsWith("anonymous_"))
            {
                OnLogoutButtonClicked(); // Выполняем полный логаут
            }
            else
            {
                // Если он уже был анонимом или что-то странное, просто переключаем, если нужно
                // Но обычно этот экран для залогиненных, так что такой выбор должен вести к логауту.
                // Если мы хотим анонимный режим после логаута, то MasterServerApiService.LogoutAsync
                // должен был переключить на AnonymousLogin или UIManager должен это сделать по событию от SessionManager
            }
        }
        // Если выбран "С аккаунтом", мы уже здесь, ничего не делаем.
    }
    
    private void SetUIInteractable(bool interactable)
    {
        if (statsButton != null) statsButton.interactable = interactable;
        if (logoutButton != null) logoutButton.interactable = interactable;
        if (playButton != null) playButton.interactable = interactable;
        Debug.Log($"Play button interactable state set to: {interactable}");
        if (loginMethodDropdown != null) loginMethodDropdown.interactable = interactable;
        if (serverDropdown != null) serverDropdown.interactable = interactable;
        if (canvasGroup != null) canvasGroup.interactable = interactable;
    }

    // --- Логика для списка серверов (если нужна) ---
    // private async void FetchAndDisplayServerList() { /* ... */ }
    // private void UpdateServerDropdownDisplay() { /* ... */ }
    // private string GetSelectedServerId() { /* ... */ }
    // private void OnServerSelected(int index) { /* ... */ }
}