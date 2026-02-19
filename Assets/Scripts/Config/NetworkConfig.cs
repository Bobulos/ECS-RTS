using UnityEngine;

[CreateAssetMenu(fileName = "NetworkConfig", menuName = "Scriptable Objects/NetworkConfig")]
public class NetworkConfig : ScriptableObject
{
    public ushort port = 7979;
    public ushort networkTicks = 6;
}
