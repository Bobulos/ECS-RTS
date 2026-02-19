using Unity.NetCode;
using UnityEngine;
using Unity.Entities;
[UnityEngine.Scripting.Preserve]
public class ConnectionBootstrap : ClientServerBootstrap
{
    public override bool Initialize(string defaultWorldName)
    {
        // We want to do it manually
        return false;
    }
}