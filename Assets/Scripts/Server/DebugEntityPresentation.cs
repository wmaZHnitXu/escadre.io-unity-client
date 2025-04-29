using System.Collections;
using System.Collections.Generic;
using Server.Core.Model;
using UnityEngine;

public class DebugEntityPresentation : MonoBehaviour, IEntityObserver<DebugEntity>
{
    [SerializeField] private DebugEntity _debugEntity;
    public void ConnectToEntity(DebugEntity debugEntity)
    {
        _debugEntity = debugEntity;
        _debugEntity.OnDeathEvent += (Entity entity) => { Destroy(gameObject); };
    }

    void Update() {
        transform.position = _debugEntity.Position.ToUnityVector();
        Debug.Log(_debugEntity.IsDead);
    }
}
