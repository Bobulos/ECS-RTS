using Unity.Entities;
[UpdateBefore(typeof(UnitSpatialPartitioning))]
public partial struct UnitIDSystem : ISystem
{
    private int ID;
    private void OnCreate(ref SystemState state)
    {
        ID = 0;
    }
    private void OnUpdate(ref SystemState state)
    {
        foreach (var team  in SystemAPI.Query<RefRW<UnitTeam>>())
        {
            if (team.ValueRO.UnitID == -1)
            {
                team.ValueRW.UnitID = ID;
                ID++;
            }
        }
    }
}
