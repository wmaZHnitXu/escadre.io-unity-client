using System.Runtime.CompilerServices; // For MethodImplOptions.AggressiveInlining


/// <summary>
/// Provides extension methods to convert between Core.Primitives types
/// and their corresponding UnityEngine equivalents.
/// This class should reside within the Unity project, not the Core library.
/// </summary>
public static class CoreToUnityAdapters
{
    // --- Vector2 Conversions ---

    /// <summary>
    /// Converts a Core.Primitives.Vector2 to a UnityEngine.Vector2.
    /// </summary>
    /// <param name="coreVector">The Core.Primitives.Vector2 instance.</param>
    /// <returns>A new UnityEngine.Vector2 with the same X and Y values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static UnityEngine.Vector2 ToUnityVector(this Core.Primitives.Vector2 coreVector)
    {
        return new UnityEngine.Vector2(coreVector.X, coreVector.Y);
    }

    /// <summary>
    /// Converts a UnityEngine.Vector2 to a Server.Core.Primitives.Vector2.
    /// </summary>
    /// <param name="unityVector">The UnityEngine.Vector2 instance.</param>
    /// <returns>A new Server.Core.Primitives.Vector2 with the same X and Y values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Core.Primitives.Vector2 ToCoreVector(this UnityEngine.Vector2 unityVector)
    {
        return new Core.Primitives.Vector2(unityVector.x, unityVector.y);
    }

    // --- Vector3 Conversions ---

    /// <summary>
    /// Converts a Server.Core.Primitives.Vector3 to a UnityEngine.Vector3.
    /// </summary>
    /// <param name="coreVector">The Server.Core.Primitives.Vector3 instance.</param>
    /// <returns>A new UnityEngine.Vector3 with the same X, Y, and Z values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static UnityEngine.Vector3 ToUnityVector(this Core.Primitives.Vector3 coreVector)
    {
        return new UnityEngine.Vector3(coreVector.X, coreVector.Y, coreVector.Z);
    }

    /// <summary>
    /// Converts a UnityEngine.Vector3 to a Server.Core.Primitives.Vector3.
    /// </summary>
    /// <param name="unityVector">The UnityEngine.Vector3 instance.</param>
    /// <returns>A new Server.Core.Primitives.Vector3 with the same X, Y, and Z values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Core.Primitives.Vector3 ToCoreVector(this UnityEngine.Vector3 unityVector)
    {
        return new Core.Primitives.Vector3(unityVector.x, unityVector.y, unityVector.z);
    }

    // --- Quaternion Conversions ---

    /// <summary>
    /// Converts a Core.Primitives.Quaternion to a UnityEngine.Quaternion.
    /// </summary>
    /// <param name="coreQuaternion">The Core.Primitives.Quaternion instance.</param>
    /// <returns>A new UnityEngine.Quaternion with the same X, Y, Z, and W values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static UnityEngine.Quaternion ToUnityQuaternion(this Core.Primitives.Quaternion coreQuaternion)
    {
        return new UnityEngine.Quaternion(coreQuaternion.X, coreQuaternion.Y, coreQuaternion.Z, coreQuaternion.W);
    }

    /// <summary>
    /// Converts a UnityEngine.Quaternion to a Core.Primitives.Quaternion.
    /// </summary>
    /// <param name="unityQuaternion">The UnityEngine.Quaternion instance.</param>
    /// <returns>A new Core.Primitives.Quaternion with the same X, Y, Z, and W values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Core.Primitives.Quaternion ToCoreQuaternion(this UnityEngine.Quaternion unityQuaternion)
    {
        return new Core.Primitives.Quaternion(unityQuaternion.x, unityQuaternion.y, unityQuaternion.z, unityQuaternion.w);
    }
}