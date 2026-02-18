using Unity.NetCode;
using UnityEngine;
using Unity.Entities;
[UnityEngine.Scripting.Preserve]
public class ConnectionBootstrap : ClientServerBootstrap
{
    public override bool Initialize(string defaultWorldName)
    {
        AutoConnectPort = 0;

        // Create local aditional worlds here, if needed

        return base.Initialize(defaultWorldName);
    }
}
