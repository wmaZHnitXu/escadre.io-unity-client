// Scripts/UI/PlayerStatsUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Assets.Scripts.UILogic; // Для UIScreen, UIScreenType

public class PlayerStatsUI : UIScreen
{
    [Header("UI Elements")]
    [SerializeField] private TMP_Text nicknameText;
    [SerializeField] private TMP_Text killsText;
    [SerializeField] private TMP_Text deathsText;
    [SerializeField] private TMP_Text playTimeText;
    [SerializeField] private TMP_Text kdRatioText;
    [SerializeField] private Button backButton;

    private UIManager uiManager;

    protected override void Awake()
    {
        base.Awake();
        uiManager = UIManager.Instance;
    }

    private void Start()
    {
        backButton?.onClick.AddListener(OnBackButtonClicked);
    }

    protected override async void OnShow() // Делаем async, так как будем загружать данные
    {
        base.OnShow();
        Debug.Log("Player Stats Screen Shown.");
        ClearStats(); // Очищаем старые данные
        SetUIInteractable(false); // Блокируем на время загрузки
        // TODO: uiManager.ShowLoadingIndicator(true);

        var (success, stats, errorMessage) = await MasterServerApiService.Instance.GetMyStatsAsync();
        Debug.Log($"[PlayerStatsUI.OnShow] GetMyStatsAsync result: success={success}, errorMessage='{errorMessage}'");
        if (stats != null)
        {
            Debug.Log($"[PlayerStatsUI.OnShow] Received stats: Nickname='{stats.Nickname}', Kills='{stats.Kills}', Deaths='{stats.Deaths}', PlayTime='{stats.PlayTime}', KDRatio='{stats.KDRatio}'");
        }
        else
        {
            Debug.LogWarning("[PlayerStatsUI.OnShow] Received stats object is NULL.");
        }
        
        // TODO: uiManager.ShowLoadingIndicator(false);
        SetUIInteractable(true);

        if (success && stats != null)
        {
            DisplayStats(stats);
        }
        else
        {
            Debug.LogError($"Failed to load player stats: {errorMessage}");
            // TODO: Показать ошибку пользователю на экране статистики
            // Может быть, показать сообщение "Не удалось загрузить статистику"
            // и оставить кнопку "Назад" активной.
        }
    }

    private void ClearStats()
    {
        nicknameText.text = "Загрузка...";
        killsText.text = "Убийства: -";
        deathsText.text = "Смерти: -";
        playTimeText.text = "Время в игре: -";
        kdRatioText.text = "K/D: -";
    }

    private void DisplayStats(PlayerStatsDto stats)
    {
        nicknameText.text = $"{stats.Nickname}";
        killsText.text = $"Убийства: {stats.Kills}";
        deathsText.text = $"Смерти: {stats.Deaths}";
        playTimeText.text = $"Время в игре: {stats.PlayTime}";
        kdRatioText.text = $"K/D: {stats.KDRatio:F2}"; // F2 для форматирования float с 2 знаками после запятой
    }

    private void OnBackButtonClicked()
    {
        // Возвращаемся на экран профиля пользователя
        uiManager.SwitchToScreen(UIScreenType.UserProfileUI);
    }
    
    private void SetUIInteractable(bool state)
    {
        // Здесь можно блокировать специфичные элементы, если нужно,
        // но обычно достаточно CanvasGroup из базового UIScreen, если он управляет интерактивностью.
        // Если CanvasGroup не используется для interactable, то:
        backButton.interactable = state;
    }
}