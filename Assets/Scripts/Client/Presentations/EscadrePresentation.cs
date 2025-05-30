// File: Scripts/Client/Presentations/EscadrePresentation.cs
using UnityEngine;
using Core.Network.Proxies;
using Core.Model; // For FormationSlot if needed for debug
using TMPro; // If using TextMeshPro for UI
using Logger = Core.Logging.Logger;
using System.Linq; // For Linq operations on FormationSlots

public class EscadrePresentation : ClientProxyPresentation
{
    [Header("Escadre Visuals")]
    [SerializeField] private TextMeshProUGUI nicknameText;
    [SerializeField] private TextMeshProUGUI resourcesText;
    [SerializeField] private TextMeshProUGUI shipCountText;
    // TODO: Add references for formation slot visualizations if needed

    private EscadreProxy.ClientProxy _escadreProxy;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        _escadreProxy = TargetProxy as EscadreProxy.ClientProxy;
        if (_escadreProxy == null)
        {
            Logger.LogError($"[EscadrePresentation {gameObject.name}] TargetProxy is not an EscadreProxy.ClientProxy! Type: {TargetProxy?.GetType().Name}");
            enabled = false;
            return;
        }

        _escadreProxy.OnNicknameChanged += UpdateNicknameDisplay;
        _escadreProxy.OnResourcesChanged += UpdateResourcesDisplay;
        _escadreProxy.OnFormationChanged += UpdateFormationDisplay;
        // Optional: _escadreProxy.OnShopDesignsChanged += HandleShopDesignsChanged;

        // Initial UI Update
        UpdateNicknameDisplay();
        UpdateResourcesDisplay();
        UpdateFormationDisplay();

        // Set GameObject scale or other general escadre visual properties
        // For an Escadre, which is more of an abstract anchor, its visual might be simple (e.g., a flag or just a conceptual point)
        // Or it could have a specific model representing the "flagship" concept if desired.
        // For now, we'll assume it's mainly a point in space that its ships follow.
        // If this presentation had a distinct model, you'd configure it here.
        var mainRenderer = GetComponent<Renderer>();
        if (mainRenderer != null)
        {
            // Example: Make escadre anchor slightly visible
            mainRenderer.material.color = new Color(0.2f, 0.8f, 0.2f, 0.5f); // Greenish, semi-transparent
        }
    }

    private void UpdateNicknameDisplay()
    {
        if (_escadreProxy == null) return;
        if (nicknameText != null)
        {
            nicknameText.text = $"Escadre: {_escadreProxy.Nickname ?? "N/A"} (ID: {_escadreProxy.EntityId})";
        }
        // Logger.Log($"[EscadrePresentation {gameObject.name}] Nickname updated: {_escadreProxy.Nickname}");
    }

    private void UpdateResourcesDisplay()
    {
        if (_escadreProxy == null) return;
        if (resourcesText != null)
        {
            resourcesText.text = $"Resources: {_escadreProxy.Resources}";
        }
        // Logger.Log($"[EscadrePresentation {gameObject.name}] Resources updated: {_escadreProxy.Resources}");
    }

    private void UpdateFormationDisplay()
    {
        if (_escadreProxy == null) return;
        int currentShipCount = _escadreProxy.FormationSlots.Count(slot => slot.ShipEntityId.HasValue);
        if (shipCountText != null)
        {
            shipCountText.text = $"Ships: {currentShipCount}";
        }
        // Logger.Log($"[EscadrePresentation {gameObject.name}] Formation updated. Ship count: {currentShipCount}");

        // TODO: More complex formation visualization
        // - Instantiate/position ship placeholder GameObjects based on FormationSlots
        // - Or draw Gizmos for slot positions relative to this EscadrePresentation's transform
    }

    protected override void HandleLoudDestruction()
    {
        base.HandleLoudDestruction();
        // Example: Play a sound or a general "escadre defeated" effect
        Logger.Log($"[EscadrePresentation {gameObject.name}] Escadre ID {_escadreProxy?.EntityId} loud destruction signaled.");
        if (nicknameText != null) nicknameText.text = "DEFEATED";
        if (resourcesText != null) resourcesText.text = "";
        if (shipCountText != null) shipCountText.text = "";
    }

    protected override void UnsubscribeFromProxyEvents()
    {
        base.UnsubscribeFromProxyEvents();
        if (_escadreProxy != null)
        {
            _escadreProxy.OnNicknameChanged -= UpdateNicknameDisplay;
            _escadreProxy.OnResourcesChanged -= UpdateResourcesDisplay;
            _escadreProxy.OnFormationChanged -= UpdateFormationDisplay;
            // _escadreProxy.OnShopDesignsChanged -= HandleShopDesignsChanged;
        }
    }

    // Optional Gizmos for debugging formation slots
    void OnDrawGizmosSelected()
    {
        if (_escadreProxy != null && TargetProxy != null) // Check if target proxy exists even if not fully initialized for drawing
        {
            Gizmos.color = Color.green;
            foreach (var slot in _escadreProxy.FormationSlots)
            {
                // Convert slot's 2D relative offset to a 3D world offset from the escadre's current orientation
                Vector3 localOffset3D = new Vector3(slot.RelativeOffset.X, 0, slot.RelativeOffset.Y);
                Vector3 worldOffset = transform.rotation * localOffset3D; // Use presentation's transform as base
                Vector3 slotWorldPosition = transform.position + worldOffset;
                
                Gizmos.DrawSphere(slotWorldPosition, 0.3f); // Draw a small sphere for each slot
                if (slot.ShipEntityId.HasValue)
                {
                    #if UNITY_EDITOR
                    UnityEditor.Handles.Label(slotWorldPosition + Vector3.up * 0.5f, $"Ship: {slot.ShipEntityId.Value}");
                    #endif
                }
            }
        }
    }
}