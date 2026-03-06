using Unity.Entities;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Physics;
using Unity.Collections;
using Unity.NetCode;
using Unity.Rendering;
using Construction;
using Unity.Transforms;
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public partial struct ActionVisualSystem : ISystem
{
    private const float MAX_RAY_LENGTH = 500f;
    private CollisionFilter _terrainFilter;
    private CollisionFilter _structureFilter;
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _terrainFilter = new CollisionFilter
        {
            CollidesWith = 1 << 7,
            BelongsTo = CollisionFilter.Default.BelongsTo,
            GroupIndex = 0
        };
        _structureFilter = new CollisionFilter
        {
            CollidesWith = 1 << 8,
            BelongsTo = CollisionFilter.Default.BelongsTo,
            GroupIndex = 0
        };
        state.EntityManager.CreateSingleton<ActionVisualizationData>( new ActionVisualizationData { Data = new ActionUseData() });
    }
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingleton<ActionVisualizationData>(out var data)) return;
        if (data.Data.SelectionKey == -1) 
        {
            foreach (var (vis, entity) in SystemAPI.Query<StructureVisualTag>().WithEntityAccess())
            {
                state.EntityManager.DestroyEntity(entity);
            }
            return;
        }
        if (!SystemAPI.TryGetSingleton<ActionInfoManifest>(out var manifest)) return;
        if (!SystemAPI.TryGetSingleton<LocalSelectedUnits>(out var selectedUnits)) return;
        if (selectedUnits.Buckets.Length == 0) return;

        int targetKey = selectedUnits.Buckets[0].Key;
        int team = SystemAPI.GetSingleton<LocalPlayerData>().TeamID;

        var constructionData = SystemAPI.GetSingletonBuffer<ConstructionDataManifest>();
        //UnityEngine.Debug.Log($"list of {constructionData.Length}");
        //UnityEngine.Debug.Log($"ActionVisualSystem received data with selection key {data.Data.SelectionKey} and action index {data.Data.LocalActionIndex}");
        ActionInfo info = manifest.Blob.Value.UnitsActionInfo[data.Data.SelectionKey][data.Data.LocalActionIndex];
        //UnityEngine.Debug.Log($"Info of {info.PrefabIndex}");
        switch (info.ActionType)
        {
            case ActionType.BuildStructure:
                
                var cD = new ConstructionDataManifest();
                bool found = false;
                foreach (var ( key, uTeam, work, selected, entity) in SystemAPI.Query<
                    RefRO<SelectionKey>,
                    RefRO<Team>,
                    RefRW<Worker>,
                    RefRO<Selected>>().WithEntityAccess())
                {
                    if (uTeam.ValueRO.TeamID != team || !selected.ValueRO.Value || key.ValueRO.Value != targetKey) continue;
                    cD = constructionData[work.ValueRO.ConstructKeys[info.PrefabIndex]];
                    found = true;
                    break;
                }
                if (!found) break;
                VisualizeStructure(ref state, data.Data.RayOrigin, data.Data.RayDirection, cD.Size, cD.PrimaryKey);
                break;
        }
    }
    [BurstCompile]
    void VisualizeStructure(ref SystemState state, float3 origin, float3 direction, int3 size, int key)
    {
        if (!SystemAPI.TryGetSingleton<PhysicsWorldSingleton>(out var world)) return;

        // Get ground position from raycast
        float3 groundPos = GetGroundPositionFromRay(ref state, world, origin, direction);
        
        // Apply grid snapping
        float3 snappedPos = ConstructionUtil.SnapToGrid(world, groundPos, _terrainFilter);
        
        bool isValid = ConstructionUtil.CheckValidStructurePlacement(world, snappedPos, size, _structureFilter);

        // Update or create visual
        if (SystemAPI.TryGetSingletonEntity<StructureVisualTag>(out Entity vis))
        {
            state.EntityManager.SetComponentData(vis, new LocalTransform
            {
                Position = snappedPos,
                Scale = 1,
                Rotation = quaternion.identity,
            });
            SetValidMat(ref state, vis, isValid);
        }
        else
        {
            if (TryGetStructureFromDB(key, out var prefab))
            {
                var e = state.EntityManager.Instantiate(prefab);
                state.EntityManager.SetComponentData(e, new LocalTransform
                {
                    Position = snappedPos,
                    Scale = 1,
                    Rotation = quaternion.identity,
                });
                state.EntityManager.AddComponent<StructureVisualTag>(e);
                state.EntityManager.RemoveComponent<PhysicsCollider>(e);
                //vision
                state.EntityManager.RemoveComponent<Vision>(e);
                state.EntityManager.RemoveComponent<LocalVisibility>(e);
                state.EntityManager.RemoveComponent<GhostInstance>(e);
                if (state.EntityManager.HasComponent<ProductionStructure>(e))
                {
                    state.EntityManager.RemoveComponent<ProductionStructure>(e);
                }
                if (state.EntityManager.HasComponent<UnitHP>(e))
                {
                    state.EntityManager.RemoveComponent<UnitHP>(e);
                }
                SetValidMat(ref state, e, isValid);
            }
        }
    }
    [BurstCompile]
    float3 GetGroundPositionFromRay(ref SystemState state, PhysicsWorldSingleton world, float3 origin, float3 direction)
    {
        var rayIn = new RaycastInput
        {
            Start = origin,
            End = origin + (direction * MAX_RAY_LENGTH),
            Filter = _terrainFilter
        };

        if (world.CastRay(rayIn, out var hit))
        {
            return hit.Position;
        }
        
        return origin; // Fallback to origin if no hit
    }
    [BurstCompile]
    void SetValidMat(ref SystemState state, Entity e, bool valid)
    {
        if (!state.EntityManager.HasBuffer<LinkedEntityGroup>(e))
            return;

        DynamicBuffer<LinkedEntityGroup> buffer = state.EntityManager.GetBuffer<LinkedEntityGroup>(e);
        
        for (int i = 0; i < buffer.Length; i++)
        {
            Entity element = buffer[i].Value;

            if (state.EntityManager.HasComponent<MaterialMeshInfo>(element))
            {
                if (SystemAPI.TryGetSingleton<AssetSingleton>(out var m))
                {
                    int mat = valid ? m.ValidMaterialID : m.InvalidMaterialID;
                    var r = state.EntityManager.GetComponentData<MaterialMeshInfo>(element);
                    r.Material = mat;
                    state.EntityManager.SetComponentData(element, r);
                }
            }
        }
    }
    [BurstCompile]
    private bool TryGetStructureFromDB(int key, out Entity e)
    {
        if (SystemAPI.TryGetSingletonBuffer<StructureManifest>(out var structDb))
        {
            e = structDb[key].Value;
            return true;
        }
        e = Entity.Null;
        return false;
    }

}
public struct ActionVisualizationData : IComponentData
{
    public ActionUseData Data;
}