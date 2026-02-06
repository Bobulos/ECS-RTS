using UnityEditor;
using System.Linq;
using Unity.Entities;
using UnityEngine;

public class StructureManifestAuthoring : MonoBehaviour
{
    [SerializeField]
    public EntityData[] manifest;

    #if UNITY_EDITOR
    [ContextMenu("Update manifest")]
    public void UpdateManifest()
    {
        var data = ScriptableObjectUtil.LoadAllScriptableObjects<EntityData>();
        
        manifest = data
            .Where(e => e.entityType == EntityType.Structure)
            .OrderBy(e => e.entityGuid)
            .ToArray();
        
        // Update each entity's key to match array index
        for (int i = 0; i < manifest.Length; i++)
        {
            //UnityEngine.Debug.Log(i);
            manifest[i].key = i;
            EditorUtility.SetDirty(manifest[i]);
        }
        
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

        foreach (var i in authoring.manifest)
        {
            if (i != null)
            {
                var prefabEntity = GetEntity(i.prefab, TransformUsageFlags.Dynamic);
                buffer.Add(new StructureManifest { Value = prefabEntity });
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