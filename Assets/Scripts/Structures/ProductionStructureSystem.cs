using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

[GenerateTestsForBurstCompatibility]
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
public partial class ProductionStructureSystem : SystemBase
{
    const int MAX_QUEUE_SIZE = 8;
    //private int _count;
    protected override void OnCreate()
    {
        //_count = 100;
        UnitActionManager.OnAction += AddUnitToQueue;
    }
    protected override void OnDestroy() 
    {
        UnitActionManager.OnAction -= AddUnitToQueue;
    }
    protected override void OnUpdate()
    {
    }
    public void AddUnitToQueue(InputRecord record)
    {
        if (record.Action.ActionType != ActionType.AddUnitToQueue) return;
        
        if (!SystemAPI.TryGetSingleton<LocalSelectedUnits>(out var selectedUnits)) return;

        if (!SystemAPI.TryGetSingletonBuffer<UnitManifest>(out var manifest)) return;

        //this is the first index of the selected units
        int targetKey = selectedUnits.Buckets[0].Key;

        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (transform, team, key, prod) in SystemAPI.Query<
            RefRO<LocalTransform>, 
            RefRO<Team>,
            RefRO<SelectionKey>, 
            RefRW<ProductionStructure>>().WithAll<UnitSelecetedTag>())
        {
            //check that it is the type that needs to be modified
            if (key.ValueRO.Value != targetKey) continue;


            //Get que unit index
            var e = ecb.Instantiate(manifest[prod.ValueRO.Prefabs[0]].Value);
            //optimize this later
            /*prod.ValueRW.Queue.RemoveAt(0);
            prod.ValueRW.QueueCount--;*/
            ecb.SetComponent(e, new Team { TeamID = team.ValueRO.TeamID });
            ecb.SetComponent(e, new LocalTransform
            {
                Position = transform.ValueRO.Position + prod.ValueRO.SpawnOffset,
                Rotation = quaternion.identity,
                Scale = 1f
            });
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
}
