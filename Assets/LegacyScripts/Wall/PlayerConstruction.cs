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