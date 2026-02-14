using System;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class UnitGUIActionElement : MonoBehaviour
{
    public Texture2D defaultTex;
    public RawImage image;
    public TextMeshProUGUI text;

    public static Action<byte> OnAction;

    private EntityData data;
    private byte actionIndex;
    private KeyCode hotKeyCode;
    public void OnClick()
    {
        if (data == null) return;
        OnAction?.Invoke(actionIndex);
    }
    private void Update()
    {
        if (hotKeyCode != KeyCode.None && 
        Input.GetKeyDown(hotKeyCode))
        {
            OnClick();
        }
    }
    public void SetData(EntityData d, byte index)
    {
        int i = (int)index;
        hotKeyCode = d.hotKeys[i];
        data = d;
        actionIndex = index;
        if (i < d.visuals.Length 
        && d.visuals[i] != null)
        {
            image.texture = d.visuals[i].icon;
        }
        else
        {
            //do overide
            image.texture = d.actionIcons[i];
        }
        text.text = $"{hotKeyCode}";
    }
    public void Clear()
    {
        text.text = "";
        data = null;
        actionIndex = 0;
        image.texture = defaultTex;
    }
}
