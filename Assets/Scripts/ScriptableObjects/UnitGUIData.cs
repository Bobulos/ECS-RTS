using System;
using UnityEngine;

[CreateAssetMenu(fileName = "UnitGUIData", menuName = "Scriptable Objects/UnitGUIData")]
public class UnitGUIData : ScriptableObject
{
    public int key;
    public string unitName;
    public Sprite icon;
    public string description;

    public ActionInfo[] actions;
    public Sprite[] actionIcons;
}
[Serializable]
public struct ActionInfo
{
    public ActionType ActionType;
    public InteractionType InteractionType;
    [Header("ONLY IF ADD UNIT TO QUEUE TYPE")]public int PrefabIndex;
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