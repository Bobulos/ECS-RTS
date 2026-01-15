using UnityEngine;
using BurstCompile;
using Unity.Mathematics;
[BurstCompile]
public static class BMath
{
    public static float DistXZ(float3 a, float3 b)
    {
        float2 a2 = new float2(a.x,a.z);
        float2 b2 = new float2(b.x,b.z);

        float distSq = (a2*a2)+(b2*b2);
        return Math.Sqrt(distSq);
    }
}