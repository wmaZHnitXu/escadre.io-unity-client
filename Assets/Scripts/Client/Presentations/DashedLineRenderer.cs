// File: Client/Presentation/DashedLineRenderer.cs
using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(LineRenderer))]
public class DashedLineRenderer : MonoBehaviour
{
    private LineRenderer _lineRenderer;

    [Header("Dash Settings")]
    public float dashLength = 1.0f;
    public float gapLength = 0.5f;
    public Material dashMaterial; // Assign a material that supports dashing (e.g., with a texture)

    [Header("Texture Tiling Mode (If Using Texture for Dashing)")]
    public bool useTextureTiling = true; // If true, uses material tiling for dashes
    public float textureTilingFactor = 1.0f; // Multiplies the calculated tiling

    private UnityEngine.Vector3 _startPoint;
    private UnityEngine.Vector3 _endPoint;
    private bool _isDirty = true; // Flag to check if points or properties changed
    private bool _isVisible = false;


    void Awake()
    {
        _lineRenderer = GetComponent<LineRenderer>();
        _lineRenderer.useWorldSpace = true;

        if (dashMaterial != null)
        {
            _lineRenderer.material = dashMaterial;
        }
        else if (_lineRenderer.sharedMaterial == null) // Fallback if no material assigned at all
        {
             // A simple unlit material could work if not using texture-based dashing
            _lineRenderer.material = new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply"));
            Core.Logging.Logger.LogWarning($"[DashedLineRenderer '{name}'] No dashMaterial assigned and LineRenderer had no material. Using a default particle material.");
        }
        _lineRenderer.positionCount = 0; // Start with no points
        _lineRenderer.enabled = false;   // Start hidden
    }

    public void SetPoints(UnityEngine.Vector3 start, UnityEngine.Vector3 end)
    {
        if (_startPoint != start || _endPoint != end)
        {
            _startPoint = start;
            _endPoint = end;
            _isDirty = true;
        }
    }

    public void SetColor(Color color)
    {
        if (_lineRenderer.startColor != color || _lineRenderer.endColor != color)
        {
            _lineRenderer.startColor = color;
            _lineRenderer.endColor = color;
            // No _isDirty = true needed as LineRenderer updates color immediately
        }
    }

    public void SetWidth(float width)
    {
        if (_lineRenderer.startWidth != width || _lineRenderer.endWidth != width)
        {
            _lineRenderer.startWidth = width;
            _lineRenderer.endWidth = width;
            _isDirty = true; // Width changes might affect dashing if not texture based
        }
    }

    public void Show()
    {
        if (!_isVisible)
        {
            _isVisible = true;
            _lineRenderer.enabled = true;
            _isDirty = true; // Force re-render when shown
        }
        // If already visible, _isDirty flag from SetPoints etc. will handle updates.
    }

    public void Hide()
    {
        if (_isVisible)
        {
            _isVisible = false;
            _lineRenderer.enabled = false;
        }
        // Optionally clear points, though disabled renderer won't show them
        // _lineRenderer.positionCount = 0;
    }

    void LateUpdate() // Using LateUpdate to ensure positions are final for the frame
    {
        if (_isVisible && _isDirty)
        {
            RenderDashedLine();
            _isDirty = false;
        }
    }

    private void RenderDashedLine()
    {
        if (!_lineRenderer.enabled) return; // Should not happen if _isVisible is true

        float lineLength = UnityEngine.Vector3.Distance(_startPoint, _endPoint);
        if (lineLength < 0.01f) // Line is too short to render
        {
            _lineRenderer.positionCount = 0;
            return;
        }
        
        if (dashLength <= 0) // Solid line if dashLength is zero or negative
        {
            _lineRenderer.positionCount = 2;
            _lineRenderer.SetPosition(0, _startPoint);
            _lineRenderer.SetPosition(1, _endPoint);
            if (_lineRenderer.textureMode == LineTextureMode.Tile) // Reset tiling if it was set
            {
                 MaterialPropertyBlock props = new MaterialPropertyBlock();
                _lineRenderer.GetPropertyBlock(props);
                props.SetVector("_MainTex_ST", new Vector4(1f, 1f, 0f, 0f)); // Default tiling
                _lineRenderer.SetPropertyBlock(props);
            }
            return;
        }


        if (useTextureTiling && _lineRenderer.material != null && _lineRenderer.material.HasProperty("_MainTex_ST"))
        {
            _lineRenderer.positionCount = 2;
            _lineRenderer.SetPosition(0, _startPoint);
            _lineRenderer.SetPosition(1, _endPoint);
            _lineRenderer.textureMode = LineTextureMode.Tile;

            float totalPatternLength = dashLength + gapLength;
            if (totalPatternLength > 0.001f)
            {
                MaterialPropertyBlock props = new MaterialPropertyBlock();
                _lineRenderer.GetPropertyBlock(props);
                // Tiling calculation: number of patterns that fit in the line length
                float tiling = lineLength / totalPatternLength * textureTilingFactor;
                props.SetVector("_MainTex_ST", new Vector4(tiling, 1f, 0f, 0f));
                _lineRenderer.SetPropertyBlock(props);
            }
        }
        else // Manual vertex generation for dashes
        {
            _lineRenderer.textureMode = LineTextureMode.Stretch; // Or whatever default if not tiling
            UnityEngine.Vector3 direction = (_endPoint - _startPoint).normalized;
            List<UnityEngine.Vector3> points = new List<UnityEngine.Vector3>();
            float currentDist = 0f;

            while (currentDist < lineLength)
            {
                points.Add(_startPoint + direction * currentDist); // Start of dash segment
                float dashEndDist = currentDist + dashLength;

                if (dashEndDist >= lineLength) // Dash segment reaches or exceeds endPoint
                {
                    points.Add(_endPoint);
                    break;
                }
                points.Add(_startPoint + direction * dashEndDist); // End of dash segment

                currentDist = dashEndDist + gapLength; // Move to start of next dash segment
                if (gapLength <= 0 && currentDist < lineLength) // Handle continuous line if no gap
                {
                    // If gap is zero, the next point is the same as the last, leading to degenerate segments.
                    // Instead, we effectively want a solid line.
                    // This case is somewhat covered by dashLength <= 0, but if gapLength is explicitly 0...
                    // For simplicity, this manual mode with gap=0 results in a solid line via many small segments.
                }
            }
            _lineRenderer.positionCount = points.Count;
            _lineRenderer.SetPositions(points.ToArray());
        }
    }
}