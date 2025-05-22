// File: Scripts/Server/Debug/ModelEntityDebugBehaviour.cs
using UnityEngine;
using Core.Model;          
using Core.Logging;
using Logger = Core.Logging.Logger;
using System;        

[DefaultExecutionOrder(100)] 
public class ModelEntityDebugBehaviour : MonoBehaviour
{
    [Header("Core Entity Info")]
    [SerializeField, ReadOnly] protected int entityId; 
    [SerializeField, ReadOnly] protected string entityType;
    [SerializeField, ReadOnly] protected Core.Primitives.Vector3 corePosition; 
    [SerializeField, ReadOnly] protected Core.Primitives.Quaternion coreRotation; 
    [SerializeField, ReadOnly] protected bool isDead;

    [Header("Debug Controls")]
    [Tooltip("If true, this GameObject's transform follows the Core Entity's state.")]
    public bool followEntity = true;
    // [Tooltip("Transform to use as the target for actions like Teleport or SetMovementTarget (for ships).")]
    // Removed actionTargetGizmo, subclasses can define their own specific gizmos if needed.
    // Or, a generic one could be re-added if a common "action target" concept emerges.

    protected Entity _targetEntity; 

    public virtual void Initialize(Entity entity)
    {
        _targetEntity = entity ?? throw new ArgumentNullException(nameof(entity));
        entityId = _targetEntity.Id;
        entityType = _targetEntity.EntityType.ToString();
        gameObject.name = $"{entityType}_{entityId}_ServerDebug"; // Added _ServerDebug suffix for clarity

        UpdateDebugInfo(); 

        Logger.Log($"[ModelEntityDebugBehaviour:{entityId}] Initialized for {gameObject.name}.");
    }

    protected virtual void LateUpdate() 
    {
        if (_targetEntity == null) 
        {
            // If the target entity becomes null (e.g., after destruction and before this GO is destroyed),
            // disable further updates to prevent errors.
            if(gameObject.activeSelf) // Only log if it was active
                Logger.LogWarning($"[ModelEntityDebugBehaviour:{entityId}] Target entity is null. Disabling updates for {gameObject.name}. This GO should be destroyed soon.");
            enabled = false; 
            return;
        }

        UpdateDebugInfo();

        if (followEntity && !_targetEntity.IsDead)
        {
            transform.position = _targetEntity.Position.ToUnityVector();
            transform.rotation = _targetEntity.Rotation.ToUnityQuaternion();
        }
        else if (followEntity && _targetEntity.IsDead)
        {
            // Optional: if entity is dead but followEntity is true,
            // ensure the debug object stays at the last known position.
            // This is usually handled by UpdateDebugInfo setting the color and then this object being destroyed.
        }
    }

    protected virtual void UpdateDebugInfo()
    {
        if (_targetEntity == null) return; // Should be caught by LateUpdate's check now
        corePosition = _targetEntity.Position;
        coreRotation = _targetEntity.Rotation;
        isDead = _targetEntity.IsDead;
        
        var r = GetComponent<Renderer>();
        if (r != null)
        {
            // Define default color or handle based on more specific types if needed.
            Color baseColor = Color.gray; // Default for unknown entity types
            if (_targetEntity.EntityType == Entity.EntityTypeEnum.DefaultShip) baseColor = Color.cyan;
            else if (_targetEntity.EntityType == Entity.EntityTypeEnum.Debug) baseColor = Color.magenta;
            else if (_targetEntity.EntityType == Entity.EntityTypeEnum.Escadre) baseColor = Color.green; // Example color for Escadre

            r.material.color = isDead ? new Color(baseColor.r * 0.3f, baseColor.g * 0.3f, baseColor.b * 0.3f, 0.5f) : baseColor;
        }
    }
    
    [ContextMenu("Teleport Entity (to this GO's Transform)")]
    protected virtual void TeleportEntityToThisTransform() // Renamed for clarity
    {
        if (_targetEntity == null || _targetEntity.IsDead)
        {
            Logger.LogWarning($"[ModelEntityDebugBehaviour:{entityId}] Cannot teleport, entity is null or dead.");
            return;
        }

        Core.Primitives.Vector3 newCorePos = transform.position.ToCoreVector();
        Core.Primitives.Quaternion newCoreRot = transform.rotation.ToCoreQuaternion();
        Logger.Log($"[ModelEntityDebugBehaviour:{entityId}] Teleporting Core Entity to Pos: {newCorePos}, Rot: {newCoreRot}");
        _targetEntity.TeleportTo(newCorePos, newCoreRot);
    }

    [ContextMenu("Kill Entity (Normal - with destruction effects)")]
    protected virtual void KillEntityNormal()
    {
        if (_targetEntity == null || _targetEntity.IsDead)
        {
            Logger.LogWarning($"[ModelEntityDebugBehaviour:{entityId}] Entity already null or dead.");
            return;
        }
        Logger.Log($"[ModelEntityDebugBehaviour:{entityId}] Calling Kill(silent=false)...");
        _targetEntity.Kill(false); 
        UpdateDebugInfo(); // Reflect immediate state change in Inspector
    }

    [ContextMenu("Kill Entity (Silent - no destruction effects)")]
    protected virtual void KillEntitySilent()
    {
        if (_targetEntity == null || _targetEntity.IsDead)
        {
             Logger.LogWarning($"[ModelEntityDebugBehaviour:{entityId}] Entity already null or dead.");
            return;
        }
        Logger.Log($"[ModelEntityDebugBehaviour:{entityId}] Calling Kill(silent=true)...");
        _targetEntity.Kill(true); 
        UpdateDebugInfo(); // Reflect immediate state change in Inspector
    }
    
    // Removed generic OnDrawGizmos for actionTargetGizmo.
    // Subclasses can implement their own specific gizmos if needed.
    // For example, DefaultShipDebugBehaviour draws its attack range.
    protected virtual void OnDrawGizmos() 
    {
        if (_targetEntity != null && !_targetEntity.IsDead)
        {
            // Optionally draw a small sphere or label at the entity's core position
            Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.3f); // Light gray, semi-transparent
            Gizmos.DrawSphere(_targetEntity.Position.ToUnityVector(), 0.25f); 
        }
    }

    protected virtual void OnDestroy()
    {
        // _targetEntity's events (like OnDeathEvent) should be unsubscribed by the system that created this
        // (e.g., DebugPresentationManager handles unsubscribing from Entity.OnDeathEvent).
        _targetEntity = null; 
    }
}