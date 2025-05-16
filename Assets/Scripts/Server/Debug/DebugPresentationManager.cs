// File: Scripts/Server/Debug/DebugPresentationManager.cs
using UnityEngine;
using System.Collections.Generic;
using Core.Model;
using Core.Logging;
using Logger = Core.Logging.Logger;
using System;

namespace ServerSpecific.Debug
{
    public class DebugPresentationManager : MonoBehaviour
    {
        [System.Serializable]
        public class EntityTypePrefabPair { /* ... as before ... */
            public Entity.EntityTypeEnum EntityType;
            public GameObject DebugPrefab;
        }

        [SerializeField] private List<EntityTypePrefabPair> entityPrefabMapping = new List<EntityTypePrefabPair>();
        [SerializeField] private Transform debugObjectParent;

        private Level _observedLevel;
        private Dictionary<int, GameObject> _activeDebugObjects = new Dictionary<int, GameObject>();
        private Dictionary<Entity.EntityTypeEnum, GameObject> _prefabLookup = new Dictionary<Entity.EntityTypeEnum, GameObject>();
        private bool _isInitialized = false;

        public void Initialize(Level level)
        {
            if (_isInitialized) { /* ... */ return; }
            _observedLevel = level ?? throw new ArgumentNullException(nameof(level));
            _prefabLookup.Clear();
            foreach (var pair in entityPrefabMapping) { /* ... */
                 if (pair.DebugPrefab == null) { Logger.LogWarning($"[DebugPresentationManager] Prefab not assigned for EntityType: {pair.EntityType}"); continue; }
                if (!_prefabLookup.ContainsKey(pair.EntityType)) { _prefabLookup.Add(pair.EntityType, pair.DebugPrefab); }
                else { Logger.LogWarning($"[DebugPresentationManager] Duplicate EntityType mapping: {pair.EntityType}."); }
            }
            _observedLevel.OnEntityAddedEvent += HandleEntityAdded;
            foreach(var entity in _observedLevel.GetAllEntities()) { HandleEntityAdded(entity); }
            _isInitialized = true;
            Logger.Log("[DebugPresentationManager] Initialized.");
        }

        private void HandleEntityAdded(Entity entity)
        {
             if (!_isInitialized || entity == null || _activeDebugObjects.ContainsKey(entity.Id)) return;
             // Don't filter by entity.IsDead here, let the debug object reflect its state, even if initially dead.

            Logger.Log($"[DebugPresentationManager] Entity Added: {entity.Id} ({entity.EntityType}). Creating debug representation.");
            if (_prefabLookup.TryGetValue(entity.EntityType, out GameObject prefabToInstantiate))
            {
                Transform parentToUse = debugObjectParent != null ? debugObjectParent : this.transform;
                GameObject newDebugInstance = Instantiate(prefabToInstantiate, parentToUse);
                newDebugInstance.transform.position = entity.Position.ToUnityVector();
                newDebugInstance.transform.rotation = entity.Rotation.ToUnityQuaternion();
                ModelEntityDebugBehaviour debugScript = newDebugInstance.GetComponent<ModelEntityDebugBehaviour>();
                if (debugScript != null)
                {
                    debugScript.Initialize(entity);
                    _activeDebugObjects.Add(entity.Id, newDebugInstance);
                    entity.OnDeathEvent += HandleEntityDeath; // Subscribe here after successful creation
                }
                else { /* ... error log and destroy ... */
                    Logger.LogError($"[DebugPresentationManager] Prefab for {entity.EntityType} does not contain ModelEntityDebugBehaviour! Destroying instance.");
                    Destroy(newDebugInstance);
                }
            }
            else { /* ... warning log ... */
                 Logger.LogWarning($"[DebugPresentationManager] No debug prefab for EntityType: {entity.EntityType}. Entity ID: {entity.Id}.");
            }
        }

        private void HandleEntityDeath(Entity entity)
        {
            if (!_isInitialized || entity == null) return;
            entity.OnDeathEvent -= HandleEntityDeath; // Unsubscribe immediately
            if (_activeDebugObjects.TryGetValue(entity.Id, out GameObject debugObject))
            {
                // Logger.Log($"[DebugPresentationManager] Entity Died: {entity.Id}. Destroying debug GO.");
                Destroy(debugObject);
                _activeDebugObjects.Remove(entity.Id);
            }
        }

        /// <summary>
        /// Tries to get the active debug GameObject associated with a given entity ID.
        /// </summary>
        public bool TryGetDebugGameObjectForEntity(int entityId, out GameObject debugGO)
        {
            if (!_isInitialized)
            {
                debugGO = null;
                return false;
            }
            return _activeDebugObjects.TryGetValue(entityId, out debugGO);
        }

        private void OnDestroy()
        {
             Logger.Log("[DebugPresentationManager] OnDestroy. Cleaning up...");
            if (_observedLevel != null) { _observedLevel.OnEntityAddedEvent -= HandleEntityAdded; }
            foreach (var kvp in _activeDebugObjects)
            {
                 if (_observedLevel != null && _observedLevel.TryGetEntity(kvp.Key, out var entity)) { entity.OnDeathEvent -= HandleEntityDeath;}
                 if (kvp.Value != null) Destroy(kvp.Value);
            }
            _activeDebugObjects.Clear();
            _prefabLookup.Clear();
            _isInitialized = false;
             Logger.Log("[DebugPresentationManager] Cleanup complete.");
        }
    }
}