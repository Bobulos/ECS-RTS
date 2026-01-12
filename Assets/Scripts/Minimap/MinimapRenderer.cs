using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine.UI;
using Unity.Burst;
using Unity.Jobs;
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
    void Awake()
    {
        minimapTexture = new RenderTexture(textureSize, textureSize, 0, RenderTextureFormat.ARGB32);
        minimapTexture.enableRandomWrite = true;
        minimapTexture.filterMode = FilterMode.Point;
        minimapTexture.Create();
        GetComponent<RawImage>().texture = minimapTexture;
    }

    void Update()
    {
        minimap.rotation = Quaternion.Euler(0,0,cam.rotation.eulerAngles.y);
        UpdatePlayerIcon();
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
    public void UpdateMinimap(int team, NativeArray<float2> unitPositions)
    {
        
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
public partial class CollectUnitsSystem : SystemBase
{
    private MinimapRenderer minimap; // cached
    protected override void OnUpdate()
    {
        if (minimap == null)
        {
            minimap = GameObject.FindFirstObjectByType<MinimapRenderer>();
            return;
        }
        

        minimap.ClearMinimap();

        if (SystemAPI.TryGetSingleton<LocalPlayerData>(out LocalPlayerData playerData))
        {
            var map = SystemAPI.GetSingleton<MapData>();

            float2 wMin = new float2(-map.Size.x * 0.5f, -map.Size.y * 0.5f);
            float2 wMax = new float2(map.Size.x * 0.5f, map.Size.y * 0.5f);

            // Allocations (TempJob so Burst can use them)
            var friendlyPos = new NativeList<float2>(10000, Allocator.TempJob);

            var friendly = new CollectUnitsJob
            {
                InverseTeam = false,
                TeamID = playerData.TeamID,
                WorldMin = wMin,
                WorldMax = wMax,
                Positions = friendlyPos.AsParallelWriter(),
            };

            JobHandle handle = friendly.ScheduleParallel(Dependency);
            Dependency = handle; // must set Dependency inside SystemBase

            handle.Complete();

            // Only update minimap if results exist
            if (!friendlyPos.IsEmpty)
                minimap.UpdateMinimap(0, friendlyPos.AsArray());


            var enemyPos = new NativeList<float2>(10000, Allocator.TempJob);

            var enemy = new CollectUnitsJob
            {
                InverseTeam = true,
                TeamID = playerData.TeamID,
                WorldMin = wMin,
                WorldMax = wMax,
                Positions = enemyPos.AsParallelWriter(),
            };

            handle = enemy.ScheduleParallel(Dependency);
            Dependency = handle; // must set Dependency inside SystemBase

            handle.Complete();

            // Only update minimap if results exist
            if (!enemyPos.IsEmpty)
                minimap.UpdateMinimap(1, enemyPos.AsArray());

            enemyPos.Dispose();
            friendlyPos.Dispose();
        }
    }
}

[BurstCompile]
[UpdateAfter(typeof(FogSystem))]
public partial struct CollectUnitsJob : IJobEntity
{
    public bool InverseTeam;
    public int TeamID;
    public float2 WorldMin;
    public float2 WorldMax;

    public NativeList<float2>.ParallelWriter Positions;
    [BurstCompile]
    void Execute(RefRO<LocalTransform> transform, RefRO<UnitTeam> team, RefRO<LocalVisibility> vis)
    {
        if (!vis.ValueRO.IsVisible) return;
        if (InverseTeam)
        {
            if (team.ValueRO.TeamID != TeamID)
            {
                float2 pos;
                pos.x = math.clamp((transform.ValueRO.Position.x - WorldMin.x) / (WorldMax.x - WorldMin.x), 0, 1);
                pos.y = math.clamp((transform.ValueRO.Position.z - WorldMin.y) / (WorldMax.y - WorldMin.y), 0, 1);
                Positions.AddNoResize(pos);
            }
        }
        else
        {
            if (team.ValueRO.TeamID == TeamID)
            {
                float2 pos;
                pos.x = math.clamp((transform.ValueRO.Position.x - WorldMin.x) / (WorldMax.x - WorldMin.x), 0, 1);
                pos.y = math.clamp((transform.ValueRO.Position.z - WorldMin.y) / (WorldMax.y - WorldMin.y), 0, 1);
                Positions.AddNoResize(pos);
            }
        }
    }
}

