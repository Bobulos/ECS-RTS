using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEditor;
using System.Linq;
using Unity.NetCode;
using RTS.InputLogging;

public struct ActionUseData
{
    public bool Shifting;
    public byte LocalActionIndex;
    public int SelectionKey;
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
    public float updateInterval = 0.1f;

    private EntityManager entityManager;
    private EntityQuery query;
    private EntityQuery visualizeQuery;
    private EntityGUIManifest manifest;
    private EntityData curGUIData;
    private Camera cam;

    private int lastSelectedKey = -1;
    private int lastSelectedCount = 0;

    private UnityEngine.Coroutine updateCoroutine;

    public static Action<ActionUseData> OnAction;
    public static event Action<ConstructData> VisualizeStructure;
    public static event Action CancelStructure;

    #endregion

    #region Unity Lifecycle

    public override void OnLoad()
    {
        manifest = FindFirstObjectByType<EntityGUIManifest>();

        World world = ClientServerBootstrap.ClientWorld;
        entityManager = world.EntityManager;
        query = entityManager.CreateEntityQuery(typeof(LocalSelectedUnits));
        visualizeQuery = entityManager.CreateEntityQuery(typeof(ActionVisualizationData));

        InputBridge.OnUpdateGUI += OnUpdateGUI;
        UnitGUIActionElement.OnAction += OnElementAction;

        if (GameLoadConfig.InReplayMode)
        {
            this.enabled = false;
            return;
        }

        cam = Camera.main;
        visualizeQuery.SetSingleton(new ActionVisualizationData
        {
            Data = new ActionUseData
            {
                Shifting = false,
                LocalActionIndex = 0,
                SelectionKey = -1,
                RayOrigin = float3.zero,
                RayDirection = float3.zero,
            }
        });

        updateCoroutine = StartCoroutine(ContinuousUpdateCoroutine());
    }

    private void OnDestroy()
    {
        InputBridge.OnUpdateGUI -= OnUpdateGUI;
        UnitGUIActionElement.OnAction -= OnElementAction;

        if (updateCoroutine != null)
            StopCoroutine(updateCoroutine);
    }

    #endregion

    #region Action Handling

    public void OnElementAction(byte localActionIndex)
    {
        ActionInfo info = curGUIData.actions[localActionIndex];
        bool isShifting = Input.GetKey(KeyCode.LeftShift);

        var data = new ActionUseData
        {
            Shifting = isShifting,
            LocalActionIndex = localActionIndex,
            SelectionKey = curGUIData.selectionKey,
            RayOrigin = float3.zero,
            RayDirection = float3.zero
        };

        switch (info.InteractionType)
        {
            case InteractionType.Target:
                Cursor.SetCursor(cursors[0], cursorOffset, CursorMode.Auto);
                StartCoroutine(TargetAction(data, curGUIData));
                break;

            case InteractionType.Instant:
                OnAction?.Invoke(data);
                break;
        }
    }

    #endregion

    #region Target Action Coroutine

    System.Collections.IEnumerator TargetAction(ActionUseData data, EntityData entity)
    {
        bool done = false;
        InputData.inAction = true;

        while (!done)
        {
            if (Input.GetMouseButtonDown(0))
            {
                if (!Input.GetKey(KeyCode.LeftShift)) done = true;

                var r = cam.ScreenPointToRay(Input.mousePosition);
                data.RayOrigin = r.origin;
                data.RayDirection = r.direction;
                data.Shifting = Input.GetKey(KeyCode.LeftShift);

                visualizeQuery.SetSingleton(new ActionVisualizationData
                {
                    Data = new ActionUseData
                    {
                        Shifting = false,
                        LocalActionIndex = 0,
                        SelectionKey = -1,
                        RayOrigin = float3.zero,
                        RayDirection = float3.zero
                    }
                });

                OnAction?.Invoke(data);
            }
            else if (Input.GetKeyDown(KeyCode.Escape))
            {
                done = true;
            }

            var sr = cam.ScreenPointToRay(Input.mousePosition);
            visualizeQuery.SetSingleton(new ActionVisualizationData
            {
                Data = new ActionUseData
                {
                    Shifting = data.Shifting,
                    LocalActionIndex = data.LocalActionIndex,
                    SelectionKey = data.SelectionKey,
                    RayOrigin = sr.origin,
                    RayDirection = sr.direction,
                }
            });

            yield return null;
        }

        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        yield return new WaitForSeconds(0.2f);
        InputData.inAction = false;
        CancelStructure?.Invoke();
    }

    #endregion

    #region GUI Update

    System.Collections.IEnumerator ContinuousUpdateCoroutine()
    {
        while (true)
        {
            UpdateGUI();
            yield return new WaitForSeconds(updateInterval);
        }
    }

    public void OnUpdateGUI() => UpdateGUI();

    void UpdateGUI()
    {
        if (!query.TryGetSingleton(out LocalSelectedUnits selectedUnits))
        {
            if (lastSelectedKey != -1)
            {
                ClearAllElements();
                lastSelectedKey = -1;
                lastSelectedCount = 0;
            }
            return;
        }

        bool found = false;
        SelectedUnitBucket toDisplay = new SelectedUnitBucket { Key = 0, Count = 0 };

        foreach (var bucket in selectedUnits.Buckets)
        {
            found = true;
            toDisplay = bucket;
            break;
        }

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

        if (toDisplay.Key == lastSelectedKey && toDisplay.Count == lastSelectedCount)
            return;

        lastSelectedKey = toDisplay.Key;
        lastSelectedCount = toDisplay.Count;

        if (!manifest.TryGetData(toDisplay.Key, out EntityData data))
        {
            ClearAllElements();
            return;
        }

        if (data.actions == null)
        {
            ClearAllElements();
            return;
        }

        curGUIData = data;

        for (int i = 0; i < elements.Length; i++)
        {
            if (i < data.actions.Length)
                elements[i].SetData(data, (byte)i);
            else
                elements[i].Clear();
        }
    }

    void ClearAllElements()
    {
        foreach (var e in elements)
            e.Clear();
        curGUIData = null;
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