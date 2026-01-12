using Unity.Mathematics;
using Unity.Entities;
using Unity.Collections;
using Unity.Transforms;
using Unity.Burst;
using System.Diagnostics;
using System.Linq;

[BurstCompile]
public partial struct UnitLocalAvoidanceJob : IJobEntity
{
    [ReadOnly] public NativeParallelMultiHashMap<int, UnitData> SpatialMap;

    public float CellSize;
    public float TimeHorizon;
    public const float FIXED_DT = 1f / 50f;

    void Execute(
        ref UnitMovement mov,
        in LocalTransform transform,
        in UnitTeam team,
        in Entity entity)
    {
        FixedList4096Bytes<Line> lines = new FixedList4096Bytes<Line>();
        lines.Clear();

        float3 pos = transform.Position;
        float2 vel = mov.Velocity;

        int cellX = (int)math.floor(pos.x / CellSize);
        int cellZ = (int)math.floor(pos.z / CellSize);

        int count = 0;
        // gather neighbors + build ORCA lines
        for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
            {
                int hash = ((cellX + dx) * 73856093) ^ ((cellZ + dz) * 19349663);

                if (!SpatialMap.TryGetFirstValue(hash, out UnitData other, out var it))
                    continue;

                do
                {
                    if (count >= 500) break;
                    if (other.Entity == entity)
                        continue;
                    count++;
                    AddORCALine(
                        pos,
                        vel,
                        mov.Radius,
                        other,
                        ref lines
                    );
                } while (SpatialMap.TryGetNextValue(out other, ref it));
            }

        /*// DEBUG: Draw all ORCA lines
        for (int i = 0; i < lines.Length; i++)
        {
            Line line = lines[i];
            float3 linePoint = new float3(pos.x + line.Point.x, pos.y, pos.z + line.Point.y);
            float3 lineDir = new float3(line.Direction.x, 0, line.Direction.y);

            // Draw the line (point + direction scaled)
            UnityEngine.Debug.DrawLine(
                    linePoint,
                    linePoint + lineDir * 5f,
                    UnityEngine.Color.yellow,
                    FIXED_DT
                );

            // Draw point marker
            UnityEngine.Debug.DrawLine(
                linePoint + new float3(0, 0.1f, 0),
                linePoint - new float3(0, 0.1f, 0),
                UnityEngine.Color.red,
                FIXED_DT
            );
        }*/

        // --- solve ORCA ---
        float2 newVelocity = SolveORCA(
            lines,
            mov.PreferredVelocity,
            mov.MaxSpeed
        );
        if (newVelocity.Equals(float2.zero))
        {
            mov.Velocity = newVelocity;
        }else
        {
            mov.Velocity = newVelocity;
        }
        /*// DEBUG: Draw velocity changes
        float3 velStart = pos;
        float3 oldVelEnd = new float3(pos.x + oldVel.x, pos.y, pos.z + oldVel.y);
        float3 newVelEnd = new float3(pos.x + newVelocity.x, pos.y, pos.z + newVelocity.y);
        float3 prefVelEnd = new float3(pos.x + mov.PreferredVelocity.x, pos.y, pos.z + mov.PreferredVelocity.y);

        //if (!oldVelEnd.Equals(newVelEnd)) UnityEngine.Debug.Log("Path orca corrected");

        // Old velocity (cyan)
        UnityEngine.Debug.DrawLine(velStart, oldVelEnd, UnityEngine.Color.cyan, FIXED_DT);

        // New velocity (green)
        UnityEngine.Debug.DrawLine(velStart, newVelEnd, UnityEngine.Color.green, FIXED_DT);

        // Preferred velocity (blue)
        UnityEngine.Debug.DrawLine(velStart, prefVelEnd, UnityEngine.Color.blue, FIXED_DT);*/

        
    }
    void AddORCALine(float3 pos, float2 vel, float radius, in UnitData other, ref FixedList4096Bytes<Line> lines)
    {
        float2 relPos = new float2(other.Position.x - pos.x, other.Position.z - pos.z);
        float2 relVel = vel - other.Velocity;
        float distSq = math.lengthsq(relPos);
        float combinedRadius = radius + other.Radius;
        float combinedRadiusSq = combinedRadius * combinedRadius;

        Line line;
        float2 u;

        if (distSq > combinedRadiusSq)
        {
            // No collision yet. Check the truncated cone (VO)
            float2 w = relVel - (relPos / TimeHorizon);
            float wLengthSq = math.lengthsq(w);
            float dotProduct1 = math.dot(w, relPos);

            // Project on cut-off circle
            if (dotProduct1 < 0.0f && (dotProduct1 * dotProduct1) > (combinedRadiusSq * wLengthSq))
            {
                float wLength = math.sqrt(wLengthSq);
                float2 unitW = w / wLength;

                line.Direction = new float2(unitW.y, -unitW.x);
                u = (combinedRadius / TimeHorizon - wLength) * unitW;
            }
            else // Project on legs
            {
                float leg = math.sqrt(distSq - combinedRadiusSq);
                if (Cross(relPos, w) > 0.0f) // Project on left leg
                {
                    line.Direction = new float2(relPos.x * leg - relPos.y * combinedRadius,
                                              relPos.x * combinedRadius + relPos.y * leg) / distSq;
                }
                else // Project on right leg
                {
                    line.Direction = -new float2(relPos.x * leg + relPos.y * combinedRadius,
                                               -relPos.x * combinedRadius + relPos.y * leg) / distSq;
                }

                float dotProduct2 = math.dot(relVel, line.Direction);
                u = dotProduct2 * line.Direction - relVel;
            }
        }
        else
        {
            // Already colliding. Use FIXED_DT to push apart.
            float invTimeStep = 1.0f / FIXED_DT;
            float2 w = relVel - (relPos * invTimeStep);
            float wLength = math.length(w);
            float2 unitW = w / wLength;

            line.Direction = new float2(unitW.y, -unitW.x);
            u = (combinedRadius * invTimeStep - wLength) * unitW;
        }

        line.Point = vel + 0.5f * u; // 0.5f for reciprocal avoidance
        lines.Add(line);
    }


    float2 SolveORCA(FixedList4096Bytes<Line> lines, float2 preferred, float maxSpeed)
    {
        float2 result = preferred;

        // Clamp preferred to max speed
        if (math.lengthsq(result) > maxSpeed * maxSpeed)
            result = math.normalize(result) * maxSpeed;

        for (int i = 0; i < lines.Length; i++)
        {
            // If the current result violates this line...
            if (Cross(lines[i].Direction, lines[i].Point - result) > 0f)
            {
                float2 oldResult = result;
                if (!SolveLine(lines, i, maxSpeed, preferred, ref result))
                {
                    //fail set pref to 0
                    return float2.zero;
                }
            }
        }
        return result;
    }

    bool SolveLine(
        FixedList4096Bytes<Line> lines,
        int index,
        float maxSpeed,
        float2 preferred,
        ref float2 result)
    {
        Line line = lines[index];

        float dot = math.dot(line.Point, line.Direction);
        float disc = dot * dot + maxSpeed * maxSpeed - math.lengthsq(line.Point);

        if (disc < 0f)
            return false;

        float sqrt = math.sqrt(disc);
        float left = -dot - sqrt;
        float right = -dot + sqrt;

        for (int i = 0; i < index; i++)
        {
            float denom = Cross(line.Direction, lines[i].Direction);
            float numer = Cross(lines[i].Direction, line.Point - lines[i].Point);

            if (math.abs(denom) < 1e-6f)
            {
                if (numer < 0f)
                {
                    return false;
                }
                continue;
            }

            float t = numer / denom;
            if (denom > 0f) right = math.min(right, t);
            else left = math.max(left, t);

            if (left > right)
                return false;
        }

        float tFinal = math.clamp(
            math.dot(line.Direction, preferred - line.Point),
            left,
            right);

        result = line.Point + tFinal * line.Direction;
        return true;
    }

    static float Cross(float2 a, float2 b)
    {
        return a.x * b.y - a.y * b.x;
    }
}

public struct Line
{
    public float2 Point;
    public float2 Direction; // normalized
}
/*float2 SolveORCA(
        FixedList512Bytes<Line> lines,
        float2 preferred,
        float maxSpeed)
    {
        float2 result = preferred;

        float speed = math.length(result);
        if (speed > maxSpeed)
            result = result / speed * maxSpeed;

        for (int i = 0; i < lines.Length; i++)
        {
            if (Cross(lines[i].Direction, lines[i].Point - result) > 0f)
            {
                if (!SolveLine(lines, i, maxSpeed, preferred, ref result))
                    continue;
            }
        }
        return result;
    }*/
/*void AddORCALine(
        float3 pos,
        float2 vel,
        float radius,
        in UnitData other,
        ref FixedList512Bytes<Line> lines)
{
    if (lines.Length >= 32)
        return;

    float2 relPos = new float2(
        other.Position.x - pos.x,
        other.Position.z - pos.z);

    float2 relVel = vel - other.Velocity;
    float combinedRadius = radius + other.Radius;
    float distSq = math.lengthsq(relPos);

    // DEBUG: Draw connection to neighbor
    UnityEngine.Debug.DrawLine(
        pos,
        other.Position,
        distSq > combinedRadius * combinedRadius ? UnityEngine.Color.white : UnityEngine.Color.magenta,
        FIXED_DT
    );

    Line line;
    float2 u;

    if (distSq > combinedRadius * combinedRadius)
    {
        float dist = math.sqrt(distSq);
        float invTimeHorizonObst = 1.0f / TimeHorizon;

        float2 w = relVel - invTimeHorizonObst * relPos;
        float wLengthSq = math.lengthsq(w);

        float dotProduct = math.dot(w, relPos);

        if (dotProduct < 0.0f && dotProduct * dotProduct > combinedRadius * combinedRadius * wLengthSq)
        {
            // No collision within time horizon
            float wLength = math.sqrt(wLengthSq);
            float2 unitW = w / wLength;

            line.Direction = new float2(unitW.y, -unitW.x);
            u = (combinedRadius * invTimeHorizonObst - wLength) * unitW;
        }
        else
        {
            dist = math.sqrt(distSq);
            w = relVel - (relPos / dist) * (combinedRadius - dist) / FIXED_DT;
            float wLen = math.length(w);
            float2 unitW = math.normalize(w);

            line.Direction = new float2(unitW.y, -unitW.x);
            u = (combinedRadius / FIXED_DT - wLen) * unitW;
        }
    }
    else
    {
        float dist = math.sqrt(distSq);
        float2 w = relVel - (relPos / dist) * (combinedRadius - dist) / FIXED_DT;
        float wLen = math.length(w);
        float2 unitW = math.normalize(w);

        line.Direction = new float2(unitW.y, -unitW.x);
        u = (combinedRadius / FIXED_DT - wLen) * unitW;
    }

    line.Point = vel + 0.5f * u;
    lines.Add(line);
}*/