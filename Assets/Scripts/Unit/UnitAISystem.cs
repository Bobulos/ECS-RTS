using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

#region System Definition

[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup)), UpdateBefore(typeof(UnitSpatialPartitioning))]
public partial struct UnitStateSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
        var hpLookup = SystemAPI.GetComponentLookup<UnitHP>(false);
        float elapsedTime = (float)SystemAPI.Time.ElapsedTime;

        var job = new UnitStateMachineJob
        {
            EntityInfo = state.GetEntityStorageInfoLookup(),
            ElapsedTime = elapsedTime,
            HpLookup = hpLookup,
            TransformLookup = transformLookup
        };
        
        job.Schedule();
    }
}

#endregion

#region State Machine Job

[BurstCompile]
[WithNone(typeof(DeadTag))]
public partial struct UnitStateMachineJob : IJobEntity
{
    #region Constants
    
    private const float TARGET_REPATH_THRESH_SQ = 4f;
    private const float RANGE_EXIT_HYSTERESIS = 1.2f;
    private const float REPATH_DIST_SQ = 1f;
    
    #endregion

    #region Fields
    
    [ReadOnly] public EntityStorageInfoLookup EntityInfo;
    [ReadOnly] public float ElapsedTime;
    [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
    [ReadOnly] public ComponentLookup<UnitHP> HpLookup;
    
    #endregion

    #region Execute Entry Point
    
    public void Execute(
        RefRW<Pather> pather,
        RefRW<UnitState> state,
        RefRW<UnitMovement> movement,
        RefRO<UnitTarget> target,
        RefRW<UnitAttack> attack,
        RefRO<LocalTransform> transform)
    {
        var ctx = new Context
        {
            Pather = pather,
            State = state,
            Movement = movement,
            Target = target,
            Attack = attack,
            Transform = transform
        };

        switch (state.ValueRO.State)
        {
            case UnitStates.Idle:
                UpdateIdle(ref ctx);
                break;
            case UnitStates.Move:
                UpdateMove(ref ctx);
                break;
            case UnitStates.Chase:
                UpdateChase(ref ctx);
                break;
            case UnitStates.Attack:
                UpdateAttack(ref ctx);
                break;
        }
    }
    
    #endregion

    #region State Update Methods
    
    private void UpdateIdle(ref Context ctx)
    {
        // Destination changed - new order received
        if (BMath.DistXZsq(ctx.Movement.ValueRO.Dest, ctx.Pather.ValueRO.Dest) > TARGET_REPATH_THRESH_SQ)
        {
            ctx.Pather.ValueRW.Dest = ctx.Movement.ValueRO.Dest;
            ctx.Pather.ValueRW.NeedsUpdate = true;
            ctx.Pather.ValueRW.PathCalculated = false;
            ctx.State.ValueRW.State = UnitStates.Move;
            return;
        }

        // Check for valid target
        if (!TryGetTargetPosition(ctx.Target.ValueRO.Targ, out float3 targetPos))
        {
            StopMovement(ref ctx);
            return;
        }

        // Target exists → start chasing immediately
        TransitionToChase(ref ctx, targetPos);
    }

    private void UpdateMove(ref Context ctx)
    {
        // Check if destination changed
        if (BMath.DistXZsq(ctx.Movement.ValueRO.Dest, ctx.Pather.ValueRO.Dest) > TARGET_REPATH_THRESH_SQ)
        {
            ctx.Pather.ValueRW.Dest = ctx.Movement.ValueRO.Dest;
            ctx.Pather.ValueRW.NeedsUpdate = true;
            ctx.Pather.ValueRW.PathCalculated = false;
            return;
        }

        // Reached destination
        if (BMath.DistXZsq(ctx.Transform.ValueRO.Position, ctx.Pather.ValueRO.Dest) < ctx.Pather.ValueRO.IndexDistance)
        {
            StopMovement(ref ctx);
            ctx.State.ValueRW.State = UnitStates.Idle;
            return;
        }
    }

    private void UpdateChase(ref Context ctx)
    {
        var targetEntity = ctx.Target.ValueRO.Targ;

        // Target no longer valid
        if (!TryGetTargetPosition(targetEntity, out float3 targetPos))
        {
            TransitionToIdle(ref ctx);
            return;
        }

        // In attack range → hard stop and attack
        if (ctx.Target.ValueRO.DistSq <= ctx.Attack.ValueRO.RangeSq)
        {
            StopMovement(ref ctx);
            ctx.State.ValueRW.State = UnitStates.Attack;
            return;
        }

        // Update destination only if target moved enough
        SetDestination(ref ctx, targetPos);
    }

    private void UpdateAttack(ref Context ctx)
    {
        StopMovement(ref ctx);
        var targetEntity = ctx.Target.ValueRO.Targ;

        // Validate target HP
        if (!TryGetTargetHP(targetEntity, out UnitHP targetHP))
        {
            TransitionToIdle(ref ctx);
            return;
        }

        // Validate target position
        if (!TryGetTargetPosition(targetEntity, out float3 targetPos))
        {
            TransitionToIdle(ref ctx);
            return;
        }

        // Target is dead
        if (targetHP.HP <= 0)
        {
            TransitionToIdle(ref ctx);
            return;
        }

        // If target exits range (with hysteresis) → chase again
        if (ctx.Target.ValueRO.DistSq > ctx.Attack.ValueRO.RangeSq * RANGE_EXIT_HYSTERESIS)
        {
            ctx.State.ValueRW.State = UnitStates.Chase;
        }
    }
    
    #endregion

    #region State Transition Methods
    
    private void TransitionToIdle(ref Context ctx)
    {
        ctx.State.ValueRW.State = UnitStates.Idle;
        StopMovement(ref ctx);
    }

    private void TransitionToChase(ref Context ctx, float3 targetPos)
    {
        ctx.State.ValueRW.State = UnitStates.Chase;
        SetDestination(ref ctx, targetPos);
    }
    
    #endregion

    #region Movement Helper Methods
    
    private void SetDestination(ref Context ctx, float3 dest)
    {
        if (BMath.DistXZsq(ctx.Movement.ValueRW.Dest, dest) > REPATH_DIST_SQ)
        {
            ctx.Pather.ValueRW.PathCalculated = false;
            ctx.Pather.ValueRW.NeedsUpdate = true;
        }
        
        ctx.Movement.ValueRW.Dest = dest;
        ctx.Pather.ValueRW.Dest = dest;
    }

    private void StopMovement(ref Context ctx)
    {
        float3 pos = ctx.Transform.ValueRO.Position;
        ctx.Movement.ValueRW.Dest = pos;
        ctx.Pather.ValueRW.Dest = pos;
    }
    
    #endregion

    #region Validation Helper Methods
    
    private bool IsEntityValid(Entity e)
    {
        return e != Entity.Null && EntityInfo.Exists(e);
    }

    private bool TryGetTargetPosition(Entity target, out float3 pos)
    {
        pos = default;

        if (target == Entity.Null || !IsEntityValid(target))
            return false;

        if (!TransformLookup.HasComponent(target) || !HpLookup.HasComponent(target))
            return false;

        pos = TransformLookup.GetRefRO(target).ValueRO.Position;
        return true;
    }

    private bool TryGetTargetHP(Entity target, out UnitHP hp)
    {
        hp = default;

        if (!HpLookup.HasComponent(target))
            return false;

        hp = HpLookup.GetRefRO(target).ValueRO;
        return true;
    }

    private bool AttackReady(in UnitAttack atk)
    {
        return atk.Last + atk.Rate < ElapsedTime;
    }
    
    #endregion

    #region Context Struct
    
    private struct Context
    {
        public RefRW<Pather> Pather;
        public RefRW<UnitState> State;
        public RefRW<UnitMovement> Movement;
        public RefRO<UnitTarget> Target;
        public RefRW<UnitAttack> Attack;
        public RefRO<LocalTransform> Transform;
    }
    
    #endregion
}

#endregion


[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
public partial struct UnitInitSystem : ISystem
{
    private CollisionFilter COL_FILTER;
    private int _targBucket;
    private int _maxTargBucket;
    private int _navBucket;
    private int _maxNavBucket;
    public void OnCreate(ref SystemState state)
    {
        //load settings
        var config = ConfigLoader.LoadSim();
        _targBucket = 0;
        _navBucket = 0;
        _maxTargBucket = config.targetBucketCount;
        _maxNavBucket = config.navBucketCount;

        COL_FILTER = new CollisionFilter
        {
            CollidesWith = 1 << 7,
            BelongsTo = CollisionFilter.Default.BelongsTo,
            GroupIndex = 0
        };
    }
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float count = 0;
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        var phys = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;
        foreach (var (transform, mov, uState, targ, pather, entity) in
                 SystemAPI.Query<
                     RefRW<LocalTransform>,
                     RefRW<UnitMovement>,
                     RefRW<UnitState>,
                     RefRW<UnitTarget>,
                     RefRW<Pather>
                 >().WithEntityAccess()
                 .WithNone<DeadTag>().WithAll<UnitInitFlag>())
        {
            ecb.RemoveComponent<UnitInitFlag>(entity);

            float3 pos = transform.ValueRO.Position;
            float3 of = +new float3(0, 10, 0);
            targ.ValueRW.Bucket = _targBucket;
            pather.ValueRW.Bucket = _navBucket;
            count += 0.1f;
            RaycastInput r = new RaycastInput
            {
                Start = pos + of,
                End = pos - of,
                Filter = COL_FILTER
            };
            if (phys.CastRay(r, out Unity.Physics.RaycastHit hit))
            {
                // ecb.AppendToBuffer(entity, new OrderElement
                // {
                //     Type = OrderType.Move,
                //     Position = hit.Position,
                //     Data = -1
                // });
                //ecb.AddComponent(entity, new UnitMoveOrder { Dest = hit.Position });
                //UnityEngine.Debug.Log("HEYEYEYEYYE");
            }
            _navBucket += 1;
            _targBucket += 1;
            if (_navBucket > _maxNavBucket) { _navBucket = 0; }
            if (_targBucket > _maxTargBucket) { _targBucket = 0; }

        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}

