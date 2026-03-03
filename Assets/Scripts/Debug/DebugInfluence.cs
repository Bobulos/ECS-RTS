using UnityEngine;
using UnityEditor;
using Unity.Entities;
using Unity.NetCode;

public class DebugInfluence : MapLoadedAccess
{
    private World _clientWorld;
    private EntityQuery _influenceQuery;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public override void OnLoad()
    {
        _clientWorld = ClientServerBootstrap.ClientWorld;
        _influenceQuery = _clientWorld.EntityManager.CreateEntityQuery(typeof(InfluenceMap));
    }

    // Update is called once per frame
    #if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (_influenceQuery.TryGetSingleton<InfluenceMap>(out var m))
        {
            for (int i = 0; i < 512*512; i++)
            {
                var s = m.MapNodes[i].Strength;
                GUIStyle style = new GUIStyle();
                style.normal.textColor = Color.red;
                style.alignment = TextAnchor.MiddleCenter;
                style.fontSize = 28;

                // Draw the text at the GameObject's position
                Handles.Label(transform.position + Vector3.up * 2f, $"{s}", style);
            }
            
        }
        // Optional: set a GUI style for the label (e.g., color, font size)
        
    }
    #endif

}
