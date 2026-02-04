using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UnitGUIActionElement : MonoBehaviour
{
    public Sprite defaultSprite;
    public Image image;
    public TextMeshProUGUI text;

    public static Action<byte> OnAction;

    private UnitGUIData data;
    private byte actionIndex;
    public void OnClick()
    {
        if (data == null) return;
        OnAction?.Invoke(actionIndex);
    }
    public void SetData(UnitGUIData d, byte index)
    {
        data = d;
        actionIndex = index;
        image.sprite = d.actionIcons[(int)index];
    }
    public void Clear()
    {
        data = null;
        actionIndex = 0;
        image.sprite = defaultSprite;
    }
}
