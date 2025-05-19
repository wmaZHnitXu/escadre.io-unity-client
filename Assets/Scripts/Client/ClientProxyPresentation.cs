// File: Scripts/Client/Presentation/ClientProxyPresentation.cs
using UnityEngine;
using Core.Network;
using Core.Logging;

public abstract class ClientProxyPresentation : MonoBehaviour
{
    private IClientProxy _targetProxy;
    public IClientProxy TargetProxy { get => _targetProxy; protected set => _targetProxy = value; }
    public GameObject PrefabReference { get; private set; }
    public delegate void OnPresentationDisposed(ClientProxyPresentation presentation);
    public event OnPresentationDisposed PresentationDisposedEvent;
    private bool _isInitialized = false;

    [Header("Visual Interpolation")]
    public float PositionInterpolationSpeed = 15f;
    public float RotationInterpolationSpeed = 15f;

    // No longer need _visualTargetPosition/Rotation here. Will read directly from Proxy.
    // The proxy's PositionChanged/RotationChanged events will signal that fresh data is available.

    public virtual void InitializePresentation(IClientProxy proxy, GameObject prefabRef)
    {
        if (_isInitialized) { if (TargetProxy != null) UnsubscribeFromProxyEvents(); }
        TargetProxy = proxy ?? throw new System.ArgumentNullException(nameof(proxy));
        PrefabReference = prefabRef ?? throw new System.ArgumentNullException(nameof(prefabRef));
        gameObject.name = $"{TargetProxy.EntityType}_{TargetProxy.EntityId}_Presentation";
        gameObject.SetActive(true);

        // Snap transform to initial proxy state immediately
        transform.position = TargetProxy.Position.ToUnityVector();
        transform.rotation = TargetProxy.Rotation.ToUnityQuaternion();

        SubscribeToProxyEvents();
        OnInitialized();
        _isInitialized = true;
    }

    protected virtual void SubscribeToProxyEvents()
    {
        if (TargetProxy == null) return;
        // PositionChanged and RotationChanged are subscribed to primarily to know that
        // the TargetProxy's data has been updated by its internal simulation or a server message.
        // The presentation's Update() loop will then perform the visual interpolation.
        TargetProxy.PositionChanged += OnProxyDataChanged; // Generic handler
        TargetProxy.RotationChanged += OnProxyDataChanged; // Generic handler
        TargetProxy.OnLoudDestructionSignaled += HandleLoudDestruction;
        TargetProxy.OnDestroyed += HandleProxyVanished;
    }

    protected virtual void UnsubscribeFromProxyEvents()
    {
        if (TargetProxy == null) return;
        TargetProxy.PositionChanged -= OnProxyDataChanged;
        TargetProxy.RotationChanged -= OnProxyDataChanged;
        TargetProxy.OnLoudDestructionSignaled -= HandleLoudDestruction;
        TargetProxy.OnDestroyed -= HandleProxyVanished;
    }

    protected virtual void OnInitialized() { }

    // Generic handler for proxy data changes. We don't need to store the new value here
    // because Update() will read directly from TargetProxy.Position/Rotation.
    // This event can be useful if the presentation needs to do something specific
    // *immediately* when data changes, beyond just starting interpolation.
    private void OnProxyDataChanged(Core.Primitives.Vector3 newPos) { /* Optional: React immediately */ }
    private void OnProxyDataChanged(Core.Primitives.Quaternion newRot) { /* Optional: React immediately */ }


    protected virtual void Update() // Unity's Update method
    {
        if (!_isInitialized || TargetProxy == null) return;

        // Smoothly interpolate towards the current state of the TargetProxy
        transform.position = Vector3.Lerp(transform.position, TargetProxy.Position.ToUnityVector(), Time.deltaTime * PositionInterpolationSpeed);
        transform.rotation = Quaternion.Slerp(transform.rotation, TargetProxy.Rotation.ToUnityQuaternion(), Time.deltaTime * RotationInterpolationSpeed);
    }

    protected virtual void HandleLoudDestruction() {
        // Logger.Log($"[ClientProxyPresentation {gameObject.name}] Loud destruction signaled.");
    }
    protected virtual void HandleProxyVanished() { DisposePresentation(); }
    protected virtual void DisposePresentation() {
        if (!_isInitialized) return;
        gameObject.SetActive(false); UnsubscribeFromProxyEvents();
        PresentationDisposedEvent?.Invoke(this); PresentationDisposedEvent = null;
        TargetProxy = null; _isInitialized = false;
    }
    protected virtual void OnDestroy() { if(_isInitialized) { DisposePresentation(); } }
}