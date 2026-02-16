using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public struct FixedSelectionData
{
    public FixedList128Bytes<float3> Value;

    public float3 GetVertex(int index)
    {
        if (index < 0 || index >= Value.Length) return float3.zero;
        return Value[index];
    }
}
public class InputBridge : MonoBehaviour
{
    // ... (fields) ...
    public SelectionBoxVisual selectionVisual;
    private bool isDraggingLeft = false;

    public SelectionBox selectionBox;

    // Action<SelectActionData> is the signature.
    public static event Action<FixedSelectionData, int> OnSelectUnits;
    public static event Action<byte, int> OnCodeSelectUnits;
    public static event Action<MoveUnitsData, int> OnMoveUnits;
    public static event Action<int> OnClearUnits;
    public static event Action OnUpdateGUI;
    // Use Vector2 for screen positions
    private Vector2 startScreenPos;
    //private Vector2 endScreenPos;
    private Camera mainCamera;
    public Transform rig;

    public int team;
    private void Awake()
    {
        if (GameSettings.InReplayMode) { this.enabled = false; }
        mainCamera = Camera.main;
        MinimapInteraction.OnClickEvent += MinimapClick;
    }
    #region  Minimap
    public void MinimapClick(Vector3 p, int b)
    {
        //Debug.Log("Click super sigma");
        //Vector3 mousePos = Input.mousePosition;
        //Ray ray = mainCamera.ScreenPointToRay(mousePos);
        if (b == 1)
        {
            buffer.Add(InputRecordUtil.AssembleRecord(new MoveUnitsData
                {
                    Shifting = Input.GetKey(KeyCode.LeftShift),
                    RayDirection = -Vector3.up,
                    RayOrigin = p + new Vector3(0, 20f, 0),
                }, team));
            //OnMoveUnits?.Invoke();
        }
        else if (b == 0 && rig != null)
        {
            rig.transform.position = p + new Vector3(0, rig.position.y, 0);
        }
    }
    #endregion

    FixedSelectionData selectionData;
    //fix in a seccond
    #region MainLoop
    void Update()
    {
        //UnityEngine.Debug.Log($"reading input as {InputData.inAction}");
        if (UIUtility.IsPointerOverUI() || InputData.inAction) { return; }

        Vector3 mousePos = Input.mousePosition;
        UnityEngine.Ray ray = mainCamera.ScreenPointToRay(mousePos);


        if (Input.GetMouseButton(0))
        {
            selectionBox.gameObject.SetActive(true);
            selectionData = selectionBox.UpdatePerspectiveSelection(mainCamera, startScreenPos, Input.mousePosition);
        }

        //Code selection
        if (Input.GetKeyDown(KeyCode.Space))
        {
            buffer.Add(InputRecordUtil.AssembleRecord(0, team));
        }

        // LEFT CLICK DOWN (Start Drag/Single Select)
        if (Input.GetMouseButtonDown(0))
        {
            if (!Input.GetKey(KeyCode.LeftShift))
            {
                /*OnClearUnits?.Invoke(team);*/
                buffer.Add(InputRecordUtil.AssembleDatalessRecord(InputType.ClearUnits, team));
            }

            // Capture the screen position
            startScreenPos = mousePos;

            isDraggingLeft = true;
            selectionVisual?.StartSelection(mousePos);
        }
        // LEFT CLICK UP (End Drag/Box Select/Single Select)
        else if (Input.GetMouseButtonUp(0))
        {
            /*OnSelectUnits?.Invoke(selectionBox.GetColliderEntity(), verts, team);*/
            buffer.Add(InputRecordUtil.AssembleRecord(selectionData, team));
            isDraggingLeft = false;
            selectionVisual?.EndSelection();

        }
        else if (Input.GetMouseButtonDown(1))
        {
            buffer.Add(InputRecordUtil.AssembleRecord(new MoveUnitsData
            {
                Shifting = Input.GetKey(KeyCode.LeftShift),
                RayDirection = ray.direction,
                RayOrigin = ray.origin,
            }, team));
        }
        else if (isDraggingLeft)
        {
            selectionVisual?.UpdateSelection(mousePos);
        }
    }
    #endregion
    #region PlaybackBuffer
    List<InputRecord> buffer = new List<InputRecord>(16);
    //playback buffer
    private void FixedUpdate()
    {
        bool needsGUIUpdate = false;
        
        foreach (InputRecord r in buffer)
        {
            if (PlaybackInput(r))
            {
                needsGUIUpdate = true;
            }
        }
        buffer.Clear();
        
        if (needsGUIUpdate)
        {
            OnUpdateGUI.Invoke();
        }
    }

    public bool PlaybackInput(InputRecord r)
    {
        bool affectsGUI = false;
        
        switch (r.Type)
        {
            case InputType.CodeSelectUnits:
                OnCodeSelectUnits.Invoke(r.CodeSelect, r.Team);
                affectsGUI = true;
                break;
            case InputType.SelectUnits:
                selectionBox.UpdatePerspectiveSelection(r.Select);
                //Debug.Log(selectionBox.GetColliderEntity());
                OnSelectUnits.Invoke(r.Select, team);
                affectsGUI = true;
                break;
            case InputType.MoveUnits:
                OnMoveUnits?.Invoke(new MoveUnitsData
                {
                    Shifting = r.Move.Shifting,
                    RayDirection = r.Move.RayDirection,
                    RayOrigin = r.Move.RayOrigin,
                }, team);
                break;
            case InputType.ClearUnits:
                OnClearUnits?.Invoke(r.Team);
                affectsGUI = true;
                break;
        }
        
        return affectsGUI;
    }
    #endregion
}

