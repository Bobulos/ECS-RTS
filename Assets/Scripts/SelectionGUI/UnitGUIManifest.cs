using UnityEngine;

public class UnitGUIManifest : MonoBehaviour
{
    [SerializeField]
    private UnitGUIData[] manifest;

    public UnitGUIData GetData(int key)
    {
        return manifest[key];
    }
    public bool TryGetData(int key, out UnitGUIData data)
    {
        if (key >= 0 && key < manifest.Length)
        {
            data = manifest[key];
            return true;
        }
        data = null;
        return false;
    }
}
