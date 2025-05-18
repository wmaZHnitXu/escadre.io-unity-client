// File: Scripts/Client/Presentation/ClientProxyPresentation.cs
using UnityEngine;
using Core.Network; // For IClientProxy
using Core.Logging;
using Logger = Core.Logging.Logger; // For Logger (optional, for debug)

/// <summary>
/// Abstract base class for MonoBehaviour components that visually represent
/// an IClientProxy on the client.
/// </summary>
public abstract class ClientProxyPresentation : MonoBehaviour
{
    private IClientProxy _targetProxy;
    public IClientProxy TargetProxy
    {
        get => _targetProxy;
        protected set => _targetProxy = value;
    }

    public GameObject PrefabReference { get; private set; } // Set by the factory, used for pooling

    public delegate void OnPresentationDisposed(ClientProxyPresentation presentation);
    public event OnPresentationDisposed PresentationDisposedEvent;

    private bool _isInitialized = false;

    /// <summary>
    /// Initializes the presentation with its target proxy and subscribes to relevant events.
    /// Called by a factory.
    /// </summary>
    public virtual void InitializePresentation(IClientProxy proxy, GameObject prefabRef)
    {
        if (_isInitialized)
        {
            // This might happen if re-initializing from a pool
            // Ensure previous subscriptions are cleared if any (though Dispose should handle this)
             if (TargetProxy != null) UnsubscribeFromProxyEvents();
        }

        TargetProxy = proxy ?? throw new System.ArgumentNullException(nameof(proxy));
        PrefabReference = prefabRef ?? throw new System.ArgumentNullException(nameof(prefabRef));

        gameObject.name = $"{TargetProxy.EntityType}_{TargetProxy.EntityId}_Presentation";
        gameObject.SetActive(true);

        transform.position = TargetProxy.Position.ToUnityVector();
        transform.rotation = TargetProxy.Rotation.ToUnityQuaternion();

        SubscribeToProxyEvents();
        OnInitialized(); // For derived classes to do specific setup
        _isInitialized = true;
    }

    protected virtual void SubscribeToProxyEvents()
    {
        if (TargetProxy == null) return;
        TargetProxy.PositionChanged += HandlePositionChanged;
        TargetProxy.RotationChanged += HandleRotationChanged;
        TargetProxy.OnLoudDestructionSignaled += HandleLoudDestruction;
        TargetProxy.OnDestroyed += HandleProxyVanished; // OnDestroyed means VanishEntity was received
    }

    protected virtual void UnsubscribeFromProxyEvents()
    {
        if (TargetProxy == null) return;
        TargetProxy.PositionChanged -= HandlePositionChanged;
        TargetProxy.RotationChanged -= HandleRotationChanged;
        TargetProxy.OnLoudDestructionSignaled -= HandleLoudDestruction;
        TargetProxy.OnDestroyed -= HandleProxyVanished;
    }

    /// <summary>
    /// Called after InitializePresentation for derived class specific setup.
    /// </summary>
    protected virtual void OnInitialized() { }

    protected virtual void HandlePositionChanged(Core.Primitives.Vector3 newPosition)
    {
        // TODO: Implement smooth interpolation (e.g., Lerp or a dedicated movement component)
        transform.position = newPosition.ToUnityVector();
    }

    protected virtual void HandleRotationChanged(Core.Primitives.Quaternion newRotation)
    {
        // TODO: Implement smooth interpolation (e.g., Slerp or a dedicated rotation component)
        transform.rotation = newRotation.ToUnityQuaternion();
    }

    /// <summary>
    /// Called when the proxy signals a "loud" destruction (effects should be played).
    /// The presentation itself is not yet disposed.
    /// </summary>
    protected virtual void HandleLoudDestruction()
    {
        Logger.Log($"[ClientProxyPresentation {gameObject.name}] Loud destruction signaled. Playing effects...");
        // Example: Instantiate explosion prefab, play sound.
        // gameObject.GetComponent<Renderer>().enabled = false; // Hide main model
        // You might disable colliders or other components here too.
    }

    /// <summary>
    /// Called when the target proxy's OnDestroyed event fires (meaning VanishEntity was received).
    /// This triggers the disposal of the presentation.
    /// </summary>
    protected virtual void HandleProxyVanished()
    {
        DisposePresentation();
    }

    /// <summary>
    /// Cleans up the presentation, unsubscribes from events, deactivates the GameObject,
    /// and notifies listeners (e.g., an object pool manager).
    /// </summary>
    protected virtual void DisposePresentation()
    {
        if (!_isInitialized) return; // Already disposed or never initialized

        Logger.Log($"[ClientProxyPresentation {gameObject.name}] Disposing.");
        gameObject.SetActive(false);
        UnsubscribeFromProxyEvents();

        // Call before nulling TargetProxy so factory can use PrefabReference from it if needed via event.
        PresentationDisposedEvent?.Invoke(this);
        PresentationDisposedEvent = null; // Clear subscribers

        TargetProxy = null;
        _isInitialized = false;
        // PrefabReference remains for the pool to identify it.
    }

    protected virtual void OnDestroy() // Unity's OnDestroy
    {
        // Ensure cleanup if GameObject is destroyed externally (e.g., scene change)
        // This might lead to double invocation if DisposePresentation was already called.
        // The _isInitialized flag helps manage this.
        if(_isInitialized)
        {
            Logger.LogWarning($"[ClientProxyPresentation {gameObject.name}] Unity OnDestroy called while still initialized. Forcing DisposePresentation.");
            DisposePresentation();
        }
    }
}