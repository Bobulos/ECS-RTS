using Unity.Entities;
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
public struct GameStats : IComponentData
{
    public int Killed;
}