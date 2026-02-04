using System;
using UnityEngine;

[CreateAssetMenu(fileName = "UnitGUIData", menuName = "Scriptable Objects/UnitGUIData")]
public class UnitGUIData : ScriptableObject
{
    public int key;
    public string unitName;
    public Sprite icon;
    public string description;

    public ActionData[] actions;
    public Sprite[] actionIcons;
}
[Serializable]
public struct ActionData
{
    public ActionType ActionType;
    [Header("ONLY IF ADD UNIT TO QUEUE TYPE")]public int PrefabIndex;
}
public enum ActionType : byte
{
    AddUnitToQueue,
    SetRallyPoint,
    Move,
}