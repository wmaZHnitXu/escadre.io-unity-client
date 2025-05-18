// File: Scripts/Client/Presentation/CommonClientProxyPresentationFactory.cs
using System.Collections.Generic;
using UnityEngine;
using Core.Network; // For IClientProxy
using Core.Logging;
using Logger = Core.Logging.Logger;

/// <summary>
/// A common factory for ClientProxyPresentation instances that implements object pooling.
/// </summary>
public class CommonClientProxyPresentationFactory : ClientProxyPresentationFactory
{
    protected Dictionary<GameObject, Queue<ClientProxyPresentation>> objectPools = new Dictionary<GameObject, Queue<ClientProxyPresentation>>();
    private int _instanceCounter = 0; // For unique naming of new instances

    public override ClientProxyPresentation AllocatePresentation(IClientProxy targetProxy, GameObject prefab)
    {
        if (targetProxy == null) throw new System.ArgumentNullException(nameof(targetProxy));
        if (prefab == null) throw new System.ArgumentNullException(nameof(prefab));

        ClientProxyPresentation presentationInstance;
        Queue<ClientProxyPresentation> pool;

        if (!objectPools.TryGetValue(prefab, out pool))
        {
            pool = new Queue<ClientProxyPresentation>();
            objectPools.Add(prefab, pool);
        }

        if (pool.Count == 0)
        {
            GameObject newGO = Instantiate(prefab, Vector3.zero, Quaternion.identity);
            presentationInstance = newGO.GetComponent<ClientProxyPresentation>();
            if (presentationInstance == null)
            {
                Logger.LogError($"[CommonClientProxyFactory] Prefab '{prefab.name}' is missing a ClientProxyPresentation component! Destroying instance.");
                Destroy(newGO);
                return null; // Or throw exception
            }
            newGO.name = $"{prefab.name}_Instance_{++_instanceCounter}";
            // Logger.Log($"[CommonClientProxyFactory] Instantiated new presentation: {newGO.name}");
        }
        else
        {
            presentationInstance = pool.Dequeue();
            // Logger.Log($"[CommonClientProxyFactory] Reusing presentation from pool: {presentationInstance.gameObject.name}");
        }

        presentationInstance.InitializePresentation(targetProxy, prefab); // Pass prefab for pooling key
        presentationInstance.PresentationDisposedEvent += ReturnToPool;

        return presentationInstance;
    }

    private void ReturnToPool(ClientProxyPresentation presentation)
    {
        if (presentation == null || presentation.PrefabReference == null)
        {
            Logger.LogWarning("[CommonClientProxyFactory] Attempted to return a null presentation or presentation with null PrefabReference to pool.");
            if(presentation != null && presentation.gameObject != null) Destroy(presentation.gameObject); // Clean up if possible
            return;
        }

        presentation.PresentationDisposedEvent -= ReturnToPool; // Unsubscribe

        if (objectPools.TryGetValue(presentation.PrefabReference, out Queue<ClientProxyPresentation> pool))
        {
            pool.Enqueue(presentation);
            // Logger.Log($"[CommonClientProxyFactory] Returned {presentation.gameObject.name} to pool for prefab {presentation.PrefabReference.name}. Pool size: {pool.Count}");
        }
        else
        {
            Logger.LogWarning($"[CommonClientProxyFactory] No pool found for prefab {presentation.PrefabReference.name} when trying to return {presentation.gameObject.name}. Destroying instead.");
            if (presentation.gameObject != null) Destroy(presentation.gameObject);
        }
    }

    // Optional: Method to pre-warm pools or clear them
    public void ClearPools()
    {
        Logger.Log("[CommonClientProxyFactory] Clearing all object pools.");
        foreach (var pool in objectPools.Values)
        {
            while (pool.Count > 0)
            {
                ClientProxyPresentation item = pool.Dequeue();
                if (item != null && item.gameObject != null) Destroy(item.gameObject);
            }
        }
        objectPools.Clear();
    }

    void OnDestroy()
    {
        ClearPools(); // Ensure pooled objects are destroyed when the factory itself is destroyed
    }
}