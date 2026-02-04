using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup)), UpdateAfter(typeof(UnitMovement)), BurstCompile]
public partial class ProductionStructureHandler : SystemBase
{
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
    public void AddUnitToQueue(UnitAction action, int team)
    {
        if (action.ActionType != ActionType.AddUnitToQueue) return;

        if (!SystemAPI.TryGetSingleton<LocalSelectedUnits>(out var selectedUnits)) return;

        //this is the first index of the selected units
        int targetKey = selectedUnits.Buckets[0].Key;

        float time = (float)SystemAPI.Time.ElapsedTime;

        foreach (var (transform, t, key, prod) in SystemAPI.Query<
            RefRO<LocalTransform>,
            RefRO<Team>,
            RefRO<SelectionKey>,
            RefRW<ProductionStructure>>().WithAll<UnitSelecetedTag>())
        {
            //check that it is the type that needs to be modified
            if (key.ValueRO.Value != targetKey) continue;
            if (prod.ValueRO.QueueCount <= prod.ValueRO.QueueSize)

            prod.ValueRW.QueueCount++;
            prod.ValueRW.Queue.Add(prod.ValueRO.Prefabs[action.ActionIndex]);

            //if it is the first in list need to start cycle
            if (prod.ValueRO.QueueCount == 1) prod.ValueRW.StartTime = time;
        }
    }
}
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup)), UpdateAfter(typeof(UnitMovement)), BurstCompile]
public partial struct ProductionStructureSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingletonBuffer<UnitManifest>(out var manifest)) return;

        var ecb = new EntityCommandBuffer(Allocator.Temp);
        float time = (float)SystemAPI.Time.ElapsedTime;

        foreach (var (transform, team, prod) in SystemAPI.Query<
            RefRO<LocalTransform>,
            RefRO<Team>,
            RefRW<ProductionStructure>>())
        {
            if (prod.ValueRO.QueueCount <= 0) continue;
            if (time - prod.ValueRO.StartTime >= manifest[prod.ValueRO.Queue[0]].TrainingTime)
            {
                //Get que unit index
                var e = ecb.Instantiate(manifest[prod.ValueRO.Queue[0]].Unit);

                prod.ValueRW.Queue.RemoveAt(0);
                prod.ValueRW.QueueCount--;
                prod.ValueRW.StartTime = time;

                ecb.SetComponent(e, new Team { TeamID = team.ValueRO.TeamID });
                ecb.SetComponent(e, new LocalTransform
                {
                    Position = transform.ValueRO.Position + prod.ValueRO.SpawnOffset,
                    Rotation = quaternion.identity,
                    Scale = 1f
                });
                ecb.AddComponent(e, new UnitMoveOrder 
                { Dest = transform.ValueRO.Position + 5 * prod.ValueRO.SpawnOffset });
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}