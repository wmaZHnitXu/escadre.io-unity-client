// Scripts/UI/UIManager.cs
using UnityEngine;
using System.Collections.Generic;
using System;
using Assets.Scripts.UILogic;

public class UIManager : MonoBehaviour
{
    [Header("Screen References")]
    [SerializeField] private AnonymousLoginUI anonymousLoginScreen; // Меняем тип
    [SerializeField] private RegisteredLoginUI registeredLoginScreen; // Тоже будет UIScreen
    [SerializeField] private GameUI gameUI; // И это
    [SerializeField] private RegistrationUI registrationScreen;           // <--- ДОБАВИТЬ
    [SerializeField] private PasswordRestorationUI passwordRestorationScreen; // <--- ДОБАВИТЬ
    [SerializeField] private UserProfileUI userProfileScreen; 
    [SerializeField] private EmailConfirmationSentUI emailConfirmationSentScreen;
    [SerializeField] private PasswordResetEmailSentUI passwordResetEmailSentScreen;
    [SerializeField] private EnterNewPasswordUI enterNewPasswordScreen;
    [SerializeField] private AccountNotFoundUI accountNotFoundScreen;
    [SerializeField] private PlayerStatsUI playerStatsScreen;
    [SerializeField] private ErrorScreenUI errorScreen;
    [SerializeField] private ShopUI shopScreen;
    [SerializeField] private FormationUI formationScreen;

    private UIScreen currentVisibleScreen; // Используем UIScreen

    public static UIManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        HideAllScreensInstantly(); // Скрываем все при старте
    }

    private void Start()
    {
        // Показать начальный экран
        // Убедитесь, что anonymousLoginScreen назначен в инспекторе
        if (anonymousLoginScreen != null)
        {
            SwitchToScreen(UIScreenType.AnonymousLogin, true); // true - без анимации скрытия предыдущего
        }
        else
        {
            Debug.LogError("AnonymousLoginScreen is not assigned in UIManager!");
        }
    }

    private void HideAllScreensInstantly()
    {
        // Пройдемся по всем экранам, которые у вас есть, и скроем их
        // Это нужно будет расширять по мере добавления экранов
        if (anonymousLoginScreen != null) anonymousLoginScreen.Hide(true);
        if (registeredLoginScreen != null) registeredLoginScreen.Hide(true);
        if (gameUI != null) gameUI.Hide(true);
        if (registrationScreen != null) registrationScreen.Hide(true);
        if (passwordRestorationScreen != null) passwordRestorationScreen.Hide(true);
        if (userProfileScreen != null) userProfileScreen.Hide(true);
        if (emailConfirmationSentScreen != null) emailConfirmationSentScreen.Hide(true);
        if (passwordResetEmailSentScreen != null) passwordResetEmailSentScreen.Hide(true);
        if (enterNewPasswordScreen != null) enterNewPasswordScreen.Hide(true);
        if (accountNotFoundScreen != null) accountNotFoundScreen.Hide(true);
        if (playerStatsScreen != null) playerStatsScreen.Hide(true);
        if (errorScreen != null) errorScreen.Hide(true);
        if (shopScreen != null) shopScreen.Hide(true);
        if (formationScreen != null) formationScreen.Hide(true);
    }

    public void SwitchToScreen(UIScreenType screenType, bool hideInstantly = false, Action onSwitched = null)
    {
        UIScreen screenToHide = currentVisibleScreen;
        UIScreen screenToShow = GetScreenByType(screenType);

        if (screenToShow == null)
        {
            Debug.LogError($"Screen for type {screenType} not found or not assigned in UIManager!");
            onSwitched?.Invoke();
            return;
        }

        if (screenToHide == screenToShow && screenToShow.IsVisible)
        {
             Debug.LogWarning($"Already on screen {screenType}.");
             // Можно вызвать OnShow еще раз, если это имеет смысл для обновления
             // screenToShow.OnShow();
             onSwitched?.Invoke();
             return;
        }

        Action showNext = () =>
        {
            currentVisibleScreen = screenToShow;
            screenToShow.Show(onSwitched); // Используем метод Show из UIScreen
        };

        if (screenToHide != null && screenToHide.IsVisible)
        {
            screenToHide.Hide(hideInstantly, showNext); // Используем метод Hide из UIScreen
        }
        else
        {
            showNext(); // Если нечего скрывать, просто показываем следующий
        }
    }

    public void ShowErrorScreen(string title, string message, Action onBackAction = null, UIScreenType? screenToReturnTo = null)
    {
        ErrorScreenUI es = GetScreenByType(UIScreenType.ErrorScreen) as ErrorScreenUI;
        if (es != null)
        {
            Action backAction = onBackAction;
            if (backAction == null && screenToReturnTo.HasValue)
            {
                backAction = () => SwitchToScreen(screenToReturnTo.Value);
            }
            else if (backAction == null) // Действие по умолчанию, если ничего не указано
            {
                backAction = () => {
                    // Попытаться вернуться на предыдущий экран, если есть такая логика,
                    // или на главный экран по умолчанию.
                    // Для простоты пока на AnonymousLogin.
                    SwitchToScreen(UIScreenType.AnonymousLogin);
                };
            }
            es.SetupError(title, message, backAction);
            SwitchToScreen(UIScreenType.ErrorScreen);
        }
        else
        {
            Debug.LogError("ErrorScreen is not assigned or found in UIManager. Cannot display error.");
            // Как запасной вариант, можно просто залогировать ошибку, если UI для ошибки не работает
            Debug.LogError($"FALLBACK ERROR DISPLAY: Title: {title}, Message: {message}");
        }
    }

    public UIScreen GetScreenByType(UIScreenType screenType)
    {
        switch (screenType)
        {
            case UIScreenType.AnonymousLogin: return anonymousLoginScreen;
            case UIScreenType.RegisteredLogin: return registeredLoginScreen;
            case UIScreenType.GameUI: return gameUI;
            case UIScreenType.Registration: return registrationScreen;             
            case UIScreenType.PasswordRestoration: return passwordRestorationScreen; 
            case UIScreenType.UserProfileUI: return userProfileScreen;
            case UIScreenType.EmailConfirmationSent: return emailConfirmationSentScreen;
            case UIScreenType.PasswordResetEmailSent: return passwordResetEmailSentScreen;
            case UIScreenType.EnterNewPassword: return enterNewPasswordScreen;
            case UIScreenType.AccountNotFound: return accountNotFoundScreen;
            case UIScreenType.PlayerStats: return playerStatsScreen;
            case UIScreenType.ErrorScreen: return errorScreen;
            case UIScreenType.ShopUI: return shopScreen;
            case UIScreenType.FormationUI: return formationScreen;
            
            default:
                Debug.LogError($"No screen configured for UIScreenType: {screenType}");
                return null;
        }
    }
}