// Scripts/UI/Messages/EmailConfirmationSentUI.cs (можно создать подпапку Messages)
using UnityEngine;
using UnityEngine.UI; // Для Button
using Assets.Scripts.UILogic;

public class EmailConfirmationSentUI : UIScreen
{
    [SerializeField] private Button cancelButton; // Кнопка "Отмена" или "ОК"

    private UIManager uiManager;

    protected override void Awake()
    {
        base.Awake();
        uiManager = FindObjectOfType<UIManager>();
    }

    private void Start()
    {
        cancelButton?.onClick.AddListener(OnCancelButtonClicked);
    }

    protected override void OnShow()
    {
        base.OnShow();
        Debug.Log("Email Confirmation Sent Screen Shown.");
        // Здесь можно, например, запустить таймер, если нужно автоматически закрыть окно
        // или показать какую-то анимацию
    }

    private void OnCancelButtonClicked()
    {
        // После этого сообщения пользователь обычно возвращается на экран логина
        // или на главный экран, если предполагается, что он может войти позже.
        // Для простоты вернемся на экран логина.
        uiManager.SwitchToScreen(UIScreenType.RegisteredLogin);
    }
}