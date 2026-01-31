using UnityEngine;

public class FogOfWarRendering : MonoBehaviour
{
    // The Material that uses the custom fog shader. 
    // This should be assigned in the Inspector.
    public Material mat;

    public void SetTexture(RenderTexture visible, RenderTexture explored)
    {
        if (mat == null)
            mat = GetComponent<MeshRenderer>()?.material;

        if (mat == null) return;
        UnityEngine.Debug.Log("Fog vision set");
        mat.SetTexture("_Visible", visible);
        mat.SetTexture("_Explored", explored);
    }
    void Start()
    {
        if (mat == null)
        {
            mat = GetComponent<MeshRenderer>()?.material;
        }
    }
}