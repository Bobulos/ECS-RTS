using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
/// <summary>
/// Deprecated being replaced by unit action manager
/// </summary>

public class ConstructionBridge : MonoBehaviour
{
    public static event Action<ConstructWallData> VisualizeWalls;
    public static event Action<ConstructData> VisualizeStructure;
    public static event Action CancelContrstruction;
    public static event Action<ConstructWallData, int> ConstructWalls;
    public static event Action<ConstructData, int> ConstructStructure;

    public LayerMask terrainMask;
    public ConstructionData constructData;

    public int team;
    
    public ConstructionData[] constructs;

    public void UpdateConstructionData(ConstructionData d)
    {
        CancelContrstruction?.Invoke();
        constructData = d;
    }
    Camera cam;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        //constructs = ScriptableObjectUtil.LoadAllScriptableObjects<ConstructionData>().ToArray();
        cam = Camera.main;
        if (GameLoadConfig.InReplayMode) { this.enabled = false;  }
    }
    float3 startBuildPos;

    bool startBuild = false;
    void Update()
    {
        UnityEngine.Ray camRay = cam.ScreenPointToRay(Input.mousePosition);
        //cancel the build
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            OnCancel();
        }
        if (Input.GetMouseButtonDown(1))
        {
            CancelContrstruction.Invoke();
            startBuild = false;
        }
        if (constructData == null) { return; }
        if (UIUtility.IsPointerOverUI()) { return; }
        if (!Physics.Raycast(camRay, out UnityEngine.RaycastHit hit, 600f, terrainMask)) {  return;}

        if (constructData.mode == ConstructionMode.Wall)
        {
            if (Input.GetMouseButtonDown(0))
            {
                startBuild = !startBuild;
                if (startBuild)
                {
                    startBuildPos = hit.point;

                }
                //build walls command
                else
                {
                    buffer.Add(InputRecordUtil.AssembleRecord(new ConstructWallData
                    {
                        start = startBuildPos,
                        end = hit.point,
                        constructData = constructData,
                        isSingleVis = false
                    }));
                    CancelContrstruction?.Invoke();
                }

            }
            else if (startBuild)
            {
                //Debug.Log("Start build");
                VisualizeWalls?.Invoke(new ConstructWallData
                {
                    start = startBuildPos,
                    end = hit.point,
                    constructData = constructData,
                    isSingleVis = false
                });
            }
            else
            {
                //Debug.Log("Start build");
                VisualizeWalls?.Invoke(new ConstructWallData
                {
                    start = hit.point,
                    end = hit.point,
                    constructData = constructData,
                    isSingleVis = true
                });
            }
        }
        else if (constructData.mode == ConstructionMode.Structure)
        {
            var ray = cam.ScreenPointToRay(Input.mousePosition);
            if (Input.GetMouseButtonDown(0))
            {
                buffer.Add(InputRecordUtil.AssembleRecord(new ConstructData
                {
                    Data = constructData,
                    Dir = ray.direction,
                    Origin = ray.origin
                }));
            }
            //visualize
            else
            {
                VisualizeStructure?.Invoke(new ConstructData
                {
                    Data = constructData,
                    Dir = ray.direction,
                    Origin = ray.origin
                });
            }
        }
    }
    //private Camera cam;
    private void FixedUpdate()
    {

        foreach (InputRecord r in buffer)
        {
            PlaybackRLInput(r);
        }
        buffer.Clear();
    }
    List<InputRecord> buffer = new List<InputRecord>();
    public void PlaybackRLInput(InputRecord r)
    {
        switch (r.Type)
        {
            case InputType.ConstructWalls:
                //Debug.Log("Play back wall input");
                ConstructWalls.Invoke(new ConstructWallData
                {
                    start = r.Wall.start,
                    end = r.Wall.end,
                    //using this one
                    constructData = r.Wall.constructData,
                    //for repalay
                    constructID = Array.IndexOf(constructs, r.Wall.constructData)
                }, team);
                break;

            case InputType.Construct:
                ConstructStructure?.Invoke(new ConstructData
                {
                    Dir = r.Structure.Dir,

                    Origin = r.Structure.Origin,
                    Data = r.Structure.Data,
                    

                    //for replay mabeye check if in range
                    ConstructID = Array.IndexOf(constructs, r.Structure.ConstructID),
                }, team);
                break;
        }
    }
    public void PlaybackInput(InputRecord r)
    {
        switch (r.Type)
        {
            case InputType.ConstructWalls:
                //Debug.Log("Play back wall input");
                ConstructWalls?.Invoke(new ConstructWallData
                {
                    start = r.Wall.start,
                    end = r.Wall.end,
                    constructData = constructs[r.Wall.constructID],
                    //dont need it ig
                    constructID = 0
                }, team);
                break;
            case InputType.Construct:
                ConstructStructure?.Invoke(new ConstructData
                {
                    Dir = r.Structure.Dir,
                    Data = constructs[r.Structure.ConstructID],
                    ConstructID = 0,
                }, team);
                break;
        }
    }
    void OnCancel()
    {
        constructData = null;
        CancelContrstruction.Invoke();
    }
}
public struct ConstructWallData
{
    public float3 start;
    public float3 end;

    public bool isSingleVis;
    //used for playback
    public int constructID;
    public ConstructionData constructData;
}
public struct ConstructData
{
    public float3 Origin;
    public float3 Dir;
    public ConstructionData Data;
    public int ConstructID;

}