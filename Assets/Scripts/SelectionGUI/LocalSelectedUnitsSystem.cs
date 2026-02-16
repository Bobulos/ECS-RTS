
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Creates buckets also required for server opps
/// </summary>

//[UpdateInGroup(typeof(PresentationSystemGroup))]
//[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public partial struct SelectionGUIManagerSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        var e = state.EntityManager.CreateSingleton<LocalSelectedUnits>();
    }
    private int _teamID;
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingleton<LocalPlayerData>(out var data))
            return;

        if (!SystemAPI.TryGetSingletonEntity<LocalSelectedUnits>(out var entity))
            return;

        var newBuckets = new FixedList4096Bytes<SelectedUnitBucket>();
        int teamID = data.TeamID;

        foreach (var (team, key, selected) in SystemAPI
            .Query<Team, SelectionKey, Selected>())
        {
            if (!selected.Value || team.TeamID != teamID)
                continue;

            bool found = false;

            for (int i = 0; i < newBuckets.Length; i++)
            {
                if (newBuckets[i].Key == key.Value)
                {
                    var b = newBuckets[i];
                    b.Count++;
                    newBuckets[i] = b;
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                if (newBuckets.Length >= 64)
                    break;

                newBuckets.Add(new SelectedUnitBucket
                {
                    Key = key.Value,
                    Count = 1
                });
            }
        }
        //UnityEngine.Debug.Log($"Created {newBuckets.Length} buckets");
        state.EntityManager.SetComponentData(entity,
            new LocalSelectedUnits { Buckets = newBuckets });
    }
/*    private bool IsUniqueKey(FixedList4096Bytes<SelectedUnitBucket> d, int key)
    {
        foreach (var item in d)
        {
            if (item.Key == key)
            {
                return false;
            }
        }
        return true;
    }*/
}