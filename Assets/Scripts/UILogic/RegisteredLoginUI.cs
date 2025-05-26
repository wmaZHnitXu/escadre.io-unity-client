// Scripts/UI/RegisteredLoginUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;
using Assets.Scripts.UILogic;

public class RegisteredLoginUI : UIScreen
{
    [Header("UI Elements")]
    [SerializeField] private TMP_InputField emailInputField;
    [SerializeField] private TMP_InputField passwordInputField;
    [SerializeField] private Button loginButton;
    [SerializeField] private Button registerButton;
    [SerializeField] private Button forgotPasswordButton;
    [SerializeField] private TMP_Dropdown loginMethodDropdown;
    [SerializeField] private TMP_Dropdown serverDropdown; // Если нужен выбор сервера

    private UIManager uiManager;

    protected override void Awake()
    {
        base.Awake();
        //masterServerService = FindObjectOfType<MasterServerService>();
        uiManager = UIManager.Instance;
    }

    private void Start()
    {
        loginButton?.onClick.AddListener(OnLoginButtonClicked);
        registerButton?.onClick.AddListener(OnRegisterButtonClicked);
        forgotPasswordButton?.onClick.AddListener(OnForgotPasswordClicked);
        loginMethodDropdown?.onValueChanged.AddListener(OnLoginMethodChanged);
        // serverDropdown?.onValueChanged.AddListener(OnServerSelected);
        InitializeLoginMethodDropdown();
    }

    protected override void OnShow()
    {
        base.OnShow();
        Debug.Log("Registered Login Screen Shown.");
        emailInputField.text = ""; // Очищаем поля при показе
        passwordInputField.text = "";

        if (loginMethodDropdown != null)
        {
            for (int i = 0; i < loginMethodDropdown.options.Count; i++)
            {
                if (loginMethodDropdown.options[i].text == "С аккаунтом")
                {
                    loginMethodDropdown.SetValueWithoutNotify(i);
                    break;
                }
            }
        }
        // PopulateServerDropdown(); // Если нужен список серверов
    }

    private async void OnLoginButtonClicked()
    {
        string email = emailInputField.text;
        string password = passwordInputField.text;

        // TODO: Базовая валидация на клиенте (непустые поля, формат email)
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            Debug.LogError("Email and Password cannot be empty.");
            // TODO: Показать ошибку пользователю
            // errorDisplay.Show("Поля Email и Пароль не могут быть пустыми.");
            return;
        }

        Debug.Log($"Attempting to login: Email='{email}'");
        SetUIInteractable(false);
        // TODO: uiManager.ShowLoadingScreen(true);

        var (success, response, errorMessage) = await MasterServerApiService.Instance.LoginAsync(email, password);

        if (success && response != null)
        {
            // SessionManager уже должен был обновиться внутри MasterServerApiService.LoginAsync
            Debug.Log($"Login successful! User: {SessionManager.Instance?.CurrentUser?.Nickname}, Token: {SessionManager.Instance?.AccessToken?.Substring(0, 10)}...");
            uiManager.SwitchToScreen(UIScreenType.UserProfileUI); // Или UIScreenType.MainMenu
        }
        else
        {
            Debug.LogError($"Login failed: {errorMessage}");
            // TODO: Показать пользователю errorMessage
            // errorDisplay.Show($"Ошибка входа: {errorMessage}");
        }

        SetUIInteractable(true);
        // TODO: uiManager.ShowLoadingScreen(false);
    }

    private void OnRegisterButtonClicked()
    {
        uiManager.SwitchToScreen(UIScreenType.Registration);
    }

    private void OnForgotPasswordClicked()
    {
        uiManager.SwitchToScreen(UIScreenType.PasswordRestoration);
    }

    private void InitializeLoginMethodDropdown() // Вызывается из Start или OnShow
    {
        if (loginMethodDropdown == null) return;
        loginMethodDropdown.ClearOptions();
        List<TMP_Dropdown.OptionData> options = new List<TMP_Dropdown.OptionData>
        {
            new TMP_Dropdown.OptionData("Без аккаунта"),
            new TMP_Dropdown.OptionData("С аккаунтом")
        };
        loginMethodDropdown.AddOptions(options);
        // Устанавливаем "С аккаунтом" при инициализации этого экрана
        for (int i = 0; i < loginMethodDropdown.options.Count; i++)
        {
            if (loginMethodDropdown.options[i].text == "С аккаунтом")
            {
                loginMethodDropdown.SetValueWithoutNotify(i);
                break;
            }
        }
        loginMethodDropdown.RefreshShownValue();
    }


    private void OnLoginMethodChanged(int index)
    {
        if (loginMethodDropdown == null) return;
        string selectedMethod = loginMethodDropdown.options[index].text;
        Debug.Log($"RegisteredLoginUI: Login method changed to: {selectedMethod}");

        if (selectedMethod == "Без аккаунта")
        {
            uiManager.SwitchToScreen(UIScreenType.AnonymousLogin);
        }
    }

    private void SetUIInteractable(bool interactable)
    {
        emailInputField.interactable = interactable;
        passwordInputField.interactable = interactable;
        loginButton.interactable = interactable;
        registerButton.interactable = interactable;
        forgotPasswordButton.interactable = interactable;
        loginMethodDropdown.interactable = interactable;
        if (serverDropdown != null) serverDropdown.interactable = interactable;
        if (canvasGroup != null) canvasGroup.interactable = interactable;
    }
}