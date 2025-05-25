// File: Scripts/Shared/Debug/OceanDebugVisualizer.cs
using UnityEngine;
using Core.Ocean;
using Core.Time;
using Core.Primitives; // For our Vector3, even though we convert to Unity's for Gizmos

[DefaultExecutionOrder(200)] // Ensure it runs after potential IOceanDataProvider setup
public class OceanDebugVisualizer : MonoBehaviour
{
    [Header("Visualization Settings")]
    [SerializeField, Range(8, 128)] private int gridSize = 32; // Number of points in X and Z
    [SerializeField] private float cellWorldSize = 2.0f; // World size of each grid cell
    [SerializeField] private UnityEngine.Vector3 visualizerCenterOffset = UnityEngine.Vector3.zero; // Offset from this GameObject's position
    [SerializeField] private bool drawDisplacementPoints = true;
    [SerializeField] private bool drawSurfaceNormals = true;
    [SerializeField] private float normalLineLength = 0.5f;
    [SerializeField] private Color displacementGizmoColor = new Color(0.2f, 0.5f, 1f, 0.7f);
    [SerializeField] private Color normalGizmoColor = Color.yellow;
    [SerializeField] private float pointSphereRadius = 0.1f;

    private IOceanDataProvider _oceanDataProvider;
    private IClock _clock;
    private bool _isInitialized = false;

    public void Initialize(IOceanDataProvider oceanDataProvider, IClock clock)
    {
        if (oceanDataProvider == null)
        {
            Debug.LogError("[OceanDebugVisualizer] IOceanDataProvider cannot be null for initialization.", this);
            enabled = false;
            return;
        }
        if (clock == null)
        {
            Debug.LogError("[OceanDebugVisualizer] IClock cannot be null for initialization.", this);
            enabled = false;
            return;
        }

        _oceanDataProvider = oceanDataProvider;
        _clock = clock;
        _isInitialized = true;
        Debug.Log($"[OceanDebugVisualizer] Initialized. Grid: {gridSize}x{gridSize}, CellSize: {cellWorldSize}", this);
    }

    void OnDrawGizmosSelected() // Only draw when selected to save performance
    {
        if (!_isInitialized || _oceanDataProvider == null || _clock == null)
        {
            if (Application.isPlaying && !_isInitialized)
            {
                // Draw a fallback gizmo if not initialized in play mode yet
                Gizmos.color = Color.red;
                Gizmos.DrawWireCube(transform.position + visualizerCenterOffset, new UnityEngine.Vector3(gridSize * cellWorldSize * 0.5f, 1f, gridSize * cellWorldSize * 0.5f));
                Gizmos.DrawLine(transform.position + visualizerCenterOffset - UnityEngine.Vector3.up, transform.position + visualizerCenterOffset + UnityEngine.Vector3.up);
            }
            return;
        }

        float currentTime = _clock.CurrentTime;
        UnityEngine.Vector3 gridOrigin = transform.position + visualizerCenterOffset - 
                             new UnityEngine.Vector3(gridSize * cellWorldSize * 0.5f, 0, gridSize * cellWorldSize * 0.5f);

        for (int x = 0; x <= gridSize; x++) // Use <= to draw points for all grid lines
        {
            for (int z = 0; z <= gridSize; z++)
            {
                float worldX = gridOrigin.x + x * cellWorldSize;
                float worldZ = gridOrigin.z + z * cellWorldSize;
                float baseY = gridOrigin.y; // Base Y plane for visualization

                Core.Primitives.Vector3 displacement = _oceanDataProvider.GetDisplacement(worldX, worldZ, currentTime);
                
                // Convert Core.Primitives.Vector3 to UnityEngine.Vector3 for Gizmos
                UnityEngine.Vector3 unityDisplacement = new UnityEngine.Vector3(displacement.X, displacement.Y, displacement.Z);
                UnityEngine.Vector3 basePoint = new UnityEngine.Vector3(worldX, baseY, worldZ);
                UnityEngine.Vector3 displacedPoint = new UnityEngine.Vector3(
                    basePoint.x + unityDisplacement.x, // Apply horizontal displacement to the original XZ
                    baseY + unityDisplacement.y,       // Vertical displacement from the base Y
                    basePoint.z + unityDisplacement.z  // Apply horizontal displacement to the original XZ
                );


                if (drawDisplacementPoints)
                {
                    Gizmos.color = displacementGizmoColor;
                    Gizmos.DrawSphere(displacedPoint, pointSphereRadius);
                    // Optionally, draw a line from base to displaced point
                    // Gizmos.DrawLine(basePoint, displacedPoint); 
                }

                if (drawSurfaceNormals)
                {
                    Core.Primitives.Vector3 coreNormal = _oceanDataProvider.GetNormal(worldX, worldZ, currentTime);
                    UnityEngine.Vector3 unityNormal = new UnityEngine.Vector3(coreNormal.X, coreNormal.Y, coreNormal.Z);
                    
                    Gizmos.color = normalGizmoColor;
                    Gizmos.DrawLine(displacedPoint, displacedPoint + unityNormal * normalLineLength);
                }
            }
        }
    }
}