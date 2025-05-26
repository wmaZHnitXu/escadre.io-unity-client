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
    [SerializeField] private float oceanPlaneSize = 1000f;
    [SerializeField] private int oceanPlaneSegments = 100; // Higher for more detail

    private MeshFilter _meshFilter;
    private MeshRenderer _meshRenderer;
    private Material _oceanMaterial;
    private Material _originalMaterial; // Keep reference to original for cleanup
    private bool _usingOriginalMaterial = false; // Track if we're using the original or a copy
    private Texture3D _oceanDisplacementTexture;

    private IOceanDataProvider _oceanDataProvider; // For settings
    private IClock _clock;
    private bool _isInitialized = false;

    private static readonly int GlobalTimeProperty = Shader.PropertyToID("_GlobalTime");
    private static readonly int OceanTexProperty = Shader.PropertyToID("_OceanTex");
    private static readonly int OceanSettingsParamsProperty = Shader.PropertyToID("_OceanSettingsParams");
    private static readonly int MaxByteToDispUnscaledConstProperty = Shader.PropertyToID("_MaxByteToDispUnscaledConst");

    void Awake()
    {
        _meshFilter = GetComponent<MeshFilter>();
        _meshRenderer = GetComponent<MeshRenderer>();
    }

    public void Initialize(IOceanDataProvider dataProvider, IClock clock, byte[] rawTextureBytes, Material oceanMaterial)
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
        if (oceanMaterial == null)
        {
            Logger.LogError("[OceanPresentation] Ocean material is null.");
            enabled = false;
            return;
        }

        _oceanDataProvider = dataProvider;
        _clock = clock;
        OceanSettings settings = _oceanDataProvider.Settings;

        _meshFilter.mesh = CreateOceanPlaneMesh(oceanPlaneSize, oceanPlaneSegments);

        _oceanDisplacementTexture = new Texture3D(
            settings.TextureResolutionXZ, 
            settings.TextureResolutionXZ, 
            settings.TextureResolutionTime, 
            TextureFormat.RGB24,          
            false); // mipChain = false
        
        _oceanDisplacementTexture.wrapMode = TextureWrapMode.Repeat;
        _oceanDisplacementTexture.filterMode = FilterMode.Bilinear;
        
        // Correct way to load raw byte data:
        // Convert byte[] to NativeArray<byte> for SetPixelData
        NativeArray<byte> textureDataNative = new NativeArray<byte>(rawTextureBytes, Allocator.Temp);
        _oceanDisplacementTexture.SetPixelData(textureDataNative, 0); // Mipmap level 0
        textureDataNative.Dispose(); // Dispose the temporary NativeArray

        _oceanDisplacementTexture.Apply(false, true); // Apply changes, mark as non-readable by CPU

        // Store reference to original material
        _originalMaterial = oceanMaterial;

        // In play mode or if it's an asset, use the original material directly for real-time editing
        // In build or if we need isolation, create a copy
        #if UNITY_EDITOR
        if (Application.isPlaying)
        {
            // Use original material in play mode for real-time editing
            _oceanMaterial = oceanMaterial;
            _usingOriginalMaterial = true;
            Logger.Log("[OceanPresentation] Using original material for real-time editing in play mode.");
        }
        else
        {
            // Create copy when not in play mode
            _oceanMaterial = new Material(oceanMaterial);
            _usingOriginalMaterial = false;
        }
        #else
        // In builds, always create a copy to avoid modifying assets
        _oceanMaterial = new Material(oceanMaterial);
        _usingOriginalMaterial = false;
        #endif

        _meshRenderer.material = _oceanMaterial;

        _oceanMaterial.SetTexture(OceanTexProperty, _oceanDisplacementTexture);
        
        _oceanMaterial.SetVector(OceanSettingsParamsProperty, new Vector4(
            settings.DisplacementScale,
            settings.TextureTileWorldSize,
            settings.TextureTimeLoopDuration,
            settings.TextureResolutionXZ 
        ));
        _oceanMaterial.SetFloat(MaxByteToDispUnscaledConstProperty, 2.0f);

        _isInitialized = true;
        Logger.Log($"[OceanPresentation] Initialized. Ocean Size: {oceanPlaneSize}, Segments: {oceanPlaneSegments}. Texture: {settings.TextureResolutionXZ}x{settings.TextureResolutionXZ}x{settings.TextureResolutionTime}");
    }

    void Update()
    {
        if (!_isInitialized || _oceanMaterial == null || _clock == null)
        {
            return;
        }
        _oceanMaterial.SetFloat(GlobalTimeProperty, _clock.CurrentTime);
    }

    private Mesh CreateOceanPlaneMesh(float size, int segments)
    {
        Mesh mesh = new Mesh();
        mesh.name = "ProceduralOceanPlane";

        int vertexCount = (segments + 1) * (segments + 1);
        Vector3[] vertices = new Vector3[vertexCount];
        Vector2[] uv = new Vector2[vertexCount];
        int[] triangles = new int[segments * segments * 6];

        float segmentSize = size / segments;

        for (int i = 0, z = 0; z <= segments; z++)
        {
            for (int x = 0; x <= segments; x++, i++)
            {
                // Center the plane at (0,0,0)
                vertices[i] = new Vector3(x * segmentSize - size * 0.5f, 0, z * segmentSize - size * 0.5f);
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
        // Normals are calculated in the shader
        
        return mesh;
    }

    void OnDestroy()
    {
        if (_oceanDisplacementTexture != null)
        {
            Destroy(_oceanDisplacementTexture);
            _oceanDisplacementTexture = null;
        }
        
        // Only destroy the material if we created a copy
        if (_oceanMaterial != null && !_usingOriginalMaterial)
        {
            Destroy(_oceanMaterial);
        }
        _oceanMaterial = null;
        _originalMaterial = null;
        
        if (_meshFilter != null && _meshFilter.sharedMesh != null && _meshFilter.sharedMesh.name == "ProceduralOceanPlane")
        {
            Destroy(_meshFilter.sharedMesh); 
        }
        _isInitialized = false;
    }
}
