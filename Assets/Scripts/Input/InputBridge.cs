using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using RTS.InputLogging;

public struct FixedSelectionData
{
    public bool Shifting;
    public FixedList128Bytes<float3> Value;

    public float3 GetVertex(int index)
    {
        if (index < 0 || index >= Value.Length) return float3.zero;
        return Value[index];
    }
}

public class InputBridge : MapLoadedAccess
{
    public SelectionBoxVisual selectionVisual;
    public SelectionBox selectionBox;
    public Transform rig;

    public static event Action<FixedSelectionData> OnSelectUnits;
    public static event Action<byte> OnCodeSelectUnits;
    public static event Action<MoveUnitsData> OnMoveUnits;
    public static event Action OnClearUnits;
    public static event Action OnUpdateGUI;

    private bool isDraggingLeft = false;
    private Vector2 startScreenPos;
    private Camera mainCamera;
    private FixedSelectionData selectionData;

    public override void OnLoad()
    {
        if (GameLoadConfig.InReplayMode) { this.enabled = false; }
        mainCamera = Camera.main;
        MinimapInteraction.OnClickEvent += MinimapClick;
    }

    #region Minimap
    public void MinimapClick(Vector3 p, int b)
    {
        if (b == 1)
        {
            OnMoveUnits?.Invoke(new MoveUnitsData
            {
                Shifting = Input.GetKey(KeyCode.LeftShift),
                RayDirection = -Vector3.up,
                RayOrigin = p + new Vector3(0, 20f, 0),
            });
        }
        else if (b == 0 && rig != null)
        {
            rig.transform.position = p + new Vector3(0, rig.position.y, 0);
        }
    }
    #endregion

    #region MainLoop
    public void Update()
    {
        if (!_ready) return;
        if (UIUtility.IsPointerOverUI() || InputData.inAction) return;

        Vector3 mousePos = Input.mousePosition;
        Ray ray = mainCamera.ScreenPointToRay(mousePos);

        // Update selection box visuals while holding
        if (Input.GetMouseButton(0))
        {
            selectionBox.gameObject.SetActive(true);
            selectionData = selectionBox.UpdatePerspectiveSelection(mainCamera, startScreenPos, mousePos);
        }

        // Code selection
        if (Input.GetKeyDown(KeyCode.Space))
        {
            OnCodeSelectUnits?.Invoke(0);
            OnUpdateGUI?.Invoke();
        }

        // LEFT CLICK DOWN
        if (Input.GetMouseButtonDown(0))
        {
            if (!Input.GetKey(KeyCode.LeftShift))
            {
                OnClearUnits?.Invoke();
                OnUpdateGUI?.Invoke();
            }

            startScreenPos = mousePos;
            isDraggingLeft = true;
            selectionVisual?.StartSelection(mousePos);
        }
        // LEFT CLICK UP
        else if (Input.GetMouseButtonUp(0))
        {
            selectionData.Shifting = Input.GetKey(KeyCode.LeftShift);

            selectionBox.UpdatePerspectiveSelection(selectionData);
            OnSelectUnits?.Invoke(selectionData);
            OnUpdateGUI?.Invoke();

            isDraggingLeft = false;
            selectionVisual?.EndSelection();
        }
        // RIGHT CLICK
        else if (Input.GetMouseButtonDown(1))
        {
            OnMoveUnits?.Invoke(new MoveUnitsData
            {
                Shifting = Input.GetKey(KeyCode.LeftShift),
                RayDirection = ray.direction,
                RayOrigin = ray.origin,
            });
        }
        // DRAG UPDATE (visuals only)
        else if (isDraggingLeft)
        {
            selectionVisual?.UpdateSelection(mousePos);
        }
    }
    #endregion
}