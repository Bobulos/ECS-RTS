using System;
using System.Collections.Generic;
using TMPro;
using Unity.Entities;
using UnityEngine;

public struct UnitAction
{
    public ActionType ActionType;
    public byte ActionIndex;
}

public class UnitActionManager : MonoBehaviour
{
    public UnitGUIActionElement[] elements;

    private EntityManager entityManager;
    private EntityQuery query;

    private UnitGUIManifest manifest;

    public static Action<UnitAction, int> OnAction;

    public int team = 0;
    public List<UnitAction> buffer = new List<UnitAction>();

    private void FixedUpdate()
    {
        foreach (var input in buffer)
        {
            var record = InputRecordUtil.AssembleActionRecord(input, team);
            PlaybackInput(record);
            UnityEngine.Debug.Log($"Unit Action {input.ActionIndex}");
        }
        
        buffer.Clear();
    }

    public void PlaybackInput(InputRecord r)
    {
        OnAction.Invoke(r.Action, r.Team);
    }
    public void OnElementAction(UnitGUIData d, byte actionIndex)
    {
        buffer.Add(new UnitAction { 
            ActionType = d.actions[actionIndex],
            ActionIndex = actionIndex }
        );
    }

    public void OnUpdateGUI()
    {
        //allow time for the system to update
        Invoke(nameof(ReadSelection), Time.deltaTime * 2f);
    }

    void ReadSelection()
    {
        if (!query.TryGetSingleton(out LocalSelectedUnits selectedUnits))
            return;

        //crappy way to fetch a non null reference
        bool found = false;
        SelectedUnitBucket toDisplay = new SelectedUnitBucket { Key = 0, Count = 0};
        foreach (var bucket in selectedUnits.Buckets)
        {
            found = true;
            toDisplay = bucket;
            break;
        }

        //clear all units
        if (!found)
        {
            foreach (var e in elements)
            {
                e.Clear();
            }
            return;
        }

        if (!manifest.TryGetData(toDisplay.Key, out var data)) return;

        if (data.actions == null) return;

        //if (data.actions.Length > elements.Length) { UnityEngine.Debug.Log($"You have to many actions on {data.name} UnitGUIData"); };
        //UnityEngine.Debug.Log($"Action list length of {data.actions.Length}");
        for (int i = 0; i < data.actions.Length; i ++)
        {
            elements[i].SetData(data, (byte)i);
        }
    }
    private void Start()
    {
        manifest = FindFirstObjectByType<UnitGUIManifest>();
        World defaultWorld = World.DefaultGameObjectInjectionWorld;
        entityManager = defaultWorld.EntityManager;
        query = entityManager.CreateEntityQuery(typeof(LocalSelectedUnits));

        InputBridge.OnUpdateGUI += OnUpdateGUI;
        UnitGUIActionElement.OnAction += OnElementAction;
    }
    private void OnDestroy()
    {
        InputBridge.OnUpdateGUI -= OnUpdateGUI;
        UnitGUIActionElement.OnAction -= OnElementAction;
    }
}
