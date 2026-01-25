using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class UnitAuthoring : MonoBehaviour
{
    public int selectionKey = 0;
    public int hp = 10;
    public int dmg = 10;
    public float speed = 10f;
    public float range = 10f;
    public int teamID = 0;
    public float attackRange = 2f;
    public float attackRate = 0.5f;
    public float radius = 0.5f;
    public bool disableChildren = true;

    //public float3 dir;
}
class UnitBaker : Baker<UnitAuthoring>
{
    public override void Bake(UnitAuthoring authoring)
    {
        var entity = GetEntity(authoring, TransformUsageFlags.Dynamic);

        var unitMovement = new UnitMovement
        {
            // The math class is from the Unity.Mathematics namespace.
            // Unity.Mathematics is optimized for Burst-compiled code.
            MaxSpeed = authoring.speed,
            Radius = authoring.radius,
            Dest = authoring.transform.position,
        };
        var unitHp = new UnitHP
        {
            HP = authoring.hp,
        };
        var unitTeam = new UnitTeam
        {
            TeamID = authoring.teamID,
            UnitID = -1,
        };
        var unitTarget = new UnitTarget
        {
            Range = authoring.range,
            Targ = Entity.Null,
            Bucket = 0,
        };
        AddComponent(entity, new UnitTag { });
        AddComponent(entity, new UnitState { State = UnitStates.Idle });
        //AddComponent(entity, new UnitOrder { Order = OrderType.None });
        AddComponent(entity, unitHp);
        AddComponent(entity, unitMovement);
        AddComponent(entity, unitTeam);
        AddComponent(entity, unitTarget);
        AddComponent(entity, new UnitAttack
        {
            Dmg = authoring.dmg,
            RangeSq = authoring.attackRange * authoring.attackRange,
            Last = 0,
            Rate = authoring.attackRate
        });
        AddComponent(entity, new Pather
        {
            Dest = authoring.transform.position,
            IndexDistance = 0.5f,
            NeedsUpdate = false,
            WaypointIndex = -1,
            PathCalculated = false,
            QuerySet = false,
            //pathPositions = new NativeList<int2>(Allocator.Persistent)
            //waypoints = authoring.waypoints
            //prefab = GetEntity(authoring.prefab, TransformUsageFlags.Dynamic),
            //spawnPos = authoring.transform.position,
            //nextSpawnTime = 0.0f,
            //spawnRate = authoring.spawnRate
            //pathBuffer = new DynamicBuffer<PathPosition>(),
        });
        AddComponent(entity, new SelectionKey {Value = authoring.selectionKey });
        AddComponent<PatherWayPoint>(entity);
        AddComponent<UnitInitFlag>(entity);
        AddComponent(entity, new Vision { Level = math.round(authoring.range) });
        AddComponent(entity, new LocalVisibility { IsVisible = false, DisableChildren = authoring.disableChildren });
    }
}
public struct UnitInitFlag : IComponentData { }
public struct UnitAttack : IComponentData
{
    public float RangeSq;
    public int Dmg;
    public float Rate;
    public float Last;
}

public struct UnitMovement : IComponentData
{
    public float2 Velocity;
    public float2 PreferredVelocity;
    public float MaxSpeed;
    public float Radius;
    public float3 Dest;
}
public struct UnitTeam : IComponentData
{
    public int TeamID;
    public int UnitID;
}

public struct UnitState : IComponentData
{
    public UnitStates State;
}
public enum UnitStates
{
    Idle,
    Move,
    Chase,
    Attack,
}
public struct Vision : IComponentData
{
    public float Level;
}
public struct UnitTag : IComponentData
{

}
public struct UnitSelecetedTag : IComponentData
{
}
public struct UnitTarget : IComponentData
{
    public float Range;
    public Entity Targ;
    public float DistSq;
    //in one of 16 buckets
    public int Bucket;
}
public struct UnitHP : IComponentData
{
    public int HP;
}
// orders very sigma
public struct UnitMoveOrder : IComponentData
{
    public float3 Dest;
}
public struct UnitAttackMoveOrder : IComponentData
{
    public float3 Dest;
}