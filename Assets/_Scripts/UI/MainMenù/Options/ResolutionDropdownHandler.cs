using SyncedRush.Generics;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class ResolutionDropdownHandler : MonoBehaviour
{
    private TMP_Dropdown dropdown;

    void Awake()
    {
        dropdown = GetComponent<TMP_Dropdown>();
    }

    void Start()
    {
        PopulateOptions();

        // Determine which option matches the saved resolution stored in SettingsManager
        var sm = SyncedRush.Generics.SettingsManager.Instance;
        int savedW = sm != null ? sm.ResolutionWidth : Screen.currentResolution.width;
        int savedH = sm != null ? sm.ResolutionHeight : Screen.currentResolution.height;
        string targetResText = $"{savedW} x {savedH}";
        int targetIndex = 0;
        for (int i = 0; i < dropdown.options.Count; i++)
        {
            if (dropdown.options[i].text == targetResText)
            {
                targetIndex = i;
                break;
            }
        }
        dropdown.value = targetIndex;
        dropdown.RefreshShownValue();
    }

    void OnEnable()
    {
        if (dropdown == null) dropdown = GetComponent<TMP_Dropdown>();

        dropdown.onValueChanged.AddListener(index =>
        {
            string[] parts = dropdown.options[index].text.Split('x');
            if (parts.Length == 2)
            {
                int w = int.Parse(parts[0].Trim());
                int h = int.Parse(parts[1].Trim());
                var sm = SyncedRush.Generics.SettingsManager.Instance;
                if (sm != null)
                    sm.SetResolution(w, h);
            }
        });
    }

    void OnDisable()
    {
        if (dropdown != null)
            dropdown.onValueChanged.RemoveAllListeners();
    }

    void PopulateOptions()
    {
        dropdown.ClearOptions();
        List<string> options = new();

        Resolution[] rawRes = SettingsManager.Instance.Resolutions;
        List<Resolution> uniqueResolutions = new();

        for (int i = 0; i < rawRes.Length; i++)
        {
            bool isDuplicate = false;
            foreach (var res in uniqueResolutions)
            {
                if (res.width == rawRes[i].width && res.height == rawRes[i].height)
                {
                    isDuplicate = true;
                    break;
                }
            }

            if (!isDuplicate)
            {
                uniqueResolutions.Add(rawRes[i]);
                options.Add($"{rawRes[i].width} x {rawRes[i].height}");
            }
        }

        options.Reverse();
        dropdown.AddOptions(options);
    }
}