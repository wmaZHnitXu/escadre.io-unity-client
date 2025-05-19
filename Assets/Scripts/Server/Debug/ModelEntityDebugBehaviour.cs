// File: Scripts/Server/Debug/ModelEntityDebugBehaviour.cs
using UnityEngine;
using Core.Model;          
using Core.Primitives;     
using Core.Logging;
using Logger = Core.Logging.Logger;
using System;        
// No specific using needed for ReadOnlyAttribute if it's in global scope or same assembly

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
    [Tooltip("Transform to use as the target for teleportation.")]
    public Transform teleportTargetGizmo;


    protected Entity _targetEntity; 

    public virtual void Initialize(Entity entity)
    {
        _targetEntity = entity ?? throw new ArgumentNullException(nameof(entity));
        entityId = _targetEntity.Id;
        entityType = _targetEntity.EntityType.ToString();
        gameObject.name = $"{entityType}_{entityId}"; 

        UpdateDebugInfo(); 

        if (teleportTargetGizmo != null)
        {
            teleportTargetGizmo.gameObject.name = $"{gameObject.name}_TeleportGizmo";
            teleportTargetGizmo.position = transform.position + UnityEngine.Vector3.up * 2f; 
            teleportTargetGizmo.gameObject.SetActive(false); 
        }


        Logger.Log($"[ModelEntityDebugBehaviour:{entityId}] Initialized for {gameObject.name}.");
    }

    protected virtual void LateUpdate() 
    {
        if (_targetEntity == null) return; 

        UpdateDebugInfo();

        if (followEntity && !_targetEntity.IsDead)
        {
            transform.position = _targetEntity.Position.ToUnityVector();
            transform.rotation = _targetEntity.Rotation.ToUnityQuaternion();
        }
    }

    protected virtual void UpdateDebugInfo()
    {
        if (_targetEntity == null) return;
        corePosition = _targetEntity.Position;
        coreRotation = _targetEntity.Rotation;
        isDead = _targetEntity.IsDead;
        
        var r = GetComponent<Renderer>();
        if (r != null)
        {
            r.material.color = isDead ? Color.gray : (_targetEntity.EntityType == Entity.EntityTypeEnum.DefaultShip ? Color.cyan : Color.magenta) ;
            if (isDead) r.material.color = new Color(r.material.color.r * 0.5f, r.material.color.g * 0.5f, r.material.color.b * 0.5f);
        }
    }


    [ContextMenu("Set Entity Position to This Transform (No Teleport Event)")]
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

        Core.Primitives.Vector3 newCorePos = transform.position.ToCoreVector();
        Logger.Log($"[ModelEntityDebugBehaviour:{entityId}] Setting Core Entity position to {newCorePos} (direct, no event).");
        _targetEntity.Position = newCorePos; 
    }
    
    [ContextMenu("Teleport Entity to TeleportGizmo Position")]
    protected virtual void TeleportEntityToGizmo()
    {
        if (_targetEntity == null || _targetEntity.IsDead)
        {
            Logger.LogWarning($"[ModelEntityDebugBehaviour:{entityId}] Cannot teleport, entity is null or dead.");
            return;
        }
        if (teleportTargetGizmo == null)
        {
            Logger.LogWarning($"[ModelEntityDebugBehaviour:{entityId}] Teleport Target Gizmo not assigned.");
            return;
        }
        if (!teleportTargetGizmo.gameObject.activeSelf)
        {
            teleportTargetGizmo.position = transform.position + transform.forward * 5f + UnityEngine.Vector3.up * 1f;
            teleportTargetGizmo.gameObject.SetActive(true);
            Logger.Log($"[ModelEntityDebugBehaviour:{entityId}] Activated teleport gizmo. Move it and click again.");
            return;
        }

        Core.Primitives.Vector3 newCorePos = teleportTargetGizmo.position.ToCoreVector();
        Core.Primitives.Quaternion newCoreRot = teleportTargetGizmo.rotation.ToCoreQuaternion();
        Logger.Log($"[ModelEntityDebugBehaviour:{entityId}] Teleporting Core Entity to Pos: {newCorePos}, Rot: {newCoreRot}");
        _targetEntity.TeleportTo(newCorePos, newCoreRot);
    }


    [ContextMenu("Set Entity Rotation to This Transform (No Teleport Event)")]
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
        Core.Primitives.Quaternion newCoreRot = transform.rotation.ToCoreQuaternion();
        Logger.Log($"[ModelEntityDebugBehaviour:{entityId}] Setting Core Entity rotation to {newCoreRot} (direct, no event).");
        _targetEntity.TeleportTo(_targetEntity.Position, newCoreRot); 
        Logger.LogWarning($"[ModelEntityDebugBehaviour:{entityId}] Used Teleport to set rotation as direct setter is protected.");
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
        _targetEntity.Kill(false); 
        UpdateDebugInfo(); 
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
        _targetEntity.Kill(true); 
        UpdateDebugInfo(); 
    }
    
    protected virtual void OnDrawGizmos() 
    {
        if (_targetEntity != null && !_targetEntity.IsDead && teleportTargetGizmo != null && teleportTargetGizmo.gameObject.activeSelf)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, teleportTargetGizmo.position);
            Gizmos.DrawWireSphere(teleportTargetGizmo.position, 0.6f);
            Gizmos.DrawLine(teleportTargetGizmo.position, teleportTargetGizmo.position + teleportTargetGizmo.forward * 1.5f);
        }
    }


    protected virtual void OnDestroy()
    {
        _targetEntity = null; 
    }
}