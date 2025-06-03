// File: Scripts/Client/Presentations/DefaultShipPresentation.cs
using UnityEngine; // Fully qualify: UnityEngine.ParticleSystem, AudioClip, AudioSource, Color, Collider
using Core.Network.Proxies;
using Logger = Core.Logging.Logger; // Alias
using Client.Presentation; // For ShipTapAreaVisualizer if it's in this namespace

public class DefaultShipPresentation : ClientProxyPresentation
{
    [Header("DefaultShip Visuals")]
    public ParticleSystem explosionEffectPrefab;
    public AudioClip explosionSound;
    private AudioSource _audioSource;

    [Header("Tap Area Visualization")]
    [Tooltip("Assign the child ShipTapAreaVisualizer component here if it exists.")]
    [SerializeField] private ShipTapAreaVisualizer tapAreaVisualizer;
    [SerializeField] private float tapAreaRadius = 2.0f; // Should match TapResolverService
    [SerializeField] private Color enemyTapAreaColor = new Color(1f, 0.5f, 0.5f, 0.3f); // Reddish for enemies
    [SerializeField] private Color friendlyTapAreaColor = new Color(0.5f, 1f, 0.5f, 0.3f); // Greenish for friendlies

    private ShipProxy.ClientProxy _shipProxy;
    private ClientComposer _clientComposer; // To check local client ID

    protected override void OnInitialized()
    {
        base.OnInitialized();
        _shipProxy = TargetProxy as ShipProxy.ClientProxy;
        if (_shipProxy == null)
        {
            Logger.LogError($"[DefaultShipPresentation {gameObject.name}] TargetProxy is not a ShipProxy.ClientProxy! Type: {TargetProxy?.GetType().Name}");
            enabled = false;
            return;
        }

        // Try to find ClientComposer to determine if this ship is friendly or enemy
        // This is a common pattern if a component needs global client info.
        _clientComposer = FindObjectOfType<ClientComposer>();
        if (_clientComposer == null)
        {
            Logger.LogError($"[DefaultShipPresentation {gameObject.name}] ClientComposer not found in scene. Cannot determine friendly/enemy status for tap area.");
        }


        _shipProxy.HealthChanged += OnHealthChanged;
        _shipProxy.CurrentSpeedChanged += OnSpeedChanged;
        _shipProxy.StatsChanged += OnStatsChanged;

        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.playOnAwake = false;

        // Attempt to find tapAreaVisualizer if not assigned
        if (tapAreaVisualizer == null)
        {
            tapAreaVisualizer = GetComponentInChildren<ShipTapAreaVisualizer>();
        }

        if (tapAreaVisualizer != null)
        {
            bool isFriendly = false;
            if (_clientComposer != null && _shipProxy.OwningEscadreClientId == _clientComposer.LocalEscadreProxy?.OwnerClientId)
            {
                isFriendly = true;
            }

            tapAreaVisualizer.RefreshVisuals(tapAreaRadius, isFriendly ? friendlyTapAreaColor : enemyTapAreaColor, tapAreaVisualizer.lineWidth);
            tapAreaVisualizer.SetVisibility(true); // Always show tap area for now, or based on selection state
            Logger.Log($"[DefaultShipPresentation {gameObject.name}] TapAreaVisualizer setup. Friendly: {isFriendly}");
        }
        else
        {
            Logger.LogWarning($"[DefaultShipPresentation {gameObject.name}] ShipTapAreaVisualizer not found as child or not assigned. Tap area will not be visualized for this ship.");
        }

        OnHealthChanged(_shipProxy.CurrentHealth, _shipProxy.MaxHealth);
        OnSpeedChanged(_shipProxy.ClientSimulatedSpeed);
        OnStatsChanged();
    }

    private void OnHealthChanged(float current, float max)
    {
        var r = GetComponent<Renderer>();
        if (r != null && max > 0) r.material.color = Color.Lerp(Color.red, Color.green, current / max);
    }
    private void OnSpeedChanged(float speed)
    {
        // Logger.Log($"[DefaultShipPresentation {gameObject.name}] Speed: {speed}");
    }

    private void OnStatsChanged()
    {
        if (_shipProxy != null)
        {
            // Logger.Log($"[DefaultShipPresentation {gameObject.name}] Stats Updated - MaxSpeed: {_shipProxy.MaxSpeed}, TurnRate: {_shipProxy.TurnRate}");
        }
    }


    protected override void HandleLoudDestruction()
    {
        base.HandleLoudDestruction();
        if (explosionEffectPrefab != null) Instantiate(explosionEffectPrefab, transform.position, transform.rotation);
        if (explosionSound != null && _audioSource != null) _audioSource.PlayOneShot(explosionSound);

        var mainRenderer = GetComponent<Renderer>(); if (mainRenderer != null) mainRenderer.enabled = false;
        var mainUnityCollider = GetComponent<Collider>(); if (mainUnityCollider != null) mainUnityCollider.enabled = false;

        if (tapAreaVisualizer != null) tapAreaVisualizer.SetVisibility(false); // Hide tap area on destruction
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
    }

    // Could add methods to be called by a selection manager:
    // public void OnSelected() { if (tapAreaVisualizer != null) tapAreaVisualizer.SetVisibility(true); }
    // public void OnDeselected() { if (tapAreaVisualizer != null) tapAreaVisualizer.SetVisibility(false); }
}