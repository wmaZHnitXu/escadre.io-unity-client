// File: Scripts/Client/Presentation/OceanPresentation.cs
using UnityEngine;
using Core.Ocean;
using Core.Time;
using Core.Logging;
using Unity.Collections; // Required for NativeArray
using Logger = Core.Logging.Logger;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class OceanPresentation : MonoBehaviour
{
    [Header("Mesh Settings")]
    [SerializeField] public float oceanPlaneSize = 1000f; // This will be the diameter of the circle
    [SerializeField] private int oceanPlaneSegments = 100; // Higher for more detail
    [SerializeField] private float oceanYLevel = 0f; // The Y level of the ocean plane

    private MeshFilter _meshFilter;
    private MeshRenderer _meshRenderer;
    private Material _oceanMaterialInstance; // Renamed to indicate it's an instance
    private Texture3D _oceanDisplacementTexture;

    private IOceanDataProvider _oceanDataProvider;
    private IClock _clock;
    private bool _isInitialized = false;
    private float _snapCellSize = 1.0f; // Default, will be calculated

    private static readonly int GlobalTimeProperty = Shader.PropertyToID("_GlobalTime");
    private static readonly int OceanTexProperty = Shader.PropertyToID("_OceanTex");
    private static readonly int OceanSettingsParamsProperty = Shader.PropertyToID("_OceanSettingsParams");
    private static readonly int MaxByteToDispUnscaledConstProperty = Shader.PropertyToID("_MaxByteToDispUnscaledConst");

    public float OceanRadius => oceanPlaneSize / 2.0f;

    void Awake()
    {
        _meshFilter = GetComponent<MeshFilter>();
        _meshRenderer = GetComponent<MeshRenderer>();
        transform.position = new Vector3(transform.position.x, oceanYLevel, transform.position.z);
    }

    public void Initialize(IOceanDataProvider dataProvider, IClock clock, byte[] rawTextureBytes, Material oceanSharedMaterial)
    {
        if (dataProvider == null)
        {
            Logger.LogError("[OceanPresentation] IOceanDataProvider cannot be null.");
            enabled = false;
            return;
        }
        if (clock == null)
        {
            Logger.LogError("[OceanPresentation] IClock cannot be null.");
            enabled = false;
            return;
        }
        if (rawTextureBytes == null || rawTextureBytes.Length == 0)
        {
            Logger.LogError("[OceanPresentation] Raw texture bytes are null or empty.");
            enabled = false;
            return;
        }
        if (oceanSharedMaterial == null)
        {
            Logger.LogError("[OceanPresentation] Ocean shared material is null.");
            enabled = false;
            return;
        }

        _oceanDataProvider = dataProvider;
        _clock = clock;
        OceanSettings settings = _oceanDataProvider.Settings;

        _meshFilter.mesh = CreateCircularOceanMesh(oceanPlaneSize, oceanPlaneSegments);
        _snapCellSize = oceanPlaneSize / oceanPlaneSegments;

        _oceanDisplacementTexture = new Texture3D(
            settings.TextureResolutionXZ,
            settings.TextureResolutionXZ,
            settings.TextureResolutionTime,
            TextureFormat.RGB24,
            false); // mipChain = false

        _oceanDisplacementTexture.wrapMode = TextureWrapMode.Repeat;
        _oceanDisplacementTexture.filterMode = FilterMode.Bilinear;

        NativeArray<byte> textureDataNative = new NativeArray<byte>(rawTextureBytes, Allocator.Temp);
        _oceanDisplacementTexture.SetPixelData(textureDataNative, 0);
        textureDataNative.Dispose();

        _oceanDisplacementTexture.Apply(false, true);

        // Create an instance of the material to avoid modifying the shared asset
        _oceanMaterialInstance = new Material(oceanSharedMaterial);
        _meshRenderer.material = _oceanMaterialInstance;

        _oceanMaterialInstance.SetTexture(OceanTexProperty, _oceanDisplacementTexture);

        _oceanMaterialInstance.SetVector(OceanSettingsParamsProperty, new Vector4(
            settings.DisplacementScale,
            settings.TextureTileWorldSize,
            settings.TextureTimeLoopDuration,
            settings.TextureResolutionXZ
        ));
        _oceanMaterialInstance.SetFloat(MaxByteToDispUnscaledConstProperty, 2.0f);

        _isInitialized = true;
        Logger.Log($"[OceanPresentation] Initialized. Ocean Diameter: {oceanPlaneSize}, Segments: {oceanPlaneSegments}. Texture: {settings.TextureResolutionXZ}x{settings.TextureResolutionXZ}x{settings.TextureResolutionTime}");
    }

    /// <summary>
    /// Sets the target world position for the ocean presentation to follow, snapping to a grid.
    /// </summary>
    public void FollowTarget(Vector3 targetWorldPosition)
    {
        if (!_isInitialized) return;

        float snappedX = Mathf.Round(targetWorldPosition.x / _snapCellSize) * _snapCellSize;
        float snappedZ = Mathf.Round(targetWorldPosition.z / _snapCellSize) * _snapCellSize;

        transform.position = new Vector3(snappedX, oceanYLevel, snappedZ);
    }


    void Update()
    {
        if (!_isInitialized || _oceanMaterialInstance == null || _clock == null)
        {
            return;
        }
        _oceanMaterialInstance.SetFloat(GlobalTimeProperty, _clock.CurrentTime);
    }

    private Mesh CreateCircularOceanMesh(float diameter, int segments)
    {
        Mesh mesh = new Mesh();
        mesh.name = "ProceduralCircularOceanPlane";

        float radius = diameter / 2.0f;
        int vertexCount = (segments + 1) * (segments + 1);
        Vector3[] vertices = new Vector3[vertexCount];
        Vector2[] uv = new Vector2[vertexCount];
        int[] triangles = new int[segments * segments * 6];

        float segmentSize = diameter / segments;

        for (int i = 0, z = 0; z <= segments; z++)
        {
            for (int x = 0; x <= segments; x++, i++)
            {
                // Create vertices for a square grid first
                float vx = x * segmentSize - radius; // Centered coordinates
                float vz = z * segmentSize - radius; // Centered coordinates

                Vector2 pointOnSquare = new Vector2(vx, vz);
                float distFromCenter = pointOnSquare.magnitude;

                if (distFromCenter > radius)
                {
                    // Push vertex onto the circle's edge if it's outside
                    Vector2 dir = pointOnSquare.normalized;
                    vertices[i] = new Vector3(dir.x * radius, 0, dir.y * radius);
                }
                else
                {
                    vertices[i] = new Vector3(vx, 0, vz);
                }
                // UVs are mapped as if it's a full square, texture will be clipped by mesh shape
                uv[i] = new Vector2((float)x / segments, (float)z / segments);
            }
        }

        for (int ti = 0, vi = 0, z = 0; z < segments; z++, vi++)
        {
            for (int x = 0; x < segments; x++, ti += 6, vi++)
            {
                triangles[ti] = vi;
                triangles[ti + 1] = vi + segments + 1;
                triangles[ti + 2] = vi + 1;

                triangles[ti + 3] = vi + 1;
                triangles[ti + 4] = vi + segments + 1;
                triangles[ti + 5] = vi + segments + 2;
            }
        }

        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        // Normals are calculated in the shader or can be recalculated if needed (mesh.RecalculateNormals())
        // For a flat plane intended for shader displacement, custom normals might not be critical here.

        return mesh;
    }

    void OnDestroy()
    {
        if (_oceanDisplacementTexture != null)
        {
            Destroy(_oceanDisplacementTexture);
            _oceanDisplacementTexture = null;
        }

        if (_oceanMaterialInstance != null)
        {
            Destroy(_oceanMaterialInstance); // Always destroy the instance
        }
        _oceanMaterialInstance = null;

        if (_meshFilter != null && _meshFilter.sharedMesh != null && _meshFilter.sharedMesh.name == "ProceduralCircularOceanPlane")
        {
            Destroy(_meshFilter.sharedMesh);
        }
        _isInitialized = false;
    }
}