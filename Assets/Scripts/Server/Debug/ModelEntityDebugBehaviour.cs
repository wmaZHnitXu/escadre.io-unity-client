// File: Scripts/Server/Debug/ModelEntityDebugBehaviour.cs
using UnityEngine;
using Core.Model;          
using Core.Logging;
using Logger = Core.Logging.Logger;
using System;
using Core.Ocean; // Required for IFloatingBehavior checks

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
    
    [Header("Gizmo Toggles")]
    [Tooltip("Show floating points if the entity has a floating behavior.")]
    public bool showFloatingPointsGizmo = true;
    public Color floatingPointGizmoColor = Color.yellow;
    public float floatingPointGizmoRadius = 0.15f;


    protected Entity _targetEntity; 

    public virtual void Initialize(Entity entity)
    {
        _targetEntity = entity ?? throw new ArgumentNullException(nameof(entity));
        entityId = _targetEntity.Id;
        entityType = _targetEntity.EntityType.ToString();
        gameObject.name = $"{entityType}_{entityId}_ServerDebug"; 

        UpdateDebugInfo(); 

        Logger.Log($"[ModelEntityDebugBehaviour:{entityId}] Initialized for {gameObject.name}.");
    }

    protected virtual void LateUpdate() 
    {
        if (_targetEntity == null) 
        {
            if(gameObject.activeSelf) 
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
            Color baseColor = Color.gray; 
            if (_targetEntity.EntityType == Entity.EntityTypeEnum.DefaultShip) baseColor = Color.cyan;
            else if (_targetEntity.EntityType == Entity.EntityTypeEnum.Debug) baseColor = Color.magenta;
            else if (_targetEntity.EntityType == Entity.EntityTypeEnum.Escadre) baseColor = Color.green; 
            else if (_targetEntity.EntityType == Entity.EntityTypeEnum.DefaultCannon) baseColor = Color.red;


            r.material.color = isDead ? new Color(baseColor.r * 0.3f, baseColor.g * 0.3f, baseColor.b * 0.3f, 0.5f) : baseColor;
        }
    }
    
    [ContextMenu("Teleport Entity (to this GO's Transform)")]
    protected virtual void TeleportEntityToThisTransform() 
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
        UpdateDebugInfo(); 
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
        UpdateDebugInfo(); 
    }
    
    protected virtual void OnDrawGizmos() 
    {
        if (_targetEntity != null && !_targetEntity.IsDead)
        {
            Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.3f); 
            Gizmos.DrawSphere(_targetEntity.Position.ToUnityVector(), 0.25f); 
            
            if(showFloatingPointsGizmo) OnDrawGizmos_FloatingBehavior();
        }
    }

    protected virtual void OnDrawGizmos_FloatingBehavior()
    {
        if (_targetEntity == null || _targetEntity.FloatingBehavior == null) return;

        Gizmos.color = floatingPointGizmoColor;
        UnityEngine.Vector3 entityWorldPos = _targetEntity.Position.ToUnityVector();
        UnityEngine.Quaternion entityWorldRot = _targetEntity.Rotation.ToUnityQuaternion();

        if (_targetEntity.FloatingBehavior is MultiPointFloatingBehavior multiPointBehavior)
        {
            if (multiPointBehavior.LocalFloatingPointOffsets != null)
            {
                foreach (var localOffset in multiPointBehavior.LocalFloatingPointOffsets)
                {
                    UnityEngine.Vector3 worldOffset = entityWorldRot * localOffset.ToUnityVector();
                    Gizmos.DrawSphere(entityWorldPos + worldOffset, floatingPointGizmoRadius);
                }
            }
        }
        else if (_targetEntity.FloatingBehavior is DefaultFloatingBehavior) // Single point at origin
        {
            Gizmos.DrawSphere(entityWorldPos, floatingPointGizmoRadius);
        }
    }

    protected virtual void OnDestroy()
    {
        _targetEntity = null; 
    }
}