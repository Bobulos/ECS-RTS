using System.IO;
using UnityEngine;

public static class ConfigLoader
{
    public static T Load<T>(string path)
    {
        TextAsset textAsset = Resources.Load<TextAsset>(path);

        if (textAsset == null)
        {
            Debug.LogError($"Config not found in Resources: {path}");
            return default;
        }

        return JsonUtility.FromJson<T>(textAsset.text);
    }
}
[System.Serializable]
public class SimulationConfig
{
    public int TargetBucketCount;
    public int NavBucketCount;
    public int SpatialPartitionTargetCount;
}