// File: Scripts/Server/Debug/DebugPresentationManager.cs
using UnityEngine;
using System.Collections.Generic;
using Core.Model; // Level, Entity, EntityTypeEnum
using Core.Logging;
using Logger = Core.Logging.Logger;
using System;

namespace ServerSpecific.Debug // Namespace outside Core
{
    /// <summary>
    /// Manages the instantiation and lifecycle of debug GameObjects
    /// that represent server-side Core.Model.Entity instances within the Unity Editor scene.
    /// </summary>
    public class DebugPresentationManager : MonoBehaviour
    {
        [System.Serializable]
        public class EntityTypePrefabPair
        {
            public Entity.EntityTypeEnum EntityType;
            public GameObject DebugPrefab; // Prefab must have ModelEntityDebugBehaviour or derived script
        }

        [Tooltip("Mapping between Core Entity types and the prefabs used for their debug representation.")]
        [SerializeField] private List<EntityTypePrefabPair> entityPrefabMapping = new List<EntityTypePrefabPair>();

        [Tooltip("Optional parent transform for instantiated debug objects.")]
        [SerializeField] private Transform debugObjectParent;

        private Level _observedLevel;
        private Dictionary<int, GameObject> _activeDebugObjects = new Dictionary<int, GameObject>();
        private Dictionary<Entity.EntityTypeEnum, GameObject> _prefabLookup = new Dictionary<Entity.EntityTypeEnum, GameObject>();

        private bool _isInitialized = false;

        /// <summary>
        /// Initializes the manager with the Level to observe.
        /// Should be called by ServerComposer or similar setup script.
        /// </summary>
        public void Initialize(Level level)
        {
            if (_isInitialized)
            {
                Logger.LogWarning("[DebugPresentationManager] Already initialized.");
                return;
            }
            _observedLevel = level ?? throw new ArgumentNullException(nameof(level));

            // Build lookup dictionary for faster prefab access
            _prefabLookup.Clear();
            foreach (var pair in entityPrefabMapping)
            {
                if (pair.DebugPrefab == null)
                {
                    Logger.LogWarning($"[DebugPresentationManager] Prefab not assigned for EntityType: {pair.EntityType}");
                    continue;
                }
                if (!_prefabLookup.ContainsKey(pair.EntityType))
                {
                    _prefabLookup.Add(pair.EntityType, pair.DebugPrefab);
                }
                else
                {
                     Logger.LogWarning($"[DebugPresentationManager] Duplicate EntityType mapping found for: {pair.EntityType}. Using first entry.");
                }
            }

            // Subscribe to level events
            _observedLevel.OnEntityAddedEvent += HandleEntityAdded;

            // Handle already existing entities if manager initialized after level population
            foreach(var entity in _observedLevel.GetAllEntities())
            {
                 HandleEntityAdded(entity); // Process existing ones
            }


            _isInitialized = true;
            Logger.Log("[DebugPresentationManager] Initialized and subscribed to Level events.");
        }

        private void HandleEntityAdded(Entity entity)
        {
             if (!_isInitialized || entity == null || entity.IsDead || _activeDebugObjects.ContainsKey(entity.Id))
             {
                // Logger.Log($"[DebugPresentationManager] Skipping HandleEntityAdded for {entity?.Id}. Initialized: {_isInitialized}, EntityNull: {entity==null}, IsDead: {entity?.IsDead}, AlreadyActive: {_activeDebugObjects.ContainsKey(entity?.Id ?? -1)}");
                return;
             }

            Logger.Log($"[DebugPresentationManager] Entity Added: {entity.Id} ({entity.EntityType}). Creating debug representation.");

            if (_prefabLookup.TryGetValue(entity.EntityType, out GameObject prefabToInstantiate))
            {
                GameObject newDebugInstance = Instantiate(prefabToInstantiate, debugObjectParent);
                // Set initial transform (optional, ModelEntityDebugBehaviour will follow if enabled)
                newDebugInstance.transform.position = entity.Position.ToUnityVector();
                newDebugInstance.transform.rotation = entity.Rotation.ToUnityQuaternion();

                ModelEntityDebugBehaviour debugScript = newDebugInstance.GetComponent<ModelEntityDebugBehaviour>();
                if (debugScript != null)
                {
                    debugScript.Initialize(entity);
                    _activeDebugObjects.Add(entity.Id, newDebugInstance);

                    // Subscribe to THIS entity's death event for cleanup
                    entity.OnDeathEvent += HandleEntityDeath;
                }
                else
                {
                    Logger.LogError($"[DebugPresentationManager] Prefab for {entity.EntityType} does not contain a ModelEntityDebugBehaviour script! Destroying instance.");
                    Destroy(newDebugInstance);
                }
            }
            else
            {
                Logger.LogWarning($"[DebugPresentationManager] No debug prefab mapped for EntityType: {entity.EntityType}. Cannot create debug representation for Entity {entity.Id}.");
            }
        }

        private void HandleEntityDeath(Entity entity)
        {
            if (!_isInitialized || entity == null) return;

            // Logger.Log($"[DebugPresentationManager] Entity Died: {entity.Id}. Destroying debug representation.");

            // Unsubscribe immediately to prevent issues if event fires multiple times
            entity.OnDeathEvent -= HandleEntityDeath;

            if (_activeDebugObjects.TryGetValue(entity.Id, out GameObject debugObject))
            {
                Destroy(debugObject); // Destroy the GameObject
                _activeDebugObjects.Remove(entity.Id);
            }
            // else { Logger.LogWarning($"[DebugPresentationManager] HandleEntityDeath: No active debug object found for Entity {entity.Id}."); }
        }

        private void OnDestroy()
        {
             Logger.Log("[DebugPresentationManager] OnDestroy called. Cleaning up...");
            if (_observedLevel != null)
            {
                _observedLevel.OnEntityAddedEvent -= HandleEntityAdded;
                 // Unsubscribe from death events for all remaining entities
                 foreach (var kvp in _activeDebugObjects)
                 {
                     if (_observedLevel.TryGetEntity(kvp.Key, out var entity))
                     {
                         entity.OnDeathEvent -= HandleEntityDeath;
                     }
                     // Destroy remaining GameObjects
                     if (kvp.Value != null) Destroy(kvp.Value);
                 }
            }
             _activeDebugObjects.Clear();
             _prefabLookup.Clear();
            _isInitialized = false;
             Logger.Log("[DebugPresentationManager] Cleanup complete.");
        }
    }
}