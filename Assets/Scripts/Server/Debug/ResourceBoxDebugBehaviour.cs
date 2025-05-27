// File: Scripts/Server/Debug/ResourceBoxDebugBehaviour.cs
using UnityEngine;
using Core.Model;
using Core.Primitives; // For ToUnityVector
using Logger = Core.Logging.Logger;

public class ResourceBoxDebugBehaviour : ModelEntityDebugBehaviour
{
    [Header("ResourceBox Specific")]
    [SerializeField, ReadOnly] protected int resourceAmount_Display;
    [SerializeField, ReadOnly] protected float finalCollectionDistance_Display;
    [SerializeField, ReadOnly] protected float suckSpeed_Display;
    [SerializeField, ReadOnly] protected int collectingShipId_Display = -1; // Display ID of the ship collecting it

    protected ResourceBox TargetResourceBox => _targetEntity as ResourceBox;

    public override void Initialize(Entity entity)
    {
        if (entity is ResourceBox resourceBox)
        {
            base.Initialize(resourceBox);
        }
        else
        {
            Logger.LogError($"[ResourceBoxDebugBehaviour] Incorrect entity type: {entity?.GetType().Name}. Expected ResourceBox.");
            _targetEntity = null;
            enabled = false;
        }
    }

    protected override void UpdateDebugInfo()
    {
        base.UpdateDebugInfo(); 
        if (TargetResourceBox != null && !TargetResourceBox.IsDead)
        {
            resourceAmount_Display = TargetResourceBox.ResourceAmount;
            finalCollectionDistance_Display = TargetResourceBox.FinalCollectionDistance;
            suckSpeed_Display = TargetResourceBox.SuckSpeed;
            collectingShipId_Display = TargetResourceBox.CollectingShip?.Id ?? -1;
        }
        else
        {
            resourceAmount_Display = 0;
            finalCollectionDistance_Display = 0;
            suckSpeed_Display = 0;
            collectingShipId_Display = -1;
        }
    }

    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos(); 

        if (TargetResourceBox != null && !TargetResourceBox.IsDead)
        {
            var worldPos = TargetResourceBox.Position.ToUnityVector();

            // Draw Final Collection Distance (small inner sphere)
            Color collectionDistColor = Color.cyan;
            collectionDistColor.a = 0.2f; 
            Gizmos.color = collectionDistColor;
            Gizmos.DrawSphere(worldPos, TargetResourceBox.FinalCollectionDistance);
            
            Gizmos.color = new Color(Color.cyan.r, Color.cyan.g, Color.cyan.b, 0.8f); 
            Gizmos.DrawWireSphere(worldPos, TargetResourceBox.FinalCollectionDistance);

            // If it's being collected, draw a line to the collecting ship
            if (TargetResourceBox.CollectingShip != null && !TargetResourceBox.CollectingShip.IsDead)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(worldPos, TargetResourceBox.CollectingShip.Position.ToUnityVector());
            }
        }
    }
}