using Unity.Entities;
using UnityEngine;

public class PlayerAuthoring : MonoBehaviour
{
    class Baker : Baker<PlayerAuthoring>
    {
        public override void Bake(PlayerAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new PlayerTag());
            AddComponent(entity, new Simulate());
            AddComponent(entity, new UnConsumedPlayerTag());
        }
    }
}
public struct UnConsumedPlayerTag : IComponentData { }
public struct PlayerTag : IComponentData { }