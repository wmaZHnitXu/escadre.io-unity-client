// File: Scripts/Server/Debug/ModelEntityDebugBehaviour.cs
using UnityEngine;
using Core.Model;          // For Entity
using Core.Primitives;     // For Vector3, Quaternion
using Core.Logging;
using Logger = Core.Logging.Logger;
using System;        // For Logger
// Assuming CoreToUnityAdapters is accessible globally or add appropriate using
// using YourGameName.Adapters; // If CoreToUnityAdapters is in a namespace

/// <summary>
/// Base MonoBehaviour for GameObjects representing Core.Model.Entity instances
/// for server-side debugging within the Unity Editor.
/// </summary>
public class ModelEntityDebugBehaviour : MonoBehaviour
{
    [Header("Core Entity Info")]
    [SerializeField, ReadOnly] protected int entityId; // ReadOnly attribute requires a custom editor or property drawer for full effect
    [SerializeField, ReadOnly] protected string entityType;
    [SerializeField, ReadOnly] protected Core.Primitives.Vector3 corePosition; // Using Core.Primitives.Vector3
    [SerializeField, ReadOnly] protected Core.Primitives.Quaternion coreRotation; // Using Core.Primitives.Quaternion
    [SerializeField, ReadOnly] protected bool isDead;

    [Header("Debug Controls")]
    [Tooltip("If true, this GameObject's transform follows the Core Entity's state.")]
    public bool followEntity = true;

    protected Entity _targetEntity; // The actual Core.Model.Entity reference

    /// <summary>
    /// Initializes the debug behaviour with the target Core Entity.
    /// Called by DebugPresentationManager.
    /// </summary>
    /// <param name="entity">The Core.Model.Entity this GameObject represents.</param>
    public virtual void Initialize(Entity entity)
    {
        _targetEntity = entity ?? throw new ArgumentNullException(nameof(entity));
        entityId = _targetEntity.Id;
        entityType = _targetEntity.EntityType.ToString();
        gameObject.name = $"{entityType}_{entityId}"; // Set GameObject name

        UpdateDebugInfo(); // Set initial values

        // Optional: Subscribe to state changes if Entity had events for them
        // _targetEntity.PositionChanged += HandlePositionChanged; // Example

        Logger.Log($"[ModelEntityDebugBehaviour:{entityId}] Initialized for {gameObject.name}.");
    }

    protected virtual void Update()
    {
        if (_targetEntity == null) return; // Not initialized or entity destroyed

        // Update displayed info (can be performance intensive if many entities)
        UpdateDebugInfo();

        // Follow entity state if enabled
        if (followEntity && !_targetEntity.IsDead)
        {
            // Use adapters to convert Core types to Unity types
            transform.position = _targetEntity.Position.ToUnityVector();
            transform.rotation = _targetEntity.Rotation.ToUnityQuaternion();
        }
    }

    /// <summary>
    /// Updates the serialized fields shown in the inspector.
    /// </summary>
    protected virtual void UpdateDebugInfo()
    {
        if (_targetEntity == null) return;
        // These assignments won't actually update the Inspector view live
        // unless using a custom editor or calling EditorUtility.SetDirty in editor scripts.
        // However, they reflect the state if you select the object.
        corePosition = _targetEntity.Position;
        coreRotation = _targetEntity.Rotation;
        isDead = _targetEntity.IsDead;
        // Maybe change color if dead?
        // var renderer = GetComponent<Renderer>();
        // if (renderer != null) renderer.material.color = isDead ? Color.grey : Color.white;
    }

    // --- Context Menu Actions ---

    [ContextMenu("Set Entity Position to This Transform")]
    protected virtual void SetEntityPositionFromTransform()
    {
        if (_targetEntity == null || _targetEntity.IsDead)
        {
            Logger.LogWarning($"[ModelEntityDebugBehaviour:{entityId}] Cannot set position, entity is null or dead.");
            return;
        }
        if (followEntity)
        {
            Logger.LogWarning($"[ModelEntityDebugBehaviour:{entityId}] Cannot set position while 'Follow Entity' is enabled.");
            return;
        }

        // Use adapter to convert Unity transform to Core type
        Core.Primitives.Vector3 newCorePos = transform.position.ToCoreVector();
        Logger.Log($"[ModelEntityDebugBehaviour:{entityId}] Setting Core Entity position to {newCorePos}");
        _targetEntity.Position = newCorePos; // Directly set the position

        // Note: If position setting involves logic (like collision), you'd call a method:
        // _targetEntity.SetPosition(newCorePos);
    }

    [ContextMenu("Set Entity Rotation to This Transform")]
    protected virtual void SetEntityRotationFromTransform()
    {
         if (_targetEntity == null || _targetEntity.IsDead)
        {
            Logger.LogWarning($"[ModelEntityDebugBehaviour:{entityId}] Cannot set rotation, entity is null or dead.");
            return;
        }
        if (followEntity)
        {
             Logger.LogWarning($"[ModelEntityDebugBehaviour:{entityId}] Cannot set rotation while 'Follow Entity' is enabled.");
            return;
        }
        // Use adapter to convert Unity transform to Core type
        Core.Primitives.Quaternion newCoreRot = transform.rotation.ToCoreQuaternion();
        Logger.Log($"[ModelEntityDebugBehaviour:{entityId}] Setting Core Entity rotation to {newCoreRot}");
        // _targetEntity.Rotation = newCoreRot; // Need protected setter on Entity or method
        Logger.LogWarning($"[ModelEntityDebugBehaviour:{entityId}] Entity.Rotation setter is protected. Cannot set directly.");
        // Example: _targetEntity.SetRotation(newCoreRot); // If such method exists
    }


    [ContextMenu("Kill Entity (Normal)")]
    protected virtual void KillEntityNormal()
    {
        if (_targetEntity == null || _targetEntity.IsDead)
        {
            Logger.LogWarning($"[ModelEntityDebugBehaviour:{entityId}] Entity already null or dead.");
            return;
        }
        Logger.Log($"[ModelEntityDebugBehaviour:{entityId}] Calling Kill(silent=false)...");
        _targetEntity.Kill(false); // Normal kill (with destruction logic/events)
        UpdateDebugInfo(); // Update isDead status display
    }

    [ContextMenu("Kill Entity (Silent)")]
    protected virtual void KillEntitySilent()
    {
        if (_targetEntity == null || _targetEntity.IsDead)
        {
             Logger.LogWarning($"[ModelEntityDebugBehaviour:{entityId}] Entity already null or dead.");
            return;
        }
        Logger.Log($"[ModelEntityDebugBehaviour:{entityId}] Calling Kill(silent=true)...");
        _targetEntity.Kill(true); // Silent kill (only essential cleanup)
        UpdateDebugInfo(); // Update isDead status display
    }

    protected virtual void OnDestroy()
    {
        // Called when the GameObject is destroyed
        // Logger.Log($"[ModelEntityDebugBehaviour:{entityId}] GameObject destroyed.");
        // Optional: Unsubscribe from entity events if subscribed in Initialize
        // if (_targetEntity != null) {
        //    _targetEntity.PositionChanged -= HandlePositionChanged; // Example
        // }
        _targetEntity = null; // Clear reference
    }
}

// Helper attribute for ReadOnly fields in Inspector (requires custom editor or asset for full functionality)
public class ReadOnlyAttribute : PropertyAttribute { }