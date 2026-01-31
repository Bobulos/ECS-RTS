using UnityEngine;

public class FogOfWarRendering : MonoBehaviour
{
    // The Material that uses the custom fog shader. 
    // This should be assigned in the Inspector.
    public Material mat;
    public bool set;

    public RenderTexture vis;
    public RenderTexture exp;
    MaterialPropertyBlock _block;

    void Awake()
    {
        _block = new MaterialPropertyBlock();
        set = false;
    }

    public void SetTexture(RenderTexture visible, RenderTexture explored)
    {
        if (visible == null || explored == null) return;

        /*var r = GetComponent<MeshRenderer>();
        if (!r) return;*/
        set = true;
        vis = visible; exp = explored;

        //r.GetPropertyBlock(_block);
        mat.SetTexture("_Visible", visible);
        mat.SetTexture("_Explored", explored);
/*
        var terrain = GetComponent<Terrain>();
        if (terrain != null && newMaterial != null)
        {
            // Set the material type to custom for the materialTemplate to be used
            terrain.materialType = Terrain.MaterialType.Custom;
            terrain.materialTemplate = newMaterial;
        }*/
        //r.SetPropertyBlock(_block);
    }
    void Start()
    {
        if (mat == null)
        {
            mat = GetComponent<MeshRenderer>()?.material;
        }
    }
}