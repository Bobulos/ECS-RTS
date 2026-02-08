using System;
using UnityEngine;

[CreateAssetMenu(fileName = "UnitGUIData", menuName = "Scriptable Objects/UnitGUIData")]
public class EntityData : ScriptableObject
{
    public GameObject prefab;
    public EntityType entityType;

    public string entityName;
    public Texture2D icon;
    public string description;

    public ActionInfo[] actions;

    [Header("Overides deafualt keep null if want to fetch")]
    public Texture2D[] actionIcons;


    //production
    public float trainingTime;



    //Ident
    public Hash128 entityGuid;
    public int key;
    public int keyGUI;
}
[Serializable]
public struct ActionInfo
{
    public ActionType ActionType;
    public InteractionType InteractionType;
    [Header("ONLY IF ADD UNIT TO QUEUE TYPE")]public int PrefabIndex;
    //[Header("0 move 1 rally 2 atk")]public int CursorIndex;
}
public enum EntityType
{
    Unit,
    Structure,
}
public enum InteractionType
{
    Instant,
    Target,
}
public enum ActionType : byte
{
    AddUnitToQueue,
    SetRallyPoint,
    Move,
}