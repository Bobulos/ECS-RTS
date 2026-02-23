// using Unity.Entities;
// using Unity.NetCode;
// using Unity.Scenes;

// [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
// public partial struct ServerLoadGameSceneSystem : ISystem
// {
//     public void OnCreate(ref SystemState state)
//     {
//         state.RequireForUpdate<NetworkId>();
//     }

//     public void OnUpdate(ref SystemState state)
//     {
//         var sceneEntity = SystemAPI.GetSingleton<GameSceneReference>();
//         SceneSystem.LoadSceneAsync(state.WorldUnmanaged, sceneEntity.SceneGUID);

//         state.Enabled = false;
//     }
// }