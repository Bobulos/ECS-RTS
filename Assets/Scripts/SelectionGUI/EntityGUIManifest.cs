using UnityEditor;
using System.Linq;
using UnityEngine;
public class EntityGUIManifest : MonoBehaviour
{
    [SerializeField]
    private EntityData[] manifest;

    #if UNITY_EDITOR

    [ContextMenu("Update manifest")]
    public void UpdateManifest()
    {
        var data = ScriptableObjectUtil.LoadAllScriptableObjects<EntityData>();
        
        manifest = data
            .OrderBy(e => e.entityGuid)
            .ToArray();
        
        // Update each entity's key to match array index
        for (int i = 0; i < manifest.Length; i++)
        {
            manifest[i].key = i;
            EditorUtility.SetDirty(manifest[i]);
        }
        
        AssetDatabase.SaveAssets();
    }
    #endif
    public EntityData GetData(int key)
    {
        return manifest[key];
    }
    public bool TryGetData(int key, out EntityData data)
    {
        if (key >= 0 && key < manifest.Length)
        {
            data = manifest[key];
            return true;
        }
        data = null;
        return false;
    }
}
