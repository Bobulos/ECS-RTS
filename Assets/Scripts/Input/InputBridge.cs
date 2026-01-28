using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

// ... (usings and class definition) ...

public class InputBridge : MonoBehaviour
{
    // ... (fields) ...
    public SelectionBoxVisual selectionVisual;
    private bool isDraggingLeft = false;

    public SelectionBox selectionBox;

    // Action<SelectActionData> is the signature.
    public static event Action<Entity, SelectionData, uint> OnSelectUnits;
    public static event Action<byte, uint> OnCodeSelectUnits;
    public static event Action<MoveUnitsData, uint> OnMoveUnits;
    public static event Action<uint> OnClearUnits;
    public static event Action OnUpdateGUI;
    // Use Vector2 for screen positions
    private Vector2 startScreenPos;
    //private Vector2 endScreenPos;
    private Camera mainCamera;
    public Transform rig;

    public uint team;
    private void Awake()
    {
        if (GameSettings.InReplayMode) { this.enabled = false; }
        mainCamera = Camera.main;
        MinimapInteraction.OnClickEvent += MinimapClick;
    }

    public void MinimapClick(Vector3 p, int b)
    {
        //Debug.Log("Click super sigma");
        //Vector3 mousePos = Input.mousePosition;
        //Ray ray = mainCamera.ScreenPointToRay(mousePos);
        if (b == 1)
        {
            OnMoveUnits?.Invoke(new MoveUnitsData
            {
                CurrentRayDirection = -Vector3.up,
                CurrentRayOrigin = p + new Vector3(0, 5, 0),
            }, 0);
        }
        else if (b == 0 && rig != null)
        {
            rig.transform.position = p + new Vector3(0, rig.position.y, 0);
        }

    }


    SelectionData selectionData;
    //fix in a seccond
    void Update()
    {
        if (UIUtility.IsPointerOverUI()) { return; }

        Vector3 mousePos = Input.mousePosition;
        UnityEngine.Ray ray = mainCamera.ScreenPointToRay(mousePos);


        if (Input.GetMouseButton(0))
        {
            selectionBox.gameObject.SetActive(true);
            selectionData = selectionBox.UpdatePerspectiveSelection(mainCamera, startScreenPos, Input.mousePosition);
        }

        //Specail selection
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
                CurrentRayDirection = ray.direction,
                CurrentRayOrigin = ray.origin,
            }, team));
        }
        else if (isDraggingLeft)
        {
            selectionVisual?.UpdateSelection(mousePos);
        }
    }
    List<InputRecord> buffer = new List<InputRecord>(16);
    //playback buffer
    private void FixedUpdate()
    {
        foreach (InputRecord r in buffer)
        {
            PlaybackInput(r);
        }
        buffer.Clear();
    }
    public void PlaybackInput(InputRecord r)
    {
        switch (r.Type)
        {
            case InputType.CodeSelectUnits:
                OnCodeSelectUnits.Invoke(r.CodeSelect, r.Team);
                break;
            case InputType.SelectUnits:
                selectionBox.UpdatePerspectiveSelection(r.Select);
                OnSelectUnits.Invoke(selectionBox.GetColliderEntity(), r.Select, team);
                OnUpdateGUI.Invoke();
                break;
            case InputType.MoveUnits:
                OnMoveUnits?.Invoke(new MoveUnitsData
                {
                    CurrentRayDirection = r.Move.CurrentRayDirection,
                    CurrentRayOrigin = r.Move.CurrentRayOrigin,
                }, team);
                break;
            case InputType.ClearUnits:
                OnClearUnits?.Invoke(r.Team);
                OnUpdateGUI.Invoke();
                break;
        }
    }
}

public struct MoveUnitsData
{
    // We can keep the direction and the current ray's origin for the DOTS system 
    // to reuse in its raycast calculations.
    public float3 CurrentRayOrigin;
    public float3 CurrentRayDirection;
}