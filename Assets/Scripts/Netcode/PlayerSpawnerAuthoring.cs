using Unity.Entities;
using Unity.VisualScripting;
using UnityEngine;

class PlayerSpawnerAuthoring : MonoBehaviour
{
    public GameObject playerPrefab;
}

class PlayerSpawnerAuthoringBaker : Baker<PlayerSpawnerAuthoring>
{
    public override void Bake(PlayerSpawnerAuthoring authoring)
    {
        var e =  GetEntity(authoring, TransformUsageFlags.None);
        AddComponent(e, new PlayerSpawner { PlayerPrefab = GetEntity(authoring.playerPrefab, TransformUsageFlags.Dynamic) });
    }
}
public struct PlayerSpawner : IComponentData
{
    public Entity PlayerPrefab;
}