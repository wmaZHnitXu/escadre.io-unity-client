// File: Scripts/Client/Presentations/CruiserShipPresentation.cs
using UnityEngine; // Fully qualify: UnityEngine.ParticleSystem, AudioClip, AudioSource, Color, Collider
using Core.Network.Proxies;
using Logger = Core.Logging.Logger; // Alias
using Client.Presentation; // For ShipTapAreaVisualizer if it's in this namespace
using Client.InputServices;
using System.Linq; // For TapResolverService related colors if used dynamically

public class CruiserShipPresentation : ClientProxyPresentation
{
    [Header("CruiserShipPresentation Visuals")]
    public ParticleSystem explosionEffectPrefab;
    public AudioClip explosionSound;
    private AudioSource _audioSource;

    [Header("Tap Area Visualization")]
    [Tooltip("Assign the child ShipTapAreaVisualizer component here if it exists.")]
    [SerializeField] private ShipTapAreaVisualizer tapAreaVisualizer;
    [SerializeField] private float tapAreaRadius = 2.0f; // Should match TapResolverService SHIP_TAP_RADIUS
    
    // Colors for different states of the tap area
    [SerializeField] private Color friendlyTapAreaColor = new Color(0.5f, 1f, 0.5f, 0.3f); // Greenish
    [SerializeField] private Color enemyTapAreaColor = new Color(1f, 0.5f, 0.5f, 0.3f);    // Reddish
    [SerializeField] private Color enemyTargetedTapAreaColor = new Color(1f, 0.2f, 0.2f, 0.5f); // Brighter/Deeper Red

    private ShipProxy.ClientProxy _shipProxy;
    private ClientComposer _clientComposer; // To check local client ID and targeted enemies

    protected override void OnInitialized()
    {
        base.OnInitialized();
        _shipProxy = TargetProxy as ShipProxy.ClientProxy;
        if (_shipProxy == null)
        {
            Logger.LogError($"[CruiserShipPresentation {gameObject.name}] TargetProxy is not a ShipProxy.ClientProxy! Type: {TargetProxy?.GetType().Name}");
            enabled = false;
            return;
        }

        _clientComposer = FindObjectOfType<ClientComposer>(); // Common way to get global context
        if (_clientComposer == null)
        {
            Logger.LogWarning($"[CruiserShipPresentation {gameObject.name}] ClientComposer not found in scene. Cannot determine advanced tap area states.");
        }

        _shipProxy.HealthChanged += OnHealthChanged;
        _shipProxy.CurrentSpeedChanged += OnSpeedChanged;
        _shipProxy.StatsChanged += OnStatsChanged;
        // If client composer's local escadre proxy changes, we might need to re-evaluate tap area color
        if (_clientComposer != null)
        {
            _clientComposer.OnLocalEscadreProxyChanged += HandleLocalEscadreProxyChangedForTapArea;
            if (_clientComposer.LocalEscadreProxy != null)
            {
                _clientComposer.LocalEscadreProxy.OnTargetEscadreEntityIdsChanged += UpdateTapAreaVisuals;
            }
        }


        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.playOnAwake = false;

        if (tapAreaVisualizer == null)
        {
            tapAreaVisualizer = GetComponentInChildren<ShipTapAreaVisualizer>();
        }
        
        UpdateTapAreaVisuals(); // Initial setup of tap area

        OnHealthChanged(_shipProxy.CurrentHealth, _shipProxy.MaxHealth);
        OnSpeedChanged(_shipProxy.ClientSimulatedSpeed);
        OnStatsChanged();
    }

    private void HandleLocalEscadreProxyChangedForTapArea(EscadreProxy.ClientProxy newLocalEscadreProxy)
    {
        // Unsubscribe from old local escadre's target changes
        if (_clientComposer != null && _clientComposer.LocalEscadreProxy != null && _clientComposer.LocalEscadreProxy != newLocalEscadreProxy)
        {
             _clientComposer.LocalEscadreProxy.OnTargetEscadreEntityIdsChanged -= UpdateTapAreaVisuals;
        }
        // Subscribe to new local escadre's target changes
        if (newLocalEscadreProxy != null)
        {
            newLocalEscadreProxy.OnTargetEscadreEntityIdsChanged += UpdateTapAreaVisuals;
        }
        UpdateTapAreaVisuals();
    }


    private void UpdateTapAreaVisuals()
    {
        if (tapAreaVisualizer == null || _shipProxy == null) return;

        bool isFriendly = false;
        bool isTargetedByPlayer = false;

        if (_clientComposer != null && _clientComposer.LocalEscadreProxy != null)
        {
            if (_shipProxy.OwningEscadreClientId == _clientComposer.LocalEscadreProxy.OwnerClientId)
            {
                isFriendly = true;
            }
            else // It's an enemy ship
            {
                // Check if this ship's escadre is targeted by the local player
                if (_clientComposer.LocalEscadreProxy.TargetEscadreEntityIds.Contains(_shipProxy.OwningEscadreClientId))
                {
                     // This check is slightly off: TargetEscadreEntityIds contains Escadre Entity IDs,
                     // not OwningEscadreClientId. We need the actual escadre proxy of this ship.
                     if (_clientComposer.ClientLevel.TryGetProxy(_shipProxy.OwningEscadreClientId, out var escadreIProxy) && escadreIProxy is EscadreProxy.ClientProxy thisShipsEscadre)
                     {
                         if (_clientComposer.LocalEscadreProxy.TargetEscadreEntityIds.Contains(thisShipsEscadre.EntityId))
                         {
                            isTargetedByPlayer = true;
                         }
                     }
                }
            }
        }

        Color chosenColor;
        if (isFriendly)
        {
            chosenColor = friendlyTapAreaColor;
        }
        else // Enemy
        {
            chosenColor = isTargetedByPlayer ? enemyTargetedTapAreaColor : enemyTapAreaColor;
        }
        
        tapAreaVisualizer.RefreshVisuals(tapAreaRadius, chosenColor, tapAreaVisualizer.lineWidth);
        // Visibility can be controlled based on selection state or if it's always visible
        tapAreaVisualizer.SetVisibility(true);
    }


    private void OnHealthChanged(float current, float max)
    {
        var r = GetComponent<Renderer>();
        if (r != null && max > 0) r.material.color = Color.Lerp(Color.red, Color.green, current / max);
    }
    private void OnSpeedChanged(float speed) { /* ... */ }
    private void OnStatsChanged() { /* ... */ }


    protected override void HandleLoudDestruction()
    {
        base.HandleLoudDestruction();
        if (explosionEffectPrefab != null) Instantiate(explosionEffectPrefab, transform.position, transform.rotation);
        if (explosionSound != null && _audioSource != null) _audioSource.PlayOneShot(explosionSound);

        var mainRenderer = GetComponent<Renderer>(); if (mainRenderer != null) mainRenderer.enabled = false;
        var mainUnityCollider = GetComponent<Collider>(); if (mainUnityCollider != null) mainUnityCollider.enabled = false;

        if (tapAreaVisualizer != null) tapAreaVisualizer.SetVisibility(false);
    }

    protected override void UnsubscribeFromProxyEvents()
    {
        base.UnsubscribeFromProxyEvents();
        if (_shipProxy != null)
        {
            _shipProxy.HealthChanged -= OnHealthChanged;
            _shipProxy.CurrentSpeedChanged -= OnSpeedChanged;
            _shipProxy.StatsChanged -= OnStatsChanged;
        }
        if (_clientComposer != null)
        {
            _clientComposer.OnLocalEscadreProxyChanged -= HandleLocalEscadreProxyChangedForTapArea;
            if (_clientComposer.LocalEscadreProxy != null)
            {
                _clientComposer.LocalEscadreProxy.OnTargetEscadreEntityIdsChanged -= UpdateTapAreaVisuals;
            }
        }
    }
}