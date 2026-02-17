using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
partial struct GoInGameServerSystem : ISystem
{
    //private ComponentLookup<NetworkId> _networkIdLookup;
    
    public void OnCreate(ref SystemState state)
    {
        //_networkIdLookup = state.GetComponentLookup<NetworkId>(true);
        state.RequireForUpdate<PlayerSpawner>(); // Assumes you have a player spawner singleton
    }

    //[BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        
        //_networkIdLookup.Update(ref state);
        
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        var playerPrefab = SystemAPI.GetSingleton<PlayerSpawner>().PlayerPrefab;

        //UnityEngine.Debug.Log($"PlayerPrefab: {playerPrefab}");
        
        foreach (var (rpc, entity) in 
            SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>>()
            .WithAll<GoInGameRequestRpc>()
            .WithEntityAccess())
        {
            UnityEngine.Debug.Log($"Client go in game request received, player spawned added input components, and linked to connection");
            var connectionEntity = rpc.ValueRO.SourceConnection;
            

            /// Mark connection in-game
            ecb.AddComponent(connectionEntity, new NetworkStreamInGame());

            // Spawn the player ghost
            var playerEntity = ecb.Instantiate(playerPrefab);

            // Assign ghost ownership
            var networkId = SystemAPI.GetComponent<NetworkId>(rpc.ValueRO.SourceConnection);
            ecb.AddComponent(playerEntity, new GhostOwner { NetworkId = networkId.Value  });

            // Set command target on connection
            //ecb.SetComponent(connectionEntity, new CommandTarget { Value = playerEntity });

            // Clean up the RPC request entity
            ecb.DestroyEntity(entity);
            
        }
        
        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
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
