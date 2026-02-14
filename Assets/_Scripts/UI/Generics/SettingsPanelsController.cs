using UnityEngine;
using UnityEngine.Events;

namespace SyncedRush.UI.Settings
{
	public class SettingsPanelsController : MonoBehaviour
	{
		[SerializeField] GameObject generalPanel;
		[SerializeField] GameObject keybindsPanel;
		[SerializeField] GameObject crosshairPanel;
		[SerializeField] UnityEvent startEvent;

        private void Start()
        {
            startEvent?.Invoke();
        }

        public void TurnOnGeneral(bool value)
		{
			generalPanel.SetActive(value);
		}

        public void TurnOnKeybinds(bool value)
        {
			keybindsPanel.SetActive(value);
        }

        public void TurnOnCrosshair(bool value)
        {
            crosshairPanel.SetActive(value);
        }
    }
}