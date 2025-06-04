// Scripts/UI/GameUI.cs
using UnityEngine;
using UnityEngine.UI;
using Assets.Scripts.UILogic; // Assuming UIManager and UIScreenType are here

public class GameUI : UIScreen
{
    [Header("Test Buttons")]
    [SerializeField] private Button openShopButton;
    [SerializeField] private Button toggleFormationButton; // <<<< ADDED HERE

    private UIManager uiManager;

    protected override void Awake()
    {
        base.Awake();
        uiManager = UIManager.Instance;
        if (uiManager == null)
        {
            Debug.LogError("UIManager.Instance is not found in GameUI!");
        }
    }

    private void Start()
    {
        if (openShopButton != null)
        {
            openShopButton.onClick.AddListener(OnOpenShopButtonClicked);
        }
        else
        {
            Debug.LogWarning("OpenShopButton is not assigned in GameUI Inspector.");
        }

        if (toggleFormationButton != null) // <<<< ADDED HERE
        {
            toggleFormationButton.onClick.AddListener(OnToggleFormationButtonClicked);
        }
        else
        {
            Debug.LogWarning("ToggleFormationButton is not assigned in GameUI Inspector.");
        }
    }

    private void OnOpenShopButtonClicked()
    {
        if (uiManager != null)
        {
            Debug.Log("Open Shop button clicked. Switching to Shop screen.");
            uiManager.SwitchToScreen(UIScreenType.ShopUI); 
        }
        else
        {
            Debug.LogError("UIManager is not available to switch screens.");
        }
    }

    private void OnToggleFormationButtonClicked() // <<<< ADDED HERE
    {
        if (uiManager != null)
        {
            Debug.Log("Toggle Formation button clicked. Requesting FormationUI screen.");
            // This assumes your UIManager will toggle: show if hidden, hide if shown.
            // Or, if SwitchToScreen always shows, you might need a different mechanism
            // if you want it to strictly toggle. For now, this will likely just show it.
            // If UIManager.SwitchToScreen(type) hides other screens and shows 'type',
            // then pressing it again might just re-show it, not toggle.
            // A more robust toggle might involve checking uiManager.IsScreenVisible(UIScreenType.FormationUI)
            // and calling Show or Hide explicitly.
            // For simplicity of this request, using SwitchToScreen as requested.
            uiManager.SwitchToScreen(UIScreenType.FormationUI); 
        }
        else
        {
            Debug.LogError("UIManager is not available to switch screens for FormationUI.");
        }
    }

    protected override void OnShow()    
    {
        base.OnShow(); 
        Debug.Log("GameUI Shown. Ready for input.");
        if(openShopButton != null) openShopButton.gameObject.SetActive(true);
        if(toggleFormationButton != null) toggleFormationButton.gameObject.SetActive(true); // <<<< ADDED HERE
    }

    protected override void OnHide()
    {
        base.OnHide();
        Debug.Log("GameUI Hidden.");
    }
}