using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UnitGUIElement : MonoBehaviour
{
    public Image image;
    public TextMeshProUGUI text;

    public void SetData(EntityData d, int count)
    {
        image.sprite = d.icon;
        text.text = count.ToString();
    }
}
