using Unity.Burst;
using Unity.Physics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup)), UpdateAfter(typeof(UnitMovement)), BurstCompile]
public partial class ProductionStructureHandler : SystemBase
{
    const float MAX_RAY_LENGTH = 100f;
    //private int _count;
    protected override void OnCreate()
    {
        //_count = 100;
        UnitActionManager.OnAction += OnAction;
    }
    protected override void OnDestroy()
    {
        UnitActionManager.OnAction -= OnAction;
    }
    protected override void OnUpdate()
    {
    }
    private void SetRallyPoint(ActionData action, int team)
    {
        if (!SystemAPI.TryGetSingleton<PhysicsWorldSingleton>(out var world)) return;
        if (!SystemAPI.TryGetSingleton<LocalSelectedUnits>(out var selectedUnits)) return;


        //Get point hit
        var raycastInput = new RaycastInput
        {
            Start = action.RayOrigin,
            End = action.RayOrigin + action.RayDirection * MAX_RAY_LENGTH,
            Filter = CollisionFilter.Default // Or a custom filter
        };
        if (!world.CastRay(raycastInput, out var hit)) return;

        //UnityEngine.Debug.Log("GIOGIKGO");
        //this is the first index of the selected units
        int targetKey = selectedUnits.Buckets[0].Key;

        float time = (float)SystemAPI.Time.ElapsedTime;

        foreach (var (key, prod) in SystemAPI.Query<
            RefRO<SelectionKey>,
            RefRW<ProductionStructure>>().WithAll<UnitSelecetedTag>())
        {
            //check that it is the type that needs to be modified
            if (key.ValueRO.Value != targetKey) continue;
            
            prod.ValueRW.RallyPoint = hit.Position;
            //UnityEngine.Debug.Log($"Set rally point to{hit.Position}");
            // Set the structures rally point
        }
    }
    private void AddUnitToQueue(ActionData action, int team)
    {
        if (!SystemAPI.TryGetSingleton<LocalSelectedUnits>(out var selectedUnits)) return;

        // This is the first index of the selected units
        int targetKey = selectedUnits.Buckets[0].Key;

        float time = (float)SystemAPI.Time.ElapsedTime;

        foreach (var (key, prod) in SystemAPI.Query<
            RefRO<SelectionKey>,
            RefRW<ProductionStructure>>().WithAll<UnitSelecetedTag>())
        {
            //check that it is the type that needs to be modified
            if (key.ValueRO.Value != targetKey) continue;
            if (prod.ValueRO.QueueCount <= prod.ValueRO.QueueSize)

            prod.ValueRW.QueueCount++;
            prod.ValueRW.Queue.Add(prod.ValueRO.Prefabs[action.Info.PrefabIndex]);

            //if it is the first in list need to start cycle
            if (prod.ValueRO.QueueCount == 1) prod.ValueRW.StartTime = time;
        }
    }
    private void OnAction(ActionData action, int team)
    {
        switch (action.Info.ActionType)
        {
            case  ActionType.AddUnitToQueue:
                AddUnitToQueue(action, team);
                break;
            case ActionType.SetRallyPoint:
                UnityEngine.Debug.Log("Set rally point");
                SetRallyPoint(action, team);
                break;
        }
    }
}

/// Handles performance intenseive operations and non player controlled data mutation 
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
                float3 dest;
                if (math.distancesq(prod.ValueRO.RallyPoint, float3.zero) < 0.1f) 
                //Default rally point
                {
                    dest = transform.ValueRO.Position + 5 * prod.ValueRO.SpawnOffset;
                } else
                {
                    dest = prod.ValueRO.RallyPoint;
                }
                ecb.AddComponent(e, new UnitMoveOrder 
                { Dest = prod.ValueRO.RallyPoint, });
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}