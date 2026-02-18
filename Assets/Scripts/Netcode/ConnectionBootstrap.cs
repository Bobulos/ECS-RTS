using Unity.NetCode;
using UnityEngine;
using Unity.Entities;
[UnityEngine.Scripting.Preserve]
public class ConnectionBootstrap : ClientServerBootstrap
{
    public override bool Initialize(string defaultWorldName)
    {
        AutoConnectPort = 0;

        var args = System.Environment.GetCommandLineArgs();
        bool isServer = System.Array.IndexOf(args, "-server") >= 0;
        bool isHost = System.Array.IndexOf(args, "-host") >= 0;

        if (isServer)
        {
            CreateServerWorld("ServerWorld");
            return true;
        }

        if (isHost)
        {
            // Both worlds — host plays and serves
            CreateServerWorld("ServerWorld");
            CreateClientWorld("ClientWorld");
            return true;
        }

        // Pure client
        CreateClientWorld("ClientWorld");
        return true;
    }
}