using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

class GameStatsAuthoring : MonoBehaviour
{
    
}

class GameStatsAuthoringBaker : Baker<GameStatsAuthoring>
{
    public override void Bake(GameStatsAuthoring authoring)
    {
        GetEntity(authoring, TransformUsageFlags.None);
        AddComponent(new GameStats { Killed = 0 });
    }
}
[GhostComponent]
public struct GameStats : IComponentData
{
    [GhostField] public int Killed;
}