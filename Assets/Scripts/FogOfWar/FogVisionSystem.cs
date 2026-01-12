using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;


public partial class FogSystem : SystemBase
{
    private ComputeShader _shader;
    private RenderTexture _visibleTex;   // cleared each frame
    private RenderTexture _exploredTex;  // persistent, accumulative
    private ComputeBuffer _unitBuffer;

    private int _clearKernel;
    private int _stampKernel;
    private int _accumKernel;

    private bool _initialized = false;
    private const int MAX_UNITS = 20000;

    private BufferLookup<LinkedEntityGroup> _linkedEntitys;
    // persistent reusable array (no alloc each frame)
    private NativeArray<float3> _unitArray;

    protected override void OnDestroy()
    {
        if (_unitArray.IsCreated) _unitArray.Dispose();
        _unitBuffer?.Dispose();
        _visibleTex?.Release();
        _exploredTex?.Release();
        _mask.Dispose();
    }
    private FogOfWarRendering _fogRender;
    protected override void OnUpdate()
    {
        if (!SystemAPI.TryGetSingleton<MapData>(out MapData settings)) return;
        if (_fogRender == null) _fogRender = GameObject.FindAnyObjectByType<FogOfWarRendering>();
        if (!_initialized)
        {
            _shader = Resources.Load<ComputeShader>("FogOfWar");
            if (_shader == null)
            {
                Debug.LogError("FogOfWar compute shader missing.");
                Enabled = false;
                return;
            }

            _clearKernel = _shader.FindKernel("ClearVisible");
            _stampKernel = _shader.FindKernel("StampVisible");
            _accumKernel = _shader.FindKernel("AccumulateExplored");

            // persistent unit array + compute buffer
            _unitArray = new NativeArray<float3>(MAX_UNITS, Allocator.Persistent);
            _unitBuffer = new ComputeBuffer(MAX_UNITS, sizeof(float) * 3);

            // persistent explored texture
            _exploredTex = new RenderTexture(settings.Size.x, settings.Size.y, 0, RenderTextureFormat.RInt)
            {
                enableRandomWrite = true,
                filterMode = FilterMode.Point
            };
            _exploredTex.Create();

            // visible texture (cleared each frame)
            _visibleTex = new RenderTexture(settings.Size.x, settings.Size.y, 0, RenderTextureFormat.RInt)
            {
                enableRandomWrite = true,
                useMipMap = false,
                autoGenerateMips = false,
                filterMode = FilterMode.Point
            };
            _visibleTex.Create();

            // Hook renderer
            _fogRender?.SetTexture(_visibleTex,_exploredTex); // show explored by default (or combine in shader)

            _initialized = true;
            return; // skip first frame after init
        }

        if (!_initialized || _shader == null) return;

        // ---------------------------
        // Gather units into persistent array (no allocations)
        // ---------------------------
        int idx = 0;
        var playerData = SystemAPI.GetSingleton<LocalPlayerData>();
        foreach (var (vision, transform, team) in SystemAPI.Query<RefRO<Vision>, RefRO<LocalTransform>, RefRO<UnitTeam>>())
        {
            if (idx >= MAX_UNITS) break;

            if (team.ValueRO.TeamID == playerData.TeamID)
            {
                // Imp mov threshold
                float3 p = transform.ValueRO.Position;
                _unitArray[idx] = new float3(p.x, p.z, vision.ValueRO.Level);
                idx++;
            }

        }

        int count = math.min(idx, MAX_UNITS);
        if (count == 0) return;

        // Upload only the used range
        _unitBuffer.SetData(_unitArray, 0, 0, count);

        // precompute extents and pass to shader
        int extX = settings.Size.x / 2;
        int extZ = settings.Size.y / 2;

        // --------------------------------
        // 1) Clear the visible texture
        // --------------------------------
        _shader.SetTexture(_clearKernel, "_Visible", _visibleTex);
        _shader.SetInt("_FogWidth", _visibleTex.width);
        _shader.SetInt("_FogHeight", _visibleTex.height);
        int clearX = Mathf.CeilToInt((float)_visibleTex.width / 8f);
        int clearY = Mathf.CeilToInt((float)_visibleTex.height / 8f);
        _shader.Dispatch(_clearKernel, clearX, clearY, 1);

        // --------------------------------
        // 2) StampVisible: stamp offsets into visible texture
        // --------------------------------
        _shader.SetTexture(_stampKernel, "_Visible", _visibleTex);
        _shader.SetBuffer(_stampKernel, "_Units", _unitBuffer);
        _shader.SetInt("_UnitCount", count);
        _shader.SetInts("_FogDim", _visibleTex.width, _visibleTex.height);
        _shader.SetFloats("_WorldMin", -extX, 0, -extZ);
        _shader.SetFloats("_WorldMax", extX, 0, extZ);

        int groups = Mathf.CeilToInt(count / 64f);
        _shader.Dispatch(_stampKernel, groups, 1, 1);


        // 3) Accumulate into persistent explored texture
        _shader.SetTexture(_accumKernel, "_Visible", _visibleTex);
        _shader.SetTexture(_accumKernel, "_Explored", _exploredTex);

        int ax = Mathf.CeilToInt(_visibleTex.width / 8f);
        int ay = Mathf.CeilToInt(_visibleTex.height / 8f);
        _shader.Dispatch(_accumKernel, ax, ay, 1);


        //Debug.Log($"VisibleTex actual size = {_visibleTex.width}x{_visibleTex.height}");
        
        if (_hasMaskUpdate)
        {
            _hasMaskUpdate = false;
            float2 worldMin = (float2)settings.Size * -0.5f;
            var ecb = new EntityCommandBuffer(Allocator.TempJob);
            _linkedEntitys.Update(this);
            var job = new LocalVisibilityJob
            {
                ECB = ecb,
                Groups = _linkedEntitys,
                Mask = _mask,
                WorldMin = worldMin,
                CellSize = 1f,
                GridResolution = settings.Size.x,
            };
            var handle = job.Schedule(Dependency);
            handle.Complete();
            ecb.Playback(EntityManager);
            ecb.Dispose();
        }

        if (_readCount >= 16)
        {
            _readCount = 0;
            AsyncGPUReadback.Request(_visibleTex, 0, OnVisReadback);
        }
        else
        {
            _readCount += 1;
        }
        //gpu readback on visiblility texture
        
    }
    int _readCount = 0;
    bool _hasMaskUpdate = false;
    void OnVisReadback(AsyncGPUReadbackRequest req)
    {
        if (req.hasError || _visibleTex == null) return;

        // Get the raw returned bytes (may include padding)
        var raw = req.GetData<int>(); // NativeArray<byte>

        int texW = _visibleTex.width;   // expected width (e.g. 512)
        int texH = _visibleTex.height;  // expected height (e.g. 512)

        if (raw.Length == texW * texH)
        {
            // No padding — direct copy
            raw.CopyTo(_mask);
            _hasMaskUpdate = true;
            return;
        }

        // Compute row stride (bytes per row in the returned buffer).
        // This is the safe way: rowStride = totalBytes / height
        int rowStride = raw.Length / texH;

        // Copy only the texW bytes from each row into _mask (texW*texH elements)
        // _mask must be allocated with length texW * texH (Allocator.Persistent)
        for (int y = 0; y < texH; y++)
        {
            int srcRowStart = y * rowStride;
            int dstRowStart = y * texW;

            // Use Copy for each row (NativeArray has Copy)
            NativeArray<int>.Copy(raw, srcRowStart, _mask, dstRowStart, texW);
        }

        _hasMaskUpdate = true;
    }
    NativeArray<int> _mask;
    protected override void OnCreate()
    {
        _mask = new NativeArray<int>(512 * 512, Allocator.Persistent);
        _hasMaskUpdate = false;
        _linkedEntitys = GetBufferLookup<LinkedEntityGroup>(true);
    }

}
[BurstCompile]
public partial struct LocalVisibilityJob : IJobEntity
{
    public float2 WorldMin;
    public float CellSize;
    public int GridResolution;
    [ReadOnly]public NativeArray<int> Mask;
    [ReadOnly]public BufferLookup<LinkedEntityGroup> Groups;
    public EntityCommandBuffer ECB;
    public void Execute(Entity e, RefRO<LocalTransform> t, RefRW<LocalVisibility> vis)
    {
        //foreach (byte b in Mask) { if  (b != 0) { Debug.Log("WOWOWOWOWOWOW"); } }
        float3 pos = t.ValueRO.Position;
        int gx = (int)((pos.x - WorldMin.x) / CellSize);
        int gy = (int)((pos.z - WorldMin.y) / CellSize);
        int index = gy * GridResolution + gx;

        if (index > 0 && index <= Mask.Length)
        {
            if (vis.ValueRO.DisableChildren && Groups.TryGetBuffer(e, out var buffer))
            {
                foreach (var item in buffer)
                {
                    if (Mask[index] > 0)
                    {
                        ECB.RemoveComponent<DisableRendering>(item.Value);
                        vis.ValueRW.IsVisible = true;
                    }
                    else
                    {
                        ECB.AddComponent<DisableRendering>(item.Value);
                        vis.ValueRW.IsVisible = false;
                    }
                }

            }
            else
            {
                if (Mask[index] > 0)
                {
                    ECB.RemoveComponent<DisableRendering>(e);
                    vis.ValueRW.IsVisible = true;
                }
                else
                {
                    ECB.AddComponent<DisableRendering>(e);
                    vis.ValueRW.IsVisible = false;
                }
            }
        }


        
        //Debug.Log($"Tres sigma {Mask[index]} at {index} index");
    }
}
public struct LocalVisibility : IComponentData
{
    public bool IsVisible;
    public bool DisableChildren;
}