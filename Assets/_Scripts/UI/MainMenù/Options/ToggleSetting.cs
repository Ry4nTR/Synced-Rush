using SyncedRush.Generics;
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

        void Start()
        {
            if (toggle == null) { Debug.LogError("Toggle non assegnato!"); return; }

            float savedValue = SettingsManager.Instance.GetAnyFloat(optionKeyName);

            // Usiamo una variabile temporanea per non triggerare l'evento durante il setup se non serve
            toggle.isOn = savedValue == 1f;
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
            SettingsManager.Instance.SaveAnyFloat(optionKeyName, floatValue);

            onValueChange.Invoke(floatValue);
        }
    }
}