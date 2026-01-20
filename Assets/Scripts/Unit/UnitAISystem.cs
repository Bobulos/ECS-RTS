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
    [ReadOnly] public float ElapsedTime;
    public EntityCommandBuffer Ecb;
    //[ReadOnly] public ComponentLookup<UnitMovement> TransformLookup;
    [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
    [ReadOnly] public ComponentLookup<UnitHP> HpLookup;
    const float STOPPING_DIST_SQ = 1f;
    const float RESUME_MOVE_DIST_SQ = 1.5f;
    const float TARGET_REPATH_THRESH_SQ = 4f;
    public void Execute(
    RefRW<Pather> pather,
    RefRW<UnitState> uState,
    RefRW<UnitMovement> mov,
    RefRO<UnitTarget> targ,
    RefRW<UnitAttack> attack,
    RefRO<LocalTransform> transform)
    {
        var currentState = uState.ValueRO.State;
        var position = new float2(transform.ValueRO.Position.x, transform.ValueRO.Position.z);
        var destination = new float2(mov.ValueRO.Dest.x, mov.ValueRO.Dest.z);
        var distanceToDestSq = math.distancesq(position, destination);

        switch (currentState)
        {
            case UnitStates.Idle:
                HandleIdleState(transform, uState, mov, pather, targ, distanceToDestSq);
                break;

            case UnitStates.Move:
                HandleMoveState(uState, distanceToDestSq);
                break;

            case UnitStates.Chase:
                HandleChaseState(pather, uState, mov, targ, attack, transform);
                break;

            case UnitStates.Attack:
                HandleAttackState(pather, uState, targ, attack, transform);
                break;
        }
    }

    private void HandleIdleState(
        RefRO<LocalTransform> transform,
        RefRW<UnitState> uState,
        RefRW<UnitMovement> mov,
        RefRW<Pather> pather,
        RefRO<UnitTarget> targ,
        float distanceToDestSq)
    {
        if (targ.ValueRO.Targ != Entity.Null && IsTargetValid(targ.ValueRO.Targ))
        {
            //Debug.Log("I GOO NOE");
            float3 pos = TransformLookup.GetRefRO(targ.ValueRO.Targ).ValueRO.Position;
            mov.ValueRW.Dest = pos;
            pather.ValueRW.Dest = pos;
            uState.ValueRW.State = UnitStates.Chase;
        }
        else if (distanceToDestSq > RESUME_MOVE_DIST_SQ)
        {
            uState.ValueRW.State = UnitStates.Move;
        }
        else
        {
            mov.ValueRW.Dest = transform.ValueRO.Position;
            /*pather.ValueRW.Dest = transform.ValueRO.Position;
            pather.ValueRW.NeedsUpdate = true;
            pather.ValueRW.PathCalculated = false;*/
        }
    }

    private void HandleMoveState(
        RefRW<UnitState> uState,
        /*RefRW<UnitMovement> mov,
        RefRO<LocalTransform> transform,*/
        float distanceToDestSq)
    {
        if (distanceToDestSq <= STOPPING_DIST_SQ)
        {
            uState.ValueRW.State = UnitStates.Idle;
        }
        /*UnityEngine.Debug.DrawLine(
            mov.ValueRO.Dest,
            transform.ValueRO.Position,
            Color.cyan,
            1f / 50f);*/

    }
    
    private void HandleChaseState(
        RefRW<Pather> pather,
        RefRW<UnitState> uState,
        RefRW<UnitMovement> mov,
        RefRO<UnitTarget> targ,
        RefRW<UnitAttack> attack,
        RefRO<LocalTransform> transform)
    {
        var target = targ.ValueRO.Targ;

        if (!IsTargetValid(target))
        {
            uState.ValueRW.State = UnitStates.Idle;
            mov.ValueRW.Dest = transform.ValueRO.Position;
            pather.ValueRW.Dest = transform.ValueRO.Position;
            return;
        }

        // Update destination to target's current position
        float3 targetPos = TransformLookup.GetRefRO(target).ValueRO.Position;

        float3 delta = targetPos - mov.ValueRO.Dest;
        delta.y = 0;

        if (math.lengthsq(delta) > TARGET_REPATH_THRESH_SQ)
        {
            mov.ValueRW.Dest = targetPos;
            pather.ValueRW.Dest = targetPos;
            pather.ValueRW.PathCalculated = false;
            pather.ValueRW.NeedsUpdate = true;
        }

        /*UnityEngine.Debug.DrawLine(
            mov.ValueRO.Dest,
            transform.ValueRO.Position,
            Color.magenta,
            1f / 50f);*/

        // Transition to attack if within range
        if (targ.ValueRO.DistSq < attack.ValueRO.RangeSq)
        {
            uState.ValueRW.State = UnitStates.Attack;
        }
    }

    private void HandleAttackState(
        RefRW<Pather> pather,
        RefRW<UnitState> uState,
        RefRO<UnitTarget> targ,
        RefRW<UnitAttack> attack,
        RefRO<LocalTransform> transform)
    {
        var target = targ.ValueRO.Targ;

        if (!IsTargetValid(target))
        {
            uState.ValueRW.State = UnitStates.Idle;
            return;
        }

        var targetHP = HpLookup.GetRefRO(target);
        if (targetHP.ValueRO.HP <= 0)
        {
            uState.ValueRW.State = UnitStates.Idle;
            return;
        }

        // Process attack on cooldown
        if (attack.ValueRO.Last + attack.ValueRO.Rate < ElapsedTime)
        {
            attack.ValueRW.Last = ElapsedTime;

            Ecb.SetComponent(target, new UnitHP
            {
                HP = targetHP.ValueRO.HP - attack.ValueRO.Dmg
            });
        }

        // Transition back to chase if target moves out of range (with hysteresis)
        const float RANGE_HYSTERESIS = 1.2f;
        if (targ.ValueRO.DistSq > attack.ValueRO.RangeSq * RANGE_HYSTERESIS)
        {
            uState.ValueRW.State = UnitStates.Chase;
            pather.ValueRW.PathCalculated = false;
        }
    }
    private bool IsTargetValid(Entity target)
    {
        if (target == Entity.Null)
            return false;

        return TransformLookup.HasComponent(target)
            && HpLookup.HasComponent(target);
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
    private const int MAX_PER_BUCKET = 100;
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
        int addedCount = 0;
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
            if (addedCount > MAX_PER_BUCKET) break;
            addedCount++;
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

