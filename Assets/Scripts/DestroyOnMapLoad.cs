using UnityEngine;

public class DestroyOnMapLoad : MonoBehaviour
{
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        MapConnectionBootstrap.OnMapLoaded += OnMapLoaded;
    }
    void OnMapLoaded()
    {
        Destroy(this.gameObject);
    }
}
