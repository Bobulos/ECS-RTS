using System;
using UnityEngine;

[CreateAssetMenu(fileName = "ConstructionData", menuName = "Scriptable Objects/ConstructionData")]
public class ConstructionData : ScriptableObject
{
    public ConstructionMode mode;
    public float spacing = 5f;
    public Vector3 size = new Vector3(10, 10, 10);
    public EntityData primary;
    public EntityData secondary;

    public Hash128 Guid;
    /*public GameObject constructor;
    public GameObject visPrefab;*/
}
public enum ConstructionMode : byte
{
    None,
    Structure,
    Wall,
}