using UnityEngine;

[CreateAssetMenu(fileName = "ProductionKey", menuName = "Scriptable Objects/ProductionKey")]
public class ProductionKey : ScriptableObject
{
    public int Key = 0;

    public float TrainingTime;

    //placeholders
    public int Cost;
}
