/*using UnityEngine;
using System.Collections.Generic;
// NEW: Required for DOTS physics bridge and math types
using Unity.Physics;
using Unity.Mathematics;
using Unity.Entities;

public class WallConstructor : MonoBehaviour
{
    public ConstructionData data;

    public Transform target;
    public GameObject nodePrefab;

    public float spacing = 1f;
    public Vector3 offset = Vector3.zero;

    public LayerMask terrainMask;
    public LayerMask blockMask;

    public UnityEngine.Material validMat;
    public UnityEngine.Material invalidMat;

    private List<GameObject> visPool = new List<GameObject>();


    //private PhysicsWorldSingleton m_PhysicsWorld;

    private void Start()
    {
        // ... (Initialization of visPool remains the same) ...
        for (int i = 0; i < 100; i++)
        {
            GameObject v = Instantiate(data.visPrefab);
            v.SetActive(false);
            visPool.Add(v);
        }
    }


    // -------------------- BUILD WALL NODES --------------------
    public void BuildSegments()
    {
        if (!CheckValid()) { return; }

        Vector3 dir = (target.position - transform.position).normalized;
        float dist = Vector3.Distance(transform.position, target.position);

        WallNode prevNode = null;

        for (float d = 0; d <= dist; d += spacing)
        {
            Vector3 castPos = transform.position + dir * d;
            if (TryGetGroundPoint(castPos, out Vector3 point))
            {
                WallNode node = Instantiate(nodePrefab, point + offset, Quaternion.identity)
                                     .GetComponent<WallNode>();

                if (prevNode != null)
                    prevNode.SetNext(node);

                prevNode = node;
            }
        }
        //end node
        if (TryGetGroundPoint(target.position, out Vector3 hitPos))
        {
            WallNode node = Instantiate(nodePrefab, hitPos + offset, Quaternion.identity)
                                     .GetComponent<WallNode>();

            if (prevNode != null)
                prevNode.SetNext(node);
        }
    }


    // -------------------- VISUAL GHOST WALL --------------------
    public void BuildVisSegments()
    {
        // ... (BuildVisSegments remains the same, relying on CheckValid) ...
        foreach (var v in visPool)
            v.SetActive(false);

        Vector3 dir = (target.position - transform.position).normalized;
        float dist = Vector3.Distance(transform.position, target.position);

        int poolIndex = 0;


        bool blocked = CheckValid();


        for (float d = 0; d <= dist && poolIndex < visPool.Count; d += spacing)
        {
            Vector3 castPos = transform.position + dir * d;

            if (TryGetGroundPoint(castPos, out Vector3 point))
            {
                GameObject vis = visPool[poolIndex++];
                vis.transform.position = point + offset;

                vis.GetComponent<MeshRenderer>().material =
                    blocked ? validMat : invalidMat;

                vis.SetActive(true);
            }
        }
    }
    public float endBounds = 2.5f;
    public float checkRadius = 2f;

    // NEW: Uses DOTS-aware API to detect colliders in the active physics world
    bool CheckValid()
    {
        // 1. Convert to Unity.Mathematics types required by DOTS API
        float3 startPoint = (float3)transform.position;
        float3 endPoint = (float3)target.position;
        float3 dir = endPoint - startPoint;
        float dist = math.length(dir);
        float3 normalizedDir = dir / dist; // float3 / float = float3

        float sphereRadiusOffset = checkRadius;
        float3 castOrigin = startPoint;
        float castDistance = dist;

        if (dist > sphereRadiusOffset)
        {
            castOrigin += normalizedDir * sphereRadiusOffset;
            castDistance -= sphereRadiusOffset * 2f;
            if (castDistance < 0) castDistance = 0f;
        }

        // Convert the LayerMask to an integer for the DOTS API
        int blockLayerMask = (int)blockMask;

        UnityEngine.RaycastHit hit;

        // 3. Perform the DOTS Physics SphereCast (Required for Hybrid scenes)
        if (Physics.SphereCast(
            castOrigin,
            checkRadius,
            normalizedDir,
            out hit,
            castDistance,
            blockLayerMask
        ))
        {
            Debug.Log("Blocked by active DOTS/Hybrid object.");
            return false; // Path is blocked
        }

        return true; // Path is clear
    }
    // -------------------- HELPER --------------------
    private bool TryGetGroundPoint(Vector3 pos, out Vector3 result)
    {
        UnityEngine.Ray r = new UnityEngine.Ray(pos + Vector3.up * 10f, Vector3.down);
        if (UnityEngine.Physics.Raycast(r, out UnityEngine.RaycastHit hit, 50f, terrainMask))
        {
            result = hit.point;
            return true;
        }

        result = Vector3.zero;
        return false;
    }

    private void OnDestroy()
    {
        foreach (var v in visPool)
            Destroy(v);
    }
}*/