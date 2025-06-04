// File: Scripts/Client/UI/ClientUIManager.cs
using UnityEngine;
using Logger = Core.Logging.Logger;
using System.Collections.Generic; // For List
using Client.UI.Formation; // For FormationUI

namespace Client.UI
{
    public class ClientUIManager : MonoBehaviour, IUIContext // Implement IUIContext
    {
        [Header("Core Dependencies")]
        [SerializeField] private ClientComposer clientComposer; // Must be assigned

        [Header("UI Controllers/Displays")]
        [Tooltip("Manages displaying player resources.")]
        [SerializeField] private ResourceDisplay resourceDisplay;
        [Tooltip("Manages showing/hiding UI groups based on session state (Loading, Gameplay, Game Over).")]
        [SerializeField] private SessionStateDisplay sessionStateDisplay;
        
        // Changed from FormationUIMediator to FormationUI
        [Tooltip("Manages the formation editing UI window.")]
        [SerializeField] private FormationUI formationUIScreen; // <<<< MODIFIED HERE


        [Header("Action Button Handlers")]
        [Tooltip("Handles the 'Cancel Attack' button functionality.")]
        [SerializeField] private CancelAttackButtonHandler cancelAttackButtonHandler;
        [Tooltip("Handles a 'Buy Default Ship' button. Configure its ShipDesignIdToBuy in its Inspector.")]
        [SerializeField] private BuyShipButtonHandler buyDefaultShipButtonHandler;
        [Tooltip("Handles the 'Upgrade First Ship' button.")]
        [SerializeField] private UpgradeShipButtonHandler upgradeShipButtonHandler;
        [Tooltip("Handles the 'Set Random Formation' button.")]
        [SerializeField] private SetFormationButtonHandler setFormationButtonHandler;

        [SerializeField] private List<BaseActionButtonHandler> allActionHandlers = new List<BaseActionButtonHandler>();


        private bool _isFullyInitialized = false;

        void Start()
        {
            if (allActionHandlers.Count == 0)
            {
                if (cancelAttackButtonHandler != null) allActionHandlers.Add(cancelAttackButtonHandler);
                if (buyDefaultShipButtonHandler != null) allActionHandlers.Add(buyDefaultShipButtonHandler);
                if (upgradeShipButtonHandler != null) allActionHandlers.Add(upgradeShipButtonHandler);
                if (setFormationButtonHandler != null) allActionHandlers.Add(setFormationButtonHandler);
            }
        }

        public void Initialize(ClientComposer composer)
        {
            if (_isFullyInitialized)
            {
                Logger.LogWarning("[ClientUIManager] Already fully initialized.");
                return;
            }
            clientComposer = composer ?? throw new System.ArgumentNullException(nameof(composer));

            // Initialize main display controllers
            if (resourceDisplay != null) resourceDisplay.Initialize(this);
            else Logger.LogWarning("[ClientUIManager] ResourceDisplay not assigned.");

            if (sessionStateDisplay != null) sessionStateDisplay.Initialize(this);
            else Logger.LogWarning("[ClientUIManager] SessionStateDisplay not assigned.");

            // Initialize FormationUI screen
            if (formationUIScreen != null) formationUIScreen.Initialize(this); // <<<< MODIFIED HERE
            else Logger.LogWarning("[ClientUIManager] FormationUIScreen not assigned.");


            // Initialize all action button handlers
            if (allActionHandlers.Count > 0)
            {
                foreach (var handler in allActionHandlers)
                {
                    if (handler != null) handler.Initialize(this);
                    else Logger.LogWarning("[ClientUIManager] Found a null entry in allActionHandlers list.");
                }
            }
            else 
            {
                if (cancelAttackButtonHandler != null) cancelAttackButtonHandler.Initialize(this);
                else Logger.LogWarning("[ClientUIManager] CancelAttackButtonHandler not assigned.");

                if (buyDefaultShipButtonHandler != null) buyDefaultShipButtonHandler.Initialize(this);
                else Logger.LogWarning("[ClientUIManager] BuyDefaultShipButtonHandler not assigned.");

                if (upgradeShipButtonHandler != null) upgradeShipButtonHandler.Initialize(this);
                else Logger.LogWarning("[ClientUIManager] UpgradeShipButtonHandler not assigned.");

                if (setFormationButtonHandler != null) setFormationButtonHandler.Initialize(this);
                else Logger.LogWarning("[ClientUIManager] SetFormationButtonHandler not assigned.");
            }

            _isFullyInitialized = true;
            Logger.Log("[ClientUIManager] Fully initialized all UI components.");
        }

        public ClientComposer GetClientComposer()
        {
            return clientComposer;
        }

        void OnDestroy()
        {
            // Cleanup if necessary
        }
    }
}