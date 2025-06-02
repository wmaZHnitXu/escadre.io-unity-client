// Scripts/UI/ErrorScreenUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Assets.Scripts.UILogic;
using System; // Для Action

public class ErrorScreenUI : UIScreen
{
    [Header("UI Elements")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button backButton;

    private UIManager uiManager;
    private Action currentOnBackAction; // Действие при нажатии "Назад"

    protected override void Awake()
    {
        base.Awake();
        uiManager = UIManager.Instance;
    }

    private void Start()
    {
        backButton?.onClick.AddListener(OnBackButtonClicked);
    }

    /// <summary>
    /// Показывает экран ошибки с указанным заголовком, сообщением и действием для кнопки "Назад".
    /// </summary>
    /// <param name="title">Заголовок окна ошибки (например, "Ошибка!").</param>
    /// <param name="message">Текст ошибки.</param>
    /// <param name="onBackAction">Действие, выполняемое при нажатии кнопки "Назад". Если null, вернется на предыдущий активный экран или на главный.</param>
    public void SetupError(string title, string message, Action onBackAction = null)
    {
        if (titleText != null) titleText.text = title;
        if (messageText != null) messageText.text = message;
        
        currentOnBackAction = onBackAction;
    }

    private void OnBackButtonClicked()
    {
        if (currentOnBackAction != null)
        {
            currentOnBackAction.Invoke();
        }
        else
        {
            // Поведение по умолчанию: если есть предыдущий экран, пытаемся на него вернуться,
            // иначе на какой-то "безопасный" экран, например, анонимного входа.
            // UIManager должен будет хранить стек предыдущих экранов или иметь логику
            // для определения "предыдущего" или "домашнего" экрана.
            // Пока что просто переключим на AnonymousLogin, если нет специфичного действия.
            Debug.Log("ErrorScreen: No specific back action, switching to AnonymousLogin.");
            uiManager.SwitchToScreen(UIScreenType.AnonymousLogin);
        }
    }

    protected override void OnShow()
    {
        base.OnShow();
        // Можно не очищать текст ошибки при OnShow, так как он устанавливается через SetupError
        Debug.Log($"Error Screen Shown: Title='{titleText?.text}', Message='{messageText?.text}'");
    }
}