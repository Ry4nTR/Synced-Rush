using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using SyncedRush.Generics;

namespace SyncedRush.UI.Settings
{
    public class KeybindAssigner : MonoBehaviour
    {
        [SerializeField] private InputActionReference actionToRebind;
        [SerializeField] private TextMeshProUGUI buttonText;
        [SerializeField] private int bindingIndex = 0;

        private InputActionRebindingExtensions.RebindingOperation rebindingOperation;

        void Start() => RefreshDisplay();

        public void StartRebinding()
        {
            if (actionToRebind == null) return;

            actionToRebind.action.Disable();

            buttonText.text = "<color=yellow>...</color>"; // Feedback visivo immediato

            rebindingOperation = actionToRebind.action.PerformInteractiveRebinding(bindingIndex)
                .WithControlsExcluding("<Mouse>/delta")
                .WithControlsExcluding("<Pointer>/position")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnMatchWaitForAnother(0.1f)
                .OnComplete(operation => FinishRebinding())
                .OnCancel(operation => FinishRebinding())
                .Start();
        }

        private void FinishRebinding()
        {
            rebindingOperation.Dispose();
            actionToRebind.action.Enable();

            Generics.SettingsManager.Instance.SaveRebinds();

            SettingsManager.Instance.LoadRebinds();

            RefreshDisplay();
        }

        public void RefreshDisplay()
        {
            if (actionToRebind != null && buttonText != null)
            {
                buttonText.text = actionToRebind.action.GetBindingDisplayString(bindingIndex);
            }
        }
    }
}