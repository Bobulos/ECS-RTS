using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using Unity.NetCode;

public class SelectionGUIManager : MonoBehaviour
{
    public TextMeshProUGUI description;
    public GameObject GUIElementPrefab;
    
    [Header("Pool Settings")]
    public int initialPoolSize = 10;
    
    [Header("Update Settings")]
    public float updateInterval = 0.1f; // Update every 0.1 seconds

    private EntityGUIManifest manifest;
    private EntityManager entityManager;
    private EntityQuery query;
    
    // Object pool
    private Queue<UnitGUIElement> pool = new Queue<UnitGUIElement>();
    private List<UnitGUIElement> activeElements = new List<UnitGUIElement>();
    
    private Coroutine updateCoroutine;
    
    // Cache for detecting changes
    private Dictionary<int, int> lastBucketData = new Dictionary<int, int>();

    void Start()
    {
        manifest = FindFirstObjectByType<EntityGUIManifest>();
        World defaultWorld = ClientServerBootstrap.ClientWorld;
        entityManager = defaultWorld.EntityManager;
        query = entityManager.CreateEntityQuery(typeof(LocalSelectedUnits));

        // Initialize pool
        InitializePool();

        // Subscribe to events (optional - for immediate updates)
        InputBridge.OnUpdateGUI += OnUpdateGUI;
        
        // Start continuous update
        updateCoroutine = StartCoroutine(ContinuousUpdateCoroutine());
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
    
    // Continuous update coroutine
    IEnumerator ContinuousUpdateCoroutine()
    {
        while (true)
        {
            UpdateGUI();
            yield return new WaitForSeconds(updateInterval);
        }
    }
    
    // Immediate event update
    public void OnUpdateGUI()
    {
        UpdateGUI();
    }

    void UpdateGUI()
    {
        // Read selection data
        if (!query.TryGetSingleton(out LocalSelectedUnits selectedUnits))
        {
            // No selection data - clear GUI if there are active elements
            if (activeElements.Count > 0)
            {
                ReturnAllToPool();
                lastBucketData.Clear();
            }
            return;
        }

        // Check if data has changed
        if (!HasSelectionChanged(selectedUnits))
        {
            return; // No changes, skip update
        }

        // Return all active elements to pool
        ReturnAllToPool();

        // Update the GUI
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
        
        // Update cache
        UpdateCache(selectedUnits);
    }
    
    bool HasSelectionChanged(LocalSelectedUnits selectedUnits)
    {
        // Quick check: different number of buckets
        if (lastBucketData.Count != selectedUnits.Buckets.Length)
        {
            return true;
        }
        
        // Check each bucket
        foreach (var bucket in selectedUnits.Buckets)
        {
            if (!lastBucketData.TryGetValue(bucket.Key, out int cachedCount) || 
                cachedCount != bucket.Count)
            {
                return true;
            }
        }
        
        return false;
    }
    
    void UpdateCache(LocalSelectedUnits selectedUnits)
    {
        lastBucketData.Clear();
        
        foreach (var bucket in selectedUnits.Buckets)
        {
            lastBucketData[bucket.Key] = bucket.Count;
        }
    }
}

/// <summary>
/// This things goal is to add all selection unique
/// selection datas to an entity;
/// </summary>
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