using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UnitGUIElement : MonoBehaviour
{
    public RawImage image;
    public TextMeshProUGUI text;

    public void SetData(EntityData d, int count)
    {
        image.texture = d.icon;
        text.text = count.ToString();
    }
}
