#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
public static class ScriptableObjectUtil
{
    public static List<T> LoadAllScriptableObjects<T>() where T : ScriptableObject
    {
        return AssetDatabase.FindAssets($"t:{typeof(T).Name}")
            .Select(g => AssetDatabase.LoadAssetAtPath<T>(
                AssetDatabase.GUIDToAssetPath(g)))
            .Where(a => a != null)
            .ToList();
    }
}
#endif