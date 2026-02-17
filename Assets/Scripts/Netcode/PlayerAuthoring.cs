// On your player prefab, add this authoring component:
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

public class PlayerAuthoring : MonoBehaviour
{
    class Baker : Baker<PlayerAuthoring>
    {
        public override void Bake(PlayerAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            
            //probably this is no longer neccesary
            //AddComponent<GhostOwner>(entity);
            AddComponent<Simulate>(entity);
            AddComponent<CommandSource>(entity);
            AddComponent<CommandTarget>(entity);
            //AddBuffer<RtsCommand>(entity);
        }
    }
}