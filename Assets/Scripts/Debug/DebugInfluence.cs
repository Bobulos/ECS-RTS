using UnityEngine;
using UnityEditor;
using Unity.Entities;
using Unity.NetCode;
using AICommander;
using Unity.Mathematics;
public class DebugInfluence : MapLoadedAccess
{
    private World _clientWorld;
    private EntityQuery _influenceQuery;
    private bool _initialized = false;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public override void OnLoad()
    {
        _initialized = true;
        _clientWorld = ClientServerBootstrap.ClientWorld;
        _influenceQuery = _clientWorld.EntityManager.CreateEntityQuery(typeof(InfluenceMap));
    }

    // Update is called once per frame
    #if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (_initialized && _influenceQuery.TryGetSingleton<InfluenceMap>(out var m))
        {
            //float halfSize = 512f/2f;
            int gridSize = 512 / InfluenceMapUtil.NODE_SIZE;
            int totalNodes = gridSize * gridSize;
            for (int i = 0; i < totalNodes; i++)
            {
                var t = m.MapNodes[i].TeamFavor;
                var s = m.MapNodes[i].Strength;
                GUIStyle style = new GUIStyle();
                style.normal.textColor = Color.azure;
                style.alignment = TextAnchor.MiddleCenter;
                style.fontSize = 28;

                // Draw the text at the GameObject's position
                int2 pos = InfluenceMapUtil.GetPositionOfNode(i, gridSize);
                Handles.Label(new Vector3(pos.x, 0, pos.y) + Vector3.up * 2f, $"{s},{t}", style);
            }
            
        }
        // Optional: set a GUI style for the label (e.g., color, font size)
        
    }
    #endif

}
