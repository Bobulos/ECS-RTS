using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
public struct ActionData
{
    //WRITE
    public byte ActionByte;

    //NOT FOR REPLAY FETCHED DYNAMICALLY
    public ActionInfo Info;

    //for stuff like set rally points WRITE
    public float3 RayOrigin;
    public float3 RayDirection;
}
public class UnitActionManager : MonoBehaviour
{
    public Texture2D[] cursors;

    public UnitGUIActionElement[] elements;

    private EntityManager entityManager;
    private EntityQuery query;

    private EntityGUIManifest manifest;

    private EntityData curGUIData;
    //                   NOT
    //                   FOR
    //                   REPLAY     Team
    public static Action<ActionData, int> OnAction;

    public int team = 0;
    public List<ActionData> buffer = new List<ActionData>();

    private Camera cam;


    private void FixedUpdate()
    {
        //only playback input buffer if not in playback mode
        
        //the byte is the index of the action in the UGUIData 
        foreach (var input in buffer)
        {
            var record = InputRecordUtil.AssembleRecord(input, team);
            PlaybackInput(record);
            //UnityEngine.Debug.Log($"Unit Action {input}");
        }
        
        buffer.Clear();
    }

    public void PlaybackInput(InputRecord r)
    {
        var data = new ActionData
        {
            // Dont need to use this
            ActionByte = r.Action.ActionByte,
            Info = curGUIData.actions[r.Action.ActionByte],
            RayOrigin = r.Action.RayOrigin,
            RayDirection = r.Action.RayDirection
        };
        OnAction.Invoke(data, r.Team);
    }
    public void OnElementAction(byte actionByte)
    {
        ActionInfo info = curGUIData.actions[actionByte];

        var data = new ActionData
        {
            // Dont need to use this
            ActionByte = actionByte,
            Info = info,
            RayOrigin = float3.zero,
            RayDirection = float3.zero
        };
        switch (info.InteractionType)
        {
            case InteractionType.Target:
                Cursor.SetCursor(cursors[0], Vector2.zero, CursorMode.Auto);
                StartCoroutine(TargetAction(data));
                break;

            case InteractionType.Instant:
                buffer.Add(data);
                break;
        }
    }

    System.Collections.IEnumerator TargetAction(ActionData data)
    {
        //Debug.Log("Starting targetings");
        bool done = false;
        InputData.inAction = true;
        while (!done)
        {
            // when left click
            if (Input.GetMouseButtonDown(0))
            {
                done = true;
                var r = cam.ScreenPointToRay(Input.mousePosition);
                data.RayOrigin = r.origin;
                data.RayDirection = r.direction;
                buffer.Add(data);
            } else if (Input.GetKeyDown(KeyCode.Escape))
            {
                done = true;
            }
            yield return null; // Wait until the next frame
        }

        //reset texture
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);

        yield return new WaitForSeconds(0.5f);
        InputData.inAction = false;
        
        
        //Debug.Log("Done");
    }
    // System.Collections.IEnumerable ChangeInputData()
    // {
    //     yield return null;
    //     InputData.inAction = false;
    // }
    //int reads = 0;
    public void OnUpdateGUI()
    {
        foreach (var e in elements)
        {
            e.Clear();
        }
        StopAllCoroutines(); // Stop any pending readbacks
        StartCoroutine(ReadSelection());
    }

    System.Collections.IEnumerator ReadSelection()
    {
        yield return null; // Wait one frame for the system to update
        
        UnityEngine.Debug.Log("Read");
        
        if (!query.TryGetSingleton(out LocalSelectedUnits selectedUnits))
            yield break;

        // Crappy way to fetch a non null reference
        bool found = false;
        SelectedUnitBucket toDisplay = new SelectedUnitBucket { Key = 0, Count = 0};
        foreach (var bucket in selectedUnits.Buckets)
        {
            found = true;
            toDisplay = bucket;
            break;
        }

        // Clear all units
        if (!found)
        {
            foreach (var e in elements)
            {
                e.Clear();
            }
            yield break;
        }

        curGUIData = null;

        if (!manifest.TryGetData(toDisplay.Key, out EntityData data)) 
            yield break;

        if (data.actions == null) 
            yield break;

        curGUIData = data;
        
        for (int i = 0; i < data.actions.Length; i++)
        {
            elements[i].SetData(data, (byte)i);
        }
    }

    // void ReadSelection()
    // {
    //     //reads = 0;
    //     UnityEngine.Debug.Log("Read");
    //     if (!query.TryGetSingleton(out LocalSelectedUnits selectedUnits))
    //         return;

    //     //crappy way to fetch a non null reference
    //     bool found = false;
    //     SelectedUnitBucket toDisplay = new SelectedUnitBucket { Key = 0, Count = 0};
    //     foreach (var bucket in selectedUnits.Buckets)
    //     {
    //         found = true;
    //         toDisplay = bucket;
    //         break;
    //     }

    //     //clear all units
    //     if (!found)
    //     {
    //         foreach (var e in elements)
    //         {
    //             e.Clear();
    //         }
    //         return;
    //     }

    //     curGUIData = null;

    //     if (!manifest.TryGetData(toDisplay.Key, out UnitGUIData data)) return;

    //     if (data.actions == null) return;

    //     curGUIData = data;
    //     //if (data.actions.Length > elements.Length) { UnityEngine.Debug.Log($"You have to many actions on {data.name} UnitGUIData"); };
    //     //UnityEngine.Debug.Log($"Action list length of {data.actions.Length}");
    //     for (int i = 0; i < data.actions.Length; i ++)
    //     {
    //         elements[i].SetData(data, (byte)i);
    //     }
    // }
    private void Start()
    {
        manifest = FindFirstObjectByType<EntityGUIManifest>();
        World defaultWorld = World.DefaultGameObjectInjectionWorld;
        entityManager = defaultWorld.EntityManager;
        query = entityManager.CreateEntityQuery(typeof(LocalSelectedUnits));

        InputBridge.OnUpdateGUI += OnUpdateGUI;
        UnitGUIActionElement.OnAction += OnElementAction;

        if (GameSettings.InReplayMode) this.enabled = false;

        cam = Camera.main;
    }
    private void OnDestroy()
    {
        InputBridge.OnUpdateGUI -= OnUpdateGUI;
        UnitGUIActionElement.OnAction -= OnElementAction;
    }
}
