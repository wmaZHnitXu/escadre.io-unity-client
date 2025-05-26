// File: Scripts/Shared/Debug/OceanDebugVisualizer.cs
using UnityEngine;
using Core.Ocean;
using Core.Time;
using Core.Primitives; 
using System;
using Vector3 = UnityEngine.Vector3; // For MathF

[DefaultExecutionOrder(200)] 
public class OceanDebugVisualizer : MonoBehaviour
{
    [Header("Visualization Settings")]
    [SerializeField, Range(8, 128)] private int gridSize = 32; 
    [SerializeField] private float cellWorldSize = 2.0f; 
    [SerializeField] private Vector3 visualizerCenterOffset = Vector3.zero; 
    [SerializeField] private float pointSphereRadius = 0.1f;

    [Header("Interpolated Ocean State")]
    [SerializeField] private bool drawInterpolatedOcean = true;
    [SerializeField] private Color interpolatedDisplacementColor = new Color(0.2f, 0.5f, 1f, 0.7f);
    [SerializeField] private bool drawInterpolatedNormals = true;
    [SerializeField] private Color interpolatedNormalColor = Color.yellow;
    [SerializeField] private float normalLineLength = 0.5f;
    
    [Header("Nearest Discrete Time Slice")]
    [SerializeField] private bool showNearestDiscreteTimeSlice = false;
    [SerializeField] private Color discreteTimeSliceDisplacementColor = new Color(1f, 0.5f, 0.2f, 0.7f);
    [SerializeField] private bool drawDiscreteTimeSliceNormals = false;
    [SerializeField] private Color discreteTimeSliceNormalColor = new Color(1f, 0.8f, 0.2f);
    [SerializeField, ReadOnly] private int currentNearestTimeSliceIndex_Display = 0;


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

    void OnDrawGizmosSelected() 
    {
        if (!_isInitialized || _oceanDataProvider == null || _clock == null || _oceanDataProvider.Settings == null)
        {
            if (Application.isPlaying && !_isInitialized)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireCube(transform.position + visualizerCenterOffset, new Vector3(gridSize * cellWorldSize * 0.5f, 1f, gridSize * cellWorldSize * 0.5f));
                Gizmos.DrawLine(transform.position + visualizerCenterOffset - Vector3.up, transform.position + visualizerCenterOffset + Vector3.up);
            }
            return;
        }

        float currentTime = _clock.CurrentTime;
        Vector3 gridOrigin = transform.position + visualizerCenterOffset - 
                             new Vector3(gridSize * cellWorldSize * 0.5f, 0, gridSize * cellWorldSize * 0.5f);
        OceanSettings settings = _oceanDataProvider.Settings;

        // Calculate nearest discrete time slice for display
        float timeNormalizedForSliceIndex = (currentTime % settings.TextureTimeLoopDuration) / settings.TextureTimeLoopDuration;
        float texTimeFloat = timeNormalizedForSliceIndex * (settings.TextureResolutionTime - 1);
        currentNearestTimeSliceIndex_Display = (int)MathF.Round(texTimeFloat);
        currentNearestTimeSliceIndex_Display = Math.Clamp(currentNearestTimeSliceIndex_Display, 0, settings.TextureResolutionTime - 1);
        float timeForDiscreteSliceViz = (currentNearestTimeSliceIndex_Display / (float)Math.Max(1, settings.TextureResolutionTime - 1)) * settings.TextureTimeLoopDuration;


        for (int x = 0; x <= gridSize; x++) 
        {
            for (int z = 0; z <= gridSize; z++)
            {
                float worldX = gridOrigin.x + x * cellWorldSize;
                float worldZ = gridOrigin.z + z * cellWorldSize;
                float baseY = gridOrigin.y; 

                // Draw fully interpolated ocean state
                if (drawInterpolatedOcean)
                {
                    Core.Primitives.Vector3 interpDisplacement = _oceanDataProvider.GetDisplacement(worldX, worldZ, currentTime);
                    UnityEngine.Vector3 unityInterpDisp = new UnityEngine.Vector3(interpDisplacement.X, interpDisplacement.Y, interpDisplacement.Z);
                    UnityEngine.Vector3 interpDisplacedPoint = new UnityEngine.Vector3(
                        worldX + unityInterpDisp.x, 
                        baseY + unityInterpDisp.y,       
                        worldZ + unityInterpDisp.z  
                    );

                    Gizmos.color = interpolatedDisplacementColor;
                    Gizmos.DrawSphere(interpDisplacedPoint, pointSphereRadius);

                    if (drawInterpolatedNormals)
                    {
                        Core.Primitives.Vector3 coreInterpNormal = _oceanDataProvider.GetNormal(worldX, worldZ, currentTime);
                        UnityEngine.Vector3 unityInterpNormal = new UnityEngine.Vector3(coreInterpNormal.X, coreInterpNormal.Y, coreInterpNormal.Z);
                        
                        Gizmos.color = interpolatedNormalColor;
                        Gizmos.DrawLine(interpDisplacedPoint, interpDisplacedPoint + unityInterpNormal * normalLineLength);
                    }
                }

                // Draw ocean state snapped to the nearest discrete time slice
                if (showNearestDiscreteTimeSlice)
                {
                    Core.Primitives.Vector3 discreteTimeSliceDisplacement = _oceanDataProvider.GetDisplacement(worldX, worldZ, timeForDiscreteSliceViz);
                    UnityEngine.Vector3 unityDiscreteDisp = new UnityEngine.Vector3(discreteTimeSliceDisplacement.X, discreteTimeSliceDisplacement.Y, discreteTimeSliceDisplacement.Z);
                    UnityEngine.Vector3 discreteTimeSliceDisplacedPoint = new UnityEngine.Vector3(
                        worldX + unityDiscreteDisp.x,
                        baseY + unityDiscreteDisp.y,
                        worldZ + unityDiscreteDisp.z
                    );

                    Gizmos.color = discreteTimeSliceDisplacementColor;
                    Gizmos.DrawSphere(discreteTimeSliceDisplacedPoint, pointSphereRadius * 0.8f); // Slightly smaller sphere

                    if (drawDiscreteTimeSliceNormals)
                    {
                        Core.Primitives.Vector3 coreDiscreteNormal = _oceanDataProvider.GetNormal(worldX, worldZ, timeForDiscreteSliceViz);
                        UnityEngine.Vector3 unityDiscreteNormal = new UnityEngine.Vector3(coreDiscreteNormal.X, coreDiscreteNormal.Y, coreDiscreteNormal.Z);

                        Gizmos.color = discreteTimeSliceNormalColor;
                        Gizmos.DrawLine(discreteTimeSliceDisplacedPoint, discreteTimeSliceDisplacedPoint + unityDiscreteNormal * normalLineLength * 0.8f);
                    }
                }
            }
        }
    }
}