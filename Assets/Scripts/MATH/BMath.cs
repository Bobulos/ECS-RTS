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
    public static uint ManhattanDist2D(int2 a, int2 b)
    {
        uint x = (uint)(a.x-b.x);
        uint y = (uint)(a.y-b.y);
        return x + y;
    }
    //[BurstCompile]
    public static float3 IgnoreY(float3 a)
    {
        return new float3(a.x, 0, a.z);
    }
    public static int2 FlatPosition(float3 a)
    {
        return new int2((int)a.x, (int)a.z);
    }
}