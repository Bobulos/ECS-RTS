using UnityEngine;
using UnityEngine.SceneManagement;


public abstract class MapLoadedAccess : MonoBehaviour
{
    //[SerializeField] private string _requiredScene;

    public bool _ready;

    private void Start()
    {
        UnityEngine.Debug.Log($"[MapLoaded] subscribing to OnMapLoaded");
        _ready = false;
        MapConnectionBootstrap.OnMapLoaded += OnMapLoaded;
    }
    private void OnMapLoaded()
    {
        UnityEngine.Debug.Log($"<color=red>[MapLoaded] Loading complete, allowing access</color>");
        _ready = true;
        OnLoad();
    }
    public abstract void OnLoad();
}