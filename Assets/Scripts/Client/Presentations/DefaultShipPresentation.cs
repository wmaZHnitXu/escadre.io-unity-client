// File: Scripts/Client/Presentations/DefaultShipPresentation.cs
using UnityEngine;
using Core.Network.Proxies;
using Logger = Core.Logging.Logger;

public class DefaultShipPresentation : ClientProxyPresentation
{
    [Header("DefaultShip Visuals")]
    public ParticleSystem explosionEffectPrefab;
    public AudioClip explosionSound;
    private AudioSource _audioSource;

    private ShipProxy.ClientProxy _shipProxy;

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

        _shipProxy.HealthChanged += OnHealthChanged;
        _shipProxy.CurrentSpeedChanged += OnSpeedChanged; 
        _shipProxy.StatsChanged += OnStatsChanged; // Subscribe to stats changes

        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();

        // Initialize display with current values
        OnHealthChanged(_shipProxy.CurrentHealth, _shipProxy.MaxHealth);
        OnSpeedChanged(_shipProxy.ClientSimulatedSpeed);
        OnStatsChanged(); // Update stats display initially
    }

    private void OnHealthChanged(float current, float max)
    {
        // Example: Change color based on health
        // Logger.Log($"[DefaultShipPresentation {gameObject.name}] Health: {current}/{max}");
        // You could have a MeshRenderer and change its material color:
        // var r = GetComponent<Renderer>();
        // if (r != null) r.material.color = Color.Lerp(Color.red, Color.green, current/max);
    }
    private void OnSpeedChanged(float speed) 
    {
        // Example: Adjust thruster particle effects
        // Logger.Log($"[DefaultShipPresentation {gameObject.name}] Speed: {speed}");
    }

    private void OnStatsChanged()
    {
        // Example: Update UI display if showing MaxSpeed or TurnRate
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
        var mainCollider = GetComponent<Collider>(); if (mainCollider != null) mainCollider.enabled = false;
        // Consider disabling other child renderers or components as well
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
}