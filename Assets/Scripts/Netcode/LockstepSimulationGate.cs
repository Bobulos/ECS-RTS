using Unity.Entities;
using Unity.NetCode;
using Unity.Collections;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public partial class LockstepSimulationGate : SystemBase
{
    bool _logging = false;
    protected override void OnCreate()
    {
        //_logging = NetworkConfigLoader.LoadNetwork().logNetwork;
        //RequireForUpdate<NetworkStreamInGame>();
        EntityManager.CreateSingleton<CurrentTurnInput>(new CurrentTurnInput { Ready = false });
    }
    protected override void OnUpdate()
    {
        // Always reset ready at start of frame
        foreach (var playerInput in SystemAPI.Query<RefRW<CurrentTurnInput>>())
            playerInput.ValueRW.Ready = false;

        var ecb = new EntityCommandBuffer(Allocator.Temp);
        bool turnReady = false;
        TurnReadyRpc turn = default;

        foreach (var (rpc, entity) in
            SystemAPI.Query<RefRO<TurnReadyRpc>>()
            .WithAll<ReceiveRpcCommandRequest>()
            .WithEntityAccess())
        {
            turnReady = true;
            turn = rpc.ValueRO;
            ecb.DestroyEntity(entity);
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();


        //---------------------------------
        //MISSION CRITICAL:
        //---------------------------------
        if (!turnReady) return;

        foreach (var playerInput in SystemAPI.Query<RefRW<CurrentTurnInput>>())
        {
            if (_logging) UnityEngine.Debug.Log($"<color=cyan>[Client] Received turn back {turn.TurnNumber} with input types {PackerUtil.Unpack(turn.Input0).Type} and {PackerUtil.Unpack(turn.Input1).Type}</color>");
            playerInput.ValueRW.Input0 = PackerUtil.Unpack(turn.Input0);
            playerInput.ValueRW.Input1 = PackerUtil.Unpack(turn.Input1);
            playerInput.ValueRW.Ready = true;
        }
    }
}
// Singleton to hold current turn's inputs for simulation systems to read
public struct CurrentTurnInput : IComponentData
{
    public BittableInput Input0;
    public BittableInput Input1;
    public bool Ready;
}