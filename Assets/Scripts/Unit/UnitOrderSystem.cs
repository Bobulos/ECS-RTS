using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
/// <summary>
/// This system handles incomming orders for units
/// </summary>
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(SimulationSystemGroup)),UpdateAfter(typeof(NavSystem))]
public partial struct UnitOrderSystem : ISystem
{
    private ComponentLookup<Worker> _workerLookup;
    public void OnCreate(ref SystemState state)
    {
        _workerLookup = SystemAPI.GetComponentLookup<Worker>(true);
    }
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        _workerLookup.Update(ref state);
        var ecbSys = SystemAPI.GetSingleton<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>();
        var OrderJob = new OrderJob
        {
            Ecb = ecbSys.CreateCommandBuffer(state.WorldUnmanaged),
            WorkerLookup = _workerLookup,
            
            //Orders = SystemAPI.GetBufferLookup<OrderElement>(),
        };
        OrderJob.Schedule();
    }
}
#region  Main Job
[BurstCompile]
public partial struct OrderJob : IJobEntity
{
    //prob for build
    const float ARRIVE_DIST_SQ = 2f;
    public EntityCommandBuffer Ecb;
    //public Entity ConstructionRequestEntity
    [ReadOnly] public ComponentLookup<Worker> WorkerLookup;
    [BurstCompile]
    public void Execute(Entity entity, 
        RefRO<LocalTransform> transform, 
        RefRO<Team> team,
        //RefRO<Worker> worker, 
        RefRW<UnitMovement> mov,
        //RefRW<Pather> pather,
        RefRW<OrderList> orders)
    {
        //UnityEngine.Debug.Log($"length of {orders.ValueRO.Value.Length}");
        //UnityEngine.Debug.Log("Did a move order");
        if (orders.ValueRO.Value.Length <= 0) return;
        //UnityEngine.Debug.Log("Did a move order");

        float3 position = transform.ValueRO.Position;
        switch (orders.ValueRO.Value[0].Type)
        {
            case OrderType.Move:
                HandleMove(orders, position, mov);
                break;
            case OrderType.BuildStructure:
                HandleBuildStructure(entity, orders, position, team.ValueRO.TeamID, mov);
                break;
        }
    }
    #endregion
    #region Move Order
    [BurstCompile]
    private void HandleMove(
        RefRW<OrderList> orders,
        float3 pos,
        RefRW<UnitMovement> mov)
    {
        var curDest = orders.ValueRO.Value[0].Position;
        // dist from me to current dest
        //UnityEngine.Debug.Log("Go go go");
        float distSq = BMath.DistXZsq(pos, curDest);

        mov.ValueRW.Dest = curDest;
        // if close enough to the dest then index to next order
        if (distSq <= ARRIVE_DIST_SQ)
        {
            //UnityEngine.Debug.Log("Index order");
            orders.ValueRW.Value.RemoveAt(0);
            
        }
    }
    #region  BuildOrder
    [BurstCompile]
    private void HandleBuildStructure(
        Entity entity,
        RefRW<OrderList> orders,
        float3 pos,
        int teamID,
        RefRW<UnitMovement> mov)
    {
        if (WorkerLookup.TryGetRefRO(entity, out RefRO<Worker> worker))
        {
            var curOrder = orders.ValueRO.Value[0];
            var curDest = curOrder.Position;
            // dist from me to current dest
            //UnityEngine.Debug.Log("Go go go");
            float distSq = BMath.DistXZsq(pos, curDest);

            mov.ValueRW.Dest = curDest;
            // if close enough to the dest then index to next order
            if (distSq <= ARRIVE_DIST_SQ)
            {
                //UnityEngine.Debug.Log("Build go");
                // Good
                orders.ValueRW.Value.RemoveAt(0);

                // create build request
                var req = Ecb.CreateEntity();
                Ecb.AddComponent(req, new ConstructionRequest
                {
                    Position = curOrder.Position,
                    Key = worker.ValueRO.ConstructKeys[curOrder.Data],
                    TeamID = teamID
                });   
            }
        }
    }
    #endregion
}

[GhostComponent]
public struct OrderList : IComponentData
{
    [GhostField] public FixedList512Bytes<OrderElement> Value;
}
// Prob dont use for instant 
// actions like set rally point
// 8 before allocate to the heap
//17 bytes
public struct OrderElement
{

    public OrderType Type;
    public float3 Position;

    //used for stuf like a build key
    public int Data;
}
public enum OrderType : byte
{
    Move,
    BuildStructure,
}
#endregion