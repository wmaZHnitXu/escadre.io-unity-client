// File: Client/Presentation/ShipTapAreaVisualizer.cs
using UnityEngine; // Fully qualify: UnityEngine.Vector3, Mathf, Color, LineRenderer, Transform

[RequireComponent(typeof(LineRenderer))]
public class ShipTapAreaVisualizer : MonoBehaviour
{
    private LineRenderer _lineRenderer;

    [Header("Circle Settings")]
    [Tooltip("The radius of the circle to draw on the XZ plane, relative to this GameObject's parent.")]
    public float radius = 2.0f; // Should match TapResolverService.SHIP_TAP_RADIUS
    [Tooltip("Number of segments to approximate the circle.")]
    [Range(8, 128)] public int segments = 32;
    [Tooltip("Color of the circle line.")]
    public Color lineColor = new Color(0.5f, 0.8f, 1f, 0.5f);
    [Tooltip("Width of the circle line.")]
    public float lineWidth = 0.1f;
    [Tooltip("Vertical offset from this GameObject's local origin (which should be parented to the ship).")]
    public float yOffset = 0.05f;

    private bool _isVisible = false; // Default to hidden, let the parent (ShipPresentation) enable it.

    void Awake()
    {
        _lineRenderer = GetComponent<LineRenderer>();
        _lineRenderer.useWorldSpace = false; // Points are local to this GameObject's transform.
        _lineRenderer.loop = true;
        // Initial visual properties are set here, can be overridden by RefreshVisuals.
        _lineRenderer.startWidth = lineWidth;
        _lineRenderer.endWidth = lineWidth;
        _lineRenderer.startColor = lineColor;
        _lineRenderer.endColor = lineColor;

        // The visualizer's local position y is the offset. X and Z should be 0 relative to parent.
        transform.localPosition = new UnityEngine.Vector3(0, yOffset, 0);

        UpdateCirclePoints();
        SetVisibility(_isVisible); // Apply initial visibility (usually hidden)
    }

    // No Update() needed if parameters are static once set and position is handled by parenting.

    private void UpdateCirclePoints()
    {
        if (segments < 3)
        {
            _lineRenderer.positionCount = 0;
            return;
        }

        _lineRenderer.positionCount = segments + 1; // segments points + first point to close loop

        float angleStep = 360.0f / segments;
        for (int i = 0; i <= segments; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            // Points are in this GameObject's local XZ plane.
            // Its Y position is already handled by transform.localPosition.y = yOffset.
            UnityEngine.Vector3 point = new UnityEngine.Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
            _lineRenderer.SetPosition(i, point);
        }
    }

    public void SetVisibility(bool visible)
    {
        _isVisible = visible;
        if (_lineRenderer != null)
        {
            _lineRenderer.enabled = _isVisible;
        }
    }

    // Call this if radius, segments, lineWidth, or lineColor change at runtime from parent.
    public void RefreshVisuals(float newRadius, Color newLineColor, float newLineWidth, int newSegments = -1)
    {
        if (_lineRenderer == null) _lineRenderer = GetComponent<LineRenderer>();

        radius = newRadius;
        lineColor = newLineColor;
        lineWidth = newLineWidth;
        if (newSegments > 0) segments = newSegments;

        _lineRenderer.startWidth = lineWidth;
        _lineRenderer.endWidth = lineWidth;
        _lineRenderer.startColor = lineColor;
        _lineRenderer.endColor = lineColor;

        UpdateCirclePoints();
        // Visibility is managed separately by SetVisibility
    }

    public void SetColor(Color color)
    {
        lineColor = color;
        if (_lineRenderer != null)
        {
            _lineRenderer.startColor = lineColor;
            _lineRenderer.endColor = lineColor;
        }
    }
     public void SetRadius(float newRadius)
    {
        if (Mathf.Approximately(radius, newRadius)) return;
        radius = newRadius;
        if(_lineRenderer != null && _lineRenderer.enabled) UpdateCirclePoints();
    }
}