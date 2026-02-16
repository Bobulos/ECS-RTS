using Unity.NetCode;
using UnityEngine;
[UnityEngine.Scripting.Preserve]
public class ConnectionBootstrap : ClientServerBootstrap
{
    public override bool Initialize(string defaultWorldName)
    {
        AutoConnectPort = 7979;
        return base.Initialize(defaultWorldName);
    }
}
