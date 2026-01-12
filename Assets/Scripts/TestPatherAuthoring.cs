using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Unity.Collections;
public class PatherAuthoring : MonoBehaviour
{
    //public GameObject prefab;
    public float3 destination = float3.zero;
    public float indexDistance;
    public float speed;
    //public List<float3> waypoints = new List<float3>();
}
class PatherBaker : Baker<PatherAuthoring>
{
    public override void Bake(PatherAuthoring authoring)
    {
        Debug.Log("Baked bean");
        Entity entity = GetEntity(TransformUsageFlags.None);
        AddComponent(entity, new Pather
        {
            Dest = authoring.destination,
            IndexDistance = authoring.indexDistance,
            NeedsUpdate = true,
            WaypointIndex = -1,
            PathCalculated = false,
            QuerySet = false,
            //pathPositions = new NativeList<int2>(Allocator.Persistent)
            //waypoints = authoring.waypoints
            //prefab = GetEntity(authoring.prefab, TransformUsageFlags.Dynamic),
            //spawnPos = authoring.transform.position,
            //nextSpawnTime = 0.0f,
            //spawnRate = authoring.spawnRate
            //pathBuffer = new DynamicBuffer<PathPosition>(),
        });
        AddComponent<PatherWayPoint>(entity);
    }
}