using System;
using Unity.Mathematics;
using UnityEngine;

[CreateAssetMenu(fileName = "ConstructionData", menuName = "Scriptable Objects/ConstructionData")]
public class ConstructionData : ScriptableObject
{
    public ConstructionMode mode;
    public float spacing = 5f;
    public int3 size = new int3(10, 10, 10);
    public EntityData primary;
    public EntityData secondary;
    public int key;
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