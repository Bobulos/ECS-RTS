using NUnit.Framework.Constraints;
using System;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;

public class ConstructionBridge : MonoBehaviour
{
    public static event Action<ConstructWallData> VisualizeWalls;
    public static event Action<ConstructData> VisualizeStructure;
    public static event Action CancelContrstruction;
    public static event Action<ConstructWallData, uint> ConstructWalls;
    public static event Action<ConstructData, uint> ConstructStructure;

    public LayerMask terrainMask;
    public ConstructionData constructData;

    public uint team;

    public void UpdateConstructionData(ConstructionData d)
    {
        CancelContrstruction?.Invoke();
        constructData = d;
    }
    Camera cam;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        cam = Camera.main;
        if (GameSettings.InReplayMode) { this.enabled = false; }
    }
    float3 startBuildPos;
    float3 endBuildPos;
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
        if (constructData != null)
        {
            if (!UIUtility.IsPointerOverUI() && Physics.Raycast(camRay, out UnityEngine.RaycastHit hit, 100f, terrainMask))
            {

                if (constructData.mode == ConstructionMode.Wall )
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
                            }, team));
                            CancelContrstruction?.Invoke();
                        }
                        
                    }else if (startBuild)
                    {
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
                        VisualizeWalls?.Invoke(new ConstructWallData
                        {
                            start = hit.point,
                            end = hit.point,
                            constructData = constructData,
                            isSingleVis = true
                        });
                    }
                } else if (constructData.mode == ConstructionMode.Structure)
                {
                    if (Input.GetMouseButtonDown(0))
                    {
                        
                        buffer.Add(InputRecordUtil.AssembleRecord(new ConstructData{ 
                            constructData = constructData,
                            pos = hit.point,
                        }, team));
                    } 
                    //visualize
                    else
                    {
                        VisualizeStructure?.Invoke(new ConstructData
                        {
                            constructData = constructData,
                            pos = hit.point,
                        });
                    }
                }

            }

        }
    }
    private void FixedUpdate()
    {
        
        foreach(InputRecord r in buffer)
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
                    pos = r.Structure.pos,
                    constructData = r.Structure.constructData,
                    constructID = Array.IndexOf(constructs, r.Structure.constructData),
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
                ConstructStructure?.Invoke( new ConstructData
                {
                    pos = r.Structure.pos,
                    constructData = r.Structure.constructData,
                    constructID = 0,
                }, team);
                break;
        }
    }
    public ConstructionData[] constructs;
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
    public float3 pos;
    public ConstructionData constructData;
    public int constructID;

}
/*using System;
using Unity.Entities;
using UnityEngine;


public enum ConstructionMode
{
    None,
    Building,
    Wall,
}
public class PlayerConstruction : MonoBehaviour
{
    //public GameObject constructor;
    public LayerMask terrainMask;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        cam = Camera.main;
    }
    private bool startPlacing = true;
    private Camera cam;

    //preview
    public Transform target;


    //build tools
    private WallConstructor curCons;

    public ConstructionData constructData = null;
    //private Transform end;
    // Update is called once per frame
    //bool active = false;

    public void UpdateConstructionData(ConstructionData d)
    {
        constructData = d;
    }
    private GameObject vis = null;
    void Update()
    {

        //cancel the build
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            startPlacing = true;
            TryCleanUp();
            constructData = null;
        }
        if (constructData != null)
        {
            if (constructData.mode == ConstructionMode.Wall)
            {
                //instantiate the the vis
                if (vis == null)
                {
                    vis = Instantiate(constructData.visPrefab);
                    vis.transform.position = target.position;
                    vis.transform.SetParent(target.transform, true);
                }
                ConstructWallUpdate();
            }
        }
    }
    void ConstructWallUpdate()
    {
        Ray camRay = cam.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(camRay, out RaycastHit hit, 1000f, terrainMask))
        {
            //snapping logic
            if (IsSnapping(hit.point, out Vector3 pos))
            {
                target.position = pos;
            }
            else
            {
                target.position = hit.point;
            }

            if (Input.GetMouseButtonDown(0) && !UIUtility.IsPointerOverUI())
            {
                if (startPlacing)
                {
                    //spawning the start node
                    GameObject node = Instantiate(constructData.constructor, target.position, Quaternion.identity);
                    curCons = node.GetComponent<WallConstructor>();
                    startPlacing = false;
                }
                else
                {
                    curCons.BuildSegments();

                    //cleaningStuff up
                    TryCleanUp();
                    startPlacing = true;
                }
            }
            if (curCons != null) { curCons.target = target; }
            if (!startPlacing && curCons != null) { curCons.BuildVisSegments(); }

        }
    }
    public LayerMask snapMask;
    public float snapRadius = 2.0f;
    public bool IsSnapping(Vector3 check, out Vector3 pos)
    {
        var coll = Physics.OverlapSphere(check, snapRadius, snapMask);

        pos = Vector3.zero;

        float min = Mathf.Infinity;
        foreach (var c in coll)
        {
            //Debug.Log("Contact");
            float dist = Vector3.Distance(c.transform.position, transform.position);
            if (dist < min)
            {
                min = dist;
                pos = c.transform.position;
            }
        }
        if (coll.Length == 0) { return false; } else { return true; }
    }
    private void TryCleanUp()
    {
        if (vis != null)
            Destroy(vis.gameObject);
        if (curCons != null)
            Destroy(curCons.gameObject);
    }
}
*/