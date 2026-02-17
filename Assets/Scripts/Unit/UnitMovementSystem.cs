using TMPro;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
//[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
[BurstCompile, UpdateInGroup(typeof(FixedStepSimulationSystemGroup)), UpdateAfter(typeof(UnitSpatialPartitioning))]
public partial struct UnitMovementSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var waypointLookup = SystemAPI.GetBufferLookup<PatherWayPoint>(isReadOnly: true);
        var movementJob = new MovementJob
        {
            WaypointLookup = waypointLookup,
            World = SystemAPI.GetSingleton<PhysicsWorldSingleton>(),
            Filter = new CollisionFilter
            {
                CollidesWith = 1 << 7,
                BelongsTo = CollisionFilter.Default.BelongsTo,
                GroupIndex = 0
            }

        };

        state.Dependency = movementJob.ScheduleParallel(state.Dependency);
    }
}

[WithNone(typeof(DeadTag))]
[BurstCompile]
public partial struct MovementJob : IJobEntity
{
    //private const float CLUSTER_WAYPOINT_INDEX_DIST = 0.9f;
    private const float FIXED_DT = 1f / 50f;
    //private const float MIN_VELOCITY_SQ = 1e-6f;
    private const float MIN_DIRECTION_LENGTH = 1e-4f;
    private const float MIN_ARRIVE_DISTANCE_SQ = 0.5f;
    private const float GROUND_RAYCAST_OFFSET = 10f;
    private const float SLERP_SPEED = 4f;
    private const float DEBUG_LINE_LENGTH = 1f;
    private const float INDEX_DISTANCE_SQ = 1f;
    private const float LANE_STRENGTH = 1.5f;
    [ReadOnly] public BufferLookup<PatherWayPoint> WaypointLookup;
    [ReadOnly] public PhysicsWorldSingleton World;
    [ReadOnly] public CollisionFilter Filter;

    [BurstCompile]
    void Execute(
        Entity entity,
        ref UnitMovement mov,
        ref LocalTransform transform,
        ref Pather pather,
        ref UnitState state)
    {
        float3 currentPosition = transform.Position;
        float3 targetPosition = GetTargetPosition(entity, ref pather, ref mov, ref state, currentPosition, currentPosition);

        UpdatePreferredVelocity(entity, ref mov, currentPosition, targetPosition, INDEX_DISTANCE_SQ);
        ApplyMovement(ref transform, mov.Velocity, currentPosition);
        GroundUnit(ref transform, pather.Dest);
        //                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                              DrawDebugDest(mov.Dest, transform.Position);
        //DrawDebugVelocities(transform.Position, mov.PreferredVelocity, mov.Velocity);
    }
    [BurstCompile]
    private float3 GetTargetPosition(
        Entity entity,
        ref Pather pather,
        ref UnitMovement mov,
        ref UnitState state,
        float3 defaultDestination,
        float3 currentPosition)
    {
        /*            if (!WaypointLookup.TryGetBuffer(entity, out var waypoints) || !pather.PathCalculated)
                        return defaultDestination;

                    if (waypoints.Length <= 1 || pather.WaypointIndex >= waypoints.Length)
                        return defaultDestination;*/
        if (state.State == UnitStates.Idle 
            || state.State == UnitStates.Attack
            ||!WaypointLookup.TryGetBuffer(entity, out var waypoints) 
            || waypoints.Length == 0
            || !pather.PathCalculated)
        {
            //UnityEngine.Debug.Log("Default");
            return defaultDestination;
        }
            
        return UpdateWaypointIndex(ref pather, ref mov, waypoints, currentPosition);
    }
    [BurstCompile]
    private float3 UpdateWaypointIndex(
        ref Pather pather,
        ref UnitMovement mov,
        DynamicBuffer<PatherWayPoint> waypoints,
        float3 currentPosition)
    {
        float3 currentWaypoint = waypoints[pather.WaypointIndex].Position;
        float waypointDistanceSq = pather.IndexDistance * pather.IndexDistance;
        
        //check cluster first
        // if (math.distancesq(mov.PreferredVelocity/mov.MaxSpeed, mov.Velocity/mov.MaxSpeed)
        // >= CLUSTER_WAYPOINT_INDEX_DIST)
        // {
        //     // Advance to next waypoint if available
        //     if (pather.WaypointIndex < waypoints.Length - 1)
        //     {
        //         pather.WaypointIndex++;
        //         currentWaypoint = waypoints[pather.WaypointIndex].Position;
        //     }
        // }
        // Check if we've reached current waypoint
        if (BMath.DistXZsq(currentPosition, currentWaypoint) <= INDEX_DISTANCE_SQ)
        {
            // Advance to next waypoint if available
            if (pather.WaypointIndex < waypoints.Length - 1)
            {
                pather.WaypointIndex++;
                currentWaypoint = waypoints[pather.WaypointIndex].Position;
            }
        }

        return currentWaypoint;
    }
    [BurstCompile]
    private void UpdatePreferredVelocity(
        Entity entity,
        ref UnitMovement mov,
        float3 currentPosition,
        float3 targetPosition,
        float arriveDistance)
    {
        float3 delta = targetPosition - currentPosition;
        delta.y = 0f;

        float distanceSq = math.lengthsq(delta);
        float arriveDistanceSq = math.max(MIN_ARRIVE_DISTANCE_SQ, arriveDistance);

        // If we're close enough to target, stop
        if (distanceSq <= arriveDistanceSq)
        {
            mov.PreferredVelocity = float2.zero;
            return;
        }

        // Calculate direction and preferred velocity
        float2 direction2D = new float2(delta.x, delta.z);
        float directionLength = math.length(direction2D);

        if (directionLength > MIN_DIRECTION_LENGTH)
        {
            float2 dir = direction2D / directionLength;

            // --- perpendicular bias ---
            // float2 perp = new float2(-dir.y, dir.x);

            // // Stable per-entity bias (deterministic)
            // uint hash = (uint)entity.Index * 747796405u + 2891336453u;
            // float bias01 = (hash & 1023u) / 1023f; // 0-1
            // float signedBias = (bias01 - 0.5f) * 2f; // -1 to 1

            // //float laneWidth = mov.Radius * 1.5f; // tune this
            // float2 biasedDir = math.normalize(dir + perp * signedBias * LANE_STRENGTH);

            mov.PreferredVelocity = dir * mov.MaxSpeed;
        }
        else
        {
            mov.PreferredVelocity = float2.zero;
        }
    }
    [BurstCompile]
    private void ApplyMovement(
        ref LocalTransform transform,
        float2 velocity,
        float3 currentPosition)
    {
        // Calculate movement based on ORCA velocity
        float3 movement = new float3(velocity.x, 0f, velocity.y) * FIXED_DT;
        float3 nextPosition = currentPosition + movement;

        // Update rotation to face movement direction
        //UpdateRotation(ref transform, velocity);

        transform.Position = nextPosition;
    }

    /*        private void UpdateRotation(ref LocalTransform transform, float2 velocity)
            {
                float3 forward = new float3(velocity.x, 0f, velocity.y);
                float velocitySq = math.lengthsq(forward);

                if (velocitySq > MIN_VELOCITY_SQ)
                {
                    transform.Rotation = quaternion.LookRotationSafe(forward, math.up());
                }
            }*/
    [BurstCompile]
    private void GroundUnit(
        ref LocalTransform transform,
        float3 targetPosition
    )
    {
        bool applyRotation = true;
        // Fallback forward if target is basically the same position
        if (math.distancesq(transform.Position, targetPosition) <= MIN_ARRIVE_DISTANCE_SQ)
        {
            applyRotation = false;
            //targetPosition = transform.Position + transform.Forward();
        }

        float3 rayStart = transform.Position + new float3(0, GROUND_RAYCAST_OFFSET, 0);
        float3 rayEnd = transform.Position - new float3(0, GROUND_RAYCAST_OFFSET, 0);

        var raycastInput = new RaycastInput
        {
            Start = rayStart,
            End = rayEnd,
            Filter = Filter
        };

        if (World.CastRay(raycastInput, out RaycastHit hit))
        {
            // Snap position to ground
            transform.Position.y = hit.Position.y;


            if (!applyRotation) return;

            float3 up = hit.SurfaceNormal;

            float3 toTarget = math.normalize(targetPosition - transform.Position);

            // Project forward onto surface plane
            float3 forward = math.normalize(
                toTarget - up * math.dot(toTarget, up)
            );
            quaternion targetRotation = quaternion.LookRotation(forward, up);

            transform.Rotation = math.slerp(
                transform.Rotation,
                targetRotation,
                FIXED_DT * SLERP_SPEED
            );

            
        }
    }

    // [BurstCompile]
    // private void DrawDebugVelocities(float3 position, float2 preferredVelocity, float2 actualVelocity)
    // {
    //     float3 basePosition = position;

    //     // Yellow line for preferred velocity
    //     float3 preferredDirection = new float3(preferredVelocity.x, 0f, preferredVelocity.y);
    //     UnityEngine.Debug.DrawLine(
    //         basePosition,
    //         basePosition + preferredDirection * DEBUG_LINE_LENGTH,
    //         UnityEngine.Color.yellow,
    //         FIXED_DT);

    //     // Red line for actual velocity
    //     float3 actualDirection = new float3(actualVelocity.x, 0f, actualVelocity.y);
    //     UnityEngine.Debug.DrawLine(
    //         basePosition,
    //         basePosition + actualDirection * DEBUG_LINE_LENGTH,
    //         UnityEngine.Color.red,
    //         FIXED_DT);
    // }
    // private void DrawDebugDest(float3 pos, float3 dest)
    // {
    //     UnityEngine.Debug.DrawLine(pos, dest, UnityEngine.Color.cyan, FIXED_DT);
    // }
}