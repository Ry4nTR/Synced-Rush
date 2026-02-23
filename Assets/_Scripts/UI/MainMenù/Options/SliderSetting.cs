using SyncedRush.Generics;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SyncedRush.UI.Settings
{
    public class SliderSetting : MonoBehaviour
    {
        [SerializeField] private Slider slider;
        [SerializeField] private TextMeshProUGUI textValue;
        [SerializeField] private string optionKeyName;
        [SerializeField] private string valueFormat = "F0"; // "F0" = intero, "F2" = 2 decimali
        [SerializeField] private UnityEvent<float> onValueChange;

        // Mapping from legacy key names (used by previous PlayerPrefs-based
        // implementation) to the new FloatSettingKey enums. This allows old
        // OptionPanel prefabs to continue working without renaming their
        // optionKeyName fields. Add entries here when introducing new
        // settings or when migrating additional keys.
        private static readonly System.Collections.Generic.Dictionary<string, FloatSettingKey> floatKeyMap = new System.Collections.Generic.Dictionary<string, FloatSettingKey>
        {
            { "Sens", FloatSettingKey.Sensitivity },
            { "Sensitivity", FloatSettingKey.Sensitivity },
            { "FOV", FloatSettingKey.WorldFov },
            { "WorldFOV", FloatSettingKey.WorldFov },
            { "ViewmodelFOV", FloatSettingKey.ViewmodelFov },
            { "Audio", FloatSettingKey.MasterVolume },
            { "Crosshair_LineLength", FloatSettingKey.CrosshairLineLength },
            { "Crosshair_Thickness", FloatSettingKey.CrosshairThickness },
            { "Crosshair_Gap", FloatSettingKey.CrosshairGap },
            { "Crosshair_DotSize", FloatSettingKey.CrosshairDotSize },
            { "Crosshair_Smooth", FloatSettingKey.CrosshairSmoothTime },
            { "Crosshair_Opacity", FloatSettingKey.CrosshairOpacity }
        };

        void Start()
        {
            if (slider == null)
            {
                Debug.LogError("Slider non assegnato!");
                return;
            }

            // Attempt to map the optionKeyName to the new FloatSettingKey enum. If
            // parsing fails, fall back to zero.
            float savedValue = 0f;
            FloatSettingKey parsedKey;
            if (!string.IsNullOrEmpty(optionKeyName))
            {
                // Use mapping for legacy names first, then try direct parse
                if (!floatKeyMap.TryGetValue(optionKeyName, out parsedKey) &&
                    !System.Enum.TryParse(optionKeyName, out parsedKey))
                {
                    parsedKey = default;
                }
                savedValue = SettingsManager.Instance.GetFloat(parsedKey);
            }
            slider.value = savedValue;
            UpdateUI(savedValue);
        }

        private void OnEnable()
        {
            slider.onValueChanged.AddListener(HandleValueChanged);
        }

        private void OnDisable()
        {
            slider.onValueChanged.RemoveListener(HandleValueChanged);
        }

        private void HandleValueChanged(float value)
        {
            FloatSettingKey parsedKey;
            if (!string.IsNullOrEmpty(optionKeyName))
            {
                if (!floatKeyMap.TryGetValue(optionKeyName, out parsedKey) &&
                    !System.Enum.TryParse(optionKeyName, out parsedKey))
                {
                    parsedKey = default;
                }
                SettingsManager.Instance.SetFloat(parsedKey, value);
            }
            UpdateUI(value);
            onValueChange.Invoke(value);
        }

        private void UpdateUI(float value)
        {
            if (textValue != null)
                textValue.text = value.ToString(valueFormat);
        }
    }
}