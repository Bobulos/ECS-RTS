using UnityEngine;

/// <summary>
/// Load configs from resource because it will be expected later
/// Avoids null reference exceptions
/// </summary>
public class ConfigBootstrap : MonoBehaviour
{    void Start()
    {
        NetworkConfigLoader.LoadNetwork();
        SimConfigLoader.LoadSim();
    }
}
