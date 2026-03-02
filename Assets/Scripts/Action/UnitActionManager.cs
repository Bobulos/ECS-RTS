using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEditor;
using System.Linq;
using Unity.NetCode;

//32
public struct ActionData
{
    public bool Shifting;
    //WRITE
    public byte ActionByte;

    //NOT FOR REPLAY FETCHED DYNAMICALLY
    //6 B
    public ActionInfo Info;

    //for stuff like set rally points WRITE
    public float3 RayOrigin;
    public float3 RayDirection;
}

public class UnitActionManager : MapLoadedAccess
{
    #region Fields
    
    public Vector2 cursorOffset;
    public Texture2D[] cursors;
    public UnitGUIActionElement[] elements;
    public ConstructionData[] structures;
    [Header("Update Settings")]
    public float updateInterval = 0.1f; // Update every 0.1 seconds

    private EntityManager entityManager;
    private EntityQuery query;
    private EntityGUIManifest manifest;
    private EntityData curGUIData;
    private Camera cam;
    private List<ActionData> buffer = new List<ActionData>();
    
    // Cache for change detection
    private int lastSelectedKey = -1;
    private int lastSelectedCount = 0;
    
    private UnityEngine.Coroutine updateCoroutine;

    public static Action<ActionData> OnAction;
    public static event Action<ConstructData> VisualizeStructure;
    public static event Action CancelStructure;
    
    #endregion

    #region Unity Lifecycle
    
    public override void OnLoad()
    {
        manifest = FindFirstObjectByType<EntityGUIManifest>();

        World defaultWorld = ClientServerBootstrap.ClientWorld;
        entityManager = defaultWorld.EntityManager;
        query = entityManager.CreateEntityQuery(typeof(LocalSelectedUnits));

        InputBridge.OnUpdateGUI += OnUpdateGUI;
        UnitGUIActionElement.OnAction += OnElementAction;
        
        if (GameLoadConfig.InReplayMode) 
        {
            this.enabled = false;
            return;
        }

        cam = Camera.main;
        
        // Start continuous update
        updateCoroutine = StartCoroutine(ContinuousUpdateCoroutine());
    }

    private void OnDestroy()
    {
        InputBridge.OnUpdateGUI -= OnUpdateGUI;
        UnitGUIActionElement.OnAction -= OnElementAction;
        
        if (updateCoroutine != null)
        {
            StopCoroutine(updateCoroutine);
        }
    }
    
    #endregion

    #region Input Buffer Playback
    
    private void FixedUpdate()
    {
        // Only playback input buffer if not in playback mode
        // The byte is the index of the action in the UGUIData 
        foreach (var input in buffer)
        {
            var record = InputRecordUtil.AssembleRecord(input);
            PlaybackInput(record);
            //UnityEngine.Debug.Log($"Action executed - Shifting: {record.Action.Shifting}");
        }
        
        buffer.Clear();
    }

    public void PlaybackInput(InputRecord r)
    {
        var data = new ActionData
        {
            Shifting = r.Action.Shifting, // Preserve shifting from record
            ActionByte = r.Action.ActionByte,
            Info = curGUIData.actions[r.Action.ActionByte],
            RayOrigin = r.Action.RayOrigin,
            RayDirection = r.Action.RayDirection
        };
        OnAction?.Invoke(data);
    }
    
    #endregion

    #region Action Handling
    
    public void OnElementAction(byte actionByte)
    {
        ActionInfo info = curGUIData.actions[actionByte];

        // Capture shift state immediately when action is triggered
        bool isShifting = Input.GetKey(KeyCode.LeftShift);

        var data = new ActionData
        {
            Shifting = isShifting,
            ActionByte = actionByte,
            Info = info,
            RayOrigin = float3.zero,
            RayDirection = float3.zero
        };

        switch (info.InteractionType)
        {
            case InteractionType.Target:
                // Set cursor for targeting mode
                Cursor.SetCursor(cursors[0], cursorOffset, CursorMode.Auto);
                StartCoroutine(TargetAction(data, curGUIData));
                break;

            case InteractionType.Instant:
                // Instant actions go straight to buffer
                buffer.Add(data);
                break;
        }
    }
    
    #endregion

    #region Target Action Coroutine
    
    System.Collections.IEnumerator TargetAction(ActionData data, EntityData entity)
    {
        bool done = false;
        InputData.inAction = true;

        while (!done)
        {
            // Check for left click to confirm action
            if (Input.GetMouseButtonDown(0))
            {
                if (!Input.GetKey(KeyCode.LeftShift)) done = true;
                
                var r = cam.ScreenPointToRay(Input.mousePosition);
                data.RayOrigin = r.origin;
                data.RayDirection = r.direction;
                
                // Re-check shift state at the moment of confirmation
                // This allows player to add/remove shift during targeting
                data.Shifting = Input.GetKey(KeyCode.LeftShift);
                
                buffer.Add(data);
            } 
            else if (Input.GetKeyDown(KeyCode.Escape))
            {
                done = true;
            }

            // Visualize structure placement
            if (data.Info.ActionType == ActionType.BuildStructure)
            {
                var r = cam.ScreenPointToRay(Input.mousePosition);
                VisualizeStructure?.Invoke(new ConstructData
                {
                    Origin = r.origin,
                    Dir = r.direction,
                    Data = structures[entity.visuals[data.ActionByte].key],
                });
            }

            yield return null;
        }

        // Reset cursor
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);

        // Small delay before allowing new input
        yield return new WaitForSeconds(0.2f);
        InputData.inAction = false;

        // Cancel structure visualization if not queuing
        CancelStructure?.Invoke();
    }
    
    #endregion

    #region GUI Update
    
    // Continuous update coroutine
    System.Collections.IEnumerator ContinuousUpdateCoroutine()
    {
        while (true)
        {
            UpdateGUI();
            yield return new WaitForSeconds(updateInterval);
        }
    }
    
    // Optional: Immediate update triggered by event
    public void OnUpdateGUI()
    {
        UpdateGUI();
    }

    void UpdateGUI()
    {
        if (!query.TryGetSingleton(out LocalSelectedUnits selectedUnits))
        {
            // No selection - clear if we had something selected before
            if (lastSelectedKey != -1)
            {
                ClearAllElements();
                lastSelectedKey = -1;
                lastSelectedCount = 0;
            }
            return;
        }

        // Find first non-empty bucket
        bool found = false;
        SelectedUnitBucket toDisplay = new SelectedUnitBucket { Key = 0, Count = 0 };
        
        foreach (var bucket in selectedUnits.Buckets)
        {
            found = true;
            toDisplay = bucket;
            break;
        }

        // Clear all elements if no selection
        if (!found)
        {
            if (lastSelectedKey != -1)
            {
                ClearAllElements();
                lastSelectedKey = -1;
                lastSelectedCount = 0;
            }
            return;
        }

        // Check if selection has changed
        if (toDisplay.Key == lastSelectedKey && toDisplay.Count == lastSelectedCount)
        {
            return; // No change, skip update
        }

        // Update cache
        lastSelectedKey = toDisplay.Key;
        lastSelectedCount = toDisplay.Count;

        // Get and validate data
        if (!manifest.TryGetData(toDisplay.Key, out EntityData data))
        {
            ClearAllElements();
            curGUIData = null;
            return;
        }

        if (data.actions == null)
        {
            ClearAllElements();
            curGUIData = null;
            return;
        }

        curGUIData = data;

        // Populate action elements
        for (int i = 0; i < elements.Length; i++)
        {
            if (i < data.actions.Length)
            {
                elements[i].SetData(data, (byte)i);
            }
            else
            {
                elements[i].Clear();
            }
        }
    }

    void ClearAllElements()
    {
        foreach (var e in elements)
        {
            e.Clear();
        }
        curGUIData = null;
    }

    // Kept for backwards compatibility but now just calls UpdateGUI
    System.Collections.IEnumerator ReadSelection()
    {
        yield return new WaitForSeconds(0.2f);
        UpdateGUI();
    }
    
    #endregion

    #region Editor Utilities
    
    #if UNITY_EDITOR
    [ContextMenu("Update manifest")]
    public void UpdateManifest()
    {
        var constructionData = ScriptableObjectUtil.LoadAllScriptableObjects<ConstructionData>();

        structures = constructionData
            .OrderBy(e => e.Guid)
            .ToArray();

        for (int i = 0; i < structures.Length; i++)
        {
            structures[i].key = i;
            EditorUtility.SetDirty(structures[i]);
        }
        
        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets();
    }
    #endif
    
    #endregion
}