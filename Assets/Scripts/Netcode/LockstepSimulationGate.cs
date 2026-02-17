using Unity.Entities;
using Unity.NetCode;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public partial class LockstepSimulationGate : SystemBase
{
    protected override void OnCreate()
    {
        //RequireForUpdate<NetworkStreamInGame>();
        EntityManager.CreateSingleton<CurrentTurnInput>(new CurrentTurnInput { Ready = false });
        EntityManager.CreateSingleton<LockstepReady>(new LockstepReady { Value = false });
    }
    protected override void OnUpdate()
    {
        var receiver = World.GetExistingSystemManaged<LockstepTurnReceiverSystem>();
        if (receiver == null || receiver.PendingTurns.Count == 0) 
        {
            // Stall - don't simulate this frame
            // You can disable downstream systems here
            return;
        }
        //UnityEngine.Debug.Log($"Processing turn {receiver.PendingTurns.Peek().TurnNumber}");
        var turn = receiver.PendingTurns.Dequeue();

        // Feed inputs into your InputHandlerSystem
        // Input0 = local player, Input1 = remote player (or look up by NetworkId)
        foreach (var playerInput in
            SystemAPI.Query<RefRW<CurrentTurnInput>>())
        {
            playerInput.ValueRW.Input0 = turn.Input0;
            playerInput.ValueRW.Input1 = turn.Input1;
            playerInput.ValueRW.Ready = true;
        }
    }
}
public struct LockstepReady : IComponentData
{
    public bool Value;
}
// Singleton to hold current turn's inputs for simulation systems to read
public struct CurrentTurnInput : IComponentData
{
    public BittableInput Input0;
    public BittableInput Input1;
    public bool Ready;
}