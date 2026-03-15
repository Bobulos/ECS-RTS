using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
[BurstCompile]
public partial class SelectionVisualSystem : SystemBase
{
    private EntityQuery _needsVisualQuery;
    private EntityQuery _removeVisualQuery;

    protected override void OnCreate()
    {
        // Units that are selected but have no visual child yet
        _needsVisualQuery = SystemAPI.QueryBuilder()
            .WithAll<Selected>()
            .WithNone<HasSelectionVisual>()
            .Build();

        // Visual entities whose parent is no longer selected
        _removeVisualQuery = SystemAPI.QueryBuilder()
            .WithAll<SelectedVisualTag, Parent>()
            .Build();
    }

    [BurstCompile]
    protected override void OnUpdate()
    {
        if (!SystemAPI.TryGetSingleton<AssetSingleton>(out var assetSingleton))
            return;

        var ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(World.Unmanaged);

        var selectedLookup   = SystemAPI.GetComponentLookup<Selected>(true);
        var hasVisualLookup  = SystemAPI.GetComponentLookup<HasSelectionVisual>(true);
        var structureLookup  = SystemAPI.GetComponentLookup<StructureTag>(true);

        // ── Add visuals ──────────────────────────────────────────────────────
        new AddSelectionVisualJob
        {
            ECB            = ecb.AsParallelWriter(),
            VisualPrefab   = assetSingleton.SelectedVisual,
            StructureLookup = structureLookup,
        }.ScheduleParallel(_needsVisualQuery);

        // ── Remove visuals ───────────────────────────────────────────────────
        new RemoveSelectionVisualJob
        {
            ECB            = ecb.AsParallelWriter(),
            SelectedLookup = selectedLookup,
        }.ScheduleParallel(_removeVisualQuery);
    }
}

/// <summary>
/// Spawns a selection visual for every selected unit that doesn't have one yet.
/// A <see cref="HasSelectionVisual"/> tag is added to the unit so the query
/// naturally excludes it on subsequent frames — no child-buffer scan needed.
/// </summary>
[BurstCompile]
public partial struct AddSelectionVisualJob : IJobEntity
{
    public EntityCommandBuffer.ParallelWriter ECB;
    public Entity VisualPrefab;

    [ReadOnly] public ComponentLookup<StructureTag> StructureLookup;

    private void Execute(
        Entity unit,
        [ChunkIndexInQuery] int sortKey,
        in Selected selected)
    {
        if (!selected.Value) return;

        float scale = StructureLookup.HasComponent(unit) ? 2.3f : 1f;

        Entity visual = ECB.Instantiate(sortKey, VisualPrefab);

        ECB.AddComponent(sortKey, visual, new Parent { Value = unit });
        ECB.SetComponent(sortKey, visual, new LocalTransform
        {
            Position = float3.zero,
            Rotation = quaternion.identity,
            Scale    = scale,
        });
        ECB.AddComponent<SelectedVisualTag>(sortKey, visual);

        // Tag the unit so this job won't touch it again next frame
        ECB.AddComponent<HasSelectionVisual>(sortKey, unit);
    }
}

/// <summary>
/// Destroys selection visuals whose parent unit is no longer selected (or gone).
/// Also removes the <see cref="HasSelectionVisual"/> marker from the parent so
/// a visual will be re-added if the unit is selected again.
/// </summary>
[BurstCompile]
public partial struct RemoveSelectionVisualJob : IJobEntity
{
    public EntityCommandBuffer.ParallelWriter ECB;

    [ReadOnly] public ComponentLookup<Selected> SelectedLookup;

    private void Execute(
        Entity visual,
        [ChunkIndexInQuery] int sortKey,
        in Parent parent,
        in SelectedVisualTag _)
    {
        bool shouldRemove =
            !SelectedLookup.TryGetComponent(parent.Value, out var selected)
            || !selected.Value;

        if (!shouldRemove) return;

        ECB.DestroyEntity(sortKey, visual);

        // Clean up the marker on the parent (if it still exists)
        if (SelectedLookup.HasComponent(parent.Value))
            ECB.RemoveComponent<HasSelectionVisual>(sortKey, parent.Value);
    }
}

/// <summary>Zero-size tag placed on a unit while it has a selection visual.</summary>
public struct HasSelectionVisual : IComponentData { }