using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

public class SelectionGUIManager : MonoBehaviour
{
    public TextMeshProUGUI description;
    public GameObject GUIElementPrefab;
    
    [Header("Pool Settings")]
    public int initialPoolSize = 10;

    private EntityGUIManifest manifest;
    private EntityManager entityManager;
    private EntityQuery query;
    
    // Object pool
    private Queue<UnitGUIElement> pool = new Queue<UnitGUIElement>();
    private List<UnitGUIElement> activeElements = new List<UnitGUIElement>();
    
    private Coroutine updateCoroutine;

    void Start()
    {
        manifest = FindFirstObjectByType<EntityGUIManifest>();
        World defaultWorld = World.DefaultGameObjectInjectionWorld;
        entityManager = defaultWorld.EntityManager;
        query = entityManager.CreateEntityQuery(typeof(LocalSelectedUnits));

        // Initialize pool
        InitializePool();

        // Subscribe to events
        InputBridge.OnUpdateGUI += OnUpdateGUI;
    }

    private void OnDestroy()
    {
        InputBridge.OnUpdateGUI -= OnUpdateGUI;
        
        // Stop coroutine if running
        if (updateCoroutine != null)
        {
            StopCoroutine(updateCoroutine);
        }
    }

    void InitializePool()
    {
        for (int i = 0; i < initialPoolSize; i++)
        {
            CreateNewPooledElement();
        }
    }

    UnitGUIElement CreateNewPooledElement()
    {
        GameObject go = Instantiate(GUIElementPrefab, transform);
        UnitGUIElement element = go.GetComponent<UnitGUIElement>();
        go.SetActive(false);
        pool.Enqueue(element);
        return element;
    }

    UnitGUIElement GetFromPool()
    {
        if (pool.Count == 0)
        {
            return CreateNewPooledElement();
        }

        UnitGUIElement element = pool.Dequeue();
        element.gameObject.SetActive(true);
        activeElements.Add(element);
        return element;
    }

    void ReturnToPool(UnitGUIElement element)
    {
        element.gameObject.SetActive(false);
        activeElements.Remove(element);
        pool.Enqueue(element);
    }

    void ReturnAllToPool()
    {
        // Use a copy to avoid modification during iteration
        var elementsToReturn = new List<UnitGUIElement>(activeElements);
        
        foreach (var element in elementsToReturn)
        {
            ReturnToPool(element);
        }
        
        activeElements.Clear();
    }
    public void OnUpdateGUI()
    {
        // Stop any existing coroutine
        if (updateCoroutine != null)
        {
            StopCoroutine(updateCoroutine);
        }
        
        // Start new update coroutine
        updateCoroutine = StartCoroutine(UpdateGUICoroutine());
    }

    IEnumerator UpdateGUICoroutine()
    {
        // Return all active elements to pool
        ReturnAllToPool();
        
        // Wait for end of frame to ensure all ECS systems have run
        yield return new WaitForEndOfFrame();
        
        // Read selection data
        if (!query.TryGetSingleton(out LocalSelectedUnits selectedUnits))
        {
            yield break;
        }

        int elementIndex = 0;
        foreach (var bucket in selectedUnits.Buckets)
        {
            var data = manifest.GetData(bucket.Key);
            
            if (data != null)
            {
                UnitGUIElement element = GetFromPool();
                element.SetData(data, bucket.Count);
                element.transform.SetSiblingIndex(elementIndex++);
            }
        }
        
        updateCoroutine = null;
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
            .Query<Team, SelectionKey>()
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