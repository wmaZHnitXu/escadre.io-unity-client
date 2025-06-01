// File: Scripts/Client/Presentations/BulletPresentation.cs
using UnityEngine;
using Core.Network.Proxies;
using Logger = Core.Logging.Logger;

public class BulletPresentation : ClientProxyPresentation
{
    [Header("Bullet Visuals")]
    [SerializeField] private TrailRenderer trailRenderer;
    [SerializeField] private GameObject hitEffectPrefab; // Particle system for impact
    [SerializeField] private AudioClip hitSound;       // Sound for impact
    private AudioSource _audioSource;

    private ProjectileProxy.ClientProxy _projectileProxy;
    private bool _hitVisualsTriggered = false;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        _projectileProxy = TargetProxy as ProjectileProxy.ClientProxy;
        if (_projectileProxy == null)
        {
            Logger.LogError($"[BulletPresentation {gameObject.name}] TargetProxy is not a ProjectileProxy.ClientProxy! Type: {TargetProxy?.GetType().Name}");
            enabled = false;
            return;
        }

        _projectileProxy.OnHitVisualsClientEvent += HandleHitVisuals;

        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.playOnAwake = false;

        if (trailRenderer != null)
        {
            trailRenderer.Clear(); // Clear trail if reusing from pool
            trailRenderer.emitting = true;
        }
        _hitVisualsTriggered = false;

        // The base Update handles position/rotation Lerp.
        // BulletProxy.ClientProxy.Update handles the predictive movement.
    }

    private void HandleHitVisuals(int victimId, Core.Primitives.Vector3 hitPoint, Core.Primitives.Vector3 hitNormal)
    {
        if (_hitVisualsTriggered) return; // Prevent multiple triggers if event comes slightly delayed
        _hitVisualsTriggered = true;

        // Logger.Log($"[BulletPresentation {gameObject.name}] HitVisuals event received. Victim: {victimId}, Point: {hitPoint.ToUnityVector()}");

        // Instantiate hit effect
        if (hitEffectPrefab != null)
        {
            Instantiate(hitEffectPrefab, hitPoint.ToUnityVector(), Quaternion.LookRotation(hitNormal.ToUnityVector()));
        }

        // Play hit sound at the point of impact (could also be on the hit effect prefab)
        if (hitSound != null && _audioSource != null)
        {
            // For a brief sound, playing it at world location is fine.
            // If AudioSource on this bullet GO, it would need to be positioned at hit point.
            // Better to have AudioSource on hitEffectPrefab or use AudioSource.PlayClipAtPoint.
            AudioSource.PlayClipAtPoint(hitSound, hitPoint.ToUnityVector());
        }

        // Stop rendering the bullet itself as it has "hit"
        var mainRenderer = GetComponent<Renderer>();
        if (mainRenderer != null) mainRenderer.enabled = false;
        if (trailRenderer != null) trailRenderer.emitting = false;

        // The server will send a VanishEntity or DestroyEntity message shortly after,
        // which will call HandleProxyDestroyed() and then DisposePresentation().
        // We don't immediately dispose here because the proxy itself isn't "destroyed" yet.
    }

    protected override void HandleProxyDestroyed()
    {
        // Logger.Log($"[BulletPresentation {gameObject.name}] Proxy Destroyed. Triggering DisposePresentation.");
        // If hit visuals haven't been triggered (e.g., bullet fizzled due to lifetime),
        // ensure visuals are cleaned up.
        if (!_hitVisualsTriggered)
        {
            var mainRenderer = GetComponent<Renderer>();
            if (mainRenderer != null) mainRenderer.enabled = false; // Hide if not already hidden by hit
            if (trailRenderer != null) trailRenderer.emitting = false;
        }
        base.HandleProxyDestroyed(); // This calls DisposePresentation
    }
    
    protected override void DisposePresentation()
    {
        // Reset state for pooling
        _hitVisualsTriggered = false;
        var mainRenderer = GetComponent<Renderer>();
        if (mainRenderer != null) mainRenderer.enabled = true; // Re-enable for next use from pool
        if (trailRenderer != null)
        {
            trailRenderer.Clear();
            trailRenderer.emitting = false; // Ensure trail is off and cleared
        }
        base.DisposePresentation();
    }


    protected override void UnsubscribeFromProxyEvents()
    {
        base.UnsubscribeFromProxyEvents();
        if (_projectileProxy != null)
        {
            _projectileProxy.OnHitVisualsClientEvent -= HandleHitVisuals;
        }
    }

    // Ensure OnEnable also resets trail if reused from pool.
    // ClientProxyPresentation.InitializePresentation calls gameObject.SetActive(true)
    // so OnEnable will be called.
    void OnEnable()
    {
        if (_isInitialized) // Check if it's a re-enable after pooling
        {
            if (trailRenderer != null)
            {
                trailRenderer.Clear();
                trailRenderer.emitting = true;
            }
            var mainRenderer = GetComponent<Renderer>();
            if (mainRenderer != null) mainRenderer.enabled = true;
            _hitVisualsTriggered = false;
        }
    }
}