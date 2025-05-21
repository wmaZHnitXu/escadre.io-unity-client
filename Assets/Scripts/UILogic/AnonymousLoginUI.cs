// Scripts/UI/AnonymousLoginUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;
using Assets.Scripts.UILogic;
// using DG.Tweening; // Убираем DoTween

public class AnonymousLoginUI : UIScreen // Наследуемся от UIScreen
{
    [Header("UI Elements")]
    [SerializeField] private TMP_InputField nicknameInputField;
    [SerializeField] private Button playButton;
    [SerializeField] private TMP_Dropdown loginMethodDropdown;
    [SerializeField] private TMP_Dropdown serverDropdown;
    // [SerializeField] private CanvasGroup panelCanvasGroup; // Это поле теперь в UIScreen

    // Убираем настройки анимации
    // [Header("Animation Settings")]
    // [SerializeField] private float animationDuration = 0.3f;
    // [SerializeField] private Ease showEase = Ease.OutQuad;
    // [SerializeField] private Ease hideEase = Ease.InQuad;

    private UIManager uiManager;
    private MasterServerService masterServerService;
    private ServerListService serverListService;

    private List<GameServerInfoDto> availableServers = new List<GameServerInfoDto>();

    // Awake уже есть в UIScreen, если нужна специфичная логика для AnonymousLoginUI в Awake,
    // можно переопределить, не забыв вызвать base.Awake()
    protected override void Awake()
    {
        base.Awake(); // Вызываем Awake базового класса

        // Ваша специфичная логика для Awake в AnonymousLoginUI, если нужна
        uiManager = FindObjectOfType<UIManager>();
        masterServerService = FindObjectOfType<MasterServerService>();
        serverListService = FindObjectOfType<ServerListService>();
    }

    private void Start()
    {
        playButton.onClick.AddListener(OnPlayButtonClicked);
        nicknameInputField.onValueChanged.AddListener(OnNicknameInputValueChanged);
        loginMethodDropdown.onValueChanged.AddListener(OnLoginMethodChanged);
        serverDropdown.onValueChanged.AddListener(OnServerSelected);

        ValidateNickname(nicknameInputField.text);
        InitializeLoginMethodDropdown();
        // В реальном проекте: FetchAndDisplayServerList();
        // Для теста пока оставим так:
        if (serverListService != null)
        {
             FetchAndDisplayServerList(); // Попробуем загрузить при старте
        }
        else
        {
            Debug.LogWarning("ServerListService not found on Start, using dummy data for servers.");
            PopulateServerDropdownWithDummyData();
        }
    }

    // Убираем ShowPanel/HidePanel, так как они теперь в UIScreen
    // #region Public Methods for UIManager (Show/Hide)
    // ...
    // #endregion

    // Переопределяем OnShow, если нужно что-то делать при показе этого экрана
    protected override void OnShow()
    {
        base.OnShow();
        Debug.Log("Anonymous Login Screen Shown. Ready for input.");
        // Установить значение "Без аккаунта" в выпадающем списке
        if (loginMethodDropdown != null)
        {
            // Найти индекс опции "Без аккаунта"
            for (int i = 0; i < loginMethodDropdown.options.Count; i++)
            {
                if (loginMethodDropdown.options[i].text == "Без аккаунта")
                {
                    // Используем SetValueWithoutNotify, чтобы не вызвать OnLoginMethodChanged снова и не уйти в цикл
                    loginMethodDropdown.SetValueWithoutNotify(i);
                    break;
                }
            }
        }
        // FetchAndDisplayServerList(); // Если нужно обновлять при каждом показе
        // nicknameInputField.Select();
        // nicknameInputField.ActivateInputField();
    }

    // Переопределяем OnHide, если нужно что-то делать при скрытии
    protected override void OnHide()
    {
        base.OnHide();
        Debug.Log("Anonymous Login Screen Hidden.");
        // Например, сбросить поля ввода, если это необходимо
        // nicknameInputField.text = "";
    }


    #region UI Element Logic

    private void OnNicknameInputValueChanged(string newNickname)
    {
        ValidateNickname(newNickname);
    }

    private void ValidateNickname(string nickname)
    {
        bool isValid = !string.IsNullOrWhiteSpace(nickname) && nickname.Length >= 3;
        playButton.interactable = isValid;
        // Убираем анимацию кнопки
        // if (isValid)
        // {
        //     playButton.transform.DOScale(1.05f, 0.1f).SetLoops(2, LoopType.Yoyo);
        // }
    }

    private async void OnPlayButtonClicked()
    {
        string nickname = nicknameInputField.text;
        if (string.IsNullOrWhiteSpace(nickname))
        {
            Debug.LogError("Nickname cannot be empty!");
            // nicknameInputField.transform.DOShakePosition(0.5f, new Vector3(10, 0, 0), 10, 90, false, true); // Убираем анимацию
            return;
        }

        string selectedServerId = GetSelectedServerId();
        if (string.IsNullOrEmpty(selectedServerId))
        {
            Debug.LogError("No server selected or server list is empty!");
            // serverDropdown.transform.DOShakePosition(0.5f, new Vector3(10, 0, 0), 10, 90, false, true); // Убираем анимацию
            return;
        }

        Debug.Log($"Attempting to login anonymously with Nickname: {nickname} to Server: {selectedServerId}");
        SetUIInteractable(false);

        if (masterServerService != null)
        {
            var (success, response, errorMessage) = await masterServerService.GetAnonymousTokenAsync(nickname);
            if (success && response != null)
            {
                Debug.Log($"Anonymous login successful! Token: {response.AccessToken}");
                // SessionManager.Instance.SetTokens(response.AccessToken, response.NewRefreshToken, response.AccessTokenExpiration);
                // SessionManager.Instance.SetNickname(nickname);
                // SessionManager.Instance.SetSelectedServer(GetSelectedServerInfo());
                uiManager.SwitchToScreen(UIScreenType.GameUI);
            }
            else
            {
                Debug.LogError($"Anonymous login failed: {errorMessage}");
                SetUIInteractable(true);
            }
        }
        else
        {
            Debug.LogError("MasterServerService not found!");
            SetUIInteractable(true);
        }
    }

    private void InitializeLoginMethodDropdown()
    {
        loginMethodDropdown.ClearOptions();
        List<TMP_Dropdown.OptionData> options = new List<TMP_Dropdown.OptionData>
        {
            new TMP_Dropdown.OptionData("Без аккаунта"),
            new TMP_Dropdown.OptionData("С аккаунтом")
        };
        loginMethodDropdown.AddOptions(options);
        loginMethodDropdown.value = 0;
        loginMethodDropdown.RefreshShownValue();
    }

    private void OnLoginMethodChanged(int index)
    {
        // Убедимся, что ссылка на UIManager есть
        if (uiManager == null) uiManager = FindObjectOfType<UIManager>();

        string selectedMethod = loginMethodDropdown.options[index].text;
        Debug.Log($"AnonymousLoginUI: Login method changed to: {selectedMethod}");

        if (selectedMethod == "С аккаунтом")
        {
            // Переключаемся на экран входа с аккаунтом
            uiManager.SwitchToScreen(UIScreenType.RegisteredLogin);
        }
        // Если выбран "Без аккаунта", ничего дополнительно делать не нужно, т.к. мы уже на этом экране
    }

    private void PopulateServerDropdownWithDummyData()
    {
        availableServers.Clear();
        availableServers.Add(new GameServerInfoDto { Id = "moscow_1", Name = "Москва", Ping = 23 });
        availableServers.Add(new GameServerInfoDto { Id = "europe_1", Name = "Европа", Ping = 50 });
        UpdateServerDropdownDisplay();
    }

    public async void FetchAndDisplayServerList()
    {
        if (serverListService == null)
        {
            Debug.LogError("ServerListService not found! Using dummy data.");
            PopulateServerDropdownWithDummyData();
            return;
        }

        var (success, servers, errorMessage) = await serverListService.GetServerListAsync();
        if (success && servers != null)
        {
            availableServers = servers;
            UpdateServerDropdownDisplay();
        }
        else
        {
            Debug.LogError($"Failed to fetch server list: {errorMessage}");
            availableServers.Clear();
            UpdateServerDropdownDisplay();
        }
    }

    private void UpdateServerDropdownDisplay()
    {
        serverDropdown.ClearOptions();
        List<TMP_Dropdown.OptionData> serverOptions = new List<TMP_Dropdown.OptionData>();
        if (availableServers.Count == 0)
        {
            serverOptions.Add(new TMP_Dropdown.OptionData("Нет доступных серверов"));
            serverDropdown.interactable = false;
        }
        else
        {
            foreach (var server in availableServers)
            {
                serverOptions.Add(new TMP_Dropdown.OptionData($"{server.Name} ({server.Ping} мс)"));
            }
            serverDropdown.interactable = true;
        }
        serverDropdown.AddOptions(serverOptions);
        if (availableServers.Count > 0) serverDropdown.value = 0;
        serverDropdown.RefreshShownValue();
    }

    private void OnServerSelected(int index)
    {
        if (availableServers.Count > 0 && index >= 0 && index < availableServers.Count)
        {
            GameServerInfoDto selectedServer = availableServers[index];
            Debug.Log($"Server selected: {selectedServer.Name} (ID: {selectedServer.Id})");
        }
    }

    private string GetSelectedServerId()
    {
        if (availableServers.Count > 0 && serverDropdown.value >= 0 && serverDropdown.value < availableServers.Count)
        {
            return availableServers[serverDropdown.value].Id;
        }
        return null;
    }

    private GameServerInfoDto GetSelectedServerInfo()
    {
         if (availableServers.Count > 0 && serverDropdown.value >= 0 && serverDropdown.value < availableServers.Count)
        {
            return availableServers[serverDropdown.value];
        }
        return null;
    }

    #endregion

    #region Helper Methods
    private void SetUIInteractable(bool interactable)
    {
        // Если canvasGroup был автоматически добавлен в UIScreen, можно его использовать
        // if (canvasGroup != null)
        // {
        //     canvasGroup.interactable = interactable;
        // }
        // else // Иначе, по старинке
        // {
            nicknameInputField.interactable = interactable;
            playButton.interactable = interactable && !string.IsNullOrWhiteSpace(nicknameInputField.text);
            loginMethodDropdown.interactable = interactable;
            serverDropdown.interactable = interactable && availableServers.Count > 0;
        // }

        // Убираем анимацию прозрачности
        // float targetAlpha = interactable ? 1f : 0.7f;
        // nicknameInputField.GetComponent<CanvasGroup>()?.DOFade(targetAlpha, 0.1f);
        // playButton.GetComponent<CanvasGroup>()?.DOFade(targetAlpha, 0.1f);
    }
    #endregion
}

// DTO GameServerInfoDto и TokenResponseDto остаются как были (или из отдельных файлов)