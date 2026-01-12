using UnityEngine;

public class FogOfWarRendering : MonoBehaviour
{
    // The Material that uses the custom fog shader. 
    // This should be assigned in the Inspector.
    public Material mat;


    /// <summary>
    /// Called by the FogSystem to pass the Compute Shader's output texture.
    /// </summary>
    /// <param name="texture">The RenderTexture containing the fog data.</param>
    public void SetTexture(RenderTexture visible, RenderTexture explored)
    {
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