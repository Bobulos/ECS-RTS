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

    //this is all structs keep it that way no managed bs
    public ActionInfo[] actions;

    [Header("Hotkeys for action of equivalent index")]
    public KeyCode[] hotKeys;

    [Header("Never sent to system used for structure visualization")]
    public EntityData[] visuals;

    [Header("Overides icons fetched from visuals")]
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
    [Header("ONLY FOR BUILD STUFF")]public int PrefabIndex;

    //[Header("Construct data NOT USED SYSTEM SIDE")] public ConstructionData construction;
    //[Header("Temporary")] public in
    //[Header("0 move 1 rally 2 atk")]public int CursorIndex;
}
public enum EntityType
{
    Unit,
    Structure,
}
public enum InteractionType : byte
{
    Instant,
    Target,
}
public enum ActionType : byte
{
    AddUnitToQueue,
    SetRallyPoint,
    Move,
    BuildStructure,
}