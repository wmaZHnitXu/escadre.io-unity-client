// File: Scripts/Server/Debug/DebugEntityDebugBehaviour.cs
using UnityEngine;
using Core.Model;
using Core.Primitives;
using Core.Logging;
using Logger = Core.Logging.Logger;

/// <summary>
/// Debug MonoBehaviour specifically for Core.Model.DebugEntity.
/// Adds controls for its specific properties and methods.
/// </summary>
public class DebugEntityDebugBehaviour : ModelEntityDebugBehaviour
{
    [Header("Debug Entity Specific")]
    [SerializeField, ReadOnly] protected float hydration;
    [SerializeField, ReadOnly] protected float guilt;

    // Need reference to the specific type
    protected DebugEntity TargetDebugEntity => _targetEntity as DebugEntity;

    public override void Initialize(Entity entity)
    {
        // Ensure we're initializing with the correct type
        if (entity is DebugEntity debugEntity)
        {
            base.Initialize(debugEntity); // Call base initialization
        }
        else
        {
             Logger.LogError($"[DebugEntityDebugBehaviour] Attempted to initialize with incorrect entity type: {entity?.GetType().Name ?? "null"}. Expected DebugEntity.");
             // Prevent further execution if type is wrong
             _targetEntity = null;
             enabled = false; // Disable component
        }
    }

    protected override void UpdateDebugInfo()
    {
        base.UpdateDebugInfo(); // Update common fields
        if (TargetDebugEntity != null)
        {
            hydration = TargetDebugEntity.Hydration;
            guilt = TargetDebugEntity.Guilt;
        }
    }

    // --- Context Menu for DebugEntity Actions ---

    [ContextMenu("Call SetRestPosition (to Current Transform)")]
    protected void CallSetRestPosition()
    {
        if (TargetDebugEntity == null || TargetDebugEntity.IsDead)
        {
            Logger.LogWarning($"[DebugEntityDebugBehaviour:{entityId}] Entity is null or dead.");
            return;
        }
        Core.Primitives.Vector3 targetPos = transform.position.ToCoreVector();
        Logger.Log($"[DebugEntityDebugBehaviour:{entityId}] Calling SetRestPosition({targetPos})");
        TargetDebugEntity.SetRestPosition(targetPos);
        // Position will update automatically if followEntity is true, or on next UpdateDebugInfo
    }

     [ContextMenu("Call SpitAt (Forward)")]
    protected void CallSpitAtForward()
    {
        if (TargetDebugEntity == null || TargetDebugEntity.IsDead)
        {
             Logger.LogWarning($"[DebugEntityDebugBehaviour:{entityId}] Entity is null or dead.");
            return;
        }
        // Calculate a point slightly in front of the debug transform
        Core.Primitives.Vector3 targetPos = (transform.position + transform.forward * 5f).ToCoreVector();
        Logger.Log($"[DebugEntityDebugBehaviour:{entityId}] Calling SpitAt({targetPos})");
        TargetDebugEntity.SpitAt(targetPos);
    }

    [ContextMenu("Call ShoutAt (Self)")]
    protected void CallShoutAtSelf()
    {
        if (TargetDebugEntity == null || TargetDebugEntity.IsDead)
        {
             Logger.LogWarning($"[DebugEntityDebugBehaviour:{entityId}] Entity is null or dead.");
            return;
        }
         Logger.Log($"[DebugEntityDebugBehaviour:{entityId}] Calling ShoutAt(self)");
         TargetDebugEntity.ShoutAt(TargetDebugEntity);
    }
}