using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
namespace AICommander
{
    public struct InfluenceMap : IComponentData
{
    public FixedList4096Bytes<InfluenceMapNode> MapNodes;
}

// 2 bytes
public struct InfluenceMapNode
{
    public sbyte TeamFavor;
    public byte Strength;
}

public static class InfluenceMapUtil
{
    //private static int _mapDimensions = 0;
    public const int NODE_SIZE = 16;

    public static InfluenceMapNode GetNode(
        FixedList4096Bytes<InfluenceMapNode> map,
        float3 pos,
        int2 mapSize)
    {
        // Convert world position to grid coordinates
        int x = (int)math.floor(pos.x / NODE_SIZE);
        int z = (int)math.floor(pos.z / NODE_SIZE);

        // Clamp to valid range
        x = math.clamp(x, 0, mapSize.x - 1);
        z = math.clamp(z, 0, mapSize.y - 1);

        int index = z * mapSize.x + x;
        return map[index];
    }

    public static FixedList4096Bytes<InfluenceMapNode> BuildMap(int worldSize)
    {
        int gridSize = worldSize / NODE_SIZE;
        int totalNodes = gridSize * gridSize;

        var map = new FixedList4096Bytes<InfluenceMapNode>();

        for (int i = 0; i < totalNodes; i++)
        {
            map.Add(new InfluenceMapNode
            {
                TeamFavor = 0,
                Strength = 0
            });
        }

        return map;
    }

    public static int2 GetPositionOfNode(int index, int gridSize)
    {
        int x = index % gridSize;
        int z = index / gridSize;

        return new int2(
            x * NODE_SIZE + NODE_SIZE / 2,
            z * NODE_SIZE + NODE_SIZE / 2
        );
    }
}
}
