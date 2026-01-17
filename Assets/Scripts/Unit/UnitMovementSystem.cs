using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;


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

    [BurstCompile]
    [WithNone(typeof(DeadTag))]
    private partial struct MovementJob : IJobEntity
    {
        private const float FIXED_DT = 1f / 50f;
        private const float MIN_VELOCITY_SQ = 1e-6f;
        private const float MIN_DIRECTION_LENGTH = 1e-4f;
        private const float MIN_ARRIVE_DISTANCE_SQ = 0.01f;
        private const float GROUND_RAYCAST_OFFSET = 10f;
        private const float DEBUG_LINE_LENGTH = 1f;

        [ReadOnly] public BufferLookup<PatherWayPoint> WaypointLookup;
        [ReadOnly] public PhysicsWorldSingleton World;
        [ReadOnly] public CollisionFilter Filter;

        [BurstCompile]
        void Execute(
            Entity entity,
            ref UnitMovement mov,
            ref LocalTransform transform,
            ref Pather pather,
            ref UnitState unitState)
        {
            float3 currentPosition = transform.Position;
            float3 targetPosition = GetTargetPosition(entity, ref pather, mov.Dest, currentPosition);

            UpdatePreferredVelocity(ref mov, currentPosition, targetPosition, pather.IndexDistance);
            ApplyMovement(ref transform, mov.Velocity, currentPosition);
            GroundUnit(ref transform, transform.Position);

            //                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                              DrawDebugDest(mov.Dest, transform.Position);
            //DrawDebugVelocities(transform.Position, mov.PreferredVelocity, mov.Velocity);
        }

        private float3 GetTargetPosition(
            Entity entity,
            ref Pather pather,
            float3 defaultDestination,
            float3 currentPosition)
        {
            if (!WaypointLookup.TryGetBuffer(entity, out var waypoints) || !pather.PathCalculated)
                return defaultDestination;

            if (waypoints.Length <= 1 || pather.WaypointIndex >= waypoints.Length - 2)
                return defaultDestination;

            return UpdateWaypointIndex(ref pather, waypoints, currentPosition);
        }

        private float3 UpdateWaypointIndex(
            ref Pather pather,
            DynamicBuffer<PatherWayPoint> waypoints,
            float3 currentPosition)
        {
            float3 currentWaypoint = waypoints[pather.WaypointIndex].Position;
            float waypointDistanceSq = pather.IndexDistance * pather.IndexDistance;

            // Check if we've reached current waypoint
            if (math.distancesq(currentPosition, currentWaypoint) <= waypointDistanceSq)
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

        private void UpdatePreferredVelocity(
            ref UnitMovement mov,
            float3 currentPosition,
            float3 targetPosition,
            float arriveDistance)
        {
            float3 delta = targetPosition - currentPosition;
            delta.y = 0f;

            float distanceSq = math.lengthsq(delta);
            float arriveDistanceSq = math.max(MIN_ARRIVE_DISTANCE_SQ, arriveDistance * arriveDistance);

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
                mov.PreferredVelocity = (direction2D / directionLength) * mov.MaxSpeed;
            }
            else
            {
                mov.PreferredVelocity = float2.zero;
            }
        }

        private void ApplyMovement(
            ref LocalTransform transform,
            float2 velocity,
            float3 currentPosition)
        {
            // Calculate movement based on ORCA velocity
            float3 movement = new float3(velocity.x, 0f, velocity.y) * FIXED_DT;
            float3 nextPosition = currentPosition + movement;

            // Update rotation to face movement direction
            UpdateRotation(ref transform, velocity);

            transform.Position = nextPosition;
        }

        private void UpdateRotation(ref LocalTransform transform, float2 velocity)
        {
            float3 forward = new float3(velocity.x, 0f, velocity.y);
            float velocitySq = math.lengthsq(forward);

            if (velocitySq > MIN_VELOCITY_SQ)
            {
                transform.Rotation = quaternion.LookRotationSafe(forward, math.up());
            }
        }

        private void GroundUnit(ref LocalTransform transform, float3 position)
        {
            float3 rayStart = position + new float3(0, GROUND_RAYCAST_OFFSET, 0);
            float3 rayEnd = position - new float3(0, GROUND_RAYCAST_OFFSET, 0);

            var raycastInput = new RaycastInput
            {
                Start = rayStart,
                End = rayEnd,
                Filter = Filter
            };

            if (World.CastRay(raycastInput, out RaycastHit hit))
            {
                position.y = hit.Position.y;
                transform.Position = position;
            }
        }

        private void DrawDebugVelocities(float3 position, float2 preferredVelocity, float2 actualVelocity)
        {
            float3 basePosition = position;

            // Yellow line for preferred velocity
            float3 preferredDirection = new float3(preferredVelocity.x, 0f, preferredVelocity.y);
            UnityEngine.Debug.DrawLine(
                basePosition,
                basePosition + preferredDirection * DEBUG_LINE_LENGTH,
                UnityEngine.Color.yellow,
                FIXED_DT);

            // Red line for actual velocity
            float3 actualDirection = new float3(actualVelocity.x, 0f, actualVelocity.y);
            UnityEngine.Debug.DrawLine(
                basePosition,
                basePosition + actualDirection * DEBUG_LINE_LENGTH,
                UnityEngine.Color.red,
                FIXED_DT);
        }
        private void DrawDebugDest(float3 pos, float3 dest)
        {
            UnityEngine.Debug.DrawLine(pos, dest, UnityEngine.Color.cyan, FIXED_DT);
        }
    }
    private static float3 IY(float3 a)
    {
        return new float3(a.x, 0, a.z);
    }

}
