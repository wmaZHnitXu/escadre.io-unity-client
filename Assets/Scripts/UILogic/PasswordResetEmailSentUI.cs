// Scripts/UI/Messages/PasswordResetEmailSentUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro; // Для текста таймера и email
using System.Collections; // Для корутины таймера
using Assets.Scripts.UILogic;

public class PasswordResetEmailSentUI : UIScreen
{
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button resendButton;
    [SerializeField] private TMP_Text resendButtonText; // Текст на кнопке "Отправить ещё раз (1:05)"

    private UIManager uiManager;
    private MasterServerApiService masterServerApiService;

    private float resendCooldown = 65f; // 1 минута 5 секунд = 65 секунд
    private float currentCooldown;
    private Coroutine resendTimerCoroutine;
    private string userEmailForResend; // Сохраняем email для повторной отправки

    protected override void Awake()
    {
        base.Awake();
        uiManager = FindObjectOfType<UIManager>();
        masterServerApiService = MasterServerApiService.Instance;
        if (masterServerApiService == null)
        {
            Debug.LogError($"{this.GetType().Name}: MasterServerApiService.Instance is null!");
        }
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
        var (success, response, errorMessage) = await masterServerApiService.RequestPasswordResetAsync(userEmailForResend);
        // TODO: uiManager.ShowLoadingScreen(false);

        if (success && response != null && response.IsSuccess)
        {
            Debug.Log($"Password reset email resent successfully to {userEmailForResend}. Server message: {response.Error}");
            StartResendTimer();
            // TODO: Показать пользователю сообщение "Письмо отправлено повторно"
        }
        else
        {
            Debug.LogError($"Failed to resend password reset email: {errorMessage}");
            // TODO: Показать ошибку
            resendButton.interactable = true; // Разблокируем, если ошибка, чтобы можно было попробовать еще раз после кулдауна (таймер не перезапускаем)
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