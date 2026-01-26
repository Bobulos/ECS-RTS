using Unity.Burst;
using Unity.Mathematics;


[BurstCompile]
public static class BMath
{
    //[BurstCompile]
    public static float DistXZ(float3 a, float3 b)
    {
        float2 a2 = new float2(a.x, a.z);
        float2 b2 = new float2(b.x, b.z);
        return math.distance(a2, b2);
    }
    //[BurstCompile]
    public static float DistXZsq(float3 a, float3 b)
    {
        float2 a2 = new float2(a.x, a.z);
        float2 b2 = new float2(b.x, b.z);
        return math.distancesq(a2, b2);
    }
    //[BurstCompile]
    public static float3 IgnoreY(float3 a)
    {
        return new float3(a.x, 0, a.z);
    }
}