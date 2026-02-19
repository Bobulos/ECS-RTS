using Unity.Entities;
using UnityEngine;

public class PlayerSpawnerAuthoring : MonoBehaviour
{
    public GameObject PlayerPrefab;

    class Baker : Baker<PlayerSpawnerAuthoring>
    {
        public override void Bake(PlayerSpawnerAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new PlayerSpawner
            {
                PlayerPrefab = GetEntity(authoring.PlayerPrefab, TransformUsageFlags.None)
            });
        }
    }
}

public struct PlayerSpawner : IComponentData
{
    public Entity PlayerPrefab;
}