// File: Scripts/Shared/Ocean/OceanSettingsProvider.cs
using UnityEngine;
using Core.Ocean;
using Core.Logging;
using Logger = Core.Logging.Logger;

public class OceanSettingsProvider : MonoBehaviour
{
    [Header("Ocean Texture File")]
    [Tooltip("Assign the .bytes file containing the 3D ocean texture data (64x64x64 slices, RGB for XYZ displacement).")]
    public TextAsset oceanTextureBytesFile;

    [Header("Core Ocean Parameters")]
    [Tooltip("Overall multiplier for the displacement values read from the texture.")]
    public float displacementScale = 1.5f;
    
    [Tooltip("The real-world size (in game units) that one full tile of the X-Z plane of the texture covers.")]
    public float textureTileWorldSize = 128.0f;
    
    [Tooltip("The real-world time (in seconds) that one full loop of all time slices in the texture represents.")]
    public float textureTimeLoopDuration = 20.0f;

    [Header("Texture Dimensions (Must match the source .bytes file)")]
    [Tooltip("Resolution of the texture in X and Z dimensions (e.g., 64 for a 64x64 texel slice).")]
    public int textureResolutionXZ = 64;
    
    [Tooltip("Number of time slices in the texture (e.g., 64 for 64 time steps).")]
    public int textureResolutionTime = 64;
    
    private byte[] _loadedOceanBytes;
    private bool _bytesLoadedAttempted = false;

    void Awake()
    {
        LoadBytesAndLog();
    }

    private void LoadBytesAndLog()
    {
        if (_bytesLoadedAttempted) return;
        _bytesLoadedAttempted = true;

        if (oceanTextureBytesFile != null && oceanTextureBytesFile.bytes != null && oceanTextureBytesFile.bytes.Length > 0)
        {
            _loadedOceanBytes = oceanTextureBytesFile.bytes;
            int expectedSize = textureResolutionTime * textureResolutionXZ * textureResolutionXZ * 3;
            if (_loadedOceanBytes.Length == expectedSize)
            {
                Logger.Log($"[OceanSettingsProvider] Successfully loaded {_loadedOceanBytes.Length} bytes from ocean texture file: {oceanTextureBytesFile.name}");
            }
            else
            {
                 Logger.LogWarning($"[OceanSettingsProvider] Ocean texture file '{oceanTextureBytesFile.name}' has unexpected size. Expected {expectedSize}, got {_loadedOceanBytes.Length}. This might lead to errors or incorrect ocean behavior. Please ensure Texture Dimensions match the file content and layout.");
            }
        }
        else
        {
            Logger.LogWarning($"[OceanSettingsProvider] Ocean texture file '{oceanTextureBytesFile?.name ?? "NOT ASSIGNED"}' not assigned, empty, or failed to load. Ocean data providers will likely use dummy data or fail if they require real data.");
            _loadedOceanBytes = null;
        }
    }

    /// <summary>
    /// Gets the OceanSettings configured in the Inspector.
    /// </summary>
    public OceanSettings GetSettings()
    {
        return new OceanSettings(
            displacementScale,
            textureTileWorldSize,
            textureTimeLoopDuration,
            textureResolutionXZ,
            textureResolutionTime
        );
    }

    /// <summary>
    /// Gets the raw byte data loaded from the oceanTextureBytesFile.
    /// Returns null if the file was not assigned or failed to load.
    /// </summary>
    public byte[] GetOceanTextureBytes()
    {
        if (!_bytesLoadedAttempted) // Ensure bytes are loaded if accessed before Awake (e.g. by another Awake)
        {
            LoadBytesAndLog();
        }
        return _loadedOceanBytes;
    }
}