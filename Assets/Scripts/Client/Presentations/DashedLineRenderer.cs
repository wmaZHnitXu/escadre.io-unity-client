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
    public Material dashMaterial;

    [Header("Texture Tiling Mode (If Used)")]
    public bool useTextureTiling = true;
    public float textureTilingFactor = 1.0f;

    // New field to store the original prefab this instance came from
    public DashedLineRenderer OriginalPrefab { get; set; }


    private UnityEngine.Vector3 _startPoint;
    private UnityEngine.Vector3 _endPoint;
    private bool _isDirty = true;

    void Awake()
    {
        _lineRenderer = GetComponent<LineRenderer>();
        _lineRenderer.useWorldSpace = true;

        if (dashMaterial != null)
        {
            _lineRenderer.material = dashMaterial;
        }
        else if (_lineRenderer.sharedMaterial == null)
        {
            _lineRenderer.material = new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply")); // Unity built-in fallback
            Core.Logging.Logger.LogWarning($"[DashedLineRenderer '{name}'] No material assigned and LineRenderer had no material. Using a default particle material.");
        }
        _lineRenderer.positionCount = 0;
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
        _lineRenderer.startColor = color;
        _lineRenderer.endColor = color;
    }

    public void SetWidth(float width)
    {
        _lineRenderer.startWidth = width;
        _lineRenderer.endWidth = width;
    }

    public void Show()
    {
        // Ensure the line is enabled. If it was already enabled but points changed (dirty),
        // LateUpdate will handle rendering. If it was disabled, enabling and marking dirty ensures it renders.
        if (!_lineRenderer.enabled)
        {
            _lineRenderer.enabled = true;
            _isDirty = true; // Force redraw if it was hidden
        }
        // If it was already enabled, _isDirty flag (set by SetPoints) will trigger LateUpdate.
    }


    public void Hide()
    {
        if (_lineRenderer.enabled)
        {
            _lineRenderer.enabled = false;
        }
        // Setting positionCount to 0 visually clears it, but might be redundant if renderer is disabled.
        // It's good practice for pooling to reset state.
        _lineRenderer.positionCount = 0;
    }

    void LateUpdate()
    {
        if (_isDirty && _lineRenderer.enabled)
        {
            RenderDashedLine();
            _isDirty = false;
        }
    }

    private void RenderDashedLine()
    {
        if (dashLength <= 0 || gapLength < 0)
        {
            _lineRenderer.positionCount = 2;
            _lineRenderer.SetPosition(0, _startPoint);
            _lineRenderer.SetPosition(1, _endPoint);
            return;
        }

        if (useTextureTiling)
        {
            _lineRenderer.positionCount = 2;
            _lineRenderer.SetPosition(0, _startPoint);
            _lineRenderer.SetPosition(1, _endPoint);
            _lineRenderer.textureMode = LineTextureMode.Tile;
            float lineLength = UnityEngine.Vector3.Distance(_startPoint, _endPoint);
            float totalPatternLength = dashLength + gapLength;
            if (totalPatternLength > 0.001f)
            {
                MaterialPropertyBlock props = new MaterialPropertyBlock();
                _lineRenderer.GetPropertyBlock(props);
                props.SetVector("_MainTex_ST", new Vector4(lineLength / totalPatternLength * textureTilingFactor, 1f, 0f, 0f));
                _lineRenderer.SetPropertyBlock(props);
            }
        }
        else
        {
            float lineLength = UnityEngine.Vector3.Distance(_startPoint, _endPoint);
            if (lineLength < 0.01f)
            {
                _lineRenderer.positionCount = 0;
                return;
            }

            UnityEngine.Vector3 direction = (_endPoint - _startPoint).normalized;
            List<UnityEngine.Vector3> points = new List<UnityEngine.Vector3>();
            float currentDist = 0f;

            while (currentDist < lineLength)
            {
                points.Add(_startPoint + direction * currentDist);
                float dashEndDist = currentDist + dashLength;
                if (dashEndDist >= lineLength)
                {
                    points.Add(_endPoint);
                    break;
                }
                points.Add(_startPoint + direction * dashEndDist);
                currentDist = dashEndDist + gapLength;
            }
            _lineRenderer.positionCount = points.Count;
            _lineRenderer.SetPositions(points.ToArray());
        }
    }
}