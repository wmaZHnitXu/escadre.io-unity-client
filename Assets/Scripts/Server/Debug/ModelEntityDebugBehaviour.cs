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
    [Tooltip("Transform to use as the target for actions like Teleport or SetMovementTarget (for ships).")]
    public Transform actionTargetGizmo; // Renamed from teleportTargetGizmo


    protected Entity _targetEntity; 

    public virtual void Initialize(Entity entity)
    {
        _targetEntity = entity ?? throw new ArgumentNullException(nameof(entity));
        entityId = _targetEntity.Id;
        entityType = _targetEntity.EntityType.ToString();
        gameObject.name = $"{entityType}_{entityId}"; 

        UpdateDebugInfo(); 

        if (actionTargetGizmo != null)
        {
            actionTargetGizmo.gameObject.name = $"{gameObject.name}_ActionTargetGizmo";
            actionTargetGizmo.position = transform.position + Vector3.up * 2f + transform.forward * 2f; 
            actionTargetGizmo.gameObject.SetActive(false); 
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
    
    [ContextMenu("Teleport Entity to ActionTargetGizmo Position")]
    protected virtual void TeleportEntityToGizmo()
    {
        if (_targetEntity == null || _targetEntity.IsDead)
        {
            Logger.LogWarning($"[ModelEntityDebugBehaviour:{entityId}] Cannot teleport, entity is null or dead.");
            return;
        }
        if (actionTargetGizmo == null)
        {
            Logger.LogWarning($"[ModelEntityDebugBehaviour:{entityId}] Action Target Gizmo not assigned.");
            return;
        }
        if (!actionTargetGizmo.gameObject.activeSelf)
        {
            actionTargetGizmo.position = transform.position + transform.forward * 5f + Vector3.up * 1f;
            actionTargetGizmo.gameObject.SetActive(true);
            Logger.Log($"[ModelEntityDebugBehaviour:{entityId}] Activated action target gizmo. Move it and click Teleport again.");
            return;
        }

        Core.Primitives.Vector3 newCorePos = actionTargetGizmo.position.ToCoreVector();
        Core.Primitives.Quaternion newCoreRot = actionTargetGizmo.rotation.ToCoreQuaternion();
        Logger.Log($"[ModelEntityDebugBehaviour:{entityId}] Teleporting Core Entity to Pos: {newCorePos}, Rot: {newCoreRot}");
        _targetEntity.TeleportTo(newCorePos, newCoreRot);
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
        // Draw actionTargetGizmo line if it's active and entity is not dead
        if (_targetEntity != null && !_targetEntity.IsDead && actionTargetGizmo != null && actionTargetGizmo.gameObject.activeSelf)
        {
            Gizmos.color = Color.yellow; // Changed color for general action target
            Gizmos.DrawLine(transform.position, actionTargetGizmo.position);
            Gizmos.DrawWireSphere(actionTargetGizmo.position, 0.6f);
            Gizmos.DrawLine(actionTargetGizmo.position, actionTargetGizmo.position + actionTargetGizmo.forward * 1.5f); // Show orientation
        }
    }

    protected virtual void OnDestroy()
    {
        _targetEntity = null; 
    }
}