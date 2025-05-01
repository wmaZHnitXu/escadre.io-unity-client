using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Server.Core.Model;
using UnityEngine;

public class LevelIntegrityTestComposition : MonoBehaviour, IComposer<Level>
{
    [SerializeField] private LevelIntegrityTestPresenter _presenter;
    private Level _level;
    private HashSet<Entity> _entities;
    public Level Assemble()
    {
        _entities = new ();
        Level level = new Level();
        level.OnEntityAddedEvent += _presenter.AllocatePresentation;
        return level;
    }

    void Start()
    {
        _level = Assemble();       
    }

    void Update()
    {
        bool youAreFreeToSpawn = Input.GetKey(KeyCode.W);

        if (_entities.Count > 0 && Random.Range(0f, 1.0f) < 0.1f) {
            int index = Random.Range(0, _entities.Count - 1);
            var ent = _entities.ElementAt(index);
            ent.Kill();
            _entities.Remove(ent);
        }



        
        _level.DoUpdate(Time.deltaTime);



        if (Random.Range(0f, 1.0f) < 0.1f && youAreFreeToSpawn) {
            DebugEntity debugEntity = new DebugEntity(_level, new Vector3(
                10.1f * Random.Range(0f, 1.0f),
                10f * Random.Range(0f, 1.0f),
                1f * Random.Range(0f, 1.0f)).ToCoreVector());
            _entities.Add(debugEntity);
        }
    }
}
