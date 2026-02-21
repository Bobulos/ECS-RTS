using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class ExpectedPlayersDropdown : MonoBehaviour
{
    [Header("References")]
    public TMP_Dropdown dropdown;

    [Header("Settings")]
    public int defaultValue = 2; // Which number is selected by default (1-8)

    [SerializeField] private int maxValue = 8;
    void Start()
    {
        if (dropdown == null)
            dropdown = GetComponent<TMP_Dropdown>();

        PopulateDropdown();
    }

    void PopulateDropdown()
    {
        dropdown.ClearOptions();

        List<string> options = new List<string>();
        for (int i = 1; i <= 8; i++)
        {
            options.Add(i.ToString());
        }

        dropdown.AddOptions(options);

        // Set default selection (index is value - 1)
        dropdown.value = Mathf.Clamp(defaultValue - 1, 0, maxValue - 1);
        dropdown.RefreshShownValue();

        // Listen for changes
        dropdown.onValueChanged.AddListener(OnDropdownValueChanged);
    }

    void OnDropdownValueChanged(int index)
    {
        int selectedNumber = index + 1;
        //Debug.Log("Selected number: " + selectedNumber);

        // Add your logic here
        OnNumberSelected(selectedNumber);
    }

    public void OnNumberSelected(int number)
    {
        GameLoadConfig.ExpectedPlayers = number;
    }

    /// <summary>
    /// Get the currently selected number (1-8).
    /// </summary>
    public int GetSelectedNumber()
    {
        return dropdown.value + 1;
    }

    /// <summary>
    /// Set the dropdown to a specific number (1-8) from code.
    /// </summary>
    public void SetSelectedNumber(int number)
    {
        dropdown.value = Mathf.Clamp(number - 1, 0, maxValue - 1);
    }

    void OnDestroy()
    {
        if (dropdown != null)
            dropdown.onValueChanged.RemoveListener(OnDropdownValueChanged);
    }
}