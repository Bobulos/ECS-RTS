using UnityEngine;

public static class SimConfigLoader
{
    private const string SIM_CONFIG_PATH = "Default";
    private static SimulationConfig simConfig;
    public static SimulationConfig LoadSim()
    {
        if (simConfig == null)
        {
            simConfig = Resources.Load<SimulationConfig>(SIM_CONFIG_PATH);
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
public static class NetworkConfigLoader
{
    private const string NETWORK_CONFIG_PATH = "Default";
    private static NetworkConfig networkConfig;
    public static NetworkConfig LoadNetwork()
    {
        if (networkConfig == null)
        {
            networkConfig = Resources.Load<NetworkConfig>(NETWORK_CONFIG_PATH);
        }
        return networkConfig;
    }
}
/*[System.Serializable]
public class SimulationConfig
{
    public int TargetBucketCount;
    public int NavBucketCount;
    public int SpatialPartitionTargetCount;
}*/