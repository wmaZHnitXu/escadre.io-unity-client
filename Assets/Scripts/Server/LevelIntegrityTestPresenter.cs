using System.Collections;
using System.Collections.Generic;
using Server.Core.Model;
using UnityEngine;

public class LevelIntegrityTestPresenter : MonoBehaviour
{
    [SerializeField] private GameObject _debugPrefab;

    public void AllocatePresentation(Entity entity) {
        var GO = Instantiate(_debugPrefab, Vector3.zero, Quaternion.identity);
        GO.GetComponent<DebugEntityPresentation>().ConnectToEntity(entity as DebugEntity);
    }
}
