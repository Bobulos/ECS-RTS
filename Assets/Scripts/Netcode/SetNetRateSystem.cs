// using Unity.NetCode;
// using Unity.Entities;

// [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
// public partial struct SetNetRateSystem : ISystem
// {
//     public void OnCreate(ref SystemState state)
//     {
//         // Require the component to exist
//         state.RequireForUpdate<ClientServerTickRate>();
//     }

//     public void OnUpdate(ref SystemState state)
//     {
//         var tickRate = SystemAPI.GetSingletonRW<ClientServerTickRate>();
        
//         tickRate.ValueRW.NetworkTickRate = 120;
        
//         tickRate.ValueRW.SimulationTickRate = 60;
        
//         // Ensure this system only runs once
//         state.Enabled = false;
//     }
// }
