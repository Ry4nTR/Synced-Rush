using SyncedRush.Generics;
using UnityEngine;
using UnityEngine.UI;

namespace SyncedRush.UI.Settings
{
	public class KeybindResetter : MonoBehaviour
	{
		[SerializeField] private ConfirmPanelController confirmPanel;
		private Button _button;

        private void Awake()
        {
            _button = GetComponent<Button>();
            if (_button == null)
                Debug.LogError("Button non trovato!");
        }

        private void OnEnable()
        {
            _button.onClick.AddListener(AskReset);
        }
        private void OnDisable()
        {
            _button.onClick.RemoveListener(AskReset);
        }

        private void AskReset()
        {
            confirmPanel.AskConfirm(ResetKeybindings);
        }

        private void ResetKeybindings()
        {
            SettingsManager.Instance.ResetAllBindings();
        }
    }
}