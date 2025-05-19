// Scripts/UI/UIManager.cs
using UnityEngine;
using System.Collections.Generic;
using System;
using Assets.Scripts.UILogic;

public class UIManager : MonoBehaviour
{
    [Header("Screen References")]
    [SerializeField] private AnonymousLoginUI anonymousLoginScreen; // Меняем тип
    // [SerializeField] private RegisteredLoginScreen registeredLoginScreen; // Тоже будет UIScreen
    [SerializeField] private GameUI gameUI; // И это

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
        // if (registeredLoginScreen != null) registeredLoginScreen.Hide(true);
        // if (mainMenuScreen != null) mainMenuScreen.Hide(true);
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

    private UIScreen GetScreenByType(UIScreenType screenType)
    {
        switch (screenType)
        {
            case UIScreenType.AnonymousLogin: return anonymousLoginScreen;
            // case UIScreenType.RegisteredLogin: return registeredLoginScreen;
            case UIScreenType.GameUI: return gameUI;
            default:
                Debug.LogError($"No screen configured for UIScreenType: {screenType}");
                return null;
        }
    }
}