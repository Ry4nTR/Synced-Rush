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

        void Start()
        {
            if (slider == null) { Debug.LogError("Slider non assegnato!"); return; }

            float savedValue = SettingsManager.Instance.GetAnyFloat(optionKeyName);

            // Usiamo una variabile temporanea per non triggerare l'evento durante il setup se non serve
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
            SettingsManager.Instance.SaveAnyFloat(optionKeyName, value);

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