using SyncedRush.Generics;
using SyncedRush.UI.Settings;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SyncedRush.UI.Settings
{
    public class ToggleSetting : MonoBehaviour
    {
        [SerializeField] private Toggle toggle;
        [SerializeField] private string optionKeyName;
        [SerializeField] private UnityEvent<float> onValueChange;

        // Mapping from legacy boolean keys to new BoolSettingKey values. This
        // allows existing OptionPanel prefabs to function without renaming
        // their optionKeyName fields. Extend this dictionary when adding
        // additional bool settings.
        private static readonly System.Collections.Generic.Dictionary<string, BoolSettingKey> boolKeyMap = new System.Collections.Generic.Dictionary<string, BoolSettingKey>
        {
            { "InvertY", BoolSettingKey.InvertY },
            { "Crosshair_ShowDot", BoolSettingKey.CrosshairShowDot }
        };

        void Start()
        {
            if (toggle == null)
            {
                Debug.LogError("Toggle non assegnato!");
                return;
            }
            bool savedValue = false;
            if (!string.IsNullOrEmpty(optionKeyName))
            {
                BoolSettingKey parsedKey;
                if (!boolKeyMap.TryGetValue(optionKeyName, out parsedKey) &&
                    !System.Enum.TryParse(optionKeyName, out parsedKey))
                {
                    parsedKey = default;
                }
                savedValue = SettingsManager.Instance.GetBool(parsedKey);
            }
            toggle.isOn = savedValue;
        }

        private void OnEnable()
        {
            toggle.onValueChanged.AddListener(HandleValueChanged);
        }

        private void OnDisable()
        {
            toggle.onValueChanged.RemoveListener(HandleValueChanged);
        }

        private void HandleValueChanged(bool value)
        {
            float floatValue = value ? 1f : 0f;
            if (!string.IsNullOrEmpty(optionKeyName))
            {
                BoolSettingKey parsedKey;
                if (!boolKeyMap.TryGetValue(optionKeyName, out parsedKey) &&
                    !System.Enum.TryParse(optionKeyName, out parsedKey))
                {
                    parsedKey = default;
                }
                SettingsManager.Instance.SetBool(parsedKey, value);
            }
            onValueChange.Invoke(floatValue);
        }
    }
}