using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
public class SelectionGUIManager : MonoBehaviour
{

    public TextMeshProUGUI description;
    public GameObject GUIElement;

    private UnitGUIManifest manifest;
    
    private EntityManager entityManager;
    private EntityQuery query;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        manifest = FindFirstObjectByType<UnitGUIManifest>();
        World defaultWorld = World.DefaultGameObjectInjectionWorld;
        entityManager = defaultWorld.EntityManager;
        query = entityManager.CreateEntityQuery(typeof(LocalSelectedUnits));

        //subscribe to events
        InputBridge.OnUpdateGUI += UpdateGUI;
    }
    private void OnDestroy()
    {
        InputBridge.OnUpdateGUI -= UpdateGUI;
    }
    public void UpdateGUI()
    {
        List<GameObject> list = new List<GameObject>();
        for (int i = 0; i < transform.childCount; i++)
        {
            list.Add(transform.GetChild(i).gameObject);
        }
        foreach (GameObject go in list) { Destroy(go); }
        Invoke(nameof(ReadSelection), Time.deltaTime*2f);
    }

    void ReadSelection()
    {
        if (!query.TryGetSingleton(out LocalSelectedUnits localSelectedUnits))
            return;

        foreach (var bucket in localSelectedUnits.Buckets)
        {
            var data = manifest.GetData(bucket.Key);
            //Debug.Log($"{bucket.Count} of unit nameof {data.name}");
            var e = Instantiate(GUIElement, transform).GetComponent<UnitGUIElement>();
            e.SetData(data, bucket.Count);
        }
    }
}

/// <summary>
/// This things goal is to add all selction unique
/// selection datas to an entity;
/// </summary>
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

        foreach (var (team, key) in SystemAPI
            .Query<UnitTeam, SelectionKey>()
            .WithAll<UnitSelecetedTag>())
        {
            if (team.TeamID != teamID)
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
public struct LocalSelectedUnits : IComponentData
{
    //64 unique buckets
    public FixedList4096Bytes<SelectedUnitBucket> Buckets;
}
public struct SelectedUnitBucket
{
    public int Key;
    public int Count;
}