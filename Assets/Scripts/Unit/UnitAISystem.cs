using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup)), UpdateBefore(typeof(UnitSpatialPartitioning))]
public partial struct UnitStateSystem : ISystem
{
    const float STOPPING_DIST_SQ = 1f;
    const float RESUME_MOVE_DIST_SQ = 1.5f;
    const float TARGET_REPATH_THRESH_SQ = 4f; // re-path only if target moved 2m+

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
        var hpLookup = SystemAPI.GetComponentLookup<UnitHP>(false);
        var ecb = new EntityCommandBuffer(Allocator.TempJob);

        float elapsedTime = (float)SystemAPI.Time.ElapsedTime;

        /*        foreach (var (pather, uState, mov, targ, attack, transform, entity) in
                         SystemAPI.Query<RefRW<Pather>, 
                         RefRW<UnitState>, 
                         RefRW<UnitMovement>, 
                         RefRO<UnitTarget>, 
                         RefRW<UnitAttack>, 
                         RefRO<LocalTransform>>()
                         .WithEntityAccess()
                         .WithNone<DeadTag>())
                {

                }*/
        var job = new UnitStateMachineJob
        {
            Ecb = ecb,
            ElapsedTime = elapsedTime,
            HpLookup = hpLookup,
            TransformLookup = transformLookup
        };

        var handle = job.Schedule(state.Dependency);
        handle.Complete();


        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}


[BurstCompile]
[WithNone(typeof(DeadTag))]
public partial struct UnitStateMachineJob : IJobEntity
{
    private const float TARGET_REPATH_THRESH_SQ = 4f;
    private const float RANGE_EXIT_HYSTERESIS = 1.2f;

    [ReadOnly] public float ElapsedTime;
    [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
    [ReadOnly] public ComponentLookup<UnitHP> HpLookup;

    public EntityCommandBuffer Ecb;

    public void Execute(
        RefRW<Pather> pather,
        RefRW<UnitState> state,
        RefRW<UnitMovement> movement,
        RefRO<UnitTarget> target,
        RefRW<UnitAttack> attack,
        RefRW<LocalTransform> transform)
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

    // =====================================================================
    // STATES
    // =====================================================================
    private void UpdateMove(ref Context ctx)
    {
        if (BMath.DistXZsq(ctx.Transform.ValueRO.Position, ctx.Pather.ValueRO.Dest) < ctx.Pather.ValueRO.IndexDistance)
        {
            //made it to the dest got to idele
            StopMovement(ref ctx);
            ctx.State.ValueRW.State = UnitStates.Idle;
            return;
        }
    }
    private void UpdateIdle(ref Context ctx)
    {
        if (!TryGetTargetPosition(ctx.Target.ValueRO.Targ, out float3 targetPos))
        {
            StopMovement(ref ctx);
            return;
        }

        // Target exists → start chasing immediately
        TransitionToChase(ref ctx, targetPos);
    }

    private void UpdateChase(ref Context ctx)
    {
        var targetEntity = ctx.Target.ValueRO.Targ;

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
        float3 delta = targetPos - ctx.Movement.ValueRO.Dest;
        delta.y = 0f;

        if (math.lengthsq(delta) > TARGET_REPATH_THRESH_SQ)
        {
            SetDestination(ref ctx, targetPos);
        }
    }

    private void UpdateAttack(ref Context ctx)
    {
        var targetEntity = ctx.Target.ValueRO.Targ;

        if (!TryGetTargetHP(targetEntity, out UnitHP targetHP))
        {
            TransitionToIdle(ref ctx);
            return;
        }

        if (targetHP.HP <= 0)
        {
            TransitionToIdle(ref ctx);
            return;
        }

        // ATTACK STATE OWNS MOVEMENT
        StopMovement(ref ctx);

        // Execute attack
        if (AttackReady(ctx.Attack.ValueRO))
        {
            ctx.Attack.ValueRW.Last = ElapsedTime;

            Ecb.SetComponent(targetEntity, new UnitHP
            {
                HP = targetHP.HP - ctx.Attack.ValueRO.Dmg
            });
        }


        if (TryGetTargetPosition(targetEntity, out float3 pos))
        {
            float3 dir = pos - ctx.Transform.ValueRO.Position;
            dir.y = 0f;

            if (math.lengthsq(dir) > 0.0001f)
            {
                ctx.Transform.ValueRW.Rotation =
                    quaternion.LookRotationSafe(math.normalize(dir), math.up());
            }
        }

        // If target exits range (with hysteresis) → chase again
        if (ctx.Target.ValueRO.DistSq >
            ctx.Attack.ValueRO.RangeSq * RANGE_EXIT_HYSTERESIS)
        {
            ctx.State.ValueRW.State = UnitStates.Chase;
            ctx.Pather.ValueRW.PathCalculated = false;
            ctx.Pather.ValueRW.NeedsUpdate = true;
        }
    }

    // =====================================================================
    // TRANSITIONS / HELPERS
    // =====================================================================

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

    private void SetDestination(ref Context ctx, float3 dest)
    {
        ctx.Movement.ValueRW.Dest = dest;
        ctx.Pather.ValueRW.Dest = dest;
        ctx.Pather.ValueRW.PathCalculated = false;
        ctx.Pather.ValueRW.NeedsUpdate = true;
    }

    private void StopMovement(ref Context ctx)
    {
        float3 pos = ctx.Transform.ValueRO.Position;
        ctx.Movement.ValueRW.Dest = pos;
        ctx.Pather.ValueRW.Dest = pos;
    }

    private bool AttackReady(in UnitAttack atk)
    {
        return atk.Last + atk.Rate < ElapsedTime;
    }

    private bool TryGetTargetPosition(Entity target, out float3 pos)
    {
        pos = default;

        if (target == Entity.Null)
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

    // =====================================================================
    // CONTEXT
    // =====================================================================

    private struct Context
    {
        public RefRW<Pather> Pather;
        public RefRW<UnitState> State;
        public RefRW<UnitMovement> Movement;
        public RefRO<UnitTarget> Target;
        public RefRW<UnitAttack> Attack;
        public RefRW<LocalTransform> Transform;
    }
}

/// <summary>
/// This system handles incomming orders for units
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct UnitOrderSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        //move order
        foreach (var (mov, buf, moveOrder, uState, pather, entity) in
                 SystemAPI.Query<
                     RefRW<UnitMovement>,
                     DynamicBuffer<PatherWayPoint>,
                     RefRO<UnitMoveOrder>,
                     RefRW<UnitState>,
                     RefRW<Pather>
                 >().WithEntityAccess()
                 .WithNone<DeadTag>())
        {
            // Clear old waypoints
            buf.Clear();

            // Update destination info
            pather.ValueRW.Dest = moveOrder.ValueRO.Dest;
            mov.ValueRW.Dest = moveOrder.ValueRO.Dest;
            uState.ValueRW.State = UnitStates.Move;

            // Reset pathing state
            pather.ValueRW.WaypointIndex = 0;
            pather.ValueRW.NeedsUpdate = true;

            // Remove the order now that it's been handled
            ecb.RemoveComponent<UnitMoveOrder>(entity);
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
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

