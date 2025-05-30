// File: Scripts/Server/Debug/DestructibleEntityDebugBehaviour.cs
using UnityEngine;
using Core.Model;          
using Core.Logging;
using Core.Primitives; // Required for Collider and BoxCollider
using Logger = Core.Logging.Logger;
using System;
using System.Linq; // Required for FirstOrDefault
using Core.Ocean; // Required for IFloatingBehavior checks


[DefaultExecutionOrder(100)] 
public class DestructibleEntityDebugBehaviour : ModelEntityDebugBehaviour
{
    [Header("Destructible Entity Info")]
    [SerializeField, ReadOnly] protected float currentHealth;
    [SerializeField, ReadOnly] protected float maxHealth;
    [SerializeField, ReadOnly] protected int colliderCount_Display;

    [Header("Gizmo Toggles")]
    [Tooltip("Show colliders attached to this entity.")]
    public bool showCollidersGizmo = true;
    public Color colliderGizmoColor = Color.blue;


    protected DestructibleEntity TargetDestructibleEntity => _targetEntity as DestructibleEntity;

    public override void Initialize(Entity entity)
    {
        if (entity is DestructibleEntity de)
        {
            base.Initialize(de);
        }
        else
        {
            Logger.LogError($"[DestructibleEntityDebugBehaviour] Incorrect entity type: {entity?.GetType().Name}. Expected DestructibleEntity or derived.");
            _targetEntity = null; enabled = false;
        }
    }

    protected override void UpdateDebugInfo()
    {
        base.UpdateDebugInfo();
        if (TargetDestructibleEntity != null)
        {
            currentHealth = TargetDestructibleEntity.CurrentHealth;
            maxHealth = TargetDestructibleEntity.MaxHealth;
            colliderCount_Display = TargetDestructibleEntity.Colliders.Count;
        }
        else
        {
            currentHealth = 0;
            maxHealth = 0;
            colliderCount_Display = 0;
        }
    }
    
    [ContextMenu("Apply 10 Damage (Kinetic)")]
    protected virtual void ApplyDebugDamage()
    {
        if (TargetDestructibleEntity == null || TargetDestructibleEntity.IsDead)
        {
            Logger.LogWarning($"[DestructibleEntityDebugBehaviour:{entityId}] Cannot apply damage, entity is null or dead.");
            return;
        }
        DamageInfo dmg = new DamageInfo(
            10f, 
            DamageType.Kinetic, 
            TargetDestructibleEntity.Position, 
            Core.Primitives.Vector3.Back, // Arbitrary direction
            null, 
            null
        );
        Logger.Log($"[DestructibleEntityDebugBehaviour:{entityId}] Applying 10 debug damage.");
        TargetDestructibleEntity.ApplyDamage(dmg);
        UpdateDebugInfo(); // Refresh display
    }


    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos(); // Draws entity label, position, and floating points
        
        if (TargetDestructibleEntity != null && !TargetDestructibleEntity.IsDead && showCollidersGizmo)
        {
            foreach (var coreCollider in TargetDestructibleEntity.Colliders)
            {
                DrawColliderGizmo(coreCollider);
            }
        }
    }

    protected virtual void DrawColliderGizmo(Core.Primitives.Collider coreCollider)
    {
        if (coreCollider == null || coreCollider.OwnerEntity == null) return;

        Gizmos.color = colliderGizmoColor;
        
        // Entity's world transform
        UnityEngine.Vector3 entityWorldPos = coreCollider.OwnerEntity.Position.ToUnityVector();
        UnityEngine.Quaternion entityWorldRot = coreCollider.OwnerEntity.Rotation.ToUnityQuaternion();

        if (coreCollider is Core.Primitives.BoxCollider box)
        {
            // The box's center in world space is Owner.Pos + Owner.Rot * box.LocalOffset
            UnityEngine.Vector3 boxWorldCenter = entityWorldPos + (entityWorldRot * box.LocalOffset.ToUnityVector());
            
            // Gizmos.matrix needs to be set to the box's world transform
            // TRS: Position (boxWorldCenter), Rotation (entityWorldRot), Scale (1,1,1 as size is handled by DrawWireCube)
            UnityEngine.Matrix4x4 originalMatrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(boxWorldCenter, entityWorldRot, UnityEngine.Vector3.one);
            
            // Draw the cube centered at origin in this new Gizmos.matrix space, with its actual size
            Gizmos.DrawWireCube(UnityEngine.Vector3.zero, box.Size.ToUnityVector());
            
            Gizmos.matrix = originalMatrix; // Restore original matrix
        }
        // else if (coreCollider is SphereCollider sphere) { ... draw sphere ... }
        // else if (coreCollider is CapsuleCollider capsule) { ... draw capsule ... }
    }
}