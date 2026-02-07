#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class ManifestBuilder : MonoBehaviour
{
    [Header("Prefab Sources")]
    public string _unitPath = "Assets/Prefabs/Units";
    public string _structurePath = "Assets/Prefabs/Structures";

    [Header("Output Data")]
    public string _iconPath = "Assets/Entitys/Icons";
    public string _unitDataPath = "Assets/Data/GUI/Units";
    public string _structureDataPath = "Assets/Data/GUI/Structures";
    public string _constructionDataPath = "Assets/Data/Construction";

    // [ContextMenu("Build Full Manifest")]
    // void BuildFullManifest()
    // {
    //     BuildEntityData();
    //     BuildConstructionData();
    //     GenerateIcons();
    // }

    [ContextMenu("Build Entity Data")]
    void BuildEntityData()
    {
        if (Application.isPlaying) return;

        EnsureFolder(_unitDataPath);
        EnsureFolder(_structureDataPath);

        var prefabs = GetPrefabs<UnitAuthoring>(_unitPath)
            .Concat(GetPrefabs<StructureAuthoring>(_structurePath))
            .Concat(GetPrefabs<WallAuthoring>(_structurePath))
            .Concat(GetPrefabs<ProductionStructureAuthoring>(_structurePath))
            .Distinct()
            .ToList();

        Dictionary<Hash128, EntityData> dataById = ScriptableObjectUtil.LoadAllScriptableObjects<EntityData>()
            .Where(d => d.entityGuid.isValid)
            .ToDictionary(d => d.entityGuid);

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
                        _unitDataPath,
                        dataById,
                        ref created,
                        ref modified,
                        PopulateUnitData
                    );
                }
                if (prefab.TryGetComponent<WallAuthoring>(out var wall))
                {
                    ProcessPrefab(
                        prefab,
                        wall,
                        _structureDataPath,
                        dataById,
                        ref created,
                        ref modified,
                        PopulateWallData
                    );
                }
                if (prefab.TryGetComponent<ProductionStructureAuthoring>(out var prod))
                {
                    ProcessPrefab(
                        prefab,
                        prod,
                        _structureDataPath,
                        dataById,
                        ref created,
                        ref modified,
                        PopulateProductionStructureData
                    );
                }
                if (prefab.TryGetComponent<StructureAuthoring>(out var structure))
                {
                    ProcessPrefab(
                        prefab,
                        structure,
                        _structureDataPath,
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

        Debug.Log($"Entity Data Complete — Created: {created}, Modified: {modified}");
    }

    [ContextMenu("Build Construction Data Only")]
    void BuildConstructionData()
    {
        if (Application.isPlaying) return;

        EnsureFolder(_constructionDataPath);

        var prefabs = GetPrefabs<StructureAuthoring>(_structurePath)
            .Concat(GetPrefabs<WallAuthoring>(_structurePath))
            .Concat(GetPrefabs<ProductionStructureAuthoring>(_structurePath))
            .Distinct()
            .ToList();

        Dictionary<Hash128, ConstructionData> constructionDataById = ScriptableObjectUtil.LoadAllScriptableObjects<ConstructionData>()
            .Where(d => d.Guid.isValid)
            .ToDictionary(d => d.Guid);

        int created = 0;
        int modified = 0;

        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var prefab in prefabs)
            {
                ProcessConstructionData(prefab, constructionDataById, ref created, ref modified);
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        Debug.Log($"Construction Data Complete — Created: {created}, Modified: {modified}");
    }

    [ContextMenu("Generate Icons Only")]
    void GenerateIcons()
    {
        if (Application.isPlaying) return;

        EnsureFolder(_iconPath);

        var prefabs = GetPrefabs<UnitAuthoring>(_unitPath)
            .Concat(GetPrefabs<StructureAuthoring>(_structurePath))
            .Concat(GetPrefabs<WallAuthoring>(_structurePath))
            .Concat(GetPrefabs<ProductionStructureAuthoring>(_structurePath))
            .Distinct()
            .ToList();

        int generated = 0;
        int skipped = 0;

        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var prefab in prefabs)
            {
                string genPath = $"{_iconPath}/{prefab.name}.png";
                Texture2D existingIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(genPath);
                
                if (existingIcon != null)
                {
                    skipped++;
                    continue;
                }

                Texture2D icon = CreatePrefabIcon(prefab);
                if (icon != null)
                {
                    generated++;
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        Debug.Log($"Icon Generation Complete — Generated: {generated}, Skipped: {skipped}");
    }

    [ContextMenu("Regenerate All Icons(Recomended)")]
    void RegenerateAllIcons()
    {
        if (Application.isPlaying) return;

        EnsureFolder(_iconPath);

        var prefabs = GetPrefabs<UnitAuthoring>(_unitPath)
            .Concat(GetPrefabs<StructureAuthoring>(_structurePath))
            .Concat(GetPrefabs<WallAuthoring>(_structurePath))
            .Concat(GetPrefabs<ProductionStructureAuthoring>(_structurePath))
            .Distinct()
            .ToList();

        int generated = 0;

        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var prefab in prefabs)
            {
                // Delete existing icon if it exists
                string genPath = $"{_iconPath}/{prefab.name}.png";
                if (AssetDatabase.LoadAssetAtPath<Texture2D>(genPath) != null)
                {
                    AssetDatabase.DeleteAsset(genPath);
                }

                Texture2D icon = CreatePrefabIcon(prefab);
                if (icon != null)
                {
                    generated++;
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        Debug.Log($"Icon Regeneration Complete — Generated: {generated}");
    }

    // --------------------------------------------------
    // Core processing
    // --------------------------------------------------
    void ProcessPrefab<TAuthoring>(
        GameObject prefab,
        TAuthoring authoring,
        string outputPath,
        Dictionary<Hash128, EntityData> dataById,
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
        
        Debug.Log($"Processing {prefab.name} with GUID: {entityGuid}");

        if (!dataById.TryGetValue(entityGuid, out var data))
        {
            data = ScriptableObject.CreateInstance<EntityData>();
            data.name = prefab.name;
            data.entityGuid = entityGuid;

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

    void ProcessConstructionData(
        GameObject prefab,
        Dictionary<Hash128, ConstructionData> constructionDataById,
        ref int created,
        ref int modified
    )
    {
        string assetPath = AssetDatabase.GetAssetPath(prefab);
        string guid = AssetDatabase.AssetPathToGUID(assetPath);
        Hash128 entityGuid = Hash128.Parse(guid);
        
        if (string.IsNullOrEmpty(assetPath))
        {
            Debug.LogWarning($"Could not get asset path for prefab: {prefab.name}");
            return;
        }

        Debug.Log($"Processing ConstructionData for {prefab.name} with GUID: {entityGuid}");

        if (!constructionDataById.TryGetValue(entityGuid, out var constructionData))
        {
            constructionData = ScriptableObject.CreateInstance<ConstructionData>();
            constructionData.name = prefab.name;
            constructionData.Guid = entityGuid;

            string fullPath = $"{_constructionDataPath}/{prefab.name}.asset";
            AssetDatabase.CreateAsset(constructionData, fullPath);
            constructionDataById.Add(entityGuid, constructionData);
            created++;
            Debug.Log($"Created new ConstructionData for {prefab.name}");
        }
        else
        {
            Undo.RecordObject(constructionData, "Update Construction Data");
            modified++;
            Debug.Log($"Modified existing ConstructionData for {prefab.name}");
        }

        EditorUtility.SetDirty(constructionData);
    }

    // --------------------------------------------------
    // Population
    // --------------------------------------------------
    void PopulateUnitData(EntityData data, UnitAuthoring authoring, GameObject prefab)
    {
        data.entityType = EntityType.Unit;
        data.prefab = prefab;
        data.icon = GetOrCreatePrefabIcon(prefab);
        authoring.data = data;
    }

    void PopulateStructureData(EntityData data, StructureAuthoring authoring, GameObject prefab)
    {
        data.entityType = EntityType.Structure;
        data.prefab = prefab;
        data.icon = GetOrCreatePrefabIcon(prefab);
        authoring.data = data;
    }

    void PopulateWallData(EntityData data, WallAuthoring authoring, GameObject prefab)
    {
        data.entityType = EntityType.Structure;
        data.prefab = prefab;
        data.icon = GetOrCreatePrefabIcon(prefab);
        authoring.data = data;
    }

    void PopulateProductionStructureData(EntityData data, ProductionStructureAuthoring authoring, GameObject prefab)
    {
        data.entityType = EntityType.Structure;
        data.prefab = prefab;
        data.icon = GetOrCreatePrefabIcon(prefab);
        authoring.data = data;
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

    private Texture2D GetOrCreatePrefabIcon(GameObject prefab)
    {
        if (prefab == null) return null;
        
        string genPath = $"{_iconPath}/{prefab.name}.png";
        Texture2D existingIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(genPath);
        
        if (existingIcon != null)
        {
            return existingIcon;
        }
        
        return CreatePrefabIcon(prefab);
    }

    private Texture2D CreatePrefabIcon(GameObject prefab)
    {
        if (prefab == null) return null;

        string genPath = $"{_iconPath}/{prefab.name}.png";
        
        // Generate new icon
        Texture2D preview = AssetPreview.GetAssetPreview(prefab);
        if (preview == null) return null;
        
        // Create persistent copy
        Texture2D copy = new Texture2D(preview.width, preview.height, TextureFormat.RGBA32, false);
        
        RenderTexture rt = RenderTexture.GetTemporary(preview.width, preview.height);
        Graphics.Blit(preview, rt);
        
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;
        
        copy.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        copy.Apply();
        
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);
        
        // Save as PNG asset
        if (!System.IO.Directory.Exists(_iconPath))
        {
            System.IO.Directory.CreateDirectory(_iconPath);
        }
        
        byte[] bytes = copy.EncodeToPNG();
        System.IO.File.WriteAllBytes(genPath, bytes);
        AssetDatabase.ImportAsset(genPath);
        
        // Clean up temporary texture
        Object.DestroyImmediate(copy);
        
        // Return the saved asset
        return AssetDatabase.LoadAssetAtPath<Texture2D>(genPath);
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