using UnityEngine;

public static class ConfigLoader
{
    private const string SIM_SETTINGS_PATH = "Default";
    private static SimulationConfig simConfig;
    public static SimulationConfig LoadSim()
    {
        if (simConfig == null)
        {
            simConfig = Resources.Load<SimulationConfig>(SIM_SETTINGS_PATH);
        }
        return simConfig;
        
        /*TextAsset textAsset = Resources.Load<TextAsset>(path);

        if (textAsset == null)
        {
            Debug.LogError($"Config not found in Resources: {path}");
            return default;
        }

        return JsonUtility.FromJson<T>(textAsset.text);*/
    }
}
/*[System.Serializable]
public class SimulationConfig
{
    public int TargetBucketCount;
    public int NavBucketCount;
    public int SpatialPartitionTargetCount;
}*/