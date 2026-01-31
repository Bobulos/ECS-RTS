using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
//[UpdateAfter(typeof(UnitDeadTagSystem))]
partial struct TurretLookSystem : ISystem
{
    private ComponentLookup<UnitTarget> _targetLookup;
    private ComponentLookup<LocalToWorld> _transformLookup;
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _targetLookup = SystemAPI.GetComponentLookup<UnitTarget>(true);
        _transformLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        _targetLookup.Update(ref state);
        _transformLookup.Update(ref state);

        //EntityStorageInfoLookup entityInfo = state.GetEntityStorageInfoLookup();
        var job = new LookTurretJob
        {
            Dt = SystemAPI.Time.DeltaTime,
            TargetLookup = _targetLookup,
            LocalToWorldLookup = _transformLookup
        };
        job.ScheduleParallel();
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {

    }
}
[BurstCompile]
public partial struct LookTurretJob : IJobEntity
{
    //dont write to this one
    public float Dt;
    [ReadOnly] public ComponentLookup<UnitTarget> TargetLookup;
    [ReadOnly] public ComponentLookup<LocalToWorld> LocalToWorldLookup;
    [BurstCompile]
    public void Execute(RefRO<TurretLook> l, ref LocalTransform transform, 
        RefRO<LocalToWorld> world, RefRO<Parent> parentEntity)
    {
        if (!TargetLookup.HasComponent(parentEntity.ValueRO.Value)) return;

        var t = TargetLookup.GetRefRO(parentEntity.ValueRO.Value);

        if (t.ValueRO.Targ == Entity.Null) return;


        float3 targetPos = t.ValueRO.TargetPos;
        float3 turretPos = world.ValueRO.Position;

        // Direction to target in world space
        float3 dir = targetPos - turretPos;
        dir.y = 0f;
        if (math.lengthsq(dir) < 0.0001f)
            return;
        dir = math.normalize(dir);

        // Desired rotation in world space
        quaternion targetRotWorld = quaternion.LookRotationSafe(dir, math.up());

        // Get parent's world transform
        RefRO<LocalToWorld> parentWorld = LocalToWorldLookup.GetRefRO(parentEntity.ValueRO.Value);

        // Convert target rotation from world space to local space
        quaternion parentRotInverse = math.inverse(parentWorld.ValueRO.Rotation);
        quaternion targetRotLocal = math.mul(parentRotInverse, targetRotWorld);

        // Smooth rotation in local space
        transform.Rotation = math.slerp(
            transform.Rotation,
            targetRotLocal,
            l.ValueRO.Speed * Dt
        );
    }
}
