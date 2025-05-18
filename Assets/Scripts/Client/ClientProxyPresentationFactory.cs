// File: Scripts/Client/Presentation/ClientProxyPresentationFactory.cs
using UnityEngine;
using Core.Network; // For IClientProxy

/// <summary>
/// Abstract base class for factories that create and manage ClientProxyPresentation instances.
/// </summary>
public abstract class ClientProxyPresentationFactory : MonoBehaviour
{
    public abstract ClientProxyPresentation AllocatePresentation(IClientProxy targetProxy, GameObject prefab);
}