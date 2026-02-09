using UnityEditor;
using System.Linq;
using Unity.Entities;
using UnityEngine;
using Unity.Mathematics;
using ConstructionMan;
public class StructureManifestAuthoring : MonoBehaviour
{
    [SerializeField]
    public EntityData[] manifest;
    public ConstructionData[] construction;

    #if UNITY_EDITOR
    [ContextMenu("Update manifest")]
    public void UpdateManifest()
    {
        var data = ScriptableObjectUtil.LoadAllScriptableObjects<EntityData>();
        var constructionData = ScriptableObjectUtil.LoadAllScriptableObjects<ConstructionData>();

        manifest = data
            .Where(e => e.entityType == EntityType.Structure)
            .OrderBy(e => e.entityGuid)
            .ToArray();

        construction = constructionData
            .OrderBy(e => e.Guid)
            .ToArray();
        

        // Update each entity's key to match array index
        for (int i = 0; i < manifest.Length; i++)
        {
            //UnityEngine.Debug.Log(i);
            manifest[i].key = i;
            EditorUtility.SetDirty(manifest[i]);
        }
        for (int i = 0; i < construction.Length; i++)
        {
            //UnityEngine.Debug.Log(i);
            construction[i].key = i;
            EditorUtility.SetDirty(manifest[i]);
        }
        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets();
    }
    #endif
}

class StructureManifestBaker : Baker<StructureManifestAuthoring>
{
    public override void Bake(StructureManifestAuthoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.None);

        // AddBuffer creates and returns the buffer - no need for AddComponent
        var buffer = AddBuffer<StructureManifest>(entity);
        var constructionBuffer = AddBuffer<ConstructionDataManifest>(entity);
        //AddA(entity, new ManagedConstructionDataManifest {data = authoring.construction});
        foreach (var i in authoring.manifest)
        {
            if (i != null)
            {
                var prefabEntity = GetEntity(i.prefab, TransformUsageFlags.Dynamic);
                buffer.Add(new StructureManifest { Value = prefabEntity });
            }
        }
        foreach (var i in authoring.construction)
        {
            if (i != null)
            {
                // public ConstructionMode Mode;
                // public float Spacing;
                // public float3 Size;
                // public int PrimaryKey;
                // public int SecondaryKey;
                int sKey = -1;
                if (i.secondary != null)
                {
                    sKey = i.secondary.key;
                }
                constructionBuffer.Add(new ConstructionDataManifest
                {
                    //bake data for runtime
                    Mode = i.mode,
                    Spacing = i.spacing,
                    Size = i.size,
                    PrimaryKey = i.primary.key,
                    
                    SecondaryKey = sKey,
                });
            }
        }
    }
}

// Buffer element should be simple - just hold one entity
[InternalBufferCapacity(8)] // Number of elements before it allocates to heap
public struct StructureManifest : IBufferElementData
{
    public Entity Value;
}
