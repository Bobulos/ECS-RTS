using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation), UpdateAfter(typeof(InputHandlerSystem))]
[BurstCompile]
public partial class SelectionVisualSystem : SystemBase
{
    private EntityQuery _selectedUnitsQuery;
    private EntityQuery _visualsQuery;

    protected override void OnCreate()
    {
        _selectedUnitsQuery = SystemAPI.QueryBuilder()
            .WithAll<Selected>()
            .Build();
            
        _visualsQuery = SystemAPI.QueryBuilder()
            .WithAll<SelectedVisualTag, Parent>()
            .Build();
    }

    protected override void OnUpdate()
    {
        if (!SystemAPI.TryGetSingleton<AssetSingleton>(out var assetSingleton)) 
            return;

        var ecb = new EntityCommandBuffer(Allocator.TempJob);

        // Handle adding visuals for newly selected units
        foreach (var (selected, entity) in 
                 SystemAPI.Query<RefRO<Selected>>()
                 .WithEntityAccess()
                 .WithNone<Child>())
        {
            if (selected.ValueRO.Value)
            {
                AddVisual(ref ecb, entity, assetSingleton);
            }
        }

        // Handle adding visuals for units that already have children but need visual
        foreach (var (selected, children, entity) in 
                 SystemAPI.Query<RefRO<Selected>, DynamicBuffer<Child>>()
                 .WithEntityAccess())
        {
            if (selected.ValueRO.Value)
            {
                bool hasVisual = false;
                foreach (var child in children)
                {
                    if (EntityManager.HasComponent<SelectedVisualTag>(child.Value))
                    {
                        hasVisual = true;
                        break;
                    }
                }

                if (!hasVisual)
                {
                    AddVisual(ref ecb, entity, assetSingleton);
                }
            }
        }

        // Handle removing visuals for deselected units
        var parentLookup = SystemAPI.GetComponentLookup<Parent>(true);
        var selectedLookup = SystemAPI.GetComponentLookup<Selected>(true);

        foreach (var (parent, entity) in 
                 SystemAPI.Query<RefRO<Parent>>()
                 .WithAll<SelectedVisualTag>()
                 .WithEntityAccess())
        {
            Entity parentEntity = parent.ValueRO.Value;
            
            if (selectedLookup.HasComponent(parentEntity))
            {
                if (!selectedLookup[parentEntity].Value)
                {
                    ecb.DestroyEntity(entity);
                }
            }
            else
            {
                // Parent doesn't have Selected component, destroy visual
                ecb.DestroyEntity(entity);
            }
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }

    [BurstCompile]
    private void AddVisual(ref EntityCommandBuffer ecb, Entity unit, AssetSingleton assetSingleton)
    {
        if (!EntityManager.HasBuffer<Child>(unit))
        {
            ecb.AddBuffer<Child>(unit);
        }

        var visual = ecb.Instantiate(assetSingleton.SelectedVisual);

        ecb.AddComponent(visual, new Parent { Value = unit });
        
        // Determine scale based on whether it's a structure or unit
        float scale = EntityManager.HasComponent<StructureTag>(unit) ? 2.3f : 1f;
        
        ecb.SetComponent(visual, new LocalTransform
        {
            Position = new float3(0, 0, 0),
            Rotation = quaternion.identity,
            Scale = scale
        });
        
        ecb.AddComponent<SelectedVisualTag>(visual);
    }
}