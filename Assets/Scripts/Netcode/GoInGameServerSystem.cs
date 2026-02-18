using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
partial struct GoInGameServerSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerSpawner>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        var playerPrefab = SystemAPI.GetSingleton<PlayerSpawner>().PlayerPrefab;

        foreach (var (rpc, entity) in
            SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>>()
            .WithAll<GoInGameRequestRpc>()
            .WithEntityAccess())
        {
            var connectionEntity = rpc.ValueRO.SourceConnection;
            var networkId = SystemAPI.GetComponent<NetworkId>(connectionEntity);

            UnityEngine.Debug.Log($"Client {networkId.Value} joined, spawning player");

            ecb.AddComponent(connectionEntity, new NetworkStreamInGame());
           


            var playerEntity = ecb.Instantiate(playerPrefab);
            ecb.AddComponent(playerEntity, new GhostOwner { NetworkId = networkId.Value });
            //ecb.SetComponent(connectionEntity, new CommandTarget { Value = playerEntity });


            ecb.DestroyEntity(entity);
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();

        // Count connected in-game players and update LockstepServerSystem
        int playerCount = 0;
        foreach (var _ in SystemAPI.Query<RefRO<NetworkStreamInGame>>())
            playerCount++;

        // Update expected player count dynamically
        // var lockstepSystem = state.World.GetExistingSystemManaged<LockstepServerSystem>();
        // if (lockstepSystem != null)
        //     lockstepSystem.SetExpectedPlayers(playerCount);
    }
}

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial class StartServerListenSystem : SystemBase
{
    protected override void OnCreate()
    {
        var ep = NetworkEndpoint.AnyIpv4.WithPort(7979);
        SystemAPI.GetSingletonRW<NetworkStreamDriver>().ValueRW.Listen(ep);
        UnityEngine.Debug.Log("Server listening on port 7979");
        Enabled = false;
    }

    protected override void OnUpdate() { }
}

// This component is used to identify the source of commands in a predicted system
[GhostComponent(PrefabType = GhostPrefabType.PredictedClient)]
public struct CommandSource : IComponentData {}

// This component references which entity to send commands to
public struct CommandTarget : IComponentData 
{
    public Entity Value; // The player entity to control
}



// [UpdateInGroup(typeof(SimulationSystemGroup))]
// //[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
// public partial struct ExecutePlayerCommandsSystem : ISystem
// {
//     public void OnUpdate(ref SystemState state)
//     {
//         // Current server tick
//         var tick = SystemAPI.GetSingleton<NetworkTime>().ServerTick;

//         // Query all player ghosts
//         foreach (var buffer in
//                  SystemAPI.Query<DynamicBuffer<RtsCommand>>()
//                  .WithAll<CommandSource>())
//         {
//             // Read commands for this tick
//             if (!buffer.GetDataAtTick(tick, out var cmd))
//                 continue;

//             // Apply command
//             HandleCommand(cmd);
//         }
//     }

//     private void HandleCommand(RtsCommand cmd)
//     {
//         switch (cmd.Type)
//         {
//             case InputType.MoveUnits:
//                 UnityEngine.Debug.Log($"Received MoveUnits command for team {cmd.Team} with move data: {cmd.Move}");
//                 //CommandHandlers.Move(cmd.Team, cmd.Move);
//                 break;
//             case InputType.ClearUnits:
//             UnityEngine.Debug.Log($"Received ClearUnits command for team {cmd.Team}");
//                 //CommandHandlers.Clear(cmd.Team);
//                 break;
//             case InputType.Action:
//                 UnityEngine.Debug.Log($"Received Action command for team {cmd.Team} with action data: {cmd.Action}");
//                 //CommandHandlers.Action(cmd.Team, cmd.Action);
//                 break;
//             case InputType.CodeSelectUnits:
//                 UnityEngine.Debug.Log($"Received CodeSelectUnits command for team {cmd.Team} with code: {cmd.CodeSelect}");
//                 //CommandHandlers.CodeSelect(cmd.Team, cmd.CodeSelect);
//                 break;
//             case InputType.SelectUnits:
//                 UnityEngine.Debug.Log($"Received SelectUnits command for team {cmd.Team} with selection data: {cmd.Select}");
//                 //CommandHandlers.Select(cmd.Team, cmd.Select);
//                 break;
//         }
//     }
// }
