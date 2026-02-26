using Unity.Entities;
using UnityEngine;
using Unity.NetCode;

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public partial class DebugGhostCollectionSystem : SystemBase
{
    protected override void OnUpdate()
    {
        if (!SystemAPI.TryGetSingletonEntity<GhostCollection>(out var collectionEntity))
        {
            Debug.Log("No GhostCollection yet");
            return;
        }

        var ghostPrefabs = EntityManager.GetBuffer<GhostCollectionPrefab>(collectionEntity);
        Debug.Log($"Ghost collection has {ghostPrefabs.Length} prefabs:");
        for (int i = 0; i < ghostPrefabs.Length; i++)
        {
            var prefab = ghostPrefabs[i];
            Debug.Log($"  [{i}] Entity: {prefab.GhostPrefab}, Hash: {prefab.GhostType}");
        }

        Enabled = false; // only run once
    }
}