using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UI;
using Unity.NetCode;
//[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public class MinimapRenderer : MonoBehaviour
{
    public ComputeShader minimapComputeShader;
    public RenderTexture minimapTexture;
    public int textureSize = 256;
    //public float dotSize = 2f; // pixels
    public RectTransform playerIcon;

    public Transform cam;
    public RectTransform minimap;
    // Bounds of the world to normalize positions
    public Vector2 worldMin = new Vector2(-200, -200);
    public Vector2 worldMax = new Vector2(200, 200);

    private ComputeBuffer positionBuffer;

    public int teamID = 0;

    public EntityManager entityManager;
    public EntityQuery mapQuery;
    private EntityQuery query;

    private NativeList<float2> friendly;
    private NativeList<float2> enemy;
    void Awake()
    {
        minimapTexture = new RenderTexture(textureSize, textureSize, 0, RenderTextureFormat.ARGB32);
        minimapTexture.enableRandomWrite = true;
        minimapTexture.filterMode = FilterMode.Point;
        minimapTexture.Create();
        GetComponent<RawImage>().texture = minimapTexture;
    }
    void Start()
    {
        //runs pon client world
        entityManager = ClientServerBootstrap.ClientWorld.EntityManager;
        //mapQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<MinimapData>());
        query = entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<LocalTransform>(),
            ComponentType.ReadOnly<Team>(),
            ComponentType.ReadOnly<GhostInstance>()
        );

        friendly = new NativeList<float2>(Allocator.Persistent);
        enemy = new NativeList<float2>(Allocator.Persistent);
    }
    void OnDestroy()
    {
        friendly.Dispose();
        enemy.Dispose();
        if (positionBuffer != null)
            positionBuffer.Release();
    }
    void Update()
    {
        minimap.rotation = Quaternion.Euler(0, 0, cam.rotation.eulerAngles.y);
        UpdatePlayerIcon();
        

        CollectUnits();

        
        ClearMinimap();
        
        UpdateMinimap(0, friendly);
        UpdateMinimap(1, enemy);
        // Collect ECS unit positions
    }
    [BurstCompile]
    void CollectUnits()
    {
        enemy.Clear();
        friendly.Clear();
        //var data = mapQuery.GetSingleton<MinimapData>();
        var transforms = query.ToComponentDataArray<LocalTransform>(Allocator.Temp);
        var teams = query.ToComponentDataArray<Team>(Allocator.Temp);
        
        var worldSize = worldMax*2;
        for (int i = 0; i < transforms.Length; i++)
        {
            
            var position = transforms[i].Position;
            float2 pos;
            pos.x = math.clamp((position.x - worldMin.x) / worldSize.x, 0, 1);
            pos.y = math.clamp((position.z - worldMin.y) / worldSize.y, 0, 1);
            
            if (teams[i].TeamID == teamID)
            {
                friendly.Add(pos);
            } else
            {
                enemy.Add(pos);
            }
        }
    }
    // void Update()
    // {
    //     minimap.rotation = Quaternion.Euler(0, 0, cam.rotation.eulerAngles.y);
    //     UpdatePlayerIcon();
        
    //     if (!mapQuery.TryGetSingleton<MinimapData>(out var map)) return;
    //     //if (!SystemAPI.ManagedAPI.TryGetSingleton<MinimapData>(out var map)) return;

    //     UnityEngine.Debug.Log($"Map data of {map.enemy.Length} units");

    //     ClearMinimap();
        
    //     UpdateMinimap(1, map.enemy);
    //     UpdateMinimap(0, map.friendly);

    // }
    void UpdatePlayerIcon()
    {
        if (playerIcon == null || minimap == null || cam == null)
            return;

        // 1. Normalize camera world position → 0–1 UV space
        float nx = math.clamp((cam.position.x - worldMin.x) / (worldMax.x - worldMin.x), 0f, 1f);
        float nz = math.clamp((cam.position.z - worldMin.y) / (worldMax.y - worldMin.y), 0f, 1f);

        // 2. Convert UV → anchored UI coordinates
        float mapX = (nx - 0.5f) * minimap.rect.width;
        float mapY = (nz - 0.5f) * minimap.rect.height;

        playerIcon.anchoredPosition = new Vector2(mapX, mapY);

        // 3. Rotate icon opposite minimap so it stays upright
        playerIcon.rotation = Quaternion.Euler(0, 0, -cam.rotation.eulerAngles.y);
    }
    [Tooltip("RGBA")]
    public Vector4[] teamColors;
    //NativeArray<float2>
    public void UpdateMinimap(int team, NativeList<float2> unitPositions)
    {
        if (unitPositions.Length == 0) return;
        int stampKernel = minimapComputeShader.FindKernel("Stamp");

        if (positionBuffer != null)
            positionBuffer.Release();

        positionBuffer = new ComputeBuffer(unitPositions.Length, sizeof(float) * 2);
        positionBuffer.SetData(unitPositions.AsArray());

        minimapComputeShader.SetBuffer(stampKernel, "_Positions", positionBuffer);
        minimapComputeShader.SetTexture(stampKernel, "_Result", minimapTexture);
        minimapComputeShader.SetVector("_Color", teamColors[team]);
        minimapComputeShader.SetInt("_UnitCount", unitPositions.Length);

        // 1 thread per unit
        int stampGroups = Mathf.CeilToInt(unitPositions.Length / 64f);
        minimapComputeShader.Dispatch(stampKernel, stampGroups, 1, 1);

        //UnityEngine.Debug.Log($"Minimap detected {unitPositions[0]} unit position");
    }
    public void ClearMinimap()
    {
        int clearKernel = minimapComputeShader.FindKernel("Clear");

        minimapComputeShader.SetTexture(clearKernel, "_Result", minimapTexture);
        int tgx = Mathf.CeilToInt(textureSize / 8f);
        int tgy = Mathf.CeilToInt(textureSize / 8f);
        minimapComputeShader.Dispatch(clearKernel, tgx, tgy, 1);
    }
}


//managed component
// public class MinimapData : IComponentData
// {
//     public float2[] friendly;
//     public float2[] enemy;
// }
// [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
// public partial class CollectUnitsSystem : SystemBase
// {
//     public const int MAX_ALLOC_UNITS = 1024 * 8;
//     //private MinimapRenderer _minimap; // cached

//     private NativeList<float2> _friendly;
//     private NativeList<float2> _enemy;

//     private float2 _wMin;
//     protected override void OnCreate()
//     {
//         _friendly = new NativeList<float2>(MAX_ALLOC_UNITS, Allocator.Persistent);
//         _enemy = new NativeList<float2>(MAX_ALLOC_UNITS, Allocator.Persistent);

//         var entity = EntityManager.CreateEntity();
//         // EntityManager.AddComponentData(entity, new MinimapData
//         // {
//         //     friendly = new NativeArray<float2>(MAX_ALLOC_UNITS/2 ,Allocator.Persistent), // Initialize with empty arrays
//         //     enemy =  new NativeArray<float2>(MAX_ALLOC_UNITS/2 ,Allocator.Persistent),
//         // });
//         EntityManager.AddComponentData(entity, new MinimapData
//         {
//             friendly = new float2[MAX_ALLOC_UNITS/2], // Initialize with empty arrays
//             enemy =  new float2[MAX_ALLOC_UNITS/2],
//         });
//     }
//     protected override void OnDestroy()
//     {
//         _friendly.Dispose();
//         _enemy.Dispose();
//         // if (SystemAPI.ManagedAPI.TryGetSingleton(out MinimapData minimap))
//         // {
//         //     minimap.friendly.Dispose();
//         //     minimap.enemy.Dispose();
//         // }
//     }
//     private JobHandle _pendingJob;
//     private bool _jobScheduled;

//     protected override void OnUpdate()
//     {
//         // First, check if previous job is done and update minimap
//         if (_jobScheduled && _pendingJob.IsCompleted)
//         {
//             //_pendingJob.Complete();
//             if (SystemAPI.ManagedAPI.TryGetSingleton(out MinimapData minimap))
//             {
//                 if (_friendly.Length > 0)
//                     minimap.friendly = _friendly.AsArray().ToArray();
//                 if (_enemy.Length > 0)
//                     minimap.enemy = _enemy.AsArray().ToArray();
//             }
            
//             _jobScheduled = false;
//         }

//         // Then schedule new job
//         _friendly.Clear(); 
//         _enemy.Clear();

//         if (!SystemAPI.TryGetSingleton(out LocalPlayerData playerData)) return;

//         var map = SystemAPI.GetSingleton<MapData>();
//         float2 wMin = new float2(-map.Size.x * 0.5f, -map.Size.y * 0.5f);
//         float2 wMax = new float2(map.Size.x * 0.5f, map.Size.y * 0.5f);

//         var job = new CollectUnitsJob
//         {
//             TeamID = playerData.TeamID,
//             WorldMin = wMin,
//             WorldSize = wMax - wMin,
//             Friendly = _friendly,
//             Enemy = _enemy,
//         };
//         job.Schedule();        
//         // _pendingJob = job.Schedule(Dependency);
//         // Dependency = _pendingJob;
//         _jobScheduled = true;
//     }
// }

// [BurstCompile]
// public partial struct CollectUnitsJob : IJobEntity
// {
//     public bool InverseTeam;
//     public int TeamID;
//     public float2 WorldMin;
//     public float2 WorldSize;

//     public NativeList<float2> Friendly;
//     public NativeList<float2> Enemy;
//     [BurstCompile]
//     void Execute(RefRO<LocalTransform> transform,
//              RefRO<Team> team,
//              RefRO<LocalVisibility> vis)
//     {
//         if (!vis.ValueRO.IsVisible) return;

//         float2 pos;
//         pos.x = math.clamp((transform.ValueRO.Position.x - WorldMin.x) / WorldSize.x, 0, 1);
//         pos.y = math.clamp((transform.ValueRO.Position.z - WorldMin.y) / WorldSize.y, 0, 1);

//         if (team.ValueRO.TeamID == TeamID)
//             Friendly.AddNoResize(pos);
//         else
//             Enemy.AddNoResize(pos);
//     }
// }

