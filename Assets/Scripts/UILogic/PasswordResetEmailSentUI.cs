// Scripts/UI/Messages/PasswordResetEmailSentUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro; // Для текста таймера и email
using System.Collections; // Для корутины таймера
using Assets.Scripts.UILogic;

public class PasswordResetEmailSentUI : UIScreen
{
    [SerializeField] private TMP_Text emailSentToText; // Текст "Мы отправили на почту yournamespecialsvo@proton.mail..."
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button resendButton;
    [SerializeField] private TMP_Text resendButtonText; // Текст на кнопке "Отправить ещё раз (1:05)"

    private UIManager uiManager;
    private MasterServerService masterServerService; // Для повторной отправки

    private float resendCooldown = 65f; // 1 минута 5 секунд = 65 секунд
    private float currentCooldown;
    private Coroutine resendTimerCoroutine;
    private string userEmailForResend; // Сохраняем email для повторной отправки

    protected override void Awake()
    {
        base.Awake();
        uiManager = FindObjectOfType<UIManager>();
        masterServerService = FindObjectOfType<MasterServerService>();
    }

    private void Start()
    {
        cancelButton?.onClick.AddListener(OnCancelButtonClicked);
        resendButton?.onClick.AddListener(OnResendButtonClicked);
        resendButton.interactable = false; // По умолчанию кнопка неактивна, пока идет таймер
    }

    // Метод для установки email, который будет отображаться и использоваться для повторной отправки
    public void SetUserEmail(string email)
    {
        userEmailForResend = email;
        if (emailSentToText != null)
        {
            // Форматируем текст как на скриншоте
            emailSentToText.text = $"Мы отправили на почту <color=#YOUR_HIGHLIGHT_COLOR>{email}</color> письмо со ссылкой, после перехода по которой пароль будет сброшен.";
            // Замените #YOUR_HIGHLIGHT_COLOR на нужный вам цвет, например, <color=yellow> или <color=#FFFF00>
            // Если цвет не нужен, просто: $"Мы отправили на почту {email} письмо..."
        }
    }

    protected override void OnShow()
    {
        base.OnShow();
        Debug.Log("Password Reset Email Sent Screen Shown.");
        StartResendTimer();
    }

    protected override void OnHide()
    {
        base.OnHide();
        if (resendTimerCoroutine != null)
        {
            StopCoroutine(resendTimerCoroutine);
            resendTimerCoroutine = null;
        }
    }

    private void OnCancelButtonClicked()
    {
        uiManager.SwitchToScreen(UIScreenType.RegisteredLogin);
    }

    private async void OnResendButtonClicked()
    {
        if (string.IsNullOrEmpty(userEmailForResend) || !resendButton.interactable) return;

        Debug.Log($"Resending password reset email to: {userEmailForResend}");
        // TODO: Вызвать masterServerService.RequestPasswordResetAsync(userEmailForResend);
        // Заглушка
        resendButton.interactable = false; // Блокируем кнопку на время запроса
        // Тут может быть небольшой индикатор загрузки на самой кнопке

        await System.Threading.Tasks.Task.Delay(1000); // Имитация запроса
        bool mockResendSuccess = true; // Имитация

        if (mockResendSuccess)
        {
            Debug.Log("Password reset email resent successfully (mock).");
            StartResendTimer(); // Перезапускаем таймер
            // TODO: Показать пользователю сообщение "Письмо отправлено повторно"
        }
        else
        {
            Debug.LogError("Failed to resend password reset email (mock).");
            // TODO: Показать ошибку
            resendButton.interactable = true; // Разблокируем, если ошибка
        }
    }

    private void StartResendTimer()
    {
        if (resendTimerCoroutine != null)
        {
            StopCoroutine(resendTimerCoroutine);
        }
        currentCooldown = resendCooldown;
        resendButton.interactable = false;
        resendTimerCoroutine = StartCoroutine(ResendTimerCoroutine());
    }

    private IEnumerator ResendTimerCoroutine()
    {
        while (currentCooldown > 0)
        {
            currentCooldown -= Time.deltaTime;
            int minutes = Mathf.FloorToInt(currentCooldown / 60f);
            int seconds = Mathf.FloorToInt(currentCooldown % 60f);
            resendButtonText.text = $"Отправить ещё раз ({minutes:00}:{seconds:00})";
            yield return null;
        }
        currentCooldown = 0;
        resendButtonText.text = "Отправить ещё раз";
        resendButton.interactable = true;
        resendTimerCoroutine = null;
    }
}