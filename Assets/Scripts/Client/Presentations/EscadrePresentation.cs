// File: Scripts/Client/Presentations/EscadrePresentation.cs
using UnityEngine;
using Core.Network.Proxies;
using Core.Model; 
using TMPro; 
using Logger = Core.Logging.Logger;
using System.Linq; 

public class EscadrePresentation : ClientProxyPresentation
{
    [Header("Escadre Visuals")]
    [SerializeField] private TextMeshProUGUI nicknameText;
    [SerializeField] private TextMeshProUGUI resourcesText;
    [SerializeField] private TextMeshProUGUI shipCountText;
    
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
        // Example: if shop designs were important for this presentation
        // _escadreProxy.OnShopDesignsChanged += UpdateShopDesignsDisplay;


        UpdateNicknameDisplay();
        UpdateResourcesDisplay();
        UpdateFormationDisplay();
        // UpdateShopDesignsDisplay(); // If applicable

        // Escadre visual might be a flag or simple marker.
        // Its position is the average of its ships, managed by ClientProxyPresentation's Update().
        var mainRenderer = GetComponent<Renderer>();
        if (mainRenderer != null)
        {
            mainRenderer.material.color = new Color(0.1f, 0.6f, 0.1f, 0.7f); // Darker Green
        }
    }

    private void UpdateNicknameDisplay()
    {
        if (_escadreProxy == null) return;
        if (nicknameText != null)
        {
            nicknameText.text = $"Escadre: {_escadreProxy.Nickname ?? "N/A"} (ID: {_escadreProxy.EntityId})";
        }
    }

    private void UpdateResourcesDisplay()
    {
        if (_escadreProxy == null) return;
        if (resourcesText != null)
        {
            resourcesText.text = $"Resources: {_escadreProxy.Resources}";
        }
    }

    private void UpdateFormationDisplay()
    {
        if (_escadreProxy == null) return;
        int currentShipCount = _escadreProxy.FormationSlots.Count(slot => slot.ShipEntityId.HasValue);
        if (shipCountText != null)
        {
            shipCountText.text = $"Ships: {currentShipCount}";
        }

        // More complex formation visualization could be done here if needed,
        // e.g., drawing Gizmos for slot positions relative to this EscadrePresentation's transform.
    }

    // Example for shop designs if relevant to this presentation
    // private void UpdateShopDesignsDisplay() { ... }

    protected override void HandleLoudDestruction()
    {
        base.HandleLoudDestruction();
        Logger.Log($"[EscadrePresentation {gameObject.name}] Escadre ID {_escadreProxy?.EntityId} loud destruction signaled.");
        if (nicknameText != null) nicknameText.text = "DEFEATED";
        if (resourcesText != null) resourcesText.text = "---";
        if (shipCountText != null) shipCountText.text = "---";
    }

    protected override void UnsubscribeFromProxyEvents()
    {
        base.UnsubscribeFromProxyEvents();
        if (_escadreProxy != null)
        {
            _escadreProxy.OnNicknameChanged -= UpdateNicknameDisplay;
            _escadreProxy.OnResourcesChanged -= UpdateResourcesDisplay;
            _escadreProxy.OnFormationChanged -= UpdateFormationDisplay;
            // _escadreProxy.OnShopDesignsChanged -= UpdateShopDesignsDisplay;
        }
    }

    // Optional Gizmos for debugging formation slots relative to the Escadre's average position
    void OnDrawGizmosSelected()
    {
        if (_escadreProxy != null && TargetProxy != null && Application.isPlaying) // Ensure proxy is valid and game is running
        {
            Gizmos.color = Color.green;
            // The Escadre's transform.position is already the average of its ships.
            // The _escadreProxy.FormationSlots offsets are relative to the *commanded fleet target point*.
            // To visualize them relative to the *current average position* (this.transform.position),
            // we need to consider the Escadre's current orientation (_escadreProxy.Rotation),
            // which itself is oriented towards the commanded target point.
            
            // So, these Gizmos will show where the formation slots *would be* if the fleet's
            // center (this.transform.position) was the command target point, oriented by this.transform.rotation.
            // This is a reasonable way to visualize the intended local formation.
            
            foreach (var slot in _escadreProxy.FormationSlots)
            {
                Vector3 localOffset3D = new Vector3(slot.RelativeOffset.X, 0, slot.RelativeOffset.Y);
                // Apply the Escadre's current rotation (which reflects its commanded orientation)
                Vector3 worldOffset = _escadreProxy.Rotation.ToUnityQuaternion() * localOffset3D; 
                Vector3 slotWorldPosition = transform.position + worldOffset; // Relative to current Escadre average position
                
                Gizmos.DrawSphere(slotWorldPosition, 0.3f); 
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