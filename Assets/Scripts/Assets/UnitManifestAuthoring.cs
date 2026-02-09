using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEditor;
using System.Linq;
using Unity.Entities;
using UnityEngine;

public class UnitManifestAuthoring : MonoBehaviour
{
    [SerializeField]
    public EntityData[] manifest;

    #if UNITY_EDITOR
    [ContextMenu("Update manifest")]
    public void UpdateManifest()
    {
        var data = ScriptableObjectUtil.LoadAllScriptableObjects<EntityData>();
        
        manifest = data
            .Where(e => e.entityType == EntityType.Unit)
            .OrderBy(e => e.entityGuid)
            .ToArray();
        
        // Update each entity's key to match array index
        for (int i = 0; i < manifest.Length; i++)
        {
            manifest[i].key = i;
            EditorUtility.SetDirty(manifest[i]);
        }
        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets();
    }
    #endif
    //add a function to fetch the Entity data
}
// [Serializable]
// public class UnitManifestAuthoringElement
// {
//     public GameObject prefab;
//     public EntityData data;
// }

class UnitManifestBaker : Baker<UnitManifestAuthoring>
{
    public override void Bake(UnitManifestAuthoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.None);

        // AddBuffer creates and returns the buffer - no need for AddComponent
        var buffer = AddBuffer<UnitManifest>(entity);

        foreach (var e in authoring.manifest)
        {
            if (e != null)
            {
                var prefabEntity = GetEntity(e.prefab, TransformUsageFlags.Dynamic);
                buffer.Add(new UnitManifest { Unit = prefabEntity, 
                TrainingTime = e.trainingTime});
            }
        }
    }
}

// Buffer element should be simple - just hold one entity
[InternalBufferCapacity(8)] // Number of elements before it allocates to heap
public struct UnitManifest : IBufferElementData
{
    public Entity Unit;
    public float TrainingTime;
}