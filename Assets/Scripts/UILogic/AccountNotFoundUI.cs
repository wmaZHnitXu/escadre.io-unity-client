// Scripts/UI/Messages/AccountNotFoundUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Assets.Scripts.UILogic;

public class AccountNotFoundUI : UIScreen
{
    [SerializeField] private TMP_Text messageText; // Для "Аккаунта с почтой ... не существует"
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

    // Метод для установки email, который не был найден
    public void SetMissingEmail(string email)
    {
        if (messageText != null)
        {
            messageText.text = $"Аккаунта с почтой <color=yellow>{email}</color> не существует.\nПроверьте корректность введённого адреса.";
        }
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