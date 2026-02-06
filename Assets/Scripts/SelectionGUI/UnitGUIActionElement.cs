using System;
using TMPro;
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
    public void OnClick()
    {
        if (data == null) return;
        OnAction?.Invoke(actionIndex);
    }
    public void SetData(EntityData d, byte index)
    {
        data = d;
        actionIndex = index;
        image.texture = d.actionIcons[(int)index];
    }
    public void Clear()
    {
        data = null;
        actionIndex = 0;
        image.texture = defaultTex;
    }
}
