// Scripts/UI/GameUI.cs (или где у вас этот файл)
using UnityEngine;
using UnityEngine.UI; // Для Button
using Assets.Scripts.UILogic; // Для UIScreen, UIScreenType, UIManager

public class GameUI : UIScreen
{
    [Header("Test Buttons")]
    [SerializeField] private Button openShopButton; // Кнопка для открытия магазина

    private UIManager uiManager;

    protected override void Awake()
    {
        base.Awake();
        uiManager = UIManager.Instance; // Получаем UIManager
        if (uiManager == null)
        {
            Debug.LogError("UIManager.Instance is not found in GameUI!");
        }
    }

    private void Start()
    {
        // Подписываемся на событие нажатия кнопки
        if (openShopButton != null)
        {
            openShopButton.onClick.AddListener(OnOpenShopButtonClicked);
        }
        else
        {
            Debug.LogWarning("OpenShopButton is not assigned in GameUI Inspector.");
        }
    }

    private void OnOpenShopButtonClicked()
    {
        if (uiManager != null)
        {
            Debug.Log("Open Shop button clicked. Switching to Shop screen.");
            // Убедитесь, что UIScreenType.Shop - это правильный enum для вашего экрана магазина
            uiManager.SwitchToScreen(UIScreenType.ShopUI); 
        }
        else
        {
            Debug.LogError("UIManager is not available to switch screens.");
        }
    }

    protected override void OnShow()    
    {
        base.OnShow(); 
        Debug.Log("GameUI Shown. Ready for input.");
        // Можно сделать кнопку активной только при показе, если нужно
        if(openShopButton != null) openShopButton.gameObject.SetActive(true);
    }

    protected override void OnHide()
    {
        base.OnHide();
        Debug.Log("GameUI Hidden.");
        // Можно скрыть кнопку, если она специфична только для этого экрана
        // if(openShopButton != null) openShopButton.gameObject.SetActive(false);
    }

    // Не забудьте отписаться от события в OnDestroy, если GameUI может уничтожаться
    // Но так как это UIScreen, управляемый UIManager, обычно он просто деактивируется,
    // а не уничтожается во время сессии игры. Если он может быть уничтожен, добавьте:
    /*
    private void OnDestroy()
    {
        if (openShopButton != null)
        {
            openShopButton.onClick.RemoveListener(OnOpenShopButtonClicked);
        }
    }
    */
}