using Unity.Entities;
using UnityEngine;

class LinkedEntityAuthoring : MonoBehaviour
{
    
}

class LinkedEntityBaker : Baker<LinkedEntityAuthoring>
{
    public override void Bake(LinkedEntityAuthoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.Dynamic);

        // Create the LinkedEntityGroup buffer
        var linkedEntities = AddBuffer<LinkedEntityGroup>(entity);

        // Add self first (convention)
        linkedEntities.Add(new LinkedEntityGroup { Value = entity });

        // Manually add all children
        foreach (Transform child in authoring.transform)
        {
            var childEntity = GetEntity(child.gameObject, TransformUsageFlags.Dynamic);
            linkedEntities.Add(new LinkedEntityGroup { Value = childEntity });
        }

        //Grandchildren
        foreach (Transform child in authoring.transform)
        {
            AddChildrenRecursive(child, linkedEntities);
        }
    }

    void AddChildrenRecursive(Transform parent, DynamicBuffer<LinkedEntityGroup> buffer)
    {
        foreach (Transform child in parent)
        {
            var childEntity = GetEntity(child.gameObject, TransformUsageFlags.Dynamic);
            buffer.Add(new LinkedEntityGroup { Value = childEntity });
            AddChildrenRecursive(child, buffer);
        }
    }
}