#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class EntityManifestBuilder : MonoBehaviour
{
    [Header("Prefab Sources")]
    public string unitPath = "Assets/Prefabs/Units";
    public string structurePath = "Assets/Prefabs/Structures";

    [Header("Output Data")]
    public string unitDataPath = "Assets/Data/GUI/Units";
    public string structureDataPath = "Assets/Data/GUI/Structures";

    [ContextMenu("Build Entity Manifest")]
    void BuildManifestFromPrefabs()
    {
        if (Application.isPlaying) return;

        EnsureFolder(unitDataPath);
        EnsureFolder(structureDataPath);

        // Fixed: Get different component types from correct folders
        var prefabs = GetPrefabs<UnitAuthoring>(unitPath)
            .Concat(GetPrefabs<StructureAuthoring>(structurePath))
            .Concat(GetPrefabs<WallAuthoring>(structurePath))
            .Concat(GetPrefabs<ProductionStructureAuthoring>(structurePath))
            .Distinct()
            .ToList();

        Dictionary<Hash128, EntityData> dataById = ScriptableObjectUtil.LoadAllScriptableObjects<EntityData>()
            .Where(d => d.entityGuid.isValid)
            .ToDictionary(d => d.entityGuid); // Use GUID string instead of Hash128



        int created = 0;
        int modified = 0;

        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var prefab in prefabs)
            {
                if (prefab.TryGetComponent<UnitAuthoring>(out var unit))
                {
                    ProcessPrefab(
                        prefab,
                        unit,
                        unitDataPath,
                        dataById,
                        ref created,
                        ref modified,
                        PopulateUnitData
                    );
                }
                else if (prefab.TryGetComponent<WallAuthoring>(out var wall))
                {
                    ProcessPrefab(
                        prefab,
                        wall,
                        structureDataPath,
                        dataById,
                        ref created,
                        ref modified,
                        PopulateWallData
                    );
                }
                else if (prefab.TryGetComponent<ProductionStructureAuthoring>(out var prod))
                {
                    ProcessPrefab(
                        prefab,
                        prod,
                        structureDataPath,
                        dataById,
                        ref created,
                        ref modified,
                        PopulateProductionStructureData
                    );
                }
                else if (prefab.TryGetComponent<StructureAuthoring>(out var structure))
                {
                    ProcessPrefab(
                        prefab,
                        structure,
                        structureDataPath,
                        dataById,
                        ref created,
                        ref modified,
                        PopulateStructureData
                    );
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        Debug.Log($"Entity Manifest Complete — Created: {created}, Modified: {modified}");
    }

    // --------------------------------------------------
    // Core processing
    // --------------------------------------------------
    void ProcessPrefab<TAuthoring>(
        GameObject prefab,
        TAuthoring authoring,
        string outputPath,
        Dictionary<Hash128, EntityData> dataById,  // Use string GUID
        ref int created,
        ref int modified,
        System.Action<EntityData, TAuthoring, GameObject> populate
    ) where TAuthoring : Component
    {
        string assetPath = AssetDatabase.GetAssetPath(prefab);
        string guid = AssetDatabase.AssetPathToGUID(assetPath);
        Hash128 entityGuid = Hash128.Parse(guid);
        if (string.IsNullOrEmpty(assetPath))
        {
            Debug.LogWarning($"Could not get asset path for prefab: {prefab.name}");
            return;
        }

        // Use the GUID directly as a string - this is unique per asset
        //string entityGuid = AssetDatabase.AssetPathToGUID(assetPath);
        
        Debug.Log($"Processing {prefab.name} with GUID: {entityGuid}");

        if (!dataById.TryGetValue(entityGuid, out var data))
        {
            data = ScriptableObject.CreateInstance<EntityData>();
            data.name = prefab.name;
            data.entityGuid = entityGuid;  // Store as string

            string fullPath = $"{outputPath}/{prefab.name}.asset";
            AssetDatabase.CreateAsset(data, fullPath);
            dataById.Add(entityGuid, data);
            created++;
            Debug.Log($"Created new EntityData for {prefab.name}");
        }
        else
        {
            Undo.RecordObject(data, "Update Entity Data");
            modified++;
            Debug.Log($"Modified existing EntityData for {prefab.name}");
        }

        populate(data, authoring, prefab);
        EditorUtility.SetDirty(data);
    }

    // --------------------------------------------------
    // Population
    // --------------------------------------------------
    void PopulateUnitData(EntityData data, UnitAuthoring authoring, GameObject prefab)
    {
        data.entityType = EntityType.Unit;
        data.prefab = prefab;
        authoring.data = data;
        // data.displayName = authoring.displayName;
        // data.icon = authoring.icon;
        // data.entityType = EntityType.Unit;
    }

    void PopulateStructureData(EntityData data, StructureAuthoring authoring, GameObject prefab)
    {
        data.entityType = EntityType.Structure;
        data.prefab = prefab;
        authoring.data = data;
        // data.displayName = authoring.displayName;
        // data.entityType = EntityType.Structure;
    }

    void PopulateWallData(EntityData data, WallAuthoring authoring, GameObject prefab)
    {
        data.entityType = EntityType.Structure;
        data.prefab = prefab;
        authoring.data = data;
        // data.displayName = authoring.displayName;
        // data.entityType = EntityType.Wall;
    }

    void PopulateProductionStructureData(EntityData data, ProductionStructureAuthoring authoring, GameObject prefab)
    {
        data.entityType = EntityType.Structure;
        data.prefab = prefab;
        authoring.data = data;
        // data.displayName = authoring.displayName;
        // data.entityType = EntityType.ProductionStructure;
    }

    // --------------------------------------------------
    // Utilities
    // --------------------------------------------------
    void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        string[] folders = path.Split('/');
        string currentPath = folders[0];

        for (int i = 1; i < folders.Length; i++)
        {
            string newPath = currentPath + "/" + folders[i];
            if (!AssetDatabase.IsValidFolder(newPath))
            {
                AssetDatabase.CreateFolder(currentPath, folders[i]);
            }
            currentPath = newPath;
        }
    }

    List<GameObject> GetPrefabs<T>(string folder) where T : Component
    {
        if (!AssetDatabase.IsValidFolder(folder))
        {
            Debug.LogWarning($"Folder does not exist: {folder}");
            return new List<GameObject>();
        }

        return AssetDatabase.FindAssets("t:Prefab", new[] { folder })
            .Select(guid => AssetDatabase.LoadAssetAtPath<GameObject>(
                AssetDatabase.GUIDToAssetPath(guid)))
            .Where(p => p != null && p.GetComponent<T>() != null)
            .ToList();
    }


}
#endif