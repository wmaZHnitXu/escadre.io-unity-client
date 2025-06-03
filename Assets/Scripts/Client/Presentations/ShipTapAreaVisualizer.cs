// File: Client/Presentation/ShipTapAreaVisualizer.cs
using UnityEngine; // Fully qualify: UnityEngine.Vector3, Mathf, Color, LineRenderer, Transform

[RequireComponent(typeof(LineRenderer))]
public class ShipTapAreaVisualizer : MonoBehaviour
{
    private LineRenderer _lineRenderer;

    [Header("Circle Settings")]
    [Tooltip("The radius of the circle to draw on the XZ plane, relative to this GameObject's parent.")]
    public float radius = 2.0f;
    [Tooltip("Number of segments to approximate the circle.")]
    [Range(8, 128)] public int segments = 32;
    [Tooltip("Color of the circle line.")]
    public Color lineColor = new Color(0.5f, 0.8f, 1f, 0.5f); // Default color
    [Tooltip("Width of the circle line.")]
    public float lineWidth = 0.1f;
    [Tooltip("Vertical offset from this GameObject's local origin (parented to ship).")]
    public float yOffset = 0.05f;

    private bool _isVisible = false;
    private bool _isDirty = true; // To redraw if properties change

    void Awake()
    {
        _lineRenderer = GetComponent<LineRenderer>();
        _lineRenderer.useWorldSpace = false; // Points are local to this GameObject's transform.
        _lineRenderer.loop = true;
        _lineRenderer.positionCount = 0; // Initialize with no points
        _lineRenderer.enabled = false;   // Start hidden

        // Apply initial visual properties. These can be overridden by RefreshVisuals.
        transform.localPosition = new UnityEngine.Vector3(0, yOffset, 0);
        // Initial refresh to set default values to line renderer.
        RefreshVisuals(radius, lineColor, lineWidth, segments);
    }

    void Update() // Or LateUpdate
    {
        if (_isVisible && _isDirty)
        {
            UpdateCirclePoints();
            _isDirty = false;
        }
    }

    private void UpdateCirclePoints()
    {
        if (!_lineRenderer.enabled && _isVisible) _lineRenderer.enabled = true; // Ensure renderer is active if visible

        if (segments < 3)
        {
            _lineRenderer.positionCount = 0;
            return;
        }

        _lineRenderer.positionCount = segments + 1;

        float angleStep = 360.0f / segments;
        for (int i = 0; i <= segments; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            UnityEngine.Vector3 point = new UnityEngine.Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
            _lineRenderer.SetPosition(i, point);
        }
    }

    public void SetVisibility(bool visible)
    {
        if (_isVisible == visible) return;
        _isVisible = visible;
        _lineRenderer.enabled = _isVisible;
        if (_isVisible) _isDirty = true; // Ensure redraw when becoming visible
    }

    public void RefreshVisuals(float newRadius, Color newLineColor, float newLineWidth, int newSegments = -1)
    {
        bool changed = false;
        if (!Mathf.Approximately(radius, newRadius))
        {
            radius = newRadius;
            changed = true;
        }
        if (lineColor != newLineColor)
        {
            lineColor = newLineColor;
            _lineRenderer.startColor = lineColor;
            _lineRenderer.endColor = lineColor;
            // Color changes apply immediately, no need to set changed = true for this.
        }
        if (!Mathf.Approximately(lineWidth, newLineWidth))
        {
            lineWidth = newLineWidth;
            _lineRenderer.startWidth = lineWidth;
            _lineRenderer.endWidth = lineWidth;
            changed = true;
        }
        if (newSegments > 0 && segments != newSegments)
        {
            segments = newSegments;
            changed = true;
        }

        if (changed)
        {
            _isDirty = true;
        }
        // If visible and dirty, Update/LateUpdate will call UpdateCirclePoints.
        // If not visible but dirty, UpdateCirclePoints will be called when SetVisibility(true) is next called.
    }

    // Optional: Individual setters if needed, though RefreshVisuals is more comprehensive.
    public void SetColor(Color color)
    {
        if (lineColor != color)
        {
            lineColor = color;
            _lineRenderer.startColor = lineColor;
            _lineRenderer.endColor = lineColor;
        }
    }
    public void SetRadius(float newRadius)
    {
        if (!Mathf.Approximately(radius, newRadius))
        {
            radius = newRadius;
            _isDirty = true;
        }
    }
}