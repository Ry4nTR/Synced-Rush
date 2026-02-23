using SyncedRush.Generics;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SyncedRush.UI.Settings
{
    /// <summary>
    /// Binds a UI toggle to a boolean setting defined in <see cref="BoolSettingKey"/>. When the
    /// toggle value changes the SettingsManager is updated and persisted. A
    /// UnityEvent can be invoked with the new float value (1 for true,
    /// 0 for false) for additional side effects.
    /// </summary>
    public class BoolSettingToggleBinder : MonoBehaviour
    {
        [SerializeField] private Toggle toggle;
        [SerializeField] private BoolSettingKey settingKey;
        [SerializeField] private UnityEvent<float> onValueChange;

        private void Start()
        {
            if (toggle == null)
            {
                Debug.LogError("BoolSettingToggleBinder: Toggle not assigned!");
                return;
            }
            bool savedValue = SettingsManager.Instance != null ?
                SettingsManager.Instance.GetBool(settingKey) : false;
            toggle.isOn = savedValue;
        }

        private void OnEnable()
        {
            if (toggle != null)
                toggle.onValueChanged.AddListener(HandleValueChanged);
        }

        private void OnDisable()
        {
            if (toggle != null)
                toggle.onValueChanged.RemoveListener(HandleValueChanged);
        }

        private void HandleValueChanged(bool value)
        {
            var sm = SettingsManager.Instance;
            if (sm != null)
            {
                sm.SetBool(settingKey, value);
                onValueChange?.Invoke(value ? 1f : 0f);
            }
        }
    }
}