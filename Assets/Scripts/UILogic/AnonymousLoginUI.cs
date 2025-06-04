// Scripts/UI/AnonymousLoginUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic; // Для списка серверов
using Assets.Scripts.UILogic;

public class AnonymousLoginUI : UIScreen
{
    [Header("UI Elements")]
    [SerializeField] private TMP_InputField nicknameInputField;
    [SerializeField] private Button playButton;
    [SerializeField] private TMP_Dropdown loginMethodDropdown;
    [SerializeField] private TMP_Dropdown serverDropdown; // Если нужен выбор сервера для анонимов


    private UIManager uiManager;
    // private ServerListService serverListService; // Если сервер-лист нужен здесь

    // private List<GameServerInfoDto> availableServers = new List<GameServerInfoDto>(); // Если нужен сервер-лист

    protected override void Awake()
    {
        base.Awake();
        uiManager = UIManager.Instance; // Предполагаем, что UIManager уже существует
        // serverListService = FindObjectOfType<ServerListService>();
    }

    private void Start()
    {
        playButton?.onClick.AddListener(OnPlayButtonClicked);
        nicknameInputField?.onValueChanged.AddListener(OnNicknameInputValueChanged);
        loginMethodDropdown?.onValueChanged.AddListener(OnLoginMethodChanged);
        // serverDropdown?.onValueChanged.AddListener(OnServerSelected);

        ValidateNickname(nicknameInputField?.text ?? "");
        InitializeLoginMethodDropdown();
        // PopulateServerDropdown(); // Если нужен сервер-лист
    }

    protected override void OnShow()
    {
        base.OnShow();
        Debug.Log("Anonymous Login Screen Shown.");
        nicknameInputField.text = ""; // Очищаем поле ника при показе
        ValidateNickname("");

        if (loginMethodDropdown != null)
        {
            for (int i = 0; i < loginMethodDropdown.options.Count; i++)
            {
                if (loginMethodDropdown.options[i].text == "Без аккаунта")
                {
                    loginMethodDropdown.SetValueWithoutNotify(i);
                    break;
                }
            }
        }
        // FetchAndDisplayServerList(); // Если нужно обновлять список серверов
    }

    private void OnNicknameInputValueChanged(string newNickname)
    {
        ValidateNickname(newNickname);
    }

    private void ValidateNickname(string nickname)
    {
        bool isValid = !string.IsNullOrWhiteSpace(nickname) && nickname.Length >= 1;
        if (playButton != null) playButton.interactable = isValid;
    }

    private async void OnPlayButtonClicked()
    {
        string nickname = nicknameInputField.text;
        if (string.IsNullOrWhiteSpace(nickname) || nickname.Length < 1)
        {
            Debug.LogError("Nickname is too short or empty!");
            // TODO: Показать ошибку пользователю (например, всплывающее сообщение или подсветка поля)
            // errorDisplay.Show("Никнейм должен содержать минимум 3 символа.");
            return;
        }

        // string selectedServerId = GetSelectedServerId(); // Если есть выбор сервера

        Debug.Log($"Attempting to login anonymously with Nickname: {nickname}");
        SetUIInteractable(false);
        // TODO: Показать индикатор загрузки (например, uiManager.ShowLoadingScreen(true))

        var (success, response, errorMessage) = await MasterServerApiService.Instance.GetAnonymousTokenAsync(nickname);

        if (success && response != null)
        {
            Debug.Log($"Anonymous login successful! User: {SessionManager.Instance?.CurrentUser?.Nickname}");
            // SessionManager уже должен был создать сессию внутри GetAnonymousTokenAsync в MasterServerApiService
            // Переключаемся на следующий экран (например, главное меню или лобби)
            //uiManager.SwitchToScreen(UIScreenType.GameUI); // Или UIScreenType.MainMenu
            DuctTape.Instance.GoPlayTheGame();
        }
        else
        {
            Debug.LogError($"Anonymous login failed: {errorMessage}");
            // Показываем ошибку пользователю. Кнопка "Назад" вернет на AnonymousLogin.
            UIManager.Instance.ShowErrorScreen("Ошибка Входа", errorMessage, null, UIScreenType.AnonymousLogin);
            SetUIInteractable(true); // Разблокируем текущий экран, т.к. остались на нем (пока не нажали "Назад" на ErrorScreen)
        }

        SetUIInteractable(true);
        // TODO: Скрыть индикатор загрузки (uiManager.ShowLoadingScreen(false))
    }

    private void InitializeLoginMethodDropdown()
    {
        if (loginMethodDropdown == null) return;
        loginMethodDropdown.ClearOptions();
        List<TMP_Dropdown.OptionData> options = new List<TMP_Dropdown.OptionData>
        {
            new TMP_Dropdown.OptionData("С аккаунтом"),
            new TMP_Dropdown.OptionData("Без аккаунта")
        };
        loginMethodDropdown.AddOptions(options);
        loginMethodDropdown.SetValueWithoutNotify(1); // "Без аккаунта" по умолчанию
        loginMethodDropdown.RefreshShownValue();
    }

    private void OnLoginMethodChanged(int index)
    {
        if (loginMethodDropdown == null) return;
        string selectedMethod = loginMethodDropdown.options[index].text;
        Debug.Log($"AnonymousLoginUI: Login method changed to: {selectedMethod}");

        if (selectedMethod == "С аккаунтом")
        {
            uiManager.SwitchToScreen(UIScreenType.RegisteredLogin);
        }
    }

    private void SetUIInteractable(bool interactable)
    {
        nicknameInputField.interactable = interactable;
        if (playButton != null) playButton.interactable = interactable && !string.IsNullOrWhiteSpace(nicknameInputField.text) && nicknameInputField.text.Length >=3;
        loginMethodDropdown.interactable = interactable;
        if (serverDropdown != null) serverDropdown.interactable = interactable; // && availableServers.Count > 0;
        if (canvasGroup != null) canvasGroup.interactable = interactable;
    }

    // --- Логика для списка серверов (если нужна) ---
    // private async void FetchAndDisplayServerList() { /* ... */ }
    // private void UpdateServerDropdownDisplay() { /* ... */ }
    // private string GetSelectedServerId() { /* ... */ }
    // private void OnServerSelected(int index) { /* ... */ }
}