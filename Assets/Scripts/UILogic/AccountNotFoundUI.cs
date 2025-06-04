// Scripts/UI/Messages/AccountNotFoundUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Assets.Scripts.UILogic;

public class AccountNotFoundUI : UIScreen
{
    [SerializeField] private Button backButton;

    private UIManager uiManager;

    protected override void Awake()
    {
        base.Awake();
        uiManager = FindObjectOfType<UIManager>();
    }

    private void Start()
    {
        backButton?.onClick.AddListener(OnBackButtonClicked);
    }

    protected override void OnShow()
    {
        base.OnShow();
        Debug.Log("Account Not Found Screen Shown.");
    }

    private void OnBackButtonClicked()
    {
        // Возвращаемся на экран, с которого был инициирован поиск (обычно это PasswordRestoration)
        uiManager.SwitchToScreen(UIScreenType.PasswordRestoration);
    }
}