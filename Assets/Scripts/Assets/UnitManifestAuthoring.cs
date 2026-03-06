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
    [SerializeField]
    public EntityData[] totalManifest;
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
        data = ScriptableObjectUtil.LoadAllScriptableObjects<EntityData>();
        
        totalManifest = data
            //.Where(e => e.entityType == EntityType.Unit)
            .OrderBy(e => e.entityGuid)
            .ToArray();
        
        // Update each entity's key to match array index
        for (int i = 0; i < totalManifest.Length; i++)
        {
            totalManifest[i].key = i;
            EditorUtility.SetDirty(totalManifest[i]);
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
/// <summary>
/// The outer array is tied to selection key while
/// the inner array is tied to passed action byte
/// </summary>
public struct ActionInfoBlob
{
    public BlobArray<BlobArray<ActionInfo>> UnitsActionInfo;
}

// Singleton component that holds the blob reference
public struct ActionInfoManifest : IComponentData
{
    public BlobAssetReference<ActionInfoBlob> Blob;
}

class ActionInfoManifestBaker : Baker<UnitManifestAuthoring>
{
    public override void Bake(UnitManifestAuthoring authoring)
    {
        var builder = new BlobBuilder(Allocator.Temp);

        ref ActionInfoBlob root = ref builder.ConstructRoot<ActionInfoBlob>();

        // Allocate outer array (one entry per unit)
        var outerArray = builder.Allocate(ref root.UnitsActionInfo, authoring.totalManifest.Length);

        for (int i = 0; i < authoring.totalManifest.Length; i++)
        {
            var actions = authoring.totalManifest[i].actions;

            // Allocate the inner array
            var innerArray = builder.Allocate(ref outerArray[i], actions.Length);

            for (int a = 0; a < actions.Length; a++)
            {
                innerArray[a] = actions[a];
            }
        }

        var blobRef = builder.CreateBlobAssetReference<ActionInfoBlob>(Allocator.Persistent);
        AddBlobAsset(ref blobRef, out _);
        builder.Dispose();

        var entity = GetEntity(authoring, TransformUsageFlags.None);
        AddComponent(entity, new ActionInfoManifest { Blob = blobRef });
    }
}
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