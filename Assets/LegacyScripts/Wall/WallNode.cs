/*using UnityEngine;
using UnityEngine.Rendering.Universal;

public class WallNode : MonoBehaviour
{
    public GameObject segmentPrefab;

    private WallNode next;
    private GameObject segmentInstance;

    // Assign a new neighbor
    public void SetNext(WallNode n)
    {
        next = n;
        UpdateSegment();
    }

    // Creates or moves the connecting segment
    private void UpdateSegment()
    {
        if (next == null)
        {
            if (segmentInstance != null)
                Destroy(segmentInstance);
            return;
        }

        Vector3 start = transform.position;
        Vector3 end = next.transform.position;

        if (segmentInstance == null)
            segmentInstance = Instantiate(segmentPrefab, transform);


        segmentInstance.transform.position = (start + end) * 0.5f;

        // rotate segment to face next node
        Vector3 dir = end - start;
        segmentInstance.transform.rotation = Quaternion.LookRotation(dir);
        // scale to stretch between nodes
        segmentInstance.transform.localScale =
            new Vector3(
                segmentInstance.transform.localScale.x,
                segmentInstance.transform.localScale.y,
                dir.magnitude
            );
    }

    private void OnDestroy()
    {
        if (segmentInstance != null)
            Destroy(segmentInstance);
    }
}
*/