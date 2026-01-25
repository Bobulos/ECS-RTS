using UnityEngine;

[CreateAssetMenu(fileName = "SimulationConfig", menuName = "Scriptable Objects/SimulationConfig")]
public class SimulationConfig : ScriptableObject
{
    [Header("Local Avoidance")]
    public float timeHorizon;
    public int spatialPartitionTargetCount;

    [Header("Target Finding")]
    public int targetBucketCount;
    public float cellSize = 10f;

    [Header("Navigation Avoidance")]
    public int navBucketCount;
    
}
