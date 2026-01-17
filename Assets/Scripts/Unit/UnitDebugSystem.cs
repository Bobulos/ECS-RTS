using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
public partial struct UnitDebugSystem : ISystem
{
    private double last;
    private double rate;

    private EntityQuery _query;
    public void OnCreate(ref SystemState state)
    {
        rate = 2;
        last = SystemAPI.Time.ElapsedTime;
        _query = state.GetEntityQuery(
            ComponentType.ReadOnly<LocalTransform>(),
            ComponentType.ReadWrite<UnitTarget>(),
            ComponentType.ReadOnly<UnitTeam>()
        );

    }
    public void OnUpdate(ref SystemState state)
    {
        double et = SystemAPI.Time.ElapsedTime;
        if (last + rate < et)
        {
            NativeArray<Entity> arr = _query.ToEntityArray(Allocator.Temp);
            Debug.Log("Num units" + arr.Count());
            last = et;
        }

    }
}
