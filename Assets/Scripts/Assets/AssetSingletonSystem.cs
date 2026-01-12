using Unity.Entities;
using Unity.Rendering;

public partial struct AssetSingletonSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        // 1. Check if the Singleton already exists (Assertion Guard)
        // If it exists, disable the system and exit immediately.
        if (SystemAPI.HasSingleton<AssetSingleton>())
        {
            state.Enabled = false;
            return;
        }

        Entity toDestroy = Entity.Null;

        // 2. Query for the temporary initialization component
        // Use RefRO<T> for safety and performance
        foreach (var (init, initEntity) in SystemAPI.Query<RefRO<AssetSingletonInit>>().WithEntityAccess())
        {
            // 3. Read the Material IDs using the EntityManager
            // The MaterialMeshInfo component was added during baking (TransformUsageFlags.Renderable)

            var validMaterialID = state.EntityManager.GetComponentData<MaterialMeshInfo>(init.ValueRO.ValidMaterialEntity).Material;
            var invalidMaterialID = state.EntityManager.GetComponentData<MaterialMeshInfo>(init.ValueRO.InvalidMaterialEntity).Material;

            // 4. Create the NEW dedicated Singleton Entity
            var singletonEntity = state.EntityManager.CreateEntity(typeof(AssetSingleton));

            // 5. Set the final component data
            state.EntityManager.SetComponentData(singletonEntity, new AssetSingleton
            {
                SelectedVisual = init.ValueRO.SelectedVisual,
                ValidMaterialID = validMaterialID, // Now correctly using int
                InvalidMaterialID = invalidMaterialID // Now correctly using int
            });

            toDestroy = initEntity; // Mark the temporary initializer entity for destruction
            break;                  // Stop the loop immediately (only one singleton initializer expected)
        }

        // 6. Clean up: Destroy the temporary initializer entity
        if (toDestroy != Entity.Null)
        {
            state.EntityManager.DestroyEntity(toDestroy);

            // 7. Critical: Disable the system so it never runs again and avoids the assertion error
            state.Enabled = false;
        }
    }
}