using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UI;
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
        entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        mapQuery = entityManager.CreateEntityQuery(typeof(MinimapData));
    }
    void Update()
    {
        minimap.rotation = Quaternion.Euler(0, 0, cam.rotation.eulerAngles.y);
        UpdatePlayerIcon();

        var data = mapQuery.GetSingleton<MinimapData>();
        ClearMinimap();
        UpdateMinimap(1, data.enemy);
        UpdateMinimap(0, data.friendly);

        // Collect ECS unit positions
    }
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
    public void UpdateMinimap(int team, float2[] unitPositions)
    {
        if (unitPositions.Length == 0) return;
        int stampKernel = minimapComputeShader.FindKernel("Stamp");

        if (positionBuffer != null)
            positionBuffer.Release();

        positionBuffer = new ComputeBuffer(unitPositions.Length, sizeof(float) * 2);
        positionBuffer.SetData(unitPositions);

        minimapComputeShader.SetBuffer(stampKernel, "_Positions", positionBuffer);
        minimapComputeShader.SetTexture(stampKernel, "_Result", minimapTexture);
        minimapComputeShader.SetVector("_Color", teamColors[team]);
        minimapComputeShader.SetInt("_UnitCount", unitPositions.Length);

        // 1 thread per unit
        int stampGroups = Mathf.CeilToInt(unitPositions.Length / 64f);
        minimapComputeShader.Dispatch(stampKernel, stampGroups, 1, 1);
    }
    public void ClearMinimap()
    {
        int clearKernel = minimapComputeShader.FindKernel("Clear");

        minimapComputeShader.SetTexture(clearKernel, "_Result", minimapTexture);
        int tgx = Mathf.CeilToInt(textureSize / 8f);
        int tgy = Mathf.CeilToInt(textureSize / 8f);
        minimapComputeShader.Dispatch(clearKernel, tgx, tgy, 1);
    }
    void OnDestroy()
    {
        if (positionBuffer != null)
            positionBuffer.Release();
    }
}


//managed component
public class MinimapData : IComponentData
{
    public float2[] friendly;
    public float2 [] enemy;
}
//[UpdateBefore(typeof(LocalV))]
public partial class CollectUnitsSystem : SystemBase
{
    public const int MAX_ALLOC_UNITS = 1024 * 4;
    //private MinimapRenderer _minimap; // cached

    private NativeList<float2> _friendly;
    private NativeList<float2> _enemy;

    private float2 _wMin;
    protected override void OnCreate()
    {
        _friendly = new NativeList<float2>(MAX_ALLOC_UNITS, Allocator.Persistent);
        _enemy = new NativeList<float2>(MAX_ALLOC_UNITS, Allocator.Persistent);

        var entity = EntityManager.CreateEntity();
        EntityManager.AddComponentData(entity, new MinimapData
        {
            friendly = new float2[0], // Initialize with empty arrays
            enemy = new float2[0]
        });
    }
    protected override void OnDestroy()
    {
        _friendly.Dispose();
        _enemy.Dispose();
    }
    private JobHandle _pendingJob;
    private bool _jobScheduled;

    protected override void OnUpdate()
    {
        // First, check if previous job is done and update minimap
        if (_jobScheduled && _pendingJob.IsCompleted)
        {
            _pendingJob.Complete();
            
            if (SystemAPI.ManagedAPI.TryGetSingleton(out MinimapData minimap))
            {
                if (_friendly.Length > 0)
                    minimap.friendly = _friendly.AsArray().ToArray();
                if (_enemy.Length > 0)
                    minimap.enemy = _enemy.AsArray().ToArray();
            }
            
            _jobScheduled = false;
        }

        // Then schedule new job
        _friendly.Clear(); 
        _enemy.Clear();

        if (!SystemAPI.TryGetSingleton(out LocalPlayerData playerData)) return;

        var map = SystemAPI.GetSingleton<MapData>();
        float2 wMin = new float2(-map.Size.x * 0.5f, -map.Size.y * 0.5f);
        float2 wMax = new float2(map.Size.x * 0.5f, map.Size.y * 0.5f);

        var job = new CollectUnitsJob
        {
            TeamID = playerData.TeamID,
            WorldMin = wMin,
            WorldSize = wMax - wMin,
            Friendly = _friendly,
            Enemy = _enemy,
        };
        
        _pendingJob = job.Schedule(Dependency);
        Dependency = _pendingJob;
        _jobScheduled = true;
    }
}

[BurstCompile]
public partial struct CollectUnitsJob : IJobEntity
{
    public bool InverseTeam;
    public int TeamID;
    public float2 WorldMin;
    public float2 WorldSize;

    public NativeList<float2> Friendly;
    public NativeList<float2> Enemy;
    [BurstCompile]
    void Execute(RefRO<LocalTransform> transform,
             RefRO<Team> team,
             RefRO<LocalVisibility> vis)
    {
        if (!vis.ValueRO.IsVisible) return;

        float2 pos;
        pos.x = math.clamp((transform.ValueRO.Position.x - WorldMin.x) / WorldSize.x, 0, 1);
        pos.y = math.clamp((transform.ValueRO.Position.z - WorldMin.y) / WorldSize.y, 0, 1);

        if (team.ValueRO.TeamID == TeamID)
            Friendly.Add(pos);
        else
            Enemy.Add(pos);
    }
}

