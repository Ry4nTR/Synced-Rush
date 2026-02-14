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

        int savedW = PlayerPrefs.GetInt("Settings_ResWidth", Screen.currentResolution.width);
        int savedH = PlayerPrefs.GetInt("Settings_ResHeight", Screen.currentResolution.height);

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

        dropdown.onValueChanged.AddListener(index => {
            string[] parts = dropdown.options[index].text.Split('x');
            if (parts.Length == 2)
            {
                int w = int.Parse(parts[0].Trim());
                int h = int.Parse(parts[1].Trim());

                SettingsManager.Instance.SaveResolution(w, h);

                PlayerPrefs.SetInt("Settings_ResWidth", w);
                PlayerPrefs.SetInt("Settings_ResHeight", h);
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