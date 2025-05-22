// File: Scripts/Client/Presentation/ClientProxyPresentation.cs
using UnityEngine;
using Core.Network;
using Core.Logging; // Keep for Logger if used, though not in current version directly
// using Logger = Core.Logging.Logger; // Avoid if Core.Logging.Logger not directly used

public abstract class ClientProxyPresentation : MonoBehaviour
{
    private IClientProxy _targetProxy;
    public IClientProxy TargetProxy { get => _targetProxy; protected set => _targetProxy = value; }
    public GameObject PrefabReference { get; private set; } // Used by pooling factory to know which pool to return to
    
    public delegate void OnPresentationDisposedDelegate(ClientProxyPresentation presentation); // Changed delegate name for clarity
    public event OnPresentationDisposedDelegate PresentationDisposedEvent;
    
    private bool _isInitialized = false;

    [Header("Visual Interpolation")]
    public float PositionInterpolationSpeed = 15f;
    public float RotationInterpolationSpeed = 15f;


    public virtual void InitializePresentation(IClientProxy proxy, GameObject prefabRef)
    {
        if (_isInitialized) 
        { 
            if (TargetProxy != null) UnsubscribeFromProxyEvents(); 
            // Core.Logging.Logger.LogWarning($"[ClientProxyPresentation {gameObject.name}] Re-initializing. Old Proxy ID: {TargetProxy?.EntityId}, New Proxy ID: {proxy?.EntityId}");
        }
        TargetProxy = proxy ?? throw new System.ArgumentNullException(nameof(proxy));
        PrefabReference = prefabRef ?? throw new System.ArgumentNullException(nameof(prefabRef)); // Store prefab reference
        
        gameObject.name = $"{TargetProxy.EntityType}_{TargetProxy.EntityId}_Presentation";
        gameObject.SetActive(true); // Ensure it's active if reused from pool

        transform.position = TargetProxy.Position.ToUnityVector();
        transform.rotation = TargetProxy.Rotation.ToUnityQuaternion();

        SubscribeToProxyEvents();
        OnInitialized(); // Call derived class initialization
        _isInitialized = true;
    }

    protected virtual void SubscribeToProxyEvents()
    {
        if (TargetProxy == null) return;
        TargetProxy.PositionChanged += OnProxyPositionChanged; 
        TargetProxy.RotationChanged += OnProxyRotationChanged; 
        TargetProxy.OnLoudDestructionSignaled += HandleLoudDestruction;
        TargetProxy.OnDestroyed += HandleProxyDestroyed; // Renamed from Vanished
    }

    protected virtual void UnsubscribeFromProxyEvents()
    {
        if (TargetProxy == null) return;
        TargetProxy.PositionChanged -= OnProxyPositionChanged;
        TargetProxy.RotationChanged -= OnProxyRotationChanged;
        TargetProxy.OnLoudDestructionSignaled -= HandleLoudDestruction;
        TargetProxy.OnDestroyed -= HandleProxyDestroyed;
    }

    // Called by derived classes if they need to do specific setup
    protected virtual void OnInitialized() { } 

    // Specific handlers for position/rotation changes from the proxy
    protected virtual void OnProxyPositionChanged(Core.Primitives.Vector3 newPosition) { /* Visual update is handled by Lerp in Update() */ }
    protected virtual void OnProxyRotationChanged(Core.Primitives.Quaternion newRotation) { /* Visual update is handled by Lerp in Update() */ }


    protected virtual void Update() 
    {
        if (!_isInitialized || TargetProxy == null) return;

        // Interpolate towards the TargetProxy's current (simulated or server-corrected) state
        transform.position = Vector3.Lerp(transform.position, TargetProxy.Position.ToUnityVector(), Time.deltaTime * PositionInterpolationSpeed);
        transform.rotation = Quaternion.Slerp(transform.rotation, TargetProxy.Rotation.ToUnityQuaternion(), Time.deltaTime * RotationInterpolationSpeed);
    }

    // Called when the proxy signals a "loud" destruction (e.g., explosion, effects)
    protected virtual void HandleLoudDestruction() {
        // Core.Logging.Logger.Log($"[ClientProxyPresentation {gameObject.name}] Loud destruction signaled by proxy {TargetProxy.EntityId}.");
        // Derived classes implement specific destruction visuals/audio.
        // The GameObject itself is not destroyed here; that happens on HandleProxyDestroyed.
    }

    // Called when the proxy is definitively destroyed/vanished from the client's perspective
    protected virtual void HandleProxyDestroyed() 
    { 
        // Core.Logging.Logger.Log($"[ClientProxyPresentation {gameObject.name}] Proxy {TargetProxy.EntityId} destroyed. Disposing presentation.");
        DisposePresentation(); 
    }
    
    protected virtual void DisposePresentation() 
    {
        if (!_isInitialized) return; // Already disposed or never initialized

        // Core.Logging.Logger.Log($"[ClientProxyPresentation {gameObject.name}] Disposing. Proxy ID: {TargetProxy?.EntityId}");
        
        gameObject.SetActive(false); // Deactivate for pooling
        UnsubscribeFromProxyEvents(); // Crucial to prevent lingering subscriptions
        
        PresentationDisposedEvent?.Invoke(this); // Notify factory/manager for pooling or cleanup
        PresentationDisposedEvent = null; // Clear event
        
        // TargetProxy = null; // Cleared by the system that owns the proxy lifecycle, or just ensures this presentation stops interacting
        _isInitialized = false; 
    }

    // Unity's OnDestroy, a fallback if DisposePresentation isn't called explicitly (e.g. scene change)
    protected virtual void OnDestroy() 
    { 
        if(_isInitialized) 
        { 
            // Core.Logging.Logger.LogWarning($"[ClientProxyPresentation {gameObject.name}] OnDestroy called while still initialized. Forcing DisposePresentation. Proxy ID: {TargetProxy?.EntityId}");
            DisposePresentation(); 
        } 
    }
}