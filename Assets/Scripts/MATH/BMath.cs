using UnityEngine;
using Unity.Burst;
using Unity.Mathematics;


[BurstCompile]
public static class BMath
{
    public static float DistXZ(float3 a, float3 b)
    {
        float2 a2 = new float2(a.x,a.z);
        float2 b2 = new float2(b.x,b.z);
        return math.distance(a2,b2);
    }
}